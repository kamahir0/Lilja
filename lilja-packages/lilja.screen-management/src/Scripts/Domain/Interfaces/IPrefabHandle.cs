using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Prefab をロード・解放するハンドルの I/F
    /// </summary>
    public interface IPrefabHandle
    {
        /// <summary>
        /// Prefab を非同期でロードします
        /// </summary>
        public UniTask<GameObject> LoadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Prefab を解放します
        /// </summary>
        public void Release();
    }
}
