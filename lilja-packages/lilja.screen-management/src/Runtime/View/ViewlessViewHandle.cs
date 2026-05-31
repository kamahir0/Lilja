using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// ビュー（GameObject）の実体を持たない論理画面用のダミービューハンドルクラス。
    /// </summary>
    internal sealed class ViewlessViewHandle : IViewHandle
    {
        /// <summary>
        /// シングルトンインスタンスを取得します。
        /// </summary>
        public static readonly ViewlessViewHandle Instance = new();

        private ViewlessViewHandle()
        {
        }

        #region IViewHandle

        /// <inheritdoc />
        public GameObject[] RootObjects => Array.Empty<GameObject>();

        /// <inheritdoc />
        public bool IsLoaded => true;

        /// <inheritdoc />
        public bool IsUnloadedTemporarily { get; set; }

        /// <inheritdoc />
        public bool UnloadsAncestors => false;

        /// <inheritdoc />
        public void Initialize(Type ownerType)
        {
        }

        /// <inheritdoc />
        public UniTask PreloadAsync(GameScreenContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask LoadAsync(GameScreenContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        public UniTask UnloadAsync(CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        #endregion
    }
}
