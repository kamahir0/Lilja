using System;

namespace Lilja.Repository
{
    /// <summary>
    /// 永続化に使うプリミティブ表現を公開するインスタンスメソッドであることを示します。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ToPrimitiveAttribute : Attribute
    {
    }
}
