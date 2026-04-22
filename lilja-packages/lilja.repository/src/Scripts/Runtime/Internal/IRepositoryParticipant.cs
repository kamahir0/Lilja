using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// リポジトリがトランザクションコミットへ参加するためのフックを定義します。
    /// </summary>
    internal interface IRepositoryParticipant
    {
        /// <summary>
        /// コミットに向けてリポジトリ固有のトランザクション状態を準備します。
        /// </summary>
        /// <param name="transactionState">現在のトランザクション用に作成された状態。</param>
        /// <param name="ct">準備処理を取り消せるトークン。</param>
        /// <returns>準備処理が完了したときに完了するタスク。</returns>
        UniTask PrepareCommitAsync(object transactionState, CancellationToken ct);

        /// <summary>
        /// 事前に準備したトランザクション状態を、確定済みのリポジトリ状態へ反映します。
        /// </summary>
        /// <param name="transactionState">現在のトランザクション用に作成された状態。</param>
        void ApplyCommit(object transactionState);
    }
}
