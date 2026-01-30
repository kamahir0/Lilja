namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Scene を使用する Overlay の基底クラス
    /// </summary>
    public abstract class SceneOverlayBase<TArgs, TResult> : OverlayBase<TArgs, TResult>
    {
        private readonly SceneViewHandle _viewHandle;

        /// <summary> コンストラクタ </summary>
        protected SceneOverlayBase()
        {
            _viewHandle = new SceneViewHandle(GetSceneName());
        }

        /// <summary> Scene名を取得します </summary>
        private string GetSceneName()
        {
            const string overlay = "Overlay";

            var typeName = GetType().Name;
            return typeName.EndsWith(overlay)
                ? typeName[..^overlay.Length]
                : typeName;
        }

        #region OverlayBase

        /// <inheritdoc />
        public override bool IsHeavy => true;

        #endregion

        #region ScreenBase

        /// <inheritdoc />
        protected sealed override IViewHandle ViewHandle => _viewHandle;

        #endregion
    }
}