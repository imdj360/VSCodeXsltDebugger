using System;
using System.Collections.Generic;
using System.Linq;
using Saxon.Api;

namespace XsltDebugger.DebugAdapter;

public class SaxonDebugExtension : ExtensionFunctionDefinition
{
    private readonly SaxonEngine _engine;
    private readonly string _stylesheetPath;

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

    public override XdmSequenceType ResultType(XdmSequenceType[] ArgumentTypes)
    {
        return new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), ' ');
    }

    public override ExtensionFunctionCall MakeFunctionCall()
    {
        return new SaxonDebugExtensionCall(_engine, _stylesheetPath);
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
                    XsltEngineManager.NotifyOutput($"[trace] dbg:break context node: {node.NodeKind}, name={node.NodeName?.LocalName ?? "(no name)"}");
                }
                else
                {
                    XsltEngineManager.NotifyOutput($"[trace] dbg:break no context node available at line {line}");
                }
            }
            else
            {
                XsltEngineManager.NotifyOutput($"[trace] dbg:break only {arguments.Length} argument(s) at line {line}");
            }

            _engine.RegisterBreakpointHit(_stylesheetPath, line, contextNode);
        }

        return new List<XdmItem> { new XdmAtomicValue(string.Empty) }.GetEnumerator();
    }
}
