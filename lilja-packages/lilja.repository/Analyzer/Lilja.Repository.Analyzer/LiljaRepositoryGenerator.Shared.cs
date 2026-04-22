using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    private static bool IsPartial(INamedTypeSymbol entitySymbol)
    {
        foreach (var syntaxReference in entitySymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is ClassDeclarationSyntax classDeclaration &&
                classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPersistIndex(ISymbol symbol, out int? index)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (attributeData.AttributeClass?.ToDisplayString() != PersistAttributeMetadataName)
            {
                continue;
            }

            if (attributeData.ConstructorArguments.Length == 1 &&
                attributeData.ConstructorArguments[0].Value is int persistIndex)
            {
                index = persistIndex;
                return true;
            }
        }

        index = null;
        return false;
    }

    private static Location GetPrimaryLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }

    private static string GetTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedTypeFormat);
    }

    private static string EscapeIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;
    }

    private static string ToPascalCase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        if (identifier.Length == 1)
        {
            return identifier.ToUpperInvariant();
        }

        return char.ToUpperInvariant(identifier[0]) + identifier.Substring(1);
    }

    private static string ToCamelCase(string identifier)
    {
        var trimmed = identifier.TrimStart('_');
        if (string.IsNullOrEmpty(trimmed))
        {
            trimmed = identifier;
        }

        if (string.IsNullOrEmpty(trimmed))
        {
            trimmed = "value";
        }

        if (trimmed.Length == 1)
        {
            return EscapeIdentifier(trimmed.ToLowerInvariant());
        }

        return EscapeIdentifier(char.ToLowerInvariant(trimmed[0]) + trimmed.Substring(1));
    }
}
