using Microsoft.CodeAnalysis;

namespace Lilja.Repository.Analyzer;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor EntityMustBePartial = new DiagnosticDescriptor(
        "LILJAREPO001", "Entity must be partial", "Entity must be partial", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor GenericEntityNotSupported = new DiagnosticDescriptor(
        "LILJAREPO002", "Generic entity is not supported", "Generic entity is not supported", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor UnsupportedMember = new DiagnosticDescriptor(
        "LILJAREPO003", "Unsupported member", "Member '{0}' is not supported: {1}", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor PersistIndexMustBeUnique = new DiagnosticDescriptor(
        "LILJAREPO004", "Persist index must be unique", "Persist index '{0}' is used more than once", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor PersistIndexMustBeNonNegative = new DiagnosticDescriptor(
        "LILJAREPO005", "Persist index must be non-negative", "Persist index must be non-negative", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor KeyMustBePersisted = new DiagnosticDescriptor(
        "LILJAREPO006", "Key must be persisted", "Key member '{0}' must also have [Persist]", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor InvalidToPrimitiveDefinition = new DiagnosticDescriptor(
        "LILJAREPO007", "Invalid [ToPrimitive] definition", "Invalid [ToPrimitive] definition on '{0}'", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor InvalidFromPrimitiveDefinition = new DiagnosticDescriptor(
        "LILJAREPO008", "Invalid [FromPrimitive] definition", "Invalid [FromPrimitive] definition on '{0}'", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor EntityCycleNotSupported = new DiagnosticDescriptor(
        "LILJAREPO009", "Entity cycle is not supported", "Persisted entity graph contains a cycle involving '{0}'", "Lilja.Repository", DiagnosticSeverity.Error, true);

    public static readonly DiagnosticDescriptor MessagePackNotAvailable = new DiagnosticDescriptor(
        "LILJAREPO010", "MessagePack is not available", "MessagePack repository requested for '{0}', but compatible MessagePack types were not found", "Lilja.Repository", DiagnosticSeverity.Warning, true);
}
