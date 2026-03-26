using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

public class CliTransformRunnerTests
{
    private static string GetRepoRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string TestData(string relative) =>
        Path.GetFullPath(Path.Combine(GetRepoRoot(), "TestData", relative.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public async Task RunAsync_CompiledEngine_WritesOutputToWriter_ReturnsZero()
    {
        var stylesheet = TestData("Integration/xslt/compiled/sample.xslt");
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--engine", "compiled", "--stylesheet", stylesheet, "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(0);
        writer.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAsync_SaxonEngine_WritesOutputToWriter_ReturnsZero()
    {
        var stylesheet = TestData("Integration/xslt/saxon/sample-xslt3.xslt");
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--engine", "saxonnet", "--stylesheet", stylesheet, "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(0);
        writer.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RunAsync_MissingStylesheet_ReturnsExitCode3()
    {
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--stylesheet", "/nonexistent/file.xslt", "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_MissingXmlFile_ReturnsExitCode3()
    {
        var stylesheet = TestData("Integration/xslt/compiled/sample.xslt");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--stylesheet", stylesheet, "--xml", "/nonexistent/input.xml" };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_InvalidEngine_ReturnsExitCode2()
    {
        var stylesheet = TestData("Integration/xslt/compiled/sample.xslt");
        var xml = TestData("Integration/xml/sample.xml");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--engine", "unknownengine", "--stylesheet", stylesheet, "--xml", xml };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_BadXslt_ReturnsExitCode1()
    {
        var badXslt = Path.GetTempFileName() + ".xslt";
        var xml = TestData("Integration/xml/sample.xml");
        await File.WriteAllTextAsync(badXslt, "<xsl:stylesheet version=\"1.0\" xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\"><INVALID>");
        var writer = new StringWriter();

        try
        {
            var args = new[] { "--transform", "--engine", "compiled", "--stylesheet", badXslt, "--xml", xml };
            var runner = new CliTransformRunner(args, writer);
            var exitCode = await runner.RunAsync();

            exitCode.Should().Be(1);
        }
        finally
        {
            File.Delete(badXslt);
        }
    }

    [Fact]
    public async Task RunAsync_LaunchConfig_ReadsConfigAndRuns()
    {
        var repoRoot = GetRepoRoot();
        var launchJson = Path.Combine(repoRoot, ".vscode", "launch.json");
        var writer = new StringWriter();

        var args = new[] { "--transform", "--launch-config", launchJson, "--config-name", "XSLT: Launch (log level)" };
        var runner = new CliTransformRunner(args, writer);
        var exitCode = await runner.RunAsync();

        exitCode.Should().Be(0);
        writer.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
