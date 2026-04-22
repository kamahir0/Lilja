using System;

namespace Lilja.Repository
{
    /// <summary>
    /// Marks a constructor or static factory used to rebuild a value object from primitive values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FromPrimitiveAttribute : Attribute
    {
    }
}
