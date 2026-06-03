#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.Persistence
{
    public abstract class KeyedRepository<TKey, TData>
        where TData : class, IKeyed<TKey>
    {
        public abstract UniTask<TData> LoadAsync(TKey key, CancellationToken ct = default);

        public abstract UniTask<IReadOnlyList<TData>> LoadAllAsync(CancellationToken ct = default);

        public abstract UniTask SaveAsync(TData data, CancellationToken ct = default);

        public abstract bool Exists(TKey key);
    }
}
