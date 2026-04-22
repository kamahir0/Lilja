using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

/// <summary>
/// Centralizes diagnostic descriptors emitted by the repository source generator.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// Diagnostic emitted when an entity declaration is missing the <c>partial</c> modifier.
    /// </summary>
    public static readonly DiagnosticDescriptor EntityMustBePartial = new DiagnosticDescriptor(
        "LILJAREPO001",
        "Entity must be partial",
        "Entity must be partial",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when an entity declaration uses generic type parameters.
    /// </summary>
    public static readonly DiagnosticDescriptor GenericEntityNotSupported = new DiagnosticDescriptor(
        "LILJAREPO002",
        "Generic entity is not supported",
        "Generic entity is not supported",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when a static member is annotated for repository participation.
    /// </summary>
    public static readonly DiagnosticDescriptor StaticMemberNotSupported = new DiagnosticDescriptor(
        "LILJAREPO003",
        "Static member is not supported",
        "Static member is not supported",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when an unsupported member kind is annotated for persistence.
    /// </summary>
    public static readonly DiagnosticDescriptor OnlyAutoPropertiesSupported = new DiagnosticDescriptor(
        "LILJAREPO004",
        "Only auto-properties are supported",
        "Only auto-properties are supported",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when multiple persisted members reuse the same index.
    /// </summary>
    public static readonly DiagnosticDescriptor PersistIndexMustBeUnique = new DiagnosticDescriptor(
        "LILJAREPO005",
        "Persist index must be unique",
        "Persist index must be unique",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when a key member is not also marked for persistence.
    /// </summary>
    public static readonly DiagnosticDescriptor PersistedKeysMustAlsoBePersisted = new DiagnosticDescriptor(
        "LILJAREPO006",
        "Persisted keys must also be persisted",
        "Persisted keys must also be persisted",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when a value object's <c>[ToPrimitive]</c> method is invalid.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidToPrimitiveDefinition = new DiagnosticDescriptor(
        "LILJAREPO007",
        "Invalid [ToPrimitive] definition",
        "Invalid [ToPrimitive] definition",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);

    /// <summary>
    /// Diagnostic emitted when a value object's <c>[FromPrimitive]</c> entry point is invalid.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidFromPrimitiveDefinition = new DiagnosticDescriptor(
        "LILJAREPO008",
        "Invalid [FromPrimitive] definition",
        "Invalid [FromPrimitive] definition",
        "Lilja.Repository",
        DiagnosticSeverity.Error,
        true);
}
