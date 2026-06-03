#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Persistence
{
    public abstract class Repository<TData>
        where TData : class
    {
        public abstract UniTask<TData> LoadAsync(CancellationToken ct = default);

        public abstract UniTask SaveAsync(TData data, CancellationToken ct = default);
    }
}
