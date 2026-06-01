using System;

namespace Lilja.Repository
{
    /// <summary>
    /// フィールドまたは自動実装プロパティを、生成されるリポジトリキーの一部として示します。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class KeyAttribute : Attribute
    {
    }
}
