using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace XsltDebugger.DebugAdapter;

/// <summary>
/// Abstract base class for XSLT debug engines, providing shared functionality
/// for breakpoint management, step mode handling, and pause/continue mechanisms.
/// </summary>
public abstract class BaseXsltEngine : IXsltEngine
{
    // Shared synchronization and state fields
    protected readonly object _sync = new();
    protected List<(string file, int line)> _breakpoints = new();
    protected TaskCompletionSource<bool>? _pauseTcs;
    protected string _currentStylesheet = string.Empty;
    protected bool _nextStepRequested;
    protected StepMode _stepMode = StepMode.Continue;
    protected int _callDepth = 0;
    protected int _targetDepth = 0;
    protected string _currentStopFile = string.Empty;
    protected int _currentStopLine = -1;
    protected string _stepOriginFile = string.Empty;
    protected int _stepOriginLine = -1;

    /// <summary>
    /// Normalizes a file path by resolving file:// URIs and converting to full path.
    /// </summary>
    /// <param name="path">The path to normalize</param>
    /// <returns>Normalized absolute path, or empty string if path is null/empty</returns>
    protected static string NormalizePath(string path)
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

    /// <summary>
    /// Checks if a breakpoint is set at the specified file and line.
    /// </summary>
    /// <param name="file">The file path (should be normalized)</param>
    /// <param name="line">The line number</param>
    /// <returns>True if a breakpoint exists at this location</returns>
    protected bool IsBreakpointHit(string file, int line)
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

    /// <summary>
    /// Sets breakpoints for the debug session. File paths are normalized automatically.
    /// </summary>
    /// <param name="bps">Collection of (file, line) tuples representing breakpoints</param>
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

    // Abstract methods that must be implemented by derived classes
    public abstract Task StartAsync(string stylesheet, string xml, bool stopOnEntry);
    public abstract Task ContinueAsync();
    public abstract Task StepOverAsync();
    public abstract Task StepInAsync();
    public abstract Task StepOutAsync();
}
