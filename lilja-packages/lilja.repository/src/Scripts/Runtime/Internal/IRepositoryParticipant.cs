using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// Defines the hooks a repository uses to participate in a transaction commit.
    /// </summary>
    internal interface IRepositoryParticipant
    {
        /// <summary>
        /// Prepares the repository-specific transaction state for commit.
        /// </summary>
        /// <param name="transactionState">The state created for the current transaction.</param>
        /// <param name="ct">A token that can cancel preparation.</param>
        /// <returns>A task that completes when preparation finishes.</returns>
        UniTask PrepareCommitAsync(object transactionState, CancellationToken ct);

        /// <summary>
        /// Applies a previously prepared transaction state to the committed repository state.
        /// </summary>
        /// <param name="transactionState">The state created for the current transaction.</param>
        void ApplyCommit(object transactionState);
    }
}
