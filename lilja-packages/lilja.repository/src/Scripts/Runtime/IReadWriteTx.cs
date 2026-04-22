namespace Lilja.Repository
{
    /// <summary>
    /// Represents a repository transaction scope that allows staged writes.
    /// </summary>
    public interface IReadWriteTx : IReadOnlyTx
    {
    }
}
