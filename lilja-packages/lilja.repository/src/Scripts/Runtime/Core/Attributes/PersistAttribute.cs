using System;

namespace Lilja.Repository
{
    /// <summary>
    /// 永続化対象フィールドをマークする属性。
    /// indexでDTO内のフィールド順序を指定する。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        /// <summary>
        /// DTOフィールドのインデックス（順序）。
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="index">DTOフィールドのインデックス。</param>
        public PersistAttribute(int index)
        {
            Index = index;
        }
    }
}
