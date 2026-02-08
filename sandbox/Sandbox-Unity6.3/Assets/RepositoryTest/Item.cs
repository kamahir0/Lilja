using Lilja.Repository;

namespace RepositoryTest
{
    /// <summary>
    /// アイテムを表すEntity。
    /// Source Generatorによってリポジトリ・DTOが自動生成される。
    /// </summary>
    [Entity]
    public partial class Item
    {
        [Key] [Persist(0)] private readonly string _userId;

        /// <summary>
        /// アイテムID（主キー）。
        /// </summary>
        [Key] [Persist(1)] private readonly int _id;

        /// <summary>
        /// アイテム名。
        /// </summary>
        [Persist(2)] private readonly string _name;

        /// <summary>
        /// アイテムの位置座標。
        /// ValueObjectのフラット化をテストするためのフィールド。
        /// </summary>
        [Persist(3)] private Coordinate _position;

        public void MoveX(int step)
        {
            _position = new Coordinate { X = _position.X + step, Y = _position.Y };
        }

        public void MoveY(int step)
        {
            _position = new Coordinate { X = _position.X, Y = _position.Y + step };
        }
    }
}
