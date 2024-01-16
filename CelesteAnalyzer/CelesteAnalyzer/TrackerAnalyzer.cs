using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CelesteAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TrackerAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor DontUseFindAllRule
        = Utils.CreateDiagnostic(DiagnosticIds.DontUseFindAll);
    
    private static readonly DiagnosticDescriptor TrackerUsedOnUntrackedTypeRule
        = Utils.CreateDiagnostic(DiagnosticIds.TrackerUsedOnUntrackedType);
    
    private static readonly DiagnosticDescriptor InvalidTrackedAsRule
        = Utils.CreateDiagnostic(DiagnosticIds.InvalidTrackedAs);
    
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DontUseFindAllRule, TrackerUsedOnUntrackedTypeRule, InvalidTrackedAsRule);
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);
        context.RegisterSymbolAction(AnalyzeClassDecl, SymbolKind.NamedType);
    }

    private void AnalyzeClassDecl(SymbolAnalysisContext ctx)
    {
        if (ctx.Symbol is not INamedTypeSymbol namedTypeSymbol)
            return;
        var attrs = ctx.Symbol.GetAttributes();
        if (attrs.Length == 0)
            return;
        
        var trackedAsAttr = attrs.FirstOrDefault(attr => attr.AttributeClass?.Name is "TrackedAs");
        if (trackedAsAttr is not { ConstructorArguments.Length: 1 })
            return;

        if (trackedAsAttr.ConstructorArguments.First().Value is not ITypeSymbol trackedAsType)
            return;

        if (Utils.Extends(namedTypeSymbol, trackedAsType.Name))
            return;


        var syntax = Utils.GetAttributeSyntaxFromClassDef(trackedAsAttr,
            (ClassDeclarationSyntax)ctx.Symbol.DeclaringSyntaxReferences.First().GetSyntax());
        
        ctx.ReportDiagnostic(Diagnostic.Create(InvalidTrackedAsRule, syntax?.GetLocation(), trackedAsType.Name));
    }
    
    private void AnalyzeInvocationOperation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation invocationOperation ||
            context.Operation.Syntax is not InvocationExpressionSyntax invocationSyntax)
            return;

        var methodSymbol = invocationOperation.TargetMethod;

        if (methodSymbol.MethodKind != MethodKind.Ordinary)
            return;

        switch (methodSymbol.ReceiverType?.Name, methodSymbol.Name)
        {
            case ("EntityList", "FindAll"):
            case ("EntityList", "FindFirst"):
                AnalyzeEntityListFind(context, methodSymbol, invocationSyntax);
                break;
            case ("Tracker", var name):
                AnalyzeTracker(context, methodSymbol, invocationSyntax);
                break;
        }
    }

    private void AnalyzeEntityListFind(OperationAnalysisContext context, IMethodSymbol symbol, InvocationExpressionSyntax syntax)
    {
        if (!symbol.IsGenericMethod)
            return;

        if (symbol.TypeArguments.FirstOrDefault() is not { } usedType)
            return;

        // using a find method on a tracked type, or a type you have control over, is wasteful
        if (Utils.IsTracked(usedType) || Utils.IsSourceChangeable(usedType, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(DontUseFindAllRule, syntax.GetLocation()));
        }
    }

    private void AnalyzeTracker(OperationAnalysisContext context, IMethodSymbol symbol,
        InvocationExpressionSyntax syntax)
    {
        if (symbol.Name is "IsEntityTracked" or "IsComponentTracked")
            return;
        
        if (symbol.TypeArguments.FirstOrDefault() is not { } usedType)
            return;

        if (Utils.IsTracked(usedType))
            return;
        
        context.ReportDiagnostic(Diagnostic.Create(TrackerUsedOnUntrackedTypeRule, syntax.GetLocation(), usedType));
    }
}