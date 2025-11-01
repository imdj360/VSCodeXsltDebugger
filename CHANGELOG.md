# Change Log

All notable changes to the XSLT Debugger extension will be documented in this file.

## [1.0.0] - 2025

### Added

- Shared XSLT 1.0 instrumentation helper used by both engines so Saxon can now debug XSLT 1.0 stylesheets that do not rely on `msxsl:script`.
- Version-aware Saxon pipeline that switches to the 1.0-safe probes while retaining the existing XSLT 2.0/3.0 instrumentation.
- Integration coverage for the new Saxon 1.0 path (`SaxonEngine_ShouldCaptureVariables_WhenRunningXslt1Stylesheet`) and console smoke tests for both engines.

### Changed

- Reorganised integration samples under `TestData/Integration/xslt/compiled/` and `TestData/Integration/xslt/saxon/` to mirror the engine split.
- XsltCompiledEngine now delegates all 1.0 probe insertion to the shared helper, keeping instrumentation logic in one place.
- Bumped the extension version to `1.0.0` and updated packaging docs to reference the new VSIX build numbers.
- `.gitignore` / `.vscodeignore` now filter generated `out/` folders and `*.out.xml` artifacts across the tree.

### Fixed

- Ensured Saxon 1.0 runs produce the same breakpoint and variable capture behaviour as the compiled engine by reusing the same probe shapes.

## [0.6.0] - 2025

### Added

- Paired `template-entry`/`template-exit` instrumentation in both engines so call depth is tracked reliably.
- Interactive console `StepIntoTest` highlights template markers while stepping through call-template scenarios.
- Dedicated `StepIntoTests` suite covering step-into, step-over, and step-out flows for compiled and Saxon engines (115 tests total).

### Changed

- Step mode controller records the originating stop location to decide when `Step Over` and `Step Out` should halt.
- Saxon instrumentation always emits probes for `xsl:call-template`, even when sibling probes exist, ensuring the line after the call is reachable.
- Documentation references the 0.6.0 VSIX packages and explains the new stepping behaviour.

### Fixed

- `Step Over` no longer runs the Saxon engine to completion after an `xsl:call-template`; execution now pauses on the next statement.
- `Step Out` consistently returns to the caller template thanks to call-depth unwinding and exit probes.

## [0.0.3] - 2025

### Fixed

- Replaced Saxon-HE 10.9.0 with SaxonHE10Net31Api 10.9.15 (community IKVM build)
- Resolved MissingMethodException errors on .NET 8+ by using modern IKVM-compiled Saxon
- Saxon .NET engine now works on all platforms (Windows, macOS, Linux) without Java

### Changed

- Upgraded project to target .NET 8.0 for better compatibility with modern packages
- Removed Saxon Java engine as .NET Saxon works cross-platform
- Engine options now limited to `"compiled"` (XSLT 1.0) and `"saxonnet"` (XSLT 2.0/3.0)

### Improved

- XSLT 2.0/3.0 support now works reliably cross-platform using pure .NET approach
- Uses similar approach as Azure Logic Apps Data Mapper but with SaxonHE10Net31Api
- Documented architecture and function-debugging workflow (README + docs/function-debugging-guidelines.md)
- Enhanced Saxon instrumentation: `dbg:probe="1"` tagging, safe loop messaging, and accumulator guardrails
- Added advanced Saxon integration tests (XSLT 2.0/3.0 samples) to regression suite
- Packaging scripts (`package-*.sh`) now execute unit tests automatically before producing VSIX bundles

### Credits

- Uses Martin Honnen's community IKVM builds under Mozilla Public License 2.0

## [0.0.1] - 2025

### Fixed

- Fixed inline C# compilation when containing `using` statements that conflicted with generated prelude
- XSLT stylesheets with inline C# code can now safely include standard namespaces

### Changed

- Modified `CompileAndCreateExtensionObject` method to dynamically build prelude
- Checks for existing `using` statements in user code and avoids duplicates

### Added

- Initial release with XSLT debugging support
- Breakpoint support for XSLT files
- Variable inspection for XSLT context and variables
- XPath evaluation in current context
- Inline C# scripting support via `msxsl:script`
- Multiple engine support (compiled XSLT 1.0 and Saxon .NET for XSLT 2.0/3.0)
- Configurable log levels: none, log, trace, traceall
- Stop on entry option
- Debug/non-debug execution modes
