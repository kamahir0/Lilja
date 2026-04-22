using System;

namespace Lilja.Repository
{
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityAttribute : Attribute
{
}
}
