using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Saxon.Api;
using SaxonItemType = net.sf.saxon.s9api.ItemType;
using SaxonOccurrence = net.sf.saxon.s9api.OccurrenceIndicator;
using SaxonSequenceType = net.sf.saxon.s9api.SequenceType;
using SaxonUnderlyingSequenceType = net.sf.saxon.value.SequenceType;

namespace XsltDebugger.DebugAdapter;

public class SaxonDebugExtension : ExtensionFunctionDefinition
{
    private readonly SaxonEngine _engine;
    private readonly string _stylesheetPath;
    private static readonly SaxonSequenceType BreakResultSequenceType =
        SaxonSequenceType.makeSequenceType(SaxonItemType.ANY_ITEM, SaxonOccurrence.ZERO);
    private static readonly Func<SaxonUnderlyingSequenceType, XdmSequenceType> SequenceTypeConverter = CreateSequenceTypeConverter();

    public SaxonDebugExtension(SaxonEngine engine, string stylesheetPath)
    {
        _engine = engine;
        _stylesheetPath = stylesheetPath;
    }

    public override QName FunctionName => new QName("urn:xslt-debugger", "break");

    public override int MinimumNumberOfArguments => 1;

    public override int MaximumNumberOfArguments => 2;

    public override XdmSequenceType[] ArgumentTypes => new[]
    {
        new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_DOUBLE), ' '),
        new XdmSequenceType(XdmAnyNodeType.Instance, ' ')
    };

    public override XdmSequenceType ResultType(XdmSequenceType[] argumentTypes)
    {
        var underlying = BreakResultSequenceType.getUnderlyingSequenceType();
        return SequenceTypeConverter(underlying);
    }

    public override ExtensionFunctionCall MakeFunctionCall()
    {
        return new SaxonDebugExtensionCall(_engine, _stylesheetPath);
    }

    private static Func<SaxonUnderlyingSequenceType, XdmSequenceType> CreateSequenceTypeConverter()
    {
        var method = typeof(XdmSequenceType).GetMethod(
            "FromSequenceType",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            new[] { typeof(SaxonUnderlyingSequenceType) },
            modifiers: null);

        if (method != null)
        {
            return underlying => (XdmSequenceType)method.Invoke(obj: null, parameters: new object[] { underlying })!;
        }

        // Fallback: single optional item type permits empty result without mutating output.
        return _ => new XdmSequenceType(XdmAnyItemType.Instance, '?');
    }
}

public class SaxonDebugExtensionCall : ExtensionFunctionCall
{
    private readonly SaxonEngine _engine;
    private readonly string _stylesheetPath;

    public SaxonDebugExtensionCall(SaxonEngine engine, string stylesheetPath)
    {
        _engine = engine;
        _stylesheetPath = stylesheetPath;
    }

    public override IEnumerator<XdmItem> Call(IEnumerator<XdmItem>[] arguments, DynamicContext context)
    {
        var lineArg = arguments[0];
        if (!lineArg.MoveNext())
        {
            return EmptyEnumerator<XdmItem>.INSTANCE;
        }

        var lineValue = lineArg.Current;
        if (lineValue is XdmAtomicValue atomicValue)
        {
            var lineNumber = Convert.ToDouble(atomicValue.Value);
            var line = (int)Math.Round(lineNumber);

            XdmNode? contextNode = null;
            if (arguments.Length > 1)
            {
                var contextArg = arguments[1];
                if (contextArg.MoveNext() && contextArg.Current is XdmNode node)
                {
                    contextNode = node;
                    if (XsltEngineManager.TraceEnabled)
                    {
                        XsltEngineManager.NotifyOutput($"[trace] dbg:break context node: {node.NodeKind}, name={node.NodeName?.LocalName ?? "(no name)"}");
                    }
                }
                else if (XsltEngineManager.TraceEnabled)
                {
                    XsltEngineManager.NotifyOutput($"[trace] dbg:break no context node available at line {line}");
                }
            }
            else if (XsltEngineManager.TraceEnabled)
            {
                XsltEngineManager.NotifyOutput($"[trace] dbg:break only {arguments.Length} argument(s) at line {line}");
            }

            if (XsltEngineManager.IsTraceEnabled)
            {
                var nodeName = contextNode?.NodeName?.LocalName ?? "(no context)";
                XsltEngineManager.NotifyOutput($"[trace] Breakpoint hit at {_stylesheetPath}:{line}, context node: {nodeName}");
            }

            if (XsltEngineManager.IsTraceAllEnabled && contextNode != null)
            {
                var nodeName = contextNode.NodeName?.LocalName ?? "(no name)";
                var nodeValue = contextNode.StringValue ?? string.Empty;
                var nodeType = contextNode.NodeKind.ToString();
                XsltEngineManager.NotifyOutput($"[traceall] Breakpoint context detail at {_stylesheetPath}:{line}:\n" +
                    $"  Current node: <{nodeName}>\n" +
                    $"  Node type: {nodeType}\n" +
                    $"  Value: {(nodeValue.Length > 100 ? nodeValue.Substring(0, 100) + "..." : nodeValue)}");
            }

            _engine.RegisterBreakpointHit(_stylesheetPath, line, contextNode);
        }

        return EmptyEnumerator<XdmItem>.INSTANCE;
    }
}
