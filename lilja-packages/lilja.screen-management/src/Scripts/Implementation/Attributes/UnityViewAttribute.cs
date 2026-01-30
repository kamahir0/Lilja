using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// この属性を持つフィールドにはView参照が注入されます
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class UnityViewAttribute : Attribute { }
}
