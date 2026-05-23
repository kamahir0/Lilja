using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// World 関連の操作を提供する Service
    /// </summary>
    public static class WorldService
    {
        /// <summary>
        /// Worldを切り替えます
        /// </summary>
        public static async UniTask SwitchAsync(Type worldType, object args, CancellationToken cancellationToken)
        {
            // 最前面がWorldであれば退場演出を実行
            if (Repository.Instance.ScreenStack.TopScreen is IWorld currentWorld)
            {
                await currentWorld.CloseAsync(cancellationToken);
            }

            // トランジション
            if (Repository.Instance.Transition != null)
            {
                await Repository.Instance.Transition.OutAsync(cancellationToken);
            }

            // シーン0状態を防ぐための一時シーン
            using var _ = TempSceneUtility.CreateTempSceneScope();

            // すべてのScreenをキャプチャ
            var screens = Repository.Instance.ScreenStack.ToArray();

            // スタックを先にクリア（OverlayService の Contains チェックが false を返すようにする）
            Repository.Instance.ScreenStack.OverlayStack.Clear();

            // すべてのScreenを破棄
            foreach (var screen in screens)
            {
                screen.UnloadView();
                screen.Dispose();
            }

            // 新しいWorldを作成
            var world = Repository.Instance.WorldFactories[worldType].Invoke();
            Repository.Instance.ScreenStack.CurrentWorld = world;

            // 初期化
            await world.InitializeAsync(args, cancellationToken);

            // Viewをロード
            await world.LoadViewAsync(cancellationToken);

            // トランジション
            if (Repository.Instance.Transition != null)
            {
                await Repository.Instance.Transition.InAsync(cancellationToken);
            }

            // 入場演出
            await world.OpenAsync(cancellationToken);
        }

        /// <summary>
        /// 一番最初の World を出します
        /// </summary>
        public static async UniTask InitializeAsync(Type worldType, object args, CancellationToken cancellationToken)
        {
            // 新しいWorldを作成
            var world = Repository.Instance.WorldFactories[worldType]();
            Repository.Instance.ScreenStack.CurrentWorld = world;

            // 初期化
            await world.InitializeAsync(args, cancellationToken);

            // Viewをロード
            await world.LoadViewAsync(cancellationToken);

            // 入場演出
            await world.OpenAsync(cancellationToken);

            // トランジション
            if (Repository.Instance.Transition != null)
            {
                await Repository.Instance.Transition.InAsync(cancellationToken);
            }
        }
    }
}
