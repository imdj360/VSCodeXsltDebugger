# XSLT Debugger

XSLT Debugger is a Visual Studio Code extension that lets you debug XSLT stylesheets using a .NET-based debug adapter. It supports setting breakpoints inside XSLT stylesheets, stepping through transforms, inspecting variables, and evaluating XPath expressions during execution.

## Features

- **Breakpoint Support**: Set breakpoints in XSLT files and step through transformations
- **Variable Inspection**: Inspect XSLT context, variables, and XML node values
- **XPath Evaluation**: Evaluate XPath expressions in the current context
- **Inline C# Scripting**: Debug XSLT stylesheets with embedded C# code using Roslyn
- **Multiple Engines**: Support for compiled XSLT engine (XSLT 1.0) and Saxon engine (XSLT 2.0/3.0)

## Engines

The debugger supports two XSLT processing engines:

### Compiled Engine
- **XSLT Version**: 1.0
- **Features**: Full support for XSLT 1.0, inline C# scripting via `msxsl:script`
- **Compatibility**: Works on all platforms
- **Use Case**: XSLT 1.0 stylesheets with or without inline C# code

### Saxon Engine (.NET)
- **XSLT Version**: 2.0 and 3.0
- **Features**: Full XSLT 2.0/3.0 support, XPath 2.0/3.0, advanced functions
- **Implementation**: Uses SaxonHE10Net31Api (community IKVM build for .NET 8+)
- **Compatibility**: Works on all platforms (Windows, macOS, Linux)
- **License**: Free and open source (Mozilla Public License 2.0)
- **Use Case**: Modern XSLT 2.0/3.0 stylesheets (same approach as Azure Logic Apps Data Mapper)

### Engine Selection
The debugger automatically validates your XSLT and suggests the appropriate engine:

- **XSLT 1.0 with inline C#**: Uses compiled engine
- **XSLT 2.0/3.0**: Uses Saxon .NET engine
- **Manual Override**: Set `"engine": "compiled"` or `"engine": "saxonnet"` in launch.json

### Known Limitations
- **Saxon Engine (.NET)**: Now uses community IKVM build (SaxonHE10Net31Api) which resolves previous .NET 8+ compatibility issues. Supports transforms but breakpoint debugging is still in development.
- **Debugging**: Breakpoint debugging is currently fully implemented for the compiled engine only. Saxon engine supports transforms but not step-through debugging yet.
- **XSLT 3.0**: For XSLT 3.0 stylesheets, you must use `"engine": "saxonnet"`. The compiled engine only supports XSLT 1.0. Debugging instrumentation is automatically disabled for XSLT 3.0 to prevent execution issues.

## Quick Start

1. **Build the extension**:
   ```bash
   npm install
   npm run compile
   dotnet build ./XsltDebugger.DebugAdapter
   ```

2. **Run the Extension Development Host**:
   - Press F5 in VS Code to launch the extension development host
   - Select the `XSLT: Launch` configuration to debug a stylesheet

