using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Saxon.Api;

namespace XsltDebugger.ConsoleTest;

/// <summary>
/// Test for LmlBasedXslt.xslt compilation
/// </summary>
class LmlCompilationTest
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== XSLT Compilation Test ===\n");

        var stylesheetPath = Path.GetFullPath("TestData/Integration/xslt/v3/LmlBasedXslt.xslt");
        var xmlPath = Path.GetFullPath("TestData/Integration/xml/ShipmentConf-lml.xml");

        if (!File.Exists(stylesheetPath))
        {
            Console.WriteLine($"ERROR: Stylesheet not found: {stylesheetPath}");
            return;
        }

        if (!File.Exists(xmlPath))
        {
            Console.WriteLine($"ERROR: XML not found: {xmlPath}");
            return;
        }

        Console.WriteLine($"📄 Stylesheet: {Path.GetFileName(stylesheetPath)}");
        Console.WriteLine($"📄 XML: {Path.GetFileName(xmlPath)}");
        Console.WriteLine("\nAttempting to compile XSLT with Saxon (XSLT 3.0)...\n");

        // Try with Saxon directly
        try
        {
            var processor = new Processor();
            processor.SetProperty("http://saxon.sf.net/feature/xsltVersion", "3.0");

            var compiler = processor.NewXsltCompiler();
            compiler.XsltLanguageVersion = "3.0";

            // Capture compilation errors
            var errorList = new List<StaticError>();
            compiler.ErrorList = errorList;

            using var styleStream = File.OpenRead(stylesheetPath);
            compiler.BaseUri = new Uri(stylesheetPath);
            var executable = compiler.Compile(styleStream);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Compilation successful!");
            Console.ResetColor();

            // Try to run the transformation
            var transformer = executable.Load();

            using var xmlStream = File.OpenRead(xmlPath);
            var docBuilder = processor.NewDocumentBuilder();
            docBuilder.BaseUri = new Uri(xmlPath);
            var inputDoc = docBuilder.Build(xmlStream);

            transformer.InitialContextNode = inputDoc;

            var output = new StringWriter();
            var serializer = processor.NewSerializer(output);

            transformer.Run(serializer);

            Console.WriteLine("\n✓ Transformation successful!");
            Console.WriteLine("\nOutput:");
            Console.WriteLine(output.ToString());
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Compilation/Transformation FAILED!");
            Console.WriteLine($"\n{ex.GetType().Name}: {ex.Message}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
            }

            Console.ResetColor();

            Console.WriteLine("\n=== DIAGNOSIS ===");
            Console.WriteLine("The XSLT has a syntax error on lines 20-22:");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  <Quantity>{if (../../Net castable as xs:decimal) then");
            Console.WriteLine("      format-number(xs:decimal(../../Net),");
            Console.WriteLine("      '0.00') else }</Quantity>");
            Console.ResetColor();
            Console.WriteLine("\nThe 'else' clause is empty! It needs a value.");
            Console.WriteLine("\nSuggested fixes:");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  1) Return empty string:");
            Console.WriteLine("     <Quantity>{if (../../Net castable as xs:decimal) then");
            Console.WriteLine("         format-number(xs:decimal(../../Net), '0.00')");
            Console.WriteLine("         else ''}</Quantity>");
            Console.WriteLine("\n  2) Return default value:");
            Console.WriteLine("     <Quantity>{if (../../Net castable as xs:decimal) then");
            Console.WriteLine("         format-number(xs:decimal(../../Net), '0.00')");
            Console.WriteLine("         else '0.00'}</Quantity>");
            Console.ResetColor();
        }
    }
}
