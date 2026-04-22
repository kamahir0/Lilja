using System;

namespace Lilja.Repository
{
    /// <summary>
    /// Marks a partial class as a repository entity handled by the source generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityAttribute : Attribute
    {
    }
}
