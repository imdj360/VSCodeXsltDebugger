# XSLT Debugger for Visual Studio Code

A powerful Visual Studio Code extension that enables debugging support for XSLT stylesheets. Set breakpoints, step through transformations, inspect variables, and evaluate XPath expressions in real-time using a .NET-based debug adapter.

## Features

- **Breakpoint Support**: Set breakpoints in XSLT files and step through transformations
- **Variable Inspection**: Automatically materialises XSLT variables and context nodes inside VS Code’s VARIABLES pane
- **XPath Evaluation**: Evaluate XPath expressions in the current context
- **Inline C# Scripting**: Debug XSLT stylesheets with embedded C# code using Roslyn
- **Multiple Engines**: Support for compiled XSLT engine (XSLT 1.0) and Saxon engine (XSLT 2.0/3.0)
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **Probe Tagging**: Instrumented breakpoints and trace messages are tagged with `dbg:probe="1"` so repeated runs stay idempotent

## XSLT Processing Engines

| Feature           | Compiled Engine                  | Saxon .NET Engine                    |
| ----------------- | -------------------------------- | ------------------------------------ |
| **XSLT Version**  | 1.0                              | 2.0, 3.0                             |
| **XPath Version** | 1.0                              | 2.0, 3.0                             |
| **Special Features** | Inline C# via `msxsl:script`  | Full XSLT 2.0/3.0 features           |
| **Best For**      | XSLT 1.0 with inline C#          | Modern XSLT 2.0/3.0 stylesheets      |

**Engine Selection**: Auto-detected based on XSLT version, or manually set with `"engine": "compiled"` or `"engine": "saxonnet"` in launch.json

### ⚠️ Current Limitations

- Debugging focuses on basic XSLT structures (templates, loops, expressions); complex dynamic calls are not instrumented
- Cannot step into inline C# scripts
- Variable inspection uses "falldown" approach: variables are auto-captured via instrumentation as execution progresses forward (cannot re-run or step back to previous lines)
- No support for: **step back**, goto targets, set variable, conditional breakpoints, or debug console autocomplete
- Variable capture limited to `@select`-based variables; complex variables with content children may not be fully captured
- Trace logging adds ~5-15% overhead in `trace`/`traceall` modes

**Note**: Step-into for `xsl:call-template` is fully supported (F11).

## Quick Start

### For Users

1. **Install the extension** from the VS Code marketplace:

   **Platform-Specific Extensions:**

   - **macOS**: Search for "XSLT Debugger Darwin" in VS Code Extensions
   - **Windows**: Search for "XSLT Debugger Windows" in VS Code Extensions

   **Or install from `.vsix` file:**

   ```bash
   # macOS
   code --install-extension xsltdebugger-darwin-darwin-arm64-0.6.0.vsix

   # Windows
   code --install-extension xsltdebugger-windows-win32-x64-0.6.0.vsix
   ```

