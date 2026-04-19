using System.Collections.Generic;
using System.Linq;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

internal static class EmitterSupport
{
    public static string Qualify(string typeName)
    {
        return typeName.StartsWith("global::", System.StringComparison.Ordinal)
            ? typeName
            : $"global::{typeName}";
    }

    public static string GetKeyTypeName(EntityInfo entity)
    {
        if (!entity.HasKey)
        {
            return string.Empty;
        }

        if (!entity.IsCompositeKey)
        {
            return entity.KeyMembers[0].TypeName;
        }

        return $"({string.Join(", ", entity.KeyMembers.Select(static member => member.TypeName))})";
    }

    public static string GetKeyParamName(EntityInfo entity)
    {
        if (!entity.HasKey)
        {
            return string.Empty;
        }

        return entity.IsCompositeKey ? "key" : entity.KeyMembers[0].ParameterName;
    }

    public static string GetEntityKeyExpression(EntityInfo entity, string entityExpression)
    {
        if (!entity.IsCompositeKey)
        {
            return GetEntityMemberAccess(entity.KeyMembers[0], entityExpression);
        }

        return $"({string.Join(", ", entity.KeyMembers.Select(member => GetEntityMemberAccess(member, entityExpression)))})";
    }

    public static string GetDtoKeyExpression(EntityInfo entity, string dtoExpression)
    {
        if (!entity.IsCompositeKey)
        {
            return GetDtoMemberValueExpression(entity.KeyMembers[0], dtoExpression);
        }

        return $"({string.Join(", ", entity.KeyMembers.Select(member => GetDtoMemberValueExpression(member, dtoExpression)))})";
    }

    public static string GetEntityMemberAccess(EntityMemberInfo member, string instanceExpression)
    {
        return $"{instanceExpression}.{member.MemberName}";
    }

    public static string GetDtoFieldName(EntityMemberInfo member)
    {
        return member.DtoFieldName;
    }

    public static string GetDtoFieldName(EntityMemberInfo member, TupleElementInfo element)
    {
        return CodeGenHelpers.EscapeIdentifier($"{CodeGenHelpers.ToPascalCase(member.Name)}_{element.Name}");
    }

    public static string GetDtoFieldAccess(EntityMemberInfo member, string dtoExpression)
    {
        return $"{dtoExpression}.{GetDtoFieldName(member)}";
    }

    public static string GetDtoFieldAccess(EntityMemberInfo member, TupleElementInfo element, string dtoExpression)
    {
        return $"{dtoExpression}.{GetDtoFieldName(member, element)}";
    }

    public static string GetDtoMemberValueExpression(EntityMemberInfo member, string dtoExpression)
    {
        if (!member.ValueObjectInfo.IsValueObject)
        {
            return GetDtoFieldAccess(member, dtoExpression);
        }

        var args = string.Join(", ", member.ValueObjectInfo.TupleElements.Select(
            element => GetDtoFieldAccess(member, element, dtoExpression)));

        if (member.ValueObjectInfo.IsFromPrimitiveStatic)
        {
            return $"{member.TypeName}.{member.ValueObjectInfo.FromPrimitiveMethodName}({args})";
        }

        return $"new {member.TypeName}({args})";
    }

    public static IEnumerable<string> EnumerateDtoFieldDeclarations(EntityMemberInfo member)
    {
        if (!member.ValueObjectInfo.IsValueObject)
        {
            yield return $"        public {member.TypeName} {GetDtoFieldName(member)} = default!;";
            yield break;
        }

        foreach (var element in member.ValueObjectInfo.TupleElements)
        {
            yield return $"        public {element.TypeName} {GetDtoFieldName(member, element)} = default!;";
        }
    }
}
