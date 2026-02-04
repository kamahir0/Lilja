using System.Collections.Generic;

namespace Lilja.Repository.Analyzer.Models;

/// <summary>
/// Entity情報。
/// </summary>
internal readonly struct EntityInfo
{
    /// <summary>
    /// 名前空間。
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// クラス名。
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// フィールド一覧（Persistインデックス順）。
    /// </summary>
    public IReadOnlyList<FieldInfo> Fields { get; }

    /// <summary>
    /// Keyフィールドが存在するかどうか。
    /// </summary>
    public bool HasKey { get; }

    /// <summary>
    /// Keyフィールド（存在する場合）。
    /// </summary>
    public FieldInfo? KeyField { get; }

    /// <summary>
    /// DTO復元用のprivateコンストラクタを生成する必要があるかどうか。
    /// 既にPersist属性フィールドを網羅したコンストラクタが存在する場合はfalse。
    /// </summary>
    public bool NeedsConstructorGeneration { get; }

    public EntityInfo(string @namespace, string className, IReadOnlyList<FieldInfo> fields, bool needsConstructorGeneration)
    {
        Namespace = @namespace;
        ClassName = className;
        Fields = fields;
        NeedsConstructorGeneration = needsConstructorGeneration;

        FieldInfo? keyField = null;
        foreach (var field in fields)
        {
            if (field.IsKey)
            {
                keyField = field;
                break;
            }
        }
        HasKey = keyField.HasValue;
        KeyField = keyField;
    }
}
