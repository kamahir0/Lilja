using Lilja.Repository;

namespace Lilja.Repository.Test
{
    public struct TestValueObject
    {
        public int X;
        public int Y;

        public TestValueObject(int x, int y)
        {
            X = x;
            Y = y;
        }

        [ToPrimitive]
        public (int x, int y) ToPrimitive()
        {
            return (X, Y);
        }
    }

    [Entity]
    public partial class TestEntity
    {
        [Key]
        [Persist(0)]
        private string _id;

        [Persist(1)]
        private TestValueObject _value;

        [Persist(2)]
        private string _description;

        public string Id => _id;
        public TestValueObject Value => _value;
        public string Description => _description;

        public TestEntity(string id, TestValueObject value, string description)
        {
            _id = id;
            _value = value;
            _description = description;
        }

        public void SetData(TestValueObject value, string description)
        {
            _value = value;
            _description = description;
        }
    }
}
