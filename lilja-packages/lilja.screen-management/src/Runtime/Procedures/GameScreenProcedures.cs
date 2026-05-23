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

                    // 3. 子画面ビューのロードと物理的セットアップ
                    await Screen.PrepareAsync(calleeScreen, cancellationToken);

                    // 4. 入場演出を非同期実行
                    await ((IGameScreenInternal<TArgs>)calleeScreen).OpenAsync(
                        args,
                        cancellationToken
                    );

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
                        // 4. ビューのロードと物理的セットアップ
                        await Screen.PrepareAsync(nextScreen, cancellationToken);

                        // 5. 入場演出を非同期実行
                        await ((IGameScreenInternal<TArgs>)nextScreen).OpenAsync(
                            args,
                            cancellationToken
                        );
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

                var parentToRestore = root.Parent;

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

                // 4. サブツリーの破棄完了後、一時アンロードされていた先祖のビューを安全かつ高速に復元する
                if (parentToRestore != null)
                {
                    await Screen.RestoreAncestorsAsync(parentToRestore, cancellationToken);
                }
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
                        Screen.Teardown(screen);
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

        /// <summary>
        /// 単一の画面（Screen）に対する物理的なインフラ処理（ロード・解体）を司るモジュール。
        /// </summary>
        internal static class Screen
        {
            /// <summary>
            /// 画面アセットをロードし、依存注入、ソート順、入力遮断を適用して画面を物理的に使用可能な状態にします。
            /// </summary>
            internal static async UniTask PrepareAsync(
                IGameScreenInternal screen,
                CancellationToken cancellationToken
            )
            {
                var viewHandle = screen.GetViewHandle();
                if (viewHandle == null)
                {
                    throw new InvalidOperationException(
                        $"ViewHandle is null in screen '{screen.GetType().Name}'."
                    );
                }

                // 1. 遅延解決 (コンストラクタの罠の完全回避)
                viewHandle.Initialize(screen.GetType());

                // 2. ビューハンドルが先祖アンロードを要求している場合、既存ビューをロード前に一時アンロードする
                if (viewHandle.UnloadsAncestors)
                {
                    UnloadAncestors(screen.Context.Connector);
                }

                // 3. ビューの非同期ロード
                await viewHandle.LoadAsync(screen.Context, cancellationToken);

                var rootObjects = viewHandle.RootObjects;

                // 4. Canvas 描順の適用
                CanvasOrderUtility.ApplyCanvasOrder(rootObjects, screen.Context.Layer);

                // 5. [View] によるコンポーネント自動注入
                ViewInjectUtility.Inject(screen, rootObjects);

                // 6. 重ね合わされる Overlay (Layer > 0) の場合、背面レイキャストブロッカーを生成
                if (screen.Context.Layer > 0)
                {
                    CanvasOrderUtility.CreateBehindRaycastBlocker(rootObjects);
                }
            }

            /// <summary>
            /// ビューアセットをアンロードし、注入された参照フィールドを null でクリアした上で、画面を破棄します。
            /// </summary>
            internal static void Teardown(IGameScreenInternal screen)
            {
                try
                {
                    // [View] 参照フィールドを null クリアし、メモリリークを完全に防止する
                    ViewInjectUtility.Nullify(screen);
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

            /// <summary>
            /// 指定されたコネクタから親（先祖）方向へ遡り、ロード済みのビューを一時アンロードしてフラグを設定します。
            /// </summary>
            private static void UnloadAncestors(GameScreenConnector startConnector)
            {
                for (var parent = startConnector.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent.Owner is IGameScreenInternal screen)
                    {
                        var handle = screen.GetViewHandle();
                        if (handle != null && handle.IsLoaded)
                        {
                            // [View] 参照フィールドを一旦 null クリア
                            ViewInjectUtility.Nullify(screen);

                            // ビューを物理的にアンロード
                            handle.Unload();

                            // 一時アンロード状態を記録
                            handle.IsUnloadedTemporarily = true;
                        }
                    }
                }
            }

            /// <summary>
            /// 一時アンロードされていた先祖のビューを、並列ロードと直列インスタンス化のハイブリッドで安全かつ超高速に復元します。
            /// </summary>
            internal static async UniTask RestoreAncestorsAsync(
                GameScreenConnector startConnector,
                CancellationToken cancellationToken
            )
            {
                var pendingScreens = new List<IGameScreenInternal>();
                for (var parent = startConnector; parent != null; parent = parent.Parent)
                {
                    if (parent.Owner is IGameScreenInternal screen)
                    {
                        var handle = screen.GetViewHandle();
                        if (handle != null)
                        {
                            if (handle.IsUnloadedTemporarily)
                            {
                                pendingScreens.Add(screen);
                            }

                            // [境界の打ち切り]
                            // 先祖アンロードを行う画面（重い画面 h）に遭遇した場合、
                            // その画面自体の復元までをこのライフサイクルで処理し、それより親の領域は探索を打ち切ります。
                            if (handle.UnloadsAncestors)
                            {
                                break;
                            }
                        }
                    }
                }

                if (pendingScreens.Count == 0)
                {
                    return;
                }

                // 1. [並列アセットロード] すべての一時アンロード画面のアセット事前ロードを一斉に並列実行（WhenAll）
                var preloadTasks = new List<UniTask>(pendingScreens.Count);
                foreach (var screen in pendingScreens)
                {
                    var handle = screen.GetViewHandle();
                    preloadTasks.Add(handle.PreloadAsync(screen.Context, cancellationToken));
                }
                await UniTask.WhenAll(preloadTasks);

                // 2. [直列インスタンス化] アセットがメモリに載った状態で、最親から順に 1 つずつ直列にインスタンス化/アクティベート
                for (var i = pendingScreens.Count - 1; i >= 0; i--)
                {
                    var screen = pendingScreens[i];
                    var handle = screen.GetViewHandle();

                    // 物理ロード処理を安全に再利用（重複の完全排除）
                    await PrepareAsync(screen, cancellationToken);
                    handle.IsUnloadedTemporarily = false;
                }
            }
        }
    }
}
