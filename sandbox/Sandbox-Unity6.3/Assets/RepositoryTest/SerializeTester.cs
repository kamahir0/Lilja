// using Lilja.Repository.Generated.Dtos.RepositoryTest;
// using Lilja.Repository.Generated.Formatters.RepositoryTest;
// using MessagePack;
// using MessagePack.Formatters;
// using MessagePack.Resolvers;
// using UnityEngine;

// namespace RepositoryTest
// {
//     public class SerializeTester : MonoBehaviour
//     {
//         private void Start()
//         {
//             // Formatterを登録
//             var resolver = CompositeResolver.Create(
//                 new IMessagePackFormatter[] { new ItemDtoFormatter() },
//                 new IFormatterResolver[] { StandardResolver.Instance }
//             );
//             var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);

//             var dto = new ItemDto
//             {
//                 Id = 27,
//                 Name = "Test",
//                 Position_x = 1,
//                 Position_y = 9
//             };

//             // optionsを渡す
//             var binary = MessagePackSerializer.Serialize(dto, options);
//             var dto2 = MessagePackSerializer.Deserialize<ItemDto>(binary, options);

//             Debug.Log($"Id: {dto2.Id}, Name: {dto2.Name}, Position: ({dto2.Position_x}, {dto2.Position_y})");
//         }
//     }
// }