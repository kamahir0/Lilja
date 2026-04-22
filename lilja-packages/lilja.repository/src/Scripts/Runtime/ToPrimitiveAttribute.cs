using System;

namespace Lilja.Repository
{
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToPrimitiveAttribute : Attribute
{
}
}
