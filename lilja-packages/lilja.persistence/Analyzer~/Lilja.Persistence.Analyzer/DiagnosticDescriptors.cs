using Microsoft.CodeAnalysis;

namespace Lilja.Persistence.Analyzer;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor PersistableMustBePartial = new DiagnosticDescriptor(
        "LILJAPERSIST001",
        "Persistable must be partial",
        "Persistable type must be partial",
        "Lilja.Persistence",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor UnsupportedMember = new DiagnosticDescriptor(
        "LILJAPERSIST002",
        "Persist member is unsupported",
        "Persist member '{0}' is unsupported: {1}",
        "Lilja.Persistence",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor PersistIndexMustBeUnique = new DiagnosticDescriptor(
        "LILJAPERSIST003",
        "Persist index must be unique",
        "Persist index '{0}' is duplicated",
        "Lilja.Persistence",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor PersistIndexMustBeNonNegative = new DiagnosticDescriptor(
        "LILJAPERSIST004",
        "Persist index must be non-negative",
        "Persist index must be non-negative",
        "Lilja.Persistence",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor KeyMustBePersisted = new DiagnosticDescriptor(
        "LILJAPERSIST005",
        "Key must be persisted",
        "Key member '{0}' must also be marked with [Persist]",
        "Lilja.Persistence",
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor RootMustNotBeKeyed = new DiagnosticDescriptor(
        "LILJAPERSIST006",
        "Root persistable must not be keyed",
        "Root persistable type must not declare [Key] members",
        "Lilja.Persistence",
        DiagnosticSeverity.Error,
        true);
}
