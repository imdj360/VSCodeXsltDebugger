using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.Tests;

public class AttributeInstrumentationTests
{
    private const string TestDataFolder = "TestData/Integration";

    [Theory]
    [InlineData("compiled")]
    [InlineData("saxon")]
    public async Task XslAttribute_ShouldNotBeInstrumented(string engineType)
    {
        // Arrange
        var xsltPath = Path.Combine(TestDataFolder, "attribute-test.xslt");
        var xmlPath = Path.Combine(TestDataFolder, "attribute-test.xml");

        if (!File.Exists(xsltPath) || !File.Exists(xmlPath))
        {
            // Skip test if files don't exist
            return;
        }

        IXsltEngine engine = engineType == "compiled"
            ? new XsltCompiledEngine()
            : new SaxonEngine();

        XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Trace);

        var fullXsltPath = Path.GetFullPath(xsltPath);
        engine.SetBreakpoints(new[] { (fullXsltPath, 12) });

        var completed = false;
        var transformationSucceeded = false;

        XsltEngineManager.EngineTerminated += (exitCode) =>
        {
            completed = true;
            transformationSucceeded = exitCode == 0;
        };

        // Act
        await engine.StartAsync(fullXsltPath, Path.GetFullPath(xmlPath), stopOnEntry: false);
        await Task.Delay(2000);

        // Assert
        Assert.True(completed, "Transformation did not complete");
        Assert.True(transformationSucceeded, "Transformation failed");

        // Verify output file exists and has correct content
        var outputPath = Path.Combine(TestDataFolder, "out", "attribute-test.out.xml");
        Assert.True(File.Exists(outputPath), "Output file was not created");

        var outputDoc = XDocument.Load(outputPath);
        var elements = outputDoc.Root?.Elements("element");
        Assert.NotNull(elements);
        Assert.Equal(3, elements.Count());

        // Verify attributes are correctly set
        var elementList = elements.ToList();
        Assert.Equal("1", elementList[0].Attribute("id")?.Value);
        Assert.Equal("test-type", elementList[0].Attribute("type")?.Value);
        Assert.Equal("First Item", elementList[0].Value);

        Assert.Equal("2", elementList[1].Attribute("id")?.Value);
        Assert.Equal("test-type", elementList[1].Attribute("type")?.Value);
        Assert.Equal("Second Item", elementList[1].Value);

        Assert.Equal("3", elementList[2].Attribute("id")?.Value);
        Assert.Equal("test-type", elementList[2].Attribute("type")?.Value);
        Assert.Equal("Third Item", elementList[2].Value);
    }

    [Fact]
    public void Xslt1Instrumentation_ShouldSkipAttributeElements()
    {
        // Arrange
        var xslt = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
    <xsl:template match=""/"">
        <element>
            <xsl:attribute name=""id"">
                <xsl:value-of select=""@id""/>
            </xsl:attribute>
            <xsl:value-of select=""text()""/>
        </element>
    </xsl:template>
</xsl:stylesheet>";

        var doc = XDocument.Parse(xslt, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        var debugNamespace = XNamespace.Get("urn:xslt-debugger");

        // Act
        Xslt1Instrumentation.InstrumentStylesheet(doc, "test.xslt", debugNamespace, addProbeAttribute: false);

        // Assert - verify no instrumentation inside xsl:attribute
        var xslNamespace = XNamespace.Get("http://www.w3.org/1999/XSL/Transform");
        var attributes = doc.Descendants(xslNamespace + "attribute").ToList();

        foreach (var attribute in attributes)
        {
            // Check that there are no dbg:break calls inside xsl:attribute elements
            var breakCalls = attribute.Descendants(xslNamespace + "value-of")
                .Where(e => e.Attribute("select")?.Value.Contains("dbg:break") == true)
                .ToList();

            Assert.Empty(breakCalls);
        }
    }

    [Theory]
    [InlineData("compiled")]
    [InlineData("saxon")]
    public async Task AttributeMessageTest_ShouldGenerateAttributesInTargetMessages(string engineType)
    {
        // Arrange
        var xsltPath = Path.Combine(TestDataFolder, "attribute-message-test.xslt");
        var xmlPath = Path.Combine(TestDataFolder, "attribute-message-test.xml");

        if (!File.Exists(xsltPath) || !File.Exists(xmlPath))
        {
            // Skip test if files don't exist
            return;
        }

        IXsltEngine engine = engineType == "compiled"
            ? new XsltCompiledEngine()
            : new SaxonEngine();

        XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Log);

        var fullXsltPath = Path.GetFullPath(xsltPath);
        engine.SetBreakpoints(new[] { (fullXsltPath, 12) });

        var completed = false;
        var transformationSucceeded = false;

        XsltEngineManager.EngineTerminated += (exitCode) =>
        {
            completed = true;
            transformationSucceeded = exitCode == 0;
        };

        // Act
        await engine.StartAsync(fullXsltPath, Path.GetFullPath(xmlPath), stopOnEntry: false);
        await Task.Delay(2000);

        // Assert
        Assert.True(completed, "Transformation did not complete");
        Assert.True(transformationSucceeded, "Transformation failed");

        // Verify output file exists and has correct content
        var outputPath = Path.Combine(TestDataFolder, "out", "attribute-message-test.out.xml");
        Assert.True(File.Exists(outputPath), "Output file was not created");

        var outputDoc = XDocument.Load(outputPath);
        var messages = outputDoc.Root?.Elements("message").ToList();
        Assert.NotNull(messages);
        Assert.Equal(3, messages.Count);

        // Verify first message with high priority
        Assert.Equal("ORD-001", messages[0].Attribute("orderId")?.Value);
        Assert.Equal("urgent", messages[0].Attribute("status")?.Value);
        Assert.Equal("Alice Johnson", messages[0].Attribute("customer")?.Value);
        Assert.Contains("Laptop", messages[0].Element("content")?.Value ?? "");

        // Verify second message with low priority
        Assert.Equal("ORD-002", messages[1].Attribute("orderId")?.Value);
        Assert.Equal("normal", messages[1].Attribute("status")?.Value);
        Assert.Equal("Bob Smith", messages[1].Attribute("customer")?.Value);
        Assert.Contains("Mouse", messages[1].Element("content")?.Value ?? "");

        // Verify third message with high priority
        Assert.Equal("ORD-003", messages[2].Attribute("orderId")?.Value);
        Assert.Equal("urgent", messages[2].Attribute("status")?.Value);
        Assert.Equal("Carol White", messages[2].Attribute("customer")?.Value);
        Assert.Contains("Keyboard", messages[2].Element("content")?.Value ?? "");
    }
}
