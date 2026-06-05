using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

/// <summary>
/// リポジトリソースジェネレーターが出力する診断ディスクリプターを一元管理します。
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// エンティティ宣言に <c>partial</c> 修飾子がない場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor EntityMustBePartial = new DiagnosticDescriptor(
        "LILJAREPO001",
        "Entity must be partial",
        "Entity must be partial",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// エンティティ宣言がジェネリック型引数を使用している場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor GenericEntityNotSupported = new DiagnosticDescriptor(
        "LILJAREPO002",
        "Generic entity is not supported",
        "Generic entity is not supported",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// 静的メンバーにリポジトリ参加用の注釈が付いている場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor StaticMemberNotSupported = new DiagnosticDescriptor(
        "LILJAREPO003",
        "Static member is not supported",
        "Static member is not supported",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// 未対応のメンバー種別に永続化注釈が付いている場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor OnlyAutoPropertiesSupported = new DiagnosticDescriptor(
        "LILJAREPO004",
        "Only auto-properties are supported",
        "Only auto-properties are supported",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// 複数の永続化対象メンバーが同じインデックスを再利用している場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor PersistIndexMustBeUnique = new DiagnosticDescriptor(
        "LILJAREPO005",
        "Persist index must be unique",
        "Persist index must be unique",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// キーメンバーが永続化対象としてもマークされていない場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor PersistedKeysMustAlsoBePersisted = new DiagnosticDescriptor(
        "LILJAREPO006",
        "Persisted keys must also be persisted",
        "Persisted keys must also be persisted",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// 値オブジェクトの <c>[ToPrimitive]</c> メソッドが無効な場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidToPrimitiveDefinition = new DiagnosticDescriptor(
        "LILJAREPO007",
        "Invalid [ToPrimitive] definition",
        "Invalid [ToPrimitive] definition",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// 値オブジェクトの <c>[FromPrimitive]</c> エントリーポイントが無効な場合に出力される診断です。
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidFromPrimitiveDefinition = new DiagnosticDescriptor(
        "LILJAREPO008",
        "Invalid [FromPrimitive] definition",
        "Invalid [FromPrimitive] definition",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);
}
