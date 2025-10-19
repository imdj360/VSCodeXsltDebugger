# XSLT Function Instrumentation Analysis

## Scope
- Capture how the VS Code extension, debug adapter, and run-time engines collaborate today.
- Document the guardrails that currently block instrumentation inside `xsl:function` for Saxon (XSLT 2.0/3.0).
- Outline the work required to safely support breakpoints within XSLT functions without corrupting results.

## System Overview
- **VS Code front end** – the extension activates the .NET debug adapter and wires launch configurations (`src/extension.ts:1`).
- **Debug adapter** – the adapter speaks DAP, manages session state, and delegates execution to an engine (`XsltDebugger.DebugAdapter/DapServer.cs:1`, `XsltDebugger.DebugAdapter/SessionState.cs:1`).
- **Engines** – two implementations of `IXsltEngine`:
  - `XslCompiledEngine` for the .NET `XslCompiledTransform` path (mainly XSLT 1.0) (`XsltDebugger.DebugAdapter/XsltCompiledEngine.cs:17`).
  - `SaxonEngine` for Saxon-HE, enabling XSLT 2.0/3.0 (`XsltDebugger.DebugAdapter/SaxonEngine.cs:17`).
- **Breakpoint plumbing** – both engines inject `<xsl:value-of select="dbg:break(...)"/>` into the stylesheet so the runtime calls back into the adapter via `dbg:break` (`XsltDebugger.DebugAdapter/SaxonDebugExtension.cs:31`).

## SaxonEngine Instrumentation Flow
- `StartAsync` loads the stylesheet, validates compatibility, registers the `dbg:break` extension, and then instruments the DOM before compilation (`XsltDebugger.DebugAdapter/SaxonEngine.cs:72`).
- `EnsureDebugNamespace` ensures a `dbg` prefix resolves to `urn:xslt-debugger` so generated instructions compile cleanly (`XsltDebugger.DebugAdapter/SaxonEngine.cs:558`).
- `InstrumentStylesheet` gathers candidate elements via `ShouldInstrument`, skips ones without line info, and injects the breakpoint call either as the first child or a preceding sibling based on structural rules (`XsltDebugger.DebugAdapter/SaxonEngine.cs:572`).
- Additional behaviours:
  - Prevents instrumentation inside `xsl:choose` branches unless the branch is itself `xsl:when`/`xsl:otherwise` (`XsltDebugger.DebugAdapter/SaxonEngine.cs:640`).
  - Emits diagnostic `xsl:message` entries for `xsl:for-each` to capture position info (`XsltDebugger.DebugAdapter/SaxonEngine.cs:624`).
- `InstrumentVariables` inserts `xsl:message` calls after safe variable/parameter declarations to surface runtime values, guarded by `IsSafeToInstrumentVariable` to avoid forbidden contexts (`XsltDebugger.DebugAdapter/SaxonEngine.cs:692`, `XsltDebugger.DebugAdapter/SaxonEngine.cs:743`).

## Existing Guardrails Around Functions
- `ShouldInstrument` rejects any `xsl:function` node and any descendant of an `xsl:function`, preventing breakpoint injection inside function bodies (`XsltDebugger.DebugAdapter/SaxonEngine.cs:834`, `XsltDebugger.DebugAdapter/SaxonEngine.cs:867`).
- `InstrumentStylesheet` adds a second check during insertion to skip elements that sit beneath `function`, `variable`, `param`, or `with-param` to handle dynamically constructed candidate lists (`XsltDebugger.DebugAdapter/SaxonEngine.cs:612`).
- `IsSafeToInstrumentVariable` only allows instrumentation inside a function when the variable uses a `select` attribute (single expression) and has no child content, because appending messages within the returned sequence could change the function result (`XsltDebugger.DebugAdapter/SaxonEngine.cs:743`).
- The Saxon extension currently declares the return type of `dbg:break` as `xs:string` and actually returns an empty string (`XsltDebugger.DebugAdapter/SaxonDebugExtension.cs:31`, `XsltDebugger.DebugAdapter/SaxonDebugExtension.cs:109`); inserting it into expression-oriented constructs risks producing unexpected string items.

## Why `xsl:function` Is Risky Today
- XSLT functions must return sequence results that are typically consumed in expressions. Injecting `<xsl:value-of>` inside a function introduces a text node (even if empty), which can alter the sequence or force implicit conversions.
- Many function bodies rely on `xsl:sequence`, `xsl:return`, or expression-only content; additional instructions may violate the content model (e.g., within `xsl:sequence` or streaming constructs).
- Functions might be invoked frequently. Breakpoint instrumentation must avoid repeated allocations or altering tail-call optimization; current instrumentation does not discriminate.

## Work Items to Support Function Instrumentation
1. **Define a non-intrusive breakpoint instruction**
   - Option A: introduce a dedicated helper such as `<xsl:sequence select="dbg:break(...), ()"/>` that ensures the breakpoint returns an empty sequence. This requires updating `SaxonDebugExtension` to advertise an `empty-sequence()` result type.
   - Option B: emit an `xsl:variable` with side-effect-free evaluation (e.g., `<xsl:variable name="dbg_ignore" select="dbg:break(...)" as="empty-sequence()"/>`) and immediately drop it. Needs analysis of Saxon’s static typing rules.
2. **Extend `ShouldInstrument` to whitelist functions**
   - Allow the outer `xsl:function` element to receive entry instrumentation if desired.
   - Permit descendants while still skipping sensitive constructs (e.g., attributes, `xsl:sequence` bodies). Expect additional filtering similar to `ElementsDisallowingChildInstrumentation` to cover XSLT 2.0 constructs.
3. **Refine `InstrumentStylesheet` insertion logic**
   - Provide alternate insertion routines when a target or its ancestors reside inside a function, ensuring we use the new helper instruction rather than the default `<xsl:value-of>` (`XsltDebugger.DebugAdapter/SaxonEngine.cs:619`).
   - Consider placing a single breakpoint at the top of each function (`AddFirst`) and relying on expression-level step support for internals.
4. **Variable instrumentation inside functions**
   - Revisit `IsSafeToInstrumentVariable` so that function-scoped variables with content blocks either use the new helper or are explicitly skipped with diagnostics.
5. **Update extension function signature**
   - Adjust argument and return typing in `SaxonDebugExtension` so Saxon’s static checker accepts the new empty-sequence contract, and ensure `Call` still notifies the engine (`XsltDebugger.DebugAdapter/SaxonDebugExtension.cs:53`).
6. **End-to-end validation**
   - Add sample XSLT 3.0 functions in the console test project and automated tests (`XsltDebugger.ConsoleTest/ProgramUsingSaxonEngine.cs:12`).
   - Verify DAP scenarios: stop-on-entry, breakpoint hits, variable scopes, and XPath evaluation within function contexts (`XsltDebugger.DebugAdapter/DapServer.cs:281` for scope handling).

## Open Questions / Follow-Ups
- Should function entry breakpoints be implicit (pause on first instruction) or rely on user-defined breakpoints only?
- Do we need to expose function names in the DAP stack frame presentation to distinguish function scopes?
- How should we handle pure expression functions (single XPath expression) where inserting any node would change semantics—would a compiled `dbg:break()` call inside the expression be sufficient?

