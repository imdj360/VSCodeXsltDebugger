# CLI Transform Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `xslt.runTransform` VS Code command that runs an XSLT transform headlessly and shows trace output in the Output panel, enabling Claude Code post-edit hooks to auto-detect and fix errors.

**Architecture:** A new `CliTransformRunner` C# class consumes the existing engine event system (`EngineOutput`, `EngineTerminated`) and routes output to stdout/stderr instead of DAP. `XsltEngineManager` gains an `OutputWriter` property so engines can write transform results to stdout. A new `xslt.runTransform` VS Code command in `extension.ts` spawns the adapter with `--transform` and pipes its output to an Output panel.

**Tech Stack:** C# / .NET 8, xUnit + FluentAssertions (tests), TypeScript / VS Code API (`child_process.spawn`, `OutputChannel`)

---

## File Map

**Create:**
- `XsltDebugger.DebugAdapter/CliTransformRunner.cs` — headless transform lifecycle
- `XsltDebugger.Tests/CliTransformRunnerTests.cs` — xUnit tests for all CLI scenarios

**Modify:**
- `XsltDebugger.DebugAdapter/XsltEngineManager.cs` — add `OutputWriter` property
- `XsltDebugger.DebugAdapter/XsltCompiledEngine.cs` — write to `OutputWriter` when set
- `XsltDebugger.DebugAdapter/SaxonEngine.cs` — write to `OutputWriter` when set
- `XsltDebugger.DebugAdapter/Program.cs` — add `--transform` branch at top of `Main()`
- `src/extension.ts` — register `xslt.runTransform` command
- `package.json` — declare command and activation event

---

## Task 1: Add `OutputWriter` to `XsltEngineManager`

**Files:**
- Modify: `XsltDebugger.DebugAdapter/XsltEngineManager.cs`

- [ ] **Step 1: Add the property**

Open `XsltEngineManager.cs`. After the `StylesheetNamespaces` property (line 22), add:

```csharp
/// <summary>
/// When set, engines write transform result output here instead of the default file path.
/// Used by CliTransformRunner to route output to stdout.
/// </summary>
public static TextWriter? OutputWriter { get; set; }
```

Also add it to the `Reset()` method (after `ClearStylesheetNamespaces()` call at the bottom):

```csharp
OutputWriter = null;
```

- [ ] **Step 2: Build to verify no compile errors**

```bash
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add XsltDebugger.DebugAdapter/XsltEngineManager.cs
git commit -m "feat: add OutputWriter property to XsltEngineManager"
```

---

## Task 2: Wire `OutputWriter` in `XsltCompiledEngine`

**Files:**
- Modify: `XsltDebugger.DebugAdapter/XsltCompiledEngine.cs` (around lines 209–224)

- [ ] **Step 1: Replace the output writing block**

Find this block (lines ~211–224):

```csharp
if (XsltEngineManager.IsLogEnabled)
{
    XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
}
if (string.IsNullOrWhiteSpace(outPath))
{
    XsltEngineManager.NotifyOutput("Output path for transform is empty; skipping write.");
}
else
{
    using var fs = File.Create(outPath);
    using var writer = XmlWriter.Create(fs, xslt.OutputSettings ?? new XmlWriterSettings { Indent = true });
    xslt.Transform(xmlReader, args, writer);
}
```

Replace with:

```csharp
if (XsltEngineManager.OutputWriter != null)
{
    // CLI mode: write result to the provided TextWriter (e.g. Console.Out)
    if (XsltEngineManager.IsLogEnabled)
    {
        XsltEngineManager.NotifyOutput("[log] Writing transform output to stdout.");
    }
    var xmlWriterSettings = xslt.OutputSettings ?? new XmlWriterSettings { Indent = true };
    xmlWriterSettings = xmlWriterSettings.Clone();
    xmlWriterSettings.CloseOutput = false;
    using var xmlWriter = XmlWriter.Create(XsltEngineManager.OutputWriter, xmlWriterSettings);
    xslt.Transform(xmlReader, args, xmlWriter);
}
else
{
    if (XsltEngineManager.IsLogEnabled)
    {
        XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
    }
    using var fs = File.Create(outPath);
    using var writer = XmlWriter.Create(fs, xslt.OutputSettings ?? new XmlWriterSettings { Indent = true });
    xslt.Transform(xmlReader, args, writer);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add XsltDebugger.DebugAdapter/XsltCompiledEngine.cs
git commit -m "feat: compiled engine writes to OutputWriter when set"
```

---

## Task 3: Wire `OutputWriter` in `SaxonEngine`

