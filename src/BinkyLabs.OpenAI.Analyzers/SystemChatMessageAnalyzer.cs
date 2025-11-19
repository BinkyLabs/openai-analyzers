using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BinkyLabs.OpenAI.Analyzers
{
    /// <summary>
	/// Analyzer that detects interpolated strings in SystemChatMessage constructors.
	/// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class SystemChatMessageAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
		/// The diagnostic ID for the analyzer.
		/// </summary>
        public const string DiagnosticId = "BOA001";

        private static readonly LocalizableString Title = "Avoid inputs in SystemChatMessage";
        private static readonly LocalizableString MessageFormat = "SystemChatMessage contains interpolated expressions which may include user input. Move user content to UserChatMessage to prevent prompt injection.";
        private static readonly LocalizableString Description = "Including user inputs in SystemChatMessage is a security risk and might allow bad actors to perform prompt injection. System Messages should only contain static information.";
        private const string Category = "Security";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description,
            helpLinkUri: "https://github.com/BinkyLabs/openai-analyzers/blob/main/rules/BOA001.md");

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeImplicitObjectCreation, SyntaxKind.ImplicitObjectCreationExpression);
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ObjectCreationExpressionSyntax objectCreation)
            {
                // Try to get the type symbol from the entire node first (more reliable for test scenarios)
                var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken);
                AnalyzeSystemChatMessageCreation(context, objectCreation.Type, objectCreation.ArgumentList, typeInfo.Type);
            }
        }

        private static void AnalyzeImplicitObjectCreation(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is ImplicitObjectCreationExpressionSyntax implicitObjectCreation)
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(implicitObjectCreation, context.CancellationToken);
                if (typeInfo.Type == null)
                    return;

                AnalyzeSystemChatMessageCreation(context, null, implicitObjectCreation.ArgumentList, typeInfo.Type);
            }
        }

        private static void AnalyzeSystemChatMessageCreation(
            SyntaxNodeAnalysisContext context,
            TypeSyntax typeSyntax,
            ArgumentListSyntax argumentList,
            ITypeSymbol typeSymbol = null)
        {
            if (argumentList == null || argumentList.Arguments.Count == 0)
                return;

            // Get the type symbol
            ITypeSymbol type = typeSymbol;
            if (type == null && typeSyntax != null)
            {
                type = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type;
            }

            if (type == null || type.TypeKind == TypeKind.Error)
                return;

            // Check if this is a SystemChatMessage
            if (type.Name != "SystemChatMessage")
                return;

            var namespaceName = type.ContainingNamespace?.ToDisplayString();
            if (namespaceName != "OpenAI.Chat")
                return;

            // Check the first argument for interpolated strings
            var firstArgument = argumentList.Arguments[0];

            // Direct interpolation check
            if (HasNonConstantInterpolation(context, firstArgument.Expression))
            {
                var diagnostic = Diagnostic.Create(Rule, firstArgument.Expression.GetLocation());
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Check if the argument is a variable that was assigned from ChatMessageContentPart.CreateTextPart
            var interpolatedLocation = FindInterpolatedStringInDataFlow(context, firstArgument.Expression);
            if (interpolatedLocation != null)
            {
                var diagnostic = Diagnostic.Create(Rule, interpolatedLocation);
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // Check if the argument is a non-constant variable reference
            if (IsNonConstantVariable(context, firstArgument.Expression))
            {
                var diagnostic = Diagnostic.Create(Rule, firstArgument.Expression.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool HasInterpolation(ExpressionSyntax expression)
        {
            // Direct check - is this node itself an interpolated string?
            if (expression is InterpolatedStringExpressionSyntax)
                return true;

            // Check descendants - does this contain any interpolated strings?
            // This handles cases where the expression might be wrapped
            return expression.DescendantNodesAndSelf()
                .Any(node => node is InterpolatedStringExpressionSyntax);
        }

        private static bool HasNonConstantInterpolation(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            // Find all interpolated string expressions
            var interpolatedStrings = expression.DescendantNodesAndSelf()
                .OfType<InterpolatedStringExpressionSyntax>();

            foreach (var interpolatedString in interpolatedStrings)
            {
                // Check each interpolation within the string
                foreach (var content in interpolatedString.Contents)
                {
                    if (content is InterpolationSyntax interpolation)
                    {
                        // Check if the interpolated expression is a constant
                        var constantValue = context.SemanticModel.GetConstantValue(interpolation.Expression, context.CancellationToken);
                        if (!constantValue.HasValue)
                        {
                            // Not a constant - this is potentially user input
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static Location FindInterpolatedStringInDataFlow(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            // Handle identifier (variable reference)
            if (expression is IdentifierNameSyntax identifierName)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol;
                if (symbol is ILocalSymbol localSymbol)
                {
                    // Find the variable declaration
                    var declaringSyntaxReferences = localSymbol.DeclaringSyntaxReferences;
                    if (declaringSyntaxReferences.Length > 0)
                    {
                        var declarationSyntax = declaringSyntaxReferences[0].GetSyntax(context.CancellationToken);

                        // Check if it's a variable declarator
                        if (declarationSyntax is VariableDeclaratorSyntax variableDeclarator)
                        {
                            if (variableDeclarator.Initializer?.Value != null)
                            {
                                var location = FindInterpolatedStringInExpression(context, variableDeclarator.Initializer.Value);
                                if (location != null)
                                    return location;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static Location FindInterpolatedStringInExpression(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            // Check for invocation of ChatMessageContentPart.CreateTextPart
            if (expression is InvocationExpressionSyntax invocation)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                {
                    // Check if this is CreateTextPart from ChatMessageContentPart
                    if (methodSymbol.Name == "CreateTextPart" &&
                        methodSymbol.ContainingType?.Name == "ChatMessageContentPart" &&
                        methodSymbol.ContainingNamespace?.ToDisplayString() == "OpenAI.Chat")
                    {
                        // Check if the argument has interpolation
                        if (invocation.ArgumentList?.Arguments.Count > 0)
                        {
                            var firstArg = invocation.ArgumentList.Arguments[0];
                            if (HasInterpolation(firstArg.Expression))
                            {
                                return firstArg.Expression.GetLocation();
                            }
                        }
                    }
                }
            }
            // Check for object creation expressions (List<ChatMessageContentPart>)
            else if (expression is ObjectCreationExpressionSyntax objectCreation)
            {
                if (objectCreation.Initializer != null)
                {
                    foreach (var expr in objectCreation.Initializer.Expressions)
                    {
                        var location = FindInterpolatedStringInExpression(context, expr);
                        if (location != null)
                            return location;
                    }
                }
            }
            // Check for implicit object creation expressions
            else if (expression is ImplicitObjectCreationExpressionSyntax implicitObjectCreation)
            {
                if (implicitObjectCreation.Initializer != null)
                {
                    foreach (var expr in implicitObjectCreation.Initializer.Expressions)
                    {
                        var location = FindInterpolatedStringInExpression(context, expr);
                        if (location != null)
                            return location;
                    }
                }
            }

            return null;
        }

        private static bool IsNonConstantVariable(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            // Check if the expression is a simple identifier (variable reference)
            if (expression is IdentifierNameSyntax identifierName)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken).Symbol;
                
                // Check if it's a local variable or parameter (not a constant)
                if (symbol is ILocalSymbol localSymbol)
                {
                    // If it's a const, it's safe
                    if (localSymbol.IsConst)
                        return false;
                    
                    // It's a non-const local variable
                    return true;
                }
                
                // Check if it's a parameter
                if (symbol is IParameterSymbol)
                {
                    // Parameters are considered non-constant
                    return true;
                }
                
                // Check if it's a field or property (also potentially non-constant)
                if (symbol is IFieldSymbol fieldSymbol)
                {
                    // Const fields are safe
                    if (fieldSymbol.IsConst)
                        return false;
                    
                    // Non-const fields are potentially user input
                    return true;
                }
                
                if (symbol is IPropertySymbol)
                {
                    // Properties are considered non-constant
                    return true;
                }
            }
            
            return false;
        }
    }
}