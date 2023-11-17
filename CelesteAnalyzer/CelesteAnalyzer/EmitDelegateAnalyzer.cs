using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CelesteAnalyzer;

/// <summary>
/// Analyzer that checks for improper usages of ILCursor.EmitDelegate
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EmitDelegateAnalyzer : DiagnosticAnalyzer
{
    internal const string DontUseLambdasDiagnosticId = "CL0001";
    internal const string DontEmitInstanceMethodsDiagnosticId = "CL0002";
    
    // The category of the diagnostic (Design, Naming etc.).
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor DontUseLambdasRule = new(
        DontUseLambdasDiagnosticId, 
        title: new LocalizableResourceString(nameof(Resources.CL0001Title),
            Resources.ResourceManager, typeof(Resources)), 
        messageFormat: new LocalizableResourceString(nameof(Resources.CL0001MessageFormat), Resources.ResourceManager,typeof(Resources)), 
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
        description: new LocalizableResourceString(nameof(Resources.CL0001Description), Resources.ResourceManager,typeof(Resources)));
    
    private static readonly DiagnosticDescriptor DontEmitInstanceMethodsRule = new(
        DontEmitInstanceMethodsDiagnosticId,
        title: new LocalizableResourceString(nameof(Resources.CL0002Title),
            Resources.ResourceManager, typeof(Resources)), 
        messageFormat: new LocalizableResourceString(nameof(Resources.CL0002MessageFormat), Resources.ResourceManager,typeof(Resources)), 
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, 
        description: new LocalizableResourceString(nameof(Resources.CL0002Description), Resources.ResourceManager,typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DontUseLambdasRule, DontEmitInstanceMethodsRule);

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

        // check if we're calling ILCursor.EmitDelegate
        if (methodSymbol.MethodKind != MethodKind.Ordinary ||
            methodSymbol.ReceiverType?.Name != "ILCursor" ||
            methodSymbol.Name != "EmitDelegate"
           )
            return;

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