#nullable enable
namespace Lilja.Repository.Internal
{
internal sealed class RepositoryWriteState<TValue>
{
    public RepositoryWriteState(TValue? value, bool hasValue)
    {
        Value = value;
        HasValue = hasValue;
    }

    public TValue? Value { get; set; }

    public bool HasValue { get; set; }
}
}
