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
    private static string GetRepositoryRoot()
    {
        // From bin/Debug/net8.0, go up 4 levels to reach repository root
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string GetTestDataPath(string relativePath)
    {
        var repoRoot = GetRepositoryRoot();
        var fullPath = Path.Combine(repoRoot, "TestData", relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Path.GetFullPath(fullPath);
    }

    [Fact]
    public async Task CompiledEngine_ShouldTransformInlineScriptSample_WithTraceLogging()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/sample-inline-cs-with-usings.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/sample-inline-cs-with-usings.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var lines = File.ReadAllLines(fullStylesheetPath);
        var templateLine = Array.FindIndex(lines, line => line.Contains("<xsl:template match=\"/\">", StringComparison.Ordinal)) + 1;
        var breakpoints = new[] { (fullStylesheetPath, templateLine) };

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
            breakpointHit.line.Should().Be(templateLine);
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

    [Fact]
    public async Task CompiledEngine_ShouldCaptureVariablesAndHitBreakpoints()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/VariableLoggingSampleV1.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/VariableLoggingSampleV1Input.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);
        var breakpoints = new[] { (fullStylesheetPath, 8) };

        var engine = new XsltCompiledEngine();
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

            // Verify variables were captured
            XsltEngineManager.Variables.Should().ContainKey("count");
            XsltEngineManager.Variables["count"].Should().Be("2");

            XsltEngineManager.Variables.Should().ContainKey("firstName");
            XsltEngineManager.Variables["firstName"].Should().NotBeNull();
            XsltEngineManager.Variables["firstName"]!.ToString().Should().Contain("Alpha");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Verify variable instrumentation occurred
            snapshot.Should().Contain(message => message.Contains("[debug] Instrumenting 2 variable", StringComparison.OrdinalIgnoreCase));
            snapshot.Should().Contain(message => message.Contains("Captured variable: $count", StringComparison.OrdinalIgnoreCase));
            snapshot.Should().Contain(message => message.Contains("Captured variable: $firstName", StringComparison.OrdinalIgnoreCase));

            // Verify xsl:message output appears
            snapshot.Should().Contain(message => message.Contains("[xsl:message]", StringComparison.OrdinalIgnoreCase));
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
    public async Task CompiledEngine_ShouldInstrumentInlineCSharpMethods()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/sample-inline-cs-auto-instrument.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/sample-inline-cs-auto-instrument.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new XsltCompiledEngine();
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
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "transformation should complete successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Verify instrumentation occurred
            snapshot.Should().Contain(message => message.Contains("Instrumented 3 inline C# method(s)", StringComparison.OrdinalIgnoreCase),
                "should report instrumenting 3 C# methods");

            // Verify Add method logging
            snapshot.Should().Contain(message => message.Contains("[inline] [Add:", StringComparison.Ordinal) && message.Contains("args = { a = 5, b = 3 }"),
                "should log Add method entry with parameters");
            snapshot.Should().Contain(message => message.Contains("[inline] [Add:", StringComparison.Ordinal) && message.Contains("return = 8"),
                "should log Add method return value");

            // Verify Multiply method logging
            snapshot.Should().Contain(message => message.Contains("[inline] [Multiply:", StringComparison.Ordinal) && message.Contains("args = { a = 4, b = 7 }"),
                "should log Multiply method entry with parameters");
            snapshot.Should().Contain(message => message.Contains("[inline] [Multiply:", StringComparison.Ordinal) && message.Contains("return = 28"),
                "should log Multiply method return value");

            // Verify FormatNumber method logging
            snapshot.Should().Contain(message => message.Contains("[inline] [FormatNumber:", StringComparison.Ordinal) && message.Contains("args = { num = 1000000 }"),
                "should log FormatNumber method entry with parameters");
            snapshot.Should().Contain(message => message.Contains("[inline] [FormatNumber:", StringComparison.Ordinal) && message.Contains("return = 1,000,000"),
                "should log FormatNumber method return value");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task CompiledEngine_ShouldIncludeXsltLineNumbersInInlineCSharpLogs()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/ShipmentConfv1.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/ShipmentConf-proper.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new XsltCompiledEngine();
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
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "transformation should complete successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Verify XSLT line numbers are included in inline C# logs
            snapshot.Should().Contain(message => message.Contains("@XSLT:75", StringComparison.Ordinal),
                "should include XSLT line 75 for RoundToTwoDecimals call");
            snapshot.Should().Contain(message => message.Contains("@XSLT:88", StringComparison.Ordinal),
                "should include XSLT line 88 for MinDate call");

            // Verify C# line numbers are also included
            snapshot.Should().Contain(message => message.Contains("[RoundToTwoDecimals:L", StringComparison.Ordinal),
                "should include C# line number for RoundToTwoDecimals");
            snapshot.Should().Contain(message => message.Contains("[MinDate:L", StringComparison.Ordinal),
                "should include C# line number for MinDate");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task CompiledEngine_ShouldNotDoubleInstrumentManuallyLoggedMethods()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/sample-inline-cs-with-usings.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/sample-inline-cs-with-usings.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new XsltCompiledEngine();
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
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "transformation should complete successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Should NOT see "Instrumented N inline C# method(s)" because methods already use LogEntry/LogReturn
            snapshot.Should().NotContain(message => message.Contains("Instrumented 2 inline C# method(s)", StringComparison.OrdinalIgnoreCase),
                "should not instrument already manually logged methods");

            // But should still see the manual logging output (with XSLT line numbers now)
            snapshot.Should().Contain(message => message.Contains("[inline] [FormatCurrentDate:", StringComparison.Ordinal),
                "should still have manual logging from FormatCurrentDate");
            snapshot.Should().Contain(message => message.Contains("[inline] [AddDays:", StringComparison.Ordinal),
                "should still have manual logging from AddDays");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task CompiledEngine_ShouldLogForEachPositionWithoutSort()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/foreach-test.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/foreach-test.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new XsltCompiledEngine();
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
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "transformation should complete successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Verify for-each position logging for simple loop (line 10)
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=10", StringComparison.Ordinal) && message.Contains("select=/root/items/item", StringComparison.Ordinal) && message.Contains("pos=1", StringComparison.Ordinal),
                "should log position 1 for first iteration");
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=10", StringComparison.Ordinal) && message.Contains("pos=2", StringComparison.Ordinal),
                "should log position 2 for second iteration");
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=10", StringComparison.Ordinal) && message.Contains("pos=3", StringComparison.Ordinal),
                "should log position 3 for third iteration");

            // Verify for-each variable was captured
            // Note: The variable will contain the LAST for-each that executed
            // Since this test file has two for-each loops, it will be line 17 (the second one)
            XsltEngineManager.Variables.Should().ContainKey("for-each");
            XsltEngineManager.Variables["for-each"].Should().NotBeNull();
            var forEachValue = XsltEngineManager.Variables["for-each"]!.ToString();
            (forEachValue.Contains("line=10") || forEachValue.Contains("line=17")).Should().BeTrue(
                "for-each variable should contain either line 10 or line 17");

            // Verify variable capture logging occurred (should see captures for BOTH loops)
            snapshot.Should().Contain(message => message.Contains("Captured variable: $for-each", StringComparison.Ordinal) && message.Contains("line=10"),
                "should log variable capture for line 10 for-each");
            snapshot.Should().Contain(message => message.Contains("Captured variable: $for-each", StringComparison.Ordinal) && message.Contains("line=17"),
                "should log variable capture for line 17 for-each");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task CompiledEngine_ShouldLogForEachPositionWithSort()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/foreach-test.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/foreach-test.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new XsltCompiledEngine();
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
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "transformation should complete successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Verify for-each position logging for loop with sort (line 17)
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=17", StringComparison.Ordinal) && message.Contains("select=/root/sorted/item", StringComparison.Ordinal) && message.Contains("pos=1", StringComparison.Ordinal),
                "should log position 1 for first iteration (after sort)");
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=17", StringComparison.Ordinal) && message.Contains("pos=2", StringComparison.Ordinal),
                "should log position 2 for second iteration (after sort)");
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=17", StringComparison.Ordinal) && message.Contains("pos=3", StringComparison.Ordinal),
                "should log position 3 for third iteration (after sort)");

            // Verify for-each variable was captured (will contain the last iteration's value)
            XsltEngineManager.Variables.Should().ContainKey("for-each");
            XsltEngineManager.Variables["for-each"].Should().NotBeNull();
            XsltEngineManager.Variables["for-each"]!.ToString().Should().Contain("line=17");
            XsltEngineManager.Variables["for-each"]!.ToString().Should().Contain("select=/root/sorted/item");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task CompiledEngine_ShouldLogForEachInShipmentConfv1()
    {
        var stylesheetPath = GetTestDataPath("Integration/xslt/compiled/ShipmentConfv1.xslt");
        var xmlPath = GetTestDataPath("Integration/xml/ShipmentConf-proper.xml");
        var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
        var fullXmlPath = Path.GetFullPath(xmlPath);

        var engine = new XsltCompiledEngine();
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
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.EngineOutput += OnOutput;
        XsltEngineManager.EngineTerminated += OnTerminated;

        try
        {
            await engine.StartAsync(fullStylesheetPath, fullXmlPath, stopOnEntry: false);

            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
            exitCode.Should().Be(0, "transformation should complete successfully");

            List<string> snapshot;
            lock (outputLock)
            {
                snapshot = outputLog.ToList();
            }

            // Verify for-each position logging for outer loop (line 60)
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=60", StringComparison.Ordinal) && message.Contains("select=/ShipmentConfirmation/Orders/OrderItems", StringComparison.Ordinal),
                "should log position for outer for-each at line 60");

            // Verify for-each position logging for nested loop (line 83)
            snapshot.Should().Contain(message => message.Contains("[xsl:message] [DBG] for-each line=83", StringComparison.Ordinal) && message.Contains("select=OperationReports/ReportInfo/OperationReportDate", StringComparison.Ordinal),
                "should log position for nested for-each at line 83");

            // Verify the existing xsl:message at line 67 is still present
            snapshot.Should().Contain(message => message.Contains("[xsl:message] Hello", StringComparison.Ordinal),
                "should preserve existing xsl:message elements");

            // Verify for-each variable was captured
            XsltEngineManager.Variables.Should().ContainKey("for-each");
            XsltEngineManager.Variables["for-each"].Should().NotBeNull();
            // The variable will contain the last for-each that executed (could be line 60 or line 83)
            var forEachValue = XsltEngineManager.Variables["for-each"]!.ToString();
            (forEachValue.Contains("line=60") || forEachValue.Contains("line=83")).Should().BeTrue(
                "for-each variable should contain either line 60 or line 83");
        }
        finally
        {
            XsltEngineManager.EngineOutput -= OnOutput;
            XsltEngineManager.EngineTerminated -= OnTerminated;
            XsltEngineManager.Reset();
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
