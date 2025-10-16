using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

/// <summary>
/// Tests for SessionState to ensure breakpoint management works correctly.
/// Note: SessionState is internal but accessible via InternalsVisibleTo.
/// </summary>
public class SessionStateTests
{
    [Fact]
    public void SetEngine_ShouldInitializeEngine()
    {
        // Arrange
        var state = new SessionState();
        var engine = new MockXsltEngine();

        // Act
        state.SetEngine(engine);

        // Assert
        state.Engine.Should().Be(engine);
    }

    [Fact]
    public void SetBreakpoints_ShouldStoreBreakpointsForFile()
    {
        // Arrange
        var state = new SessionState();
        var filePath = "/Users/test/file.xslt";
        var breakpoints = new[] { 10, 20, 30 };

        // Act
        var result = state.SetBreakpoints(filePath, breakpoints);

        // Assert
        result.Should().BeEquivalentTo(breakpoints);

        var stored = state.GetBreakpointsFor(filePath).ToList();
        stored.Should().HaveCount(3);
        stored.Select(bp => bp.line).Should().BeEquivalentTo(breakpoints);
    }

    [Fact]
    public void SetBreakpoints_ShouldReplaceExistingBreakpoints()
    {
        // Arrange
        var state = new SessionState();
        var filePath = "/Users/test/file.xslt";

        // Act
        state.SetBreakpoints(filePath, new[] { 10, 20 });
        state.SetBreakpoints(filePath, new[] { 30, 40 });

        // Assert
        var stored = state.GetBreakpointsFor(filePath).Select(bp => bp.line).ToList();
        stored.Should().BeEquivalentTo(new[] { 30, 40 });
        stored.Should().NotContain(10);
        stored.Should().NotContain(20);
    }

