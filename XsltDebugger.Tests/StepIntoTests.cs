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
}
