# XsltDebugger.DebugAdapter

This is the .NET Debug Adapter Protocol (DAP) server for the XSLT Debugger VS Code extension.

## Overview

The debug adapter implements the Debug Adapter Protocol (DAP) to enable debugging of XSLT stylesheets in Visual Studio Code. It provides breakpoint support, step-through debugging, variable inspection, and XPath evaluation.

## Architecture

- **Language**: C# (.NET 8)
- **Protocol**: Debug Adapter Protocol (DAP)
- **Communication**: JSON-RPC over stdin/stdout

## Key Components

### Core Classes

- **`Program.cs`**: Entry point for the debug adapter
- **`XsltDebugSession.cs`**: Main DAP session handler
- **`CompiledEngine.cs`**: XSLT 1.0 engine with inline C# support
- **`SaxonEngine.cs`**: XSLT 2.0/3.0 engine using Saxon .NET
- **`XsltInstrumenter.cs`**: Injects debugging hooks into XSLT stylesheets

### Features

- **Breakpoint Support**: Set and manage breakpoints in XSLT files
- **Stepping**: Step over, step into, step out functionality
- **Variable Inspection**: Inspect XSLT context nodes, variables, and attributes
- **XPath Evaluation**: Evaluate XPath expressions in the debug console
- **Inline C# Scripting**: Compile and execute inline C# code in XSLT via `msxsl:script`
- **Multiple Engines**:
  - Compiled engine for XSLT 1.0 with inline C# support
  - Saxon .NET engine for XSLT 2.0/3.0 support
- **Instrumentation**: Dynamic XSLT modification for debug hooks

## Building

Build the debug adapter:

```bash
dotnet build
```

Build in release mode:

```bash
dotnet build -c Release
```

## Running

The debug adapter is launched automatically by the VS Code extension. For manual testing:

```bash
dotnet run
```

Or run the compiled executable:

```bash
./bin/Debug/net8.0/XsltDebugger.DebugAdapter
```

## Testing

Run the unit tests:

```bash
dotnet test ../XsltDebugger.Tests/XsltDebugger.Tests.csproj
```

## Dependencies

- **Microsoft.CSharp**: For Roslyn C# compilation (inline scripts)
- **SaxonHE10Net31Api**: Saxon .NET XSLT 2.0/3.0 processor (community IKVM build)
- **System.Xml**: For XSLT 1.0 processing

## Protocol Support

The debug adapter implements the following DAP requests:

- `initialize`: Capability negotiation
- `launch`: Start debugging session
- `setBreakpoints`: Configure breakpoints
- `configurationDone`: Complete initialization
- `threads`: List execution threads
- `stackTrace`: Get call stack
- `scopes`: Get variable scopes
- `variables`: Get variable values
- `evaluate`: Evaluate XPath expressions
- `continue`: Resume execution
- `next`: Step over
- `stepIn`: Step into
- `stepOut`: Step out
- `disconnect`: End debugging session

## Development

### Debugging the Debug Adapter

1. Open the solution in Visual Studio Code
2. Set breakpoints in the C# code
3. Press F5 to launch the Extension Development Host
4. Start an XSLT debugging session
5. The C# debugger will attach to the debug adapter process

### Adding New Features

1. Implement DAP request handlers in `XsltDebugSession.cs`
2. Update the instrumentation logic in `XsltInstrumenter.cs` if needed
3. Add tests in the `XsltDebugger.Tests` project
4. Update the main README with user-facing documentation

## License

This project is part of the XSLT Debugger extension and is licensed under the MIT License.
