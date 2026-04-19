using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// Repository interface と backend 別 emitter のルータ。
/// </summary>
internal static class RepositoryEmitter
{
    public static string EmitInterface(EntityInfo entity)
    {
        var builder = new StringBuilder().Append($@"#nullable enable

namespace {entity.RepositoryNamespace}
{{
    /// <summary>
    /// {entity.ClassName}リポジトリのI/F。
    /// </summary>
    public interface I{entity.ClassName}Repository
    {{
        global::Cysharp.Threading.Tasks.UniTask InitializeAsync(global::System.Threading.CancellationToken ct = default);
");

        if (entity.HasKey)
        {
            var keyTypeName = EmitterSupport.GetKeyTypeName(entity);
            var keyParamName = EmitterSupport.GetKeyParamName(entity);
            builder.Append($@"        {entity.FullTypeName}? Read(global::Lilja.Repository.IReadOnlyTx tx, {keyTypeName} {keyParamName});
        void Create(global::Lilja.Repository.IReadWriteTx tx, {entity.FullTypeName} entity);
        void Update(global::Lilja.Repository.IReadWriteTx tx, {entity.FullTypeName} entity);
        void Delete(global::Lilja.Repository.IReadWriteTx tx, {keyTypeName} {keyParamName});
        global::System.Collections.Generic.IReadOnlyList<{entity.FullTypeName}> All(global::Lilja.Repository.IReadOnlyTx tx);
");
        }
        else
        {
            builder.Append($@"        {entity.FullTypeName}? Read(global::Lilja.Repository.IReadOnlyTx tx);
        void Create(global::Lilja.Repository.IReadWriteTx tx, {entity.FullTypeName} entity);
        void Update(global::Lilja.Repository.IReadWriteTx tx, {entity.FullTypeName} entity);
        void Delete(global::Lilja.Repository.IReadWriteTx tx);
");
        }

        return builder.Append(
@"    }
}
").ToString();
    }

    public static string EmitInMemoryImplementation(EntityInfo entity)
    {
        return InMemoryRepositoryEmitter.Emit(entity);
    }

    public static string EmitJsonImplementation(EntityInfo entity)
    {
        return JsonRepositoryEmitter.Emit(entity);
    }

    public static string EmitMessagePackImplementation(EntityInfo entity)
    {
        return MessagePackRepositoryEmitter.Emit(entity);
    }
}
