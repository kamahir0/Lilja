namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移システムで共有・使用される、各種サービスの依存設定オプション。
    /// </summary>
    public sealed record GameScreenOptions
    {
        /// <summary>
        /// デフォルトの設定オプション。
        /// </summary>
        public static GameScreenOptions Default { get; } =
            new()
            {
                PrefabProvider = new ResourcesPrefabProvider(),
                SceneLoader = new DefaultSceneLoader(),
            };

        /// <summary>
        /// 画面遷移時に使用されるトランジション演出。
        /// </summary>
        public ITransition Transition { get; init; }

        /// <summary>
        /// プレハブのロードに使用されるアセットプロバイダー。
        /// </summary>
        public IPrefabProvider PrefabProvider { get; init; }

        /// <summary>
        /// シーンのロードに使用されるサービス。
        /// </summary>
        public ISceneLoader SceneLoader { get; init; }
    }
}
