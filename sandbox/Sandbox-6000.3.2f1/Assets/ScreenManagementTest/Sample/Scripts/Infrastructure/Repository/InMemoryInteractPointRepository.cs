using System.Collections.Generic;
using System.Linq;
using ScreenManagementSample.Domain;

namespace ScreenManagementSample.Infrastructure
{
    /// <summary>
    /// インタラクトポイントのインメモリリポジトリ
    /// </summary>
    public class InMemoryInteractPointRepository : Application.IInteractPointRepository
    {
        private readonly List<InteractPoint> _interactPoints = new List<InteractPoint>();
        private readonly System.Random _random = new System.Random();

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public InMemoryInteractPointRepository()
        {
            GenerateInteractPoints();
        }

        /// <inheritdoc />
        public IReadOnlyList<InteractPoint> GetAll() => _interactPoints;

        /// <inheritdoc />
        public InteractPoint GetAt(Position position)
        {
            return _interactPoints.FirstOrDefault(p => p.Position == position && p.IsActive);
        }

        /// <inheritdoc />
        public void Reset()
        {
            _interactPoints.Clear();
            GenerateInteractPoints();
        }

        /// <summary>
        /// インタラクトポイントをランダム生成
        /// </summary>
        private void GenerateInteractPoints()
        {
            // 5x5マップで3〜5個のインタラクトポイントをランダム配置
            var count = _random.Next(3, 6);
            var usedPositions = new HashSet<(int, int)>();

            // 開始位置(0,0)は除外
            usedPositions.Add((0, 0));

            // 取得可能なアイテム候補
            var itemOptions = new[]
            {
                ("potion", 1),
                ("potion", 2),
                ("hi_potion", 1),
                ("iron_ore", 3)
            };

            while (_interactPoints.Count < count)
            {
                var x = _random.Next(0, 5);
                var y = _random.Next(0, 5);

                if (usedPositions.Contains((x, y)))
                {
                    continue;
                }

                usedPositions.Add((x, y));

                // ランダムにアイテムを選択
                var (itemId, itemCount) = itemOptions[_random.Next(itemOptions.Length)];

                _interactPoints.Add(new InteractPoint(
                    position: new Position(x, y),
                    itemId: itemId,
                    itemCount: itemCount
                ));
            }
        }
    }
}
