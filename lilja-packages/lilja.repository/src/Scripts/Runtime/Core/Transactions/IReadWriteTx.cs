namespace Lilja.Repository
{
    /// <summary>
    /// 読み書き可能トランザクションのI/F。
    /// </summary>
    public interface IReadWriteTx : IReadableTx
    {
        /// <summary>
        /// トランザクションをコミットする。
        /// </summary>
        void Commit();

        /// <summary>
        /// トランザクションをロールバックする。
        /// </summary>
        void Rollback();
    }
}
