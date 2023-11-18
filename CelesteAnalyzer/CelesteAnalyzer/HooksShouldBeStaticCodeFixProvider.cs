using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace CelesteAnalyzer;


[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HooksShouldBeStaticCodeFixProvider)), Shared]
public class HooksShouldBeStaticCodeFixProvider : CodeFixProvider
{
    private const string CommonName = "Common";

    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.HooksShouldBeStatic);

    public override FixAllProvider? GetFixAllProvider() => null;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // We link only one diagnostic and assume there is only one diagnostic in the context.
        var diagnostic = context.Diagnostics.Single();

        // 'SourceSpan' of 'Location' is the highlighted area. We're going to use this area to find the 'SyntaxNode' to rename.
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Get the root of Syntax Tree that contains the highlighted diagnostic.
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // Find SyntaxNode corresponding to the diagnostic.
        var diagnosticNode = root?.FindNode(diagnosticSpan);

        if (diagnosticNode is MethodDeclarationSyntax id)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Resources.CL0004CodeFixTitle,
                    createChangedDocument: c => MakeMethodStatic(context.Document, id, c),
                    equivalenceKey: nameof(Resources.CL0004CodeFixTitle)),
                diagnostic);
        }

        if (diagnosticNode is LambdaExpressionSyntax declaration)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Resources.CL0004CodeFixTitle,
                    createChangedDocument: c => MakeLambdaStatic(context.Document, declaration, c),
                    equivalenceKey: nameof(Resources.CL0004CodeFixTitle)),
                diagnostic);
        }
    }

    /// <summary>
    /// Executed on the quick fix action raised by the user.
    /// </summary>
    private async Task<Document> MakeMethodStatic(Document document,
        MethodDeclarationSyntax decl, CancellationToken cancellationToken)
    {
        SyntaxToken constToken = SyntaxFactory.Token(
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker), 
            SyntaxKind.StaticKeyword, 
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));

        var accessibilityToken = decl.Modifiers.FirstOrDefault(m => 
            m.IsKind(SyntaxKind.PrivateKeyword) || m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.InternalKeyword));
        var staticTokenIndex = accessibilityToken is { } ? decl.Modifiers.IndexOf(accessibilityToken) + 1 : 0;
        
        var newLambdaExpr = decl.WithModifiers(decl.Modifiers.Insert(staticTokenIndex, constToken));
        
        var formattedLambda = newLambdaExpr.WithAdditionalAnnotations(Formatter.Annotation);
        
        // Replace the old local declaration with the new local declaration.
        var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (oldRoot is null)
            return document;
        var newRoot = oldRoot.ReplaceNode(decl, formattedLambda);

        // Return document with transformed tree.
        return document.WithSyntaxRoot(newRoot);
    }
    
    private async Task<Document> MakeLambdaStatic(Document document,
        LambdaExpressionSyntax lambdaExpression, CancellationToken cancellationToken)
    {
        SyntaxToken constToken = SyntaxFactory.Token(
            lambdaExpression.GetFirstToken().LeadingTrivia, 
            SyntaxKind.StaticKeyword, 
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticMarker));

        var newLambdaExpr = lambdaExpression
            .WithModifiers(lambdaExpression.Modifiers.Insert(0, constToken));
        
        var formattedLambda = newLambdaExpr.WithAdditionalAnnotations(Formatter.Annotation);
        
        // Replace the old local declaration with the new local declaration.
        var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (oldRoot is null)
            return document;
        var newRoot = oldRoot.ReplaceNode(lambdaExpression, formattedLambda);

        // Return document with transformed tree.
        return document.WithSyntaxRoot(newRoot);
    }
}