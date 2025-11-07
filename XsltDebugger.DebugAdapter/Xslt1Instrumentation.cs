using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace XsltDebugger.DebugAdapter;

internal static class Xslt1Instrumentation
{
    public static void InstrumentStylesheet(XDocument doc, string stylesheetPath, XNamespace debugNamespace, bool addProbeAttribute)
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
                XsltEngineManager.NotifyOutput($"[trace] instrumented lines (xslt1) for '{stylesheetPath}': [{linesText}]");
            }
            catch
            {
                // Ignore logging issues
            }
        }

        foreach (var (element, line) in candidates)
        {
            if (element.Parent == null)
            {
                continue;
            }

            var isXsltElement = element.Name.Namespace == xsltNamespace;
            var localName = element.Name.LocalName;

            var isForEach = isXsltElement && string.Equals(localName, "for-each", StringComparison.OrdinalIgnoreCase);
            var isNamedTemplate = isXsltElement &&
                                  string.Equals(localName, "template", StringComparison.OrdinalIgnoreCase) &&
                                  element.Attribute("name") != null;

            var breakSelect = isNamedTemplate
                ? $"dbg:break({line!.Value}, ., 'template-entry')"
                : $"dbg:break({line!.Value}, .)";
            var breakCall = new XElement(xsltNamespace + "value-of",
                new XAttribute("select", breakSelect));

            if (addProbeAttribute)
            {
                breakCall.SetAttributeValue(debugNamespace + "probe", "1");
            }

            XElement? forEachMessage = null;
            if (isForEach)
            {
                var selectAttr = element.Attribute("select")?.Value ?? "(none)";
                forEachMessage = new XElement(xsltNamespace + "message",
                    new XElement(xsltNamespace + "text", $"[DBG] for-each line={line!.Value} select={selectAttr} pos="),
                    new XElement(xsltNamespace + "value-of", new XAttribute("select", "position()")));

                if (addProbeAttribute)
                {
                    forEachMessage.SetAttributeValue(debugNamespace + "probe", "1");
                }
            }

            var parent = element.Parent;
            var parentIsXslt = parent?.Name.Namespace == xsltNamespace;

            if (parentIsXslt && string.Equals(parent!.Name.LocalName, "choose", StringComparison.OrdinalIgnoreCase))
            {
                if (isXsltElement &&
                    (string.Equals(element.Name.LocalName, "when", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(element.Name.LocalName, "otherwise", StringComparison.OrdinalIgnoreCase)))
                {
                    element.AddFirst(breakCall);
                }
                continue;
            }

            if (isForEach)
            {
                var lastSort = element.Elements()
                    .Where(e => e.Name.Namespace == xsltNamespace &&
                               string.Equals(e.Name.LocalName, "sort", StringComparison.OrdinalIgnoreCase))
                    .LastOrDefault();

                if (lastSort != null)
                {
                    if (forEachMessage != null)
                    {
                        lastSort.AddAfterSelf(forEachMessage);
                    }
                    lastSort.AddAfterSelf(breakCall);
                }
                else
                {
                    if (forEachMessage != null)
                    {
                        element.AddFirst(forEachMessage);
                    }
                    element.AddFirst(breakCall);
                }
            }
            else if (isNamedTemplate)
            {
                var lastParamOrVar = element.Elements()
                    .Where(e => e.Name.Namespace == xsltNamespace &&
                               (e.Name.LocalName == "param" || e.Name.LocalName == "variable"))
                    .LastOrDefault();

                if (lastParamOrVar != null)
                {
                    lastParamOrVar.AddAfterSelf(breakCall);
                }
                else
                {
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
                EnsureTemplateExitProbe(element, line!.Value, xsltNamespace, debugNamespace, addProbeAttribute);
            }
        }
    }

    public static void InstrumentVariables(XDocument doc, XNamespace debugNamespace, bool addProbeAttribute)
    {
        if (doc.Root == null)
        {
            return;
        }

        var xsltNamespace = doc.Root.Name.Namespace;

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

        var groupedByParent = variables.GroupBy(v => v.Parent).ToList();

        foreach (var group in groupedByParent)
        {
            var parent = group.Key;
            if (parent == null)
            {
                continue;
            }

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

            if (safeVars.Count == 0)
            {
                continue;
            }

            var lastParamOrVar = parent.Elements()
                .Where(e => e.Name.Namespace == xsltNamespace &&
                           (e.Name.LocalName == "param" || e.Name.LocalName == "variable"))
                .LastOrDefault();

            if (lastParamOrVar == null)
            {
                continue;
            }

            foreach (var (_, varName) in safeVars)
            {
                var debugMessage = new XElement(
                    xsltNamespace + "message",
                    new XElement(xsltNamespace + "text", $"[DBG] {varName} "),
                    new XElement(xsltNamespace + "value-of", new XAttribute("select", $"${varName}")));

                if (addProbeAttribute)
                {
                    debugMessage.SetAttributeValue(debugNamespace + "probe", "1");
                }

                lastParamOrVar.AddAfterSelf(debugMessage);
                lastParamOrVar = debugMessage;

                if (XsltEngineManager.IsLogEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[debug]   Instrumented variable: ${varName}");
                }
            }
        }
    }

    private static bool ShouldInstrument(XElement element, XNamespace xsltNamespace)
    {
        if (element.Parent == null)
        {
            return false;
        }

        if (element.Ancestors().Any(a => a.Name.Namespace == xsltNamespace && a.Name.LocalName is "message" or "attribute"))
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
                "message" => false,
                "sort" => false,
                "attribute" => false,  // Don't instrument xsl:attribute itself
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
        if (element == null || !isXsltElement)
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

    private static void EnsureTemplateExitProbe(
        XElement templateElement,
        int lineNumber,
        XNamespace xsltNamespace,
        XNamespace debugNamespace,
        bool addProbeAttribute)
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
            if (addProbeAttribute)
            {
                existing.SetAttributeValue(debugNamespace + "probe", "1");
            }
            return;
        }

        var exitCall = new XElement(
            xsltNamespace + "value-of",
            new XAttribute("select", exitSelect));

        if (addProbeAttribute)
        {
            exitCall.SetAttributeValue(debugNamespace + "probe", "1");
        }

        templateElement.Add(exitCall);
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
            // Check for direct parent contexts that disallow instrumentation
            // Note: "attribute" is also checked by HasFragileAncestor above (defensive programming)
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

        return true;
    }

    private static bool IsTopLevelDeclaration(XElement element, XNamespace xsltNamespace)
    {
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
        return element.Ancestors()
            .Any(a => a.Name.Namespace == xsltNamespace &&
                      (a.Name.LocalName == "attribute" ||
                       a.Name.LocalName == "comment" ||
                       a.Name.LocalName == "processing-instruction" ||
                       a.Name.LocalName == "namespace"));
    }
}
