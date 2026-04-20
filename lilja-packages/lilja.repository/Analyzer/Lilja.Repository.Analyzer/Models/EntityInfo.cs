using System.Collections.Generic;

namespace Lilja.Repository.Analyzer.Models;

/// <summary>
/// Entity情報。
/// </summary>
internal readonly struct EntityInfo
{
    public string Namespace { get; }

    public string ClassName { get; }

    public string FullTypeName { get; }

    public string StorageIdentifier =>
        string.IsNullOrEmpty(Namespace)
            ? ClassName
            : $"{Namespace}.{ClassName}";

    public IReadOnlyList<EntityMemberInfo> PersistMembers { get; }

    public IReadOnlyList<EntityMemberInfo> KeyMembers { get; }

    public bool HasKey => KeyMembers.Count > 0;

    public bool IsCompositeKey => KeyMembers.Count > 1;

    public bool HasPersistMembers => PersistMembers.Count > 0;

    public bool NeedsConstructorGeneration { get; }

    public string DtoNamespace =>
        string.IsNullOrEmpty(Namespace)
            ? "Lilja.Repository.Generated.Dtos"
            : $"Lilja.Repository.Generated.Dtos.{Namespace}";

    public string DtoTypeName => $"{DtoNamespace}.{ClassName}Dto";

    public string FormatterNamespace =>
        string.IsNullOrEmpty(Namespace)
            ? "Lilja.Repository.Generated.Formatters"
            : $"Lilja.Repository.Generated.Formatters.{Namespace}";

    public string FormatterTypeName => $"{FormatterNamespace}.{ClassName}DtoFormatter";

    public string StorageNamespace =>
        string.IsNullOrEmpty(Namespace)
            ? "Lilja.Repository.Generated.Storage"
            : $"Lilja.Repository.Generated.Storage.{Namespace}";

    public string StorageEnvelopeTypeName => $"{StorageNamespace}.{ClassName}StorageEnvelope";

    public string StorageEnvelopeFormatterTypeName => $"{FormatterNamespace}.{ClassName}StorageEnvelopeFormatter";

    public string RepositoryNamespace =>
        string.IsNullOrEmpty(Namespace)
            ? "Repositories"
            : $"{Namespace}.Repositories";

    public EntityInfo(
        string @namespace,
        string className,
        string fullTypeName,
        IReadOnlyList<EntityMemberInfo> persistMembers,
        IReadOnlyList<EntityMemberInfo> keyMembers,
        bool needsConstructorGeneration)
    {
        Namespace = @namespace;
        ClassName = className;
        FullTypeName = fullTypeName;
        PersistMembers = persistMembers;
        KeyMembers = keyMembers;
        NeedsConstructorGeneration = needsConstructorGeneration;
    }
}
