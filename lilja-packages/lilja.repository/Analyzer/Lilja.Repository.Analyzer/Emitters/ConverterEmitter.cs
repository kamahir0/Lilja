using System;
using System.Linq;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// Converter生成（ToDto/FromDtoメソッドとprivateコンストラクタ）。
/// </summary>
internal static class ConverterEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var hasNamespace = !string.IsNullOrEmpty(entity.Namespace);
        var indent = hasNamespace ? "    " : string.Empty;
        var constructor = entity.NeedsConstructorGeneration
            ? EmitPrivateConstructor(entity, indent)
            : string.Empty;
        var toDto = EmitToDto(entity, indent);
        var fromDto = EmitFromDto(entity, indent);

        if (hasNamespace)
        {
            return $$"""
#nullable enable

namespace {{entity.Namespace}}
{
    partial class {{entity.ClassName}}
    {
{{constructor}}{{toDto}}{{fromDto}}
    }
}
""";
        }

        return $$"""
#nullable enable

partial class {{entity.ClassName}}
{
{{constructor}}{{toDto}}{{fromDto}}
}
""";
    }

    private static string EmitPrivateConstructor(EntityInfo entity, string indent)
    {
        var parameters = string.Join(
            ", ",
            entity.PersistMembers.Select(member => $"{member.TypeName} {member.ParameterName}"));
        var assignments = string.Join(
            Environment.NewLine,
            entity.PersistMembers.Select(member => $"{indent}        {member.MemberName} = {member.ParameterName};"));

        return $$"""
{{indent}}    private {{entity.ClassName}}({{parameters}})
{{indent}}    {
{{assignments}}
{{indent}}    }

""";
    }

    private static string EmitToDto(EntityInfo entity, string indent)
    {
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var entityTypeName = EmitterSupport.Qualify(entity.FullTypeName);
        var bodyLines = entity.PersistMembers.SelectMany(member => GetToDtoBodyLines(member, indent));
        var body = string.Join(Environment.NewLine, bodyLines);

        return $$"""
{{indent}}    internal static {{dtoTypeName}} ToDto({{entityTypeName}} entity)
{{indent}}    {
{{indent}}        var dto = new {{dtoTypeName}}();
{{body}}
{{indent}}        return dto;
{{indent}}    }

""";
    }

    private static string EmitFromDto(EntityInfo entity, string indent)
    {
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);
        var entityTypeName = EmitterSupport.Qualify(entity.FullTypeName);
        var constructorArguments = string.Join(
            ", ",
            entity.PersistMembers.Select(member => EmitterSupport.GetDtoMemberValueExpression(member, "dto")));

        return $$"""
{{indent}}    internal static {{entityTypeName}} FromDto({{dtoTypeName}} dto)
{{indent}}    {
{{indent}}        return new {{entity.ClassName}}({{constructorArguments}});
{{indent}}    }
""";
    }

    private static string[] GetToDtoBodyLines(EntityMemberInfo member, string indent)
    {
        if (!member.ValueObjectInfo.IsValueObject)
        {
            return new[]
            {
                $"{indent}        dto.{EmitterSupport.GetDtoFieldName(member)} = {EmitterSupport.GetEntityMemberAccess(member, "entity")};",
            };
        }

        var tupleVariableName = CodeGenHelpers.EscapeIdentifier($"{member.ParameterName}Primitive");
        return new[]
        {
            $"{indent}        var {tupleVariableName} = {EmitterSupport.GetEntityMemberAccess(member, "entity")}.{member.ValueObjectInfo.ToPrimitiveMethodName}();",
        }.Concat(member.ValueObjectInfo.TupleElements.Select(element =>
            $"{indent}        dto.{EmitterSupport.GetDtoFieldName(member, element)} = {tupleVariableName}.{element.EscapedName};"))
            .ToArray();
    }
}
