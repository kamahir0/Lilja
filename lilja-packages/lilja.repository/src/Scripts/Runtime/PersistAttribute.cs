using System;

namespace Lilja.Repository
{
    /// <summary>
    /// フィールドまたは自動実装プロパティを永続化対象として示し、そのシリアライズ順を定義します。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class PersistAttribute : Attribute
    {
        /// <summary>
        /// <see cref="PersistAttribute"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="index">
        /// ジェネレーターが永続化対象メンバーを並べる際に使う 0 始まりの位置。
        /// </param>
        public PersistAttribute(int index)
        {
            Index = index;
        }

        /// <summary>
        /// 注釈が付いたメンバーの 0 始まりの永続化順序を取得します。
        /// </summary>
        public int Index { get; }
    }
}
