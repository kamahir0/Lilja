using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer.Analysis;

internal static class RepositoryDiagnostics
{
    private const string Category = "Lilja.Repository";

    public static readonly DiagnosticDescriptor EntityMustBePartial = new(
        id: "LILJAREPO001",
        title: "Entity must be partial",
        messageFormat: "Entity '{0}' must be declared as partial.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenericEntityIsNotSupported = new(
        id: "LILJAREPO002",
        title: "Generic entity is not supported",
        messageFormat: "Entity '{0}' must not be generic.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StaticAnnotatedMember = new(
        id: "LILJAREPO003",
        title: "Static member is not supported",
        messageFormat: "Member '{0}' must not be static when using [Persist] or [Key].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyMustBeAutoProperty = new(
        id: "LILJAREPO004",
        title: "Only auto-properties are supported",
        messageFormat: "Property '{0}' must be an instance auto-property without custom accessors or indexers.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicatePersistIndex = new(
        id: "LILJAREPO005",
        title: "Persist index must be unique",
        messageFormat: "Persist index '{0}' is duplicated on entity '{1}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PersistedEntityKeyMustAlsoPersist = new(
        id: "LILJAREPO006",
        title: "Persisted keys must also be persisted",
        messageFormat: "Member '{0}' is marked with [Key] and must also be marked with [Persist] when the entity is persisted.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidToPrimitive = new(
        id: "LILJAREPO007",
        title: "Invalid [ToPrimitive] definition",
        messageFormat: "Value object type '{0}' must declare exactly one instance [ToPrimitive] method without parameters.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidFromPrimitive = new(
        id: "LILJAREPO008",
        title: "Invalid [FromPrimitive] definition",
        messageFormat: "Value object type '{0}' must declare exactly one [FromPrimitive] static factory or constructor that matches the [ToPrimitive] shape.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
