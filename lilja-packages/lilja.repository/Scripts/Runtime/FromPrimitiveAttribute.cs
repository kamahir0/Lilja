using System;

namespace Lilja.Repository
{
    /// <summary>
    /// プリミティブ値から値オブジェクトを再構築するためのコンストラクタまたは静的ファクトリを示します。
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FromPrimitiveAttribute : Attribute
    {
    }
}
