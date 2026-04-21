using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

internal static class PersistedRepositoryEmitterCommon
{
    public static void AppendConstructor(
        StringBuilder builder,
        EntityInfo entity,
        string repositoryClassName,
        string fileExtension,
        string repositoryTypeName,
        string? additionalConstructorBody = null)
    {
        builder.Append($@"        public {repositoryClassName}()
            : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, ""{entity.StorageIdentifier}.{fileExtension}""))
        {{
#if UNITY_EDITOR
            TrackRepository(global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.{repositoryTypeName});
#endif
");

        if (!string.IsNullOrWhiteSpace(additionalConstructorBody))
        {
            builder.Append(additionalConstructorBody);
            if (!additionalConstructorBody!.EndsWith("\n", System.StringComparison.Ordinal))
            {
                builder.AppendLine();
            }
        }

        builder.Append(
@"        }

");
    }

    public static void AppendKeyedMembers(
        StringBuilder builder,
        EntityInfo entity,
        string dtoTypeName,
        string loadItemsMethod,
        string saveItemsMethod)
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

");
        builder.Append(loadItemsMethod);
        builder.AppendLine();
        builder.Append(saveItemsMethod);
    }

    public static void AppendSingletonMembers(
        StringBuilder builder,
        EntityInfo entity,
        string dtoTypeName,
        string loadValueMethod,
        string saveValueMethod)
    {
        builder.Append($@"        protected override {dtoTypeName} ToDto({entity.FullTypeName} entity)
        {{
            return {entity.ClassName}.ToDto(entity);
        }}

        protected override {entity.FullTypeName} FromDto({dtoTypeName} dto)
        {{
            return {entity.ClassName}.FromDto(dto);
        }}

");
        builder.Append(loadValueMethod);
        builder.AppendLine();
        builder.Append(saveValueMethod);
    }

    public static string BuildLoadItemsMethod(
        string dtoTypeName,
        string envelopeTypeName,
        string deserializeEnvelopeBody)
    {
        return $@"        protected override async global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<{dtoTypeName}>?> LoadItemsAsync(global::System.Threading.CancellationToken ct)
        {{
            ct.ThrowIfCancellationRequested();
            if (!global::System.IO.File.Exists(FilePath))
            {{
                return null;
            }}

            var envelope = await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
{IndentBlock(deserializeEnvelopeBody, 16)}
            }});
            return envelope?.Items;
        }}";
    }

    public static string BuildSaveItemsMethod(
        string dtoTypeName,
        string envelopeTypeName,
        string serializeEnvelopeBody)
    {
        return $@"        protected override global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<{dtoTypeName}> items, global::System.Threading.CancellationToken ct)
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
{IndentBlock(serializeEnvelopeBody, 16)}
            }});
        }}";
    }

    public static string BuildLoadValueMethod(
        string dtoTypeName,
        string envelopeTypeName,
        string deserializeEnvelopeBody)
    {
        return $@"        protected override async global::Cysharp.Threading.Tasks.UniTask<{dtoTypeName}?> LoadValueAsync(global::System.Threading.CancellationToken ct)
        {{
            ct.ThrowIfCancellationRequested();
            if (!global::System.IO.File.Exists(FilePath))
            {{
                return null;
            }}

            var envelope = await global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>
            {{
                ct.ThrowIfCancellationRequested();
{IndentBlock(deserializeEnvelopeBody, 16)}
            }});
            return envelope is not null && envelope.HasValue ? envelope.Item : null;
        }}";
    }

    public static string BuildSaveValueMethod(
        string dtoTypeName,
        string envelopeTypeName,
        string serializeEnvelopeBody)
    {
        return $@"        protected override global::Cysharp.Threading.Tasks.UniTask SaveValueAsync({dtoTypeName}? value, global::System.Threading.CancellationToken ct)
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
{IndentBlock(serializeEnvelopeBody, 16)}
            }});
        }}";
    }

    private static string IndentBlock(string source, int spaces)
    {
        var indent = new string(' ', spaces);
        var normalized = source.Replace("\r\n", "\n").TrimEnd('\n', '\r');
        return indent + normalized.Replace("\n", "\n" + indent);
    }
}
