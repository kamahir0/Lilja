// namespace Lilja.Repository.Sample
// {
//     /// <summary>
//     /// アイテムを表すEntity。
//     /// Source Generatorによってリポジトリ・DTOが自動生成される。
//     /// </summary>
//     [Entity]
//     public partial class Item
//     {
//         /// <summary>
//         /// アイテムID（主キー）。
//         /// </summary>
//         [Key]
//         [Persist(0)]
//         private int _id;
//
//         /// <summary>
//         /// アイテム名。
//         /// </summary>
//         [Persist(1)]
//         private string _name;
//
//         /// <summary>
//         /// アイテムの位置座標。
//         /// ValueObjectのフラット化をテストするためのフィールド。
//         /// </summary>
//         [Persist(2)]
//         private Coordinate _position;
//
//         /// <summary>
//         /// コンストラクタ。
//         /// </summary>
//         /// <param name="id">アイテムID。</param>
//         /// <param name="name">アイテム名。</param>
//         /// <param name="position">位置座標。</param>
//         public Item(int id, string name, Coordinate position)
//         {
//             _id = id;
//             _name = name;
//             _position = position;
//         }
//     }
// }
