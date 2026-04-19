using System.Text;
using Lilja.Repository.Analyzer.Models;

namespace Lilja.Repository.Analyzer.Emitters;

internal static class RepositoryEmitterCommon
{
    public static StringBuilder BeginRepositoryClass(EntityInfo entity, string className, string baseTypeName)
    {
        return new StringBuilder().Append($@"#nullable enable

namespace {entity.RepositoryNamespace}
{{
    /// <summary>
    /// {entity.ClassName}のリポジトリ実装。
    /// </summary>
    public sealed class {className} : {baseTypeName}, I{entity.ClassName}Repository
    {{
");
    }

    public static string EndRepositoryClass(StringBuilder builder)
    {
        return builder.Append(
@"    }
}
").ToString();
    }
}
