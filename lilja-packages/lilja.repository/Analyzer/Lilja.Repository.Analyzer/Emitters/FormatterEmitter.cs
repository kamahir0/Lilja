using System;
using System.Collections.Generic;
using System.Linq;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// MessagePack formatter 生成。
/// </summary>
internal static class FormatterEmitter
{
    public static string EmitDtoFormatter(EntityInfo entity)
    {
        var flattenedFields = entity.PersistMembers
            .SelectMany(EnumerateFlattenedFieldDescriptors)
            .ToArray();
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var serializeLines = string.Join(
            Environment.NewLine,
            flattenedFields.Select(field =>
                $"            global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<{field.TypeName}>(options.Resolver).Serialize(ref writer, value.{field.FieldName}, options);"));
        var deserializeAssignments = string.Join(
            Environment.NewLine,
            flattenedFields.Select((field, index) => $$"""
            if (count > {{index}})
            {
                result.{{field.FieldName}} = global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<{{field.TypeName}}>(options.Resolver).Deserialize(ref reader, options)!;
            }
"""));

        return $$"""
#nullable enable

namespace {{entity.FormatterNamespace}}
{
    /// <summary>
    /// {{entity.ClassName}}Dto 用の MessagePack formatter。
    /// </summary>
    public sealed class {{entity.ClassName}}DtoFormatter : global::MessagePack.Formatters.IMessagePackFormatter<{{dtoTypeName}}>
    {
        public void Serialize(ref global::MessagePack.MessagePackWriter writer, {{dtoTypeName}} value, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader({{flattenedFields.Length}});
{{serializeLines}}
        }

        public {{dtoTypeName}} Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null!;
            }

            var count = reader.ReadArrayHeader();
            var result = new {{dtoTypeName}}();

{{deserializeAssignments}}

            for (var index = {{flattenedFields.Length}}; index < count; index++)
            {
                reader.Skip();
            }

            return result;
        }
    }
}
""";
    }

    public static string EmitStorageEnvelopeFormatter(EntityInfo entity)
    {
        var envelopeTypeName = EmitterSupport.Qualify(entity.StorageEnvelopeTypeName);
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var serializeBody = entity.HasKey
            ? $$"""
            writer.WriteArrayHeader(1);
            global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<global::System.Collections.Generic.List<{{dtoTypeName}}>>(options.Resolver).Serialize(ref writer, value.Items, options);
"""
            : $$"""
            writer.WriteArrayHeader(2);
            global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<bool>(options.Resolver).Serialize(ref writer, value.HasValue, options);
            global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<{{dtoTypeName}}>(options.Resolver).Serialize(ref writer, value.Item!, options);
""";
        var deserializeBody = entity.HasKey
            ? $$"""
            if (count > 0)
            {
                result.Items = global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<global::System.Collections.Generic.List<{{dtoTypeName}}>>(options.Resolver).Deserialize(ref reader, options) ?? new global::System.Collections.Generic.List<{{dtoTypeName}}>();
            }
            for (var index = 1; index < count; index++)
            {
                reader.Skip();
            }
"""
            : $$"""
            if (count > 0)
            {
                result.HasValue = global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<bool>(options.Resolver).Deserialize(ref reader, options);
            }
            if (count > 1)
            {
                result.Item = global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<{{dtoTypeName}}>(options.Resolver).Deserialize(ref reader, options);
            }
            for (var index = 2; index < count; index++)
            {
                reader.Skip();
            }
""";

        return $$"""
#nullable enable

namespace {{entity.FormatterNamespace}}
{
    internal sealed class {{entity.ClassName}}StorageEnvelopeFormatter : global::MessagePack.Formatters.IMessagePackFormatter<{{envelopeTypeName}}>
    {
        public void Serialize(ref global::MessagePack.MessagePackWriter writer, {{envelopeTypeName}} value, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

{{serializeBody}}
        }

        public {{envelopeTypeName}} Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null!;
            }

            var count = reader.ReadArrayHeader();
            var result = new {{envelopeTypeName}}();
{{deserializeBody}}
            return result;
        }
    }
}
""";
    }

    private static IEnumerable<(string FieldName, string TypeName)> EnumerateFlattenedFieldDescriptors(
        EntityMemberInfo member)
    {
        if (!member.ValueObjectInfo.IsValueObject)
        {
            yield return (EmitterSupport.GetDtoFieldName(member), member.TypeName);
            yield break;
        }

        foreach (var element in member.ValueObjectInfo.TupleElements)
        {
            yield return (EmitterSupport.GetDtoFieldName(member, element), element.TypeName);
        }
    }
}
