using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// Emits every generated source file required for a validated entity model.
    /// </summary>
    /// <param name="context">The Roslyn source production context.</param>
    /// <param name="model">The analyzed entity model.</param>
    /// <param name="hasMessagePack">Whether MessagePack support is available in the compilation.</param>
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
    /// Adds one generated source file using a namespace-qualified hint name when necessary.
    /// </summary>
    /// <param name="context">The Roslyn source production context.</param>
    /// <param name="model">The entity model that owns the generated file.</param>
    /// <param name="fileName">The generated file name.</param>
    /// <param name="source">The generated source text.</param>
    private static void AddSource(SourceProductionContext context, EntityModel model, string fileName, string source)
    {
        var hintName = string.IsNullOrEmpty(model.NamespaceName) ? fileName : model.StorageIdentifier + "." + fileName;
        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    /// Generates the repository interface exposed for an entity.
    /// </summary>
    /// <param name="model">The analyzed entity model.</param>
    /// <returns>The generated source code.</returns>
    private static string GenerateInterface(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
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
        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// Generates the in-memory repository implementation for an entity.
    /// </summary>
    /// <param name="model">The analyzed entity model.</param>
    /// <returns>The generated source code.</returns>
    private static string GenerateInMemoryRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
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

        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// Generates the JSON-backed repository implementation for an entity.
    /// </summary>
    /// <param name="model">The analyzed entity model.</param>
    /// <returns>The generated source code.</returns>
    private static string GenerateJsonRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
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

        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// Generates the MessagePack-backed repository implementation for an entity.
    /// </summary>
    /// <param name="model">The analyzed entity model.</param>
    /// <returns>The generated source code.</returns>
    private static string GenerateMessagePackRepository(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.RepositoryNamespace);
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

        AppendNamespaceEnd(sb, model.RepositoryNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// Appends the parameterless constructor used by generated in-memory repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="typeName">The generated repository type name.</param>
    /// <param name="repositoryType">The editor diagnostics repository type enum member.</param>
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
    /// Appends the parameterless constructor used by generated persisted repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="typeName">The generated repository type name.</param>
    /// <param name="extension">The file extension used by the repository.</param>
    /// <param name="repositoryType">The editor diagnostics repository type enum member.</param>
    /// <param name="storageIdentifier">The storage identifier used for the file name.</param>
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
    /// Appends the constructor and resolver setup used by generated MessagePack repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="typeName">The generated repository type name.</param>
    /// <param name="model">The analyzed entity model.</param>
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
    /// Appends the <c>ToDto</c> override that delegates to the generated entity helper.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="model">The analyzed entity model.</param>
    private static void AppendToDtoOverride(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".ToDto(entity);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the <c>FromDto</c> override that delegates to the generated entity helper.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="model">The analyzed entity model.</param>
    private static void AppendFromDtoOverride(StringBuilder sb, EntityModel model)
    {
        sb.Append("    protected override ").Append(model.EntityTypeName).Append(" FromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(model.EntityTypeName).AppendLine(".FromDto(dto);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends JSON load and save overrides for keyed repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="model">The analyzed entity model.</param>
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
    /// Appends JSON load and save overrides for singleton repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="model">The analyzed entity model.</param>
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
    /// Appends MessagePack load and save overrides for keyed repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="model">The analyzed entity model.</param>
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
    /// Appends MessagePack load and save overrides for singleton repositories.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="model">The analyzed entity model.</param>
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
    /// Creates a source builder initialized with the common auto-generated file header.
    /// </summary>
    /// <returns>The initialized source builder.</returns>
    private static StringBuilder CreateSourceBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        return sb;
    }

    /// <summary>
    /// Appends a namespace declaration when the generated type is not in the global namespace.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="namespaceName">The namespace to open.</param>
    private static void AppendNamespaceStart(StringBuilder sb, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return;
        }

        sb.Append("namespace ").Append(namespaceName).AppendLine();
        sb.AppendLine("{");
    }

    /// <summary>
    /// Appends the closing brace for a previously opened namespace declaration.
    /// </summary>
    /// <param name="sb">The destination source builder.</param>
    /// <param name="namespaceName">The namespace being closed.</param>
    private static void AppendNamespaceEnd(StringBuilder sb, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return;
        }

        sb.AppendLine("}");
    }
}
