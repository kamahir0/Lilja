using System;

namespace Lilja.Repository
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        public PersistAttribute()
        {
            Index = -1;
        }

        public PersistAttribute(int index)
        {
            Index = index;
        }

        public int Index { get; }

        public bool HasExplicitIndex => Index >= 0;
    }
}
