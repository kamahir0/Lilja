using Lilja.Repository;

namespace Lilja.Repository.Analyzer.Tests.Samples
{
    public readonly struct SampleCoordinate
    {
        public int X { get; }

        public int Y { get; }

        [FromPrimitive]
        public SampleCoordinate(int x, int y)
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
    public partial class ItemEntity
    {
        [Key]
        [Persist(0)]
        private readonly int _id;

        [Persist(1)]
        public string Name { get; }

        [Persist(2)]
        public SampleCoordinate Position { get; }

        public ItemEntity(int id, string name, SampleCoordinate position)
        {
            _id = id;
            Name = name;
            Position = position;
        }
    }

    [Entity]
    public partial class SettingsEntity
    {
        [Persist(0)]
        public int Volume { get; }

        public SettingsEntity(int volume)
        {
            Volume = volume;
        }
    }

    [Entity]
    public partial class FieldOnlyEntity
    {
        [Key]
        [Persist(0)]
        private readonly string _id;

        [Persist(1)]
        private readonly int _score;

        public FieldOnlyEntity(string id, int score)
        {
            _id = id;
            _score = score;
        }
    }
}

namespace Lilja.Repository.Analyzer.Tests.Samples.Inventory
{
    [Entity]
    public partial class SharedNameEntity
    {
        [Key]
        [Persist(0)]
        private readonly int _id;

        [Persist(1)]
        public string Name { get; }

        public SharedNameEntity(int id, string name)
        {
            _id = id;
            Name = name;
        }
    }
}

namespace Lilja.Repository.Analyzer.Tests.Samples.Profile
{
    [Entity]
    public partial class SharedNameEntity
    {
        [Key]
        [Persist(0)]
        private readonly int _id;

        [Persist(1)]
        public string Name { get; }

        public SharedNameEntity(int id, string name)
        {
            _id = id;
            Name = name;
        }
    }
}