2. **Create a debug configuration** in [.vscode/launch.json](#setting-up-a-debug-configuration)

3. **Start debugging** by pressing F5 or selecting "Debug XSLT" from the debug menu

### For Developers

1. **Clone and build the extension**:

   ```bash
   npm install
   npm run compile
   dotnet build ./XsltDebugger.DebugAdapter
   dotnet test ./XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal
   ```

2. **Run the Extension Development Host**:

   - Press F5 in VS Code to launch the extension development host
   - Select the "XSLT: Launch" configuration to debug a stylesheet

3. **Package and install locally**:

   **Build both platforms at once** (recommended):

   ```bash
   ./package-all.sh
   code --install-extension xsltdebugger-darwin-darwin-arm64-0.6.0.vsix
   ```

   **Platform-specific packaging** (build individually):

   ```bash
   # For macOS only
   ./package-darwin.sh
   code --install-extension xsltdebugger-darwin-darwin-arm64-0.6.0.vsix

   # For Windows only
   ./package-win.sh
   code --install-extension xsltdebugger-windows-win32-x64-0.6.0.vsix
   ```

## Usage

### Setting Up a Debug Configuration

Create a `.vscode/launch.json` file in your project workspace:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "type": "xslt",
      "request": "launch",
      "name": "Debug XSLT",
      "engine": "compiled",
      "stylesheet": "${workspaceFolder}/ShipmentConf.xslt",
      "xml": "${workspaceFolder}/ShipmentConf.xml",
      "stopOnEntry": false
    }
  ]
}
```

### Configuration Parameters

| Parameter     | Type    | Required | Description                                                                         | Example                                              |
| ------------- | ------- | -------- | ----------------------------------------------------------------------------------- | ---------------------------------------------------- |
| `type`        | string  | ✅       | Must be `"xslt"`                                                                    | `"xslt"`                                             |
| `request`     | string  | ✅       | Must be `"launch"`                                                                  | `"launch"`                                           |
| `name`        | string  | ✅       | Display name in debug menu                                                          | `"Debug XSLT"`                                       |
| `engine`      | string  | ❌       | Engine type (`"compiled"` or `"saxonnet"`, default: `"compiled"`)                   | `"saxonnet"`                                         |
| `stylesheet`  | string  | ✅       | Path to XSLT file                                                                   | `"${file}"` or `"${workspaceFolder}/transform.xslt"` |
| `xml`         | string  | ✅       | Path to input XML                                                                   | `"${workspaceFolder}/data.xml"`                      |
| `stopOnEntry` | boolean | ❌       | Pause at transform start                                                            | `false`                                              |
| `debug`       | boolean | ❌       | Enable debugging mode (breakpoints and stepping, default: `true`)                   | `true`                                               |
| `logLevel`    | string  | ❌       | Logging verbosity: `"none"`, `"log"`, `"trace"`, or `"traceall"` (default: `"log"`) | `"log"`                                              |

### Variable Substitutions

- `${file}`: Currently open file in editor
- `${workspaceFolder}`: Root of your workspace
- `${workspaceFolder}/relative/path.xslt`: Specific file in workspace

### Example Configurations

**Basic debugging (auto engine selection):**

```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug XSLT",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml"
}
```

**XSLT 2.0/3.0 with Saxon engine:**

```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug XSLT 2.0/3.0",
  "engine": "saxonnet",
  "stylesheet": "${file}",
  "xml": "${workspaceFolder}/data.xml"
}
```

**Advanced debugging with detailed logging:**

```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug with Trace",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml",
  "logLevel": "trace",
  "stopOnEntry": true
}
```

### Debugging Features

| Feature           | Description                                           | How to Use                                             |
| ----------------- | ----------------------------------------------------- | ------------------------------------------------------ |
| **Breakpoints**   | Pause execution at specific XSLT instructions         | Click in the gutter next to line numbers               |
| **Stepping**      | Control execution flow with full step-into support    | F10 (step over), F11 (step into), Shift+F11 (step out) |
| **Variables**     | Inspect context nodes, attributes, and XSLT variables | View in the Variables panel during debugging           |
| **Watch**         | Monitor specific XPath expressions                    | Add expressions to the Watch panel                     |
| **Debug Console** | Evaluate XPath expressions interactively              | Type XPath expressions in the Debug Console            |

#### Stepping Features

- **F11 (Step Into)**: Steps into `xsl:call-template` calls, allowing you to debug named templates
- **F10 (Step Over)**: Executes the current line without stepping into template calls
- **Shift+F11 (Step Out)**: Continues execution until returning from the current template

#### Variable Inspection Notes

- **Auto-Capture**: Variables are automatically instrumented during stylesheet compilation - debug messages are injected after each variable declaration
- **Falldown Approach**: Variables appear in the Variables panel as execution flows forward past their declarations (not available before declaration)
- **No Step Back**: Since variables are captured via forward instrumentation, you cannot step backward to re-inspect previous values
- **XSLT 2.0/3.0**: Full support via Saxon engine with `@select`-based variable capture
- **XSLT 1.0**: Limited variable inspection via Compiled engine

### Log Levels

| Level             | Output                                           | Overhead | Use Case                      |
| ----------------- | ------------------------------------------------ | -------- | ----------------------------- |
| **none**          | Errors only                                      | ~0%      | Performance testing           |
| **log** (default) | Lifecycle, compilation, file I/O                 | <1%      | Normal development            |
| **trace**         | + Breakpoint hits, execution flow                | ~5-10%   | Troubleshooting breakpoints   |
| **traceall**      | + Node values, XPath results, attribute details  | ~15%     | Complex XPath debugging       |

### Inline C# Scripting

The debugger supports XSLT stylesheets with embedded C# code using `msxsl:script` elements:

```xml
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:msxsl="urn:schemas-microsoft-com:xslt"
                xmlns:my="urn:my-scripts">
  <msxsl:script language="C#" implements-prefix="my">
    public string Hello(string name) {
      return "Hello, " + name;
    }
  </msxsl:script>

  <xsl:template match="/">
    <output>
      <xsl:value-of select="my:Hello(/root/name)"/>
    </output>
  </xsl:template>
</xsl:stylesheet>
```

## Requirements

### For Users

- Visual Studio Code 1.105.0 or higher
- No additional dependencies (the extension includes the .NET debug adapter)

### For Developers

- Visual Studio Code 1.105.0 or higher
- .NET 8.0 SDK (for building the debug adapter)
- Node.js 18 or higher (for building the extension)

## Architecture

**VS Code Extension (TS)** → DAP Protocol → **.NET Debug Adapter** → **XSLT Engines**

- **Extension** ([src/extension.ts](src/extension.ts)): Registers debug type, resolves paths
- **Adapter** ([XsltDebugger.DebugAdapter/](XsltDebugger.DebugAdapter/)): DAP server, engine management, breakpoint/stepping logic
- **Engines**: `XsltCompiledEngine` (XSLT 1.0 + C#), `SaxonEngine` (XSLT 2.0/3.0)
- **Instrumentation**:
  - Breakpoints: Both engines inject `dbg:break()` extension function calls at breakpoint lines
  - Variables: Saxon engine injects `<xsl:message>` elements after variable declarations to auto-capture values
  - Message Listener: [SaxonMessageListener.cs](XsltDebugger.DebugAdapter/SaxonMessageListener.cs) parses `[DBG]` messages and populates the Variables panel

## What's New in v0.6.0

- **Reliable stepping**: F10/F11/Shift+F11 work correctly with `xsl:call-template` for both engines
- **Improved instrumentation**: Template entry/exit tracking prevents "fall through" during nested calls
- **Test coverage**: 115+ integration tests covering step-in/over/out scenarios

See [CHANGELOG.md](CHANGELOG.md) for full version history.

## Contributing

Contributions are welcome!

```bash
# Build and test
npm install && npm run compile
dotnet build ./XsltDebugger.DebugAdapter
dotnet test ./XsltDebugger.Tests

# Package locally
./package-all.sh  # macOS + Windows
code --install-extension xsltdebugger-darwin-darwin-arm64-0.6.0.vsix
```

Submit PRs with tests for new functionality.


## License

MIT License - see [LICENSE](LICENSE) file.

**Third-Party**: SaxonHE10Net31Api uses Mozilla Public License 2.0 (Martin Honnen's community IKVM build).
