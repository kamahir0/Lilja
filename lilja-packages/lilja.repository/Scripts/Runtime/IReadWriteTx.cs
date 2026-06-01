namespace Lilja.Repository
{
    /// <summary>
    /// 書き込みのステージングを許可するリポジトリトランザクションスコープを表します。
    /// </summary>
    public interface IReadWriteTx : IReadOnlyTx
    {
    }
}
