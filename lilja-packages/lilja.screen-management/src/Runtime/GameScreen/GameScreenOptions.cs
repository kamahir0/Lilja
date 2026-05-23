namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移システムで共有・使用される、各種サービスの依存設定オプション。
    /// </summary>
    public sealed class GameScreenOptions
    {
        private IPrefabProvider _prefabProvider;
        private ISceneLoader _sceneLoader;

        /// <summary>
        /// デフォルトの設定オプション。
        /// </summary>
        public static GameScreenOptions Default { get; } = new GameScreenOptions();

        /// <summary>
        /// 画面遷移時に使用されるトランジション演出。
        /// </summary>
        public ITransition Transition { get; set; }

        /// <summary>
        /// プレハブのロードに使用されるアセットプロバイダー。
        /// </summary>
        public IPrefabProvider PrefabProvider
        {
            get => _prefabProvider ??= new ResourcesPrefabProvider();
            set => _prefabProvider = value;
        }

        /// <summary>
        /// シーンのロードに使用されるサービス。
        /// </summary>
        public ISceneLoader SceneLoader
        {
            get => _sceneLoader ??= new DefaultSceneLoader();
            set => _sceneLoader = value;
        }
    }
}
