using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// Determines whether any declaration of the entity is marked as <c>partial</c>.
    /// </summary>
    /// <param name="entitySymbol">The entity symbol to inspect.</param>
    /// <returns><see langword="true"/> when a partial declaration exists; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Determines whether a symbol has an attribute with the supplied metadata name.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="metadataName">The fully qualified attribute metadata name.</param>
    /// <returns><see langword="true"/> when the attribute is present; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Attempts to read the constructor index from a <c>[Persist]</c> attribute.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="index">The extracted persistence index when available.</param>
    /// <returns><see langword="true"/> when a valid index is present; otherwise <see langword="false"/>.</returns>
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

    /// <summary>
    /// Returns the first source location for a symbol, or <see cref="Location.None"/> when unavailable.
    /// </summary>
    /// <param name="symbol">The symbol whose location is required.</param>
    /// <returns>The primary location used for diagnostics.</returns>
    private static Location GetPrimaryLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }

    /// <summary>
    /// Formats a type symbol using the fully qualified display format required by generated source.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to format.</param>
    /// <returns>The fully qualified type name.</returns>
    private static string GetTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedTypeFormat);
    }

    /// <summary>
    /// Escapes C# keywords so generated identifiers remain valid source.
    /// </summary>
    /// <param name="identifier">The identifier to escape.</param>
    /// <returns>The original or escaped identifier.</returns>
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

    /// <summary>
    /// Converts an identifier to PascalCase for generated type and field names.
    /// </summary>
    /// <param name="identifier">The identifier to convert.</param>
    /// <returns>The PascalCase identifier.</returns>
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

    /// <summary>
    /// Converts an identifier to camelCase for generated local variables and constructor parameters.
    /// </summary>
    /// <param name="identifier">The identifier to convert.</param>
    /// <returns>The camelCase identifier.</returns>
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
