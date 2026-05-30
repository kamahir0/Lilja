using System.Collections.Generic;
using System.Threading;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面スタックの共通の設定パラメータや、アクティブな画面リスト、多重遷移防止などを一元管理するコンテキストクラス。
    /// </summary>
    public sealed class GameScreenContext
    {
        private readonly List<IGameScreenInternal> _activeScreens = new();

        internal GameScreenContext()
        {
            Gate = new SemaphoreSlim(1, 1);
            PrefabProvider = new ResourcesPrefabProvider();
            SceneLoader = new DefaultSceneLoader();
        }

        /// <summary>
        /// 新規のルートコンテキストを生成します。
        /// </summary>
        /// <param name="transition">この遷移スタック全体で共有・使用されるトランジション演出。</param>
        /// <param name="prefabProvider">プレハブのロードに使用されるアセットプロバイダー。</param>
        /// <param name="sceneLoader">シーンのロードに使用されるサービス。</param>
        /// <returns>安全に初期化されたルートコンテキストインスタンス</returns>
        public static GameScreenContext CreateRoot(
            ITransition transition = null,
            IPrefabProvider prefabProvider = null,
            ISceneLoader sceneLoader = null
        )
        {
            return new GameScreenContext
            {
                Transition = transition,
                PrefabProvider = prefabProvider ?? new ResourcesPrefabProvider(),
                SceneLoader = sceneLoader ?? new DefaultSceneLoader(),
            };
        }

        /// <summary>
        /// このコンテキスト（スタック）内で現在アクティブな画面の読み取り専用リスト。
        /// サードパーティの拡張アセンブリからも安全に参照可能です。
        /// </summary>
        public IReadOnlyList<IGameScreen> ActiveScreens => _activeScreens;

        /// <summary>
        /// 内部（Procedures等）での遷移・破棄操作に使用する実体リスト。
        /// </summary>
        internal List<IGameScreenInternal> ActiveScreensInternal => _activeScreens;

        /// <summary>
        /// 多重遷移を防ぐための非同期セマフォ。
        /// </summary>
        internal SemaphoreSlim Gate { get; }

        /// <summary>
        /// このグループ全体がクローズ処理中であるかを示すフラグ。
        /// </summary>
        internal bool IsClosing { get; set; }

        /// <summary>
        /// 画面遷移時に使用されるデフォルトのトランジション演出。
        /// </summary>
        public ITransition Transition { get; set; }

        /// <summary>
        /// プレハブのロードに使用されるアセットプロバイダー。
        /// </summary>
        public IPrefabProvider PrefabProvider { get; set; }

        /// <summary>
        /// シーンのロードに使用されるサービス。
        /// </summary>
        public ISceneLoader SceneLoader { get; set; }
    }
}
