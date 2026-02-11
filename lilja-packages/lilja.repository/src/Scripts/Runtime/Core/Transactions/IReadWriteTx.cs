using System;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository
{
    /// <summary>
    /// 読み書き可能トランザクションのI/F。
    /// </summary>
    public interface IReadWriteTx : IReadOnlyTx
    {
        /// <summary>
        /// コミット時に実行する非同期アクションを登録する。
        /// </summary>
        /// <param name="asyncAction">コミット時に実行する非同期アクション。</param>
        void OnCommit(Func<UniTask> asyncAction);

        /// <summary>
        /// ロールバック時に実行する非同期アクションを登録する。
        /// </summary>
        /// <param name="asyncAction">ロールバック時に実行する非同期アクション。</param>
        void OnRollback(Func<UniTask> asyncAction);
    }
}
