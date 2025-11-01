using System;
using System.Collections;
using System.Linq;

namespace XsltDebugger.DebugAdapter;

public static class InlineXsltLogger
{
    public static void Log(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            XsltEngineManager.NotifyOutput($"[inline] {message}");
        }
    }

    public static void Log(string format, params object[] args)
    {
        try
        {
            Log(string.Format(format, args));
        }
        catch (FormatException)
        {
            Log(format);
        }
    }

    public static void LogEntry(object? parameters = null,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
    {
        var xsltLine = GetXsltCallerLine();
        var prefix = xsltLine.HasValue ? $"[{member}:L{lineNumber}@XSLT:{xsltLine.Value}]" : $"[{member}:L{lineNumber}]";

        if (parameters == null)
        {
            Log($"{prefix} entered.");
        }
        else
        {
            Log($"{prefix} args = {FormatObject(parameters)}");
        }
    }

    public static T LogReturn<T>(T value,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0)
    {
        var xsltLine = GetXsltCallerLine();
        var prefix = xsltLine.HasValue ? $"[{member}:L{lineNumber}@XSLT:{xsltLine.Value}]" : $"[{member}:L{lineNumber}]";
        Log($"{prefix} return = {FormatObject(value)}");
        return value;
    }

    private static int? GetXsltCallerLine()
    {
        return XsltEngineManager.LastStop?.line;
    }

    private static string FormatObject(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        return value switch
        {
            string s => s,
            System.Collections.IEnumerable enumerable and not string => "[" + string.Join(", ", enumerable.Cast<object?>().Select(FormatObject)) + "]",
            _ => value.ToString() ?? value.GetType().Name
        };
    }
}
