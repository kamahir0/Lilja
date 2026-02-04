using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// Repository生成。
/// </summary>
internal static class RepositoryEmitter
{
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

        if (entity.HasKey && entity.KeyField.HasValue)
        {
            var keyField = entity.KeyField.Value;
            var entityFullName = string.IsNullOrEmpty(entity.Namespace)
                ? entity.ClassName
                : $"{entity.Namespace}.{entity.ClassName}";

            // Keyed Entity: CRUD operations
            sb.AppendLine($"        {entityFullName} Read(IReadableTx tx, {keyField.TypeName} key);");
            sb.AppendLine($"        void Create(IReadWriteTx tx, {entityFullName} entity);");
            sb.AppendLine($"        void Update(IReadWriteTx tx, {entityFullName} entity);");
            sb.AppendLine($"        void Delete(IReadWriteTx tx, {keyField.TypeName} key);");
        }
        else
        {
            // Singleton Entity
            var entityFullName = string.IsNullOrEmpty(entity.Namespace)
                ? entity.ClassName
                : $"{entity.Namespace}.{entity.ClassName}";

            sb.AppendLine($"        {entityFullName} Read(IReadableTx tx);");
            sb.AppendLine($"        void Update(IReadWriteTx tx, {entityFullName} entity);");
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

        if (entity.HasKey && entity.KeyField.HasValue)
        {
            var keyField = entity.KeyField.Value;

            // Dictionary storage
            sb.AppendLine($"        private readonly Dictionary<{keyField.TypeName}, {entityFullName}> _storage = new Dictionary<{keyField.TypeName}, {entityFullName}>();");
            sb.AppendLine();

            // GetKey helper - static ToDto経由でキーを取得
            sb.AppendLine($"        private static {keyField.TypeName} GetKey({entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return {entity.ClassName}.ToDto(entity).{keyField.DtoFieldName};");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadableTx tx, {keyField.TypeName} key)");
            sb.AppendLine("        {");
            sb.AppendLine("            _storage.TryGetValue(key, out var entity);");
            sb.AppendLine("            return entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Create
            sb.AppendLine($"        public void Create(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            _storage[GetKey(entity)] = entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            _storage[GetKey(entity)] = entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx, {keyField.TypeName} key)");
            sb.AppendLine("        {");
            sb.AppendLine("            _storage.Remove(key);");
            sb.AppendLine("        }");
        }
        else
        {
            // Singleton: single field storage
            sb.AppendLine($"        private {entityFullName} _entity;");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadableTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            return _entity;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            _entity = entity;");
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
        sb.AppendLine("using UnityEngine;");
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

        if (entity.HasKey && entity.KeyField.HasValue)
        {
            var keyField = entity.KeyField.Value;

            // Keyed Entity: Dictionary storage with wrapper for serialization
            sb.AppendLine($"        private Dictionary<{keyField.TypeName}, {dtoFullName}> _cache;");
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
            sb.AppendLine("            Load();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GetKey helper - static ToDto経由でキーを取得
            sb.AppendLine($"        private static {keyField.TypeName} GetKey({entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return {entity.ClassName}.ToDto(entity).{keyField.DtoFieldName};");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        private static {keyField.TypeName} GetKeyFromDto({dtoFullName} dto)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return dto.{keyField.DtoFieldName};");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadableTx tx, {keyField.TypeName} key)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!_cache.TryGetValue(key, out var dto))");
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
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var dto = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx, {keyField.TypeName} key)");
            sb.AppendLine("        {");
            sb.AppendLine("            _cache.Remove(key);");
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Load
            sb.AppendLine("        private void Load()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = new Dictionary<{keyField.TypeName}, {dtoFullName}>();");
            sb.AppendLine("            if (!File.Exists(_filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var json = File.ReadAllText(_filePath);");
            sb.AppendLine("                var wrapper = JsonUtility.FromJson<StorageWrapper>(json);");
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

            // Save
            sb.AppendLine("        private void Save()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var wrapper = new StorageWrapper();");
            sb.AppendLine("                wrapper.Items.AddRange(_cache.Values);");
            sb.AppendLine("                var json = JsonUtility.ToJson(wrapper, true);");
            sb.AppendLine("                File.WriteAllText(_filePath, json);");
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
            sb.AppendLine("            Load();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadableTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_cache == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return {entity.ClassName}.FromDto(_cache);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Load
            sb.AppendLine("        private void Load()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!File.Exists(_filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var json = File.ReadAllText(_filePath);");
            sb.AppendLine($"                _cache = JsonUtility.FromJson<{dtoFullName}>(json);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to load {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Save
            sb.AppendLine("        private void Save()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var json = JsonUtility.ToJson(_cache, true);");
            sb.AppendLine("                File.WriteAllText(_filePath, json);");
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
        sb.AppendLine("using UnityEngine;");
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

        if (entity.HasKey && entity.KeyField.HasValue)
        {
            var keyField = entity.KeyField.Value;

            // Keyed Entity: Dictionary storage
            sb.AppendLine($"        private Dictionary<{keyField.TypeName}, {dtoFullName}> _cache;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"        public MessagePack{entity.ClassName}Repository()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _filePath = Path.Combine(Application.persistentDataPath, \"{entity.ClassName}.msgpack\");");
            sb.AppendLine($"            var resolver = CompositeResolver.Create(new IMessagePackFormatter[] {{ new {formatterFullName}() }}, new IFormatterResolver[] {{ StandardResolver.Instance }});");
            sb.AppendLine("            _options = MessagePackSerializerOptions.Standard.WithResolver(resolver);");
            sb.AppendLine("            Load();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // GetKey helper - static ToDto経由でキーを取得
            sb.AppendLine($"        private static {keyField.TypeName} GetKey({entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return {entity.ClassName}.ToDto(entity).{keyField.DtoFieldName};");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        private static {keyField.TypeName} GetKeyFromDto({dtoFullName} dto)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return dto.{keyField.DtoFieldName};");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadableTx tx, {keyField.TypeName} key)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!_cache.TryGetValue(key, out var dto))");
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
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var dto = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            _cache[GetKeyFromDto(dto)] = dto;");
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Delete
            sb.AppendLine($"        public void Delete(IReadWriteTx tx, {keyField.TypeName} key)");
            sb.AppendLine("        {");
            sb.AppendLine("            _cache.Remove(key);");
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Load
            sb.AppendLine("        private void Load()");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = new Dictionary<{keyField.TypeName}, {dtoFullName}>();");
            sb.AppendLine("            if (!File.Exists(_filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var bytes = File.ReadAllBytes(_filePath);");
            sb.AppendLine($"                var list = MessagePackSerializer.Deserialize<List<{dtoFullName}>>(bytes, _options);");
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

            // Save
            sb.AppendLine("        private void Save()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var list = new List<{dtoFullName}>(_cache.Values);");
            sb.AppendLine("                var bytes = MessagePackSerializer.Serialize(list, _options);");
            sb.AppendLine("                File.WriteAllBytes(_filePath, bytes);");
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
            sb.AppendLine("            Load();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Read
            sb.AppendLine($"        public {entityFullName} Read(IReadableTx tx)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_cache == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return null;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return {entity.ClassName}.FromDto(_cache);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Update
            sb.AppendLine($"        public void Update(IReadWriteTx tx, {entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            _cache = {entity.ClassName}.ToDto(entity);");
            sb.AppendLine("            Save();");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Load
            sb.AppendLine("        private void Load()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!File.Exists(_filePath))");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var bytes = File.ReadAllBytes(_filePath);");
            sb.AppendLine($"                _cache = MessagePackSerializer.Deserialize<{dtoFullName}>(bytes, _options);");
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"Failed to load {_filePath}: {ex.Message}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Save
            sb.AppendLine("        private void Save()");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var bytes = MessagePackSerializer.Serialize(_cache, _options);");
            sb.AppendLine("                File.WriteAllBytes(_filePath, bytes);");
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
