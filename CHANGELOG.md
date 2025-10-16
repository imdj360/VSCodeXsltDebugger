# Change Log

All notable changes to the XSLT Debugger extension will be documented in this file.

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
