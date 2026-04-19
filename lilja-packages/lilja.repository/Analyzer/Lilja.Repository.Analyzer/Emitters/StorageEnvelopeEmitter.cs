using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// 永続化 envelope 生成。
/// </summary>
internal static class StorageEnvelopeEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var members = entity.HasKey
            ? $"        public global::System.Collections.Generic.List<{dtoTypeName}> Items = new global::System.Collections.Generic.List<{dtoTypeName}>();"
            : $$"""
        public bool HasValue;
        public {{dtoTypeName}}? Item;
""";

        return $$"""
#nullable enable

namespace {{entity.StorageNamespace}}
{
    [global::System.Serializable]
    internal sealed class {{entity.ClassName}}StorageEnvelope
    {
{{members}}
    }
}
""";
    }
}
