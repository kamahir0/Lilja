using System;

namespace Lilja.Repository
{
    /// <summary>
    /// Entityクラスをマークする属性。
    /// Source Generatorによってリポジトリ・DTOが自動生成される。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityAttribute : Attribute
    {
    }
}
