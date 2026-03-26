# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

XsltDebugger is a VS Code extension that enables debugging of XSLT stylesheets. It uses a dual-component architecture:
- **TypeScript extension** (VS Code frontend): registers the debug type, validates config, launches the adapter
- **.NET Debug Adapter** (C# backend): implements the Debug Adapter Protocol (DAP) over stdin/stdout JSON-RPC

## Build Commands

```bash
# TypeScript extension
npm install
npm run compile          # Compile TypeScript → JavaScript (out/)
npm run watch            # Watch mode for development

# .NET debug adapter
dotnet build ./XsltDebugger.DebugAdapter
dotnet build XsltDebugger.sln    # Build all projects

# Tests
dotnet test ./XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal
dotnet test ./XsltDebugger.Tests/XsltDebugger.Tests.csproj --filter "FullyQualifiedName~StepInto"

# Run transform without debugging (headless)
dotnet run --project XsltDebugger.DebugAdapter -- --transform \
  --engine saxonnet \
  --stylesheet path/to/file.xslt \
  --xml path/to/input.xml \
  --log-level trace

# Packaging
./package-darwin.sh      # macOS ARM64 VSIX
./package-win.sh         # Windows x64 VSIX
./package-all.sh         # Both platforms
```

## Architecture

### Communication Flow

```
VS Code UI → extension.ts (TypeScript)
                ↓ (spawns via dotnet CLI)
         DapServer.cs (C# / DAP JSON-RPC over stdin/stdout)
                ↓
         XsltEngineFactory
          ├── XsltCompiledEngine  (XSLT 1.0 + inline C# via msxsl:script)
          └── SaxonEngine         (XSLT 1.0 / 2.0 / 3.0 via Saxon.Api)
```

### How Debugging Works

Debugging is **instrumentation-based** — not traditional debugger hooks. Before execution, the engine:
1. Loads the XSLT as `XDocument` with line info
2. Injects `dbg:break()` XPath calls at breakpoint locations
3. Injects `xsl:message` elements for variable capture
4. Compiles and runs the instrumented stylesheet

**Step semantics** are implemented via call depth tracking (`_callDepth`, `_targetDepth` in `BaseXsltEngine.cs`).

### Engine Selection

Auto-selected based on stylesheet contents:
- **XsltCompiledEngine**: Used when `msxsl:script` (inline C#) is detected, or explicitly via launch config `engine: "compiled"`
- **SaxonEngine**: Used for XSLT 2.0/3.0, or when no inline C# is needed

### Variable Capture

- **Compiled engine**: Basic — only captures `@select`-based variable expressions
- **Saxon engine**: Full — `SaxonInstrumentation` injects `xsl:message` elements after variable declarations; `SaxonMessageListener` parses `[DBG]`-prefixed messages to populate the Variables panel

### Key Source Files

| File | Purpose |
|------|---------|
| `src/extension.ts` | Extension entry: registers debug type, resolves paths, spawns .NET adapter |
| `XsltDebugger.DebugAdapter/Program.cs` | Adapter entry point; initializes engine and DAP server |
| `XsltDebugger.DebugAdapter/DapServer.cs` | DAP protocol handler; routes requests, manages state |
| `XsltDebugger.DebugAdapter/IXsltEngine.cs` | Engine interface contract |
| `XsltDebugger.DebugAdapter/BaseXsltEngine.cs` | Shared breakpoint/stepping depth logic |
| `XsltDebugger.DebugAdapter/XsltCompiledEngine.cs` | .NET `XslCompiledTransform` engine |
| `XsltDebugger.DebugAdapter/SaxonEngine.cs` | Saxon.Api engine for XSLT 2.0/3.0 |
| `XsltDebugger.DebugAdapter/Xslt1Instrumentation.cs` | Injects debug hooks into XSLT 1.0 |
| `XsltDebugger.DebugAdapter/SaxonInstrumentation.cs` | Injects debug hooks for XSLT 2.0/3.0 |
| `XsltDebugger.DebugAdapter/SaxonMessageListener.cs` | Parses `[DBG]` messages from Saxon output |
| `XsltDebugger.DebugAdapter/InlineCSharpInstrumenter.cs` | Roslyn instrumentation for `msxsl:script` blocks |
| `XsltDebugger.DebugAdapter/SessionState.cs` | Tracks per-session debug state |
| `XsltDebugger.DebugAdapter/CliTransformRunner.cs` | Headless transform mode — no DAP, result to stdout, trace to stderr |

## Testing

Tests live in `XsltDebugger.Tests/` (xUnit). Integration test fixtures are in `TestData/Integration/`:
- `xslt/` — test stylesheets
- `xml/` — input XML documents
- `out/` — generated instrumented XSLTs and transform output

The `--test-engine` CLI flag on the adapter runs the engine standalone without a DAP client (useful for isolated engine testing).

## Known Limitations

- `xsl:attribute` elements cannot be stepped through (processed atomically)
- No conditional breakpoints or "set variable" support
- Variable capture only works for `@select`-based variable declarations
- No step-back support (instrumentation is forward-only)
- ~5–15% performance overhead in trace mode

## Development Notes

- Set `XSLT_MOCK_ADAPTER=1` env var to use a mock adapter instead of the real .NET adapter (for extension-side development)
- The adapter searches for its DLL in `net8.0`, `net9.0`, `net7.0`, `net6.0` subdirectories (see `locateAdapter()` in `extension.ts`)
- Instrumented stylesheets use `dbg:probe="1"` attribute tagging to avoid double-instrumentation on re-runs
- Saxon engine uses `SaxonHE10Net31Api` (community IKVM build, v10.9.15)
