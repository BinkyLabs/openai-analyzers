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

namespace BinkyLabs.OpenAI.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SystemChatMessageCodeFixProvider)), Shared]
    public class SystemChatMessageCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SystemChatMessageAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the argument that triggered the diagnostic
            var argument = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ArgumentSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Split into SystemChatMessage and UserChatMessage",
                    createChangedDocument: c => SplitMessagesAsync(context.Document, argument, c),
                    equivalenceKey: nameof(SystemChatMessageCodeFixProvider)),
                diagnostic);
        }

        private async Task<Document> SplitMessagesAsync(Document document, ArgumentSyntax argument, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Find the object creation expression (either explicit or implicit)
            var objectCreation = argument.Parent.Parent;

            // Find the collection initializer or argument list that contains this object creation
            var collectionElement = objectCreation.Parent;

            // Get the interpolated string expression
            if (!(argument.Expression is InterpolatedStringExpressionSyntax interpolatedString))
                return document;

            // Extract the static parts and dynamic parts
            var staticParts = SyntaxFactory.List<InterpolatedStringContentSyntax>();
            var userMessageExpressions = new System.Collections.Generic.List<ExpressionSyntax>();

            foreach (var content in interpolatedString.Contents)
            {
                if (content is InterpolationSyntax interpolation)
                {
                    // Add the expression to user messages
                    userMessageExpressions.Add(interpolation.Expression);
                }
                else if (content is InterpolatedStringTextSyntax textSyntax)
                {
                    // Keep text in the static part
                    staticParts = staticParts.Add(textSyntax);
                }
            }

            // Create a new SystemChatMessage with only static content
            ExpressionSyntax newSystemMessageContent;
            if (interpolatedString.StringStartToken.Text.Contains("\"\"\""))
            {
                // Raw string literal
                var staticText = string.Concat(staticParts.Select(p => ((InterpolatedStringTextSyntax)p).TextToken.Text));
                newSystemMessageContent = SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(),
                        SyntaxKind.MultiLineRawStringLiteralToken,
                        $"\"\"\"\n{staticText.TrimEnd()}\n        \"\"\"",
                        staticText,
                        SyntaxFactory.TriviaList()));
            }
            else
            {
                // Regular string - just remove interpolations
                newSystemMessageContent = SyntaxFactory.InterpolatedStringExpression(
                    interpolatedString.StringStartToken,
                    staticParts,
                    interpolatedString.StringEndToken);
            }

            var newSystemMessageArgument = SyntaxFactory.Argument(newSystemMessageContent);

            ExpressionSyntax newSystemMessage;
            if (objectCreation is ObjectCreationExpressionSyntax explicitCreation)
            {
                newSystemMessage = explicitCreation.WithArgumentList(
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(newSystemMessageArgument)));
            }
            else if (objectCreation is ImplicitObjectCreationExpressionSyntax implicitCreation)
            {
                // For implicit, create explicit SystemChatMessage
                newSystemMessage = SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName("SystemChatMessage"))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(newSystemMessageArgument)));
            }
            else
            {
                return document;
            }

            // Create UserChatMessage for each interpolated expression
            var userMessages = userMessageExpressions.Select(expr =>
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName("UserChatMessage"))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(expr)))));

            // Find the collection and add the new messages
            SyntaxNode newRoot;
            if (collectionElement is InitializerExpressionSyntax initializer)
            {
                // Collection initializer syntax
                var newExpressions = SyntaxFactory.SeparatedList<ExpressionSyntax>(
                    new[] { newSystemMessage }.Concat(userMessages));

                var index = initializer.Expressions.IndexOf((ExpressionSyntax)objectCreation);
                var updatedExpressions = initializer.Expressions.RemoveAt(index).InsertRange(index, newExpressions);
                var newInitializer = initializer.WithExpressions(updatedExpressions);
                newRoot = root.ReplaceNode(initializer, newInitializer);
            }
            else
            {
                // Array creation or other context - just replace the single object creation
                var newExpressions = new[] { newSystemMessage }.Concat(userMessages).ToArray();
                if (newExpressions.Length == 1)
                {
                    newRoot = root.ReplaceNode(objectCreation, newExpressions[0]);
                }
                else
                {
                    // For multiple messages, we'd need more context - for now just replace with the system message
                    newRoot = root.ReplaceNode(objectCreation, newExpressions[0]);
                }
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}