using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    ///  Prefab を使用する Overlay の基底クラス
    /// </summary>
    public abstract class PrefabOverlayBase<TArgs, TResult> : OverlayBase<TArgs, TResult>
    {
        #region For Implementers

        /// <summary>
        /// ビューを事前にロードします。
        /// </summary>
        public UniTask PreloadViewAsync(CancellationToken cancellationToken = default)
        {
            return _viewHandle.PreloadAsync(cancellationToken);
        }

        #endregion

        private readonly PrefabViewHandle _viewHandle;

        /// <summary> コンストラクタ </summary>
        protected PrefabOverlayBase()
        {
            var prefabHandle = Repository.Instance.PrefabHandleFactory.Invoke(GetPrefabKey());
            _viewHandle = new PrefabViewHandle(prefabHandle);
        }

        /// <summary> Resourcesの場合はパス、Addressableの場合はアドレスとして使われるキーを返します </summary>
        private string GetPrefabKey()
        {
            const string overlay = "Overlay";

            var typeName = GetType().Name;
            var prefabName = typeName.EndsWith(overlay)
                ? typeName[..^overlay.Length] // "Overlay"を除く
                : typeName;
            return $"Overlay/{prefabName}";
        }

        #region ScreenBase

        /// <inheritdoc/>
        protected sealed override IViewHandle ViewHandle => _viewHandle;

        #endregion
    }
}