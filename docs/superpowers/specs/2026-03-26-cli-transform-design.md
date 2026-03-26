# CLI Transform Mode — Design Spec

**Date:** 2026-03-26
**Feature:** Headless XSLT transform via VS Code command + `--transform` adapter flag
**Status:** Approved

---

## Summary

Add a "Run Transform" capability that executes an XSLT transform without starting a debug session. Implemented as:

1. **`--transform` flag on the .NET adapter** — headless execution, no DAP protocol, result to stdout, trace/errors to stderr
2. **`xslt.runTransform` VS Code command** — user-facing entry point; locates the adapter DLL (reusing `locateAdapter()`), spawns it with `--transform`, and shows output in the VS Code Output panel

The adapter DLL remains an internal implementation detail — users never call it directly.

---

## Architecture

### Key Insight: DAP vs Instrumentation

DAP (Debug Adapter Protocol) is only the *communication layer* between VS Code and the adapter. The engines (`XsltCompiledEngine`, `SaxonEngine`) are independent — they instrument the XSLT and fire events (`EngineOutput`, `EngineTerminated`, `EngineStopped`). `DapServer` is currently the only consumer of those events, routing them back to VS Code as DAP messages.

`CliTransformRunner` is a second consumer of the same engine events — routing them to stdout/stderr instead. No changes to the engines or `IXsltEngine` interface.

### Flow

```
User runs "XSLT: Run Transform" command (or Claude Code hook triggers it)
       ↓
extension.ts — xslt.runTransform command handler
  ├── locateAdapter()          // reuse existing DLL finder
  ├── resolve stylesheet + xml from active editor / launch.json
  ├── spawn: dotnet adapter.dll --transform --stylesheet X --xml Y --log-level trace
  └── pipe stdout + stderr → VS Code Output panel ("XSLT Transform")
       ↓
XsltDebugger.DebugAdapter (--transform mode)
  ├── CliTransformRunner.RunAsync()
  ├── engine.StartAsync(stylesheet, xml, stopOnEntry: false)  // no breakpoints
  ├── stdout → transform result (XML/HTML/text)
  └── stderr → trace log + errors + xsl:message output
       ↓
Output panel shows combined result
Claude reads it via hook feedback → auto-fixes errors
```

---

## Components

### 1. `xslt.runTransform` VS Code Command (TypeScript — `extension.ts`)

Registered in `activate()` alongside existing debug registrations.

**Handler:**
1. Find active `.xslt` file (active editor, or prompt if none open)
2. Resolve input XML — try `${file-stem}-input.xml` alongside the stylesheet, else prompt
3. `locateAdapter()` — reuse existing function, no changes
4. Create/get Output channel `"XSLT Transform"`
5. Spawn: `dotnet <adapterDll> --transform --stylesheet <path> --xml <path> --log-level trace`
6. Pipe stdout + stderr to Output channel
7. Show Output panel, reveal on completion
8. Display exit code message: `"Transform succeeded"` or `"Transform failed (exit N)"`

**`package.json` additions:**
- `contributes.commands`: `{ "command": "xslt.runTransform", "title": "XSLT: Run Transform" }`
- Keyboard shortcut (optional): `Ctrl+Shift+T` / `Cmd+Shift+T` scoped to XSLT files

### 2. `CliTransformRunner` (new C# file — `XsltDebugger.DebugAdapter`)

Single responsibility: own the headless transform lifecycle.

**`RunAsync() → Task<int>`:**
1. Parse args (`--stylesheet`, `--xml`, `--engine`, `--output`, `--log-level`, `--launch-config`, `--config-name`)
2. If `--launch-config` provided, load and merge — CLI args override launch.json values
3. Validate stylesheet + xml exist → exit 3 if not
4. Validate engine name → exit 2 if unrecognised
5. `XsltEngineManager.SetDebugFlags(debug: false, logLevel: LogLevel.Trace)`
6. Set `XsltEngineManager.OutputWriter = Console.Out` (or `StreamWriter` for `--output`)
7. Subscribe `XsltEngineManager.EngineOutput` → `Console.Error`
8. Subscribe `XsltEngineManager.EngineTerminated` → signal completion + capture exit code
9. `engine.SetBreakpoints([])`
10. `engine.StartAsync(stylesheet, xml, stopOnEntry: false)`
11. Await, return exit code

### 3. `XsltEngineManager.OutputWriter` (new property)

**Required engine fix:** Currently both engines skip writing output when `outPath` is empty (`XsltCompiledEngine.cs:215-218`, `SaxonEngine.cs` same). For CLI mode, stdout must be a valid target.

