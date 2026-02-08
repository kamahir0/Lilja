using System;

namespace Lilja.Repository
{
    /// <summary>
    /// ValueObjectのプリミティブ復元メソッドまたはコンストラクタをマークする属性。
    /// [ToPrimitive]と対になる属性で、DTOからValueObjectを復元する際に使用される。
    /// staticメソッドまたはコンストラクタに付与可能。
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FromPrimitiveAttribute : Attribute
    {
    }
}
