using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

public class SaxonEngineIntegrationTests
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

    private static async Task<(List<string> Output, string OutFile, int ExitCode)> RunSaxonAsync(
        string stylesheetRelativePath,
        string xmlRelativePath,
        LogLevel logLevel = LogLevel.TraceAll)
    {
        var stylesheetPath = GetTestDataPath(stylesheetRelativePath);
        var xmlPath = GetTestDataPath(xmlRelativePath);
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new SaxonEngine();
        var outputLog = new List<string>();
        var outputLock = new object();
        var terminatedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOutput(string message)
        {
            lock (outputLock)
            {
                outputLog.Add(message);
            }
        }
        void OnTerminated(int code) => terminatedSource.TrySetResult(code);

        var stylesheetDir = Path.GetDirectoryName(fullStylesheetPath) ?? throw new InvalidOperationException("Unable to determine stylesheet directory.");
        var outDir = Path.Combine(stylesheetDir, "out");
        var outFile = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(fullStylesheetPath)}.out.xml");
        CleanupOutput(outFile, outDir);

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, logLevel);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            return (snapshot, outFile, exitCode);
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_ShouldCaptureVariablesAndHitBreakpoints()
    {
        var stylesheetPath = GetTestDataPath("Integration/VariableLoggingSample.xslt");
        var xmlPath = GetTestDataPath("Integration/ItemsSample.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var breakpoints = new[] { (fullStylesheetPath, 8) };

        var engine = new SaxonEngine();
        var outputLog = new List<string>();
        var outputLock = new object();
        var breakpointHitSource = new TaskCompletionSource<(string file, int line, DebugStopReason reason)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminatedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        XsltEngineManager.SetDebugFlags(true, LogLevel.TraceAll);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineStopped += OnStopped;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            engine.SetBreakpoints(breakpoints);
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "successful transformation should terminate with 0 exit code");

            var breakpointHit = await breakpointHitSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            breakpointHit.file.Should().Be(fullStylesheetPath);
            breakpointHit.line.Should().Be(8);
            breakpointHit.reason.Should().Be(DebugStopReason.Breakpoint);

            XsltEngineManager.Variables.Should().ContainKey("itemCount");
            XsltEngineManager.Variables["itemCount"].Should().Be("2");

            XsltEngineManager.Variables.Should().ContainKey("firstName");
            XsltEngineManager.Variables["firstName"].Should().NotBeNull();
            XsltEngineManager.Variables["firstName"]!.ToString().Should().Contain("Alpha");

            XsltEngineManager.Variables.Should().ContainKey("currentName");
            XsltEngineManager.Variables["currentName"].Should().Be("Beta");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            snapshot.Should().Contain(message => message.Contains("[debug] Instrumenting 3 variable", StringComparison.OrdinalIgnoreCase));
            snapshot.Should().Contain(message => message.Contains("Captured variable: $itemCount", StringComparison.OrdinalIgnoreCase));
            snapshot.Should().Contain(message => message.Contains("Captured variable: $currentName", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineStopped -= OnStopped;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_ShouldReportCompilationErrors()
    {
        var stylesheetPath = GetTestDataPath("Integration/InvalidFunctionCall.xslt");
        var xmlPath = GetTestDataPath("Integration/ItemsSample.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var engine = new SaxonEngine();

        var outputLog = new List<string>();
        var outputLock = new object();
        var terminatedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOutput(string message)
        {
            lock (outputLock)
            {
                outputLog.Add(message);
            }
        }
        void OnTerminated(int code) => terminatedSource.TrySetResult(code);

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.TraceAll);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(1, "compilation failures should terminate with error code 1");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            snapshot.Should().Contain(message => message.Contains("Saxon compilation error", StringComparison.OrdinalIgnoreCase));
            XsltEngineManager.Variables.Should().BeEmpty();
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_ShouldTransformShipmentSample_WithTraceLogging()
    {
        var stylesheetPath = GetTestDataPath("Integration/ShipmentConf3.xslt");
        var xmlPath = GetTestDataPath("Integration/ShipmentConf-proper.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var engine = new SaxonEngine();

        var outputLog = new List<string>();
        var outputLock = new object();
        var terminatedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOutput(string message)
        {
            lock (outputLock)
            {
                outputLog.Add(message);
            }
        }
        void OnTerminated(int code) => terminatedSource.TrySetResult(code);

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "valid sample stylesheet should transform successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            snapshot.Should().Contain(message => message.Contains("[trace]", StringComparison.OrdinalIgnoreCase), "trace logging should be active");
            snapshot.Should().Contain(message => message.Contains("Stylesheet compiled successfully", StringComparison.OrdinalIgnoreCase));

            XsltEngineManager.Variables.Should().ContainKey("net-str");
            XsltEngineManager.Variables["net-str"]!.ToString().Should().Be("1500.50");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_ShouldRespectVariableInstrumentationGuardrails()
    {
        var stylesheetPath = GetTestDataPath("Integration/test-guardrails.xslt");
        var xmlPath = GetTestDataPath("Integration/sample.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var engine = new SaxonEngine();

        var outputLog = new List<string>();
        var outputLock = new object();
        var terminatedSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOutput(string message)
        {
            lock (outputLock)
            {
                outputLog.Add(message);
            }
        }
        void OnTerminated(int code) => terminatedSource.TrySetResult(code);

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "guardrail sample should transform successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            snapshot.Should().Contain(message => message.Contains("[trace]", StringComparison.OrdinalIgnoreCase), "trace logging should be active");
            snapshot.Should().Contain(message => message.Contains("Skipped unsafe instrumentation: $unsafe1", StringComparison.OrdinalIgnoreCase));
            snapshot.Should().Contain(message => message.Contains("Skipped unsafe instrumentation: $unsafe2", StringComparison.OrdinalIgnoreCase));

            XsltEngineManager.Variables.Should().ContainKey("safe1");
            XsltEngineManager.Variables["safe1"]!.ToString().Should().Be("123");
            XsltEngineManager.Variables.Should().ContainKey("safe2");
            XsltEngineManager.Variables["safe2"]!.ToString().Should().Be("789");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_ShouldTransformAdvancedXslt2_WithInstrumentation()
    {
        var (log, outFile, exitCode) = await RunSaxonAsync("Integration/AdvanceXslt2.xslt", "Integration/AdvanceFile.xml");
        try
        {
            exitCode.Should().Be(0);
            log.Should().Contain(message => message.Contains("Instrumenting 15 variable", StringComparison.OrdinalIgnoreCase));
            log.Should().Contain(message => message.Contains("Captured variable: $arrival", StringComparison.OrdinalIgnoreCase));
            log.Should().Contain(message => message.Contains("Captured variable: $for-each-group", StringComparison.OrdinalIgnoreCase));

            File.Exists(outFile).Should().BeTrue("advanced transform should produce output");
            var output = await File.ReadAllTextAsync(outFile);
            output.Should().Contain("<ShipmentSummary");
            output.Should().Contain("<ReportCount>5</ReportCount>");
        }
        finally
        {
            var outDir = Path.GetDirectoryName(outFile) ?? string.Empty;
            CleanupOutput(outFile, outDir);
        }
    }

    [Fact]
    public async Task SaxonEngine_ShouldTransformAdvancedXslt3_WithAccumulatorInstrumentation()
    {
        var (log, outFile, exitCode) = await RunSaxonAsync("Integration/AdvanceXslt3.xslt", "Integration/AdvanceFile.xml");
        try
        {
            exitCode.Should().Be(0);
            log.Should().Contain(message => message.Contains("Instrumenting 24 variable", StringComparison.OrdinalIgnoreCase));
            log.Should().Contain(message => message.Contains("Captured variable: $arrival-raw", StringComparison.OrdinalIgnoreCase));
            log.Should().Contain(message => message.Contains("Captured variable: $for-each-group", StringComparison.OrdinalIgnoreCase));
            log.Any(message => message.Contains("xsl:accumulator-rule", StringComparison.OrdinalIgnoreCase))
                .Should().BeFalse("accumulator rules must remain free of instrumentation");

            File.Exists(outFile).Should().BeTrue();
            var output = await File.ReadAllTextAsync(outFile);
            output.Should().Contain("<LatestReportBeforeDeparture>2025-07-14T12:00:00</LatestReportBeforeDeparture>");
            output.Should().Contain("<TimezoneApplied>false</TimezoneApplied>");
        }
        finally
        {
            var outDir = Path.GetDirectoryName(outFile) ?? string.Empty;
            CleanupOutput(outFile, outDir);
        }
    }

    private static void CleanupOutput(string outFile, string outDir)
    {
        try
        {
            if (!string.IsNullOrEmpty(outFile) && File.Exists(outFile))
            {
                File.Delete(outFile);
            }

            if (!string.IsNullOrEmpty(outDir) &&
                Directory.Exists(outDir) &&
                !Directory.EnumerateFileSystemEntries(outDir).Any())
            {
                Directory.Delete(outDir);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }
}
