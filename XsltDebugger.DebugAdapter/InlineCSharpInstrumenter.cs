using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XsltDebugger.DebugAdapter;

/// <summary>
/// Instruments inline C# methods to automatically log entry and return values
/// using InlineXsltLogger.
/// </summary>
public class InlineCSharpInstrumenter : CSharpSyntaxRewriter
{
    private int _instrumentedMethodCount = 0;

    public int InstrumentedMethodCount => _instrumentedMethodCount;

    public static string Instrument(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var instrumenter = new InlineCSharpInstrumenter();
        var instrumentedRoot = instrumenter.Visit(root);

        if (XsltEngineManager.IsLogEnabled && instrumenter.InstrumentedMethodCount > 0)
        {
            XsltEngineManager.NotifyOutput($"[debug] Instrumented {instrumenter.InstrumentedMethodCount} inline C# method(s)");
        }

        return instrumentedRoot.ToFullString();
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Skip if not public or if void return type
        if (!node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)) ||
            node.ReturnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return base.VisitMethodDeclaration(node);
        }

        // Skip if method body is null (abstract/interface methods)
        if (node.Body == null)
        {
            return base.VisitMethodDeclaration(node);
        }

        // Check if already instrumented (contains LogEntry or LogReturn calls)
        if (AlreadyInstrumented(node))
        {
            return base.VisitMethodDeclaration(node);
        }

        _instrumentedMethodCount++;

        // Create LogEntry statement with parameters
        var logEntryStatement = CreateLogEntryStatement(node);

        // Instrument all return statements with LogReturn
        var instrumentedBody = (BlockSyntax)new ReturnStatementInstrumenter().Visit(node.Body);

        // Add LogEntry as first statement in method body
        var newStatements = instrumentedBody.Statements.Insert(0, logEntryStatement);
        var newBody = instrumentedBody.WithStatements(newStatements);

        return node.WithBody(newBody);
    }

    private bool AlreadyInstrumented(MethodDeclarationSyntax node)
    {
        if (node.Body == null) return false;

        // Check if method contains LogEntry or LogReturn calls
        var hasLogCalls = node.Body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
            {
                var identifierName = invocation.Expression as IdentifierNameSyntax;
                return identifierName?.Identifier.Text is "LogEntry" or "LogReturn";
            });

        return hasLogCalls;
    }

    private StatementSyntax CreateLogEntryStatement(MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters;

        if (parameters.Count == 0)
        {
            // LogEntry();
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("LogEntry")));
        }
        else
        {
            // LogEntry(new { param1, param2, ... });
            var anonymousObjectProperties = parameters.Select(p =>
                SyntaxFactory.AnonymousObjectMemberDeclarator(
                    SyntaxFactory.IdentifierName(p.Identifier.Text)));

            var anonymousObject = SyntaxFactory.AnonymousObjectCreationExpression(
                SyntaxFactory.SeparatedList(anonymousObjectProperties));

            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.IdentifierName("LogEntry"),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(anonymousObject)))));
        }
    }

    /// <summary>
    /// Rewrites return statements to wrap the return value with LogReturn()
    /// </summary>
    private class ReturnStatementInstrumenter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression == null)
            {
                return base.VisitReturnStatement(node);
            }

            // Wrap: return expr; => return LogReturn(expr);
            var logReturnInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName("LogReturn"),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(node.Expression))));

            return node.WithExpression(logReturnInvocation);
        }
    }
}
