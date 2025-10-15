using System;
using System.Xml.XPath;

namespace XsltDebugger.DebugAdapter;

public class XsltDebugExtension
{
    private readonly XsltCompiledEngine _engine;
    private readonly string _stylesheetPath;

    public XsltDebugExtension(XsltCompiledEngine engine, string stylesheetPath)
    {
        _engine = engine;
        _stylesheetPath = stylesheetPath;
    }

    public string Break(double lineNumber) => BreakInternal(lineNumber, null);

    public string Break(double lineNumber, XPathNodeIterator? context) => BreakInternal(lineNumber, context);

    // XSLT invokes lowercase 'break' for extension functions; expose alias.
    public string @break(double lineNumber) => Break(lineNumber);

    public string @break(double lineNumber, XPathNodeIterator? context) => BreakInternal(lineNumber, context);

    private string BreakInternal(double lineNumber, XPathNodeIterator? context)
    {
        var line = (int)Math.Round(lineNumber);
        var navigator = ExtractNavigator(context);

        if (XsltEngineManager.IsTraceEnabled)
        {
            var nodeName = navigator?.Name ?? "(no context)";
            XsltEngineManager.NotifyOutput($"[trace] Breakpoint hit at {_stylesheetPath}:{line}, context node: {nodeName}");
        }

        if (XsltEngineManager.IsTraceAllEnabled && navigator != null)
        {
            var xpath = GetXPathToNode(navigator);
            var nodeValue = navigator.Value ?? string.Empty;
            var nodeType = navigator.NodeType.ToString();
            XsltEngineManager.NotifyOutput($"[traceall] Breakpoint context detail at {_stylesheetPath}:{line}:\n" +
                $"  Current node: <{navigator.Name}>\n" +
                $"  XPath: {xpath}\n" +
                $"  Node type: {nodeType}\n" +
                $"  Value: {(nodeValue.Length > 100 ? nodeValue.Substring(0, 100) + "..." : nodeValue)}");
        }

        _engine.RegisterBreakpointHit(_stylesheetPath, line, navigator);
        return string.Empty;
    }

    private static string GetXPathToNode(XPathNavigator navigator)
    {
        try
        {
            var pathParts = new System.Collections.Generic.List<string>();
            var current = navigator.Clone();

            while (current.NodeType != System.Xml.XPath.XPathNodeType.Root)
            {
                if (current.NodeType == System.Xml.XPath.XPathNodeType.Element)
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
            return "(xpath unavailable)";
        }
    }

    private static XPathNavigator? ExtractNavigator(XPathNodeIterator? context)
    {
        if (context == null)
        {
            return null;
        }

        try
        {
            var clone = context.Clone();
            if (clone.MoveNext())
            {
                return clone.Current?.Clone();
            }
        }
        catch
        {
            // Ignore extraction failures and fall back to null context.
        }
        return null;
    }
}
