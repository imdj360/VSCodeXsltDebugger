using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Saxon.Api;

namespace XsltDebugger.ConsoleTest;

class ProgramWithInstrumentation
{
    private const string DebugNamespace = "urn:xslt-debugger";

    static void Main(string[] args)
    {
        Console.WriteLine("=== XSLT 3.0 Saxon Test WITH INSTRUMENTATION ===\n");

        var stylesheetPath = args.Length > 0 ? args[0] : "ShipmentConf3.xslt";
        var xmlPath = args.Length > 1 ? args[1] : "ShipmentConf.xml";

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

            // Instrument the stylesheet
            Console.WriteLine("\n2. INSTRUMENTING stylesheet (adding debug breakpoints)...");
            EnsureDebugNamespace(xdoc);
            InstrumentStylesheet(xdoc);

            // Save instrumented version for inspection
            var instrumentedPath = Path.ChangeExtension(stylesheetPath, ".instrumented.xslt");
            xdoc.Save(instrumentedPath);
            Console.WriteLine($"   Instrumented stylesheet saved to: {instrumentedPath}");

            Console.WriteLine("\n3. Initializing Saxon processor...");
            var processor = new Processor();
            processor.SetProperty("http://saxon.sf.net/feature/xsltVersion", "3.0");

            var compiler = processor.NewXsltCompiler();
            compiler.XsltLanguageVersion = "3.0";

            // Register the debug extension function
            Console.WriteLine("\n4. Registering debug extension function...");
            processor.RegisterExtensionFunction(new DummyDebugExtension());

            Console.WriteLine("\n5. Compiling INSTRUMENTED stylesheet...");
            XsltExecutable executable;
            using (var reader = xdoc.CreateReader())
            {
                var documentBuilder = processor.NewDocumentBuilder();
                var stylesheetDoc = documentBuilder.Build(reader);
                executable = compiler.Compile(stylesheetDoc);
            }
            Console.WriteLine("   ✓ Compilation successful!");

            Console.WriteLine("\n6. Loading input XML...");
            var inputBuilder = processor.NewDocumentBuilder();
            var inputDoc = inputBuilder.Build(new Uri(Path.GetFullPath(xmlPath)));
            Console.WriteLine("   ✓ XML loaded!");

            Console.WriteLine("\n7. Running transformation...");
            var transformer = executable.Load();
            transformer.InitialContextNode = inputDoc;

            var outputPath = Path.ChangeExtension(stylesheetPath, ".instrumented-test.out.xml");
            using (var writer = new StreamWriter(outputPath))
            {
                var serializer = processor.NewSerializer(writer);
                transformer.Run(serializer);
            }

            Console.WriteLine($"   ✓ Transformation complete!");
            Console.WriteLine($"   Output written to: {outputPath}");

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

    private static void EnsureDebugNamespace(XDocument doc)
    {
        if (doc.Root == null) return;

        var existing = doc.Root.GetNamespaceOfPrefix("dbg");
        if (existing == null || existing.NamespaceName != DebugNamespace)
        {
            doc.Root.SetAttributeValue(XNamespace.Xmlns + "dbg", DebugNamespace);
        }
    }

    private static void InstrumentStylesheet(XDocument doc)
    {
        if (doc.Root == null) return;

        var xsltNamespace = doc.Root.Name.Namespace;
        var candidates = doc
            .Descendants()
            .Where(e => ShouldInstrument(e, xsltNamespace))
            .Select(e => (Element: e, Line: GetLineNumber(e)))
            .Where(tuple => tuple.Line.HasValue)
            .ToList();

        Console.WriteLine($"   Found {candidates.Count} elements to instrument:");
        foreach (var (element, line) in candidates)
        {
            Console.WriteLine($"     - {element.Name.LocalName} at line {line}");
        }

        foreach (var (element, line) in candidates)
        {
            if (element.Parent == null) continue;

            var breakCall = new XElement(
                xsltNamespace + "value-of",
                new XAttribute("select", $"dbg:break({line!.Value}, .)"));

            var parent = element.Parent;
            var parentIsStylesheet = parent.Name.Namespace == xsltNamespace
                && (parent.Name.LocalName == "stylesheet" || parent.Name.LocalName == "transform");

            if (parentIsStylesheet)
            {
                element.AddFirst(breakCall);
            }
            else
            {
                element.AddBeforeSelf(breakCall);
            }
        }
    }

    private static bool ShouldInstrument(XElement element, XNamespace xsltNamespace)
    {
        if (element.Parent == null) return false;

        if (element.Name.Namespace == xsltNamespace)
        {
            var localName = element.Name.LocalName;
            var shouldInstrument = localName switch
            {
                "stylesheet" or "transform" => false,
                "attribute-set" or "decimal-format" or "import" or "include" or "key" or "namespace-alias" or "output" or "preserve-space" or "strip-space" => false,
                "param" or "variable" or "with-param" => false,
                // XSLT 2.0/3.0 specific top-level elements
                "function" or "accumulator" or "character-map" or "import-schema" => false,
                _ => true
            };

            return shouldInstrument;
        }

        var nearestXsltAncestor = element.Ancestors().FirstOrDefault(a => a.Name.Namespace == xsltNamespace);
        if (nearestXsltAncestor == null) return false;

        var ancestorLocal = nearestXsltAncestor.Name.LocalName;
        if (ancestorLocal is "stylesheet" or "transform") return false;

        // Don't instrument elements inside XSLT 2.0/3.0 function bodies
        if (ancestorLocal is "function") return false;

        return true;
    }

    private static int? GetLineNumber(XElement element)
    {
        if (element is IXmlLineInfo info && info.HasLineInfo())
        {
            return info.LineNumber;
        }
        return null;
    }
}

// Dummy debug extension that just returns empty string
public class DummyDebugExtension : ExtensionFunctionDefinition
{
    public override QName FunctionName => new QName("urn:xslt-debugger", "break");
    public override int MinimumNumberOfArguments => 1;
    public override int MaximumNumberOfArguments => 2;
    public override XdmSequenceType[] ArgumentTypes => new[]
    {
        new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_DOUBLE), ' '),
        new XdmSequenceType(XdmAnyNodeType.Instance, ' ')
    };

    public override XdmSequenceType ResultType(XdmSequenceType[] ArgumentTypes)
    {
        return new XdmSequenceType(XdmAtomicType.BuiltInAtomicType(QName.XS_STRING), ' ');
    }

    public override ExtensionFunctionCall MakeFunctionCall()
    {
        return new DummyDebugExtensionCall();
    }
}

public class DummyDebugExtensionCall : ExtensionFunctionCall
{
    public override System.Collections.Generic.IEnumerator<XdmItem> Call(
        System.Collections.Generic.IEnumerator<XdmItem>[] arguments,
        DynamicContext context)
    {
        // Just print that we were called
        var lineArg = arguments[0];
        if (lineArg.MoveNext() && lineArg.Current is XdmAtomicValue atomicValue)
        {
            var lineNumber = Convert.ToDouble(atomicValue.Value);
            Console.WriteLine($"       [DEBUG] Break at line {lineNumber}");
        }

        return new System.Collections.Generic.List<XdmItem> { new XdmAtomicValue(string.Empty) }.GetEnumerator();
    }
}
