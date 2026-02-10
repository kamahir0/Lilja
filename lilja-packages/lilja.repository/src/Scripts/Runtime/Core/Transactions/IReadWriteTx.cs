using System;

namespace Lilja.Repository
{
    /// <summary>
    /// 読み書き可能トランザクションのI/F。
    /// </summary>
    public interface IReadWriteTx : IReadOnlyTx
    {
        /// <summary>
        /// コミット時に実行するアクションを登録する。
        /// </summary>
        /// <param name="action">コミット時に実行するアクション。</param>
        void OnCommit(Action action);

        /// <summary>
        /// ロールバック時に実行するアクションを登録する。
        /// </summary>
        /// <param name="action">ロールバック時に実行するアクション。</param>
        void OnRollback(Action action);
    }
}
