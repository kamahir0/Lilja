using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// ITransferable実装生成。
/// </summary>
internal static class TransferableEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var dtoTypeName = $"Lilja.Generated.Dtos.{entity.ClassName}Dto";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"    partial class {entity.ClassName} : Lilja.Repository.ITransferable<{dtoTypeName}>");
        sb.AppendLine("    {");

        // ToDto
        sb.AppendLine($"        public {dtoTypeName} ToDto()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var dto = new {dtoTypeName}();");

        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                // ValueObjectはToPrimitiveメソッドを呼び出してフラット化
                sb.AppendLine($"            var {field.Name}_tuple = {field.Name}.{field.ValueObjectInfo.ToPrimitiveMethodName}();");
                foreach (var element in field.ValueObjectInfo.TupleElements)
                {
                    sb.AppendLine($"            dto.{field.DtoFieldName}_{element.Name} = {field.Name}_tuple.{element.Name};");
                }
            }
            else
            {
                sb.AppendLine($"            dto.{field.DtoFieldName} = {field.Name};");
            }
        }

        sb.AppendLine("            return dto;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // FromDto
        sb.AppendLine($"        public void FromDto({dtoTypeName} dto)");
        sb.AppendLine("        {");

        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                // ValueObjectを再構築
                var args = new StringBuilder();
                for (int i = 0; i < field.ValueObjectInfo.TupleElements.Count; i++)
                {
                    if (i > 0) args.Append(", ");
                    var element = field.ValueObjectInfo.TupleElements[i];
                    args.Append($"dto.{field.DtoFieldName}_{element.Name}");
                }
                sb.AppendLine($"            {field.Name} = new {field.FullTypeName}({args});");
            }
            else
            {
                sb.AppendLine($"            {field.Name} = dto.{field.DtoFieldName};");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}
