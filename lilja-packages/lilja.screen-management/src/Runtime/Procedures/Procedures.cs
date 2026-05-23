using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面のロード、アンロード、一時停止、再開、ツリー構造の接続・切断など、すべてのランタイム手続きを担当する静的クラス。
    /// </summary>
    internal static class Procedures
    {
        #region Awaitable

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

                Connector.Connect(callerConnector, calleeConnector);

                calleeScreen.Context.Layer = callerContext.Layer + 1;
                calleeScreen.Context.Options = callerContext.Options;

                var result = default(TResult);
                ExceptionDispatchInfo signalException = null;

                try
                {
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(cancellationToken);
                    }

                    await Screen.PrepareAsync(calleeScreen, cancellationToken);

                    await ((IGameScreenInternal<TArgs>)calleeScreen).OpenAsync(
                        args,
                        cancellationToken
                    );

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
                return result;
            }
        }

        #endregion

        #region Group

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
                    if (callerConnector.Owner is IGameScreenInternal callerScreen)
                    {
                        await callerScreen.PauseAsync(cancellationToken);
                    }

                    await calleeGroup.SwitchAsync(
                        initialScreenKey,
                        initialScreenArgs,
                        cancellationToken
                    );

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
                await group.Gate.WaitAsync(cancellationToken);

                try
                {
                    var groupConnector = group.Context.Connector;

                    if (groupConnector.IsClosing)
                    {
                        throw new InvalidOperationException("This group is closing.");
                    }

                    var needsTempScene = SceneManager.sceneCount <= 1;
                    using var tempSceneScope = needsTempScene
                        ? TempSceneUtility.CreateTempSceneScope()
                        : default;

                    if (groupConnector.Child != null)
                    {
                        await Connector.DropSubtreeAsync(groupConnector.Child, cancellationToken);
                    }

                    var nextScreenObj = group.Registry.Create(key);
                    var nextScreen = (GameScreen<TArgs>)nextScreenObj;
                    var nextConnector = nextScreen.Context.Connector;

                    Connector.Connect(groupConnector, nextConnector);
                    nextScreen.Context.Layer = group.Context.Layer;
                    nextScreen.Context.Options = group.Context.Options;
                    nextScreen.Group = group;

                    try
                    {
                        await Screen.PrepareAsync(nextScreen, cancellationToken);

                        await ((IGameScreenInternal<TArgs>)nextScreen).OpenAsync(
                            args,
                            cancellationToken
                        );
                    }
                    catch
                    {
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

        #endregion

        #region Connector

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
            /// 指定されたコネクタ以下のサブツリーを安全に再帰破棄します。
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

                var needsTempScene = SceneManager.sceneCount <= 1;
                using var tempSceneScope = needsTempScene
                    ? TempSceneUtility.CreateTempSceneScope()
                    : default;

                var front = MarkClosingAndGetFront(root);

                ExceptionDispatchInfo closeException = null;

                try
                {
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
                    CleanupDropChain(root, front);
                }
                catch (Exception cleanupException) when (closeException != null)
                {
                    throw new AggregateException(closeException.SourceException, cleanupException);
                }

                closeException?.Throw();

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
                Disconnect(root.Parent, root);

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

        #endregion

        #region Screen

        /// <summary>
        /// 単一の画面（Screen）に対する物理的なインフラ処理を司るモジュール。
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

                viewHandle.Initialize(screen.GetType());

                if (viewHandle.UnloadsAncestors)
                {
                    UnloadAncestors(screen.Context.Connector);
                }

                await viewHandle.LoadAsync(screen.Context, cancellationToken);

                var rootObjects = viewHandle.RootObjects;

                CanvasOrderUtility.ApplyCanvasOrder(rootObjects, screen.Context.Layer);

                ViewInjectUtility.Inject(screen, rootObjects);

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
                    ViewInjectUtility.Nullify(screen);
                }
                finally
                {
                    try
                    {
                        screen.GetViewHandle()?.Unload();
                    }
                    finally
                    {
                        screen.Dispose();
                    }
                }
            }

            private static void UnloadAncestors(GameScreenConnector startConnector)
            {
                for (var parent = startConnector.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent.Owner is IGameScreenInternal screen)
                    {
                        var handle = screen.GetViewHandle();
                        if (handle != null && handle.IsLoaded)
                        {
                            ViewInjectUtility.Nullify(screen);

                            handle.Unload();

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

                var preloadTasks = new List<UniTask>(pendingScreens.Count);
                foreach (var screen in pendingScreens)
                {
                    var handle = screen.GetViewHandle();
                    preloadTasks.Add(handle.PreloadAsync(screen.Context, cancellationToken));
                }
                await UniTask.WhenAll(preloadTasks);

                for (var i = pendingScreens.Count - 1; i >= 0; i--)
                {
                    var screen = pendingScreens[i];
                    var handle = screen.GetViewHandle();

                    await PrepareAsync(screen, cancellationToken);
                    handle.IsUnloadedTemporarily = false;
                }
            }
        }

        #endregion
    }
}