**Files:**
- Modify: `XsltDebugger.DebugAdapter/SaxonEngine.cs` (around lines 267–280)

- [ ] **Step 1: Replace the output writing block**

Find this block (lines ~267–280):

```csharp
if (XsltEngineManager.IsLogEnabled)
{
    XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
}

using (var writer = new StreamWriter(outPath))
{
    var serializer = _processor.NewSerializer(writer);
    if (XsltEngineManager.TraceEnabled)
    {
        XsltEngineManager.NotifyOutput("[trace] Saxon.Start: run()");
    }
    _transformer.Run(serializer);
}
```

Replace with:

```csharp
if (XsltEngineManager.OutputWriter != null)
{
    // CLI mode: write result to the provided TextWriter (e.g. Console.Out)
    if (XsltEngineManager.IsLogEnabled)
    {
        XsltEngineManager.NotifyOutput("[log] Writing transform output to stdout.");
    }
    if (XsltEngineManager.TraceEnabled)
    {
        XsltEngineManager.NotifyOutput("[trace] Saxon.Start: run()");
    }
    var serializer = _processor.NewSerializer(XsltEngineManager.OutputWriter);
    _transformer.Run(serializer);
}
else
{
    if (XsltEngineManager.IsLogEnabled)
    {
        XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
    }
    using var writer = new StreamWriter(outPath);
    var serializer = _processor.NewSerializer(writer);
    if (XsltEngineManager.TraceEnabled)
    {
        XsltEngineManager.NotifyOutput("[trace] Saxon.Start: run()");
    }
    _transformer.Run(serializer);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add XsltDebugger.DebugAdapter/SaxonEngine.cs
git commit -m "feat: Saxon engine writes to OutputWriter when set"
```

---

## Task 4: Write `CliTransformRunner` tests first (TDD)

**Files:**
- Create: `XsltDebugger.Tests/CliTransformRunnerTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

public class CliTransformRunnerTests
{
    private static string GetRepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string TestData(string relative) =>
        Path.GetFullPath(Path.Combine(GetRepoRoot(), "TestData", relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public async Task RunAsync_CompiledEngine_WritesOutputToWriter_ReturnsZero()
    {
        var stylesheet = TestData("Integration/xslt/compiled/sample.xslt");
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--engine", "compiled", "--stylesheet", stylesheet, "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(0);
        writer.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAsync_SaxonEngine_WritesOutputToWriter_ReturnsZero()
    {
        var stylesheet = TestData("Integration/xslt/saxon/sample-xslt3.xslt");
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--engine", "saxonnet", "--stylesheet", stylesheet, "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(0);
        writer.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAsync_MissingStylesheet_ReturnsExitCode3()
    {
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--stylesheet", "/nonexistent/file.xslt", "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_MissingXmlFile_ReturnsExitCode3()
    {
        var stylesheet = TestData("Integration/xslt/compiled/sample.xslt");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--stylesheet", stylesheet, "--xml", "/nonexistent/input.xml" };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_InvalidEngine_ReturnsExitCode2()
    {
        var stylesheet = TestData("Integration/xslt/compiled/sample.xslt");
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--engine", "unknownengine", "--stylesheet", stylesheet, "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_BadXslt_ReturnsExitCode1()
    {
        // Write a temp bad XSLT file
        var badXslt = Path.GetTempFileName() + ".xslt";
        var xml = TestData("Integration/xml/sample.xml");
        await File.WriteAllTextAsync(badXslt, "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><INVALID>");
        var writer = new StringWriter();

        try
        {
            var args = new[] { "--transform", "--engine", "compiled", "--stylesheet", badXslt, "--xml", xml };
            var runner = new CliTransformRunner(args, writer);
            var exitCode = await runner.RunAsync();

            exitCode.Should().Be(1);
        }
        finally
        {
            File.Delete(badXslt);
        }
    }

    [Fact]
    public async Task RunAsync_LaunchConfig_ReadsConfigAndRuns()
    {
        var repoRoot = GetRepoRoot();
        var launchJson = Path.Combine(repoRoot, ".vscode", "launch.json");
        var writer = new StringWriter();

        // Use the first config from launch.json that has compiled engine
        var args = new[] { "--transform", "--launch-config", launchJson, "--config-name", "Debug XSLT Compiled Sample" };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(0);
        writer.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure (CliTransformRunner doesn't exist yet)**

```bash
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj --filter "FullyQualifiedName~CliTransformRunnerTests" -v minimal 2>&1 | head -30
```

Expected: Build error — `The type or namespace name 'CliTransformRunner' could not be found`

- [ ] **Step 3: Commit the failing tests**

```bash
git add XsltDebugger.Tests/CliTransformRunnerTests.cs
git commit -m "test: add failing tests for CliTransformRunner"
```

---

## Task 5: Implement `CliTransformRunner`

**Files:**
- Create: `XsltDebugger.DebugAdapter/CliTransformRunner.cs`

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
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
        XsltEngineManager.OutputWriter = _outputWriter;

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

            await _outputWriter.FlushAsync().ConfigureAwait(false);
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
                    else { i++; }
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
```

