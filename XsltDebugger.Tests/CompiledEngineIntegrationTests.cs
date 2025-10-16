using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

public class CompiledEngineIntegrationTests
{
    private static string GetProjectDirectory()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }

    private static string GetTestDataPath(string relativePath)
    {
        var projectDir = GetProjectDirectory();
        var fullPath = Path.Combine(projectDir, "TestData", relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetFullPath(fullPath);
    }

    [Fact]
    public async Task CompiledEngine_ShouldTransformInlineScriptSample_WithTraceLogging()
    {
        var stylesheetPath = GetTestDataPath("Integration/sample-inline-cs-with-usings.xslt");
        var xmlPath = GetTestDataPath("Integration/sample-inline-cs-with-usings.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var breakpoints = new[] { (fullStylesheetPath, 24) };

        var engine = new XsltCompiledEngine();
        var outputLog = new List<string>();
        var outputLock = new object();
        var breakpointHitSource = new TaskCompletionSource<(string file, int line, DebugStopReason reason)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminatedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var stylesheetDir = Path.GetDirectoryName(fullStylesheetPath) ?? throw new InvalidOperationException("Unable to determine stylesheet directory.");
        var outDir = Path.Combine(stylesheetDir, "out");
        var outFile = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(fullStylesheetPath)}.out.xml");

        TryDeleteOutput(outFile, outDir);

        void OnOutput(string message)
        {
            lock (outputLock)
            {
                outputLog.Add(message);
            }
        }

        void OnStopped(string file, int line, DebugStopReason reason)
        {
            breakpointHitSource.TrySetResult((file, line, reason));
            _ = engine.ContinueAsync();
        }

        void OnTerminated(int code) => terminatedSource.TrySetResult(code);

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineStopped += OnStopped;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            engine.SetBreakpoints(breakpoints);
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var breakpointHit = await breakpointHitSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            breakpointHit.file.Should().Be(fullStylesheetPath);
            breakpointHit.line.Should().Be(24);
            breakpointHit.reason.Should().Be(DebugStopReason.Breakpoint);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "inline script sample should transform successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            snapshot.Should().Contain(message => message.Contains("[trace]", StringComparison.OrdinalIgnoreCase), "trace logging should be active");
            snapshot.Should().Contain(message => message.Contains("Transform completed successfully", StringComparison.OrdinalIgnoreCase));

            File.Exists(outFile).Should().BeTrue("compiled engine writes transformation results to disk");
            var output = await File.ReadAllTextAsync(outFile);
            output.Should().Contain("XSLT Debugging Test");
            output.Should().Contain("Date plus 7 days: 2024-01-08");
            output.Should().Contain("Test Item 1");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineStopped -= OnStopped;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
            TryDeleteOutput(outFile, outDir);
        }
    }

    private static void TryDeleteOutput(string outFile, string outDir)
    {
        try
        {
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }

            if (Directory.Exists(outDir) && !Directory.EnumerateFileSystemEntries(outDir).Any())
            {
                Directory.Delete(outDir);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore failures.
        }
    }
}
