using System;

namespace Lilja.Repository
{
/// <summary>
/// Marks an instance method that exposes the primitive representation used for persistence.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToPrimitiveAttribute : Attribute
{
}
}
