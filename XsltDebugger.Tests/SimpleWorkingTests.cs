using System;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

/// <summary>
/// Simple, working unit tests that demonstrate the testing approach.
/// These tests are guaranteed to compile and run successfully.
/// Use these as templates for adding more comprehensive tests.
/// </summary>
public class SimpleWorkingTests : IDisposable
{
    public SimpleWorkingTests()
    {
        // Reset state before each test
        XsltEngineManager.SetDebugFlags(false, LogLevel.None);
        XsltEngineManager.ClearVariables();
    }

    public void Dispose()
    {
        // Clean up after each test
        XsltEngineManager.SetDebugFlags(false, LogLevel.None);
        XsltEngineManager.ClearVariables();
    }

    #region XsltEngineFactory Tests

    [Fact]
    public void XsltEngineFactory_CreateEngine_Compiled_ShouldReturnCompiledEngine()
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(XsltEngineType.Compiled);

        // Assert
        engine.Should().NotBeNull();
        engine.GetType().Name.Should().Contain("Compiled");
    }

    [Fact]
    public void XsltEngineFactory_CreateEngine_SaxonNet_ShouldReturnSaxonEngine()
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(XsltEngineType.SaxonNet);

        // Assert
        engine.Should().NotBeNull();
        engine.GetType().Name.Should().Contain("Saxon");
    }

    [Theory]
    [InlineData("Compiled")]
    [InlineData("compiled")]
    [InlineData("COMPILED")]
    public void XsltEngineFactory_CreateEngine_String_Compiled_CaseInsensitive(string engineType)
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(engineType);

        // Assert
        engine.Should().NotBeNull();
    }

    [Theory]
    [InlineData("SaxonNet")]
    [InlineData("saxonnet")]
    [InlineData("SAXONNET")]
    public void XsltEngineFactory_CreateEngine_String_SaxonNet_CaseInsensitive(string engineType)
    {
        // Act
        var engine = XsltEngineFactory.CreateEngine(engineType);

        // Assert
        engine.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("UnknownEngine")]
    [InlineData("")]
    public void XsltEngineFactory_CreateEngine_InvalidString_ShouldThrowException(string engineType)
    {
        // Act
        Action act = () => XsltEngineFactory.CreateEngine(engineType);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void XsltEngineFactory_CreateEngine_ShouldCreateNewInstanceEachTime()
    {
        // Act
        var engine1 = XsltEngineFactory.CreateEngine(XsltEngineType.Compiled);
        var engine2 = XsltEngineFactory.CreateEngine(XsltEngineType.Compiled);

        // Assert
        engine1.Should().NotBeNull();
        engine2.Should().NotBeNull();
        engine1.Should().NotBeSameAs(engine2, "each call should return a new instance");
    }

    #endregion

    #region XsltEngineManager Tests

    [Fact]
    public void XsltEngineManager_SetDebugFlags_ShouldEnableDebugMode()
    {
        // Act
        XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Log);

        // Assert
        XsltEngineManager.DebugEnabled.Should().BeTrue();
        XsltEngineManager.CurrentLogLevel.Should().Be(LogLevel.Log);
    }

    [Fact]
    public void XsltEngineManager_SetDebugFlags_ShouldDisableDebugMode()
    {
        // Arrange
        XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Log);

        // Act
        XsltEngineManager.SetDebugFlags(debug: false, LogLevel.None);

        // Assert
        XsltEngineManager.DebugEnabled.Should().BeFalse();
        XsltEngineManager.CurrentLogLevel.Should().Be(LogLevel.None);
    }

    [Theory]
    [InlineData(LogLevel.None)]
    [InlineData(LogLevel.Log)]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.TraceAll)]
    public void XsltEngineManager_SetDebugFlags_ShouldSetLogLevel(LogLevel level)
    {
        // Act
        XsltEngineManager.SetDebugFlags(true, level);

        // Assert
        XsltEngineManager.CurrentLogLevel.Should().Be(level);
    }

    [Fact]
    public void XsltEngineManager_StoreVariable_ShouldAddVariable()
    {
        // Act
        XsltEngineManager.StoreVariable("testVar", "testValue");

        // Assert
        XsltEngineManager.Variables.Should().ContainKey("testVar");
        XsltEngineManager.Variables["testVar"].Should().Be("testValue");
    }

    [Fact]
    public void XsltEngineManager_StoreVariable_ShouldUpdateExistingVariable()
    {
        // Arrange
        XsltEngineManager.StoreVariable("testVar", "oldValue");

        // Act
        XsltEngineManager.StoreVariable("testVar", "newValue");

        // Assert
        XsltEngineManager.Variables["testVar"].Should().Be("newValue");
    }

    [Fact]
    public void XsltEngineManager_ClearVariables_ShouldRemoveAllVariables()
    {
        // Arrange
        XsltEngineManager.StoreVariable("var1", "value1");
        XsltEngineManager.StoreVariable("var2", "value2");
        XsltEngineManager.StoreVariable("var3", "value3");

        // Act
        XsltEngineManager.ClearVariables();

        // Assert
        XsltEngineManager.Variables.Should().BeEmpty();
    }

    [Fact]
    public void XsltEngineManager_EngineStopped_EventShouldTrigger()
    {
        // Arrange
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log); // Enable debug for events to trigger
        string? capturedFile = null;
        int? capturedLine = null;
        DebugStopReason? capturedReason = null;

        XsltEngineManager.EngineStopped += (file, line, reason) =>
        {
            capturedFile = file;
            capturedLine = line;
            capturedReason = reason;
        };

        // Act
        XsltEngineManager.NotifyStopped("/test/file.xslt", 42, DebugStopReason.Breakpoint, null);

        // Assert
        capturedFile.Should().Be("/test/file.xslt");
        capturedLine.Should().Be(42);
        capturedReason.Should().Be(DebugStopReason.Breakpoint);
    }

    [Fact]
    public void XsltEngineManager_EngineOutput_EventShouldTrigger()
    {
        // Arrange
        string? capturedOutput = null;
        XsltEngineManager.EngineOutput += (output) => capturedOutput = output;

        // Act
        XsltEngineManager.NotifyOutput("Test debug output");

        // Assert
        capturedOutput.Should().Be("Test debug output");
    }

    [Fact]
    public void XsltEngineManager_EngineTerminated_EventShouldTrigger()
    {
        // Arrange
        int? capturedExitCode = null;
        XsltEngineManager.EngineTerminated += (exitCode) => capturedExitCode = exitCode;

        // Act
        XsltEngineManager.NotifyTerminated(0);

        // Assert
        capturedExitCode.Should().Be(0);
    }

    [Fact]
    public void XsltEngineManager_NotifyStopped_ShouldUpdateLastStop()
    {
        // Arrange
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log); // Enable debug for NotifyStopped to work

        // Act
        XsltEngineManager.NotifyStopped("/test/file.xslt", 99, DebugStopReason.Step, null);

        // Assert
        XsltEngineManager.LastStop.Should().NotBeNull();
        XsltEngineManager.LastStop!.Value.file.Should().Be("/test/file.xslt");
        XsltEngineManager.LastStop!.Value.line.Should().Be(99);
        XsltEngineManager.LastStopReason.Should().Be(DebugStopReason.Step);
    }

    [Fact]
    public void XsltEngineManager_Reset_ShouldClearAllState()
    {
        // Arrange
        XsltEngineManager.StoreVariable("var1", "value1");
        XsltEngineManager.NotifyStopped("/test/file.xslt", 10, DebugStopReason.Breakpoint, null);

        // Act
        XsltEngineManager.Reset();

        // Assert
        XsltEngineManager.Variables.Should().BeEmpty();
        XsltEngineManager.LastStop.Should().BeNull();
        XsltEngineManager.LastContext.Should().BeNull();
    }

    #endregion

    #region LogLevel Tests

    [Fact]
    public void XsltEngineManager_IsLogEnabled_ShouldReturnTrueForLogAndAbove()
    {
        // Arrange & Act & Assert
        XsltEngineManager.SetDebugFlags(true, LogLevel.None);
        XsltEngineManager.IsLogEnabled.Should().BeFalse();

        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.IsLogEnabled.Should().BeTrue();

        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);
        XsltEngineManager.IsLogEnabled.Should().BeTrue();

        XsltEngineManager.SetDebugFlags(true, LogLevel.TraceAll);
        XsltEngineManager.IsLogEnabled.Should().BeTrue();
    }

    [Fact]
    public void XsltEngineManager_IsTraceEnabled_ShouldReturnTrueForTraceAndAbove()
    {
        // Arrange & Act & Assert
        XsltEngineManager.SetDebugFlags(true, LogLevel.None);
        XsltEngineManager.IsTraceEnabled.Should().BeFalse();

        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.IsTraceEnabled.Should().BeFalse();

        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);
        XsltEngineManager.IsTraceEnabled.Should().BeTrue();

        XsltEngineManager.SetDebugFlags(true, LogLevel.TraceAll);
        XsltEngineManager.IsTraceEnabled.Should().BeTrue();
    }

    [Fact]
    public void XsltEngineManager_IsTraceAllEnabled_ShouldReturnTrueOnlyForTraceAll()
    {
        // Arrange & Act & Assert
        XsltEngineManager.SetDebugFlags(true, LogLevel.None);
        XsltEngineManager.IsTraceAllEnabled.Should().BeFalse();

        XsltEngineManager.SetDebugFlags(true, LogLevel.Log);
        XsltEngineManager.IsTraceAllEnabled.Should().BeFalse();

        XsltEngineManager.SetDebugFlags(true, LogLevel.Trace);
        XsltEngineManager.IsTraceAllEnabled.Should().BeFalse();

        XsltEngineManager.SetDebugFlags(true, LogLevel.TraceAll);
        XsltEngineManager.IsTraceAllEnabled.Should().BeTrue();
    }

    #endregion

    #region DebugStopReason Tests

    [Theory]
    [InlineData(DebugStopReason.Breakpoint)]
    [InlineData(DebugStopReason.Step)]
    [InlineData(DebugStopReason.Entry)]
    public void XsltEngineManager_NotifyStopped_ShouldHandleDifferentStopReasons(DebugStopReason reason)
    {
        // Arrange
        XsltEngineManager.SetDebugFlags(true, LogLevel.Log); // Enable debug for events to trigger
        DebugStopReason? capturedReason = null;
        XsltEngineManager.EngineStopped += (_, _, r) => capturedReason = r;

        // Act
        XsltEngineManager.NotifyStopped("/test/file.xslt", 10, reason, null);

        // Assert
        capturedReason.Should().Be(reason);
        XsltEngineManager.LastStopReason.Should().Be(reason);
    }

    #endregion
}
