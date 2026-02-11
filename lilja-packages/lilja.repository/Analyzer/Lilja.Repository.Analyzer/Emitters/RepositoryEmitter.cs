using System.Collections.Generic;
using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// Repository生成。
/// </summary>
internal static class RepositoryEmitter
{
    #region ヘルパーメソッド

    /// <summary>
    /// キーの型文字列を取得する。
    /// 複合キーの場合はValueTuple形式、単一キーの場合は型名のみ。
    /// </summary>
    private static string GetKeyTypeName(EntityInfo entity)
    {
        if (!entity.HasKey) return string.Empty;

        if (entity.IsCompositeKey)
        {
            // 複合キー: (Type1, Type2, ...)
            var types = new List<string>();
            foreach (var keyField in entity.KeyFields)
            {
                types.Add(keyField.TypeName);
            }
            return $"({string.Join(", ", types)})";
        }
        else
        {
            // 単一キー
            return entity.KeyFields[0].TypeName;
        }
    }

    /// <summary>
    /// キーのパラメータ名を取得する。
    /// 複合キーの場合は"key"固定、単一キーの場合はフィールド名をcamelCase化。
    /// </summary>
    private static string GetKeyParamName(EntityInfo entity)
    {
        if (!entity.HasKey) return string.Empty;

        if (entity.IsCompositeKey)
        {
            return "key";
        }
        else
        {
            return entity.KeyFields[0].Name.ToCamelCase();
        }
    }

    /// <summary>
    /// GetKeyヘルパーメソッドの本体を生成する。
    /// </summary>
    private static string GetKeyReturnExpression(EntityInfo entity, string entityClassName, string dtoVarName)
    {
        if (!entity.HasKey) return string.Empty;

        if (entity.IsCompositeKey)
        {
            // 複合キー: (dto.Key1, dto.Key2, ...)
            var parts = new List<string>();
            foreach (var keyField in entity.KeyFields)
            {
                parts.Add($"{dtoVarName}.{keyField.DtoFieldName}");
            }
            return $"({string.Join(", ", parts)})";
        }
        else
        {
            return $"{dtoVarName}.{entity.KeyFields[0].DtoFieldName}";
        }
    }


