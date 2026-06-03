using System;

namespace Lilja.Persistence
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ToPrimitiveAttribute : Attribute
    {
    }
}
