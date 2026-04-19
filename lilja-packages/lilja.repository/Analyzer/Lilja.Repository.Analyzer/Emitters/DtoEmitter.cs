using System;
using System.Linq;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

/// <summary>
/// DTO生成。
/// </summary>
internal static class DtoEmitter
{
    public static string Emit(EntityInfo entity)
    {
        var fieldDeclarations = string.Join(
            Environment.NewLine,
            entity.PersistMembers.SelectMany(EmitterSupport.EnumerateDtoFieldDeclarations));

        return $$"""
#nullable enable

namespace {{entity.DtoNamespace}}
{
    /// <summary>
    /// {{entity.ClassName}}のDTO。
    /// </summary>
    [global::System.Serializable]
    public sealed class {{entity.ClassName}}Dto
    {
{{fieldDeclarations}}
    }
}
""";
    }
}
