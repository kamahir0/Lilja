using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// World の基底クラス
    /// </summary>
    public abstract class WorldBase<TArgs> : ScreenBase, IWorld
    {
        #region For Implementers

        /// <summary> 初期化します </summary>
        protected virtual UniTask InitializeAsync(TArgs args, CancellationToken cancellationToken) => UniTask.CompletedTask;

        #endregion

        private readonly SceneViewHandle _viewHandle;

        /// <summary> コンストラクタ </summary>
        protected WorldBase()
        {
            _viewHandle = new SceneViewHandle(GetSceneName());
        }

        /// <inheritdoc/>
        protected sealed override IViewHandle ViewHandle => _viewHandle;

        /// <inheritdoc/>
        protected override int LayerIndex => 0; // NOTE: Worldは常に0

        /// <summary> World のシーン名を取得 </summary>
        private string GetSceneName()
        {
            const string world = "World";

            var typeName = GetType().Name;
            return typeName.EndsWith(world) ? typeName[..^world.Length] : typeName;
        }

        #region IWorld

        /// <inheritdoc/>
        UniTask IWorld.InitializeAsync(object args, CancellationToken cancellationToken) => InitializeAsync((TArgs)args, cancellationToken);

        #endregion
    }
}