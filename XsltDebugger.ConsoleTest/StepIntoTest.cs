using System;
using System.IO;
using System.Threading.Tasks;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.ConsoleTest;

/// <summary>
/// Interactive console test for step-into functionality
/// </summary>
class StepIntoTest
{
    private static IXsltEngine? _engine;
    private static int _callDepth = 0;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== XSLT Step-Into Test ===\n");

        // Use the step-into test files
        var stylesheetPath = Path.GetFullPath("TestData/Integration/step-into-test.xslt");
        var xmlPath = Path.GetFullPath("TestData/Integration/step-into-test.xml");

        if (!File.Exists(stylesheetPath))
        {
            Console.WriteLine($"ERROR: Stylesheet not found: {stylesheetPath}");
            Console.WriteLine("Make sure you run this from the project root directory.");
            return;
        }

        if (!File.Exists(xmlPath))
        {
            Console.WriteLine($"ERROR: XML not found: {xmlPath}");
            return;
        }

        // Choose engine
        Console.WriteLine("Select engine:");
        Console.WriteLine("  1) Compiled (XSLT 1.0)");
        Console.WriteLine("  2) Saxon (XSLT 2.0/3.0)");
        Console.Write("\nChoice [1]: ");
        var choice = Console.ReadLine();
        var useCompiled = string.IsNullOrWhiteSpace(choice) || choice == "1";

        try
        {
            // Create engine
            if (useCompiled)
            {
                Console.WriteLine("\n✓ Using XsltCompiledEngine");
                _engine = new XsltCompiledEngine();
            }
            else
            {
                Console.WriteLine("\n✓ Using SaxonEngine");
                _engine = new SaxonEngine();
            }

            // Enable debugging
            XsltEngineManager.SetDebugFlags(debug: true, LogLevel.Trace);

            // Set up event handlers
            XsltEngineManager.EngineOutput += (output) =>
            {
                if (output.Contains("[template-entry]"))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  {output}");
                    Console.ResetColor();
                }
                else if (output.StartsWith("[trace]"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  {output}");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"  {output}");
                }
            };

            var stepCount = 0;
            XsltEngineManager.EngineStopped += async (file, line, reason) =>
            {
                stepCount++;
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║ STOPPED - Step #{stepCount,-44} ║");
                Console.WriteLine($"║ Line: {line,-52} ║");
                Console.WriteLine($"║ Reason: {reason,-50} ║");
                Console.WriteLine($"╚════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                // Show context
                var context = XsltEngineManager.LastContext;
                if (context != null)
                {
                    Console.WriteLine($"\n  📍 Context: <{context.Name}>");
                }

                // Show variables
                if (XsltEngineManager.Variables.Count > 0)
                {
                    Console.WriteLine($"\n  📊 Variables:");
                    foreach (var kvp in XsltEngineManager.Variables)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"     ${kvp.Key}");
                        Console.ResetColor();
                        Console.WriteLine($" = {kvp.Value}");
                    }
                }

                // Interactive prompt
                Console.WriteLine("\n  Commands:");
                Console.WriteLine("    [Enter] - Continue (F5)");
                Console.WriteLine("    i       - Step Into (F11)");
                Console.WriteLine("    o       - Step Over (F10)");
                Console.WriteLine("    u       - Step Out (Shift+F11)");
                Console.Write("\n  > ");

                var cmd = Console.ReadLine()?.ToLower().Trim();

                if (cmd == "i")
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("  → Step Into (F11)");
                    Console.ResetColor();
                    await _engine!.StepInAsync();
                }
                else if (cmd == "o")
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("  → Step Over (F10)");
                    Console.ResetColor();
                    await _engine!.StepOverAsync();
                }
                else if (cmd == "u")
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("  → Step Out (Shift+F11)");
                    Console.ResetColor();
                    await _engine!.StepOutAsync();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  → Continue (F5)");
                    Console.ResetColor();
                    await _engine!.ContinueAsync();
                }
            };

            XsltEngineManager.EngineTerminated += (exitCode) =>
            {
                Console.WriteLine();
                Console.ForegroundColor = exitCode == 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║ TRANSFORMATION {(exitCode == 0 ? "COMPLETED" : "FAILED"),-41} ║");
                Console.WriteLine($"║ Total Steps: {stepCount,-45} ║");
                Console.WriteLine($"╚════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
            };

            Console.WriteLine($"\n📄 Stylesheet: step-into-test.xslt");
            Console.WriteLine($"📄 XML: step-into-test.xml");
            Console.WriteLine($"\nStarting with stopOnEntry=true...\n");

            // Run the transformation
            await _engine.StartAsync(
                stylesheetPath,
                xmlPath,
                stopOnEntry: true
            );

            // Wait for completion
            await Task.Delay(2000);

            Console.WriteLine("\n=== TEST COMPLETE ===\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n=== ERROR ===");
            Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Console.ResetColor();
        }
    }
}
