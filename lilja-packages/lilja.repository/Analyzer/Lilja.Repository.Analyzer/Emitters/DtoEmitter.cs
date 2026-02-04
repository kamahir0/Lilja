using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// DTO生成。
/// </summary>
internal static class DtoEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var sb = new StringBuilder();

        // Entityのnamespaceを含めたDTO namespace
        var dtoNamespace = string.IsNullOrEmpty(entity.Namespace)
            ? "Lilja.Repository.Generated.Dtos"
            : $"Lilja.Repository.Generated.Dtos.{entity.Namespace}";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {dtoNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {entity.ClassName}のDTO。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [Serializable]");
        sb.AppendLine($"    public class {entity.ClassName}Dto");
        sb.AppendLine("    {");

        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                // ValueObjectはフラット化
                foreach (var element in field.ValueObjectInfo.TupleElements)
                {
                    sb.AppendLine($"        public {element.TypeName} {field.DtoFieldName}_{element.Name};");
                }
            }
            else
            {
                sb.AppendLine($"        public {field.TypeName} {field.DtoFieldName};");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
