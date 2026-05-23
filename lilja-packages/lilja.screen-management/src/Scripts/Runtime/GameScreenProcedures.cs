using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面のロード、アンロード、一時停止、再開、ツリー構造の接続・切断、
    /// および描画順の設定や背面入力遮断など、すべてのランタイム手続き（プロシージャ）を担当する静的クラス。
    /// </summary>
    internal static class GameScreenProcedures
    {
        /// <summary>
        /// AwaitableGameScreen（結果を待てる画面）に関する手続きモジュール。
        /// </summary>
        internal static class Awaitable
        {
            /// <summary>
            /// AwaitableGameScreen を呼び出し元の階層に接続し、表示・演出を行い、結果の確定と破棄完了まで非同期待機します。
            /// </summary>
            internal static async UniTask<TResult> CallAsync<TArgs, TResult>(
                GameScreenContext callerContext,
                AwaitableGameScreen<TArgs, TResult> calleeScreen,
                TArgs args,
                CancellationToken cancellationToken
            )
            {
                if (calleeScreen == null)
                {
                    throw new ArgumentNullException(nameof(calleeScreen));
                }

                var callerConnector = callerContext.Connector;
                var calleeConnector = calleeScreen.Context.Connector;

                // 1. ツリー上に接続
                Connector.Connect(callerConnector, calleeConnector);

                // レイヤーと Options の伝播
                calleeScreen.Context.Layer = callerContext.Layer + 1;
                calleeScreen.Context.Options = callerContext.Options;

                var result = default(TResult);
                ExceptionDispatchInfo signalException = null;

                try
                {
                    // 2. 呼び出し側の親画面を一時停止 (Pause)
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(cancellationToken);
                    }

                    // 3. 子画面ビューの遅延初期化とロード、ソート順、入力遮断、オープン演出の実行
                    await PrepareAndOpenScreenAsync(calleeScreen, args, cancellationToken);

                    // 4. 結果が確定するかキャンセルされるまで非同期待機
                    var completionSource = calleeScreen.CompletionSource;
                    using (
                        cancellationToken.RegisterWithoutCaptureExecutionContext(() =>
                            completionSource.TrySetCanceled()
                        )
                    )
                    {
                        result = await completionSource.Task;
                    }
                }
                catch (Exception exception)
                {
                    signalException = ExceptionDispatchInfo.Capture(exception);
                }

                try
                {
                    // 5. callerがまだcalleeを所有しているなら、この Call が再帰破棄 (Teardown) まで責任を持ってクリーンアップする
                    if (callerConnector.Child == calleeConnector)
                    {
                        await Connector.DropSubtreeAsync(calleeConnector, CancellationToken.None);
                    }

                    // 6. 呼び出し元の親画面を再開 (Resume)
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.ResumeAsync(cancellationToken);
                    }
                }
                catch (Exception teardownException) when (signalException != null)
                {
                    throw new AggregateException(
                        signalException.SourceException,
                        teardownException
                    );
                }

                signalException?.Throw();
                return result;
            }
        }

        /// <summary>
        /// GameScreenGroup（画面グループ）に関する手続きモジュール。
        /// </summary>
        internal static class Group
        {
            /// <summary>
            /// 画面グループを起動し、初期画面を表示してグループの寿命が終了するまで非同期待機します。
            /// </summary>
            internal static async UniTask CallAsync<TArgs>(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup,
                string initialScreenKey,
                TArgs initialScreenArgs,
                CancellationToken cancellationToken
            )
            {
                var callerConnector = callerContext.Connector;
                var calleeConnector = Connect(callerContext, calleeGroup);

                ExceptionDispatchInfo signalException = null;

                try
                {
                    // 1. 呼び出し元の親画面を一時停止 (Pause)
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(cancellationToken);
                    }

                    // 2. 初期画面へ切り替える (Switch)
                    await calleeGroup.SwitchAsync(
                        initialScreenKey,
                        initialScreenArgs,
                        cancellationToken
                    );

                    // 3. グループの寿命終了 (Complete等) まで待機
                    var completionSource = calleeGroup.CompletionSource;
                    using (
                        cancellationToken.RegisterWithoutCaptureExecutionContext(() =>
                            completionSource.TrySetCanceled()
                        )
                    )
                    {
                        await completionSource.Task;
                    }
                }
                catch (Exception exception)
                {
                    signalException = ExceptionDispatchInfo.Capture(exception);
                }

                try
                {
                    // 4. クリーンアップと親画面の再開
                    if (callerConnector.Child == calleeConnector)
                    {
                        await Connector.DropSubtreeAsync(calleeConnector, CancellationToken.None);
                    }

                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.ResumeAsync(cancellationToken);
                    }
                }
                catch (Exception teardownException) when (signalException != null)
                {
                    throw new AggregateException(
                        signalException.SourceException,
                        teardownException
                    );
                }

                signalException?.Throw();
            }

            /// <summary>
            /// 画面グループが排他所有している現在のアクティブ画面を破棄し、新しい画面へ切り替えます。
            /// </summary>
            internal static async UniTask SwitchAsync<TArgs>(
                GameScreenGroup group,
                string key,
                TArgs args,
                CancellationToken cancellationToken
            )
            {
                // 多重画面遷移の防止
                await group.Gate.WaitAsync(cancellationToken);

                try
                {
                    var groupConnector = group.Context.Connector;

                    if (groupConnector.IsClosing)
                    {
                        throw new InvalidOperationException("This group is closing.");
                    }

                    // 1. 既存の現在表示中の画面（Child）があれば先に安全に破棄（クローズ演出含む）する
                    // シーン数が一時的に 0 になるのを防ぐため、必要に応じて一時シーンを生成
                    var needsTempScene = SceneManager.sceneCount <= 1;
                    using var tempSceneScope = needsTempScene
                        ? TempSceneUtility.CreateTempSceneScope()
                        : default;

                    if (groupConnector.Child != null)
                    {
                        await Connector.DropSubtreeAsync(groupConnector.Child, cancellationToken);
                    }

                    // 2. レジストリから新しい画面インスタンスを生成
                    var nextScreenObj = group.Registry.Create(key);
                    var nextScreen = (GameScreen<TArgs>)nextScreenObj;
                    var nextConnector = nextScreen.Context.Connector;

                    // 3. ツリー接続
                    Connector.Connect(groupConnector, nextConnector);
                    nextScreen.Context.Layer = group.Context.Layer;
                    nextScreen.Context.Options = group.Context.Options;
                    nextScreen.Group = group;

                    try
                    {
                        // 4. ビューのロードとオープン演出の実行
                        await PrepareAndOpenScreenAsync(nextScreen, args, cancellationToken);
                    }
                    catch
                    {
                        // オープン失敗時は接続を切ってクリーンアップする
                        if (groupConnector.Child == nextConnector)
                        {
                            await Connector.DropSubtreeAsync(nextConnector, CancellationToken.None);
                        }
                        throw;
                    }
                }
                finally
                {
                    group.Gate.Release();
                }
            }

            private static GameScreenConnector Connect(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup
            )
            {
                if (calleeGroup == null)
                {
                    throw new ArgumentNullException(nameof(calleeGroup));
                }

                var calleeConnector = calleeGroup.Context.Connector;
                Connector.Connect(callerContext.Connector, calleeConnector);

                calleeGroup.Context.Layer = callerContext.Layer + 1;
                calleeGroup.Context.Options = callerContext.Options;

                calleeGroup.ConfigureInternal();
                return calleeConnector;
            }
        }

        /// <summary>
        /// ランタイムツリー上の双方向ノード（コネクタ）の接続・切断、および再帰破棄の手続きモジュール。
        /// </summary>
        internal static class Connector
        {
            internal static void Connect(GameScreenConnector parent, GameScreenConnector child)
            {
                if (parent == null)
                    throw new ArgumentNullException(nameof(parent));
                if (child == null)
                    throw new ArgumentNullException(nameof(child));
                if (parent.Owner == null)
                    throw new InvalidOperationException("The parent connector is not running.");
                if (parent.IsClosing)
                    throw new InvalidOperationException("The parent connector is closing.");
                if (parent.Child != null)
                    throw new InvalidOperationException(
                        "The parent connector already has a child connector."
                    );
                if (ReferenceEquals(parent, child))
                    throw new InvalidOperationException("A connector cannot connect to itself.");
                if (child.Parent != null)
                    throw new InvalidOperationException(
                        "The child connector is already connected."
                    );
                if (child.IsClosing)
                    throw new InvalidOperationException("The child connector is closing.");
                if (child.Owner == null)
                    throw new InvalidOperationException("The child connector is not running.");

                child.Parent = parent;
                child.IsClosing = false;
                parent.Child = child;
            }

            internal static void Disconnect(GameScreenConnector parent, GameScreenConnector child)
            {
                if (child == null)
                    throw new ArgumentNullException(nameof(child));

                if (parent == null)
                {
                    child.Parent = null;
                    return;
                }

                if (parent.Child != child)
                {
                    throw new InvalidOperationException(
                        "The child connector is not connected to the parent connector."
                    );
                }

                parent.Child = null;
                child.Parent = null;
            }

            /// <summary>
            /// 指定されたコネクタ以下のサブツリー（葉ノードまで）を安全に再帰破棄（Teardown）します。
            /// 最前面（アクティブな画面）のクローズ演出を実行した後に、すべてのビューアンロード・参照解放を一気に行います。
            /// </summary>
            internal static async UniTask DropSubtreeAsync(
                GameScreenConnector root,
                CancellationToken cancellationToken
            )
            {
                if (root == null)
                {
                    throw new ArgumentNullException(nameof(root));
                }

                // シーンアンロードにより一時的にシーン数が 0 になるのを防ぐ防衛策
                var needsTempScene = SceneManager.sceneCount <= 1;
                using var tempSceneScope = needsTempScene
                    ? TempSceneUtility.CreateTempSceneScope()
                    : default;

                // 1. 最前面ノード（葉）を取得し、同時にツリーに IsClosing フラグを設定
                var front = MarkClosingAndGetFront(root);

                ExceptionDispatchInfo closeException = null;

                try
                {
                    // 2. 最前面の画面のクローズ演出を非同期実行する
                    var frontScreen = FindFrontScreen(root, front);
                    if (frontScreen != null)
                    {
                        await frontScreen.CloseAsync(cancellationToken);
                    }
                }
                catch (Exception exception)
                {
                    closeException = ExceptionDispatchInfo.Capture(exception);
                }

                try
                {
                    // 3. ビューアンロード、[UnityView]の参照クリア、およびオブジェクトの Dispose を末尾から実行
                    CleanupDropChain(root, front);
                }
                catch (Exception cleanupException) when (closeException != null)
                {
                    throw new AggregateException(closeException.SourceException, cleanupException);
                }

                closeException?.Throw();
            }

            private static GameScreenConnector MarkClosingAndGetFront(GameScreenConnector root)
            {
                var front = root;
                for (var connector = root; connector != null; connector = connector.Child)
                {
                    connector.IsClosing = true;
                    front = connector;
                }
                return front;
            }

            private static IGameScreenInternal FindFrontScreen(
                GameScreenConnector root,
                GameScreenConnector front
            )
            {
                for (var connector = front; connector != null; connector = connector.Parent)
                {
                    if (connector.Owner is IGameScreenInternal screen)
                    {
                        return screen;
                    }

                    if (connector == root)
                    {
                        break;
                    }
                }
                return null;
            }

            private static void CleanupDropChain(
                GameScreenConnector root,
                GameScreenConnector front
            )
            {
                // 親ツリーからこのサブツリーを切断
                Disconnect(root.Parent, root);

                // 葉ノードから根ノードに向けて順番にクリーンアップ
                for (var connector = front; connector != null; )
                {
                    var parent = connector.Parent;

                    CleanupOwner(connector);
                    ClearConnector(connector);

                    if (connector == root)
                    {
                        break;
                    }

                    connector = parent;
                }
            }

            private static void CleanupOwner(GameScreenConnector connector)
            {
                switch (connector.Owner)
                {
                    case GameScreenGroup group:
                        group.CompletionSource.TrySetCanceled();
                        break;
                    case IGameScreenInternal screen:
                        // ビューの解放と [UnityView] の null クリアを実行し、最後に Dispose する
                        TeardownScreen(screen);
                        break;
                }
            }

            private static void ClearConnector(GameScreenConnector connector)
            {
                connector.Parent = null;
                connector.Child = null;
                connector.Owner = null;
                connector.IsClosing = false;
            }
        }

        #region 内部ライフサイクルプロシージャ

        /// <summary>
        /// 画面アセットをロードし、依存注入、ソート順、入力遮断を適用した後に、Open演出を実行します。
        /// </summary>
        private static async UniTask PrepareAndOpenScreenAsync<TArgs>(
            GameScreenBase<TArgs> screen,
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            var viewHandle = ((IGameScreenInternal)screen).GetViewHandle();
            if (viewHandle == null)
            {
                throw new InvalidOperationException(
                    $"ViewHandle is null in screen '{screen.GetType().Name}'."
                );
            }

            // 1. 遅延解決 (コンストラクタの罠の完全回避)
            viewHandle.Initialize(screen.GetType());

            // 2. ビューの非同期ロード
            await viewHandle.LoadAsync(screen.Context, cancellationToken);

            var rootObjects = viewHandle.RootObjects;

            // 3. Canvas 描画順の適用
            Layout.ApplyCanvasOrder(rootObjects, screen.Context.Layer);

            // 4. [UnityView] によるコンポーネント自動注入
            UnityViewUtility.Inject(screen, rootObjects);

            // 5. 重ね合わされる Overlay (Layer > 0) の場合、背面レイキャストブロッカーを生成
            if (screen.Context.Layer > 0)
            {
                Layout.CreateBehindRaycastBlocker(rootObjects);
            }

            // 6. 入場演出を非同期実行
            await ((IGameScreenInternal<TArgs>)screen).OpenAsync(args, cancellationToken);
        }

        /// <summary>
        /// ビューアセットをアンロードし、注入された参照フィールドを null でクリアした上で、画面を破棄します。
        /// </summary>
        private static void TeardownScreen(IGameScreenInternal screen)
        {
            try
            {
                // [UnityView] 参照フィールドを null クリアし、メモリリークを完全に防止する
                UnityViewUtility.Nullify(screen);
            }
            finally
            {
                try
                {
                    // インターフェース経由で直接100%安全かつ超高速にアンロード！ (リフレクション完全排除)
                    screen.GetViewHandle()?.Unload();
                }
                finally
                {
                    // 画面オブジェクト自体の Dispose を実行
                    screen.Dispose();
                }
            }
        }

        #endregion

        #region ビュー処理（旧ユーティリティの統合プロシージャ）

        /// <summary>
        /// キャンバスの描画順や背面レイキャストブロックなど、物理的なレイアウト調整を行う手続きモジュール。
        /// </summary>
        internal static class Layout
        {
            private const int LayerOrderRange = 1000;
            private static readonly List<Canvas> _canvasBuffer = new(16);

            /// <summary>
            /// 生成されたビューオブジェクトにレイヤーインデックスに基づく描画順を適用します（旧 CanvasOrderUtility の統合）。
            /// </summary>
            internal static void ApplyCanvasOrder(GameObject[] rootObjects, int layerIndex)
            {
                if (rootObjects == null || rootObjects.Length == 0)
                {
                    return;
                }

                var canvases = new List<Canvas>();
                foreach (var root in rootObjects)
                {
                    if (root == null)
                    {
                        continue;
                    }
                    root.GetComponentsInChildren(true, canvases);
                }

                if (canvases.Count == 0)
                {
                    return;
                }

                var baseOrder = layerIndex * LayerOrderRange;

                // 既存の相対順序を維持しながらソートして割り当て
                canvases.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));

                for (var i = 0; i < canvases.Count; i++)
                {
                    var canvas = canvases[i];
                    if (canvas.renderMode == RenderMode.WorldSpace)
                    {
                        Debug.LogWarning(
                            $"[GameScreenProcedures] WorldSpace Canvas はソート順制御のサポート対象外です: {canvas.gameObject.name}"
                        );
                        continue;
                    }
                    canvas.sortingOrder = baseOrder + i;
                }
            }

            /// <summary>
            /// 背面に重なった画面へのクリック入力を遮断するブロッカーを生成します（旧 BehindRaycastBlockerUtility の統合）。
            /// </summary>
            internal static void CreateBehindRaycastBlocker(GameObject[] rootObjects)
            {
                if (rootObjects == null || rootObjects.Length == 0)
                {
                    return;
                }

                _canvasBuffer.Clear();

                // 全てのCanvasを取得
                foreach (var root in rootObjects)
                {
                    if (root == null)
                    {
                        continue;
                    }
                    root.GetComponentsInChildren(true, _canvasBuffer);
                }

                if (_canvasBuffer.Count == 0)
                {
                    return;
                }

                // 最奥（SortingOrderが最小）のCanvasを探す
                Canvas targetCanvas = null;
                var minOrder = int.MaxValue;

                foreach (var canvas in _canvasBuffer)
                {
                    if (canvas.renderMode == RenderMode.WorldSpace)
                    {
                        continue;
                    }

                    if (canvas.sortingOrder < minOrder)
                    {
                        minOrder = canvas.sortingOrder;
                        targetCanvas = canvas;
                    }
                }

                _canvasBuffer.Clear();

                if (targetCanvas == null)
                {
                    return;
                }

                // ブロッカーの生成と配置
                var blocker = new GameObject("RaycastBlocker", typeof(RectTransform));
                blocker.transform.SetParent(targetCanvas.transform, false);
                blocker.transform.SetAsFirstSibling();

                var rect = blocker.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                // 描画負荷のない InvisibleGraphic (MonoBehaviour) を追加して入力を遮断
                var graphic = blocker.AddComponent<InvisibleGraphic>();
                graphic.raycastTarget = true;
            }
        }

        #endregion
    }
}
