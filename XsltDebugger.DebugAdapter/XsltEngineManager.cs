using System;
using System.Collections.Generic;
using System.Xml.XPath;

namespace XsltDebugger.DebugAdapter;

public static class XsltEngineManager
{
    public const int ThreadId = 1;

    public static IXsltEngine? ActiveEngine { get; set; }

    public static (string file, int line)? LastStop { get; private set; }

    public static DebugStopReason LastStopReason { get; private set; } = DebugStopReason.Breakpoint;
    public static XPathNavigator? LastContext { get; private set; }

    // XSLT Variables storage
    public static Dictionary<string, object?> Variables { get; private set; } = new();

    public static bool DebugEnabled { get; private set; } = true;
    public static LogLevel CurrentLogLevel { get; private set; } = LogLevel.Log;

    // Convenience properties for checking log levels
    public static bool IsLogEnabled => CurrentLogLevel >= LogLevel.Log;
    public static bool IsTraceEnabled => CurrentLogLevel >= LogLevel.Trace;
    public static bool IsTraceAllEnabled => CurrentLogLevel >= LogLevel.TraceAll;

    // Legacy property for backward compatibility
    public static bool TraceEnabled => IsTraceEnabled;

    public static event Action<string, int, DebugStopReason>? EngineStopped;

    public static event Action<string>? EngineOutput;

    public static event Action<int>? EngineTerminated;

    public static void SetDebugFlags(bool debug, LogLevel logLevel)
    {
        DebugEnabled = debug;
        CurrentLogLevel = logLevel;
        if (IsTraceEnabled)
        {
            NotifyOutput($"[trace] Debug flags set: debug={debug}, logLevel={logLevel}");
        }
    }

    public static void NotifyStopped(string file, int line, DebugStopReason reason, XPathNavigator? context)
    {
        if (!DebugEnabled)
        {
            return;
        }

        LastStop = (file, line);
        LastStopReason = reason;
        LastContext = context?.Clone();

        if (TraceEnabled)
        {
            NotifyOutput($"[trace] Stopped at {file}:{line}, reason={reason}");
        }

        EngineStopped?.Invoke(file, line, reason);
    }

    public static void UpdateContext(XPathNavigator? context)
    {
        LastContext = context?.Clone();
        if (TraceEnabled)
        {
            NotifyOutput($"[trace] XsltEngineManager.UpdateContext: LastContext={(LastContext != null ? $"SET to {LastContext.Name}" : "set to NULL")}");
        }
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

    public static void StoreVariable(string name, object? value)
    {
        Variables[name] = value;
        if (IsTraceAllEnabled)
        {
            NotifyOutput($"[traceall] Variable stored: ${name} = {FormatValue(value)}");
        }
    }

    public static void ClearVariables()
    {
        Variables.Clear();
        if (IsTraceAllEnabled)
        {
            NotifyOutput("[traceall] Variables cleared");
        }
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "null";
        if (value is XPathNodeIterator iter) return $"{iter.Count} nodes";
        var str = value.ToString() ?? "null";
        return str.Length > 100 ? str.Substring(0, 100) + "..." : str;
    }

    public static void Reset()
    {
        LastStop = null;
        LastStopReason = DebugStopReason.Breakpoint;
        LastContext = null;
        DebugEnabled = true;
        CurrentLogLevel = LogLevel.Log;
        ClearVariables();
    }
}

public enum DebugStopReason
{
    Breakpoint,
    Entry,
    Step
}

public enum LogLevel
{
    None = 0,      // Silent - only errors
    Log = 1,       // General execution events
    Trace = 2,     // Detailed troubleshooting
    TraceAll = 3   // Full XPath value tracking
}
