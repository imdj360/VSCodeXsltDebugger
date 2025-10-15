using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XsltDebugger.DebugAdapter;

internal sealed class SessionState
{
    private readonly Dictionary<string, HashSet<int>> _breakpoints = new(StringComparer.OrdinalIgnoreCase);
    private IXsltEngine? _engine;

    public IXsltEngine? Engine => _engine;
    public bool DebugEnabled { get; private set; } = true;
    public LogLevel CurrentLogLevel { get; private set; } = LogLevel.Log;

    public void SetEngine(IXsltEngine engine, bool debug = true, LogLevel logLevel = LogLevel.Log)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        DebugEnabled = debug;
        CurrentLogLevel = logLevel;
        XsltEngineManager.ActiveEngine = engine;
        XsltEngineManager.SetDebugFlags(debug, logLevel);
        ApplyBreakpointsToEngine();
    }

    public void ClearEngine()
    {
        _engine = null;
        XsltEngineManager.ActiveEngine = null;
    }

    public IReadOnlyList<int> SetBreakpoints(string file, IEnumerable<int> lines)
    {
        var normalized = NormalizePath(file);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<int>();
        }

        var set = new HashSet<int>(lines ?? Array.Empty<int>());
        if (set.Count == 0)
        {
            _breakpoints.Remove(normalized);
        }
        else
        {
            _breakpoints[normalized] = set;
        }

        ApplyBreakpointsToEngine();
        return set.OrderBy(l => l).ToArray();
    }

    public IEnumerable<(string file, int line)> GetBreakpointsFor(string file)
    {
        var normalized = NormalizePath(file);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Enumerable.Empty<(string, int)>();
        }

        if (_breakpoints.TryGetValue(normalized, out var set))
        {
            return set.Select(line => (normalized, line));
        }

        return Enumerable.Empty<(string, int)>();
    }

    public List<(string file, int line)> GetAllBreakpoints()
    {
        var result = new List<(string file, int line)>();
        foreach (var entry in _breakpoints)
        {
            foreach (var line in entry.Value)
            {
                result.Add((entry.Key, line));
            }
        }
        return result;
    }

    private void ApplyBreakpointsToEngine()
    {
        var engine = _engine;
        if (engine == null)
        {
            return;
        }

        engine.SetBreakpoints(GetAllBreakpoints());
    }

    private static string NormalizePath(string path)
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
}
