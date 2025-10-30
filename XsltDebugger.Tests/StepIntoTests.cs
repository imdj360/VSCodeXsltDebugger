using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

public class StepIntoTests
{
    private enum StepState
    {
        SeekCallSite,
        SeekNestedEntry,
        InsideNested,
        AfterStep,
        Completed
    }

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

    private static (string xsltPath, string xmlPath) CreateSimpleTemplateTest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xslt-step-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var xsltPath = Path.Combine(tempDir, "test.xslt");
        var xmlPath = Path.Combine(tempDir, "test.xml");

        // Simple XSLT with nested templates for step testing
        File.WriteAllText(xsltPath, @"<?xml version=""1.0""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
  <xsl:output method=""xml"" indent=""yes""/>

  <xsl:template match=""/"">
    <root>
      <xsl:call-template name=""level1""/>
    </root>
  </xsl:template>

  <xsl:template name=""level1"">
    <level1>
      <xsl:call-template name=""level2""/>
    </level1>
  </xsl:template>

  <xsl:template name=""level2"">
    <level2>Done</level2>
  </xsl:template>
</xsl:stylesheet>");

        File.WriteAllText(xmlPath, @"<?xml version=""1.0""?><data/>");

        return (xsltPath, xmlPath);
    }

    [Fact]
    public async Task CompiledEngine_StepInto_ShouldEnterNamedTemplate()
    {
        // Arrange
        var (xsltPath, xmlPath) = CreateSimpleTemplateTest();
        var engine = new XsltCompiledEngine();
        var stops = new List<(int line, DebugStopReason reason)>();
        var templateEntryCount = 0;
        var terminatedSource = new TaskCompletionSource<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineOutput += (output) =>
        {
            if (output.Contains("[template-entry]"))
            {
                templateEntryCount++;
            }
        };

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stops.Add((line, reason));

            // Use StepInto for all stops
            await engine.StepInAsync();
        };

        XsltEngineManager.EngineTerminated += (code) => terminatedSource.TrySetResult(code);

