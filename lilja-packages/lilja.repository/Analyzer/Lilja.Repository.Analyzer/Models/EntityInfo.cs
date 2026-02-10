using System.Collections.Generic;
using System.Linq;

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
    /// Keyフィールド一覧（Persistインデックス順）。
    /// </summary>
    public IReadOnlyList<FieldInfo> KeyFields { get; }

    /// <summary>
    /// Keyフィールドが存在するかどうか。
    /// </summary>
    public bool HasKey => KeyFields.Count > 0;

    /// <summary>
    /// 複合キーかどうか（Keyフィールドが2つ以上）。
    /// </summary>
    public bool IsCompositeKey => KeyFields.Count > 1;

    /// <summary>
    /// Persist属性フィールドが存在するかどうか。
    /// </summary>
    public bool HasPersistFields => Fields.Count > 0;

    /// <summary>
    /// DTO復元用のprivateコンストラクタを生成する必要があるかどうか。
    /// 既にPersist属性フィールドを網羅したコンストラクタが存在する場合はfalse。
    /// </summary>
    public bool NeedsConstructorGeneration { get; }

    public EntityInfo(string @namespace, string className, IReadOnlyList<FieldInfo> fields, IReadOnlyList<FieldInfo> keyFields, bool needsConstructorGeneration)
    {
        Namespace = @namespace;
        ClassName = className;
        Fields = fields;
        KeyFields = keyFields;
        NeedsConstructorGeneration = needsConstructorGeneration;
    }
}
