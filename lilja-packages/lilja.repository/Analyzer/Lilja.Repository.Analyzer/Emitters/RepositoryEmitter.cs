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
        var dtoFullName = $"Lilja.Generated.Dtos.{entity.ClassName}Dto";

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

            // GetKey helper - ITransferable経由でDTOからキーを取得
            sb.AppendLine($"        private static {keyField.TypeName} GetKey({entityFullName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var transferable = (ITransferable<{dtoFullName}>)entity;");
            sb.AppendLine($"            return transferable.ToDto().{keyField.DtoFieldName};");
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
}
