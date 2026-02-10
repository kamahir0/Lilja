using System.Collections.Generic;
using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// KeyAccessor生成。
/// Entity partial classにinternal static GetKeyメソッドを追加する。
/// </summary>
internal static class KeyAccessorEmitter
{
    /// <summary>
    /// KeyAccessorコードを生成する。
    /// </summary>
    public static string Emit(EntityInfo entity)
    {
        if (!entity.HasKey)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        var keyTypeName = GetKeyTypeName(entity);

        sb.AppendLine("#nullable disable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
        }

        var indent = string.IsNullOrEmpty(entity.Namespace) ? "    " : "        ";
        var classIndent = string.IsNullOrEmpty(entity.Namespace) ? "" : "    ";

        sb.AppendLine($"{classIndent}partial class {entity.ClassName}");
        sb.AppendLine($"{classIndent}{{");

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Entityからキーを取得する。");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}internal static {keyTypeName} GetKey({entity.ClassName} entity)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    return {GetKeyReturnExpression(entity)};");
        sb.AppendLine($"{indent}}}");

        sb.AppendLine($"{classIndent}}}");

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// キーの型文字列を取得する。
    /// </summary>
    private static string GetKeyTypeName(EntityInfo entity)
    {
        if (entity.IsCompositeKey)
        {
            var types = new List<string>();
            foreach (var keyField in entity.KeyFields)
            {
                types.Add(keyField.TypeName);
            }
            return $"({string.Join(", ", types)})";
        }
        else
        {
            return entity.KeyFields[0].TypeName;
        }
    }

    /// <summary>
    /// GetKeyメソッドのreturn式を生成する。
    /// フィールドに直接アクセスする（DTO経由ではない）。
    /// </summary>
    private static string GetKeyReturnExpression(EntityInfo entity)
    {
        if (entity.IsCompositeKey)
        {
            var parts = new List<string>();
            foreach (var keyField in entity.KeyFields)
            {
                parts.Add($"entity.{keyField.Name}");
            }
            return $"({string.Join(", ", parts)})";
        }
        else
        {
            return $"entity.{entity.KeyFields[0].Name}";
        }
    }
}
