using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.AssetManagement
{
    public interface IAssetLoader
    {
        UniTask<T> LoadAsync<T>(string key, AssetLifetime lifetime, CancellationToken ct = default)
            where T : Object;
    }
}
