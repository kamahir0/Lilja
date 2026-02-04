using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// 座標を表すValueObject。
    /// [ToPrimitive]属性によりValueObjectとして認識される。
    /// </summary>
    public readonly struct Coordinate
    {
        /// <summary>
        /// X座標。
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Y座標。
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="x">X座標。</param>
        /// <param name="y">Y座標。</param>
        public Coordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// プリミティブ型に変換する。
        /// Source Generatorがこのメソッドを検出してDTOにフラット化する。
        /// </summary>
        /// <returns>X, Y座標のタプル。</returns>
        [ToPrimitive]
        public (int x, int y) Serialize()
        {
            return (X, Y);
        }
    }
}
