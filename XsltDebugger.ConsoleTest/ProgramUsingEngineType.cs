using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.ConsoleTest;

class ProgramUsingEngineType
{
    private const string TestDataFolder = "TestData";
    private const string IntegrationFolderName = "Integration";
    private static readonly string DefaultTestDataFolder = Path.Combine(TestDataFolder, IntegrationFolderName);

    static async Task Main(string[] args)
    {
        // Parse engine type from command line (--engine compiled or --engine saxon)
        string engineType = "saxon"; // default
        var filteredArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--engine" && i + 1 < args.Length)
            {
                engineType = args[i + 1].ToLower();
                i++; // skip next arg
            }
            else
            {
                filteredArgs.Add(args[i]);
            }
        }

        Console.WriteLine($"=== XSLT Debugger Console Test (Using {(engineType == "compiled" ? "CompiledEngine" : "SaxonEngine")}) ===\n");

        // Use different defaults based on engine type
        var defaultXslt = engineType == "compiled"
            ? Path.Combine(DefaultTestDataFolder, "xslt/v1/sample-inline-cs-with-usings.xslt")
            : Path.Combine(DefaultTestDataFolder, "xslt/v3/ShipmentConf3.xslt");
        var defaultXml = Path.Combine(DefaultTestDataFolder, engineType == "compiled" ? "xml/sample-inline-cs-with-usings.xml" : "xml/ShipmentConf-proper.xml");

        var stylesheetPath = ResolveInput(filteredArgs.ToArray(), 0, defaultXslt);
        var xmlPath = ResolveInput(filteredArgs.ToArray(), 1, defaultXml);

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
            // Create engine based on type
            IXsltEngine engine;
            if (engineType == "compiled")
            {
                Console.WriteLine("1. Initializing XsltCompiledEngine from DebugAdapter...");
                engine = new XsltCompiledEngine();
            }
            else
            {
                Console.WriteLine("1. Initializing SaxonEngine from DebugAdapter...");
                engine = new SaxonEngine();
            }

            // Enable debugging with log level
            XsltEngineManager.SetDebugFlags(debug: true, LogLevel.TraceAll);
            Console.WriteLine("   >> Debugging ENABLED (TraceAll)\n");

            // Set breakpoint - detect which file and set appropriate line
            var fullStylesheetPath = Path.GetFullPath(stylesheetPath);
            int breakpointLine = stylesheetPath.Contains("message-test") ? 9 :
                                 stylesheetPath.Contains("ShipmentConf3") ? 55 :
                                 stylesheetPath.Contains("VariableLoggingSampleV1") ? 8 :
                                 stylesheetPath.Contains("step-into-test") ? 12 : 26;
            engine.SetBreakpoints(new[] { (fullStylesheetPath, breakpointLine) });
            Console.WriteLine($"   >> Breakpoint set at line {breakpointLine}\n");

            // Set up event handlers to capture output
            XsltEngineManager.EngineOutput += (output) =>
            {
                // Highlight important messages with colors
                if (output.Contains("Instrumenting", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($">> {output}");
                    Console.ResetColor();
                }
                else if (output.Contains("[xsl:message]", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($">> {output}");
                    Console.ResetColor();
                }
                else if (output.Contains("Captured variable", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($">> {output}");
                    Console.ResetColor();
                }
                else if (output.StartsWith("[trace]") || output.StartsWith("[traceall]"))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(output);
                    Console.ResetColor();
                }
                else if (output.StartsWith("[debug]"))
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(output);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(output);
                }
            };

            XsltEngineManager.EngineStopped += (file, line, reason) =>
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║ BREAKPOINT HIT - Line {line,-45}║");
                Console.WriteLine($"╚═══════════════════════════════════════════════════════════════╝");
                Console.ResetColor();

                // Display context information
                var context = XsltEngineManager.LastContext;
                if (context != null)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("\n  📍 CONTEXT:");
                    Console.ResetColor();
                    Console.WriteLine($"     Node Type: {context.NodeType}");
                    Console.WriteLine($"     Name: {context.Name}");
                    var value = context.Value?.Length > 100 ? context.Value.Substring(0, 100) + "..." : context.Value ?? "";
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        Console.WriteLine($"     Value: {value.Replace("\n", "\\n").Replace("\r", "")}");
                    }

                    if (context.HasAttributes)
                    {
                        Console.WriteLine("     Attributes:");
                        var attrNav = context.Clone();
                        if (attrNav.MoveToFirstAttribute())
                        {
                            do
                            {
                                Console.WriteLine($"       @{attrNav.Name} = \"{attrNav.Value}\"");
                            } while (attrNav.MoveToNextAttribute());
                        }
                    }
                }

                // Display registered stylesheet namespaces
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n  🌐 REGISTERED NAMESPACES:");
                Console.ResetColor();
                if (XsltEngineManager.StylesheetNamespaces.Count > 0)
                {
                    foreach (var kvp in XsltEngineManager.StylesheetNamespaces)
                    {
                        var prefix = string.IsNullOrEmpty(kvp.Key) ? "(no prefix)" : kvp.Key;
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"     {prefix}");
                        Console.ResetColor();
                        Console.Write(" → ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(kvp.Value);
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("     (no custom namespaces)");
                    Console.ResetColor();
                }

                // Display XSLT variables
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n  📊 XSLT VARIABLES:");
                Console.ResetColor();
                if (XsltEngineManager.Variables.Count > 0)
                {
                    foreach (var kvp in XsltEngineManager.Variables)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"     ${kvp.Key}");
                        Console.ResetColor();
                        Console.WriteLine($" = {kvp.Value}");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("     (no variables captured yet)");
                    Console.ResetColor();
                }
                Console.WriteLine();

                // Auto-continue for console test
                Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await engine.ContinueAsync();
                });
            };

            XsltEngineManager.EngineTerminated += (exitCode) =>
            {
                Console.WriteLine();
                Console.ForegroundColor = exitCode == 0 ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║ TRANSFORMATION {(exitCode == 0 ? "COMPLETED" : "FAILED"),-47}║");
                Console.WriteLine($"║ Exit Code: {exitCode,-51}║");
                Console.WriteLine($"╚═══════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
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

    private static string ResolveInput(string[] args, int index, string fallback)
    {
        var candidate = index < args.Length ? args[index] : null;
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var resolved = TryResolve(candidate!);
            if (resolved != null)
            {
                return resolved;
            }
            return candidate!;
        }

        var fallbackResolved = TryResolve(fallback);
        return fallbackResolved ?? fallback;
    }

    private static string? TryResolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return path;
        }

        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        for (var i = 0; i < 8 && dir != null; i++)
        {
            var direct = Path.Combine(dir.FullName, path);
            if (File.Exists(direct))
            {
                return direct;
            }

            if (!StartsWithSegment(path, TestDataFolder))
            {
                var testDataPath = Path.Combine(dir.FullName, TestDataFolder, path);
                if (File.Exists(testDataPath))
                {
                    return testDataPath;
                }
            }

            if (!StartsWithSegment(path, IntegrationFolderName) &&
                !path.Contains($"{TestDataFolder}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{TestDataFolder}/", StringComparison.OrdinalIgnoreCase))
            {
                var integrationPath = Path.Combine(dir.FullName, TestDataFolder, IntegrationFolderName, path);
                if (File.Exists(integrationPath))
                {
                    return integrationPath;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool StartsWithSegment(string path, string segment)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path.StartsWith(segment + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Path.DirectorySeparatorChar != '/' &&
            path.StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Equals(segment, StringComparison.OrdinalIgnoreCase);
    }
}
