using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using System.Xml.XPath;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Saxon.Api;

namespace XsltDebugger.DebugAdapter;

internal enum StepMode
{
    Continue,   // No stepping, only break on breakpoints
    Into,       // Step into templates/function calls
    Over,       // Step over templates/function calls
    Out         // Run until returning from current depth
}

public class XsltCompiledEngine : IXsltEngine
{
    private const string DebugNamespace = "urn:xslt-debugger";

    public static readonly HashSet<string> InlineInstrumentationTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "if",
        "for-each",
        "when",
        "otherwise",
        "element",
        "attribute",
        "comment",
        "processing-instruction",
        // XSLT 2.0/3.0 specific elements
        "iterate",
        "for-each-group",
        "analyze-string",
        "matching-substring",
        "non-matching-substring",
        "try",
        "catch"
    };

    public static readonly HashSet<string> ElementsDisallowingChildInstrumentation = new(StringComparer.OrdinalIgnoreCase)
    {
        "apply-templates",
        "call-template",
        "copy",
        "copy-of",
        "value-of",
        "number",
        "sort",
        "message",
        "text",
        // Structural control element that only allows xsl:when/xsl:otherwise children
        "choose",
        // XSLT 2.0/3.0 specific elements that don't allow child instrumentation
        "next-iteration",
        "break",
        "sequence",
        "perform-sort"
    };

    private readonly object _sync = new();
    private List<(string file, int line)> _breakpoints = new();
    private TaskCompletionSource<bool>? _pauseTcs;
    private string _currentStylesheet = string.Empty;
    private bool _nextStepRequested;
    private StepMode _stepMode = StepMode.Continue;
    private int _callDepth = 0;
    private int _targetDepth = 0;
    private string _currentStopFile = string.Empty;
    private int _currentStopLine = -1;
    private string _stepOriginFile = string.Empty;
    private int _stepOriginLine = -1;

    public async Task StartAsync(string stylesheet, string xml, bool stopOnEntry)
    {
        // Run the compiled engine on a background thread to avoid blocking the DAP message loop
        if (XsltEngineManager.TraceEnabled)
        {
            XsltEngineManager.NotifyOutput("[trace] Compiled.Start: scheduling");
        }
        await Task.Run(() =>
        {
            try
            {
                if (XsltEngineManager.TraceEnabled)
                {
                    XsltEngineManager.NotifyOutput("[trace] Compiled.Start: entered");
                }
                if (string.IsNullOrWhiteSpace(stylesheet) || string.IsNullOrWhiteSpace(xml))
                {
                    XsltEngineManager.NotifyOutput("Launch request missing stylesheet or xml path.");
                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                _currentStylesheet = NormalizePath(stylesheet);
                var inputPath = NormalizePath(xml);
                var xslt = new XslCompiledTransform();

                XDocument xdoc;
                try
                {
                    xdoc = XDocument.Load(stylesheet, LoadOptions.SetLineInfo);
                }
                catch (Exception ex)
                {
                    XsltEngineManager.NotifyOutput($"Failed to load XSLT stylesheet: {ex.Message}");
                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                if (xdoc.Root == null || !IsXsltStylesheet(xdoc.Root))
                {
                    XsltEngineManager.NotifyOutput("The specified file is not a valid XSLT stylesheet.");
                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                // Validate engine compatibility
                try
                {
                    XsltCompiledEngine.ValidateEngineCompatibility(stylesheet, XsltEngineType.Compiled);
                }
                catch (Exception ex)
                {
                    XsltEngineManager.NotifyOutput($"Engine validation failed: {ex.Message}");
                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                // Extract and register stylesheet namespaces for XPath evaluation in watch expressions
                ExtractAndRegisterNamespaces(xdoc);

                // XSLT 1.0 with XslCompiledTransform
                XNamespace msxsl = "urn:schemas-microsoft-com:xslt";
                var scripts = xdoc.Descendants(msxsl + "script").ToList();

                var args = new XsltArgumentList();
                var messageHandler = new CompiledMessageHandler();
                args.XsltMessageEncountered += messageHandler.OnMessageEncountered;

                var extNamespace = xdoc.Root?.GetNamespaceOfPrefix("ext")?.NamespaceName;
                if (!string.IsNullOrWhiteSpace(extNamespace))
                {
                    args.AddExtensionObject(extNamespace, new RoslynEvaluator());
                }
                else
                {
                    var evalNs = "urn:my-ext-eval";
                    args.AddExtensionObject(evalNs, new RoslynEvaluator());
                    xdoc.Root?.SetAttributeValue(XNamespace.Xmlns + "myeval", evalNs);
                }

                foreach (var script in scripts)
                {
                    var language = script.Attribute("language")?.Value ?? "C#";
                    var implements = script.Attribute("implements-prefix")?.Value;
                    var code = script.Value;
                    if (language.IndexOf("c#", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !string.IsNullOrWhiteSpace(code) &&
                        !string.IsNullOrWhiteSpace(implements))
                    {
                        var extensionObj = CompileAndCreateExtensionObject(code);
                        var ns = xdoc.Root?.GetNamespaceOfPrefix(implements)?.NamespaceName;
                        var namespaceUri = !string.IsNullOrWhiteSpace(ns) ? ns : $"urn:my-ext-{implements}";
                        args.AddExtensionObject(namespaceUri, extensionObj);
                    }
                }

                if (scripts.Count > 0)
                {
                    xdoc.Descendants(msxsl + "script").Remove();
                }

                EnsureDebugNamespace(xdoc);
                InstrumentStylesheet(xdoc);
                InstrumentVariables(xdoc);

                // DEBUG: Save instrumented XSLT for inspection
                if (XsltEngineManager.IsTraceEnabled)
                {
                    var debugPath = Path.Combine(Path.GetDirectoryName(_currentStylesheet) ?? ".", "out",
                        Path.GetFileNameWithoutExtension(_currentStylesheet) + ".instrumented.xslt");
                    Directory.CreateDirectory(Path.GetDirectoryName(debugPath) ?? ".");
                    xdoc.Save(debugPath);
                    XsltEngineManager.NotifyOutput($"[trace] Saved instrumented XSLT to: {debugPath}");
                }

                var settings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
                args.AddExtensionObject(DebugNamespace, new XsltDebugExtension(this, _currentStylesheet));

                using (var reader = xdoc.CreateReader())
                {
                    xslt.Load(reader, settings, new XmlUrlResolver());
                }

                if (stopOnEntry && XsltEngineManager.DebugEnabled)
                {
                    PauseForBreakpoint(_currentStylesheet, 0, DebugStopReason.Entry, null);
                }

                using var xmlReader = XmlReader.Create(inputPath);

                var stylesheetDir = Path.GetDirectoryName(_currentStylesheet) ?? Directory.GetCurrentDirectory();
                var outDir = Path.Combine(stylesheetDir, "out");
                Directory.CreateDirectory(outDir);

                var stylesheetFileName = Path.GetFileNameWithoutExtension(_currentStylesheet);
                var outPath = Path.Combine(outDir, $"{stylesheetFileName}.out.xml");

                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
                }
                if (string.IsNullOrWhiteSpace(outPath))
                {
                    XsltEngineManager.NotifyOutput("Output path for transform is empty; skipping write.");
                }
                else
                {
                    using var fs = File.Create(outPath);
                    using var writer = XmlWriter.Create(fs, xslt.OutputSettings ?? new XmlWriterSettings { Indent = true });
                    xslt.Transform(xmlReader, args, writer);
                }
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput("Transform completed successfully.");
                }
                XsltEngineManager.NotifyTerminated(0);
            }
            catch (Exception ex)
            {
                XsltEngineManager.NotifyOutput($"Transform failed: {ex}");
                XsltEngineManager.NotifyTerminated(1);
            }
            finally
            {
                lock (_sync)
                {
                    _pauseTcs?.TrySetResult(true);
                    _pauseTcs = null;
                }
            }
        });
    }

    public Task ContinueAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = false;
            _stepMode = StepMode.Continue;
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public Task StepOverAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = true;
            _stepMode = StepMode.Over;
            _targetDepth = _callDepth;
            _stepOriginFile = _currentStopFile;
            _stepOriginLine = _currentStopLine;
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public Task StepInAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = true;
            _stepMode = StepMode.Into;
            _targetDepth = _callDepth;
            _stepOriginFile = string.Empty;
            _stepOriginLine = -1;
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public Task StepOutAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = true;
            _stepMode = StepMode.Out;
            _targetDepth = _callDepth - 1; // Stop when we return to parent depth
            _stepOriginFile = _currentStopFile;
            _stepOriginLine = _currentStopLine;
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public void SetBreakpoints(IEnumerable<(string file, int line)> bps)
    {
        var normalized = new List<(string file, int line)>();
        foreach (var bp in bps)
        {
            var f = NormalizePath(bp.file);
            normalized.Add((f, bp.line));
        }
        _breakpoints = normalized;
    }

    internal void RegisterBreakpointHit(string file, int line, XPathNavigator? contextNode = null, bool isTemplateEntry = false, bool isTemplateExit = false)
    {
        if (line < 0)
        {
            return;
        }

        var normalized = NormalizePath(file);

        // Track call depth for template entry/exit
        lock (_sync)
        {
            if (isTemplateEntry)
            {
                _callDepth++;
            }
            else if (isTemplateExit)
            {
                _callDepth = Math.Max(0, _callDepth - 1);
            }
        }

        // Always update the context for evaluation, even when not pausing
        var clonedContext = CloneContextNavigator(contextNode);
        XsltEngineManager.UpdateContext(clonedContext);

        // Track current line for inline C# method logging
        XsltEngineManager.UpdateCurrentLine(normalized, line);

        // Check if we hit a user-set breakpoint
        if (!isTemplateExit && IsBreakpointHit(normalized, line))
        {
            PauseForBreakpoint(normalized, line, DebugStopReason.Breakpoint, clonedContext);
            return;
        }

        // Check if we should stop based on step mode
        if (ShouldStopForStep(normalized, line, isTemplateExit))
        {
            PauseForBreakpoint(normalized, line, DebugStopReason.Step, clonedContext);
        }
    }

    private bool ShouldStopForStep(string file, int line, bool isTemplateExit)
    {
        lock (_sync)
        {
            if (!_nextStepRequested)
            {
                return false;
            }

            var shouldStop = false;

            switch (_stepMode)
            {
                case StepMode.Continue:
                    shouldStop = false;
                    break;

                case StepMode.Into:
                    // Stop at any line (including deeper calls)
                    shouldStop = !isTemplateExit;
                    break;

                case StepMode.Over:
                    // Stop once we return to the original depth, but skip synthetic template exits
                    shouldStop = !isTemplateExit && _callDepth <= _targetDepth &&
                        (!string.Equals(file, _stepOriginFile, StringComparison.OrdinalIgnoreCase) || line != _stepOriginLine);
                    break;

                case StepMode.Out:
                    // Stop when we've returned to or above the original depth (allow template exit to satisfy step out)
                    shouldStop = _callDepth <= _targetDepth;
                    break;

                default:
                    shouldStop = false;
                    break;
            }

            if (shouldStop)
            {
                _nextStepRequested = false;
            }

            return shouldStop;
        }
    }

    private bool IsBreakpointHit(string file, int line)
    {
        foreach (var bp in _breakpoints)
        {
            if (string.Equals(bp.file, file, StringComparison.OrdinalIgnoreCase) && bp.line == line)
            {
                return true;
            }
        }
        return false;
    }

    private void PauseForBreakpoint(string file, int line, DebugStopReason reason, XPathNavigator? context)
    {
        if (!XsltEngineManager.DebugEnabled)
        {
            return;
        }

        TaskCompletionSource<bool>? localTcs;
        lock (_sync)
        {
            _pauseTcs = new TaskCompletionSource<bool>();
            localTcs = _pauseTcs;
        }

        XsltEngineManager.NotifyStopped(file, line, reason, context);

        lock (_sync)
        {
            _currentStopFile = file;
            _currentStopLine = line;
        }

        try
        {
            localTcs?.Task.Wait();
        }
        catch
        {
            // Ignore interruption
        }
    }


    private static string NormalizePath(string path)
    {
        var result = path ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(result))
        {
            if (result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { result = new Uri(result).LocalPath; } catch { }
            }
            try { result = Path.GetFullPath(result); } catch { }
        }
        return result;
    }

    private static void ExtractAndRegisterNamespaces(XDocument xdoc)
    {
        if (xdoc.Root == null)
        {
            return;
        }

        var namespaces = new Dictionary<string, string>();

        // Extract all namespace declarations from the root element
        foreach (var attr in xdoc.Root.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                var prefix = attr.Name.LocalName == "xmlns" ? string.Empty : attr.Name.LocalName;
                var uri = attr.Value;

                // Skip XSLT namespace and debug namespace
                if (uri != "http://www.w3.org/1999/XSL/Transform" &&
                    uri != DebugNamespace)
                {
                    // For default namespace (no prefix), also register with "default" prefix
                    // This allows XPath expressions to use "default:ElementName" syntax
                    if (string.IsNullOrEmpty(prefix))
                    {
                        namespaces[prefix] = uri;
                        namespaces["default"] = uri;
                    }
                    else
                    {
                        namespaces[prefix] = uri;
                    }
                }
            }
        }

        XsltEngineManager.RegisterStylesheetNamespaces(namespaces);
    }

    private void EnsureDebugNamespace(XDocument doc)
    {
        if (doc.Root == null)
        {
            return;
        }

        var existing = doc.Root.GetNamespaceOfPrefix("dbg");
        if (existing == null || existing.NamespaceName != DebugNamespace)
        {
            doc.Root.SetAttributeValue(XNamespace.Xmlns + "dbg", DebugNamespace);
        }
    }

    private void InstrumentStylesheet(XDocument doc)
    {
        if (doc.Root == null)
        {
            return;
        }

        var xsltNamespace = doc.Root.Name.Namespace;
        var candidates = doc
            .Descendants()
            .Where(e => ShouldInstrument(e, xsltNamespace))
            .Select(e => (Element: e, Line: GetLineNumber(e)))
            .Where(tuple => tuple.Line.HasValue)
            .ToList();

        if (XsltEngineManager.TraceEnabled)
        {
            try
            {
                var linesText = string.Join(",", candidates.Select(c => c.Line!.Value).Distinct().OrderBy(x => x));
                XsltEngineManager.NotifyOutput($"[trace] instrumented lines (compiled) for '{_currentStylesheet}': [{linesText}]");
            }
            catch { }
        }

        foreach (var (element, line) in candidates)
        {
            if (element.Parent == null)
            {
                continue;
            }

            var isXsltElement = element.Name.Namespace == xsltNamespace;
            var localName = element.Name.LocalName;

            // Check if this is a for-each element (needs special position logging)
            var isForEach = isXsltElement && string.Equals(localName, "for-each", StringComparison.OrdinalIgnoreCase);

            // Check if this is a named template (for step-into support)
            var isNamedTemplate = isXsltElement &&
                                  string.Equals(localName, "template", StringComparison.OrdinalIgnoreCase) &&
                                  element.Attribute("name") != null;

            // Create breakpoint call with template-entry marker if it's a named template
            var breakCall = isNamedTemplate
                ? new XElement(xsltNamespace + "value-of",
                    new XAttribute("select", $"dbg:break({line!.Value}, ., 'template-entry')"))
                : new XElement(xsltNamespace + "value-of",
                    new XAttribute("select", $"dbg:break({line!.Value}, .)"));

            // Create for-each position logging message (XSLT 1.0 compatible)
            XElement? forEachMessage = null;
            if (isForEach)
            {
                var selectAttr = element.Attribute("select")?.Value ?? "(none)";

                // XSLT 1.0 compatible: use xsl:text and xsl:value-of inside xsl:message
                // Cannot use 'select' attribute on xsl:message in XSLT 1.0 (only available in XSLT 3.0)
                forEachMessage = new XElement(xsltNamespace + "message",
                    new XElement(xsltNamespace + "text", $"[DBG] for-each line={line!.Value} select={selectAttr} pos="),
                    new XElement(xsltNamespace + "value-of", new XAttribute("select", "position()"))
                );
            }

            var parent = element.Parent;
            var parentIsXslt = parent?.Name.Namespace == xsltNamespace;

            if (parentIsXslt && string.Equals(parent!.Name.LocalName, "choose", StringComparison.OrdinalIgnoreCase))
            {
                if (isXsltElement && (string.Equals(element.Name.LocalName, "when", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(element.Name.LocalName, "otherwise", StringComparison.OrdinalIgnoreCase)))
                {
                    element.AddFirst(breakCall);
                }
                continue;
            }

            // Special handling for for-each elements
            // Must insert AFTER any xsl:sort elements
            if (isForEach)
            {
                // Find the last xsl:sort element
                var lastSort = element.Elements()
                    .Where(e => e.Name.Namespace == xsltNamespace &&
                               string.Equals(e.Name.LocalName, "sort", StringComparison.OrdinalIgnoreCase))
                    .LastOrDefault();

                if (lastSort != null)
                {
                    // Insert after the last sort
                    // AddAfterSelf inserts immediately after, so we add in reverse order
                    // Want: <sort/> <breakCall/> <message/>
                    // So: add message first (goes after sort), then add breakCall (goes between sort and message)
                    if (forEachMessage != null)
                    {
                        lastSort.AddAfterSelf(forEachMessage);
                    }
                    lastSort.AddAfterSelf(breakCall);
                }
                else
                {
                    // No sorts, insert as first child
                    // AddFirst inserts at position 0, so we add in reverse order
                    // Want: <breakCall/> <message/>
                    // So: add message first (position 0), then breakCall (new position 0, pushing message to 1)
                    if (forEachMessage != null)
                    {
                        element.AddFirst(forEachMessage);
                    }
                    element.AddFirst(breakCall);
                }
            }
            // Special handling for named templates with params/variables
            // In XSLT 1.0, ALL params and variables must come first
            else if (isNamedTemplate)
            {
                // Find the last param/variable in this template
                var lastParamOrVar = element.Elements()
                    .Where(e => e.Name.Namespace == xsltNamespace &&
                               (e.Name.LocalName == "param" || e.Name.LocalName == "variable"))
                    .LastOrDefault();

                if (lastParamOrVar != null)
                {
                    // Insert the template-entry breakpoint AFTER all params/variables
                    lastParamOrVar.AddAfterSelf(breakCall);
                }
                else
                {
                    // No params or variables, safe to insert as first child
                    element.AddFirst(breakCall);
                }
            }
            else if (CanInsertAsFirstChild(element, isXsltElement))
            {
                element.AddFirst(breakCall);
            }
            else
            {
                element.AddBeforeSelf(breakCall);
            }

            if (isNamedTemplate)
            {
                EnsureTemplateExitBreakpoint(element, line!.Value, xsltNamespace);
            }
        }
    }

    private static void EnsureTemplateExitBreakpoint(XElement templateElement, int lineNumber, XNamespace xsltNamespace)
    {
        var exitSelect = $"dbg:break({lineNumber}, ., 'template-exit')";

        var existing = templateElement
            .Elements()
            .FirstOrDefault(e =>
                e.Name.Namespace == xsltNamespace &&
                string.Equals(e.Name.LocalName, "value-of", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Attribute("select")?.Value, exitSelect, StringComparison.Ordinal));

        if (existing != null)
        {
            return;
        }

        var exitCall = new XElement(
            xsltNamespace + "value-of",
            new XAttribute("select", exitSelect));

        templateElement.Add(exitCall);
    }

    private static XPathNavigator? CloneContextNavigator(XPathNavigator? context)
    {
        if (context == null)
        {
            return null;
        }

        try
        {
            var originalClone = context.Clone();
            var pathToContext = GetXPathToNavigator(originalClone);

            // Move to the document root and capture the XML backing the navigator
            originalClone.MoveToRoot();
            if (!originalClone.MoveToFirstChild())
            {
                return context.Clone();
            }

            while (originalClone.NodeType != XPathNodeType.Element)
            {
                if (!originalClone.MoveToNext())
                {
                    return context.Clone();
                }
            }

            var xml = originalClone.OuterXml;
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);

            var navigator = xmlDoc.CreateNavigator();
            if (navigator == null)
            {
                return context.Clone();
            }

            if (!string.IsNullOrEmpty(pathToContext) && pathToContext != "/")
            {
                var nsManager = BuildNamespaceManager(xmlDoc);
                var positioned = navigator.SelectSingleNode(pathToContext, nsManager);
                if (positioned != null)
                {
                    return positioned.Clone();
                }
            }

            return navigator;
        }
        catch
        {
            try
            {
                return context.Clone();
            }
            catch
            {
                return null;
            }
        }
    }

    private static XmlNamespaceManager BuildNamespaceManager(XmlDocument xmlDoc)
    {
        var manager = new XmlNamespaceManager(xmlDoc.NameTable);
        if (xmlDoc.DocumentElement == null)
        {
            return manager;
        }

        try
        {
            var navigator = xmlDoc.CreateNavigator();
            if (navigator != null && navigator.MoveToFirstChild())
            {
                foreach (var kvp in navigator.GetNamespacesInScope(XmlNamespaceScope.All))
                {
                    var prefix = kvp.Key ?? string.Empty;
                    try { manager.AddNamespace(prefix, kvp.Value); }
                    catch { /* ignore duplicates */ }
                }
            }
        }
        catch
        {
            // Ignore namespace extraction issues; fall back to empty manager
        }

        return manager;
    }

    private static string GetXPathToNavigator(XPathNavigator navigator)
    {
        try
        {
            var pathParts = new List<string>();
            var current = navigator.Clone();

            while (current.NodeType != XPathNodeType.Root)
            {
                switch (current.NodeType)
                {
                    case XPathNodeType.Element:
                    {
                        var position = 1;
                        var sibling = current.Clone();
                        while (sibling.MoveToPrevious())
                        {
                            if (sibling.Name == current.Name)
                            {
                                position++;
                            }
                        }

                        pathParts.Insert(0, $"{current.Name}[{position}]");
                        break;
                    }
                    case XPathNodeType.Attribute:
                    {
                        pathParts.Insert(0, $"@{current.Name}");
                        break;
                    }
                    case XPathNodeType.Text:
                    {
                        pathParts.Insert(0, "text()");
                        break;
                    }
                    default:
                        pathParts.Insert(0, current.NodeType.ToString());
                        break;
                }

                if (!current.MoveToParent())
                {
                    break;
                }
            }

            return pathParts.Count > 0 ? "/" + string.Join("/", pathParts) : "/";
        }
        catch
        {
            return "/";
        }
    }

    private static bool ShouldInstrument(XElement element, XNamespace xsltNamespace)
    {
        if (element.Parent == null)
        {
            return false;
        }

        // Exclude any elements inside xsl:message to avoid instrumentation conflicts
        if (element.Ancestors().Any(a => a.Name.Namespace == xsltNamespace && a.Name.LocalName is "message"))
        {
            return false;
        }

        if (element.Name.Namespace == xsltNamespace)
        {
            var localName = element.Name.LocalName;
            return localName switch
            {
                "stylesheet" or "transform" => false,
                "attribute-set" or "decimal-format" or "import" or "include" or "key" or "namespace-alias" or "output" or "preserve-space" or "strip-space" => false,
                "param" or "variable" or "with-param" => false,
                // Exclude xsl:message to avoid instrumentation conflicts
                "message" => false,
                // Exclude xsl:sort because it must be the first child of xsl:for-each
                "sort" => false,
                _ => true
            };
        }

        var nearestXsltAncestor = element.Ancestors().FirstOrDefault(a => a.Name.Namespace == xsltNamespace);
        if (nearestXsltAncestor == null)
        {
            return false;
        }

        var ancestorLocal = nearestXsltAncestor.Name.LocalName;
        if (ancestorLocal is "stylesheet" or "transform")
        {
            return false;
        }

        return true;
    }

    private static int? GetLineNumber(XElement element)
    {
        if (element is IXmlLineInfo info && info.HasLineInfo())
        {
            return info.LineNumber;
        }
        return null;
    }

    private static bool CanInsertAsFirstChild(XElement element, bool isXsltElement)
    {
        if (element == null)
        {
            return false;
        }

        if (!isXsltElement)
        {
            return false;
        }

        var parent = element.Parent;
        var localName = element.Name.LocalName;

        if (InlineInstrumentationTargets.Contains(localName))
        {
            return true;
        }

        if (parent != null)
        {
            var parentLocal = parent.Name.LocalName;
            if (string.Equals(parentLocal, "stylesheet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parentLocal, "transform", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parentLocal, "choose", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (element.IsEmpty)
        {
            return false;
        }

        if (ElementsDisallowingChildInstrumentation.Contains(localName))
        {
            return false;
        }

        return true;
    }

    private static void InstrumentVariables(XDocument doc)
    {
        if (doc.Root == null)
        {
            return;
        }

        var xsltNamespace = doc.Root.Name.Namespace;

        // Find all xsl:variable and xsl:param elements
        var variables = doc
            .Descendants()
            .Where(e => e.Name.Namespace == xsltNamespace &&
                       (e.Name.LocalName == "variable" || e.Name.LocalName == "param"))
            .Where(e => e.Attribute("name") != null)
            .Where(e => !IsTopLevelDeclaration(e, xsltNamespace))
            .ToList();

        if (XsltEngineManager.IsLogEnabled)
        {
            XsltEngineManager.NotifyOutput($"[debug] Instrumenting {variables.Count} variable(s) for debugging");
        }

        // Group variables by parent to handle templates correctly
        // In XSLT 1.0, ALL params and variables must come first in a template
        var groupedByParent = variables.GroupBy(v => v.Parent).ToList();

        foreach (var group in groupedByParent)
        {
            var parent = group.Key;
            if (parent == null) continue;

            // Collect safe variables in this group
            var safeVars = new List<(XElement element, string name)>();
            foreach (var variable in group)
            {
                var varName = variable.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(varName))
                {
                    continue;
                }

                if (!IsSafeToInstrumentVariable(variable, xsltNamespace))
                {
                    if (XsltEngineManager.IsLogEnabled)
                    {
                        XsltEngineManager.NotifyOutput($"[debug]   Skipped unsafe instrumentation: ${varName}");
                    }
                    continue;
                }

                safeVars.Add((variable, varName));
            }

            if (safeVars.Count == 0) continue;

            // Find the last param/variable in this parent
            // This ensures we insert AFTER all params/variables, not between them
            var lastParamOrVar = parent.Elements()
                .Where(e => e.Name.Namespace == xsltNamespace &&
                           (e.Name.LocalName == "param" || e.Name.LocalName == "variable"))
                .LastOrDefault();

            if (lastParamOrVar == null) continue;

            // Insert debug messages for each variable, but AFTER the last param/variable
            foreach (var (variable, varName) in safeVars)
            {
                var debugMessage = new XElement(
                    xsltNamespace + "message",
                    new XElement(xsltNamespace + "text", $"[DBG] {varName} "),
                    new XElement(xsltNamespace + "value-of", new XAttribute("select", $"${varName}"))
                );

                // Insert after the last param/variable, not after each one
                lastParamOrVar.AddAfterSelf(debugMessage);
                // Update lastParamOrVar so next message goes after this one
                lastParamOrVar = debugMessage;

                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[debug]   Instrumented variable: ${varName}");
                }
            }
        }
    }

    private static bool IsSafeToInstrumentVariable(XElement variable, XNamespace xsltNamespace)
    {
        var parent = variable.Parent;
        if (parent == null)
        {
            return false;
        }

        if (HasFragileAncestor(variable, xsltNamespace))
        {
            return false;
        }

        var parentLocalName = parent.Name.LocalName;
        var parentIsXslt = parent.Name.Namespace == xsltNamespace;

        if (parentIsXslt)
        {
            switch (parentLocalName)
            {
                case "attribute":
                case "comment":
                case "processing-instruction":
                case "namespace":
                case "output":
                case "key":
                case "decimal-format":
                case "character-map":
                case "variable":
                case "param":
                case "with-param":
                    return false;
            }
        }

        // Don't instrument variables inside xsl:attribute
        var attributeAncestor = variable.Ancestors()
            .FirstOrDefault(a => a.Name.Namespace == xsltNamespace &&
                                 a.Name.LocalName == "attribute");
        if (attributeAncestor != null)
        {
            return false;
        }

        return true;
    }

    private static bool IsTopLevelDeclaration(XElement element, XNamespace xsltNamespace)
    {
        // Check if this is a top-level variable/param (direct child of stylesheet/transform)
        var parent = element.Parent;
        if (parent != null && parent.Name.Namespace == xsltNamespace)
        {
            var parentLocal = parent.Name.LocalName;
            if (parentLocal == "stylesheet" || parentLocal == "transform")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFragileAncestor(XElement element, XNamespace xsltNamespace)
    {
        // For XSLT 1.0, we have fewer fragile contexts than XSLT 2.0/3.0
        // Main concerns: attribute generation, key/sort contexts
        return element.Ancestors()
            .Any(a => a.Name.Namespace == xsltNamespace &&
                      (a.Name.LocalName == "attribute" ||
                       a.Name.LocalName == "comment" ||
                       a.Name.LocalName == "processing-instruction" ||
                       a.Name.LocalName == "namespace"));
    }

    private static List<string> ExtractUsingStatements(string code)
    {
        var usings = new List<string>();
        var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using ", StringComparison.Ordinal))
            {
                usings.Add(trimmed);
            }
        }
        return usings;
    }

    private object CompileAndCreateExtensionObject(string code)
    {
        var classCode = code.Contains("class", StringComparison.OrdinalIgnoreCase)
            ? code
            : $"public class InlineXsltExt {{ {code} }}";

        // Build prelude with only the using statements not already present in the code
        var requiredUsings = new[]
        {
            "using System;",
            "using System.Collections;",
            "using System.Collections.Generic;",
            "using System.Globalization;",
            "using System.Linq;",
            "using System.Text;",
            "using System.Xml;",
            "using System.Xml.XPath;",
            "using System.Xml.Xsl;",
            "using XsltDebugger.DebugAdapter;",
            "using static XsltDebugger.DebugAdapter.InlineXsltLogger;"
        };

        var existingUsings = ExtractUsingStatements(classCode);
        var prelude = string.Join(Environment.NewLine,
            requiredUsings.Where(u => !existingUsings.Contains(u, StringComparer.OrdinalIgnoreCase)));

        var sourceCode = string.Concat(prelude, Environment.NewLine, classCode);

        // Instrument inline C# methods when debugging is enabled
        if (XsltEngineManager.DebugEnabled)
        {
            sourceCode = InlineCSharpInstrumenter.Instrument(sourceCode);
        }

        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(sourceCode);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(XsltArgumentList).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Xml.XmlDocument).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(XsltEngineManager).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(InlineXsltLogger).Assembly.Location)
        };

        var coreLibDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrEmpty(coreLibDirectory))
        {
            var runtimeAssemblyPath = Path.Combine(coreLibDirectory, "System.Runtime.dll");
            if (File.Exists(runtimeAssemblyPath))
            {
                references.Add(MetadataReference.CreateFromFile(runtimeAssemblyPath));
            }
        }
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "InlineXsltExtAssembly",
            new[] { syntaxTree },
            references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            throw new Exception("Failed to compile inline C# script: " + string.Join("\n", result.Diagnostics));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var allTypes = assembly.GetTypes();
        var usableTypes = allTypes
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                !t.IsGenericType &&
                !t.IsGenericTypeDefinition &&
                !Attribute.IsDefined(t, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
            .ToList();

        Type? chosen = usableTypes
            .FirstOrDefault(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length > 0)
            ?? usableTypes.FirstOrDefault(t => !t.IsNested)
            ?? usableTypes.FirstOrDefault()
            ?? allTypes.FirstOrDefault(t => !t.IsAbstract && !t.IsInterface);

        if (chosen == null)
        {
            throw new Exception("Compiled assembly does not contain a usable type for XSLT extension.");
        }

        return Activator.CreateInstance(chosen) ?? throw new Exception("Failed to instantiate compiled extension type.");
    }

    public static bool IsXsltStylesheet(XElement root)
    {
        var ns = root.Name.NamespaceName;
        return ns == "http://www.w3.org/1999/XSL/Transform" && (root.Name.LocalName == "stylesheet" || root.Name.LocalName == "transform");
    }

    public static decimal GetXsltVersion(XElement root)
    {
        if (!IsXsltStylesheet(root))
        {
            return 0;
        }

        var versionAttr = root.Attribute("version");
        if (versionAttr != null && decimal.TryParse(versionAttr.Value, out var version))
        {
            return version;
        }

        // Default version if not specified
        return 1.0m;
    }

    public static bool HasInlineCSharp(XDocument doc)
    {
        var msxsl = "urn:schemas-microsoft-com:xslt";
        return doc.Descendants(XName.Get("script", msxsl)).Any();
    }

    public static void ValidateEngineCompatibility(string stylesheet, XsltEngineType engineType)
    {
        XDocument xdoc;
        try
        {
            xdoc = XDocument.Load(stylesheet, LoadOptions.SetLineInfo);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to load XSLT stylesheet for validation: {ex.Message}");
        }

        if (xdoc.Root == null || !IsXsltStylesheet(xdoc.Root))
        {
            throw new Exception("The specified file is not a valid XSLT stylesheet.");
        }

        var version = GetXsltVersion(xdoc.Root);
        var hasInlineCSharp = HasInlineCSharp(xdoc);

        if (engineType == XsltEngineType.Compiled)
        {
            // Compiled engine supports XSLT 1.0 and inline C#
            // Also usable for XSLT 2.0/3.0 with limited feature support
            if (version >= 2.0m)
            {
                XsltEngineManager.NotifyOutput($"Warning: Using 'compiled' engine with XSLT {version}. XSLT 2.0+ features may not be fully supported. Consider using 'saxon' engine for XSLT 2.0/3.0.");
            }
        }
        else if (engineType == XsltEngineType.SaxonNet)
        {
            // Saxon engine supports XSLT 2.0/3.0 but not inline C#
            if (version < 2.0m)
            {
                XsltEngineManager.NotifyOutput($"Warning: Using Saxon engine with XSLT {version}. Saxon is optimized for XSLT 2.0/3.0. Consider using 'compiled' engine for XSLT 1.0.");
            }

            if (hasInlineCSharp)
            {
                throw new Exception("Saxon engine does not support inline C# scripts. Use 'compiled' engine for stylesheets with msxsl:script elements.");
            }
        }
    }
}
