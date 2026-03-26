using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace XsltDebugger.DebugAdapter;

/// <summary>
/// Runs an XSLT transform headlessly (no DAP protocol).
/// Writes transform result to <paramref name="outputWriter"/> (defaults to Console.Out).
/// Writes trace/error messages to Console.Error.
/// </summary>
internal sealed class CliTransformRunner
{
    private readonly string[] _args;
    private readonly TextWriter _outputWriter;

    public CliTransformRunner(string[] args, TextWriter? outputWriter = null)
    {
        _args = args;
        _outputWriter = outputWriter ?? Console.Out;
    }

    public async Task<int> RunAsync()
    {
        // 1. Parse args
        if (!TryParseArgs(_args, out var options, out var error))
        {
            await Console.Error.WriteLineAsync($"ERROR: {error}").ConfigureAwait(false);
            return options?.ExitCode ?? 2;
        }

        // 2. Validate files exist
        if (!File.Exists(options!.Stylesheet))
        {
            await Console.Error.WriteLineAsync($"ERROR: Stylesheet not found: {options.Stylesheet}").ConfigureAwait(false);
            return 3;
        }
        if (!File.Exists(options.Xml))
        {
            await Console.Error.WriteLineAsync($"ERROR: XML file not found: {options.Xml}").ConfigureAwait(false);
            return 3;
        }

        // 3. Create engine
        IXsltEngine engine;
        try
        {
            engine = XsltEngineFactory.CreateEngine(options.EngineType);
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync($"ERROR: {ex.Message}").ConfigureAwait(false);
            return 2;
        }

        // 4. Configure engine manager
        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(false, options.LogLevel);

        // Route transform result to --output file or the injected writer
        TextWriter resultWriter;
        StreamWriter? fileWriter = null;
        if (options.Output != null)
        {
            var dir = Path.GetDirectoryName(options.Output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            fileWriter = new StreamWriter(options.Output, append: false);
            resultWriter = fileWriter;
            XsltEngineManager.OutputWriterDescription = options.Output;
        }
        else
        {
            resultWriter = _outputWriter;
            XsltEngineManager.OutputWriterDescription = "stdout";
        }
        XsltEngineManager.OutputWriter = resultWriter;

        // 5. Wire events
        var terminatedTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOutput(string message) => Console.Error.WriteLine(message);
        void OnTerminated(int code) => terminatedTcs.TrySetResult(code);

        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            // 6. Run — no breakpoints, no stepping
            engine.SetBreakpoints(Array.Empty<(string, int)>());
            await engine.StartAsync(options.Stylesheet, options.Xml, stopOnEntry: false).ConfigureAwait(false);

            var exitCode = await terminatedTcs.Task
                .WaitAsync(TimeSpan.FromSeconds(60))
                .ConfigureAwait(false);

            await resultWriter.FlushAsync().ConfigureAwait(false);
            if (fileWriter != null) await fileWriter.DisposeAsync().ConfigureAwait(false);
            return exitCode;
        }
        catch (TimeoutException)
        {
            await Console.Error.WriteLineAsync("ERROR: Transform timed out after 60 seconds.").ConfigureAwait(false);
            return 1;
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    private static bool TryParseArgs(string[] args, out TransformOptions? options, out string? error)
    {
        string? stylesheet = null;
        string? xml = null;
        string? output = null;
        string? launchConfig = null;
        string? configName = null;
        var engineType = XsltEngineType.Compiled;
        var logLevel = LogLevel.Trace;
        bool engineExplicit = false;

        int i = 0;
        while (i < args.Length)
        {
            var token = args[i];
            switch (token.ToLowerInvariant())
            {
                case "--transform":
                    i++; break;
                case "--stylesheet":
                case "--xslt":
                    stylesheet = i + 1 < args.Length ? args[++i] : null; i++; break;
                case "--xml":
                    xml = i + 1 < args.Length ? args[++i] : null; i++; break;
                case "--output":
                    output = i + 1 < args.Length ? args[++i] : null; i++; break;
                case "--launch-config":
                    launchConfig = i + 1 < args.Length ? args[++i] : null; i++; break;
                case "--config-name":
                    configName = i + 1 < args.Length ? args[++i] : null; i++; break;
                case "--engine":
                    if (i + 1 < args.Length && TryParseEngine(args[i + 1], out var parsed))
                    {
                        engineType = parsed;
                        engineExplicit = true;
                        i += 2;
                    }
                    else
                    {
                        error = $"Unrecognised engine: '{(i + 1 < args.Length ? args[i + 1] : "")}'";
                        options = new TransformOptions { ExitCode = 2 };
                        return false;
                    }
                    break;
                case "--log-level":
                    if (i + 1 < args.Length && TryParseLogLevel(args[i + 1], out var level))
                    {
                        logLevel = level; i += 2;
                    }
                    else
                    {
                        error = $"Unrecognised log level: '{(i + 1 < args.Length ? args[i + 1] : "")}'";
                        options = new TransformOptions { ExitCode = 2 };
                        return false;
                    }
                    break;
                default:
                    i++; break;
            }
        }

        // Merge from launch.json if provided
        if (launchConfig != null && configName != null)
        {
            if (!TryReadLaunchConfig(launchConfig, configName, out var lc, out var lcError))
            {
                error = lcError;
                options = new TransformOptions { ExitCode = 2 };
                return false;
            }
            // CLI args take precedence
            stylesheet ??= lc!.Stylesheet;
            xml ??= lc!.Xml;
            if (!engineExplicit && lc!.EngineType.HasValue) engineType = lc.EngineType.Value;
            if (lc!.LogLevel.HasValue) logLevel = lc.LogLevel.Value;
        }

        if (string.IsNullOrWhiteSpace(stylesheet))
        {
            error = "Missing required argument: --stylesheet";
            options = new TransformOptions { ExitCode = 2 };
            return false;
        }
        if (string.IsNullOrWhiteSpace(xml))
        {
            error = "Missing required argument: --xml";
            options = new TransformOptions { ExitCode = 2 };
            return false;
        }

        options = new TransformOptions
        {
            Stylesheet = stylesheet!,
            Xml = xml!,
            Output = output,
            EngineType = engineType,
            LogLevel = logLevel,
            ExitCode = 0
        };
        error = null;
        return true;
    }

    private static bool TryParseEngine(string value, out XsltEngineType engineType)
    {
        if (string.Equals(value, "compiled", StringComparison.OrdinalIgnoreCase))
        {
            engineType = XsltEngineType.Compiled; return true;
        }
        if (string.Equals(value, "saxon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "saxonnet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "saxon-net", StringComparison.OrdinalIgnoreCase))
        {
            engineType = XsltEngineType.SaxonNet; return true;
        }
        engineType = default;
        return false;
    }

    private static bool TryParseLogLevel(string value, out LogLevel level)
    {
        return value.ToLowerInvariant() switch
        {
            "none" => TrySet(out level, LogLevel.None),
            "log" => TrySet(out level, LogLevel.Log),
            "trace" => TrySet(out level, LogLevel.Trace),
            "traceall" => TrySet(out level, LogLevel.TraceAll),
            _ => Fail(out level)
        };

        static bool TrySet(out LogLevel l, LogLevel v) { l = v; return true; }
        static bool Fail(out LogLevel l) { l = default; return false; }
    }

    private static bool TryReadLaunchConfig(string launchConfigPath, string configName,
        out LaunchConfigValues? values, out string? error)
    {
        values = null;
        if (!File.Exists(launchConfigPath))
        {
            error = $"launch.json not found: {launchConfigPath}"; return false;
        }

        try
        {
            var json = File.ReadAllText(launchConfigPath);
            var doc = JsonDocument.Parse(json);
            var workspaceFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(launchConfigPath)!, ".."));

            foreach (var config in doc.RootElement.GetProperty("configurations").EnumerateArray())
            {
                if (!config.TryGetProperty("name", out var nameProp)) continue;
                if (!string.Equals(nameProp.GetString(), configName, StringComparison.OrdinalIgnoreCase)) continue;

                string? Resolve(string? raw) =>
                    raw?.Replace("${workspaceFolder}", workspaceFolder);

                string? GetStr(string key) =>
                    config.TryGetProperty(key, out var p) ? Resolve(p.GetString()) : null;

                XsltEngineType? engine = null;
                if (config.TryGetProperty("engine", out var ep) && TryParseEngine(ep.GetString() ?? "", out var et))
                    engine = et;

                LogLevel? logLevel = null;
                if (config.TryGetProperty("logLevel", out var lp) && TryParseLogLevel(lp.GetString() ?? "", out var ll))
                    logLevel = ll;

                values = new LaunchConfigValues
                {
                    Stylesheet = GetStr("stylesheet"),
                    Xml = GetStr("xml"),
                    EngineType = engine,
                    LogLevel = logLevel
                };
                error = null;
                return true;
            }

            error = $"No config named '{configName}' found in {launchConfigPath}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Failed to parse launch.json: {ex.Message}";
            return false;
        }
    }

    private sealed class TransformOptions
    {
        public string Stylesheet { get; init; } = "";
        public string Xml { get; init; } = "";
        public string? Output { get; init; }
        public XsltEngineType EngineType { get; init; } = XsltEngineType.Compiled;
        public LogLevel LogLevel { get; init; } = LogLevel.Trace;
        public int ExitCode { get; init; }
    }

    private sealed class LaunchConfigValues
    {
        public string? Stylesheet { get; init; }
        public string? Xml { get; init; }
        public XsltEngineType? EngineType { get; init; }
        public LogLevel? LogLevel { get; init; }
    }
}
