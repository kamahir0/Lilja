using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// エンティティのいずれかの宣言が <c>partial</c> としてマークされているかどうかを判定します。
    /// </summary>
    /// <param name="entitySymbol">確認するエンティティシンボル。</param>
    /// <returns>partial 宣言が存在する場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
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
    /// シンボルが指定されたメタデータ名の属性を持つかどうかを判定します。
    /// </summary>
    /// <param name="symbol">確認するシンボル。</param>
    /// <param name="metadataName">完全修飾された属性メタデータ名。</param>
    /// <returns>属性が存在する場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
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
    /// <c>[Persist]</c> 属性からコンストラクタ引数インデックスの読み取りを試みます。
    /// </summary>
    /// <param name="symbol">確認するシンボル。</param>
    /// <param name="index">利用可能な場合に取り出された永続化インデックス。</param>
    /// <returns>有効なインデックスが存在する場合は <see langword="true"/>、それ以外は <see langword="false"/>。</returns>
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
    /// シンボルに対する最初のソース位置を返します。利用できない場合は <see cref="Location.None"/> です。
    /// </summary>
    /// <param name="symbol">位置情報が必要なシンボル。</param>
    /// <returns>診断に使う主要な位置情報。</returns>
    private static Location GetPrimaryLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault() ?? Location.None;
    }

    /// <summary>
    /// 生成ソースで必要となる完全修飾表示形式を使って型シンボルを整形します。
    /// </summary>
    /// <param name="typeSymbol">整形する型シンボル。</param>
    /// <returns>完全修飾型名。</returns>
    private static string GetTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedTypeFormat);
    }

    /// <summary>
    /// 生成される識別子が有効なソースのままでいられるよう、C# キーワードをエスケープします。
    /// </summary>
    /// <param name="identifier">エスケープする識別子。</param>
    /// <returns>元の識別子、またはエスケープ済み識別子。</returns>
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
    /// 識別子を、生成される型名やフィールド名向けの PascalCase に変換します。
    /// </summary>
    /// <param name="identifier">変換する識別子。</param>
    /// <returns>PascalCase の識別子。</returns>
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
    /// 識別子を、生成されるローカル変数やコンストラクタ引数向けの camelCase に変換します。
    /// </summary>
    /// <param name="identifier">変換する識別子。</param>
    /// <returns>camelCase の識別子。</returns>
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