3. **Package and Install**:
   ```bash
   npx vsce package
   code --install-extension xsltdebugger-0.0.1.vsix
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

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `type` | string | ✅ | Must be `"xslt"` | `"xslt"` |
| `request` | string | ✅ | Must be `"launch"` | `"launch"` |
| `name` | string | ✅ | Display name in debug menu | `"Debug XSLT"` |
| `engine` | string | ❌ | Engine type (`"compiled"` or `"saxonnet"`, default: `"compiled"`) | `"saxonnet"` |
| `stylesheet` | string | ✅ | Path to XSLT file | `"${file}"` or `"${workspaceFolder}/transform.xslt"` |
| `xml` | string | ✅ | Path to input XML | `"${workspaceFolder}/data.xml"` |
| `stopOnEntry` | boolean | ❌ | Pause at transform start | `false` |
| `debug` | boolean | ❌ | Enable debugging mode (breakpoints and stepping, default: `true`) | `true` |
| `logLevel` | string | ❌ | Logging verbosity: `"none"`, `"log"`, `"trace"`, or `"traceall"` (default: `"log"`) | `"log"` |

### Variable Substitutions

- `${file}`: Currently open file in editor
- `${workspaceFolder}`: Root of your workspace
- `${workspaceFolder}/relative/path.xslt`: Specific file in workspace

### Example Configurations

**Debug currently open XSLT file with auto engine selection:**
```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug Current XSLT",
  "stylesheet": "${file}",
  "xml": "${workspaceFolder}/input.xml",
  "stopOnEntry": false
}
```

**Debug XSLT 2.0/3.0 with Saxon .NET engine:**
```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug XSLT 2.0/3.0",
  "engine": "saxonnet",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml",
  "stopOnEntry": false
}
```

**Debug with stop on entry:**
```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug XSLT (Stop at Start)",
  "engine": "compiled",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml",
  "stopOnEntry": true
}
```

**Debug with troubleshooting traces:**
```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug XSLT (trace level)",
  "engine": "compiled",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml",
  "debug": true,
  "logLevel": "trace"
}
```

**Debug with full XPath value tracking:**
```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Debug XSLT (traceall level)",
  "engine": "compiled",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml",
  "debug": true,
  "logLevel": "traceall"
}
```

**Run without debugging (fastest execution):**
```json
{
  "type": "xslt",
  "request": "launch",
  "name": "Run XSLT (no debugging)",
  "engine": "compiled",
  "stylesheet": "${workspaceFolder}/transform.xslt",
  "xml": "${workspaceFolder}/data.xml",
  "debug": false,
  "logLevel": "none"
}
```

### Debugging Features

- **Breakpoints**: Click in the gutter next to XSLT instructions
- **Stepping**: Use F10 (step over), F11 (step into), Shift+F11 (step out)
- **Variables**: Inspect context nodes, attributes, and variables in the Variables panel
- **Watch**: Add XPath expressions to watch their values
- **Console**: Evaluate XPath expressions in the Debug Console

### Log Levels

The debugger supports a hierarchical logging system with four levels:

#### `logLevel: "none"` - Silent Mode
- **Purpose**: Maximum performance, minimal output
- **Output**: Errors only
- **Use Case**: Production runs, performance testing
- **Overhead**: ~0% (no instrumentation when `debug: false`)

#### `logLevel: "log"` - General Execution (Default)
- **Purpose**: High-level execution milestones
- **Output**:
  - Transform started/completed
  - XSLT version detected
  - Compilation status
  - File loading/writing events
- **Use Case**: Normal development, understanding what's happening
- **Overhead**: <1%

#### `logLevel: "trace"` - Troubleshooting
- **Purpose**: Detailed execution flow for debugging
- **Output**: Everything in `log` plus:
  - Breakpoint hits with context node names
  - Execution stops (breakpoint/step/entry)
  - Instrumented line numbers
  - Engine internal phases
  - XPath evaluation requests
- **Use Case**: Debugging breakpoints, understanding execution order
- **Overhead**: ~5-10%

#### `logLevel: "traceall"` - Full XPath Value Tracking
- **Purpose**: Deep inspection of all values and context
- **Output**: Everything in `trace` plus:
  - Full XPath location of context nodes
  - Node values and types
  - XPath expression results with values
  - Attribute values
  - Node structure details
- **Use Case**: Understanding data flow, debugging complex XPath expressions
- **Overhead**: ~15-20%

**Example Scenarios:**
- **Production/CI**: `logLevel: "log"` - See what's happening
- **Development**: `logLevel: "log"` or `logLevel: "trace"` - Normal debugging
- **Troubleshooting**: `logLevel: "trace"` - Why isn't my breakpoint working?
- **Deep debugging**: `logLevel: "traceall"` - What values am I actually getting?
- **Performance tests**: `debug: false, logLevel: "none"` - Maximum speed

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

- Visual Studio Code 1.105.0+
- .NET 8.0 SDK (for building the debug adapter)
- Node.js 18+ (for building the extension)
- **Saxon Engine (.NET)**: Fully compatible with .NET 8+ on all platforms (Windows, macOS, Linux) using SaxonHE10Net31Api - no additional dependencies required

## Architecture

The extension consists of:
- **TypeScript Extension**: VS Code integration and configuration
- **C# Debug Adapter**: Implements DAP protocol and XSLT execution
- **Instrumentation Engine**: Dynamically modifies XSLT to insert debug hooks

## Recent Changes

### v0.0.3 - Saxon .NET Engine Fix (IKVM Compatibility Resolution)
- **Fixed**: Replaced Saxon-HE 10.9.0 with SaxonHE10Net31Api 10.9.15 (community IKVM build)
- **Fixed**: Resolved MissingMethodException errors on .NET 8+ by using modern IKVM-compiled Saxon
- **Upgraded**: Project now targets .NET 8.0 for better compatibility with modern packages
- **Improved**: Saxon .NET engine now works on all platforms (Windows, macOS, Linux) without Java
- **Removed**: Saxon Java engine - no longer needed as .NET Saxon works cross-platform
- **Impact**: XSLT 2.0/3.0 support now works reliably cross-platform using pure .NET approach (same as Azure Logic Apps Data Mapper)
- **Credit**: Uses Martin Honnen's community IKVM builds under Mozilla Public License 2.0
- **Engines**: Now supports two engines - `"compiled"` (XSLT 1.0) and `"saxonnet"` (XSLT 2.0/3.0)

### v0.0.1 - Debug Adapter Packaging Fix
- **Issue**: Extension failed to activate with "Couldn't find a debug adapter descriptor" error
- **Root Cause**: `.vscodeignore` was excluding the debug adapter DLL from the package
- **Solution**: Modified `.vscodeignore` to include `XsltDebugger.DebugAdapter/bin/Debug/net8.0/XsltDebugger.DebugAdapter.dll`
- **Impact**: Extension now properly packages and includes the .NET debug adapter (package size: 16.31 MB)

### v0.0.1 - Inline C# Compilation Fix
- **Issue**: Inline C# scripts in XSLT stylesheets would fail to compile when containing `using` statements that conflicted with the generated prelude
- **Solution**: Modified the `CompileAndCreateExtensionObject` method to dynamically build the prelude, checking for existing `using` statements in user code and avoiding duplicates
- **Impact**: XSLT stylesheets with inline C# code can now safely include `using System;`, `using System.Globalization;`, and other standard namespaces without compilation errors

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass: `npm test`
6. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
