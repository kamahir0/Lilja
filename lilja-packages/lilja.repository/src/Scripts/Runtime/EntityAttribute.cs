using System;

namespace Lilja.Repository
{
    /// <summary>
    /// partial クラスを、ソースジェネレーターが扱うリポジトリエンティティとして示します。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityAttribute : Attribute
    {
    }
}
