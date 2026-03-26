# CLI Transform Mode — Design Spec

**Date:** 2026-03-26
**Feature:** `--transform` CLI flag for headless XSLT execution
**Status:** Approved

---

## Summary

Add a `--transform` CLI mode to `XsltDebugger.DebugAdapter` that runs an XSLT transform without the DAP protocol, writes the result to stdout (or a file), writes trace/error output to stderr, and exits with a meaningful code. Enables automation workflows — Claude Code post-hooks, CI/CD pipelines, shell scripts.

---

## Architecture

### Key Insight: DAP vs Instrumentation

DAP (Debug Adapter Protocol) is only the *communication layer* between VS Code and the adapter. The engines (`XsltCompiledEngine`, `SaxonEngine`) are independent — they instrument the XSLT and fire events (`EngineOutput`, `EngineTerminated`, `EngineStopped`). `DapServer` is currently the only consumer of those events, routing them back to VS Code as DAP messages.

`CliTransformRunner` is a second consumer of the same engine events — routing them to stdout/stderr instead. No changes to the engines or `IXsltEngine` interface.

### Flow

```
Program.Main(args)
  ├── "--transform" present?
  │     YES → CliTransformRunner.RunAsync() → exit with code
  │     NO  → existing DAP server flow (unchanged)

CliTransformRunner
  ├── parse & validate args
  ├── XsltEngineFactory.Create(engine)
  ├── XsltEngineManager.SetDebugFlags(debug: false, logLevel: trace)
  ├── wire EngineOutput  → stderr
  ├── wire transform result → stdout / --output file
  ├── wire EngineTerminated → capture exit code
  ├── engine.SetBreakpoints([])   // no breakpoints — never pauses
  └── engine.StartAsync(stylesheet, xml, stopOnEntry: false)
```

---

## Components

### `CliTransformRunner` (new file)

Single responsibility: own the CLI transform lifecycle.

**Constructor:** accepts `string[] args`

**`RunAsync() → Task<int>`:**
1. Parse args (see CLI Args below)
2. If `--launch-config` provided, load and merge config (CLI args override)
3. Validate stylesheet and xml paths exist → exit 3 if not
4. Validate engine name → exit 2 if unrecognised
5. Create engine via `XsltEngineFactory`
6. `XsltEngineManager.SetDebugFlags(debug: false, logLevel: LogLevel.Trace)`
7. Subscribe to `XsltEngineManager.EngineOutput` → stderr
8. Subscribe to `XsltEngineManager.EngineTerminated` to signal completion
9. `engine.SetBreakpoints([])`
10. `engine.StartAsync(stylesheet, xml, stopOnEntry: false)`
11. Await completion, return exit code

### Engine Output Path — Required Change

Currently, when `outPath` is empty the engines skip writing output (`XsltCompiledEngine.cs:215-218`, `SaxonEngine.cs` same pattern). For CLI mode, stdout must be a valid output target.

**Fix:** Add `XsltEngineManager.OutputWriter` (`TextWriter?`) property. When set, engines write transform result to it instead of the file path. `CliTransformRunner` sets it to `Console.Out` (or a `StreamWriter` for `--output`). DAP mode never sets it — existing behaviour unchanged.

### `Program.cs` change (minimal)

```csharp
if (args.Contains("--transform"))
    return await new CliTransformRunner(args).RunAsync();

// existing DAP flow below — untouched
```

---

## CLI Arguments

| Arg | Required | Description |
|-----|----------|-------------|
| `--transform` | yes | Activates CLI mode |
| `--stylesheet <path>` | yes* | Path to XSLT file |
| `--xml <path>` | yes* | Path to input XML file |
| `--engine <name>` | no | `compiled` or `saxonnet`/`saxon` (auto-detected if omitted) |
| `--output <path>` | no | Write result to file instead of stdout |
| `--log-level <level>` | no | `none`, `log`, `trace`, `traceall` (default: `trace`) |
| `--launch-config <path>` | no | Path to `.vscode/launch.json` |
| `--config-name <name>` | no | Name of config within launch.json |

*Required unless `--launch-config` + `--config-name` are provided.

### Launch Config Support

When `--launch-config` + `--config-name` are both provided:
- Parse the JSON, find config by `name` field
- Extract `engine`, `stylesheet`, `xml`, `logLevel`
- Resolve `${workspaceFolder}` → directory containing `.vscode/`
- CLI args take precedence over launch.json values if both present

---

## Output Streams

| Stream | Content |
|--------|---------|
| stdout | Transform result (XML, HTML, or text per `xsl:output`) |
| stderr | Errors, `xsl:message` output, trace log events |

No DAP protocol output — when `--transform` is present, DAP is never initialised.

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Transform succeeded |
| 1 | Transform failed — XSLT compilation or runtime error |
| 2 | Invalid arguments (bad engine name, missing required args) |
| 3 | File not found (stylesheet or XML path does not exist) |

---

## Log Level Behaviour

| Level | What appears on stderr |
|-------|----------------------|
| `none` | Nothing (only fatal errors) |
| `log` | Started, compiled, ended events |
| `trace` | Execution flow, template entries, breakpoint positions (**default**) |
| `traceall` | Full XPath value tracking, variable values at every step |

Default is `trace` — enough context for Claude to diagnose and auto-fix errors without being noisy.

---

## Engine Selection

Same auto-detection logic as existing DAP mode:

| `--engine` value | Engine used |
|-----------------|-------------|
| `compiled` | `XsltCompiledEngine` (XSLT 1.0, `msxsl:script`) |
| `saxonnet` or `saxon` | `SaxonEngine` (XSLT 1.0/2.0/3.0) |
| omitted | Auto-detect: compiled if `msxsl:script` present, otherwise Saxon |

---

## Testing

Six new test cases in `XsltDebugger.Tests`, using existing TestData fixtures:

| Test | Engine | Validates |
|------|--------|-----------|
| Basic compiled transform | compiled | stdout has correct XML, exit 0 |
| XSLT 3.0 transform | saxonnet | stdout has correct XML, exit 0 |
| Inline C# (`msxsl:script`) | compiled | C# executes, output correct, exit 0 |
| Bad XSLT (syntax error) | either | exit 1, error message on stderr |
| Missing stylesheet file | either | exit 3 |
| Launch config mode | either | reads launch.json, resolves `${workspaceFolder}`, runs correctly |

Existing DAP tests are untouched — the `Program.cs` branch is the only shared code path.

---

## What Is Not Built

- No interactive mode
- No watch mode (hooks handle re-triggering)
- No output diffing (caller handles comparison)
- No new NuGet dependencies

---

## Claude Code Hook Integration

Once shipped, add to `.claude/settings.json` in any XSLT project:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "command": "bash -c 'FILE=\"$TOOL_INPUT_FILE\"; if [[ \"$FILE\" == *.xslt ]]; then dotnet /path/to/XsltDebugger.DebugAdapter.dll --transform --log-level trace --stylesheet \"$FILE\" --xml \"${FILE%.xslt}-input.xml\" 2>&1; fi'"
      }
    ]
  }
}
```

After every XSLT edit: transform runs, trace output surfaces on stderr, Claude reads it and auto-fixes.
