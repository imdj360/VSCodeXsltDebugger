using System.Threading.Tasks;
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
