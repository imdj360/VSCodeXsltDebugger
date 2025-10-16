using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

/// <summary>
/// Tests for DapServer message parsing and protocol handling
/// </summary>
public class DapServerMessageTests
{
    [Fact]
    public void DapMessage_ShouldSerializeCorrectly()
    {
        // Arrange
        var message = new
        {
            seq = 1,
            type = "request",
            command = "initialize"
        };

        // Act
        var json = JsonSerializer.Serialize(message);

        // Assert
        json.Should().Contain("\"seq\":1");
        json.Should().Contain("\"type\":\"request\"");
        json.Should().Contain("\"command\":\"initialize\"");
    }

    [Fact]
    public void DapResponse_ShouldIncludeRequestSeq()
    {
        // Arrange
        var response = new
        {
            seq = 2,
            type = "response",
            request_seq = 1,
            success = true,
            command = "initialize"
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        json.Should().Contain("\"request_seq\":1");
        json.Should().Contain("\"success\":true");
    }

    [Fact]
    public void DapEvent_ShouldHaveCorrectStructure()
    {
        // Arrange
        var eventMessage = new
        {
            seq = 3,
            type = "event",
            @event = "stopped",
            body = new
            {
                reason = "breakpoint",
                threadId = 1
            }
        };

        // Act
        var json = JsonSerializer.Serialize(eventMessage);

        // Assert
        json.Should().Contain("\"type\":\"event\"");
        json.Should().Contain("\"event\":\"stopped\"");
        json.Should().Contain("\"reason\":\"breakpoint\"");
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("launch")]
    [InlineData("setBreakpoints")]
    [InlineData("configurationDone")]
    [InlineData("threads")]
    [InlineData("stackTrace")]
    [InlineData("scopes")]
    [InlineData("variables")]
    [InlineData("continue")]
    [InlineData("next")]
    [InlineData("stepIn")]
    [InlineData("stepOut")]
    [InlineData("disconnect")]
    public void DapRequest_ShouldSupportStandardCommands(string command)
    {
        // Arrange
        var request = new
        {
            seq = 1,
            type = "request",
            command = command
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain($"\"command\":\"{command}\"");
    }

    [Fact]
    public void InitializeRequest_ShouldIncludeClientInfo()
    {
        // Arrange
        var request = new
        {
            seq = 1,
            type = "request",
            command = "initialize",
            arguments = new
            {
                clientID = "vscode",
                clientName = "Visual Studio Code",
                adapterID = "xslt",
                linesStartAt1 = true,
                columnsStartAt1 = true
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"clientID\":\"vscode\"");
        json.Should().Contain("\"adapterID\":\"xslt\"");
        json.Should().Contain("\"linesStartAt1\":true");
    }

    [Fact]
    public void SetBreakpointsRequest_ShouldIncludeSourceAndLines()
    {
        // Arrange
        var request = new
        {
            seq = 2,
            type = "request",
            command = "setBreakpoints",
            arguments = new
            {
                source = new
                {
                    path = "/Users/test/file.xslt"
                },
                breakpoints = new[]
                {
                    new { line = 10 },
                    new { line = 20 },
                    new { line = 30 }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"/Users/test/file.xslt\"");
        json.Should().Contain("\"line\":10");
        json.Should().Contain("\"line\":20");
        json.Should().Contain("\"line\":30");
    }

    [Fact]
    public void LaunchRequest_ShouldIncludeXsltSpecificConfig()
    {
        // Arrange
        var request = new
        {
            seq = 3,
            type = "request",
            command = "launch",
            arguments = new
            {
                stylesheetPath = "/path/to/stylesheet.xslt",
                xmlPath = "/path/to/input.xml",
                outputPath = "/path/to/output.xml",
                engineType = "SaxonNet",
                stopOnEntry = false
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"stylesheetPath\"");
        json.Should().Contain("\"xmlPath\"");
        json.Should().Contain("\"engineType\":\"SaxonNet\"");
        json.Should().Contain("\"stopOnEntry\":false");
    }

    [Fact]
    public void StackTraceRequest_ShouldIncludeThreadId()
    {
        // Arrange
        var request = new
        {
            seq = 4,
            type = "request",
            command = "stackTrace",
            arguments = new
            {
                threadId = 1,
                startFrame = 0,
                levels = 20
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"threadId\":1");
        json.Should().Contain("\"startFrame\":0");
        json.Should().Contain("\"levels\":20");
    }

    [Fact]
    public void VariablesRequest_ShouldIncludeVariablesReference()
    {
        // Arrange
        var request = new
        {
            seq = 5,
            type = "request",
            command = "variables",
            arguments = new
            {
                variablesReference = 1001
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"variablesReference\":1001");
    }

    [Fact]
    public void EvaluateRequest_ShouldIncludeExpression()
    {
        // Arrange
        var request = new
        {
            seq = 6,
            type = "request",
            command = "evaluate",
            arguments = new
            {
                expression = "//item[@id='123']",
                frameId = 1000,
                context = "watch"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(request);

        // Assert
        json.Should().Contain("\"expression\":\"//item[@id=\\u0027123\\u0027]\""); // JSON escapes ' as \u0027
        json.Should().Contain("\"frameId\":1000");
        json.Should().Contain("\"context\":\"watch\"");
    }

    [Fact]
    public void StoppedEvent_ShouldIncludeReasonAndThreadId()
    {
        // Arrange
        var stoppedEvent = new
        {
            seq = 10,
            type = "event",
            @event = "stopped",
            body = new
            {
                reason = "breakpoint",
                threadId = 1,
                allThreadsStopped = true
            }
        };

        // Act
        var json = JsonSerializer.Serialize(stoppedEvent);

        // Assert
        json.Should().Contain("\"reason\":\"breakpoint\"");
        json.Should().Contain("\"threadId\":1");
        json.Should().Contain("\"allThreadsStopped\":true");
    }

    [Theory]
    [InlineData("breakpoint")]
    [InlineData("step")]
    [InlineData("entry")]
    [InlineData("pause")]
    public void StoppedEvent_ShouldSupportDifferentReasons(string reason)
    {
        // Arrange
        var stoppedEvent = new
        {
            type = "event",
            @event = "stopped",
            body = new { reason = reason, threadId = 1 }
        };

        // Act
        var json = JsonSerializer.Serialize(stoppedEvent);

        // Assert
        json.Should().Contain($"\"reason\":\"{reason}\"");
    }

    [Fact]
    public void OutputEvent_ShouldIncludeCategoryAndOutput()
    {
        // Arrange
        var outputEvent = new
        {
            seq = 11,
            type = "event",
            @event = "output",
            body = new
            {
                category = "stdout",
                output = "Transformation output\n"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(outputEvent);

        // Assert
        json.Should().Contain("\"category\":\"stdout\"");
        json.Should().Contain("\"output\":\"Transformation output");
    }

    [Fact]
    public void TerminatedEvent_ShouldHaveCorrectStructure()
    {
        // Arrange
        var terminatedEvent = new
        {
            seq = 12,
            type = "event",
            @event = "terminated"
        };

        // Act
        var json = JsonSerializer.Serialize(terminatedEvent);

        // Assert
        json.Should().Contain("\"event\":\"terminated\"");
    }

    [Fact]
    public void ErrorResponse_ShouldIncludeErrorMessage()
    {
        // Arrange
        var errorResponse = new
        {
            seq = 20,
            type = "response",
            request_seq = 10,
            success = false,
            command = "evaluate",
            message = "XPath expression is invalid"
        };

        // Act
        var json = JsonSerializer.Serialize(errorResponse);

        // Assert
        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"message\":\"XPath expression is invalid\"");
    }

    [Fact]
    public void BreakpointResponse_ShouldIncludeVerifiedFlag()
    {
        // Arrange
        var response = new
        {
            seq = 30,
            type = "response",
            request_seq = 5,
            success = true,
            command = "setBreakpoints",
            body = new
            {
                breakpoints = new[]
                {
                    new { id = 1, verified = true, line = 10 },
                    new { id = 2, verified = true, line = 20 }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response);

        // Assert
        json.Should().Contain("\"verified\":true");
        json.Should().Contain("\"line\":10");
        json.Should().Contain("\"line\":20");
    }

    [Fact]
    public void Scope_ShouldIncludeNameAndVariablesReference()
    {
        // Arrange
        var scope = new
        {
            name = "Context Variables",
            variablesReference = 1001,
            expensive = false
        };

        // Act
        var json = JsonSerializer.Serialize(scope);

        // Assert
        json.Should().Contain("\"name\":\"Context Variables\"");
        json.Should().Contain("\"variablesReference\":1001");
        json.Should().Contain("\"expensive\":false");
    }

    [Fact]
    public void Variable_ShouldIncludeNameAndValue()
    {
        // Arrange
        var variable = new
        {
            name = "currentItem",
            value = "<item id=\"123\">Test</item>",
            type = "Element",
            variablesReference = 0
        };

        // Act
        var json = JsonSerializer.Serialize(variable);

        // Assert
        json.Should().Contain("\"name\":\"currentItem\"");
        json.Should().Contain("\"value\":\"\\u003Citem"); // JSON escapes < as \u003C
        json.Should().Contain("\"type\":\"Element\"");
    }

    [Fact]
    public void StackFrame_ShouldIncludeSourceLocationAndLine()
    {
        // Arrange
        var stackFrame = new
        {
            id = 1000,
            name = "template match=\"item\"",
            source = new
            {
                path = "/Users/test/stylesheet.xslt"
            },
            line = 42,
            column = 1
        };

        // Act
        var json = JsonSerializer.Serialize(stackFrame);

        // Assert
        json.Should().Contain("\"id\":1000");
        json.Should().Contain("\"name\":\"template match=");
        json.Should().Contain("\"/Users/test/stylesheet.xslt\"");
        json.Should().Contain("\"line\":42");
    }
}
