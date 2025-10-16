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
                    InstrumentVariables(xdoc);
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
                        _transformer.MessageListener2 = new SaxonMessageListener();
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
                var stylesheetDir = Path.GetDirectoryName(_currentStylesheet) ?? Directory.GetCurrentDirectory();
                var outDir = Path.Combine(stylesheetDir, "out");
                Directory.CreateDirectory(outDir);

                var stylesheetFileName = Path.GetFileNameWithoutExtension(_currentStylesheet);
                var outPath = Path.Combine(outDir, $"{stylesheetFileName}.out.xml");

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

                // Debug: show which elements are being instrumented
                foreach (var (element, line) in candidates.Take(20))
                {
                    var elemName = element.Name.LocalName;
                    var parentName = element.Parent?.Name.LocalName ?? "null";
                    XsltEngineManager.NotifyOutput($"[trace]   Line {line}: <{elemName}> (parent: <{parentName}>)");
                }
            }
            catch { }
        }

        foreach (var (element, line) in candidates)
        {
            if (element.Parent == null)
            {
                continue;
            }

            // Double-check: Skip if inside a function or variable (safety check)
            if (element.Ancestors().Any(a => a.Name.Namespace == xsltNamespace &&
                (a.Name.LocalName is "function" or "variable" or "param" or "with-param")))
            {
                continue;
            }

            var isXsltElement = element.Name.Namespace == xsltNamespace;
            var breakCall = new XElement(
                xsltNamespace + "value-of",
                new XAttribute("select", $"dbg:break({line!.Value}, .)"));

            XElement? forEachMessage = null;
            if (isXsltElement && string.Equals(element.Name.LocalName, "for-each", StringComparison.OrdinalIgnoreCase))
            {
                var linePrefix = line.HasValue ? $"line={line.Value} " : string.Empty;
                var selectAttr = element.Attribute("select")?.Value ?? string.Empty;
                var safeSelect = string.IsNullOrWhiteSpace(selectAttr) ? "(none)" : EscapeApostrophes(selectAttr.Trim());
                var messageSelect =
                    $"('[DBG]', 'for-each', concat('{linePrefix}select={safeSelect} ', 'pos=', string(position())))";
                forEachMessage = new XElement(
                    xsltNamespace + "message",
                    new XAttribute("select", messageSelect));
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

            if (CanInsertAsFirstChild(element, isXsltElement))
            {
                if (forEachMessage != null)
                {
                    element.AddFirst(forEachMessage);
                    forEachMessage.AddAfterSelf(breakCall);
                    if (XsltEngineManager.IsLogEnabled && line.HasValue)
                    {
                        XsltEngineManager.NotifyOutput($"[debug]   Instrumented for-each loop with position tracking at line {line.Value}");
                    }
                }
                else
                {
                    element.AddFirst(breakCall);
                }
            }
            else
            {
                if (forEachMessage != null)
                {
                    element.AddBeforeSelf(forEachMessage);
                    forEachMessage.AddAfterSelf(breakCall);
                    if (XsltEngineManager.IsLogEnabled && line.HasValue)
                    {
                        XsltEngineManager.NotifyOutput($"[debug]   Instrumented for-each loop with position tracking at line {line.Value}");
                    }
                }
                else
                {
                    element.AddBeforeSelf(breakCall);
                }
            }
        }
    }

    private void InstrumentVariables(XDocument doc)
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

        foreach (var variable in variables)
        {
            var varName = variable.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(varName))
            {
                continue;
            }

            // GUARDRAILS: Check if it's safe to insert xsl:message after this variable
            if (!IsSafeToInstrumentVariable(variable, xsltNamespace))
            {
                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[debug]   Skipped unsafe instrumentation: ${varName}");
                }
                continue;
            }

            // Create debug message: <xsl:message select="('[DBG]', 'varName', string-join(for $x in $varName return string($x), ', '))"/>
            // This handles both single values and sequences
            var debugMessage = new XElement(
                xsltNamespace + "message",
                new XAttribute("select", $"('[DBG]', '{varName}', string-join(for $x in ${varName} return string($x), ', '))")
            );

            // Insert the message right after the variable declaration
            variable.AddAfterSelf(debugMessage);

            if (XsltEngineManager.IsLogEnabled)
            {
                XsltEngineManager.NotifyOutput($"[debug]   Instrumented variable: ${varName}");
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

        var parentLocalName = parent.Name.LocalName;
        var parentIsXslt = parent.Name.Namespace == xsltNamespace;

        // Cannot insert xsl:message in these contexts:
        if (parentIsXslt)
        {
            switch (parentLocalName)
            {
                // Cannot add instructions inside these elements
                case "attribute":
                case "comment":
                case "processing-instruction":
                case "namespace":
                case "output":
                case "key":
                case "decimal-format":
                case "character-map":
                    return false;

                // Cannot add message inside xsl:function body if variable has content
                // But if variable has @select, it's safe to add message after it
                case "function":
                    // If variable has select attribute, it's a single element - safe to add after
                    // If variable has content, adding after might be inside the function result
                    return variable.Attribute("select") != null || !variable.HasElements;

                // In xsl:variable or xsl:param content - not safe
                case "variable":
                case "param":
                case "with-param":
                    return false;
            }
        }

        // Check if we're inside an attribute value template context
        // Variables inside xsl:attribute content need special handling
        var attributeAncestor = variable.Ancestors()
            .FirstOrDefault(a => a.Name.Namespace == xsltNamespace &&
                                a.Name.LocalName == "attribute");
        if (attributeAncestor != null)
        {
            return false;
        }

        // Check if variable is in xsl:sequence - adding message after might break sequence
        if (parentIsXslt && parentLocalName == "sequence")
        {
            return false;
        }

        // Variables inside xsl:analyze-string have strict content model
        if (variable.Ancestors().Any(a => a.Name.Namespace == xsltNamespace &&
                                         a.Name.LocalName == "analyze-string"))
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

        // Don't skip variables inside functions - we want to debug them too!
        // if (element.Ancestors().Any(a => a.Name.Namespace == xsltNamespace &&
        //                                  a.Name.LocalName == "function"))
        // {
        //     return true;
        // }

        return false;
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
        // Check ALL ancestors, not just the nearest one, to handle nested structures
        // like xsl:choose inside xsl:function
        if (element.Ancestors().Any(a => a.Name.Namespace == xsltNamespace && a.Name.LocalName is "function"))
        {
            return false;
        }

        // Don't instrument elements inside variable/param declarations
        // Adding instrumentation inside variable content can inject unwanted values
        if (element.Ancestors().Any(a => a.Name.Namespace == xsltNamespace &&
            a.Name.LocalName is "variable" or "param" or "with-param"))
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

    private static string EscapeApostrophes(string value)
    {
        return value.Replace("'", "''");
    }

    internal static bool IsXsltStylesheet(XElement root)
    {
        var ns = root.Name.NamespaceName;
        return ns == "http://www.w3.org/1999/XSL/Transform" && (root.Name.LocalName == "stylesheet" || root.Name.LocalName == "transform");
    }
}
