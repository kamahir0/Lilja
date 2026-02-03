// using MessagePack;
//
// namespace Lilja.Repository
// {
//     public class GameData
//     {
//         public int Score;
//         public string PlayerName;
//     }
//
//     public class NewEmptyCSharpScript
//     {
//         public void Test()
//         {
//             var data = new GameData { Score = 1000, PlayerName = "Player1" };
//             byte[] bytes = MessagePackSerializer.Serialize(data); // シリアライズ
//
//             GameData _ = MessagePackSerializer.Deserialize<GameData>(bytes); // デシリアライズ
//         }
//     }
// }
