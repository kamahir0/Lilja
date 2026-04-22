using System.Collections.Generic;
using System.Linq;

namespace Lilja.Repository.Analyzer;

public sealed partial class LiljaRepositoryGenerator
{
    /// <summary>
    /// エンティティの永続化に使う DTO 型を生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateDto(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.DtoNamespace);
        sb.AppendLine("[global::System.Serializable]");
        sb.Append("public sealed class ").Append(model.DtoTypeNameWithoutNamespace).AppendLine();
        sb.AppendLine("{");
        foreach (var member in model.PersistedMembers)
        {
            foreach (var dtoField in member.DtoFields)
            {
                sb.Append("    public ").Append(dtoField.TypeName).Append(' ').Append(dtoField.Name).AppendLine(" = default!;");
            }
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.DtoNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// 永続化されたキー付き状態またはシングルトン状態を表すストレージエンベロープ型を生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateStorageEnvelope(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.StorageNamespace);
        sb.AppendLine("[global::System.Serializable]");
        sb.Append("internal sealed class ").Append(model.StorageEnvelopeTypeNameWithoutNamespace).AppendLine();
        sb.AppendLine("{");
        if (model.IsKeyed)
        {
            sb.Append("    public global::System.Collections.Generic.List<").Append(model.DtoTypeName).Append("> Items = new global::System.Collections.Generic.List<")
                .Append(model.DtoTypeName).AppendLine(">();");
        }
        else
        {
            sb.AppendLine("    public bool HasValue;");
            sb.Append("    public ").Append(model.DtoTypeName).AppendLine("? Item;");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.StorageNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティと DTO の相互変換を行う partial メソッドを生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateConverterPartial(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.NamespaceName);
        sb.Append("public partial class ").Append(model.EntityName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    internal static ").Append(model.DtoTypeName).Append(" ToDto(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        for (var index = 0; index < model.PersistedMembers.Length; index++)
        {
            var member = model.PersistedMembers[index];
            if (member.ValueObjectShape?.PrimitiveParts.Length is not > 1)
            {
                continue;
            }

            sb.Append("        var primitive").Append(index).Append(" = entity.").Append(member.AccessibleName).Append('.').Append(member.ValueObjectShape!.ToPrimitiveMethodName).AppendLine("();");
        }

        sb.Append("        return new ").Append(model.DtoTypeName).AppendLine();
        sb.AppendLine("        {");
        for (var memberIndex = 0; memberIndex < model.PersistedMembers.Length; memberIndex++)
        {
            var member = model.PersistedMembers[memberIndex];
            for (var fieldIndex = 0; fieldIndex < member.DtoFields.Length; fieldIndex++)
            {
                var dtoField = member.DtoFields[fieldIndex];
                sb.Append("            ").Append(dtoField.Name).Append(" = ").Append(GetToDtoExpression(model, member, memberIndex, fieldIndex)).AppendLine(",");
            }
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    internal static ").Append(model.EntityTypeName).Append(" FromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
        sb.AppendLine("    {");
        sb.Append("        return new ").Append(model.EntityTypeName).Append('(');
        for (var index = 0; index < model.PersistedMembers.Length; index++)
        {
            if (index > 0)
            {
                sb.Append(", ");
            }

            sb.Append(GetFromDtoArgumentExpression(model.PersistedMembers[index]));
        }

        sb.AppendLine(");");
        sb.AppendLine("    }");

        if (model.NeedsGeneratedConstructor)
        {
            sb.AppendLine();
            sb.Append("    private ").Append(model.EntityName).Append('(');
            for (var index = 0; index < model.PersistedMembers.Length; index++)
            {
                var member = model.PersistedMembers[index];
                if (index > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(member.TypeName).Append(' ').Append(ToCamelCase(member.Name));
            }

            sb.AppendLine(")");
            sb.AppendLine("    {");
            foreach (var member in model.PersistedMembers)
            {
                sb.Append("        this.").Append(member.AccessibleName).Append(" = ").Append(ToCamelCase(member.Name)).AppendLine(";");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.NamespaceName);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティと DTO からキーを取得する partial メソッドを生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateKeyAccessorPartial(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.NamespaceName);
        sb.Append("public partial class ").Append(model.EntityName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    internal static ").Append(model.KeyTypeName).Append(" GetKey(").Append(model.EntityTypeName).AppendLine(" entity)");
        sb.AppendLine("    {");
        sb.Append("        return ").Append(GetEntityKeyExpression(model)).AppendLine(";");
        sb.AppendLine("    }");
        if (model.IsPersisted)
        {
            sb.AppendLine();
            sb.Append("    internal static ").Append(model.KeyTypeName).Append(" GetKeyFromDto(").Append(model.DtoTypeName).AppendLine(" dto)");
            sb.AppendLine("    {");
            sb.Append("        return ").Append(GetDtoKeyExpression(model)).AppendLine(";");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.NamespaceName);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティ DTO 型用の MessagePack フォーマッターを生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateDtoFormatter(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.FormatterNamespace);
        sb.Append("public sealed class ").Append(model.DtoFormatterTypeNameWithoutNamespace).Append(" : global::MessagePack.Formatters.IMessagePackFormatter<")
            .Append(model.DtoTypeName).AppendLine(">");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::MessagePack.Formatters.IMessagePackFormatter<T> ResolveFormatter<T>(global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        return options.Resolver.GetFormatter<T>() ?? throw new global::MessagePack.MessagePackSerializationException($\"Formatter not found for {typeof(T).FullName}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public void Serialize(ref global::MessagePack.MessagePackWriter writer, ").Append(model.DtoTypeName)
            .AppendLine(" value, global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNil();");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.Append("        writer.WriteArrayHeader(").Append(model.AllDtoFields.Length).AppendLine(");");
        foreach (var dtoField in model.AllDtoFields)
        {
            sb.Append("        ResolveFormatter<").Append(dtoField.TypeName).Append(">(options).Serialize(ref writer, value.")
                .Append(dtoField.Name).AppendLine(", options);");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public ").Append(model.DtoTypeName).Append(" Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)")
            .AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (reader.TryReadNil())");
        sb.AppendLine("        {");
        sb.AppendLine("            return null!;");
        sb.AppendLine("        }");
        sb.Append("        var value = new ").Append(model.DtoTypeName).AppendLine("();");
        sb.AppendLine("        var length = reader.ReadArrayHeader();");
        for (var index = 0; index < model.AllDtoFields.Length; index++)
        {
            var dtoField = model.AllDtoFields[index];
            sb.Append("        if (length > ").Append(index).AppendLine(")");
            sb.AppendLine("        {");
            sb.Append("            value.").Append(dtoField.Name).Append(" = ResolveFormatter<").Append(dtoField.TypeName)
                .AppendLine(">(options).Deserialize(ref reader, options);");
            sb.AppendLine("        }");
        }

        sb.Append("        for (var index = ").Append(model.AllDtoFields.Length).AppendLine("; index < length; index++)");
        sb.AppendLine("        {");
        sb.AppendLine("            reader.Skip();");
        sb.AppendLine("        }");
        sb.AppendLine("        return value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.FormatterNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティのストレージエンベロープ型用の MessagePack フォーマッターを生成します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>生成されたソースコード。</returns>
    private static string GenerateStorageEnvelopeFormatter(EntityModel model)
    {
        var sb = CreateSourceBuilder();
        AppendNamespaceStart(sb, model.FormatterNamespace);
        sb.Append("internal sealed class ").Append(model.StorageEnvelopeFormatterTypeNameWithoutNamespace).Append(" : global::MessagePack.Formatters.IMessagePackFormatter<")
            .Append(model.StorageEnvelopeTypeName).AppendLine(">");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::MessagePack.Formatters.IMessagePackFormatter<T> ResolveFormatter<T>(global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        return options.Resolver.GetFormatter<T>() ?? throw new global::MessagePack.MessagePackSerializationException($\"Formatter not found for {typeof(T).FullName}.\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public void Serialize(ref global::MessagePack.MessagePackWriter writer, ").Append(model.StorageEnvelopeTypeName)
            .AppendLine(" value, global::MessagePack.MessagePackSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            writer.WriteNil();");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        if (model.IsKeyed)
        {
            sb.AppendLine("        writer.WriteArrayHeader(1);");
            sb.Append("        ResolveFormatter<global::System.Collections.Generic.List<").Append(model.DtoTypeName)
                .Append(">>(options).Serialize(ref writer, value.Items, options);").AppendLine();
        }
        else
        {
            sb.AppendLine("        writer.WriteArrayHeader(2);");
            sb.AppendLine("        ResolveFormatter<bool>(options).Serialize(ref writer, value.HasValue, options);");
            sb.Append("        ResolveFormatter<").Append(model.DtoTypeName).AppendLine("?>(options).Serialize(ref writer, value.Item, options);");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public ").Append(model.StorageEnvelopeTypeName).Append(" Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)")
            .AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (reader.TryReadNil())");
        sb.AppendLine("        {");
        sb.AppendLine("            return null!;");
        sb.AppendLine("        }");
        sb.Append("        var value = new ").Append(model.StorageEnvelopeTypeName).AppendLine("();");
        sb.AppendLine("        var length = reader.ReadArrayHeader();");
        if (model.IsKeyed)
        {
            sb.AppendLine("        if (length > 0)");
            sb.AppendLine("        {");
            sb.Append("            value.Items = ResolveFormatter<global::System.Collections.Generic.List<").Append(model.DtoTypeName)
                .Append(">>(options).Deserialize(ref reader, options);").AppendLine();
            sb.AppendLine("        }");
            sb.AppendLine("        for (var index = 1; index < length; index++)");
        }
        else
        {
            sb.AppendLine("        if (length > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            value.HasValue = ResolveFormatter<bool>(options).Deserialize(ref reader, options);");
            sb.AppendLine("        }");
            sb.AppendLine("        if (length > 1)");
            sb.AppendLine("        {");
            sb.Append("            value.Item = ResolveFormatter<").Append(model.DtoTypeName).AppendLine("?>(options).Deserialize(ref reader, options);");
            sb.AppendLine("        }");
            sb.AppendLine("        for (var index = 2; index < length; index++)");
        }

        sb.AppendLine("        {");
        sb.AppendLine("            reader.Skip();");
        sb.AppendLine("        }");
        sb.AppendLine("        return value;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        AppendNamespaceEnd(sb, model.FormatterNamespace);
        return sb.ToString();
    }

    /// <summary>
    /// エンティティメンバーを DTO フィールドへマッピングする式を構築します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <param name="member">変換対象のメンバー。</param>
    /// <param name="memberIndex">永続化対象メンバー一覧内でのメンバーインデックス。</param>
    /// <param name="fieldIndex">そのメンバーに対応する展開済み DTO フィールド一覧内でのフィールドインデックス。</param>
    /// <returns>C# の式文字列。</returns>
    private static string GetToDtoExpression(EntityModel model, MemberModel member, int memberIndex, int fieldIndex)
    {
        if (member.ValueObjectShape is null)
        {
            return "entity." + member.AccessibleName;
        }

        if (member.ValueObjectShape.PrimitiveParts.Length == 1)
        {
            return "entity." + member.AccessibleName + "." + member.ValueObjectShape.ToPrimitiveMethodName + "()";
        }

        return "primitive" + memberIndex + "." + member.DtoFields[fieldIndex].TupleAccessName;
    }

    /// <summary>
    /// DTO から 1 つのコンストラクタ引数を復元する式を構築します。
    /// </summary>
    /// <param name="member">再構築対象のメンバー。</param>
    /// <returns>C# の式文字列。</returns>
    private static string GetFromDtoArgumentExpression(MemberModel member)
    {
        if (member.ValueObjectShape is null)
        {
            return "dto." + member.DtoFields[0].Name;
        }

        if (member.ValueObjectShape.PrimitiveParts.Length == 1)
        {
            return CreateValueObjectExpression(member, new[] { "dto." + member.DtoFields[0].Name });
        }

        var arguments = member.DtoFields.Select(static field => "dto." + field.Name).ToArray();
        return CreateValueObjectExpression(member, arguments);
    }

    /// <summary>
    /// プリミティブ DTO 引数から値オブジェクトを復元する式を作成します。
    /// </summary>
    /// <param name="member">値オブジェクトを生成する対象のメンバー。</param>
    /// <param name="argumentExpressions">プリミティブ引数の式。</param>
    /// <returns>C# の式文字列。</returns>
    private static string CreateValueObjectExpression(MemberModel member, IReadOnlyList<string> argumentExpressions)
    {
        var arguments = string.Join(", ", argumentExpressions);
        if (member.ValueObjectShape!.CreationKind == ValueObjectCreationKind.StaticFactory)
        {
            return member.TypeName + "." + member.ValueObjectShape.CreationMemberName + "(" + arguments + ")";
        }

        return "new " + member.TypeName + "(" + arguments + ")";
    }

    /// <summary>
    /// エンティティインスタンスからエンティティキーを読み取る式を構築します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>C# の式文字列。</returns>
    private static string GetEntityKeyExpression(EntityModel model)
    {
        if (model.KeyMembers.Length == 1)
        {
            return "entity." + model.KeyMembers[0].AccessibleName;
        }

        return "(" + string.Join(", ", model.KeyMembers.Select(static member => "entity." + member.AccessibleName)) + ")";
    }

    /// <summary>
    /// DTO インスタンスからエンティティキーを読み取る式を構築します。
    /// </summary>
    /// <param name="model">解析済みのエンティティモデル。</param>
    /// <returns>C# の式文字列。</returns>
    private static string GetDtoKeyExpression(EntityModel model)
    {
        if (model.KeyMembers.Length == 1)
        {
            return GetFromDtoArgumentExpression(model.KeyMembers[0]);
        }

        return "(" + string.Join(", ", model.KeyMembers.Select(GetFromDtoArgumentExpression)) + ")";
    }
}
