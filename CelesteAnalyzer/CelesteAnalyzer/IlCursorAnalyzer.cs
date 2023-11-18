using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CelesteAnalyzer;

/// <summary>
/// Analyzer that checks for improper usages of ILCursor
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class IlCursorAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor DontUseLambdasRule
        = Utils.CreateDiagnostic(DiagnosticIds.DontUseLambdas);
    
    private static readonly DiagnosticDescriptor DontEmitInstanceMethodsRule
        = Utils.CreateDiagnostic(DiagnosticIds.DontEmitInstanceMethods);
    
    private static readonly DiagnosticDescriptor DontUseCursorRemoveRule
        = Utils.CreateDiagnostic(DiagnosticIds.DontUseCursorRemove);
    
    private static readonly DiagnosticDescriptor DontChainPredicatesInCursorGotoRule
        = Utils.CreateDiagnostic(DiagnosticIds.DontChainPredicatesInCursorGoto);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DontUseLambdasRule, DontEmitInstanceMethodsRule, DontUseCursorRemoveRule, DontChainPredicatesInCursorGotoRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
    }

    /// <summary>
    /// Executed on the completion of the semantic analysis associated with the Invocation operation.
    /// </summary>
    /// <param name="context">Operation context.</param>
    private void AnalyzeOperation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocationOperation ||
            context.Operation.Syntax is not InvocationExpressionSyntax invocationSyntax)
            return;

        var methodSymbol = invocationOperation.TargetMethod;

        // check if we're calling an ILCursor method
        if (methodSymbol.MethodKind != MethodKind.Ordinary || methodSymbol.ReceiverType?.Name != "ILCursor")
            return;

        switch (methodSymbol.Name)
        {
            case "EmitDelegate":
                HandleEmitDelegate(context, invocationSyntax);
                break;
            case "Remove":
            case "RemoveRange":
                HandleRemove(context, invocationSyntax);
                break;
            case "TryGotoNext":
            case "GotoNext":
            case "TryGotoPrev":
            case "GotoPrev":
                HandleTryGotoNext(context, invocationSyntax);
                break;
        }
    }

    private static void HandleTryGotoNext(OperationAnalysisContext context, InvocationExpressionSyntax invocationSyntax)
    {
        if (invocationSyntax.ArgumentList.Arguments.Count < 5)
            return;
        
        var diagnostic = Diagnostic.Create(DontChainPredicatesInCursorGotoRule, invocationSyntax.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
    
    private static void HandleRemove(OperationAnalysisContext context, InvocationExpressionSyntax invocationSyntax)
    {
        var diagnostic = Diagnostic.Create(DontUseCursorRemoveRule, invocationSyntax.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private static void HandleEmitDelegate(OperationAnalysisContext context, InvocationExpressionSyntax invocationSyntax)
    {
        // Sanity check to make sure there's only 1 argument
        if (invocationSyntax.ArgumentList.Arguments.Count != 1)
            return;

        // Traverse through the syntax tree, starting with the particular 'InvocationSyntax' to the desired node.
        var argumentSyntax = invocationSyntax.ArgumentList.Arguments.Single().Expression;

        if (argumentSyntax is LambdaExpressionSyntax)
        {
            var diagnostic = Diagnostic.Create(DontUseLambdasRule, argumentSyntax.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        if (argumentSyntax is IdentifierNameSyntax id)
        {
            if (context.Operation.SemanticModel?.GetOperation(id) is IMethodReferenceOperation { Method.IsStatic: false })
            {
                var diagnostic = Diagnostic.Create(DontEmitInstanceMethodsRule, argumentSyntax.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}