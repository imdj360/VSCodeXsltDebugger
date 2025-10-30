using System;
using System.IO;
using System.Threading.Tasks;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.ConsoleTest;

/// <summary>
/// Quick automated test to verify step-into functionality
/// </summary>
class QuickStepTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Quick Step-Into Verification Test ===\n");

        // Create a simple XSLT in-memory for testing
        var testDir = Path.Combine(Path.GetTempPath(), "xslt-step-test");
        Directory.CreateDirectory(testDir);

        var xsltPath = Path.Combine(testDir, "test.xslt");
        var xmlPath = Path.Combine(testDir, "test.xml");

        // Simple XSLT with nested templates (NO params/variables to avoid instrumentation issues)
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

        Console.WriteLine($"✓ Created test files in {testDir}\n");

        // Test both engines
        await TestEngine("Compiled", new XsltCompiledEngine(), xsltPath, xmlPath);
        Console.WriteLine();
        await TestEngine("Saxon", new SaxonEngine(), xsltPath, xmlPath);

        // Cleanup
        try { Directory.Delete(testDir, true); } catch { }

        Console.WriteLine("\n=== TEST COMPLETE ===");
    }

    static async Task TestEngine(string name, IXsltEngine engine, string xsltPath, string xmlPath)
    {
        Console.WriteLine($"Testing {name} Engine:");
        Console.WriteLine("─────────────────────────");

        XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Trace);

        var stopCount = 0;
        var templateEntries = 0;

        XsltEngineManager.EngineOutput += (output) =>
        {
            if (output.Contains("[template-entry]"))
            {
                templateEntries++;
                Console.WriteLine($"  ✓ Template entry detected (count: {templateEntries})");
            }
        };

        XsltEngineManager.EngineStopped += async (file, line, reason) =>
        {
            stopCount++;
            Console.WriteLine($"  → Stop #{stopCount} at line {line} (reason: {reason})");

            // Simulate step-into by continuing
            await engine.StepInAsync();
        };

        await engine.StartAsync(xsltPath, xmlPath, stopOnEntry: true);
        await Task.Delay(500);

        Console.WriteLine($"\n  Result:");
        Console.WriteLine($"    Total stops: {stopCount}");
        Console.WriteLine($"    Template entries: {templateEntries}");

        if (templateEntries >= 2)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"    ✓ PASS - Step-into is working!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"    ✗ FAIL - No template entries detected");
            Console.ResetColor();
        }
    }
}
