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
        _engine.RegisterBreakpointHit(_stylesheetPath, line, navigator);
        return string.Empty;
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
