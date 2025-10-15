using System;
using System.Xml.XPath;

namespace XsltDebugger.DebugAdapter;

public static class XsltEngineManager
{
    public const int ThreadId = 1;

    public static IXsltEngine? ActiveEngine { get; set; }

    public static (string file, int line)? LastStop { get; private set; }

    public static DebugStopReason LastStopReason { get; private set; } = DebugStopReason.Breakpoint;
    public static XPathNavigator? LastContext { get; private set; }

    public static event Action<string, int, DebugStopReason>? EngineStopped;

    public static event Action<string>? EngineOutput;

    public static event Action<int>? EngineTerminated;

    public static void NotifyStopped(string file, int line, DebugStopReason reason, XPathNavigator? context)
    {
        LastStop = (file, line);
        LastStopReason = reason;
        LastContext = context?.Clone();
        EngineStopped?.Invoke(file, line, reason);
    }

    public static void UpdateContext(XPathNavigator? context)
    {
        LastContext = context?.Clone();
        NotifyOutput($"[trace] XsltEngineManager.UpdateContext: LastContext={(LastContext != null ? $"SET to {LastContext.Name}" : "set to NULL")}");
    }

    public static void NotifyOutput(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            EngineOutput?.Invoke(message);
        }
    }

    public static void NotifyTerminated(int exitCode)
    {
        EngineTerminated?.Invoke(exitCode);
    }

    public static void Reset()
    {
        LastStop = null;
        LastStopReason = DebugStopReason.Breakpoint;
        LastContext = null;
    }
}

public enum DebugStopReason
{
    Breakpoint,
    Entry,
    Step
}
