using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using System.Xml.XPath;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace XsltDebugger.DebugAdapter;

public class RoslynEvaluator
{
    public async Task<object?> EvalAsync(string code, object? globals)
    {
        var opts = ScriptOptions.Default
            .WithImports("System", "System.Xml", "System.Linq")
            .WithReferences(typeof(System.Xml.XmlDocument).Assembly);
        return await CSharpScript.EvaluateAsync(code, opts, globals);
    }

    public object? Eval(string code, object? globals)
    {
        return EvalAsync(code, globals).GetAwaiter().GetResult();
    }

    public object? Eval(string code)
    {
        return EvalAsync(code, null).GetAwaiter().GetResult();
    }
}

public interface IXsltEngine
{
    Task StartAsync(string stylesheet, string xml, bool stopOnEntry);
    Task ContinueAsync();
    Task StepOverAsync();
    Task StepInAsync();
    Task StepOutAsync();
    void SetBreakpoints(IEnumerable<(string file, int line)> breakpoints);
}

public class XsltDebugExtension
{
    private readonly XsltCompiledEngine _engine;
    private readonly string _stylesheetPath;

    public XsltDebugExtension(XsltCompiledEngine engine, string stylesheetPath)
    {
        _engine = engine;
        _stylesheetPath = stylesheetPath;
    }

    public string Break(double lineNumber) => BreakInternal(lineNumber, null);

    public string Break(double lineNumber, XPathNodeIterator? context) => BreakInternal(lineNumber, context);

    // XSLT function names are case-sensitive, and the runtime requests the lower-case
    // variant. Expose an alias that simply forwards to the primary Break method.
    public string @break(double lineNumber) => Break(lineNumber);

    public string @break(double lineNumber, XPathNodeIterator? context) => BreakInternal(lineNumber, context);

    private string BreakInternal(double lineNumber, XPathNodeIterator? context)
    {
        var line = (int)Math.Round(lineNumber);
        var navigator = ExtractNavigator(context);
        _engine.RegisterBreakpointHit(_stylesheetPath, line, navigator);
        return string.Empty;
    }

    private static XPathNavigator? ExtractNavigator(XPathNodeIterator? context)
    {
        if (context == null)
        {
            return null;
        }

        try
        {
            var clone = context.Clone();
            if (clone.MoveNext())
            {
                return clone.Current?.Clone();
            }
        }
        catch
        {
            // Ignore extraction failures and fall back to null context.
        }
        return null;
    }
}

public class XsltCompiledEngine : IXsltEngine
{
    private const string DebugNamespace = "urn:xslt-debugger";

