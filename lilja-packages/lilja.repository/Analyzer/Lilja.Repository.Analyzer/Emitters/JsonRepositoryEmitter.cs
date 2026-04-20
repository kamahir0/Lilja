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
        var keyTypeName = EmitterSupport.GetKeyTypeName(entity);
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            $"Json{entity.ClassName}Repository",
            $"global::Lilja.Repository.PersistedKeyedRepositoryBase<{entity.FullTypeName}, {keyTypeName}, {dtoTypeName}>");
        builder.Append($@"        public Json{entity.ClassName}Repository()
            : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, ""{entity.StorageIdentifier}.json""))
        {{
#if UNITY_EDITOR
            TrackRepository(global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json);
#endif
        }}

");
        AppendPersistedKeyedMembers(builder, entity, dtoTypeName, envelopeTypeName);
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static string EmitSingleton(EntityInfo entity)
    {
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);

        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            $"Json{entity.ClassName}Repository",
            $"global::Lilja.Repository.PersistedSingletonRepositoryBase<{entity.FullTypeName}, {dtoTypeName}>");
        builder.Append($@"        public Json{entity.ClassName}Repository()
            : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, ""{entity.StorageIdentifier}.json""))
        {{
#if UNITY_EDITOR
            TrackRepository(global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json);
#endif
        }}

");
        AppendPersistedSingletonMembers(builder, entity, dtoTypeName, envelopeTypeName);
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
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
                var json = global::System.IO.File.ReadAllText(FilePath);
                return global::System.String.IsNullOrWhiteSpace(json)
                    ? null
                    : global::UnityEngine.JsonUtility.FromJson<{envelopeTypeName}>(json);
            }});
            return envelope?.Items;
        }}

        protected override global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<{dtoTypeName}> items, global::System.Threading.CancellationToken ct)
        {{
            var envelope = new {envelopeTypeName}
            {{
                Items = new global::System.Collections.Generic.List<{dtoTypeName}>(items.Count),
            }};
            envelope.Items.AddRange(items);
            ct.ThrowIfCancellationRequested();
            return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
                var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);
                global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);
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
                var json = global::System.IO.File.ReadAllText(FilePath);
                return global::System.String.IsNullOrWhiteSpace(json)
                    ? null
                    : global::UnityEngine.JsonUtility.FromJson<{envelopeTypeName}>(json);
            }});
            return envelope is not null && envelope.HasValue ? envelope.Item : null;
        }}

        protected override global::Cysharp.Threading.Tasks.UniTask SaveValueAsync({dtoTypeName}? value, global::System.Threading.CancellationToken ct)
        {{
            var envelope = new {envelopeTypeName}
            {{
                HasValue = value is not null,
                Item = value,
            }};
            ct.ThrowIfCancellationRequested();
            return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
                var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);
                global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);
            }});
        }}
");
    }
}
