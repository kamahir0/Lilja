using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Screen が扱うビューのハンドルの I/F
    /// </summary>
    public interface IViewHandle
    {
        /// <summary> ビューのルートオブジェクト </summary>
        public GameObject[] RootObjects { get; }

        /// <summary>
        /// ビューを非同期でロードします
        /// </summary>
        public UniTask LoadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// ビューをアンロードします
        /// </summary>
        public void Unload();
    }
}