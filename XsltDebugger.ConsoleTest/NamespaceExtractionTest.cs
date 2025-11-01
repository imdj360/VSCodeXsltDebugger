using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace XsltDebugger.ConsoleTest;

/// <summary>
/// Test namespace extraction from various XSLT files
/// </summary>
class NamespaceExtractionTest
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== XSLT Namespace Extraction Test ===\n");

        var testFiles = new[]
        {
            "TestData/Integration/xslt/saxon/LmlBasedXslt.xslt",
            "TestData/Integration/xslt/saxon/VariableLoggingSample.xslt",
            "TestData/Integration/xslt/compiled/VariableLoggingSampleV1.xslt",
            "TestData/Integration/xslt/saxon/AdvanceXslt2.xslt",
            "TestData/Integration/xslt/saxon/AdvanceXslt3.xslt",
            "TestData/Integration/xslt/saxon/ShipmentConf3.xslt"
        };

        foreach (var testFile in testFiles)
        {
            TestNamespaceExtraction(testFile);
        }

        Console.WriteLine("\n=== ALL TESTS COMPLETE ===\n");
    }

    static void TestNamespaceExtraction(string stylesheetPath)
    {
        var fullPath = Path.GetFullPath(stylesheetPath);
        var fileName = Path.GetFileName(stylesheetPath);

        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Testing: {fileName}");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        if (!File.Exists(fullPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ File not found: {fullPath}");
            Console.ResetColor();
            Console.WriteLine();
            return;
        }

        try
        {
            var xdoc = XDocument.Load(fullPath);

            if (xdoc.Root == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No root element found");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            // Extract namespaces
            var namespaces = ExtractNamespaces(xdoc);

            Console.WriteLine($"Found {namespaces.Count} namespace(s):");
            Console.WriteLine();

            if (namespaces.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  (No custom namespaces - XML uses no namespaces)");
                Console.ResetColor();
            }
            else
            {
                foreach (var ns in namespaces.OrderBy(kvp => kvp.Key))
                {
                    var prefix = string.IsNullOrEmpty(ns.Key) ? "(default)" : ns.Key;
                    var prefixColor = string.IsNullOrEmpty(ns.Key) ? ConsoleColor.Yellow : ConsoleColor.Cyan;

                    Console.ForegroundColor = prefixColor;
                    Console.Write($"  {prefix,-15}");
                    Console.ResetColor();
                    Console.Write(" → ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(ns.Value);
                    Console.ResetColor();
                }
            }

            Console.WriteLine();

            // Show sample XPath expressions
            if (namespaces.Count > 0)
            {
                Console.WriteLine("Sample watch expressions:");
                var sampleNamespaces = namespaces
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Key))
                    .Take(2);

                foreach (var ns in sampleNamespaces)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  /{ns.Key}:Element/SubElement");
                    Console.ResetColor();
                }

                // If there's a default namespace
                if (namespaces.ContainsKey("default"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  /default:Element/SubElement");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✓ Extraction successful");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    static Dictionary<string, string> ExtractNamespaces(XDocument xdoc)
    {
        if (xdoc.Root == null)
        {
            return new Dictionary<string, string>();
        }

        const string DebugNamespace = "urn:xslt-debugger";
        var namespaces = new Dictionary<string, string>();

        // Extract all namespace declarations from the root element
        foreach (var attr in xdoc.Root.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                var prefix = attr.Name.LocalName == "xmlns" ? string.Empty : attr.Name.LocalName;
                var uri = attr.Value;

                // Skip XSLT namespace and debug namespace
                if (uri != "http://www.w3.org/1999/XSL/Transform" &&
                    uri != DebugNamespace)
                {
                    // For default namespace (no prefix), also register with "default" prefix
                    if (string.IsNullOrEmpty(prefix))
                    {
                        namespaces[prefix] = uri;
                        namespaces["default"] = uri;
                    }
                    else
                    {
                        namespaces[prefix] = uri;
                    }
                }
            }
        }

        return namespaces;
    }
}
