using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Saxon.Api;

namespace XsltDebugger.DebugAdapter;

public class SaxonEngine : IXsltEngine
{
    private const string DebugNamespace = "urn:xslt-debugger";

    private readonly object _sync = new();
    private List<(string file, int line)> _breakpoints = new();
    private TaskCompletionSource<bool>? _pauseTcs;
    private string _currentStylesheet = string.Empty;
    private bool _nextStepRequested;
    private Processor? _processor;
    private XsltTransformer? _transformer;

    public async Task StartAsync(string stylesheet, string xml, bool stopOnEntry)
    {
        // Log before scheduling to confirm StartAsync was invoked
        if (XsltEngineManager.TraceEnabled)
        {
            XsltEngineManager.NotifyOutput("[trace] Saxon.Start: scheduling");
        }
        await Task.Run(() =>
        {
            try
            {
                if (XsltEngineManager.TraceEnabled)
                {
                    XsltEngineManager.NotifyOutput("[trace] Saxon.Start: entered");
                }
                if (string.IsNullOrWhiteSpace(stylesheet) || string.IsNullOrWhiteSpace(xml))
                {
                    XsltEngineManager.NotifyOutput("Launch request missing stylesheet or xml path.");
                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                _currentStylesheet = NormalizePath(stylesheet);
                var inputPath = NormalizePath(xml);

                // Initialize Saxon processor
                if (XsltEngineManager.TraceEnabled)
                {
                    XsltEngineManager.NotifyOutput("[trace] Saxon.Start: new Processor()");
                }
                _processor = new Processor();

                // Enable XSLT 3.0 features
                _processor.SetProperty("http://saxon.sf.net/feature/xsltVersion", "3.0");

                var compiler = _processor.NewXsltCompiler();
                compiler.XsltLanguageVersion = "3.0";

                // Capture compilation errors
                var errorList = new List<StaticError>();
                try { compiler.ErrorList = errorList; } catch { }
                if (XsltEngineManager.TraceEnabled)
                {
                    XsltEngineManager.NotifyOutput("[trace] Saxon.Start: compiler created");
                }

                XDocument xdoc;
                try
                {
                    if (XsltEngineManager.TraceEnabled)
                    {
                        XsltEngineManager.NotifyOutput($"[trace] Saxon.Start: loading XSLT '{_currentStylesheet}'");
                    }
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
                    XsltCompiledEngine.ValidateEngineCompatibility(stylesheet, XsltEngineType.SaxonNet);
                }
                catch (Exception ex)
                {
                    XsltEngineManager.NotifyOutput($"Engine validation failed: {ex.Message}");
                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                // Instrument the stylesheet for debugging
                var version = XsltCompiledEngine.GetXsltVersion(xdoc.Root);
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"XSLT version detected: {version}");
                }

                // Enable debugging instrumentation only if debugging is enabled
                if (XsltEngineManager.DebugEnabled)
                {
                    if (XsltEngineManager.IsTraceEnabled)
                    {
                        XsltEngineManager.NotifyOutput("[trace] Saxon.Start: register debug extension");
                    }
                    // Register extension function for debugging
                    var debugExtension = new SaxonDebugExtension(this, _currentStylesheet);
                    _processor.RegisterExtensionFunction(debugExtension);

                    EnsureDebugNamespace(xdoc);
                    InstrumentStylesheet(xdoc);
                    if (XsltEngineManager.IsLogEnabled)
                    {
                        XsltEngineManager.NotifyOutput("Debugging enabled for XSLT 2.0/3.0.");
                    }
                }
                else
                {
                    if (XsltEngineManager.IsLogEnabled)
                    {
                        XsltEngineManager.NotifyOutput("Debugging disabled. Running transform without breakpoints.");
                    }
                }

                // Compile the stylesheet
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput("Compiling stylesheet...");
                }
                try
                {
                    using (var reader = xdoc.CreateReader())
                    {
                        if (XsltEngineManager.TraceEnabled)
                        {
                            XsltEngineManager.NotifyOutput("[trace] Saxon.Start: building Xdm tree");
                        }
                        var documentBuilder = _processor.NewDocumentBuilder();
                        var stylesheetDoc = documentBuilder.Build(reader);
                        if (XsltEngineManager.TraceEnabled)
                        {
                            XsltEngineManager.NotifyOutput("[trace] Saxon.Start: compiling");
                        }
                        var executable = compiler.Compile(stylesheetDoc);
                        _transformer = executable.Load();
                        if (XsltEngineManager.IsTraceEnabled)
                        {
                            XsltEngineManager.NotifyOutput("[trace] Saxon.Start: transformer loaded");
                        }
                    }
                    if (XsltEngineManager.IsLogEnabled)
                    {
                        XsltEngineManager.NotifyOutput("Stylesheet compiled successfully.");
                    }
                }
                catch (Exception compileEx)
                {
                    XsltEngineManager.NotifyOutput($"Saxon compilation error: {compileEx.Message}");

                    // Display detailed compilation errors
                    if (errorList.Count > 0)
                    {
                        XsltEngineManager.NotifyOutput($"\n{errorList.Count} compilation error(s) found:");
                        for (int i = 0; i < errorList.Count; i++)
                        {
                            var error = errorList[i];
                            var location = error.ModuleUri != null ? $" at {error.ModuleUri}" : "";
                            var line = error.LineNumber > 0 ? $" line {error.LineNumber}" : "";
                            var col = error.ColumnNumber > 0 ? $" column {error.ColumnNumber}" : "";
                            XsltEngineManager.NotifyOutput($"  [{i + 1}]{location}{line}{col}:");
                            XsltEngineManager.NotifyOutput($"      {error.Message}");
                        }
                    }
                    else if (compileEx.InnerException != null)
                    {
                        XsltEngineManager.NotifyOutput($"Inner exception: {compileEx.InnerException.Message}");
                    }

                    XsltEngineManager.NotifyTerminated(1);
                    return;
                }

                // Load input document
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput("Loading input XML document...");
                }
                var inputBuilder = _processor.NewDocumentBuilder();
                if (XsltEngineManager.IsTraceEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[trace] Saxon.Start: loading input '{inputPath}'");
                }
                var inputDoc = inputBuilder.Build(new Uri(inputPath));

                _transformer.InitialContextNode = inputDoc;

                // Handle stopOnEntry for XSLT 2.0/3.0
                if (stopOnEntry && XsltEngineManager.DebugEnabled)
                {
                    if (XsltEngineManager.IsTraceEnabled)
                    {
                        XsltEngineManager.NotifyOutput("[trace] Saxon.Start: pause on entry");
                    }
                    PauseForBreakpoint(_currentStylesheet, 0, DebugStopReason.Entry, null);
                }

                // Set up output
                var outPath = Path.ChangeExtension(_currentStylesheet, ".out.xml");
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
                }

                using (var writer = new StreamWriter(outPath))
                {
                    var serializer = _processor.NewSerializer(writer);
                    if (XsltEngineManager.TraceEnabled)
                    {
                        XsltEngineManager.NotifyOutput("[trace] Saxon.Start: run()");
                    }
                    _transformer.Run(serializer);
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
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public Task StepOverAsync() => ResumeWithStepAsync();

    public Task StepInAsync() => ResumeWithStepAsync();

    public Task StepOutAsync() => ContinueAsync();

    private Task ResumeWithStepAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = true;
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

    internal void RegisterBreakpointHit(string file, int line, XdmNode? contextNode = null)
    {
        if (line < 0)
        {
            return;
        }

        var normalized = NormalizePath(file);
        var stepRequested = ConsumeStepRequest();

        // Always update the context for evaluation, even when not pausing
        UpdateContext(contextNode);

        if (IsBreakpointHit(normalized, line))
        {
            PauseForBreakpoint(normalized, line, DebugStopReason.Breakpoint, contextNode);
            return;
        }

        if (stepRequested)
        {
            PauseForBreakpoint(normalized, line, DebugStopReason.Step, contextNode);
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

    private XPathNavigator? ConvertSaxonNodeToNavigator(XdmNode? context)
    {
        if (context == null)
        {
            return null;
        }

        try
        {
            // Get the root document node to preserve the full document hierarchy
            // This is essential for absolute XPath expressions like /ShipmentConfirmation/Reference
            var rootNode = context;
            while (rootNode.Parent != null)
            {
                rootNode = (XdmNode)rootNode.Parent;
            }

            // Serialize the entire document to XML string
            using (var stringWriter = new StringWriter())
            {
                var serializer = _processor!.NewSerializer(stringWriter);
                serializer.SetOutputProperty(Serializer.METHOD, "xml");
                serializer.SetOutputProperty(Serializer.OMIT_XML_DECLARATION, "yes");
                serializer.SerializeXdmValue(rootNode);

                var xmlString = stringWriter.ToString();

                // Parse the string into an XmlDocument
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlString);

                // Create navigator from the .NET XmlDocument
                var navigator = xmlDoc.CreateNavigator();

                // Now navigate to the equivalent position of the original context node
                // We'll use the XPath to the original node to position the navigator correctly
                var pathToContext = GetXPathToNode(context);
                if (!string.IsNullOrEmpty(pathToContext) && pathToContext != "/")
                {
                    var contextNavigator = navigator.SelectSingleNode(pathToContext);
                    if (contextNavigator != null)
                    {
                        if (XsltEngineManager.TraceEnabled)
                        {
                            XsltEngineManager.NotifyOutput($"[trace] ConvertSaxonNodeToNavigator: positioned at {pathToContext}");
                        }
                        return contextNavigator;
                    }
                }

                return navigator;
            }
        }
        catch (Exception ex)
        {
            if (XsltEngineManager.TraceEnabled)
            {
                XsltEngineManager.NotifyOutput($"[trace] ConvertSaxonNodeToNavigator: exception - {ex.Message}");
            }
            return null;
        }
    }

    private string GetXPathToNode(XdmNode node)
    {
        // Build the XPath to this node from the root
        var pathParts = new List<string>();
        var current = node;

        while (current != null && current.NodeKind != XmlNodeType.Document)
        {
            if (current.NodeKind == XmlNodeType.Element)
            {
                var name = current.NodeName?.LocalName ?? "";
                // Count preceding siblings with the same name for position predicate
                var position = 1;
                var sibling = current;

                // Move to parent, then iterate children to find position
                var parent = current.Parent as XdmNode;
                if (parent != null)
                {
                    var children = parent.Children().ToList();
                    var index = 0;
                    foreach (var child in children)
                    {
                        if (child is XdmNode childNode && childNode.NodeKind == XmlNodeType.Element)
                        {
                            if (childNode.NodeName?.LocalName == name)
                            {
                                index++;
                                if (childNode.Implementation == current.Implementation)
                                {
                                    position = index;
                                    break;
                                }
                            }
                        }
                    }
                }

                pathParts.Insert(0, $"{name}[{position}]");
            }

            current = current.Parent as XdmNode;
        }

        return pathParts.Count > 0 ? "/" + string.Join("/", pathParts) : "/";
    }

    private void UpdateContext(XdmNode? context)
    {
        if (XsltEngineManager.IsTraceEnabled)
        {
            if (context == null)
            {
                XsltEngineManager.NotifyOutput("[trace] UpdateContext: context is null");
            }
        }

        if (context == null)
        {
            return;
        }

        // Output detailed context information at traceall level (before conversion)
        if (XsltEngineManager.IsTraceAllEnabled)
        {
            var xpath = GetXPathToNode(context);
            var nodeValue = context.StringValue ?? string.Empty;
            var nodeType = context.NodeKind.ToString();
            var nodeName = context.NodeName?.LocalName ?? "(no name)";
            XsltEngineManager.NotifyOutput($"[traceall] Context update detail:\n" +
                $"  Current node: <{nodeName}>\n" +
                $"  XPath: {xpath}\n" +
                $"  Node type: {nodeType}\n" +
                $"  Value: {(nodeValue.Length > 100 ? nodeValue.Substring(0, 100) + "..." : nodeValue)}");
        }

        var navigator = ConvertSaxonNodeToNavigator(context);
        if (XsltEngineManager.IsTraceEnabled)
        {
            if (navigator != null)
            {
                XsltEngineManager.NotifyOutput($"[trace] UpdateContext: converted Saxon node to XPathNavigator, node={navigator.Name}");
            }
            else
            {
                XsltEngineManager.NotifyOutput("[trace] UpdateContext: conversion failed");
            }
        }

        // Update the last context without triggering a stop
        XsltEngineManager.UpdateContext(navigator);
    }

    private void PauseForBreakpoint(string file, int line, DebugStopReason reason, XdmNode? context)
    {
        TaskCompletionSource<bool>? localTcs;
        lock (_sync)
        {
            _pauseTcs = new TaskCompletionSource<bool>();
            localTcs = _pauseTcs;
        }

        // Convert XdmNode to XPathNavigator for debugger context
        var navigator = ConvertSaxonNodeToNavigator(context);
        if (XsltEngineManager.TraceEnabled)
        {
            if (navigator != null)
            {
                XsltEngineManager.NotifyOutput($"[trace] PauseForBreakpoint: converted context, node={navigator.Name}");
            }
            else
            {
                XsltEngineManager.NotifyOutput("[trace] PauseForBreakpoint: no context available");
            }
        }

        XsltEngineManager.NotifyStopped(file, line, reason, navigator);

        try
        {
            localTcs?.Task.Wait();
        }
        catch
        {
            // Ignore interruption
        }
    }

    private bool ConsumeStepRequest()
    {
        lock (_sync)
        {
            if (_nextStepRequested)
            {
                _nextStepRequested = false;
                return true;
            }
        }
        return false;
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
                XsltEngineManager.NotifyOutput($"[trace] instrumented lines (saxon) for '{_currentStylesheet}': [{linesText}]");
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
            var breakCall = new XElement(
                xsltNamespace + "value-of",
                new XAttribute("select", $"dbg:break({line!.Value}, .)"));

            if (CanInsertAsFirstChild(element, isXsltElement))
            {
                element.AddFirst(breakCall);
            }
            else
            {
                element.AddBeforeSelf(breakCall);
            }
        }
    }

    private static bool ShouldInstrument(XElement element, XNamespace xsltNamespace)
    {
        if (element.Parent == null)
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
                // XSLT 2.0/3.0 specific top-level elements (Saxon)
                "function" or "accumulator" or "character-map" or "import-schema" => false,
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

        // Don't instrument elements inside XSLT 2.0/3.0 function bodies
        // Functions should be debuggable but not auto-instrumented
        if (ancestorLocal is "function")
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

        if (XsltCompiledEngine.InlineInstrumentationTargets.Contains(localName))
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

        if (XsltCompiledEngine.ElementsDisallowingChildInstrumentation.Contains(localName))
        {
            return false;
        }

        return true;
    }

    internal static bool IsXsltStylesheet(XElement root)
    {
        var ns = root.Name.NamespaceName;
        return ns == "http://www.w3.org/1999/XSL/Transform" && (root.Name.LocalName == "stylesheet" || root.Name.LocalName == "transform");
    }
}
