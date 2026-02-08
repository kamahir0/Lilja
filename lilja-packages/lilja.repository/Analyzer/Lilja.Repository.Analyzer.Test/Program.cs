Console.WriteLine("Source Generator Test");
Console.WriteLine("生成されるコード:");
Console.WriteLine("- ItemDto.g.cs");
Console.WriteLine("- Item.Transferable.g.cs");
Console.WriteLine("- ItemDtoFormatter.g.cs");
Console.WriteLine("- IItemRepository.g.cs");
Console.WriteLine("- InMemoryItemRepository.g.cs");

// Source Generatorテスト用サンプルコード
// 生成されたコードの確認用

// namespace Lilja.Repository.Sample
// {
//     /// <summary>
//     /// 座標を表すValueObject。
//     /// </summary>
//     public readonly struct Coordinate
//     {
//         public int X { get; }
//         public int Y { get; }
//
//         public Coordinate(int x, int y)
//         {
//             X = x;
//             Y = y;
//         }
//
//         [ToPrimitive]
//         public (int x, int y) Serialize() => (X, Y);
//     }
//
//     /// <summary>
//     /// アイテムを表すEntity。
//     /// </summary>
//     [Entity]
//     public partial class Item
//     {
//         [Key] [Persist(0)] private int _id;
//
//         [Persist(1)] private string _name;
//
//         [Persist(2)] private Coordinate _position;
//
//         public Item(int id, string name, Coordinate position)
//         {
//             _id = id;
//             _name = name;
//             _position = position;
//         }
//     }
//
//     [Entity]
//     public partial class ConfigData
//     {
//         [Persist(0)] public int _bgmVolume;
//     }
// }

#region DefinitionsForTest

namespace Lilja.Repository
{
    // 属性定義（テスト用にローカル定義）
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class EntityAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class KeyAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public class PersistAttribute(int index) : System.Attribute
    {
        public int Index { get; } = index;
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class ToPrimitiveAttribute : System.Attribute { }

    public interface ITransferable<T>
    {
        T ToDto();
        void FromDto(T dto);
    }
}

namespace MessagePack
{
    public enum MessagePackSerializerOptions{}
}

namespace MessagePack.Formatters
{
    public struct MessagePackWriter
    {
        public void WriteNil() { }
        public void WriteArrayHeader(int count) { }
        public void Write(object value) { }
    }

    public struct MessagePackReader
    {
        public bool TryReadNil() => true;
        public int ReadArrayHeader() => 0;
        public int ReadInt32() => 0;
        public string ReadString() => "";
    }

    public interface IMessagePackFormatter<T>
    {
        int Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options);
        T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options);
    }
}

#endregion