        try
        {
            // Act
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exitCode.Should().Be(0);
            stops.Should().NotBeEmpty();
            templateEntryCount.Should().BeGreaterOrEqualTo(2, "should detect template entries for level1 and level2");
        }
        finally
        {
            XsltEngineManager.Reset();
            try { Directory.Delete(Path.GetDirectoryName(xsltPath)!, true); } catch { }
        }
    }

    [Fact]
    public async Task SaxonEngine_StepInto_ShouldEnterNamedTemplate()
    {
        // Arrange
        var (xsltPath, xmlPath) = CreateSimpleTemplateTest();
        var engine = new SaxonEngine();
        var stops = new List<(int line, DebugStopReason reason)>();
        var templateEntryCount = 0;
        var terminatedSource = new TaskCompletionSource<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineOutput += (output) =>
        {
            if (output.Contains("[template-entry]"))
            {
                templateEntryCount++;
            }
        };

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stops.Add((line, reason));

            // Use StepInto for all stops
            await engine.StepInAsync();
        };

        XsltEngineManager.EngineTerminated += (code) => terminatedSource.TrySetResult(code);

        try
        {
            // Act
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exitCode.Should().Be(0);
            stops.Should().NotBeEmpty();
            templateEntryCount.Should().BeGreaterOrEqualTo(2, "should detect template entries for level1 and level2");
        }
        finally
        {
            XsltEngineManager.Reset();
            try { Directory.Delete(Path.GetDirectoryName(xsltPath)!, true); } catch { }
        }
    }

    [Fact]
    public async Task CompiledEngine_StepOver_ShouldStayAtSameDepth()
    {
        // Arrange
        var (xsltPath, xmlPath) = CreateSimpleTemplateTest();
        var engine = new XsltCompiledEngine();
        var stops = new List<(int line, DebugStopReason reason)>();
        var stepsExecuted = 0;
        var terminatedSource = new TaskCompletionSource<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stops.Add((line, reason));
            stepsExecuted++;

            if (stepsExecuted <= 5)
            {
                // Use StepOver for first few steps
                await engine.StepOverAsync();
            }
            else
            {
                await engine.ContinueAsync();
            }
        };

        XsltEngineManager.EngineTerminated += (code) => terminatedSource.TrySetResult(code);

        try
        {
            // Act
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exitCode.Should().Be(0);
            stops.Should().NotBeEmpty();
            stops.Count.Should().BeGreaterOrEqualTo(3, "should have multiple stops with step over");
        }
        finally
        {
            XsltEngineManager.Reset();
            try { Directory.Delete(Path.GetDirectoryName(xsltPath)!, true); } catch { }
        }
    }

    [Fact]
    public async Task SaxonEngine_StepOver_ShouldStayAtSameDepth()
    {
        // Arrange
        var (xsltPath, xmlPath) = CreateSimpleTemplateTest();
        var engine = new SaxonEngine();
        var stops = new List<(int line, DebugStopReason reason)>();
        var stepsExecuted = 0;
        var terminatedSource = new TaskCompletionSource<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stops.Add((line, reason));
            stepsExecuted++;

            if (stepsExecuted <= 5)
            {
                // Use StepOver for first few steps
                await engine.StepOverAsync();
            }
            else
            {
                await engine.ContinueAsync();
            }
        };

        XsltEngineManager.EngineTerminated += (code) => terminatedSource.TrySetResult(code);

        try
        {
            // Act
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exitCode.Should().Be(0);
            stops.Should().NotBeEmpty();
            stops.Count.Should().BeGreaterOrEqualTo(3, "should have multiple stops with step over");
        }
        finally
        {
            XsltEngineManager.Reset();
            try { Directory.Delete(Path.GetDirectoryName(xsltPath)!, true); } catch { }
        }
    }

    [Fact]
    public async Task XsltCompiledEngine_StepOver_CallTemplate_ShouldPauseAfterReturn()
    {
        var xsltPath = GetTestDataPath("Integration/step-into-test.xslt");
        var xmlPath = GetTestDataPath("Integration/step-into-test.xml");

        var lines = File.ReadAllLines(xsltPath);
        var callLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:call-template name=""formatCurrency""", StringComparison.Ordinal)) + 1;
        var afterCallLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:text>Order processed</xsl:text>", StringComparison.Ordinal)) + 1;

        callLine.Should().BeGreaterThan(0, "call-template line should exist in test stylesheet");
        afterCallLine.Should().BeGreaterThan(0, "post-call line should exist in test stylesheet");

        var engine = new XsltCompiledEngine();
        var terminatedSource = new TaskCompletionSource<int>();
        int? observedAfterCallLine = null;
        var state = StepState.SeekCallSite;

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (_, line, _) =>
        {
            switch (state)
            {
                case StepState.SeekCallSite:
                    if (line == callLine)
                    {
                        state = StepState.AfterStep;
                        await engine.StepOverAsync();
                    }
                    else
                    {
                        await engine.StepInAsync();
                    }
                    break;
                case StepState.AfterStep:
                    observedAfterCallLine = line;
                    state = StepState.Completed;
                    await engine.ContinueAsync();
                    break;
                default:
                    await engine.ContinueAsync();
                    break;
            }
        };

        XsltEngineManager.EngineTerminated += code => terminatedSource.TrySetResult(code);

        try
        {
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            exitCode.Should().Be(0);
            observedAfterCallLine.Should().NotBeNull("step-over should yield a stop after returning from nested template");
            observedAfterCallLine.Should().NotBe(callLine, "step-over should advance past the call site");
        }
        finally
        {
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_StepOver_CallTemplate_ShouldPauseAfterReturn()
    {
        var xsltPath = GetTestDataPath("Integration/step-into-test.xslt");
        var xmlPath = GetTestDataPath("Integration/step-into-test.xml");

        var lines = File.ReadAllLines(xsltPath);
        var callLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:call-template name=""formatCurrency""", StringComparison.Ordinal)) + 1;
        var afterCallLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:text>Order processed</xsl:text>", StringComparison.Ordinal)) + 1;

        callLine.Should().BeGreaterThan(0, "call-template line should exist in test stylesheet");
        afterCallLine.Should().BeGreaterThan(0, "post-call line should exist in test stylesheet");

        var engine = new SaxonEngine();
        var terminatedSource = new TaskCompletionSource<int>();
        int? observedAfterCallLine = null;
        var state = StepState.SeekCallSite;
        var observedLines = new List<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (_, line, _) =>
        {
            observedLines.Add(line);
            switch (state)
            {
                case StepState.SeekCallSite:
                    if (line == callLine)
                    {
                        state = StepState.AfterStep;
                        await engine.StepOverAsync();
                    }
                    else
                    {
                        await engine.StepInAsync();
                    }
                    break;
                case StepState.AfterStep:
                    observedAfterCallLine = line;
                    state = StepState.Completed;
                    await engine.ContinueAsync();
                    break;
                default:
                    await engine.ContinueAsync();
                    break;
            }
        };

        XsltEngineManager.EngineTerminated += code => terminatedSource.TrySetResult(code);

        try
        {
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            exitCode.Should().Be(0);

            if (observedAfterCallLine == null)
            {
                throw new InvalidOperationException($"Saxon step-over did not pause again. Lines: {string.Join(",", observedLines)}");
            }

            observedAfterCallLine.Value.Should().NotBe(callLine, "step-over should advance past the call site");
        }
        finally
        {
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task CompiledEngine_StepOut_ShouldComplete()
    {
        // Arrange
        var (xsltPath, xmlPath) = CreateSimpleTemplateTest();
        var engine = new XsltCompiledEngine();
        var stepCount = 0;
        var usedStepOut = false;
        var terminatedSource = new TaskCompletionSource<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stepCount++;

            if (stepCount < 5)
            {
                // Step into a few times to get deep
                await engine.StepInAsync();
            }
            else if (!usedStepOut)
            {
                // Use StepOut once
                usedStepOut = true;
                await engine.StepOutAsync();
            }
            else
            {
                // Continue
                await engine.ContinueAsync();
            }
        };

        XsltEngineManager.EngineTerminated += (code) => terminatedSource.TrySetResult(code);

        try
        {
            // Act
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exitCode.Should().Be(0);
            usedStepOut.Should().BeTrue("should have used StepOut");
            stepCount.Should().BeGreaterOrEqualTo(5, "should have multiple steps");
        }
        finally
        {
            XsltEngineManager.Reset();
            try { Directory.Delete(Path.GetDirectoryName(xsltPath)!, true); } catch { }
        }
    }

    [Fact]
    public async Task SaxonEngine_StepOut_ShouldComplete()
    {
        // Arrange
        var (xsltPath, xmlPath) = CreateSimpleTemplateTest();
        var engine = new SaxonEngine();
        var stepCount = 0;
        var usedStepOut = false;
        var terminatedSource = new TaskCompletionSource<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stepCount++;

            if (stepCount < 5)
            {
                // Step into a few times to get deep
                await engine.StepInAsync();
            }
            else if (!usedStepOut)
            {
                // Use StepOut once
                usedStepOut = true;
                await engine.StepOutAsync();
            }
            else
            {
                // Continue
                await engine.ContinueAsync();
            }
        };

        XsltEngineManager.EngineTerminated += (code) => terminatedSource.TrySetResult(code);

        try
        {
            // Act
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            exitCode.Should().Be(0);
            usedStepOut.Should().BeTrue("should have used StepOut");
            stepCount.Should().BeGreaterOrEqualTo(5, "should have multiple steps");
        }
        finally
        {
            XsltEngineManager.Reset();
            try { Directory.Delete(Path.GetDirectoryName(xsltPath)!, true); } catch { }
        }
    }

    [Fact]
    public async Task XsltCompiledEngine_StepOut_FromNestedTemplate_ShouldPauseAtCaller()
    {
        var xsltPath = GetTestDataPath("Integration/step-into-test.xslt");
        var xmlPath = GetTestDataPath("Integration/step-into-test.xml");

        var lines = File.ReadAllLines(xsltPath);
        var callLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:call-template name=""formatCurrency""", StringComparison.Ordinal)) + 1;
        var nestedTemplateLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:template name=""formatCurrency""", StringComparison.Ordinal)) + 1;
        callLine.Should().BeGreaterThan(0);
        nestedTemplateLine.Should().BeGreaterThan(0);

        var engine = new XsltCompiledEngine();
        var terminatedSource = new TaskCompletionSource<int>();
        int? observedAfterStepOut = null;
        var state = StepState.SeekCallSite;
        var observedLines = new List<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (_, line, _) =>
        {
            observedLines.Add(line);
            switch (state)
            {
                case StepState.SeekCallSite:
                    if (line == callLine)
                    {
                        state = StepState.SeekNestedEntry;
                        await engine.StepInAsync();
                    }
                    else
                    {
                        await engine.StepInAsync();
                    }
                    break;
                case StepState.SeekNestedEntry:
                    if (line == callLine)
                    {
                        // We are at the call site, step into nested template
                        state = StepState.SeekNestedEntry;
                        await engine.StepInAsync();
                    }
                    else if (line == nestedTemplateLine)
                    {
                        state = StepState.InsideNested;
                        await engine.StepInAsync();
                    }
                    else
                    {
                        await engine.StepInAsync();
                    }
                    break;
                case StepState.InsideNested:
                    state = StepState.AfterStep;
                    await engine.StepOutAsync();
                    break;
                case StepState.AfterStep:
                    observedAfterStepOut = line;
                    state = StepState.Completed;
                    await engine.ContinueAsync();
                    break;
                default:
                    await engine.ContinueAsync();
                    break;
            }
        };

        XsltEngineManager.EngineTerminated += code => terminatedSource.TrySetResult(code);

        try
        {
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            exitCode.Should().Be(0);
            observedAfterStepOut.Should().NotBeNull("step-out should yield a stop after climbing out of the nested template");
        }
        finally
        {
            XsltEngineManager.Reset();
        }
    }

    [Fact]
    public async Task SaxonEngine_StepOut_FromNestedTemplate_ShouldPauseAtCaller()
    {
        var xsltPath = GetTestDataPath("Integration/step-into-test.xslt");
        var xmlPath = GetTestDataPath("Integration/step-into-test.xml");

        var lines = File.ReadAllLines(xsltPath);
        var callLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:call-template name=""formatCurrency""", StringComparison.Ordinal)) + 1;
        var nestedTemplateLine = Array.FindIndex(lines, line => line.Contains(@"<xsl:template name=""formatCurrency""", StringComparison.Ordinal)) + 1;
        callLine.Should().BeGreaterThan(0);
        nestedTemplateLine.Should().BeGreaterThan(0);

        var engine = new SaxonEngine();
        var terminatedSource = new TaskCompletionSource<int>();
        int? observedAfterStepOut = null;
        var state = StepState.SeekCallSite;
        var observedLines = new List<int>();

        XsltEngineManager.Reset();
        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);

        XsltEngineManager.EngineStopped += async (_, line, _) =>
        {
            observedLines.Add(line);
            switch (state)
            {
                case StepState.SeekCallSite:
                    if (line == callLine)
                    {
                        state = StepState.SeekNestedEntry;
                        await engine.StepInAsync();
                    }
                    else
                    {
                        await engine.StepInAsync();
                    }
                    break;
                case StepState.SeekNestedEntry:
                    if (line == callLine)
                    {
                        state = StepState.SeekNestedEntry;
                        await engine.StepInAsync();
                    }
                    else if (line == nestedTemplateLine)
                    {
                        state = StepState.InsideNested;
                        await engine.StepInAsync();
                    }
                    else
                    {
                        await engine.StepInAsync();
                    }
                    break;
                case StepState.InsideNested:
                    state = StepState.AfterStep;
                    await engine.StepOutAsync();
                    break;
                case StepState.AfterStep:
                    observedAfterStepOut = line;
                    state = StepState.Completed;
                    await engine.ContinueAsync();
                    break;
                default:
                    await engine.ContinueAsync();
                    break;
            }
        };

        XsltEngineManager.EngineTerminated += code => terminatedSource.TrySetResult(code);

        try
        {
            await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
            var exitCode = await terminatedSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

            exitCode.Should().Be(0);

            if (observedAfterStepOut == null)
            {
                throw new InvalidOperationException($"Saxon step-out did not pause again. Lines: {string.Join(",", observedLines)}");
            }
        }
        finally
        {
            XsltEngineManager.Reset();
        }
    }

}