    /// <summary>
    /// 非同期版MarkDirtyメソッドを生成する。
    /// Func&lt;UniTask&gt;を直接登録する。
    /// </summary>
    private static void EmitMarkDirtyAsync(StringBuilder sb)
    {
        sb.AppendLine("        private void MarkDirty(IReadWriteTx tx)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_dirty) return;");
        sb.AppendLine("            _dirty = true;");
        sb.AppendLine("            tx.OnCommit(async () => { await SaveAsync(); _dirty = false; });");
        sb.AppendLine("            tx.OnRollback(async () => { await LoadAsync(); _dirty = false; });");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    #endregion

    public static string EmitInterface(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var repoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Repositories"
            : $"{entity.Namespace}.Repositories";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("using Lilja.Repository;");
        sb.AppendLine();
        sb.AppendLine($"namespace {repoNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {entity.ClassName}リポジトリのI/F。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public interface I{entity.ClassName}Repository");
        sb.AppendLine("    {");

        var entityFullName = string.IsNullOrEmpty(entity.Namespace)
            ? entity.ClassName
            : $"{entity.Namespace}.{entity.ClassName}";

        if (entity.HasKey)
        {
            var keyTypeName = GetKeyTypeName(entity);
            var keyParamName = GetKeyParamName(entity);

            // Keyed Entity: CRUD operations
            sb.AppendLine($"        {entityFullName} Read(IReadOnlyTx tx, {keyTypeName} {keyParamName});");
            sb.AppendLine($"        void Create(IReadWriteTx tx, {entityFullName} entity);");
            sb.AppendLine($"        void Update(IReadWriteTx tx, {entityFullName} entity);");
            sb.AppendLine($"        void Delete(IReadWriteTx tx, {keyTypeName} {keyParamName});");
        }
        else
        {
            // Singleton Entity
            sb.AppendLine($"        {entityFullName} Read(IReadOnlyTx tx);");
            sb.AppendLine($"        void Create(IReadWriteTx tx, {entityFullName} entity);");
            sb.AppendLine($"        void Update(IReadWriteTx tx, {entityFullName} entity);");
            sb.AppendLine($"        void Delete(IReadWriteTx tx);");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static string EmitInMemoryImplementation(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var repoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Repositories"
            : $"{entity.Namespace}.Repositories";
        var entityFullName = string.IsNullOrEmpty(entity.Namespace)
            ? entity.ClassName
            : $"{entity.Namespace}.{entity.ClassName}";
        var dtoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Lilja.Repository.Generated.Dtos"
            : $"Lilja.Repository.Generated.Dtos.{entity.Namespace}";
        var dtoFullName = $"{dtoNamespace}.{entity.ClassName}Dto";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Lilja.Repository;");
        sb.AppendLine();
        sb.AppendLine($"namespace {repoNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {entity.ClassName}のインメモリリポジトリ。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public class InMemory{entity.ClassName}Repository : I{entity.ClassName}Repository");
        sb.AppendLine("    {");

        if (entity.HasKey)
        {
            var keyTypeName = GetKeyTypeName(entity);
            var keyParamName = GetKeyParamName(entity);

            // Dictionary storage
            sb.AppendLine($"        private readonly Dictionary<{keyTypeName}, {entityFullName}> _storage = new Dictionary<{keyTypeName}, {entityFullName}>();

        public InMemory{entity.ClassName}Repository()
        {{
#if UNITY_EDITOR
            Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory);
#endif
        }}");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadOnlyTx tx, {keyTypeName} {keyParamName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            _storage.TryGetValue({keyParamName}, out var entity);");
            sb.AppendLine("            return entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _storage[{entity.ClassName}.GetKey(entity)] = entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _storage[{entity.ClassName}.GetKey(entity)] = entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx, {keyTypeName} {keyParamName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            _storage.Remove({keyParamName});");
            sb.AppendLine("        }");
        }
        else
        {
            // Singleton: single field storage
            sb.AppendLine($"        private {entityFullName} _entity;

        public InMemory{entity.ClassName}Repository()
        {{
#if UNITY_EDITOR
            Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.InMemory);
#endif
        }}");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadOnlyTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            return _entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            _entity = entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            _entity = entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            _entity = null;");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Json永続化リポジトリを生成する。
    /// </summary>
    public static string EmitJsonImplementation(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var repoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Repositories"
            : $"{entity.Namespace}.Repositories";
        var entityFullName = string.IsNullOrEmpty(entity.Namespace)
            ? entity.ClassName
            : $"{entity.Namespace}.{entity.ClassName}";
        var dtoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Lilja.Repository.Generated.Dtos"
            : $"Lilja.Repository.Generated.Dtos.{entity.Namespace}";
        var dtoFullName = $"{dtoNamespace}.{entity.ClassName}Dto";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using Cysharp.Threading.Tasks;");
        sb.AppendLine("using Lilja.Repository;");
        sb.AppendLine();
        sb.AppendLine($"namespace {repoNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {entity.ClassName}のJson永続化リポジトリ。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public class Json{entity.ClassName}Repository : I{entity.ClassName}Repository");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly string _filePath;");
        sb.AppendLine($"        private bool _dirty;");

        if (entity.HasKey)
        {
            var keyTypeName = GetKeyTypeName(entity);
            var keyParamName = GetKeyParamName(entity);

            // Keyed Entity: Dictionary storage with wrapper for serialization
            sb.AppendLine($"        private Dictionary<{keyTypeName}, {dtoFullName}> _cache;");
            sb.AppendLine();

            // Wrapper class for JsonUtility serialization (Dictionary not directly supported)
            sb.AppendLine("        [Serializable]");
            sb.AppendLine($"        private class StorageWrapper");
            sb.AppendLine("        {");
            sb.AppendLine($"            public List<{dtoFullName}> Items = new List<{dtoFullName}>();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public Json{entity.ClassName}Repository()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _filePath = Path.Combine(Application.persistentDataPath, \"{entity.ClassName}.json\");");
            sb.AppendLine($"            _cache = new Dictionary<{keyTypeName}, {dtoFullName}>();");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("            Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json);");
            sb.AppendLine("#endif");
            sb.AppendLine("        }");
            sb.AppendLine();

            // InitializeAsync
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 永続化データを非同期に読み込んで初期化する。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public async UniTask InitializeAsync(CancellationToken ct = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            await LoadAsync();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GetKeyFromDto helper
            sb.AppendLine($"        private static {keyTypeName} GetKeyFromDto({dtoFullName} dto)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return {GetKeyReturnExpression(entity, entity.ClassName, "dto")};");
            sb.AppendLine("        }");
            sb.AppendLine();

            // MarkDirty (async)
            EmitMarkDirtyAsync(sb);

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadOnlyTx tx, {keyTypeName} {keyParamName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (!_cache.TryGetValue({keyParamName}, out var dto))");
            sb.AppendLine("            {");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return {entity.ClassName}.FromDto(dto);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var dto = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var dto = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx, {keyTypeName} {keyParamName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache.Remove({keyParamName});");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // LoadAsync
            sb.AppendLine("        private async UniTask LoadAsync()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = new Dictionary<{keyTypeName}, {dtoFullName}>();");
            sb.AppendLine("            var filePath = _filePath;");
            sb.AppendLine("            if (!File.Exists(filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var wrapper = await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var json = File.ReadAllText(filePath);");
            sb.AppendLine("                    return JsonUtility.FromJson<StorageWrapper>(json);");
            sb.AppendLine("                });");
            sb.AppendLine("                if (wrapper?.Items != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    foreach (var dto in wrapper.Items)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to load {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // SaveAsync
            sb.AppendLine("        private async UniTask SaveAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var wrapper = new StorageWrapper();");
            sb.AppendLine("                wrapper.Items.AddRange(_cache.Values);");
            sb.AppendLine("                var filePath = _filePath;");
            sb.AppendLine("                await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var json = JsonUtility.ToJson(wrapper, true);");
            sb.AppendLine("                    Lilja.Repository.AtomicFileWriter.WriteAllText(filePath, json);");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to save {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
        else
        {
            // Singleton Entity
            sb.AppendLine($"        private {dtoFullName} _cache;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public Json{entity.ClassName}Repository()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _filePath = Path.Combine(Application.persistentDataPath, \"{entity.ClassName}.json\");");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("            Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.Json);");
            sb.AppendLine("#endif");
            sb.AppendLine("        }");
            sb.AppendLine();

            // InitializeAsync
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 永続化データを非同期に読み込んで初期化する。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public async UniTask InitializeAsync(CancellationToken ct = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            await LoadAsync();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // MarkDirty (async)
            EmitMarkDirtyAsync(sb);

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadOnlyTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_cache == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return {entity.ClassName}.FromDto(_cache);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            _cache = null;");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // LoadAsync
            sb.AppendLine("        private async UniTask LoadAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            _cache = null;");
            sb.AppendLine("            var filePath = _filePath;");
            sb.AppendLine("            if (!File.Exists(filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                _cache = await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var json = File.ReadAllText(filePath);");
            sb.AppendLine($"                    return JsonUtility.FromJson<{dtoFullName}>(json);");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to load {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // SaveAsync
            sb.AppendLine("        private async UniTask SaveAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var cache = _cache;");
            sb.AppendLine("                var filePath = _filePath;");
            sb.AppendLine("                await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var json = JsonUtility.ToJson(cache, true);");
            sb.AppendLine("                    Lilja.Repository.AtomicFileWriter.WriteAllText(filePath, json);");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to save {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// MessagePack永続化リポジトリを生成する。
    /// </summary>
    public static string EmitMessagePackImplementation(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var repoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Repositories"
            : $"{entity.Namespace}.Repositories";
        var entityFullName = string.IsNullOrEmpty(entity.Namespace)
            ? entity.ClassName
            : $"{entity.Namespace}.{entity.ClassName}";
        var dtoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Lilja.Repository.Generated.Dtos"
            : $"Lilja.Repository.Generated.Dtos.{entity.Namespace}";
        var formatterNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Lilja.Repository.Generated.Formatters"
            : $"Lilja.Repository.Generated.Formatters.{entity.Namespace}";
        var dtoFullName = $"{dtoNamespace}.{entity.ClassName}Dto";
        var formatterFullName = $"{formatterNamespace}.{entity.ClassName}DtoFormatter";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using Cysharp.Threading.Tasks;");
        sb.AppendLine("using MessagePack;");
        sb.AppendLine("using MessagePack.Formatters;");
        sb.AppendLine("using MessagePack.Resolvers;");
        sb.AppendLine("using Lilja.Repository;");
        sb.AppendLine();
        sb.AppendLine($"namespace {repoNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {entity.ClassName}のMessagePack永続化リポジトリ。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public class MessagePack{entity.ClassName}Repository : I{entity.ClassName}Repository");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly string _filePath;");
        sb.AppendLine($"        private readonly MessagePackSerializerOptions _options;");
        sb.AppendLine($"        private bool _dirty;");

        if (entity.HasKey)
        {
            var keyTypeName = GetKeyTypeName(entity);
            var keyParamName = GetKeyParamName(entity);

            // Keyed Entity: Dictionary storage
            sb.AppendLine($"        private Dictionary<{keyTypeName}, {dtoFullName}> _cache;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public MessagePack{entity.ClassName}Repository()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _filePath = Path.Combine(Application.persistentDataPath, \"{entity.ClassName}.msgpack\");");
            sb.AppendLine($"            var resolver = CompositeResolver.Create(new IMessagePackFormatter[] {{ new {formatterFullName}() }}, new IFormatterResolver[] {{ StandardResolver.Instance }});");
            sb.AppendLine("            _options = MessagePackSerializerOptions.Standard.WithResolver(resolver);");
            sb.AppendLine($"            _cache = new Dictionary<{keyTypeName}, {dtoFullName}>();");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("            Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack);");
            sb.AppendLine("#endif");
            sb.AppendLine("        }");
            sb.AppendLine();

            // InitializeAsync
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 永続化データを非同期に読み込んで初期化する。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public async UniTask InitializeAsync(CancellationToken ct = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            await LoadAsync();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GetKeyFromDto helper
            sb.AppendLine($"        private static {keyTypeName} GetKeyFromDto({dtoFullName} dto)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return {GetKeyReturnExpression(entity, entity.ClassName, "dto")};");
            sb.AppendLine("        }");
            sb.AppendLine();

            // MarkDirty (async)
            EmitMarkDirtyAsync(sb);

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadOnlyTx tx, {keyTypeName} {keyParamName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (!_cache.TryGetValue({keyParamName}, out var dto))");
            sb.AppendLine("            {");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return {entity.ClassName}.FromDto(dto);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var dto = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var dto = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx, {keyTypeName} {keyParamName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache.Remove({keyParamName});");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // LoadAsync
            sb.AppendLine("        private async UniTask LoadAsync()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = new Dictionary<{keyTypeName}, {dtoFullName}>();");
            sb.AppendLine("            var filePath = _filePath;");
            sb.AppendLine("            if (!File.Exists(filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var options = _options;");
            sb.AppendLine($"                var list = await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var bytes = File.ReadAllBytes(filePath);");
            sb.AppendLine($"                    return MessagePackSerializer.Deserialize<List<{dtoFullName}>>(bytes, options);");
            sb.AppendLine("                });");
            sb.AppendLine("                if (list != null)");
            sb.AppendLine("                {");
            sb.AppendLine("                    foreach (var dto in list)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to load {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // SaveAsync
            sb.AppendLine("        private async UniTask SaveAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var list = new List<{dtoFullName}>(_cache.Values);");
            sb.AppendLine("                var filePath = _filePath;");
            sb.AppendLine("                var options = _options;");
            sb.AppendLine("                await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var bytes = MessagePackSerializer.Serialize(list, options);");
            sb.AppendLine("                    Lilja.Repository.AtomicFileWriter.WriteAllBytes(filePath, bytes);");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to save {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
        else
        {
            // Singleton Entity
            sb.AppendLine($"        private {dtoFullName} _cache;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public MessagePack{entity.ClassName}Repository()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _filePath = Path.Combine(Application.persistentDataPath, \"{entity.ClassName}.msgpack\");");
            sb.AppendLine($"            var resolver = CompositeResolver.Create(new IMessagePackFormatter[] {{ new {formatterFullName}() }}, new IFormatterResolver[] {{ StandardResolver.Instance }});");
            sb.AppendLine("            _options = MessagePackSerializerOptions.Standard.WithResolver(resolver);");
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("            Lilja.Repository.Diagnostics.RepositoryTracker.Track(this, Lilja.Repository.Diagnostics.RepositoryTracker.RepositoryType.MessagePack);");
            sb.AppendLine("#endif");
            sb.AppendLine("        }");
            sb.AppendLine();

            // InitializeAsync
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// 永続化データを非同期に読み込んで初期化する。");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public async UniTask InitializeAsync(CancellationToken ct = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            await LoadAsync();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // MarkDirty (async)
            EmitMarkDirtyAsync(sb);

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadOnlyTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_cache == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return {entity.ClassName}.FromDto(_cache);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            _cache = null;");
            sb.AppendLine("            MarkDirty(tx);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // LoadAsync
            sb.AppendLine("        private async UniTask LoadAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            _cache = null;");
            sb.AppendLine("            var filePath = _filePath;");
            sb.AppendLine("            if (!File.Exists(filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var options = _options;");
            sb.AppendLine($"                _cache = await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var bytes = File.ReadAllBytes(filePath);");
            sb.AppendLine($"                    return MessagePackSerializer.Deserialize<{dtoFullName}>(bytes, options);");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to load {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // SaveAsync
            sb.AppendLine("        private async UniTask SaveAsync()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var cache = _cache;");
            sb.AppendLine("                var filePath = _filePath;");
            sb.AppendLine("                var options = _options;");
            sb.AppendLine("                await UniTask.RunOnThreadPool(() =>");
            sb.AppendLine("                {");
            sb.AppendLine("                    var bytes = MessagePackSerializer.Serialize(cache, options);");
            sb.AppendLine("                    Lilja.Repository.AtomicFileWriter.WriteAllBytes(filePath, bytes);");
            sb.AppendLine("                });");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to save {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
