using System;
using System.IO;
using System.Threading.Tasks;

namespace XsltDebugger.DebugAdapter;

internal static class Program
{
    private const string AdapterName = "xslt-debugger";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--test-engine", StringComparison.OrdinalIgnoreCase))
        {
            await RunEngineTest().ConfigureAwait(false);
            return 0;
        }

        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();

        var state = new SessionState();
        var server = new DapServer(input, output, state);

        XsltEngineManager.EngineStopped += server.NotifyStopped;
        XsltEngineManager.EngineOutput += server.NotifyOutput;
        XsltEngineManager.EngineTerminated += server.NotifyTerminated;

        Console.Error.WriteLine($"[{AdapterName}] Debug adapter started.");
        await server.RunAsync().ConfigureAwait(false);
        Console.Error.WriteLine($"[{AdapterName}] Debug adapter stopped.");
        return 0;
    }

    private static async Task RunEngineTest()
    {
        Console.WriteLine("Starting engine test...");
        var engine = new XsltCompiledEngine();

        var breakpointHit = new TaskCompletionSource<(string file, int line, DebugStopReason reason)>();
        void OnEngineStopped(string file, int line, DebugStopReason reason)
        {
            Console.WriteLine($"Engine stopped at {file}:{line} ({reason})");
            breakpointHit.TrySetResult((file, line, reason));
        }
        XsltEngineManager.EngineStopped += OnEngineStopped;

        string FindSample(string relative)
        {
            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            for (var i = 0; i < 6 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "sample", relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return Path.Combine(Environment.CurrentDirectory, "sample", relative);
        }

        try
        {
            var sampleSheet = FindSample("sample-inline-cs.xslt");
            var sampleXml = FindSample("sample.xml");

            // Break on the <xsl:value-of> instruction in the sample stylesheet.
            engine.SetBreakpoints(new[] { (sampleSheet, 24) });

            // Start the engine on a background thread so this test method can
            // continue and wait for the breakpoint without being blocked by the
            // engine's synchronous pause handling.
            var task = Task.Run(async () => await engine.StartAsync(sampleSheet, sampleXml, stopOnEntry: false));

            var stopInfo = await breakpointHit.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Console.WriteLine($"Hit breakpoint at line {stopInfo.line}. Continuing...");
            await engine.ContinueAsync().ConfigureAwait(false);
            await task.ConfigureAwait(false);
        }
        finally
        {
            XsltEngineManager.EngineStopped -= OnEngineStopped;
        }
        Console.WriteLine("Engine test completed.");
    }
}
