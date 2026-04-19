using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

internal static class InMemoryRepositoryEmitter
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
        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            $"InMemory{entity.ClassName}Repository",
            $"global::Lilja.Repository.InMemoryKeyedRepositoryBase<{entity.FullTypeName}, {keyTypeName}>");
        builder.Append($@"        public InMemory{entity.ClassName}Repository()
        {{
#if UNITY_EDITOR
            TrackRepository(global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory);
#endif
        }}

        protected override {keyTypeName} GetKey({entity.FullTypeName} entity)
        {{
            return {entity.ClassName}.GetKey(entity);
        }}
");
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }

    private static string EmitSingleton(EntityInfo entity)
    {
        var builder = RepositoryEmitterCommon.BeginRepositoryClass(
            entity,
            $"InMemory{entity.ClassName}Repository",
            $"global::Lilja.Repository.InMemorySingletonRepositoryBase<{entity.FullTypeName}>");
        builder.Append($@"        public InMemory{entity.ClassName}Repository()
        {{
#if UNITY_EDITOR
            TrackRepository(global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory);
#endif
        }}
");
        return RepositoryEmitterCommon.EndRepositoryClass(builder);
    }
}
