namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// ゲームマップエンティティ
    /// </summary>
    public class GameMap
    {
        /// <summary> マップ幅 </summary>
        public int Width { get; }

        /// <summary> マップ高さ </summary>
        public int Height { get; }

        public GameMap(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary> 指定位置がマップ内か判定 </summary>
        public bool IsInBounds(Position position)
        {
            return position.X >= 0 && position.X < Width &&
                   position.Y >= 0 && position.Y < Height;
        }
    }
}
