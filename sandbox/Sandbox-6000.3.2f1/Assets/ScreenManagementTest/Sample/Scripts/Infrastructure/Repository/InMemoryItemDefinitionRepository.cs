using System.Collections.Generic;
using System.Linq;
using ScreenManagementSample.Domain;

namespace ScreenManagementSample.Infrastructure
{
    /// <summary>
    /// アイテム定義のインメモリリポジトリ
    /// </summary>
    public class InMemoryItemDefinitionRepository : Application.IItemDefinitionRepository
    {
        private readonly Dictionary<ItemId, ItemDefinition> _definitions;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public InMemoryItemDefinitionRepository()
        {
            // サンプルアイテムを定義
            var items = new[]
            {
                // 消費アイテム
                new ItemDefinition(
                    id: "potion",
                    name: "回復薬",
                    description: "HPを30回復する",
                    type: ItemType.Consumable,
                    isStackable: true,
                    maxStackCount: 99
                ),
                new ItemDefinition(
                    id: "hi_potion",
                    name: "ハイポーション",
                    description: "HPを80回復する",
                    type: ItemType.Consumable,
                    isStackable: true,
                    maxStackCount: 99
                ),

                // 装備品
                new ItemDefinition(
                    id: "iron_sword",
                    name: "鉄の剣",
                    description: "攻撃力+10",
                    type: ItemType.Equipment,
                    isStackable: false
                ),
                new ItemDefinition(
                    id: "leather_armor",
                    name: "革の鎧",
                    description: "防御力+5",
                    type: ItemType.Equipment,
                    isStackable: false
                ),

                // 素材
                new ItemDefinition(
                    id: "iron_ore",
                    name: "鉄鉱石",
                    description: "鍛冶に使う素材",
                    type: ItemType.Material,
                    isStackable: true,
                    maxStackCount: 999
                ),

                // キーアイテム
                new ItemDefinition(
                    id: "magic_key",
                    name: "魔法の鍵",
                    description: "封印された扉を開ける不思議な鍵",
                    type: ItemType.KeyItem,
                    isStackable: false
                )
            };

            _definitions = items.ToDictionary(i => i.Id);
        }

        /// <inheritdoc />
        public ItemDefinition Get(ItemId id)
        {
            return _definitions.TryGetValue(id, out var definition) ? definition : null;
        }

        /// <inheritdoc />
        public ItemDefinition[] GetAll()
        {
            return _definitions.Values.ToArray();
        }
    }
}
