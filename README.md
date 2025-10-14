# XSLT Debugger

XSLT Debugger is a Visual Studio Code extension that lets you debug XSLT stylesheets using a .NET-based debug adapter. It supports setting breakpoints inside XSLT stylesheets, stepping through transforms, inspecting variables, and evaluating XPath expressions during execution.

## Features

- **Breakpoint Support**: Set breakpoints in XSLT files and step through transformations
- **Variable Inspection**: Inspect XSLT context, variables, and XML node values
- **XPath Evaluation**: Evaluate XPath expressions in the current context
- **Inline C# Scripting**: Debug XSLT stylesheets with embedded C# code using Roslyn
- **Multiple Engines**: Support for compiled XSLT engine (with Saxon placeholder)

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

1. Open an XSLT file in VS Code
2. Go to Run and Debug view (Ctrl+Shift+D)
3. Click "create a launch.json file" and select "XSLT"
4. Configure the launch settings:
   ```json
   {
     "type": "xslt",
     "request": "launch",
     "name": "Debug XSLT",
     "stylesheet": "${file}",
     "xml": "${workspaceFolder}/data/input.xml",
     "engine": "compiled",
     "stopOnEntry": false
   }
   ```

### Configuration Options

- `stylesheet`: Path to the XSLT stylesheet (defaults to current file)
- `xml`: Path to the input XML document
- `engine`: Execution engine (`"compiled"` or `"saxon"`)
- `stopOnEntry`: Pause at the start of transformation

### Debugging Features

- **Breakpoints**: Click in the gutter next to XSLT instructions
- **Stepping**: Use F10 (step over), F11 (step into), Shift+F11 (step out)
- **Variables**: Inspect context nodes, attributes, and variables in the Variables panel
- **Watch**: Add XPath expressions to watch their values
- **Console**: Evaluate XPath expressions in the Debug Console

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

## Architecture

The extension consists of:
- **TypeScript Extension**: VS Code integration and configuration
- **C# Debug Adapter**: Implements DAP protocol and XSLT execution
- **Instrumentation Engine**: Dynamically modifies XSLT to insert debug hooks

## Recent Fixes

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
