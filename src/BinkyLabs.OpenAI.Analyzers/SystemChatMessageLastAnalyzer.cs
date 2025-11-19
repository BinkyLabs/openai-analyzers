using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BinkyLabs.OpenAI.Analyzers
{
    /// <summary>
    /// Analyzer that detects when SystemChatMessage is not the last message in a collection.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SystemChatMessageLastAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for the analyzer.
        /// </summary>
        public const string DiagnosticId = "BOA002";

        private static readonly LocalizableString Title = "SystemChatMessage should be last";
        private static readonly LocalizableString MessageFormat = "Consider adding a SystemChatMessage as the last message to help mitigate potential prompt injections";
        private static readonly LocalizableString Description = "Including an additional SystemChatMessage last is a good way to help mitigate potential prompt injections by reminding the model of its constraints.";
        private const string Category = "Security";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/BinkyLabs/openai-analyzers/blob/main/rules/BOA002.md");

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeCollectionExpression, SyntaxKind.CollectionExpression);
            context.RegisterSyntaxNodeAction(AnalyzeArrayCreation, SyntaxKind.ArrayCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeImplicitArrayCreation, SyntaxKind.ImplicitArrayCreationExpression);
        }

        private static void AnalyzeCollectionExpression(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is CollectionExpressionSyntax collectionExpression)
            {
                var elements = collectionExpression.Elements;
                if (elements.Count < 2)
                    return;

                var expressions = elements
                    .OfType<ExpressionElementSyntax>()
                    .Select(e => e.Expression)
                    .Where(e => e != null)
                    .ToList();

                AnalyzeMessageCollection(context, expressions);
            }
        }

        private static void AnalyzeArrayCreation(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ArrayCreationExpressionSyntax arrayCreation)
            {
                if (arrayCreation.Initializer == null || arrayCreation.Initializer.Expressions.Count < 2)
                    return;

                AnalyzeMessageCollection(context, arrayCreation.Initializer.Expressions.ToList());
            }
        }

        private static void AnalyzeImplicitArrayCreation(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ImplicitArrayCreationExpressionSyntax implicitArrayCreation)
            {
                if (implicitArrayCreation.Initializer == null || implicitArrayCreation.Initializer.Expressions.Count < 2)
                    return;

                AnalyzeMessageCollection(context, implicitArrayCreation.Initializer.Expressions.ToList());
            }
        }

        private static void AnalyzeMessageCollection(SyntaxNodeAnalysisContext context, System.Collections.Generic.List<ExpressionSyntax> expressions)
        {
            if (expressions.Count < 2)
                return;

            // Check if the last expression is a SystemChatMessage
            var lastExpression = expressions[expressions.Count - 1];
            var lastType = GetMessageType(context, lastExpression);

            if (lastType == null)
                return;

            // Only proceed if we have chat messages (check if any of them is a chat message type)
            bool hasChatMessages = expressions.Any(expr =>
            {
                var type = GetMessageType(context, expr);
                return IsChatMessageType(type);
            });

            if (!hasChatMessages)
                return;

            // If the last message is already a SystemChatMessage, no diagnostic needed
            if (IsSystemChatMessage(lastType))
                return;

            // Check if there's at least one SystemChatMessage in the collection
            bool hasSystemMessage = false;
            bool hasUserOrAssistantMessage = false;

            foreach (var expr in expressions)
            {
                var type = GetMessageType(context, expr);
                if (type == null)
                    continue;

                if (IsSystemChatMessage(type))
                {
                    hasSystemMessage = true;
                }
                else if (IsUserChatMessage(type) || IsAssistantChatMessage(type))
                {
                    hasUserOrAssistantMessage = true;
                }
            }

            // Report diagnostic only if there's at least one SystemChatMessage and at least one User/Assistant message
            // and the last message is not a SystemChatMessage
            if (hasSystemMessage && hasUserOrAssistantMessage)
            {
                var diagnostic = Diagnostic.Create(Rule, lastExpression.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static ITypeSymbol GetMessageType(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            // Get the type from the expression
            var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
            return typeInfo.Type;
        }

        private static bool IsChatMessageType(ITypeSymbol type)
        {
            if (type == null)
                return false;

            var namespaceName = type.ContainingNamespace?.ToDisplayString();
            if (namespaceName != "OpenAI.Chat")
                return false;

            return type.Name == "SystemChatMessage" ||
                   type.Name == "UserChatMessage" ||
                   type.Name == "AssistantChatMessage" ||
                   type.Name == "ToolChatMessage" ||
                   type.Name == "FunctionChatMessage";
        }

        private static bool IsSystemChatMessage(ITypeSymbol type)
        {
            if (type == null)
                return false;

            return type.Name == "SystemChatMessage" &&
                   type.ContainingNamespace?.ToDisplayString() == "OpenAI.Chat";
        }

        private static bool IsUserChatMessage(ITypeSymbol type)
        {
            if (type == null)
                return false;

            return type.Name == "UserChatMessage" &&
                   type.ContainingNamespace?.ToDisplayString() == "OpenAI.Chat";
        }

        private static bool IsAssistantChatMessage(ITypeSymbol type)
        {
            if (type == null)
                return false;

            return type.Name == "AssistantChatMessage" &&
                   type.ContainingNamespace?.ToDisplayString() == "OpenAI.Chat";
        }
    }
}
