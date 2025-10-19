# Function Debugging & Instrumentation Checklist

This guide captures the workflow we use to keep XSLT 2.0/3.0 function instrumentation safe, traceable, and testable across both engines.

## 1. Understand the Guardrails
- Review `XsltDebugger.DebugAdapter/SaxonEngine.cs` to learn how candidates are selected (`ShouldInstrument`) and where probes are injected (`InsertProbesAsFirstChild`, `BuildProbesForElement`).
- Functions (`xsl:function`) and other fragile constructs (`xsl:sequence`, accumulators, etc.) are skipped via `FragileAnywhere`. Any change that targets new elements must reassess this list.
- Variable instrumentation relies on `IsSafeToInstrumentVariable`; ensure any new context respects its exclusions (attribute content, analyzers, nested variables, etc.).

## 2. Plan the Instrumentation
- Decide whether the new scenario needs line-based breakpoints, variable tracing, or both.
- For loops or grouping constructs, confirm that sort/instruction ordering rules are preserved (e.g., `xsl:sort` must stay ahead of other instructions).
- For functions, prefer injecting `<xsl:sequence select="dbg:break(...)" dbg:probe="1"/>` to avoid output mutations.
- When emitting helper `xsl:message` instructions, tag them with `dbg:probe="1"` for idempotence.

## 3. Update the Engine
1. Adjust candidate selection (`ShouldInstrument`, `FragileAnywhere`, `ExcludedDirectElements`) as needed.
2. Modify `BuildProbesForElement` to emit any new trace helpers.
3. Tune insertion helpers:
   - `InsertAfterLeadingSorts` keeps `xsl:for-each(-group)` valid.
   - `InsertAfterLeadingDeclarations` keeps `xsl:param`/`xsl:variable` at the top of templates.
4. Ensure variable instrumentation stays safe by extending `IsSafeToInstrumentVariable`.

## 4. Extend Test Coverage
- For new XSLT features, add stylesheets and XML samples under `XsltDebugger.Tests/TestData/Integration`.
- Create integration tests in `SaxonEngineIntegrationTests` (XSLT 2.0/3.0) or `CompiledEngineIntegrationTests` (XSLT 1.0) that:
  - Run the engine end-to-end.
  - Assert key log entries (instrumentation counts, captured variable names).
  - Assert output XML includes expected nodes/values.
- Re-run `dotnet test XsltDebugger.Tests/XsltDebugger.Tests.csproj -v minimal` to verify passing coverage.

## 5. Package & Validate in VS Code
1. Build fresh VSIX archives: `./package-all.sh`.
2. Install the macOS bundle (adjust path for Windows if needed):
   ```
   code --install-extension xsltdebugger-darwin-darwin-arm64-0.0.1.vsix
   ```
3. Launch a debug session with the new stylesheet and confirm:
   - Breakpoints hit inside the expected scopes.
   - Variables appear in the VARIABLES pane.
   - `dbg:probe="1"` prevents duplicate instrumentation on multiple runs.

## 6. Checklist Before Merging
- [ ] Saxon engine changes avoid fragile contexts (`FragileAnywhere` updated).
- [ ] For-each/for-each-group still honor sort ordering.
- [ ] Tests added/updated for new feature coverage.
- [ ] `dotnet test ...` succeeds locally.
- [ ] VSIX built and sanity tested in VS Code.

Keeping this loop tight makes it easy to extend debugger coverage (e.g., future function scenarios) without destabilising existing instrumentation. Update this document whenever we support additional constructs or adjust the guardrail strategy.
