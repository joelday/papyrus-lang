using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Papyrus.CodeAnalysis.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class SyntaxGenerator : IIncrementalGenerator
{
    private const string TokenAttributeMetadataName = "Papyrus.CodeAnalysis.Syntax.SyntaxTokenAttribute";
    private const string TriviaAttributeMetadataName = "Papyrus.CodeAnalysis.Syntax.SyntaxTriviaAttribute";
    private const string NodeAttributeMetadataName = "Papyrus.CodeAnalysis.Syntax.SyntaxNodeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static initializationContext =>
        {
            initializationContext.AddSource(
                AttributeSources.FileName,
                SourceText.From(AttributeSources.Text, System.Text.Encoding.UTF8));
        });

        var tokens = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TokenAttributeMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    TransformToken(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value);

        var trivia = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TriviaAttributeMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    TransformTrivia(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value);

        var nodes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                NodeAttributeMetadataName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) =>
                    TransformNode(syntaxContext, cancellationToken))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value);

        var allKinds = tokens.Collect()
            .Combine(trivia.Collect())
            .Combine(nodes.Collect());

        context.RegisterSourceOutput(allKinds, static (productionContext, data) =>
        {
            var tokensAndTrivia = data.Left;
            SyntaxEmitter.Emit(
                productionContext,
                tokensAndTrivia.Left,
                tokensAndTrivia.Right,
                data.Right);
        });
    }

    private static KindModel? TransformToken(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var name = type.Name;
        var text = ReadTextArgument(context.Attributes);
        if (text is null && IsKeywordName(name))
        {
            text = name.Substring(0, name.Length - "Keyword".Length);
        }

        var category = IsKeywordName(name) ? KindCategory.Keyword : KindCategory.Token;
        return new KindModel(name, category, text, ImmutableArray<ChildSlot>.Empty, type.Name);
    }

    private static KindModel? TransformTrivia(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        return new KindModel(type.Name, KindCategory.Trivia, text: null, ImmutableArray<ChildSlot>.Empty, type.Name);
    }

    private static KindModel? TransformNode(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var children = new List<ChildSlot>();
        foreach (var member in type.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is IPropertySymbol property
                && !property.IsStatic
                && property.GetMethod is not null
                && property.Parameters.Length == 0
                && property.ContainingType.Equals(type, SymbolEqualityComparer.Default))
            {
                var typeName = property.Type.Name;
                var isToken = typeName == "SyntaxToken"
                    || typeName.EndsWith("Token", StringComparison.Ordinal)
                    || typeName.EndsWith("Keyword", StringComparison.Ordinal);
                var isOptional = !isToken && property.NullableAnnotation == NullableAnnotation.Annotated;
                children.Add(new ChildSlot(property.Name, typeName, isToken, isOptional));
            }
        }

        var baseTypeName = type.BaseType is { SpecialType: not SpecialType.System_Object } baseType
            ? baseType.Name
            : "SyntaxNode";

        return new KindModel(
            StripSuffix(type.Name, "Syntax"),
            KindCategory.Node,
            text: null,
            children.ToImmutableArray(),
            type.Name,
            baseTypeName);
    }

    private static string? ReadTextArgument(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Text" && argument.Value.Value is string text)
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static bool IsKeywordName(string name)
        => name.Length > "Keyword".Length && name.EndsWith("Keyword", StringComparison.Ordinal);

    private static string StripSuffix(string name, string suffix)
    {
        if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - suffix.Length);
        }

        return name;
    }
}
