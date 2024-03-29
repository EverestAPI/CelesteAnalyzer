﻿using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CelesteAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EntityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CustomEntityWithNoValidCtorRule 
        = Utils.CreateDiagnostic(DiagnosticIds.CustomEntityWithNoValidCtor);
    
    private static readonly DiagnosticDescriptor CustomEntityNotExtendingEntityRule 
        = Utils.CreateDiagnostic(DiagnosticIds.CustomEntityNotExtendingEntity);

    private static readonly DiagnosticDescriptor CustomEntityGeneratorMethodMissingRule
        = Utils.CreateDiagnostic(DiagnosticIds.CustomEntityGeneratorMethodMissing);
    
    private static readonly DiagnosticDescriptor CustomEntityGeneratorInvalidParamsRule
        = Utils.CreateDiagnostic(DiagnosticIds.CustomEntityGeneratorInvalidParams);
    
    private static readonly DiagnosticDescriptor CustomEntityGeneratorInvalidRule
        = Utils.CreateDiagnostic(DiagnosticIds.CustomEntityGeneratorInvalid);
    
    private static readonly DiagnosticDescriptor CustomEntityNoIDsRule
        = Utils.CreateDiagnostic(DiagnosticIds.CustomEntityNoIDs);
    
    private static readonly DiagnosticDescriptor UsingSceneInWrongPlaceRule
        = Utils.CreateDiagnostic(DiagnosticIds.UsingSceneInWrongPlace);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        CustomEntityWithNoValidCtorRule, CustomEntityNotExtendingEntityRule, CustomEntityGeneratorMethodMissingRule, 
        CustomEntityGeneratorInvalidParamsRule, CustomEntityGeneratorInvalidRule, CustomEntityNoIDsRule,
        UsingSceneInWrongPlaceRule
        );
    
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeClassDecl, SymbolKind.NamedType);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private void AnalyzePropertyReference(OperationAnalysisContext ctx)
    {
        if (ctx.Operation is not IPropertyReferenceOperation op)
            return;

        if (op.Property.Name != "Scene")
            return;

        if (op.Property.ContainingType.Name != "Entity")
            return;

        var p = op.Syntax;
        while (p is not (BaseMethodDeclarationSyntax or LambdaExpressionSyntax or null))
        {
            p = p?.Parent;
        } 

        if (p is ConstructorDeclarationSyntax)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(UsingSceneInWrongPlaceRule, op.Syntax.GetLocation()));
        }
    }

    private void AnalyzeClassDecl(SymbolAnalysisContext ctx)
    {
        if (ctx.Symbol is not INamedTypeSymbol namedTypeSymbol)
            return;
        var attrs = ctx.Symbol.GetAttributes();
        if (attrs.Length == 0)
            return;
        
        var customEntityAttr = attrs.FirstOrDefault(attr => attr.AttributeClass?.Name is "CustomEntity");
        if (customEntityAttr is null)
        {
            return;
        }
        
        var syntax = new Lazy<ClassDeclarationSyntax>(() => (ClassDeclarationSyntax)ctx.Symbol.DeclaringSyntaxReferences.First().GetSyntax());

        if (!Utils.Extends(namedTypeSymbol, "Entity"))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(CustomEntityNotExtendingEntityRule, syntax.Value.GetLocation()));
            return;
        }

        var ids = customEntityAttr.ConstructorArguments.First().Values;
        if (ids.Length == 0)
        {
            var customEntityAttrSyntax = Utils.GetAttributeSyntaxFromClassDef(customEntityAttr, syntax.Value);
            ctx.ReportDiagnostic(Diagnostic.Create(CustomEntityNoIDsRule, customEntityAttrSyntax?.GetLocation()));
        }
        
        var members = namedTypeSymbol.GetMembers();
        var allAreGenerators = true;
        
        foreach (var id in ids)
        {
            var idVal = id.Value?.ToString().Trim() ?? "";
            
            var splitIdx = idVal.IndexOf('=');
            if (splitIdx == -1)
            {
                allAreGenerators = false;
                continue;
            }
            
            var generatorMethodName = idVal.Substring(splitIdx + 1).Trim();

            var generatorMethod = members.OfType<IMethodSymbol>().FirstOrDefault(m => m.Name == generatorMethodName);
            if (generatorMethod is null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(CustomEntityGeneratorMethodMissingRule, 
                    syntax.Value.GetLocation(),
                    generatorMethodName));
                continue;
            }

            if (!generatorMethod.IsStatic || !Utils.Extends(generatorMethod.ReturnType, "Entity"))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(CustomEntityGeneratorInvalidRule, generatorMethod.DeclaringSyntaxReferences.First().GetSyntax().GetLocation(),
                    generatorMethodName));
            }
            
            if (!IsValidCustomEntityGeneratorParams(generatorMethod))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(CustomEntityGeneratorInvalidParamsRule, generatorMethod.DeclaringSyntaxReferences.First().GetSyntax().GetLocation(),
                    generatorMethodName));
            }
        }

        var ctors = namedTypeSymbol.Constructors;
        if (!allAreGenerators && !ctors.Any(IsValidCustomEntityCtor))
        {
            var diagnostic = Diagnostic.Create(CustomEntityWithNoValidCtorRule, syntax.Value.GetLocation(), namedTypeSymbol.Name);
            ctx.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsValidCustomEntityCtor(IMethodSymbol ctor)
    {
        var p = ctor.Parameters;
        if (p.Length == 0 && !ctor.IsImplicitlyDeclared)
            return true;

        if (p.Length == 1 && p[0].Type.Name == "Vector2")
            return true;
        
        if (p.Length == 2 && p[0].Type.Name == "EntityData" && p[1].Type.Name == "Vector2")
            return true;
        
        if (p.Length == 3 && p[0].Type.Name == "EntityData" && p[1].Type.Name == "Vector2" && p[2].Type.Name == "EntityID")
            return true;

        return false;
    }

    private static bool IsValidCustomEntityGeneratorParams(IMethodSymbol gen)
    {
        var p = gen.Parameters;
        if (p.Length == 0 && !gen.IsImplicitlyDeclared)
            return true;

        if (p.Length == 1 && p[0].Type.Name == "Vector2")
            return true;
        
        if (p.Length == 2 && p[0].Type.Name == "EntityData" && p[1].Type.Name == "Vector2")
            return true;
        
        if (p.Length == 3 && p[0].Type.Name == "EntityData" && p[1].Type.Name == "Vector2" && p[2].Type.Name == "EntityID")
            return true;
            
        if (p.Length == 4 && p[0].Type.Name == "Level" && p[1].Type.Name == "LevelData" && p[2].Type.Name == "Vector2" && p[3].Type.Name == "EntityData")
            return true;

        return false;
    }
}