- [ ] **Step 2: Build**

```bash
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Run tests (expect most to pass, skip launch.json test for now)**

```bash
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj \
  --filter "FullyQualifiedName~CliTransformRunnerTests&FullyQualifiedName!~LaunchConfig" -v minimal
```

Expected: 5 tests pass, 0 fail

- [ ] **Step 4: Commit**

```bash
git add XsltDebugger.DebugAdapter/CliTransformRunner.cs
git commit -m "feat: implement CliTransformRunner for headless XSLT execution"
```

---

## Task 6: Wire `--transform` branch in `Program.cs`

**Files:**
- Modify: `XsltDebugger.DebugAdapter/Program.cs`

- [ ] **Step 1: Add the branch**

In `Program.cs`, after the existing `--test-engine` block (after line 19), add:

```csharp
if (args.Contains("--transform", StringComparer.OrdinalIgnoreCase))
{
    return await new CliTransformRunner(args).RunAsync().ConfigureAwait(false);
}
```

The top of `Main()` should now look like:

```csharp
private static async Task<int> Main(string[] args)
{
    if (args.Length > 0 && string.Equals(args[0], "--test-engine", StringComparison.OrdinalIgnoreCase))
    {
        var options = ParseTestEngineArgs(args.Skip(1).ToArray());
        await RunEngineTest(options).ConfigureAwait(false);
        return 0;
    }

    if (args.Contains("--transform", StringComparer.OrdinalIgnoreCase))
    {
        return await new CliTransformRunner(args).RunAsync().ConfigureAwait(false);
    }

    using var input = Console.OpenStandardInput();
    // ... rest unchanged
```

- [ ] **Step 2: Build and verify existing tests still pass**

```bash
dotnet build XsltDebugger.DebugAdapter/XsltDebugger.DebugAdapter.csproj && \
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal 2>&1 | tail -10
```

Expected: all existing tests pass

- [ ] **Step 3: Commit**

```bash
git add XsltDebugger.DebugAdapter/Program.cs
git commit -m "feat: add --transform branch to Program.cs entry point"
```

---

## Task 7: Add `xslt.runTransform` command to `package.json`

**Files:**
- Modify: `package.json`

- [ ] **Step 1: Add command contribution**

In `package.json`, inside `"contributes"`, add a `"commands"` array after the `"breakpoints"` array:

```json
"commands": [
  {
    "command": "xslt.runTransform",
    "title": "XSLT: Run Transform"
  }
],
"menus": {
  "editor/title/run": [
    {
      "command": "xslt.runTransform",
      "when": "resourceExtname == .xslt || resourceExtname == .xsl"
    }
  ]
}
```

Also add the activation event so the extension loads when the command is invoked. In `"activationEvents"`, add:

```json
"onCommand:xslt.runTransform"
```

- [ ] **Step 2: Verify JSON is valid**

```bash
node -e "require('./package.json'); console.log('OK')"
```

Expected: `OK`

- [ ] **Step 3: Commit**

```bash
git add package.json
git commit -m "feat: register xslt.runTransform command in package.json"
```

---

## Task 8: Implement `xslt.runTransform` command in `extension.ts`

**Files:**
- Modify: `src/extension.ts`

- [ ] **Step 1: Add the `child_process` import at the top**

After the existing imports, add:

```typescript
import { spawn } from 'child_process';
```

- [ ] **Step 2: Add the Output channel for transforms**

After the existing `output` channel declaration (line 5), add:

```typescript
let transformOutput: vscode.OutputChannel | undefined;

function getTransformOutput(): vscode.OutputChannel {
	if (!transformOutput) {
		transformOutput = vscode.window.createOutputChannel('XSLT Transform');
	}
	return transformOutput;
}
```

- [ ] **Step 3: Add the `runTransform` command handler function**

Add this function before the `activate` function:

```typescript
async function runTransform(
	context: vscode.ExtensionContext,
	adapterLocator: () => string | undefined
): Promise<void> {
	// Resolve stylesheet
	const editor = vscode.window.activeTextEditor;
	let stylesheetPath: string | undefined;
	if (editor && /\.(xslt|xsl)$/i.test(editor.document.fileName)) {
		stylesheetPath = editor.document.fileName;
	} else {
		const picked = await vscode.window.showOpenDialog({
			canSelectMany: false,
			filters: { 'XSLT Stylesheets': ['xslt', 'xsl'] },
			title: 'Select XSLT Stylesheet'
		});
		stylesheetPath = picked?.[0]?.fsPath;
	}
	if (!stylesheetPath) {
		return;
	}

	// Resolve XML input — try <stem>-input.xml or <stem>.xml alongside the stylesheet
	const stem = stylesheetPath.replace(/\.(xslt|xsl)$/i, '');
	const candidates = [`${stem}-input.xml`, `${stem}.xml`].filter(p => fs.existsSync(p));
	let xmlPath: string | undefined = candidates[0];
	if (!xmlPath) {
		const picked = await vscode.window.showOpenDialog({
			canSelectMany: false,
			filters: { 'XML Documents': ['xml'] },
			title: 'Select Input XML Document'
		});
		xmlPath = picked?.[0]?.fsPath;
	}
	if (!xmlPath) {
		return;
	}

	// Locate adapter DLL
	const adapterDll = adapterLocator();
	if (!adapterDll) {
		return; // adapterLocator already shows an error message
	}

	const channel = getTransformOutput();
	channel.clear();
	channel.show(true);
	channel.appendLine(`[xslt] Running transform: ${path.basename(stylesheetPath)}`);
	channel.appendLine(`[xslt] Input: ${xmlPath}`);
	channel.appendLine('');

	const proc = spawn('dotnet', [
		adapterDll,
		'--transform',
		'--stylesheet', stylesheetPath,
		'--xml', xmlPath,
		'--log-level', 'trace'
	], { cwd: path.dirname(adapterDll) });

	proc.stdout.on('data', (chunk: Buffer) => channel.append(chunk.toString()));
	proc.stderr.on('data', (chunk: Buffer) => channel.append(chunk.toString()));

	proc.on('close', (code: number | null) => {
		channel.appendLine('');
		if (code === 0) {
			channel.appendLine('[xslt] Transform succeeded.');
		} else {
			channel.appendLine(`[xslt] Transform failed (exit code ${code ?? 'unknown'}).`);
		}
	});
}
```

- [ ] **Step 4: Register the command in `activate()`**

In the `activate` function, after the existing registrations, add:

```typescript
const factory = new XsltDebugAdapterDescriptorFactory(context);

const runTransformCommand = vscode.commands.registerCommand('xslt.runTransform', () =>
	runTransform(context, () => (factory as any).locateAdapter())
);

context.subscriptions.push(configRegistration, factoryRegistration, factory, runTransformCommand);
```

Note: `locateAdapter()` is currently `private`. Change it to `public` in `XsltDebugAdapterDescriptorFactory`:

```typescript
public locateAdapter(): string | undefined {
```

- [ ] **Step 5: Compile TypeScript**

```bash
npm run compile
```

Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add src/extension.ts
git commit -m "feat: add xslt.runTransform VS Code command"
```

---

## Task 9: Run all tests and verify nothing broken

- [ ] **Step 1: Run all .NET tests**

```bash
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal
```

Expected: All existing tests pass + new `CliTransformRunnerTests` pass (except `LaunchConfig` test — see below)

- [ ] **Step 2: Check which launch.json config name to use for the test**

```bash
cat .vscode/launch.json | grep '"name"'
```

Update `CliTransformRunnerTests.RunAsync_LaunchConfig_ReadsConfigAndRuns` to use the exact name shown. Then run:

```bash
dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj \
  --filter "FullyQualifiedName~CliTransformRunnerTests" -v minimal
```

Expected: All 6 `CliTransformRunnerTests` pass

- [ ] **Step 3: Build TypeScript**

```bash
npm run compile
```

Expected: No errors

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "feat: CLI transform mode complete — all tests passing"
```

---

## Task 10: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add the new command to CLAUDE.md**

In `CLAUDE.md`, under the "Build Commands" section, add:

```markdown
# Run transform without debugging
dotnet run --project XsltDebugger.DebugAdapter -- --transform \
  --engine saxonnet \
  --stylesheet path/to/file.xslt \
  --xml path/to/input.xml \
  --log-level trace
```

Under the "Key Source Files" table, add a row:

```
| `XsltDebugger.DebugAdapter/CliTransformRunner.cs` | Headless transform mode — no DAP, result to stdout, trace to stderr |
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with CLI transform mode"
```