    private readonly object _sync = new();
    private static readonly HashSet<string> InlineInstrumentationTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "if",
        "for-each",
        "when",
        "otherwise",
        "element",
        "attribute",
        "comment",
        "processing-instruction"
    };

    private static readonly HashSet<string> ElementsDisallowingChildInstrumentation = new(StringComparer.OrdinalIgnoreCase)
    {
        "apply-templates",
        "call-template",
        "copy",
        "copy-of",
        "value-of",
        "number",
        "sort",
        "message",
        "text"
    };
    private List<(string file, int line)> _breakpoints = new();
    private TaskCompletionSource<bool>? _pauseTcs;
    private string _currentStylesheet = string.Empty;
    private bool _nextStepRequested;

    public Task StartAsync(string stylesheet, string xml, bool stopOnEntry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(stylesheet) || string.IsNullOrWhiteSpace(xml))
            {
                XsltEngineManager.NotifyOutput("Launch request missing stylesheet or xml path.");
                XsltEngineManager.NotifyTerminated(1);
                return Task.CompletedTask;
            }

            _currentStylesheet = NormalizePath(stylesheet);
            var inputPath = NormalizePath(xml);
            var xslt = new XslCompiledTransform();

            XDocument xdoc;
            try
            {
                xdoc = XDocument.Load(stylesheet, LoadOptions.SetLineInfo);
            }
            catch (Exception ex)
            {
                XsltEngineManager.NotifyOutput($"Failed to load XSLT stylesheet: {ex.Message}");
                XsltEngineManager.NotifyTerminated(1);
                return Task.CompletedTask;
            }

            if (xdoc.Root == null || !IsXsltStylesheet(xdoc.Root))
            {
                XsltEngineManager.NotifyOutput("The specified file is not a valid XSLT stylesheet.");
                XsltEngineManager.NotifyTerminated(1);
                return Task.CompletedTask;
            }
            XNamespace msxsl = "urn:schemas-microsoft-com:xslt";
            var scripts = xdoc.Descendants(msxsl + "script").ToList();

            var args = new XsltArgumentList();
            var extNamespace = xdoc.Root?.GetNamespaceOfPrefix("ext")?.NamespaceName;
            if (!string.IsNullOrWhiteSpace(extNamespace))
            {
                args.AddExtensionObject(extNamespace, new RoslynEvaluator());
            }
            else
            {
                var evalNs = "urn:my-ext-eval";
                args.AddExtensionObject(evalNs, new RoslynEvaluator());
                xdoc.Root?.SetAttributeValue(XNamespace.Xmlns + "myeval", evalNs);
            }

            foreach (var script in scripts)
            {
                var language = script.Attribute("language")?.Value ?? "C#";
                var implements = script.Attribute("implements-prefix")?.Value;
                var code = script.Value;
                if (language.IndexOf("c#", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !string.IsNullOrWhiteSpace(code) &&
                    !string.IsNullOrWhiteSpace(implements))
                {
                    var extensionObj = CompileAndCreateExtensionObject(code);
                    var ns = xdoc.Root?.GetNamespaceOfPrefix(implements)?.NamespaceName;
                    var namespaceUri = !string.IsNullOrWhiteSpace(ns) ? ns : $"urn:my-ext-{implements}";
                    args.AddExtensionObject(namespaceUri, extensionObj);
                }
            }

            if (scripts.Count > 0)
            {
                xdoc.Descendants(msxsl + "script").Remove();
            }

            EnsureDebugNamespace(xdoc);
            InstrumentStylesheet(xdoc);

            var settings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
            args.AddExtensionObject(DebugNamespace, new XsltDebugExtension(this, _currentStylesheet));

            using (var reader = xdoc.CreateReader())
            {
                xslt.Load(reader, settings, new XmlUrlResolver());
            }

            if (stopOnEntry)
            {
                PauseForBreakpoint(_currentStylesheet, 0, DebugStopReason.Entry, null);
            }

            using var xmlReader = XmlReader.Create(inputPath);
            var outPath = Path.ChangeExtension(_currentStylesheet, ".out.xml");
            XsltEngineManager.NotifyOutput($"Writing transform output to: {outPath}");
            if (string.IsNullOrWhiteSpace(outPath))
            {
                XsltEngineManager.NotifyOutput("Output path for transform is empty; skipping write.");
            }
            else
            {
                using var fs = File.Create(outPath);
                using var writer = XmlWriter.Create(fs, xslt.OutputSettings ?? new XmlWriterSettings { Indent = true });
                xslt.Transform(xmlReader, args, writer);
            }
            XsltEngineManager.NotifyTerminated(0);
        }
        catch (Exception ex)
        {
            XsltEngineManager.NotifyOutput($"Transform failed: {ex}");
            XsltEngineManager.NotifyTerminated(1);
        }
        finally
        {
            lock (_sync)
            {
                _pauseTcs?.TrySetResult(true);
                _pauseTcs = null;
            }
        }
        return Task.CompletedTask;
    }

    public Task ContinueAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = false;
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public Task StepOverAsync() => ResumeWithStepAsync();

    public Task StepInAsync() => ResumeWithStepAsync();

    public Task StepOutAsync() => ContinueAsync();

    private Task ResumeWithStepAsync()
    {
        lock (_sync)
        {
            _nextStepRequested = true;
            _pauseTcs?.TrySetResult(true);
            _pauseTcs = null;
        }
        return Task.CompletedTask;
    }

    public void SetBreakpoints(IEnumerable<(string file, int line)> bps)
    {
        var normalized = new List<(string file, int line)>();
        foreach (var bp in bps)
        {
            var f = NormalizePath(bp.file);
            normalized.Add((f, bp.line));
        }
        _breakpoints = normalized;
    }

    internal void RegisterBreakpointHit(string file, int line, XPathNavigator? contextNode = null)
    {
        if (line < 0)
        {
            return;
        }

        var normalized = NormalizePath(file);
        var stepRequested = ConsumeStepRequest();

        if (IsBreakpointHit(normalized, line))
        {
            PauseForBreakpoint(normalized, line, DebugStopReason.Breakpoint, contextNode);
            return;
        }

        if (stepRequested)
        {
            PauseForBreakpoint(normalized, line, DebugStopReason.Step, contextNode);
        }
    }

    private bool IsBreakpointHit(string file, int line)
    {
        foreach (var bp in _breakpoints)
        {
            if (string.Equals(bp.file, file, StringComparison.OrdinalIgnoreCase) && bp.line == line)
            {
                return true;
            }
        }
        return false;
    }

    private void PauseForBreakpoint(string file, int line, DebugStopReason reason, XPathNavigator? context)
    {
        TaskCompletionSource<bool>? localTcs;
        lock (_sync)
        {
            _pauseTcs = new TaskCompletionSource<bool>();
            localTcs = _pauseTcs;
        }

        XsltEngineManager.NotifyStopped(file, line, reason, context);

        try
        {
            localTcs?.Task.Wait();
        }
        catch
        {
            // Ignore interruption
        }
    }

    private bool ConsumeStepRequest()
    {
        lock (_sync)
        {
            if (_nextStepRequested)
            {
                _nextStepRequested = false;
                return true;
            }
        }
        return false;
    }

    private static string NormalizePath(string path)
    {
        var result = path ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(result))
        {
            if (result.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try { result = new Uri(result).LocalPath; } catch { }
            }
            try { result = Path.GetFullPath(result); } catch { }
        }
        return result;
    }

    private void EnsureDebugNamespace(XDocument doc)
    {
        if (doc.Root == null)
        {
            return;
        }

        var existing = doc.Root.GetNamespaceOfPrefix("dbg");
        if (existing == null || existing.NamespaceName != DebugNamespace)
        {
            doc.Root.SetAttributeValue(XNamespace.Xmlns + "dbg", DebugNamespace);
        }
    }

    private void InstrumentStylesheet(XDocument doc)
    {
        if (doc.Root == null)
        {
            return;
        }

        var xsltNamespace = doc.Root.Name.Namespace;
        var candidates = doc
            .Descendants()
            .Where(e => ShouldInstrument(e, xsltNamespace))
            .Select(e => (Element: e, Line: GetLineNumber(e)))
            .Where(tuple => tuple.Line.HasValue)
            .ToList();

        foreach (var (element, line) in candidates)
        {
            if (element.Parent == null)
            {
                continue;
            }

            var isXsltElement = element.Name.Namespace == xsltNamespace;
            var breakCall = new XElement(
                xsltNamespace + "value-of",
                new XAttribute("select", $"dbg:break({line!.Value}, .)"));

            if (CanInsertAsFirstChild(element, isXsltElement))
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
        if (element.Parent == null)
        {
            return false;
        }

        if (element.Name.Namespace == xsltNamespace)
        {
            var localName = element.Name.LocalName;
            return localName switch
            {
                "stylesheet" or "transform" => false,
                "attribute-set" or "decimal-format" or "import" or "include" or "key" or "namespace-alias" or "output" or "preserve-space" or "strip-space" => false,
                "param" or "variable" or "with-param" => false,
                _ => true
            };
        }

        var nearestXsltAncestor = element.Ancestors().FirstOrDefault(a => a.Name.Namespace == xsltNamespace);
        if (nearestXsltAncestor == null)
        {
            return false;
        }

        var ancestorLocal = nearestXsltAncestor.Name.LocalName;
        if (ancestorLocal is "stylesheet" or "transform")
        {
            return false;
        }

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

    private static bool CanInsertAsFirstChild(XElement element, bool isXsltElement)
    {
        if (element == null)
        {
            return false;
        }

        if (!isXsltElement)
        {
            return false;
        }

        var parent = element.Parent;
        var localName = element.Name.LocalName;

        if (InlineInstrumentationTargets.Contains(localName))
        {
            return true;
        }

        if (parent != null)
        {
            var parentLocal = parent.Name.LocalName;
            if (string.Equals(parentLocal, "stylesheet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parentLocal, "transform", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parentLocal, "choose", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (element.IsEmpty)
        {
            return false;
        }

        if (ElementsDisallowingChildInstrumentation.Contains(localName))
        {
            return false;
        }

        return true;
    }

    private object CompileAndCreateExtensionObject(string code)
    {
        var classCode = code.Contains("class", StringComparison.OrdinalIgnoreCase)
            ? code
            : $"public class InlineXsltExt {{ {code} }}";

        // Build prelude with only the using statements not already present in the code
        var requiredUsings = new[]
        {
            "using System;",
            "using System.Collections;",
            "using System.Collections.Generic;",
            "using System.Globalization;",
            "using System.Linq;",
            "using System.Text;",
            "using System.Xml;",
            "using System.Xml.XPath;",
            "using System.Xml.Xsl;"
        };

        var existingUsings = ExtractUsingStatements(classCode);
        var prelude = string.Join(Environment.NewLine,
            requiredUsings.Where(u => !existingUsings.Contains(u, StringComparer.OrdinalIgnoreCase)));

        var sourceCode = string.Concat(prelude, Environment.NewLine, classCode);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(XsltArgumentList).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Xml.XmlDocument).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
        };
        var compilation = CSharpCompilation.Create(
            "InlineXsltExtAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            throw new Exception("Failed to compile inline C# script: " + string.Join("\n", result.Diagnostics));
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        Type? chosen = null;
        foreach (var t in assembly.GetTypes())
        {
            var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (methods.Length > 0)
            {
                chosen = t;
                break;
            }
        }

        chosen ??= assembly.GetTypes().FirstOrDefault(t => !t.IsNested);
        if (chosen == null)
        {
            throw new Exception("Compiled assembly does not contain a usable type for XSLT extension.");
        }

        return Activator.CreateInstance(chosen) ?? throw new Exception("Failed to instantiate compiled extension type.");
    }

    private static HashSet<string> ExtractUsingStatements(string code)
    {
        var usings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = code.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using ", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(";"))
            {
                usings.Add(trimmed);
            }
        }

        return usings;
    }

    private static bool IsXsltStylesheet(XElement root)
    {
        var ns = root.Name.NamespaceName;
        return ns == "http://www.w3.org/1999/XSL/Transform" && (root.Name.LocalName == "stylesheet" || root.Name.LocalName == "transform");
    }
}
