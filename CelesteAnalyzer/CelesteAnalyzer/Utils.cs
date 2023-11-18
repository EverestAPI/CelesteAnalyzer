using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace CelesteAnalyzer;

public static class Utils
{
    /// <summary>
    /// Gets the bottommost namespace of a symbol.
    /// For example, for the symbol "On.Celeste.Player.Update", this returns "On"
    /// </summary>
    public static INamespaceSymbol? BottommostNamespace(ISymbol symbol)
    {
        var ns = symbol.ContainingNamespace;
        if (ns is null)
            return null;
        while (ns.ContainingNamespace is { IsGlobalNamespace: false } inner)
        {
            ns = inner;
        }

        return ns;
    }

    /// <summary>
    /// Gets the MethodDefinitionSyntax from a given IdentifierNameSyntax
    /// </summary>
    public static MethodDeclarationSyntax? GetMethodDeclarationSyntaxFromIdentifier(IdentifierNameSyntax id, SemanticModel sem, 
        out IMethodReferenceOperation methodReference)
    {
        if (sem.GetOperation(id) is IMethodReferenceOperation methodRef
            && methodRef.Method.DeclaringSyntaxReferences.Select(r => r.GetSyntax())
                .First(r => r is MethodDeclarationSyntax) is MethodDeclarationSyntax syntax)
        {
            methodReference = methodRef;
            return syntax;
        }

        methodReference = null!;
        return null;
    }

    /// <summary>
    /// Checks whether the type extends a class named <paramref name="className"/>, recursively.
    /// </summary>
    public static bool Extends(ITypeSymbol type, string className)
    {
        var t = type;
        while (t is not null)
        {
            if (t.Name == className)
                return true;
            t = t.BaseType;
        }

        return false;
    }
}