using System;

namespace Lilja.Persistence
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PersistableAttribute : Attribute
    {
        public bool IsRoot { get; set; }
    }
}
