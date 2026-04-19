using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// KeyAccessor生成。
/// Entity partial classにinternal static GetKey/GetKeyFromDtoを追加する。
/// </summary>
internal static class KeyAccessorEmitter
{
    public static string Emit(EntityInfo entity)
    {
        if (!entity.HasKey)
        {
            return string.Empty;
        }

        var keyTypeName = EmitterSupport.GetKeyTypeName(entity);
        var entityTypeName = EmitterSupport.Qualify(entity.FullTypeName);
        var dtoTypeName = EmitterSupport.Qualify(entity.DtoTypeName);

        if (!string.IsNullOrEmpty(entity.Namespace))
        {
            var dtoAccessor = entity.HasPersistMembers
                ? $$"""

        internal static {{keyTypeName}} GetKeyFromDto({{dtoTypeName}} dto)
        {
            return {{EmitterSupport.GetDtoKeyExpression(entity, "dto")}};
        }
"""
                : string.Empty;

            return $$"""
#nullable enable

namespace {{entity.Namespace}}
{
    partial class {{entity.ClassName}}
    {
        internal static {{keyTypeName}} GetKey({{entityTypeName}} entity)
        {
            return {{EmitterSupport.GetEntityKeyExpression(entity, "entity")}};
        }{{dtoAccessor}}
    }
}
""";
        }

        var rootDtoAccessor = entity.HasPersistMembers
            ? $$"""

    internal static {{keyTypeName}} GetKeyFromDto({{dtoTypeName}} dto)
    {
        return {{EmitterSupport.GetDtoKeyExpression(entity, "dto")}};
    }
"""
            : string.Empty;

        return $$"""
#nullable enable

partial class {{entity.ClassName}}
{
    internal static {{keyTypeName}} GetKey({{entityTypeName}} entity)
    {
        return {{EmitterSupport.GetEntityKeyExpression(entity, "entity")}};
    }{{rootDtoAccessor}}
}
""";
    }
}
