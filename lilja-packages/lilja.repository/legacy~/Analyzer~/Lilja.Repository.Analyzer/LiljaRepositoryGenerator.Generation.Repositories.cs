using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// 検証済みのエンティティモデルに必要な生成ソースファイルをすべて出力します。
    /// </summary>
    /// <param name="context">Roslyn のソース生成コンテキスト。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <param name="hasMessagePack">コンパイル内で MessagePack サポートが利用可能かどうか。</param>
    private static void EmitEntity(SourceProductionContext context, EntityModel model, bool hasMessagePack)
    {
        AddSource(context, model, $"I{model.EntityName}Repository.g.cs", GenerateInterface(model));
        AddSource(context, model, $"InMemory{model.EntityName}Repository.g.cs", GenerateInMemoryRepository(model));
        if (model.IsKeyed)
        {
            AddSource(context, model, $"{model.EntityName}.KeyAccessor.g.cs", GenerateKeyAccessorPartial(model));
        }

        if (!model.IsPersisted)
        {
            return;
        }

        AddSource(context, model, $"{model.EntityName}Dto.g.cs", GenerateDto(model));
        AddSource(context, model, $"{model.EntityName}StorageEnvelope.g.cs", GenerateStorageEnvelope(model));
        AddSource(context, model, $"Json{model.EntityName}Repository.g.cs", GenerateJsonRepository(model));
        AddSource(context, model, $"{model.EntityName}.Converter.g.cs", GenerateConverterPartial(model));

        if (!hasMessagePack)
        {
            return;
        }

        AddSource(context, model, $"{model.EntityName}DtoFormatter.g.cs", GenerateDtoFormatter(model));
        AddSource(context, model, $"{model.EntityName}StorageEnvelopeFormatter.g.cs", GenerateStorageEnvelopeFormatter(model));
        AddSource(context, model, $"MessagePack{model.EntityName}Repository.g.cs", GenerateMessagePackRepository(model));
    }

    /// <summary>
    /// 必要に応じて名前空間を含むヒント名を使って、生成ソースファイルを 1 つ追加します。
    /// </summary>
    /// <param name="context">Roslyn のソース生成コンテキスト。</param>
    /// <param name="model">生成ファイルを所有するエンティティモデル。</param>
    /// <param name="fileName">生成されるファイル名。</param>
    /// <param name="source">生成されたソーステキスト。</param>
    private static void AddSource(SourceProductionContext context, EntityModel model, string fileName, string source)
    {
        var hintName = string.IsNullOrEmpty(model.NamespaceName) ? fileName : model.StorageIdentifier + "." + fileName;
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// エンティティに対して公開されるリポジトリインターフェイスを生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateInterface(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        var namespaceBodyStart = AppendNamespaceStart(sb, model.RepositoryNamespace);
        sb.Append("public interface I").Append(model.EntityName).AppendLine("Repository");
        sb.AppendLine("{");
        sb.AppendLine("    global::Cysharp.Threading.Tasks.UniTask InitializeAsync(global::System.Threading.CancellationToken ct = default);");
        if (model.IsKeyed)
        {
            sb.Append("    ").Append(model.EntityTypeName).Append("? Read(global::Lilja.Repository.IReadOnlyTx tx, ").Append(model.KeyTypeName).AppendLine(" key);");
            sb.Append("    void Create(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.Append("    void Update(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.Append("    void Delete(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.KeyTypeName).AppendLine(" key);");
            sb.Append("    global::System.Collections.Generic.IReadOnlyList<").Append(model.EntityTypeName).AppendLine("> All(global::Lilja.Repository.IReadOnlyTx tx);");
        }
        else
        {
            sb.Append("    ").Append(model.EntityTypeName).AppendLine("? Read(global::Lilja.Repository.IReadOnlyTx tx);");
            sb.Append("    void Create(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.Append("    void Update(global::Lilja.Repository.IReadWriteTx tx, ").Append(model.EntityTypeName).AppendLine(" entity);");
            sb.AppendLine("    void Delete(global::Lilja.Repository.IReadWriteTx tx);");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.RepositoryNamespace, namespaceBodyStart);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティ用のインメモリリポジトリ実装を生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateInMemoryRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        var namespaceBodyStart = AppendNamespaceStart(sb, model.RepositoryNamespace);
        if (model.IsKeyed)
        {
            sb.Append("public sealed class InMemory").Append(model.EntityName).Append("Repository : global::Lilja.Repository.InMemoryKeyedRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.KeyTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendTrackerConstructor(sb, $"InMemory{model.EntityName}Repository", "InMemory");
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKey(").Append(model.EntityTypeName).AppendLine(" entity)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".GetKey(entity);");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }
        else
        {
            sb.Append("public sealed class InMemory").Append(model.EntityName).Append("Repository : global::Lilja.Repository.InMemorySingletonRepositoryBase<")
                .Append(model.EntityTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendTrackerConstructor(sb, $"InMemory{model.EntityName}Repository", "InMemory");
            sb.AppendLine("}");
        }

        AppendNamespaceEnd(sb, model.RepositoryNamespace, namespaceBodyStart);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティ用の JSON バックエンドのリポジトリ実装を生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateJsonRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        var namespaceBodyStart = AppendNamespaceStart(sb, model.RepositoryNamespace);
        if (model.IsKeyed)
        {
            sb.Append("public sealed class Json").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedKeyedRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I")
                .Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendPersistedConstructor(sb, $"Json{model.EntityName}Repository", "json", "Json", model.StorageIdentifier);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".GetKeyFromDto(dto);");
            sb.AppendLine("    }");
            AppendJsonKeyedLoadSave(sb, model);
            sb.AppendLine("}");
        }
        else
        {
            sb.Append("public sealed class Json").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedSingletonRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            AppendPersistedConstructor(sb, $"Json{model.EntityName}Repository", "json", "Json", model.StorageIdentifier);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            AppendJsonSingletonLoadSave(sb, model);
            sb.AppendLine("}");
        }

        AppendNamespaceEnd(sb, model.RepositoryNamespace, namespaceBodyStart);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティ用の MessagePack バックエンドのリポジトリ実装を生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateMessagePackRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        var namespaceBodyStart = AppendNamespaceStart(sb, model.RepositoryNamespace);
        if (model.IsKeyed)
        {
            sb.Append("public sealed class MessagePack").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedKeyedRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.KeyTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I")
                .Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly global::MessagePack.MessagePackSerializerOptions _options;");
            AppendMessagePackConstructor(sb, $"MessagePack{model.EntityName}Repository", model);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            sb.Append("    protected override ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".GetKeyFromDto(dto);");
            sb.AppendLine("    }");
            AppendMessagePackKeyedLoadSave(sb, model);
            sb.AppendLine("}");
        }
        else
        {
            sb.Append("public sealed class MessagePack").Append(model.EntityName).Append("Repository : global::Lilja.Repository.PersistedSingletonRepositoryBase<")
                .Append(model.EntityTypeName).Append(", ").Append(model.DtoTypeName).Append(">, I").Append(model.EntityName).AppendLine("Repository");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly global::MessagePack.MessagePackSerializerOptions _options;");
            AppendMessagePackConstructor(sb, $"MessagePack{model.EntityName}Repository", model);
            AppendToDtoOverride(sb, model);
            AppendFromDtoOverride(sb, model);
            AppendMessagePackSingletonLoadSave(sb, model);
            sb.AppendLine("}");
        }

        AppendNamespaceEnd(sb, model.RepositoryNamespace, namespaceBodyStart);
        return sb.ToString();
    }

    /// <summary>
    /// 生成されるインメモリリポジトリで使う引数なしコンストラクタを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="typeName">生成されるリポジトリ型名。</param>
    /// <param name="repositoryType">エディター診断用のリポジトリ種別 enum メンバー。</param>
    private static void AppendTrackerConstructor(StringBuilder sb, string typeName, string repositoryType)
    {
        sb.Append("    public ").Append(typeName).AppendLine("()");
        sb.AppendLine("    {");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.Append("        global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.")
            .Append(repositoryType).AppendLine(");");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// 生成される永続化リポジトリで使う引数なしコンストラクタを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="typeName">生成されるリポジトリ型名。</param>
    /// <param name="extension">リポジトリが使うファイル拡張子。</param>
    /// <param name="repositoryType">エディター診断用のリポジトリ種別 enum メンバー。</param>
    /// <param name="storageIdentifier">ファイル名に使う保存識別子。</param>
    private static void AppendPersistedConstructor(
        StringBuilder sb,
        string typeName,
        string extension,
        string repositoryType,
        string storageIdentifier)
    {
        sb.Append("    public ").Append(typeName).AppendLine("()");
        sb.Append("        : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(storageIdentifier).Append('.')
            .Append(extension).AppendLine("\"))");
        sb.AppendLine("    {");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.Append("        global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.")
            .Append(repositoryType).AppendLine(");");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成される MessagePack リポジトリで使うコンストラクタとリゾルバー設定を追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="typeName">生成されるリポジトリ型名。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendMessagePackConstructor(StringBuilder sb, string typeName, EntityModel model)
    {
        sb.Append("    public ").Append(typeName).AppendLine("()");
        sb.Append("        : base(global::System.IO.Path.Combine(global::UnityEngine.Application.persistentDataPath, \"").Append(model.StorageIdentifier).AppendLine(".msgpack\"))");
        sb.AppendLine("    {");
        sb.AppendLine("        var resolver = global::MessagePack.Resolvers.CompositeResolver.Create(");
        sb.AppendLine("            new global::MessagePack.Formatters.IMessagePackFormatter[]");
        sb.AppendLine("            {");
        sb.Append("                new global::").Append(model.FormatterNamespace).Append('.').Append(model.StorageEnvelopeFormatterTypeNameWithoutNamespace).AppendLine("(),");
        sb.Append("                new global::").Append(model.FormatterNamespace).Append('.').Append(model.DtoFormatterTypeNameWithoutNamespace).AppendLine("(),");
        sb.AppendLine("            },");
        sb.AppendLine("            new global::MessagePack.IFormatterResolver[]");
        sb.AppendLine("            {");
        sb.AppendLine("                global::MessagePack.Resolvers.StandardResolver.Instance,");
        sb.AppendLine("            });");
        sb.AppendLine("        _options = global::MessagePack.MessagePackSerializerOptions.Standard.WithResolver(resolver);");
        sb.AppendLine("#if UNITY_EDITOR");
        sb.AppendLine("        global::Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, global::Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack);");
        sb.AppendLine("#endif");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成されたエンティティヘルパーへ委譲する <c>ToDto</c> オーバーライドを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendToDtoOverride(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".ToDto(entity);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// 生成されたエンティティヘルパーへ委譲する <c>FromDto</c> オーバーライドを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendFromDtoOverride(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override ").Append(model.EntityTypeName).Append(" FromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".FromDto(dto);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// キー付きリポジトリ向けの JSON 読み込み・保存オーバーライドを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendJsonKeyedLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine(">?> LoadItemsAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var raw = global::System.IO.File.ReadAllText(FilePath);");
        sb.AppendLine("            if (string.IsNullOrWhiteSpace(raw))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.Append("            var envelope = global::UnityEngine.JsonUtility.FromJson<").Append(model.StorageEnvelopeTypeName).AppendLine(">(raw);");
        sb.Append("            return (global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName).AppendLine(">?)envelope?.Items;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine("> items, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.Append("                Items = new global::System.Collections.Generic.List<").Append(model.DtoTypeName).AppendLine(">(items),");
        sb.AppendLine("            };");
        sb.AppendLine("            var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// シングルトンリポジトリ向けの JSON 読み込み・保存オーバーライドを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendJsonSingletonLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<").Append(model.DtoTypeName)
            .AppendLine("?> LoadValueAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var raw = global::System.IO.File.ReadAllText(FilePath);");
        sb.AppendLine("            if (string.IsNullOrWhiteSpace(raw))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.Append("            var envelope = global::UnityEngine.JsonUtility.FromJson<").Append(model.StorageEnvelopeTypeName).AppendLine(">(raw);");
        sb.AppendLine("            if (envelope is null || !envelope.HasValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            return envelope.Item;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveValueAsync(").Append(model.DtoTypeName)
            .AppendLine("? value, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.AppendLine("                HasValue = value is not null,");
        sb.AppendLine("                Item = value,");
        sb.AppendLine("            };");
        sb.AppendLine("            var json = global::UnityEngine.JsonUtility.ToJson(envelope, false);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllText(FilePath, json);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// キー付きリポジトリ向けの MessagePack 読み込み・保存オーバーライドを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendMessagePackKeyedLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine(">?> LoadItemsAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var bytes = global::System.IO.File.ReadAllBytes(FilePath);");
        sb.Append("            var envelope = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.StorageEnvelopeTypeName).AppendLine(">(bytes, _options);");
        sb.Append("            return (global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName).AppendLine(">?)envelope?.Items;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveItemsAsync(global::System.Collections.Generic.IReadOnlyList<").Append(model.DtoTypeName)
            .AppendLine("> items, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.Append("                Items = new global::System.Collections.Generic.List<").Append(model.DtoTypeName).AppendLine(">(items),");
        sb.AppendLine("            };");
        sb.AppendLine("            var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// シングルトンリポジトリ向けの MessagePack 読み込み・保存オーバーライドを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="model">解析済みのエンティティモデル。</param>
    private static void AppendMessagePackSingletonLoadSave(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask<").Append(model.DtoTypeName)
            .AppendLine("?> LoadValueAsync(global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.AppendLine("            if (!global::System.IO.File.Exists(FilePath))");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            var bytes = global::System.IO.File.ReadAllBytes(FilePath);");
        sb.Append("            var envelope = global::MessagePack.MessagePackSerializer.Deserialize<").Append(model.StorageEnvelopeTypeName).AppendLine(">(bytes, _options);");
        sb.AppendLine("            if (envelope is null || !envelope.HasValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine("            return envelope.Item;");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    protected override global::Cysharp.Threading.Tasks.UniTask SaveValueAsync(").Append(model.DtoTypeName)
            .AppendLine("? value, global::System.Threading.CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        return global::Cysharp.Threading.Tasks.UniTask.RunOnThreadPool(() =>");
        sb.AppendLine("        {");
        sb.AppendLine("            ct.ThrowIfCancellationRequested();");
        sb.Append("            var envelope = new ").Append(model.StorageEnvelopeTypeName).AppendLine();
        sb.AppendLine("            {");
        sb.AppendLine("                HasValue = value is not null,");
        sb.AppendLine("                Item = value,");
        sb.AppendLine("            };");
        sb.AppendLine("            var bytes = global::MessagePack.MessagePackSerializer.Serialize(envelope, _options);");
        sb.AppendLine("            global::Lilja.Repository.AtomicFileWriter.WriteAllBytes(FilePath, bytes);");
        sb.AppendLine("        }, cancellationToken: ct);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// 共通の自動生成ファイルヘッダーで初期化されたソースビルダーを作成します。
    /// </summary>
    /// <returns>初期化済みのソースビルダー。</returns>
    private static StringBuilder CreateSourceBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        return sb;
    }

    /// <summary>
    /// 生成型がグローバル名前空間に属さない場合に、名前空間宣言を追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="namespaceName">開く名前空間。</param>
    private static int AppendNamespaceStart(StringBuilder sb, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return -1;
        }

        sb.Append("namespace ").Append(namespaceName).AppendLine();
        sb.AppendLine("{");
        return sb.Length;
    }

    /// <summary>
    /// 先に開いた名前空間宣言に対応する閉じ波かっこを追記します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="namespaceName">閉じる対象の名前空間。</param>
    /// <param name="namespaceBodyStart">名前空間の本文が始まる位置。</param>
    private static void AppendNamespaceEnd(StringBuilder sb, string namespaceName, int namespaceBodyStart)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return;
        }

        IndentNonEmptyLines(sb, namespaceBodyStart, sb.Length, "    ");
        sb.AppendLine("}");
    }

    /// <summary>
    /// 指定範囲にある空行以外の各行へインデントを付与します。
    /// </summary>
    /// <param name="sb">出力先のソースビルダー。</param>
    /// <param name="start">インデント対象範囲の開始位置。</param>
    /// <param name="end">インデント対象範囲の終了位置。</param>
    /// <param name="indent">付与するインデント文字列。</param>
    private static void IndentNonEmptyLines(StringBuilder sb, int start, int end, string indent)
    {
        var index = start;
        while (index < end)
        {
            var lineEnd = index;
            while (lineEnd < end && sb[lineEnd] != '\r' && sb[lineEnd] != '\n')
            {
                lineEnd++;
            }

            var hasContent = false;
            for (var i = index; i < lineEnd; i++)
            {
                if (!char.IsWhiteSpace(sb[i]))
                {
                    hasContent = true;
                    break;
                }
            }

            if (hasContent && !IsPreprocessorDirectiveLine(sb, index, lineEnd))
            {
                sb.Insert(index, indent);
                end += indent.Length;
                lineEnd += indent.Length;
            }

            if (lineEnd < end && sb[lineEnd] == '\r')
            {
                lineEnd++;
            }

            if (lineEnd < end && sb[lineEnd] == '\n')
            {
                lineEnd++;
            }

            index = lineEnd;
        }
    }

    /// <summary>
    /// 指定行がプリプロセッサディレクティブかどうかを判定します。
    /// </summary>
    /// <param name="sb">確認対象のソースビルダー。</param>
    /// <param name="lineStart">確認する行の開始位置。</param>
    /// <param name="lineEnd">確認する行の終了位置。</param>
    /// <returns>行頭空白を除いた最初の文字が <c>#</c> なら <see langword="true"/>。</returns>
    private static bool IsPreprocessorDirectiveLine(StringBuilder sb, int lineStart, int lineEnd)
    {
        for (var i = lineStart; i < lineEnd; i++)
        {
            if (char.IsWhiteSpace(sb[i]))
            {
                continue;
            }

            return sb[i] == '#';
        }

        return false;
    }
}
