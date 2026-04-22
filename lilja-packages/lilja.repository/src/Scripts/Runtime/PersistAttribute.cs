using System;

namespace Lilja.Repository
{
    /// <summary>
    /// Marks a field or auto-property for persistence and defines its serialized order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PersistAttribute"/> class.
        /// </summary>
        /// <param name="index">
        /// The zero-based position used when the generator orders persisted members.
        /// </param>
        public PersistAttribute(int index)
        {
            Index = index;
        }

        /// <summary>
        /// Gets the zero-based persistence order for the annotated member.
        /// </summary>
        public int Index { get; }
    }
}
