using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XsltDebugger.DebugAdapter;

internal static class Program
{
    private const string AdapterName = "xslt-debugger";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--test-engine", StringComparison.OrdinalIgnoreCase))
        {
            var options = ParseTestEngineArgs(args.Skip(1).ToArray());
            await RunEngineTest(options).ConfigureAwait(false);
            return 0;
        }

        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();

        var state = new SessionState();
        var server = new DapServer(input, output, state);

        XsltEngineManager.EngineStopped += server.NotifyStopped;
        XsltEngineManager.EngineOutput += server.NotifyOutput;
        XsltEngineManager.EngineTerminated += server.NotifyTerminated;

        Console.Error.WriteLine($"[{AdapterName}] Debug adapter started.");
        await server.RunAsync().ConfigureAwait(false);
        Console.Error.WriteLine($"[{AdapterName}] Debug adapter stopped.");
        return 0;
    }

    private static async Task RunEngineTest(TestEngineOptions options)
    {
        Console.WriteLine("Starting engine test...");
        Console.WriteLine($"Engine: {options.EngineType}");
        var engine = XsltEngineFactory.CreateEngine(options.EngineType);

        var breakpointHit = new TaskCompletionSource<(string file, int line, DebugStopReason reason)>();
        void OnEngineStopped(string file, int line, DebugStopReason reason)
        {
            Console.WriteLine($"Engine stopped at {file}:{line} ({reason})");
            breakpointHit.TrySetResult((file, line, reason));
        }
        XsltEngineManager.EngineStopped += OnEngineStopped;

        try
        {
            var defaults = GetDefaults(options.EngineType);
            var sampleSheet = ResolvePath(options.Stylesheet ?? defaults.Stylesheet);
            var sampleXml = ResolvePath(options.Xml ?? defaults.Xml);
            var breakpointLine = options.BreakLine ?? defaults.BreakLine;

            Console.WriteLine($"Stylesheet: {sampleSheet}");
            Console.WriteLine($"Input XML: {sampleXml}");
            Console.WriteLine($"Breakpoint line: {breakpointLine}");

            // Break on the <xsl:value-of> instruction in the sample stylesheet.
            engine.SetBreakpoints(new[] { (sampleSheet, breakpointLine) });

            // Start the engine on a background thread so this test method can
            // continue and wait for the breakpoint without being blocked by the
            // engine's synchronous pause handling.
            var task = Task.Run(async () => await engine.StartAsync(sampleSheet, sampleXml, stopOnEntry: false));

            var stopInfo = await breakpointHit.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Console.WriteLine($"Hit breakpoint at line {stopInfo.line}. Continuing...");
            await engine.ContinueAsync().ConfigureAwait(false);
            await task.ConfigureAwait(false);
        }
        finally
        {
            XsltEngineManager.EngineStopped -= OnEngineStopped;
        }
        Console.WriteLine("Engine test completed.");
    }

    private static TestEngineOptions ParseTestEngineArgs(string[] args)
    {
        var engineType = XsltEngineType.Compiled;
        string? stylesheet = null;
        string? xml = null;
        int? breakLine = null;

        int i = 0;
        while (i < args.Length)
        {
            var token = args[i];
            if (string.Equals(token, "--engine", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && TryParseEngine(args[i + 1], out var parsed))
                {
                    engineType = parsed;
                    i += 2;
                    continue;
                }
            }
            else if (string.Equals(token, "--stylesheet", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--xslt", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    stylesheet = args[i + 1];
                    i += 2;
                    continue;
                }
            }
            else if (string.Equals(token, "--xml", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    xml = args[i + 1];
                    i += 2;
                    continue;
                }
            }
            else if (string.Equals(token, "--line", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "--break", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedLine))
                {
                    breakLine = parsedLine;
                    i += 2;
                    continue;
                }
            }
            else if (TryParseEngine(token, out var positionalEngine))
            {
                engineType = positionalEngine;
                i++;
                continue;
            }
            else if (stylesheet == null)
            {
                stylesheet = token;
                i++;
                continue;
            }
            else if (xml == null)
            {
                xml = token;
                i++;
                continue;
            }

            i++; // Skip unrecognized token
        }

        return new TestEngineOptions(engineType, stylesheet, xml, breakLine);
    }

    private static bool TryParseEngine(string value, out XsltEngineType engineType)
    {
        if (string.Equals(value, "compiled", StringComparison.OrdinalIgnoreCase))
        {
            engineType = XsltEngineType.Compiled;
            return true;
        }

        if (string.Equals(value, "saxon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "saxonnet", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "saxon-net", StringComparison.OrdinalIgnoreCase))
        {
            engineType = XsltEngineType.SaxonNet;
            return true;
        }

        engineType = default;
        return false;
    }

    private static (string Stylesheet, string Xml, int BreakLine) GetDefaults(XsltEngineType engineType)
    {
        return engineType switch
        {
            XsltEngineType.SaxonNet => ("ShipmentConf3.xslt", "ShipmentConf.xml", 26),
            _ => ("sample-inline-cs.xslt", "sample.xml", 24)
        };
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return FindSample(path);
    }

    private static string FindSample(string relative)
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        for (var i = 0; i < 6 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "sample", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return Path.GetFullPath(relative);
    }

    private readonly struct TestEngineOptions
    {
        public TestEngineOptions(XsltEngineType engineType, string? stylesheet, string? xml, int? breakLine)
        {
            EngineType = engineType;
            Stylesheet = stylesheet;
            Xml = xml;
            BreakLine = breakLine;
        }

        public XsltEngineType EngineType { get; }
        public string? Stylesheet { get; }
        public string? Xml { get; }
        public int? BreakLine { get; }
    }
}
