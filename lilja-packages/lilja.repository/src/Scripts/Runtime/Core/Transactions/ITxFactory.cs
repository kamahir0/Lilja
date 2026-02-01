namespace Lilja.Repository
{
    /// <summary>
    /// トランザクションファクトリのI/F。
    /// </summary>
    public interface ITxFactory
    {
        /// <summary>
        /// 読み取り専用トランザクションを開始する。
        /// </summary>
        /// <returns>読み取り専用トランザクション。</returns>
        IReadableTx BeginRead();

        /// <summary>
        /// 読み書き可能トランザクションを開始する。
        /// </summary>
        /// <returns>読み書き可能トランザクション。</returns>
        IReadWriteTx BeginWrite();
    }
}
