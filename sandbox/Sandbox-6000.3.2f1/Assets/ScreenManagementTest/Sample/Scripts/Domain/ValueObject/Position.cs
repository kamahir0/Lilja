namespace ScreenManagementSample.Domain
{
    /// <summary>
    /// マップ上の位置を表す値オブジェクト
    /// </summary>
    public readonly struct Position
    {
        public int X { get; }
        public int Y { get; }

        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        public Position Move(int dx, int dy) => new(X + dx, Y + dy);

        public override string ToString() => $"({X}, {Y})";

        public override bool Equals(object obj) => obj is Position other && X == other.X && Y == other.Y;
        public override int GetHashCode() => X * 31 + Y;

        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);
    }
}
