using System;
using System.IO;
using System.Threading.Tasks;
using XsltDebugger.DebugAdapter;

namespace XsltDebugger.ConsoleTest;

class ProgramUsingSaxonEngine
{
    private const string ConsoleTestFolder = "XsltDebugger.ConsoleTest";
    private const string SampleFolderName = "sample";
    private static readonly string DefaultSampleFolder = Path.Combine(ConsoleTestFolder, SampleFolderName);

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== XSLT Debugger Console Test (Using SaxonEngine) ===\n");

        var stylesheetPath = ResolveInput(args, 0, Path.Combine(DefaultSampleFolder, "ShipmentConf3.xslt"));
        var xmlPath = ResolveInput(args, 1, Path.Combine(DefaultSampleFolder, "ShipmentConf.xml"));

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

            if (!StartsWithSegment(path, ConsoleTestFolder))
            {
                var consolePath = Path.Combine(dir.FullName, ConsoleTestFolder, path);
                if (File.Exists(consolePath))
                {
                    return consolePath;
                }
            }

            if (!StartsWithSegment(path, SampleFolderName) &&
                !path.Contains($"{ConsoleTestFolder}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{ConsoleTestFolder}/", StringComparison.OrdinalIgnoreCase))
            {
                var samplePath = Path.Combine(dir.FullName, ConsoleTestFolder, SampleFolderName, path);
                if (File.Exists(samplePath))
                {
                    return samplePath;
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
