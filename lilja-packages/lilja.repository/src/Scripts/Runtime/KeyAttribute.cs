using System;

namespace Lilja.Repository
{
/// <summary>
/// Marks a field or auto-property as part of the generated repository key.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class KeyAttribute : Attribute
{
}
}
