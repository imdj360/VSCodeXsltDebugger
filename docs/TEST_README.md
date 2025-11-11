# XSLT Debugger Test Report

## Latest Run

- Command: `dotnet test XsltDebugger.Tests`
- Result: 102 tests passed, 0 failed, 0 skipped
- Runtime: .NET 8.0 (`net8.0` target)

## Key Testing Insights

- **Unit Coverage Focus**: The current suite exercises the debug adapter’s core behaviors for XSLT 1.0 scenarios. It validates breakpoint handling, stepping, and variable inspection without touching UI layers or VS Code-specific logic.
- **Engine Parity Gaps**: XSLT 2.0/3.0 paths rely on integration tests that are still under development. Add high-level tests once Saxon step-through debugging becomes available to avoid regressions.
- **Inline C# Scripts**: Execution paths that host inline C# scripts are verified at the adapter boundary only; stepping into C# remains unsupported, matching the documented limitation.
- **Performance Monitoring**: Trace logging adds measurable overhead. Keep trace-level tests lightweight and prefer targeted scenarios to maintain fast feedback.
- **Cross-Platform Confidence**: The suite runs green on macOS/.NET 8.0. Re-run on Windows and Linux periodically (especially before packaging) to ensure platform-specific binaries remain compatible.

## Recommended Next Steps

1. Add automated coverage for Saxon transformation runs once the step-through feature stabilizes.
2. Introduce smoke tests that run the packaged extension in VS Code’s extension host to catch configuration regressions early.
3. Monitor test telemetry over time (execution duration, flaky counts) to spot performance drift introduced by new instrumentation.