Add `public static TextWriter? OutputWriter` to `XsltEngineManager`. Both engines check: if `OutputWriter != null`, write result there; else use file path as before. DAP mode never sets this — existing behaviour unchanged.

### 4. `Program.cs` change (minimal)

```csharp
if (args.Contains("--transform"))
    return await new CliTransformRunner(args).RunAsync();

// existing DAP flow below — untouched
```

---

## CLI Arguments (adapter internal)

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
- Parse JSON, find config by `name` field
- Extract `engine`, `stylesheet`, `xml`, `logLevel`
- Resolve `${workspaceFolder}` → directory containing `.vscode/`
- CLI args override launch.json values

---

## Output Streams

| Stream | Content |
|--------|---------|
| stdout | Transform result (XML, HTML, or text per `xsl:output`) |
| stderr | Errors, `xsl:message` output, trace log events |

Both are piped to the VS Code Output panel by the command handler.

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
| `trace` | Execution flow, template entries (**default**) |
| `traceall` | Full XPath value tracking, variable values at every step |

---

## Engine Selection

| `--engine` value | Engine used |
|-----------------|-------------|
| `compiled` | `XsltCompiledEngine` (XSLT 1.0, `msxsl:script`) |
| `saxonnet` or `saxon` | `SaxonEngine` (XSLT 1.0/2.0/3.0) |
| omitted | Auto-detect: compiled if `msxsl:script` present, otherwise Saxon |

---

## Testing

### Adapter tests (xUnit — `XsltDebugger.Tests`)

| Test | Engine | Validates |
|------|--------|-----------|
| Basic compiled transform | compiled | stdout has correct XML, exit 0 |
| XSLT 3.0 transform | saxonnet | stdout has correct XML, exit 0 |
| Inline C# (`msxsl:script`) | compiled | C# executes, output correct, exit 0 |
| Bad XSLT (syntax error) | either | exit 1, error on stderr |
| Missing stylesheet | either | exit 3 |
| Launch config mode | either | reads launch.json, resolves `${workspaceFolder}`, runs correctly |

Existing DAP tests untouched.

### Extension tests (TypeScript)

- Command registered and appears in command palette
- Spawns adapter with correct `--transform` args
- Output panel created and revealed

---

## What Is Not Built

- No interactive mode
- No watch mode (hooks handle re-triggering)
- No output diffing
- No new NuGet dependencies
- No direct DLL invocation by users

---

## Claude Code Hook Integration

VS Code commands cannot be triggered from shell (`code --execute-command` does not exist). The extension exposes a **localhost HTTP server** as a bridge — the hook `curl`s it, the extension runs the transform and returns the full output synchronously in the response body.

### How it works

1. On activation, the extension starts an HTTP server on a random port (`127.0.0.1` only) and writes the port to `~/.xslt-debugger-port`
2. The hook reads that file, sends `POST /run-transform` with `{ "stylesheet": "...", "xml": "..." }`
3. The extension spawns `dotnet --transform`, buffers all stdout+stderr, returns them in the HTTP response body when the process exits
4. Claude reads the response as hook feedback and auto-fixes errors

### Engine auto-detection

When no `engine` field is provided, the extension inspects the stylesheet content:
- Contains `urn:schemas-microsoft-com:xslt` namespace **and** `language="C#"` attribute → `compiled` engine
- Otherwise → `saxonnet` engine

### Hook configuration (`.claude/settings.json`)

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "command": "bash -c 'FILE=$(echo \"$CLAUDE_TOOL_INPUT\" | jq -r \".file_path // .path // empty\" 2>/dev/null); if [[ \"$FILE\" == *.xslt || \"$FILE\" == *.xsl ]]; then PORT=$(cat ~/.xslt-debugger-port 2>/dev/null); if [[ -n \"$PORT\" ]]; then curl -s -X POST \"http://127.0.0.1:$PORT/run-transform\" -H \"Content-Type: application/json\" -d \"{\\\"stylesheet\\\": \\\"$FILE\\\", \\\"xml\\\": \\\"${FILE%.*}-input.xml\\\"}\"; fi; fi'",
            "timeout": 60
          }
        ]
      }
    ]
  }
}
```

### Response codes

| Code | Meaning |
|------|---------|
| 200 | Transform succeeded — body contains output + trace |
| 400 | Bad request (invalid JSON, missing fields) |
| 404 | Stylesheet or XML file not found |
| 500 | Transform process failed (non-zero exit) or spawn error |
| 503 | Adapter DLL not found — run `dotnet build` |

After every `.xslt`/`.xsl` edit: the extension runs the transform, the Output panel shows trace + result, and Claude reads the response body to auto-fix errors.
