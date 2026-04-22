using System;

namespace Lilja.Repository
{
[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FromPrimitiveAttribute : Attribute
{
}
}
