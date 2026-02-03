using System.Collections.Generic;

namespace Lilja.Repository.Analyzer.Models;

/// <summary>
/// ValueObjectのタプル要素情報。
/// </summary>
internal readonly struct TupleElementInfo
{
    /// <summary>
    /// 要素の型名。
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// 要素名。
    /// </summary>
    public string Name { get; }

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
    /// <summary>
    /// ValueObjectかどうか。
    /// </summary>
    public bool IsValueObject { get; }

    /// <summary>
    /// ToPrimitiveメソッド名。
    /// </summary>
    public string ToPrimitiveMethodName { get; }

    /// <summary>
    /// タプル要素一覧（フラット化用）。
    /// </summary>
    public IReadOnlyList<TupleElementInfo> TupleElements { get; }

    public ValueObjectInfo(bool isValueObject, string toPrimitiveMethodName, IReadOnlyList<TupleElementInfo> tupleElements)
    {
        IsValueObject = isValueObject;
        ToPrimitiveMethodName = toPrimitiveMethodName;
        TupleElements = tupleElements;
    }

    public static ValueObjectInfo None => new(false, string.Empty, System.Array.Empty<TupleElementInfo>());
}

/// <summary>
/// フィールド情報。
/// </summary>
internal readonly struct FieldInfo
{
    /// <summary>
    /// フィールド名（アンダースコア含む）。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 型名。
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// 完全修飾型名。
    /// </summary>
    public string FullTypeName { get; }

    /// <summary>
    /// Persistインデックス。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Keyかどうか。
    /// </summary>
    public bool IsKey { get; }

    /// <summary>
    /// ValueObject情報。
    /// </summary>
    public ValueObjectInfo ValueObjectInfo { get; }

    public FieldInfo(string name, string typeName, string fullTypeName, int index, bool isKey, ValueObjectInfo valueObjectInfo)
    {
        Name = name;
        TypeName = typeName;
        FullTypeName = fullTypeName;
        Index = index;
        IsKey = isKey;
        ValueObjectInfo = valueObjectInfo;
    }

    /// <summary>
    /// DTO用のフィールド名（アンダースコアなし、PascalCase）。
    /// </summary>
    public string DtoFieldName
    {
        get
        {
            var name = Name.TrimStart('_');
            if (name.Length == 0) return Name;
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }
}
