using System;

namespace Lilja.Repository
{
    /// <summary>
    /// ValueObjectのプリミティブ変換メソッドをマークする属性。
    /// この属性が付与されたメソッドを持つ型はValueObjectとして扱われる。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ToPrimitiveAttribute : Attribute
    {
    }
}
