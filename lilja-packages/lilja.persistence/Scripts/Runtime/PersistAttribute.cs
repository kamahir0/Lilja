using System;

namespace Lilja.Persistence
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        public PersistAttribute(int index)
        {
            Index = index;
        }

        public int Index { get; }
    }
}
