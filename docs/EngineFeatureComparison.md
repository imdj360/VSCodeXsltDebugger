# XSLT Debug Engine Comparison

## Engine Profiles

### XsltCompiledEngine
- Executes via .NET `XslCompiledTransform`, delivering full XSLT 1.0 compliance and best-effort support for 2.0+ stylesheets with compatibility warnings.
- Loads and instruments stylesheets on every debug run, saving an `out/<name>.instrumented.xslt` copy when trace logging is enabled.
- Supports inline C# through `msxsl:script`, compiling snippets with Roslyn, auto-registering extension namespaces, and optionally instrumenting generated helper code.

### SaxonEngine
- Builds a Saxon HE/PE processor, enables XSLT 3.0 features, and compiles stylesheets to a reusable executable for full 2.0/3.0 behavior.
- Only instruments stylesheets when debugging is enabled, registering a custom Saxon extension function to surface probes.
- Blocks inline C# by reusing engine validation logic and reports static compilation issues (line/column) via Saxon’s `ErrorList`.

## Shared Debug Surface
- Breakpoints: both inject `dbg:break()` calls, mark template entry/exit, and honor user breakpoints through shared stepping state (`StepMode` with depth tracking).
- Stepping: continue/step-in/step-over/step-out behaviors use identical synchronization and depth logic, pausing when template boundaries or requested lines are reached.
- Watches & Namespaces: both extract stylesheet namespace declarations (adding a synthetic `default` prefix) and register them so watch expressions resolve consistently.
- Pause Context: both convert the current execution node to an `XPathNavigator` clone before notifying the debug adapter, enabling expression evaluation at stop points.
- Message Capture: both parse `[DBG]` prefixed messages into the debugger’s variable store while forwarding ordinary `xsl:message` output to the console.

## Instrumentation Differences

### XsltCompiledEngine Specialties
- Probes are injected as `xsl:value-of` calls to stay XSLT 1.0 compliant and include supplemental `xsl:message` blocks for `xsl:for-each` position reporting.
- Variable instrumentation groups declarations per template, inserting `[DBG]` messages after the final `xsl:param`/`xsl:variable` to preserve 1.0 ordering rules.
- Inline Roslyn extension objects allow breakpoints and logging inside user scripts, providing parity with template-level debugging for hybrid XSLT/C# projects.

### SaxonEngine Specialties
- Probe nodes are created as `xsl:sequence` instructions tagged with `dbg:probe="1"`, allowing the engine to skip reinstrumentation on reentry.
- Maintains extensive “fragile” element lists (e.g., `iterate`, `try/catch`, `accumulator-rule`, `merge-*`) so instrumentation never invalidates XSLT 2.0/3.0 constructs.
- Variable probes emit tuples via `xsl:message select="('[DBG]', ...)"`, leveraging 2.0 expressions (`string-join`) to flatten sequences and capture multi-item variables.
- Serializes Saxon `XdmNode` contexts back into .NET DOMs to give the debugger XPath-compatible navigation without exposing Saxon internals.

## Feature Gap Snapshot

| Feature Area | Only in XsltCompiledEngine | Only in SaxonEngine |
| --- | --- | --- |
| XSLT version focus | Inline C# compatible XSLT 1.0 runtime | Native XSLT 2.0/3.0 execution with Saxon |
| Inline scripting | Compiles `msxsl:script` via Roslyn and instruments generated code | Explicitly blocked; prompts user to switch engines |
| Instrumentation safety | Tailored to 1.0 structural rules and limited fragile contexts | Expansive guards for 2.0/3.0 constructs (accumulators, iterate/try, merge) with `dbg:probe` markers |
| Variable capture | Text/value-of messages per template, respecting 1.0 ordering | Tuple/sequence messages with `string-join` across sequences |
| Compilation diagnostics | Relies on transform exceptions | Surfaces Saxon static errors with module URI, line, and column |

## Selection Guidance
- Choose **XsltCompiledEngine** for XSLT 1.0 stylesheets, projects relying on inline C#, or environments tied to the .NET runtime.
- Choose **SaxonEngine** for XSLT 2.0/3.0 transformations, schema-aware features, or when richer compile-time diagnostics and sequence-aware variable logging are required.
- Maintain separate launch configurations so each stylesheet uses the engine aligned with its language level and extension requirements.
