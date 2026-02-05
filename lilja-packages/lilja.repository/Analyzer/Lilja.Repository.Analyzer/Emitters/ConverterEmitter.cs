using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// Converter生成（ToDto/FromDtoメソッドとprivateコンストラクタ）。
/// </summary>
internal static class ConverterEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var sb = new StringBuilder();

        // Entityのnamespaceを含めたDTO namespace
        var dtoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Lilja.Repository.Generated.Dtos"
            : $"Lilja.Repository.Generated.Dtos.{entity.Namespace}";
        var dtoTypeName = $"{dtoNamespace}.{entity.ClassName}Dto";
        var entityFullName = string.IsNullOrEmpty(entity.Namespace)
            ? entity.ClassName
            : $"{entity.Namespace}.{entity.ClassName}";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
        }

        var indent = string.IsNullOrEmpty(entity.Namespace) ? "" : "    ";

        sb.AppendLine($"{indent}partial class {entity.ClassName}");
        sb.AppendLine($"{indent}{{");

        // Private Constructor (Persist属性付きフィールドを全て引数に持つ)
        // 既に該当するコンストラクタが存在する場合はスキップ
        if (entity.NeedsConstructorGeneration)
        {
            EmitPrivateConstructor(sb, entity, indent);
        }

        // ToDto (internal static)
        EmitToDto(sb, entity, dtoTypeName, indent);

        // FromDto (internal static)
        EmitFromDto(sb, entity, dtoTypeName, entityFullName, indent);

        sb.AppendLine($"{indent}}}");

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void EmitPrivateConstructor(StringBuilder sb, EntityInfo entity, string indent)
    {
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// DTO復元用のprivateコンストラクタ。");
        sb.AppendLine($"{indent}    /// </summary>");

        // 引数リストを構築
        var paramList = new StringBuilder();
        for (int i = 0; i < entity.Fields.Count; i++)
        {
            if (i > 0) paramList.Append(", ");
            var field = entity.Fields[i];

            if (field.ValueObjectInfo.IsValueObject)
            {
                paramList.Append($"{field.FullTypeName} {field.DtoFieldName.ToCamelCase()}");
            }
            else
            {
                paramList.Append($"{field.TypeName} {field.DtoFieldName.ToCamelCase()}");
            }
        }

        sb.AppendLine($"{indent}    private {entity.ClassName}({paramList})");
        sb.AppendLine($"{indent}    {{");

        // フィールドへの代入
        foreach (var field in entity.Fields)
        {
            sb.AppendLine($"{indent}        {field.Name} = {field.DtoFieldName.ToCamelCase()};");
        }

        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
    }

    private static void EmitToDto(StringBuilder sb, EntityInfo entity, string dtoTypeName, string indent)
    {
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// EntityをDTOに変換する。");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    internal static {dtoTypeName} ToDto({entity.ClassName} entity)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        var dto = new {dtoTypeName}();");

        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                // ValueObjectはToPrimitiveメソッドを呼び出してフラット化
                sb.AppendLine($"{indent}        var {field.Name}_tuple = entity.{field.Name}.{field.ValueObjectInfo.ToPrimitiveMethodName}();");
                foreach (var element in field.ValueObjectInfo.TupleElements)
                {
                    sb.AppendLine($"{indent}        dto.{field.DtoFieldName}_{element.Name} = {field.Name}_tuple.{element.Name};");
                }
            }
            else
            {
                sb.AppendLine($"{indent}        dto.{field.DtoFieldName} = entity.{field.Name};");
            }
        }

        sb.AppendLine($"{indent}        return dto;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
    }

    private static void EmitFromDto(StringBuilder sb, EntityInfo entity, string dtoTypeName, string entityFullName, string indent)
    {
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// DTOからEntityを復元する。");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    internal static {entity.ClassName} FromDto({dtoTypeName} dto)");
        sb.AppendLine($"{indent}    {{");

        // コンストラクタ引数を構築
        var argList = new StringBuilder();
        for (int i = 0; i < entity.Fields.Count; i++)
        {
            if (i > 0) argList.Append(", ");
            var field = entity.Fields[i];

            if (field.ValueObjectInfo.IsValueObject)
            {
                // ValueObjectを再構築
                var voArgs = new StringBuilder();
                for (int j = 0; j < field.ValueObjectInfo.TupleElements.Count; j++)
                {
                    if (j > 0) voArgs.Append(", ");
                    var element = field.ValueObjectInfo.TupleElements[j];
                    voArgs.Append($"dto.{field.DtoFieldName}_{element.Name}");
                }
                argList.Append($"new {field.FullTypeName}({voArgs})");
            }
            else
            {
                argList.Append($"dto.{field.DtoFieldName}");
            }
        }

        sb.AppendLine($"{indent}        return new {entity.ClassName}({argList});");
        sb.AppendLine($"{indent}    }}");
    }
}

/// <summary>
/// 文字列拡張メソッド。
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// 文字列をCamelCaseに変換する。
    /// アンダースコアを除去し、先頭文字を小文字にする。
    /// </summary>
    public static string ToCamelCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        // アンダースコアを除去
        var removed = str.Replace("_", "");
        if (string.IsNullOrEmpty(removed))
            return str; // 元が "_" のみのような場合

        // 先頭を小文字化
        if (char.IsLower(removed[0]))
            return removed;
        
        return char.ToLowerInvariant(removed[0]) + removed.Substring(1);
    }
}
