using Lilja.Repository;

// External init
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace RepositoryTest
{
    /// <summary>
    /// 座標を表すValueObject。
    /// [ToPrimitive]属性によりValueObjectとして認識される。
    /// </summary>
    public readonly struct Coordinate
    {
        public int X { get; init; }

        public int Y { get; init; }

        /// <summary>
        /// プリミティブ型から復元する（staticメソッド）
        /// </summary>
        [FromPrimitive]
        internal static Coordinate FromPrimitive(int x, int y)
        {
            return new Coordinate { X = x, Y = y };
        }

        // NOTE: コンストラクタでもOK
        // [FromPrimitive]
        // internal Coordinate(int x, int y)
        // {
        //     X = x;
        //     Y = y;
        // }

        /// <summary>
        /// プリミティブ型に変換する（タプルを返す）
        /// </summary>
        [ToPrimitive]
        internal (int x, int y) ToPrimitive()
        {
            return (X, Y);
        }
    }
}
