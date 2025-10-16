using System;
using System.IO;
using System.Threading.Tasks;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.ConsoleTest;

class ProgramUsingSaxonEngine
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== XSLT Debugger Console Test (Using SaxonEngine) ===\n");

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
            Console.WriteLine("1. Initializing SaxonEngine from DebugAdapter...");
            var engine = new SaxonEngine();

            // Enable debugging with log level
            XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Log);
            Console.WriteLine("   >> Debugging ENABLED (Log)\n");

            // Set breakpoint - detect which file and set appropriate line
            var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
            int breakpointLine = stylesheetPath.Contains("message-test") ? 9 :
                                 stylesheetPath.Contains("ShipmentConf3") ? 55 : 26;
            engine.SetBreakpoints(new[] { (fullStylesheetPath, breakpointLine) });
            Console.WriteLine($"   >> Breakpoint set at line {breakpointLine}\n");

            // Set up event handlers to capture output
            XsltEngineManager.EngineOutput += (output) =>
            {
                Console.WriteLine(output);
            };

            XsltEngineManager.EngineStopped += (file, line, reason) =>
            {
                Console.WriteLine($"[STOPPED] {reason} at {file}:{line}");

                // Display context information
                var context = XsltEngineManager.LastContext;
                if (context != null)
                {
                    Console.WriteLine("\n  === CONTEXT VARIABLES ===");
                    Console.WriteLine($"  name: {context.Name}");
                    Console.WriteLine($"  localName: {context.LocalName}");
                    Console.WriteLine($"  nodeType: {context.NodeType}");
                    Console.WriteLine($"  value: {(context.Value?.Length > 100 ? context.Value.Substring(0, 100) + "..." : context.Value ?? "")}");

                    if (context.HasAttributes)
                    {
                        Console.WriteLine("  attributes:");
                        var attrNav = context.Clone();
                        if (attrNav.MoveToFirstAttribute())
                        {
                            do
                            {
                                Console.WriteLine($"    @{attrNav.Name} = \"{attrNav.Value}\"");
                            } while (attrNav.MoveToNextAttribute());
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\n  === CONTEXT VARIABLES ===");
                    Console.WriteLine("  (no context available)");
                }

                // Display XSLT variables
                Console.WriteLine("\n  === XSLT VARIABLES ===");
                if (XsltEngineManager.Variables.Count > 0)
                {
                    foreach (var kvp in XsltEngineManager.Variables)
                    {
                        Console.WriteLine($"  ${kvp.Key} = {kvp.Value}");
                    }
                }
                else
                {
                    Console.WriteLine("  (no XSLT variables captured)");
                }
                Console.WriteLine();

                // Auto-continue for console test (in real debugger, this would wait for user input)
                Task.Run(async () =>
                {
                    await Task.Delay(10); // Small delay to see the stop
                    await engine.ContinueAsync();
                });
            };

            XsltEngineManager.EngineTerminated += (exitCode) =>
            {
                Console.WriteLine($"\n[TERMINATED] Exit code: {exitCode}");
            };

            Console.WriteLine($"2. Starting transformation...");
            Console.WriteLine($"   Stylesheet: {stylesheetPath}");
            Console.WriteLine($"   XML: {xmlPath}");
            Console.WriteLine($"   Stop on entry: false\n");

            // Run the transformation (this is async and will call our event handlers)
            await engine.StartAsync(
                Path.GetFullPath(stylesheetPath),
                Path.GetFullPath(xmlPath),
                stopOnEntry: false
            );

            // Give it a moment to finish
            await Task.Delay(1000);

            Console.WriteLine("\n=== TEST COMPLETE ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n=== ERROR ===");
            Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }
}
