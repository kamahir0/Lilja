using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Repository.Internal
{
internal interface IRepositoryParticipant
{
    UniTask PrepareCommitAsync(object transactionState, CancellationToken ct);

    void ApplyCommit(object transactionState);
}
}
