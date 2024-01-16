using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CelesteAnalyzer;

/// <summary>
/// Analyzer that checks for dangerous hooks
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HookAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CallOrigInHooksRule
        = Utils.CreateDiagnostic(DiagnosticIds.CallOrigInHooks);
    
    private static readonly DiagnosticDescriptor HooksShouldBeStaticRule
        = Utils.CreateDiagnostic(DiagnosticIds.HooksShouldBeStatic);
    
    private static readonly DiagnosticDescriptor DontYieldReturnOrigRule
        = Utils.CreateDiagnostic(DiagnosticIds.DontYieldReturnOrig);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(CallOrigInHooksRule, HooksShouldBeStaticRule, DontYieldReturnOrigRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeObjectCreationOperation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeEventAssignment, OperationKind.EventAssignment);
    }
    
    private void AnalyzeEventAssignment(OperationAnalysisContext context)
    {
        // look for On/IL hook assignment to MonoMod.RuntimeDetour.HookGen

        if (context.Operation.SemanticModel is not { } sem)
            return;
        if (context.Operation is not IEventAssignmentOperation op)
            return;
        if (op.Syntax is not AssignmentExpressionSyntax assignment)
            return;
        if (sem.GetOperation(assignment.Left) is not IEventReferenceOperation refOp)
            return;
        if (Utils.BottommostNamespace(refOp.Event)?.Name is "On" or "IL")
            AnalyzeHookFromLambdaOrIdentifier(context, assignment.Right, sem);
    }
    
    private void AnalyzeObjectCreationOperation(OperationAnalysisContext context)
    {
        // look for Hook constructors
        if (context.Operation is not IObjectCreationOperation op)
            return;
        if (op.Syntax is not ObjectCreationExpressionSyntax creationSyntax)
            return;
        var sem = context.Operation.SemanticModel;
        if (sem is null)
            return;

        var createdType = op.Type;
        
        if (createdType is not { Name: "Hook", ContainingNamespace: { Name: "RuntimeDetour", ContainingNamespace.Name: "MonoMod" } })
            return;
        
        if (creationSyntax.ArgumentList is not { Arguments.Count: 2 })
            return;
        
        var targetArg = creationSyntax.ArgumentList!.Arguments[1];
        
        AnalyzeHookFromLambdaOrIdentifier(context, targetArg.Expression, sem);
    }

    private static void AnalyzeHookFromLambdaOrIdentifier(OperationAnalysisContext context, ExpressionSyntax targetArg, SemanticModel sem)
    {
        // check method references
        if (targetArg is IdentifierNameSyntax id)
        {
            if (Utils.GetMethodDeclarationSyntaxFromIdentifier(id, sem, out var methodRef) is { } syntax)
            {
                AnalyzeHook(context, methodRef!.Method, syntax.Body, syntax.GetLocation());
            }
        }

        // check lambdas
        if (targetArg is LambdaExpressionSyntax lambda)
        {
            if (sem.GetOperation(lambda) is not IAnonymousFunctionOperation methodRef)
                return;

            if (methodRef.Symbol.DeclaringSyntaxReferences
                    .Select(r => r.GetSyntax())
                    .OfType<LambdaExpressionSyntax>()
                    .FirstOrDefault() is { } syntax)
            {
                AnalyzeHook(context, methodRef.Symbol, (SyntaxNode?)syntax.Block ?? syntax.ExpressionBody, syntax.GetLocation());
            }
        }
    }

    private static void AnalyzeHook(OperationAnalysisContext context, IMethodSymbol methodSymbol, SyntaxNode? bodySyntax, Location loc)
    {
        var firstParam = methodSymbol.Parameters.First();
        
        // hooks should be static
        if (!methodSymbol.IsStatic)
        {
            var notStaticDiagnostic = Diagnostic.Create(HooksShouldBeStaticRule, loc);
            context.ReportDiagnostic(notStaticDiagnostic);
        }

        // now time for On.*-hook specific checks
        if (firstParam.Type.Name is "ILContext")
            return;
        
        // hooks should call orig in at least one code path
        if (bodySyntax is not null)
        {
            bool origCalled = false;
            bool isEnumeratorMethod = methodSymbol.ReturnType.Name == nameof(IEnumerator);
            
            foreach (var st in bodySyntax.DescendantNodes())
            {
                if (IsOrig(st, firstParam))
                {
                    origCalled = true;
                    
                    // no point in checking the method any more if this isn't an enumerator
                    if (!isEnumeratorMethod)
                        return;
                }

                if (isEnumeratorMethod && st is YieldStatementSyntax yield)
                {
                    if (yield.ReturnOrBreakKeyword.IsKind(SyntaxKind.ReturnKeyword))
                    {
                        // avoid yield return orig();
                        if (IsOrig(yield.Expression, firstParam))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(DontYieldReturnOrigRule, yield.GetLocation(), firstParam.Type.Name));
                        }
                    }
                }

                static bool IsOrig(SyntaxNode? st, IParameterSymbol orig)
                {
                    return st is InvocationExpressionSyntax invocationExpressionSyntax &&
                           invocationExpressionSyntax.Expression.ToString() == orig.Name;
                }
            }

            if (!origCalled)
            {
                var diagnostic = Diagnostic.Create(CallOrigInHooksRule, loc, firstParam.Type.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}