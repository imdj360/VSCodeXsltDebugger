using System;
using System.IO;
using System.Xml.Linq;
using Saxon.Api;

namespace XsltDebugger.ConsoleTest;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== XSLT 3.0 Saxon Test ===\n");

        var stylesheetPath = args.Length > 0 ? args[0] : "../../ShipmentConf.xslt";
        var xmlPath = args.Length > 1 ? args[1] : "../../ShipmentConf.xml";

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

        try
        {
            Console.WriteLine("1. Loading XSLT stylesheet...");
            var xdoc = XDocument.Load(stylesheetPath, LoadOptions.SetLineInfo);

            if (xdoc.Root == null)
            {
                Console.WriteLine("ERROR: No root element in stylesheet");
                return;
            }

            Console.WriteLine($"   Root: {xdoc.Root.Name}");
            Console.WriteLine($"   Version: {xdoc.Root.Attribute("version")?.Value ?? "not specified"}");

            // List all top-level elements
            var xsltNs = xdoc.Root.Name.Namespace;
            Console.WriteLine("\n2. Top-level XSLT elements:");
            foreach (var element in xdoc.Root.Elements())
            {
                if (element.Name.Namespace == xsltNs)
                {
                    var line = element is System.Xml.IXmlLineInfo info && info.HasLineInfo()
                        ? info.LineNumber.ToString()
                        : "?";
                    Console.WriteLine($"   - {element.Name.LocalName} (line {line})");
                }
            }

            Console.WriteLine("\n3. Initializing Saxon processor...");
            var processor = new Processor();
            processor.SetProperty("http://saxon.sf.net/feature/xsltVersion", "3.0");

            var compiler = processor.NewXsltCompiler();
            compiler.XsltLanguageVersion = "3.0";

            Console.WriteLine("\n4. Compiling stylesheet (WITHOUT instrumentation)...");
            XsltExecutable executable;
            using (var reader = xdoc.CreateReader())
            {
                var documentBuilder = processor.NewDocumentBuilder();
                var stylesheetDoc = documentBuilder.Build(reader);
                executable = compiler.Compile(stylesheetDoc);
            }
            Console.WriteLine("   ✓ Compilation successful!");

            Console.WriteLine("\n5. Loading input XML...");
            var inputBuilder = processor.NewDocumentBuilder();
            var inputDoc = inputBuilder.Build(new Uri(Path.GetFullPath(xmlPath)));
            Console.WriteLine("   ✓ XML loaded!");

            Console.WriteLine("\n6. Running transformation...");
            var transformer = executable.Load();
            transformer.InitialContextNode = inputDoc;

            var outputPath = Path.ChangeExtension(stylesheetPath, ".console-test.out.xml");
            using (var writer = new StreamWriter(outputPath))
            {
                var serializer = processor.NewSerializer(writer);
                transformer.Run(serializer);
            }

            Console.WriteLine($"   ✓ Transformation complete!");
            Console.WriteLine($"   Output written to: {outputPath}");

            // Show first few lines of output
            Console.WriteLine("\n7. Output preview:");
            var outputLines = File.ReadAllLines(outputPath);
            for (int i = 0; i < Math.Min(10, outputLines.Length); i++)
            {
                Console.WriteLine($"   {outputLines[i]}");
            }
            if (outputLines.Length > 10)
            {
                Console.WriteLine($"   ... ({outputLines.Length - 10} more lines)");
            }

            Console.WriteLine("\n=== SUCCESS ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n!!! ERROR !!!");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
        }
    }
}