    [Fact]
    public void GetBreakpointsFor_NonExistentFile_ShouldReturnEmptyList()
    {
        // Arrange
        var state = new SessionState();

        // Act
        var result = state.GetBreakpointsFor("/nonexistent/file.xslt");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllBreakpoints_ShouldReturnAllBreakpointsFromAllFiles()
    {
        // Arrange
        var state = new SessionState();
        state.SetBreakpoints("/file1.xslt", new[] { 10, 20 });
        state.SetBreakpoints("/file2.xslt", new[] { 30, 40 });

        // Act
        var allBreakpoints = state.GetAllBreakpoints();

        // Assert
        allBreakpoints.Should().HaveCount(4);
        allBreakpoints.Should().Contain(bp => bp.file.EndsWith("file1.xslt") && bp.line == 10);
        allBreakpoints.Should().Contain(bp => bp.file.EndsWith("file1.xslt") && bp.line == 20);
        allBreakpoints.Should().Contain(bp => bp.file.EndsWith("file2.xslt") && bp.line == 30);
        allBreakpoints.Should().Contain(bp => bp.file.EndsWith("file2.xslt") && bp.line == 40);
    }

    [Fact]
    public void SetBreakpoints_ShouldHandleDuplicateLineNumbers()
    {
        // Arrange
        var state = new SessionState();
        var filePath = "/Users/test/file.xslt";

        // Act
        var result = state.SetBreakpoints(filePath, new[] { 10, 10, 20, 20, 30 });

        // Assert
        result.Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    [Fact]
    public void SetBreakpoints_WithEmptyArray_ShouldClearBreakpoints()
    {
        // Arrange
        var state = new SessionState();
        var filePath = "/Users/test/file.xslt";
        state.SetBreakpoints(filePath, new[] { 10, 20, 30 });

        // Act
        state.SetBreakpoints(filePath, Array.Empty<int>());

        // Assert
        var stored = state.GetBreakpointsFor(filePath);
        stored.Should().BeEmpty();
    }

    [Fact]
    public void SetBreakpoints_ShouldHandleMultipleFiles()
    {
        // Arrange
        var state = new SessionState();

        // Act
        state.SetBreakpoints("/file1.xslt", new[] { 10 });
        state.SetBreakpoints("/file2.xslt", new[] { 20 });
        state.SetBreakpoints("/file3.xslt", new[] { 30 });

        // Assert
        state.GetBreakpointsFor("/file1.xslt").Select(bp => bp.line).Should().BeEquivalentTo(new[] { 10 });
        state.GetBreakpointsFor("/file2.xslt").Select(bp => bp.line).Should().BeEquivalentTo(new[] { 20 });
        state.GetBreakpointsFor("/file3.xslt").Select(bp => bp.line).Should().BeEquivalentTo(new[] { 30 });
    }

    [Theory]
    [InlineData("C:\\Users\\test\\file.xslt")]
    [InlineData("/Users/test/file.xslt")]
    [InlineData("/home/user/project/file.xslt")]
    public void SetBreakpoints_ShouldHandleDifferentPathFormats(string path)
    {
        // Arrange
        var state = new SessionState();

        // Act
        state.SetBreakpoints(path, new[] { 10 });

        // Assert
        var stored = state.GetBreakpointsFor(path).Select(bp => bp.line);
        stored.Should().Contain(10);
    }

    [Fact]
    public void SetEngine_WithExistingBreakpoints_ShouldApplyBreakpointsToEngine()
    {
        // Arrange
        var state = new SessionState();
        var engine = new MockXsltEngine();
        state.SetBreakpoints("/file1.xslt", new[] { 10, 20 });
        state.SetBreakpoints("/file2.xslt", new[] { 30 });

        // Act
        state.SetEngine(engine);

        // Assert
        engine.BreakpointsSet.Should().HaveCount(3);
        engine.BreakpointsSet.Should().Contain(bp => bp.line == 10);
        engine.BreakpointsSet.Should().Contain(bp => bp.line == 20);
        engine.BreakpointsSet.Should().Contain(bp => bp.line == 30);
    }

    [Fact]
    public void ClearEngine_ShouldRemoveEngine()
    {
        // Arrange
        var state = new SessionState();
        var engine = new MockXsltEngine();
        state.SetEngine(engine);

        // Act
        state.ClearEngine();

        // Assert
        state.Engine.Should().BeNull();
    }

    [Fact]
    public void SetEngine_ShouldSetDebugFlags()
    {
        // Arrange
        var state = new SessionState();
        var engine = new MockXsltEngine();

        // Act
        state.SetEngine(engine, debug: true, logLevel: LogLevel.Trace);

        // Assert
        state.DebugEnabled.Should().BeTrue();
        state.CurrentLogLevel.Should().Be(LogLevel.Trace);
    }

    [Fact]
    public void SetBreakpoints_ShouldReturnSortedBreakpoints()
    {
        // Arrange
        var state = new SessionState();
        var filePath = "/Users/test/file.xslt";

        // Act
        var result = state.SetBreakpoints(filePath, new[] { 30, 10, 20 });

        // Assert
        result.Should().BeInAscendingOrder();
        result.Should().Equal(10, 20, 30);
    }

    // Mock implementation for testing
    private class MockXsltEngine : IXsltEngine
    {
        public List<(string file, int line)> BreakpointsSet { get; } = new();

        public Task StartAsync(string stylesheetPath, string xmlPath, bool stopOnEntry)
        {
            return Task.CompletedTask;
        }

        public void SetBreakpoints(IEnumerable<(string file, int line)> breakpoints)
        {
            BreakpointsSet.Clear();
            BreakpointsSet.AddRange(breakpoints);
        }

        public Task ContinueAsync()
        {
            return Task.CompletedTask;
        }

        public Task StepOverAsync()
        {
            return Task.CompletedTask;
        }

        public Task StepInAsync()
        {
            return Task.CompletedTask;
        }

        public Task StepOutAsync()
        {
            return Task.CompletedTask;
        }

        public void Terminate()
        {
        }
    }
}
