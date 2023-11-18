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

    private static readonly DiagnosticDescriptor DontUseLambdasRule = new(
        DiagnosticIds.DontUseLambdasDiagnosticId, 
        title: new LocalizableResourceString(nameof(Resources.CL0001Title),
            Resources.ResourceManager, typeof(Resources)), 
        messageFormat: new LocalizableResourceString(nameof(Resources.CL0001MessageFormat), Resources.ResourceManager,typeof(Resources)), 
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
        description: new LocalizableResourceString(nameof(Resources.CL0001Description), Resources.ResourceManager,typeof(Resources)));
    
    private static readonly DiagnosticDescriptor DontEmitInstanceMethodsRule = new(
        DiagnosticIds.DontEmitInstanceMethodsDiagnosticId,
        title: new LocalizableResourceString(nameof(Resources.CL0002Title),
            Resources.ResourceManager, typeof(Resources)), 
        messageFormat: new LocalizableResourceString(nameof(Resources.CL0002MessageFormat), Resources.ResourceManager,typeof(Resources)), 
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
        description: new LocalizableResourceString(nameof(Resources.CL0002Description), Resources.ResourceManager,typeof(Resources)));
    
    private static readonly DiagnosticDescriptor DontUseCursorRemoveRule = new(
        DiagnosticIds.DontUseCursorRemove,
        title: new LocalizableResourceString(nameof(Resources.CL0005Title), Resources.ResourceManager, typeof(Resources)), 
        messageFormat: new LocalizableResourceString(nameof(Resources.CL0005MessageFormat), Resources.ResourceManager,typeof(Resources)), 
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
        description: new LocalizableResourceString(nameof(Resources.CL0005Description), Resources.ResourceManager,typeof(Resources)));
    
    private static readonly DiagnosticDescriptor DontChainPredicatesInCursorGotoRule = new(
        DiagnosticIds.DontChainPredicatesInCursorGoto,
        title: new LocalizableResourceString(nameof(Resources.CL0006Title), Resources.ResourceManager, typeof(Resources)), 
        messageFormat: new LocalizableResourceString(nameof(Resources.CL0006MessageFormat), Resources.ResourceManager,typeof(Resources)), 
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
        description: new LocalizableResourceString(nameof(Resources.CL0006Description), Resources.ResourceManager,typeof(Resources)));

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