#nullable enable

namespace Lilja.Persistence
{
    public interface IKeyed<TKey>
    {
        TKey Key { get; }
    }
}
