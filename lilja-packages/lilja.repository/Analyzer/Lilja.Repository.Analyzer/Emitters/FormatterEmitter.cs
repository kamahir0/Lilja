using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// MessagePackFormatter生成（依存なし）。
/// </summary>
internal static class FormatterEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var sb = new StringBuilder();
        var dtoTypeName = $"Lilja.Generated.Dtos.{entity.ClassName}Dto";

        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("using MessagePack;");
        sb.AppendLine("using MessagePack.Formatters;");
        sb.AppendLine();
        sb.AppendLine("namespace Lilja.Generated.Formatters");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// {entity.ClassName}Dto用のMessagePackFormatter。");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public sealed class {entity.ClassName}DtoFormatter : IMessagePackFormatter<{dtoTypeName}>");
        sb.AppendLine("    {");

        // フラット化後の総フィールド数を計算
        int fieldCount = 0;
        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                fieldCount += field.ValueObjectInfo.TupleElements.Count;
            }
            else
            {
                fieldCount++;
            }
        }

        // Serialize
        sb.AppendLine($"        public void Serialize(ref MessagePackWriter writer, {dtoTypeName} value, MessagePackSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNil();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            writer.WriteArrayHeader({fieldCount});");

        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                foreach (var element in field.ValueObjectInfo.TupleElements)
                {
                    EmitWriteCall(sb, element.TypeName, $"value.{field.DtoFieldName}_{element.Name}");
                }
            }
            else
            {
                EmitWriteCall(sb, field.TypeName, $"value.{field.DtoFieldName}");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Deserialize
        sb.AppendLine($"        public {dtoTypeName} Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (reader.TryReadNil())");
        sb.AppendLine("            {");
        sb.AppendLine("                return null;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var count = reader.ReadArrayHeader();");
        sb.AppendLine($"            var result = new {dtoTypeName}();");
        sb.AppendLine();

        int fieldIndex = 0;
        foreach (var field in entity.Fields)
        {
            if (field.ValueObjectInfo.IsValueObject)
            {
                foreach (var element in field.ValueObjectInfo.TupleElements)
                {
                    sb.AppendLine($"            if (count > {fieldIndex})");
                    sb.AppendLine("            {");
                    EmitReadCall(sb, element.TypeName, $"result.{field.DtoFieldName}_{element.Name}");
                    sb.AppendLine("            }");
                    fieldIndex++;
                }
            }
            else
            {
                sb.AppendLine($"            if (count > {fieldIndex})");
                sb.AppendLine("            {");
                EmitReadCall(sb, field.TypeName, $"result.{field.DtoFieldName}");
                sb.AppendLine("            }");
                fieldIndex++;
            }
        }

        sb.AppendLine();
        sb.AppendLine("            return result;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitWriteCall(StringBuilder sb, string typeName, string fieldAccess)
    {
        var writeMethod = GetWriteMethod(typeName);
        sb.AppendLine($"            writer.{writeMethod}({fieldAccess});");
    }

    private static void EmitReadCall(StringBuilder sb, string typeName, string fieldAccess)
    {
        var readMethod = GetReadMethod(typeName);
        sb.AppendLine($"                {fieldAccess} = reader.{readMethod}();");
    }

    private static string GetWriteMethod(string typeName)
    {
        return typeName switch
        {
            "bool" => "Write",
            "byte" => "Write",
            "sbyte" => "Write",
            "short" => "Write",
            "ushort" => "Write",
            "int" => "Write",
            "uint" => "Write",
            "long" => "Write",
            "ulong" => "Write",
            "float" => "Write",
            "double" => "Write",
            "string" => "Write",
            "String" => "Write",
            _ => "Write"
        };
    }

    private static string GetReadMethod(string typeName)
    {
        return typeName switch
        {
            "bool" => "ReadBoolean",
            "byte" => "ReadByte",
            "sbyte" => "ReadSByte",
            "short" => "ReadInt16",
            "ushort" => "ReadUInt16",
            "int" => "ReadInt32",
            "uint" => "ReadUInt32",
            "long" => "ReadInt64",
            "ulong" => "ReadUInt64",
            "float" => "ReadSingle",
            "double" => "ReadDouble",
            "string" or "String" => "ReadString",
            _ => "ReadInt32"
        };
    }
}
