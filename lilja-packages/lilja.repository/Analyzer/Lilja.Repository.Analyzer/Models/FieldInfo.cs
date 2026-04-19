using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;

namespace Lilja.Repository.Analyzer.Models;

/// <summary>
/// ValueObjectのタプル要素情報。
/// </summary>
internal readonly struct TupleElementInfo
{
    public string TypeName { get; }

    public string Name { get; }

    public string EscapedName => CodeGenHelpers.EscapeIdentifier(Name);

    public TupleElementInfo(string typeName, string name)
    {
        TypeName = typeName;
        Name = name;
    }
}

/// <summary>
/// ValueObject情報。
/// </summary>
internal readonly struct ValueObjectInfo
{
    public bool IsValueObject { get; }

    public string ToPrimitiveMethodName { get; }

    public string FromPrimitiveMethodName { get; }

    public bool IsFromPrimitiveStatic { get; }

    public IReadOnlyList<TupleElementInfo> TupleElements { get; }

    public ValueObjectInfo(
        bool isValueObject,
        string toPrimitiveMethodName,
        string fromPrimitiveMethodName,
        bool isFromPrimitiveStatic,
        IReadOnlyList<TupleElementInfo> tupleElements)
    {
        IsValueObject = isValueObject;
        ToPrimitiveMethodName = toPrimitiveMethodName;
        FromPrimitiveMethodName = fromPrimitiveMethodName;
        IsFromPrimitiveStatic = isFromPrimitiveStatic;
        TupleElements = tupleElements;
    }

    public static ValueObjectInfo None =>
        new ValueObjectInfo(false, string.Empty, string.Empty, false, System.Array.Empty<TupleElementInfo>());
}

internal enum EntityMemberKind
{
    Field,
    Property,
}

/// <summary>
/// Entityメンバー情報。
/// </summary>
internal readonly struct EntityMemberInfo
{
    public string Name { get; }

    public string TypeName { get; }

    public int Index { get; }

    public bool IsKey { get; }

    public bool IsPersisted { get; }

    public EntityMemberKind Kind { get; }

    public ValueObjectInfo ValueObjectInfo { get; }

    public EntityMemberInfo(
        string name,
        string typeName,
        int index,
        bool isKey,
        bool isPersisted,
        EntityMemberKind kind,
        ValueObjectInfo valueObjectInfo)
    {
        Name = name;
        TypeName = typeName;
        Index = index;
        IsKey = isKey;
        IsPersisted = isPersisted;
        Kind = kind;
        ValueObjectInfo = valueObjectInfo;
    }

    public string DtoFieldName => CodeGenHelpers.EscapeIdentifier(CodeGenHelpers.ToPascalCase(Name));

    public string ParameterName => CodeGenHelpers.EscapeIdentifier(CodeGenHelpers.ToCamelCase(Name));

    public string MemberName => CodeGenHelpers.EscapeIdentifier(Name);
}

internal static class CodeGenHelpers
{
    public static string ToPascalCase(string name)
    {
        var trimmed = name.TrimStart('_');
        if (trimmed.Length == 0)
        {
            return name;
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    public static string ToCamelCase(string name)
    {
        var pascal = ToPascalCase(name);
        if (pascal.Length == 0)
        {
            return name;
        }

        if (char.IsLower(pascal[0]))
        {
            return pascal;
        }

        return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
    }

    public static string EscapeIdentifier(string name)
    {
        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None)
        {
            return "@" + name;
        }

        return name;
    }
}
