using System;

namespace Lilja.ScreenManagement
{

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ViewAttribute : Attribute { }
}
