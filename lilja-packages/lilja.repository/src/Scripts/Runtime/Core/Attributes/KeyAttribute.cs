using System;

namespace Lilja.Repository
{
    /// <summary>
    /// 主キーフィールドをマークする属性。
    /// Entityの識別子として使用されるフィールドに付与する。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class KeyAttribute : Attribute
    {
    }
}
