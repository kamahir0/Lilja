using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

internal static class JsonRepositoryEmitter
{
    public static string Emit(EntityInfo entity)
    {
        return entity.HasKey
            ? EmitKeyed(entity)
            : EmitSingleton(entity);
    }

    private static string EmitKeyed(EntityInfo entity)
    {
        var repositoryClassName = $"Json{entity.ClassName}Repository";
        var keyTypeName = EmitterSupport.GetKeyTypeName(entity);
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            repositoryClassName,
            $"global::Lilja.Repository.PersistedKeyedRepositoryBase<{entity.FullTypeName}, {keyTypeName}, {dtoTypeName}>");
        PersistedRepositoryEmitterCommon.AppendConstructor(builder, entity, repositoryClassName, "json", "Json");
        PersistedRepositoryEmitterCommon.AppendKeyedMembers(
            builder,
            entity,
            dtoTypeName,
            PersistedRepositoryEmitterCommon.BuildLoadItemsMethod(dtoTypeName, envelopeTypeName, BuildDeserializeEnvelopeBody(envelopeTypeName)),
            PersistedRepositoryEmitterCommon.BuildSaveItemsMethod(dtoTypeName, envelopeTypeName, BuildSerializeEnvelopeBody()));
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static string EmitSingleton(EntityInfo entity)
    {
        var repositoryClassName = $"Json{entity.ClassName}Repository";
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            repositoryClassName,
            $"global::Lilja.Repository.PersistedSingletonRepositoryBase<{entity.FullTypeName}, {dtoTypeName}>");
        PersistedRepositoryEmitterCommon.AppendConstructor(builder, entity, repositoryClassName, "json", "Json");
        PersistedRepositoryEmitterCommon.AppendSingletonMembers(
            builder,
            entity,
            dtoTypeName,
            PersistedRepositoryEmitterCommon.BuildLoadValueMethod(dtoTypeName, envelopeTypeName, BuildDeserializeEnvelopeBody(envelopeTypeName)),
            PersistedRepositoryEmitterCommon.BuildSaveValueMethod(dtoTypeName, envelopeTypeName, BuildSerializeEnvelopeBody()));
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static string BuildDeserializeEnvelopeBody(
        string envelopeTypeName)
    {
        return $@"var json = global::System.IO.File.ReadAllText(FilePath);
return global::System.String.IsNullOrWhiteSpace(json)
    ? null
    : global::UnityEngine.JsonUtility.FromJson<{envelopeTypeName}>(json);";
    }

    private static string BuildSerializeEnvelopeBody()
    {
        return @"var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);
global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);";
    }
}
