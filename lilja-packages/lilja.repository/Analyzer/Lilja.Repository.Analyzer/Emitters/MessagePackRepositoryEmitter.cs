using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

internal static class MessagePackRepositoryEmitter
{
    public static string Emit(EntityInfo entity)
    {
        return entity.HasKey
            ? EmitKeyed(entity)
            : EmitSingleton(entity);
    }

    private static string EmitKeyed(EntityInfo entity)
    {
        var repositoryClassName = $"MessagePack{entity.ClassName}Repository";
        var keyTypeName = EmitterSupport.GetKeyTypeName(entity);
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var formatterTypeName = EmitterSupport.Qualify(entity.FormatterTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);
        var envelopeFormatterTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeFormatterTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            repositoryClassName,
            $"global::Lilja.Repository.PersistedKeyedRepositoryBase<{entity.FullTypeName}, {keyTypeName}, {dtoTypeName}>");
        builder.Append(
@"        private readonly global::MessagePack.MessagePackSerializerOptions _options;

");
        AppendConstructor(builder, entity, repositoryClassName, formatterTypeName, envelopeFormatterTypeName);
        builder.AppendLine();
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
        var repositoryClassName = $"MessagePack{entity.ClassName}Repository";
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var formatterTypeName = EmitterSupport.Qualify(entity.FormatterTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);
        var envelopeFormatterTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeFormatterTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            repositoryClassName,
            $"global::Lilja.Repository.PersistedSingletonRepositoryBase<{entity.FullTypeName}, {dtoTypeName}>");
        builder.Append(
@"        private readonly global::MessagePack.MessagePackSerializerOptions _options;

");
        AppendConstructor(builder, entity, repositoryClassName, formatterTypeName, envelopeFormatterTypeName);
        builder.AppendLine();
        PersistedRepositoryEmitterCommon.AppendSingletonMembers(
            builder,
            entity,
            dtoTypeName,
            PersistedRepositoryEmitterCommon.BuildLoadValueMethod(dtoTypeName, envelopeTypeName, BuildDeserializeEnvelopeBody(envelopeTypeName)),
            PersistedRepositoryEmitterCommon.BuildSaveValueMethod(dtoTypeName, envelopeTypeName, BuildSerializeEnvelopeBody()));
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static void AppendConstructor(
        System.Text.StringBuilder builder,
        EntityInfo entity,
        string repositoryClassName,
        string formatterTypeName,
        string envelopeFormatterTypeName)
    {
        PersistedRepositoryEmitterCommon.AppendConstructor(
            builder,
            entity,
            repositoryClassName,
            "msgpack",
            "MessagePack",
            $@"            var resolver = global::MessagePack.Resolvers.CompositeResolver.Create(
                new global::MessagePack.Formatters.IMessagePackFormatter[] {{ new {envelopeFormatterTypeName}(), new {formatterTypeName}() }},
                new global::MessagePack.IFormatterResolver[] {{ global::MessagePack.Resolvers.StandardResolver.Instance }});
            _options = global::MessagePack.MessagePackSerializerOptions.Standard.WithResolver(resolver);");
    }

    private static string BuildDeserializeEnvelopeBody(
        string envelopeTypeName)
    {
        return $@"var bytes = global::System.IO.File.ReadAllBytes(FilePath);
return global::MessagePack.MessagePackSerializer.Deserialize<{envelopeTypeName}>(bytes, _options);";
    }

    private static string BuildSerializeEnvelopeBody()
    {
        return @"var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);
global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);";
    }
}
