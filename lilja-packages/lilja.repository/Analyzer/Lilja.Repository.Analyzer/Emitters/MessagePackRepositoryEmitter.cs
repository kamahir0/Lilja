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
        var keyTypeName = EmitterSupport.GetKeyTypeName(entity);
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var formatterTypeName = EmitterSupport.Qualify(entity.FormatterTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);
        var envelopeFormatterTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeFormatterTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            $"MessagePack{entity.ClassName}Repository",
            $"global::Lilja.Repository.PersistedKeyedRepositoryBase<{entity.FullTypeName}, {keyTypeName}, {dtoTypeName}>");
        builder.Append(
@"        private readonly global::MessagePack.MessagePackSerializerOptions _options;

");
        AppendConstructor(builder, entity, formatterTypeName, envelopeFormatterTypeName);
        builder.AppendLine();
        AppendPersistedKeyedMembers(builder, entity, dtoTypeName, envelopeTypeName);
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static string EmitSingleton(EntityInfo entity)
    {
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var formatterTypeName = EmitterSupport.Qualify(entity.FormatterTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);
        var envelopeFormatterTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeFormatterTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            $"MessagePack{entity.ClassName}Repository",
            $"global::Lilja.Repository.PersistedSingletonRepositoryBase<{entity.FullTypeName}, {dtoTypeName}>");
        builder.Append(
@"        private readonly global::MessagePack.MessagePackSerializerOptions _options;

");
        AppendConstructor(builder, entity, formatterTypeName, envelopeFormatterTypeName);
        builder.AppendLine();
        AppendPersistedSingletonMembers(builder, entity, dtoTypeName, envelopeTypeName);
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static void AppendConstructor(
        System.Text.StringBuilder builder,
        EntityInfo entity,
        string formatterTypeName,
        string envelopeFormatterTypeName)
    {
        builder.Append($@"        public MessagePack{entity.ClassName}Repository()
            : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, ""{entity.StorageIdentifier}.msgpack""))
        {{
#if UNITY_EDITOR
            TrackRepository(global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack);
#endif
            var resolver = global::MessagePack.Resolvers.CompositeResolver.Create(
                new global::MessagePack.Formatters.IMessagePackFormatter[] {{ new {envelopeFormatterTypeName}(), new {formatterTypeName}() }},
                new global::MessagePack.IFormatterResolver[] {{ global::MessagePack.Resolvers.StandardResolver.Instance }});
            _options = global::MessagePack.MessagePackSerializerOptions.Standard.WithResolver(resolver);
        }}
");
    }

    private static void AppendPersistedKeyedMembers(
        System.Text.StringBuilder builder,
        EntityInfo entity,
        string dtoTypeName,
        string envelopeTypeName)
    {
        builder.Append($@"        protected override {dtoTypeName} ToDto({entity.FullTypeName} entity)
        {{
            return {entity.ClassName}.ToDto(entity);
        }}

        protected override {entity.FullTypeName} FromDto({dtoTypeName} dto)
        {{
            return {entity.ClassName}.FromDto(dto);
        }}

        protected override {EmitterSupport.GetKeyTypeName(entity)} GetKeyFromDto({dtoTypeName} dto)
        {{
            return {entity.ClassName}.GetKeyFromDto(dto);
        }}

        protected override async global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<{dtoTypeName}>?> LoadItemsAsync(global::System.Threading.CancellationToken ct)
        {{
            ct.ThrowIfCancellationRequested();
            if (!global::System.IO.File.Exists(FilePath))
            {{
                return null;
            }}

            var envelope = await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
                var bytes = global::System.IO.File.ReadAllBytes(FilePath);
                return global::MessagePack.MessagePackSerializer.Deserialize<{envelopeTypeName}>(bytes, _options);
            }});
            return envelope?.Items;
        }}

        protected override async global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<{dtoTypeName}> items, global::System.Threading.CancellationToken ct)
        {{
            ct.ThrowIfCancellationRequested();
            var envelope = new {envelopeTypeName}
            {{
                Items = new global::System.Collections.Generic.List<{dtoTypeName}>(items.Count),
            }};
            envelope.Items.AddRange(items);
            await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
                var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);
                global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);
            }});
        }}
");
    }

    private static void AppendPersistedSingletonMembers(
        System.Text.StringBuilder builder,
        EntityInfo entity,
        string dtoTypeName,
        string envelopeTypeName)
    {
        builder.Append($@"        protected override {dtoTypeName} ToDto({entity.FullTypeName} entity)
        {{
            return {entity.ClassName}.ToDto(entity);
        }}

        protected override {entity.FullTypeName} FromDto({dtoTypeName} dto)
        {{
            return {entity.ClassName}.FromDto(dto);
        }}

        protected override async global::Cysharp.Threading.Tasks.UniTask<{dtoTypeName}?> LoadValueAsync(global::System.Threading.CancellationToken ct)
        {{
            ct.ThrowIfCancellationRequested();
            if (!global::System.IO.File.Exists(FilePath))
            {{
                return null;
            }}

            var envelope = await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
                var bytes = global::System.IO.File.ReadAllBytes(FilePath);
                return global::MessagePack.MessagePackSerializer.Deserialize<{envelopeTypeName}>(bytes, _options);
            }});
            return envelope is not null && envelope.HasValue ? envelope.Item : null;
        }}

        protected override async global::Cysharp.Threading.Tasks.UniTask SaveValueAsync({dtoTypeName}? value, global::System.Threading.CancellationToken ct)
        {{
            ct.ThrowIfCancellationRequested();
            var envelope = new {envelopeTypeName}
            {{
                HasValue = value is not null,
                Item = value,
            }};
            await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
                var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);
                global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);
            }});
        }}
");
    }
}
