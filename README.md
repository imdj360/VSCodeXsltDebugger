# XSLT Debugger for Visual Studio Code

A powerful Visual Studio Code extension that enables debugging support for XSLT stylesheets. Set breakpoints, step through transformations, inspect variables, and evaluate XPath expressions in real-time using a .NET-based debug adapter.

## Table of Contents

- [Features](#features)
- [XSLT Processing Engines](#xslt-processing-engines)
- [Quick Start](#quick-start)
- [Usage](#usage)
  - [Setting Up a Debug Configuration](#setting-up-a-debug-configuration)
  - [Configuration Parameters](#configuration-parameters)
  - [Example Configurations](#example-configurations)
  - [Debugging Features](#debugging-features)
  - [Log Levels](#log-levels)
  - [Inline C# Scripting](#inline-c-scripting)
- [Requirements](#requirements)
- [Architecture](#architecture)
- [What's New](#whats-new)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Breakpoint Support**: Set breakpoints in XSLT files and step through transformations
- **Variable Inspection**: Inspect XSLT context, variables, and XML node values
- **XPath Evaluation**: Evaluate XPath expressions in the current context
- **Inline C# Scripting**: Debug XSLT stylesheets with embedded C# code using Roslyn
- **Multiple Engines**: Support for compiled XSLT engine (XSLT 1.0) and Saxon engine (XSLT 2.0/3.0)
- **Cross-Platform**: Works on Windows, macOS, and Linux

## XSLT Processing Engines

The debugger supports two engines to handle different XSLT versions and use cases:

### Compiled Engine (XSLT 1.0)

| Feature               | Details                                                    |
| --------------------- | ---------------------------------------------------------- |
| **XSLT Version**      | 1.0                                                        |
| **Special Features**  | Inline C# scripting via `msxsl:script`                     |
| **Debugging Support** | Full breakpoint and step-through debugging                 |
| **Platform Support**  | Windows, macOS, Linux                                      |
| **Best For**          | XSLT 1.0 stylesheets, especially those with inline C# code |

### Saxon .NET Engine (XSLT 2.0/3.0)

| Feature               | Details                                                                         |
| --------------------- | ------------------------------------------------------------------------------- |
| **XSLT Version**      | 2.0 and 3.0                                                                     |
| **Implementation**    | SaxonHE10Net31Api (community IKVM build)                                        |
| **XPath Support**     | 2.0 and 3.0                                                                     |
| **Debugging Support** | Transform execution (breakpoint debugging in development)                       |
| **Platform Support**  | Windows, macOS, Linux                                                           |
| **License**           | Mozilla Public License 2.0 (free and open source)                               |
| **Best For**          | Modern XSLT 2.0/3.0 stylesheets (same approach as Azure Logic Apps Data Mapper) |

### Engine Selection

The debugger can automatically detect the appropriate engine based on your XSLT version:

- **XSLT 1.0 + inline C#** → Compiled engine (automatic)
- **XSLT 2.0/3.0** → Saxon .NET engine (automatic)
- **Manual Override** → Set `"engine": "compiled"` or `"engine": "saxonnet"` in [launch.json](#setting-up-a-debug-configuration)

### ⚠️ Current Limitations

The debugger is designed to stay simple and stable, without attempting to fully parse complex XSLT constructs.

To keep it lightweight and predictable:

- Breakpoint and step debugging are currently limited to basic XSLT structures (templates, loops, and expressions). Deep or dynamic template calls are intentionally not instrumented to avoid complex XSLT parsing.
- Inline C# scripts execute as black boxes — stepping into C# code during debugging is not supported.
- XSLT 2.0/3.0 debugging is limited to transformation execution — step-through debugging is under development.
- Variable inspection covers `@select`-based variables; content-defined variables are skipped for now.
- Trace logging introduces minor runtime overhead (up to ~15% in `traceall` mode).
- Marketplace release is pending — available today via `.vsix` local install.

These tradeoffs ensure reliable, cross-platform debugging without slowing down transformations or overcomplicating the runtime.

🧩 Note: This is a complementary developer tool intended for debugging and learning — not a production-grade runtime.

## Quick Start

### For Users

1. **Install the extension** from the VS Code marketplace:

   **Platform-Specific Extensions:**

   - **macOS**: Search for "XSLT Debugger Darwin" in VS Code Extensions
   - **Windows**: Search for "XSLT Debugger Windows" in VS Code Extensions

   **Or install from `.vsix` file:**

   ```bash
   # macOS
   code --install-extension xsltdebugger-darwin-0.0.3.vsix

   # Windows
   code --install-extension xsltdebugger-windows-0.0.3.vsix
   ```

2. **Create a debug configuration** in [.vscode/launch.json](#setting-up-a-debug-configuration)

3. **Start debugging** by pressing F5 or selecting "Debug XSLT" from the debug menu

### For Developers

1. **Clone and build the extension**:

   ```bash
   npm install
   npm run compile
   dotnet build ./XsltDebugger.DebugAdapter
   ```

2. **Run the Extension Development Host**:

   - Press F5 in VS Code to launch the extension development host
   - Select the "XSLT: Launch" configuration to debug a stylesheet

3. **Package and install locally**:

   **Platform-specific packaging** (optimized size, only includes binaries for target platform):

   ```bash
   # For macOS
   ./package-darwin.sh
   code --install-extension xsltdebugger-darwin-*.vsix

   # For Windows
   ./package-win.sh
   code --install-extension xsltdebugger-windows-*.vsix
   ```

   **Universal packaging** (includes all platforms, larger file size):

   ```bash
   npx vsce package
   code --install-extension xsltdebugger-*.vsix
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

| Feature           | Description                                           | How to Use                                             |
| ----------------- | ----------------------------------------------------- | ------------------------------------------------------ |
| **Breakpoints**   | Pause execution at specific XSLT instructions         | Click in the gutter next to line numbers               |
| **Stepping**      | Control execution flow                                | F10 (step over), F11 (step into), Shift+F11 (step out) |
| **Variables**     | Inspect context nodes, attributes, and XSLT variables | View in the Variables panel during debugging           |
| **Watch**         | Monitor specific XPath expressions                    | Add expressions to the Watch panel                     |
| **Debug Console** | Evaluate XPath expressions interactively              | Type XPath expressions in the Debug Console            |

#### Variable Inspection Notes

- XSLT 2.0 and 3.0 variables are automatically captured and displayed

### Log Levels

The debugger supports four hierarchical logging levels:

| Level             | Purpose           | Output Includes                                                                                  | Overhead | Best For                                         |
| ----------------- | ----------------- | ------------------------------------------------------------------------------------------------ | -------- | ------------------------------------------------ |
| **none**          | Silent mode       | Errors only                                                                                      | ~0%      | Production, performance testing                  |
| **log** (default) | General execution | Transform lifecycle, XSLT version, compilation status, file I/O                                  | <1%      | Normal development                               |
| **trace**         | Troubleshooting   | Everything in `log` + breakpoint hits, execution stops, instrumented lines, XPath requests       | ~5-10%   | Debugging breakpoints, execution flow            |
| **traceall**      | Deep inspection   | Everything in `trace` + XPath locations, node values/types, expression results, attribute values | ~15-20%  | Understanding data flow, complex XPath debugging |

#### Common Scenarios

```json
// Normal development
{ "logLevel": "log" }

// Breakpoint not working?
{ "logLevel": "trace" }

// Need to see actual values?
{ "logLevel": "traceall" }

// Maximum performance
{ "debug": false, "logLevel": "none" }
```

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

The extension is built on three main components:

1. **TypeScript Extension** ([src/extension.ts](src/extension.ts))

   - VS Code integration layer
   - Debug configuration provider
   - Debug adapter factory

2. **C# Debug Adapter** ([XsltDebugger.DebugAdapter/](XsltDebugger.DebugAdapter/))

   - Implements Debug Adapter Protocol (DAP)
   - XSLT execution engines (compiled and Saxon .NET)
   - Breakpoint management and stepping logic

3. **Instrumentation Engine** ([XsltDebugger.DebugAdapter/XsltInstrumenter.cs](XsltDebugger.DebugAdapter/XsltInstrumenter.cs))
   - Dynamically modifies XSLT to insert debug hooks
   - Captures execution context at breakpoints
   - Enables variable inspection

## What's New

See the [CHANGELOG](CHANGELOG.md) for detailed version history.

### Latest Release: v0.0.3

**Saxon .NET Engine Improvements**

- Fixed compatibility issues with .NET 8+ using SaxonHE10Net31Api (community IKVM build)
- XSLT 2.0/3.0 support now works cross-platform without Java
- Same reliable approach used by Azure Logic Apps Data Mapper

**Key Features**

- Full debugging support for XSLT 1.0 with breakpoints and stepping
- Transform execution for XSLT 2.0/3.0 (breakpoint debugging in development)
- Inline C# scripting with `msxsl:script`
- Variable inspection and XPath evaluation
- Configurable log levels for troubleshooting

## Contributing

Contributions are welcome! Here's how to get started:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass: `dotnet test XsltDebugger.Tests/`
6. Build and test the extension: `npm run compile`
7. Submit a pull request

### Development Setup

```bash
# Clone the repository
git clone <repository-url>
cd XsltDebugger

# Install dependencies
npm install
dotnet restore

# Build the project
npm run compile
dotnet build XsltDebugger.DebugAdapter/

# Run tests
dotnet test XsltDebugger.Tests/

# Package for testing (platform-specific)
./package-darwin.sh      # macOS
./package-win.sh         # Windows

# Or package universal (all platforms)
npx vsce package
```

### Platform-Specific Packaging

The extension supports platform-specific packaging to reduce file size by including only the necessary binaries for each platform:

#### Package Scripts

| Script                                 | Platform | Extension Name         | Includes                |
| -------------------------------------- | -------- | ---------------------- | ----------------------- |
| [package-darwin.sh](package-darwin.sh) | macOS    | `xsltdebugger-darwin`  | osx-arm64 binaries only |
| [package-win.sh](package-win.sh)       | Windows  | `xsltdebugger-windows` | win-x64 binaries only   |

#### How It Works

Each packaging script:

1. Temporarily modifies `package.json` to use a platform-specific extension name
2. Removes unnecessary runtime binaries for other platforms
3. Packages the extension with `--target` flag for the specific platform
4. Restores the original `package.json`

#### Publishing to Marketplace

```bash
# Build platform-specific packages
./package-darwin.sh
./package-win.sh

# Publish each as a separate extension
vsce publish -p YOUR_TOKEN --packagePath xsltdebugger-darwin-*.vsix
vsce publish -p YOUR_TOKEN --packagePath xsltdebugger-windows-*.vsix
```

**Benefits:**

- Smaller download size for users (only includes their platform's binaries)
- Separate marketplace listings prevent package conflicts
- Each platform can be updated independently

```

### Project Structure

```

XsltDebugger/
├── src/ # TypeScript extension source
│ ├── extension.ts # Main extension entry point
│ └── test/ # Extension tests
├── XsltDebugger.DebugAdapter/ # C# debug adapter
│ ├── Program.cs # Debug adapter entry point
│ ├── XsltDebugSession.cs # DAP implementation
│ ├── CompiledEngine.cs # XSLT 1.0 engine
│ ├── SaxonEngine.cs # XSLT 2.0/3.0 engine
│ └── XsltInstrumenter.cs # Debugging instrumentation
├── XsltDebugger.Tests/ # C# unit tests
└── package.json # Extension manifest

```

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

### Third-Party Licenses

- **SaxonHE10Net31Api**: Mozilla Public License 2.0 (Martin Honnen's community IKVM build)
```
