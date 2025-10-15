using System;

namespace XsltDebugger.DebugAdapter;

public static class XsltEngineFactory
{
    public static IXsltEngine CreateEngine(XsltEngineType engineType)
    {
        return engineType switch
        {
            XsltEngineType.Compiled => new XsltCompiledEngine(),
            XsltEngineType.SaxonNet => new SaxonEngine(),
            _ => throw new ArgumentException($"Unsupported engine type: {engineType}", nameof(engineType))
        };
    }

    public static IXsltEngine CreateEngine(string engineType)
    {
        if (Enum.TryParse<XsltEngineType>(engineType, true, out var type))
        {
            return CreateEngine(type);
        }
        throw new ArgumentException($"Invalid engine type: {engineType}", nameof(engineType));
    }
}
