using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    public static partial class Procedures
    {
        /// <summary>
        /// 単一の画面（Screen）に対する物理的なインフラ処理を司るモジュール。
        /// </summary>
        internal static class Screen
        {
            /// <summary>
            /// 画面アセットをロードし、依存注入、ソート順、入力遮断を適用して画面を物理的に使用可能な状態にします。
            /// </summary>
            public static async UniTask PrepareAsync(
                IGameScreenInternal screen,
                CancellationToken cancellationToken
            )
            {
                var viewHandle = screen.GetViewHandle();
                if (viewHandle == null)
                {
                    throw new InvalidOperationException(
                        $"[Lilja.ScreenManagement] 画面 '{screen.GetType().Name}' の ViewHandle が null です。"
                    );
                }

                viewHandle.Initialize(screen.GetType());

                TempSceneUtility.TempSceneScope tempSceneScope = default;

                try
                {
                    if (viewHandle.UnloadsAncestors && screen.Context != null)
                    {
                        var needsTempScene = SceneManager.sceneCount <= 1;
                        if (needsTempScene)
                        {
                            tempSceneScope = TempSceneUtility.CreateTempSceneScope();
                        }

                        await UnloadAncestorsAsync(screen.Context, screen, cancellationToken);
                    }

                    await viewHandle.LoadAsync(screen.Context, cancellationToken);

                    tempSceneScope.Dispose();
                    tempSceneScope = default;
                }
                catch
                {
                    // 自身のロード失敗・キャンセル時、すでに先祖が一時アンロードされている場合は
                    // CancellationToken.None を用いて確実に復元（ロールバック）し、完全ブラックアウトを防ぐ
                    if (viewHandle.UnloadsAncestors && screen.Context != null)
                    {
                        try
                        {
                            await RestoreAncestorsAsync(
                                screen.Context,
                                screen,
                                null,
                                CancellationToken.None
                            );
                        }
                        catch (Exception restoreEx)
                        {
                            UnityEngine.Debug.LogException(
                                new Exception(
                                    $"[Lilja.ScreenManagement] LoadAsync 失敗後の先祖復元処理においてさらに例外が発生しました。",
                                    restoreEx
                                )
                            );
                        }
                    }
                    throw;
                }
                finally
                {
                    tempSceneScope.Dispose();
                }

                var rootObjects = viewHandle.RootObjects;

                CanvasOrderUtility.ApplyCanvasOrder(rootObjects, screen.Layer);

                ViewInjectUtility.Inject(screen, rootObjects);

                screen.OnViewLoaded();

                if (screen.Layer > 0)
                {
                    CanvasOrderUtility.CreateBehindRaycastBlocker(rootObjects);
                }
            }

            /// <summary>
            /// ビューアセットを非同期でアンロードし、注入された参照フィールドを null でクリアした上で、画面を破棄します。
            /// </summary>
            public static async UniTask TeardownAsync(
                IGameScreenInternal screen,
                CancellationToken cancellationToken = default
            )
            {
                try
                {
                    screen.OnViewUnloaded();
                    ViewInjectUtility.Nullify(screen);
                }
                finally
                {
                    try
                    {
                        var handle = screen.GetViewHandle();
                        if (handle != null)
                        {
                            await handle.UnloadAsync(cancellationToken);
                        }
                    }
                    finally
                    {
                        screen.Dispose();
                    }
                }
            }

            private static async UniTask UnloadAncestorsAsync(
                GameScreenContext context,
                IGameScreenInternal currentScreen,
                CancellationToken cancellationToken
            )
            {
                var list = context.ActiveScreensInternal;
                var index = list.IndexOf(currentScreen);
                if (index <= 0)
                {
                    return;
                }

                // 自分より前（先祖）のアクティブなビューをアンロード
                for (var i = index - 1; i >= 0; i--)
                {
                    var screen = list[i];
                    var handle = screen.GetViewHandle();
                    if (handle != null && handle.IsLoaded)
                    {
                        screen.OnViewUnloaded();

                        ViewInjectUtility.Nullify(screen);

                        await handle.UnloadAsync(cancellationToken);

                        handle.IsUnloadedTemporarily = true;
                    }
                }
            }

            /// <summary>
            /// 一時アンロードされていた先祖のビューを、並列ロードと直列インスタンス化のハイブリッドで安全かつ超高速に復元します。
            /// </summary>
            public static async UniTask RestoreAncestorsAsync(
                GameScreenContext context,
                IGameScreenInternal currentScreen,
                Type previousScreenType,
                CancellationToken cancellationToken
            )
            {
                var list = context.ActiveScreensInternal;
                var index = list.IndexOf(currentScreen);
                if (index < 0)
                {
                    return;
                }

                var pendingScreens = new List<IGameScreenInternal>();
                for (var i = index; i >= 0; i--)
                {
                    var screen = list[i];
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

                if (pendingScreens.Count == 0)
                {
                    return;
                }

                var preloadTasks = new List<UniTask>(pendingScreens.Count);
                foreach (var screen in pendingScreens)
                {
                    var handle = screen.GetViewHandle();
                    preloadTasks.Add(handle.PreloadAsync(context, cancellationToken));
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

            /// <summary>
            /// 画面をリストに登録し、アセット準備（Prepare）およびオープン演出（Open）をアトミックに実行します。
            /// エラー発生時は自動的にリストから安全に削除・ティアダウンして整合性を保ちます。
            /// </summary>
            internal static async UniTask PrepareAndOpenAsync<TArgs>(
                GameScreenContext context,
                IGameScreenInternal<TArgs> screen,
                TArgs args,
                Type previousScreenType,
                ITransition transition,
                CancellationToken cancellationToken
            )
            {
                var list = context.ActiveScreensInternal;
                list.Add(screen);

                try
                {
                    // 先にInitializeAsyncを実行
                    await screen.InitializeAsync(args, cancellationToken);

                    // アセットのロード＆配置（低レイヤーインフラへの委譲）
                    await PrepareAsync(screen, cancellationToken);

                    // オープン演出（フェードイン等）
                    await ExecuteEnterWithTransitionAsync(
                        screen,
                        EnterType.OnOpen,
                        previousScreenType,
                        transition,
                        false,
                        cancellationToken
                    );
                }
                catch
                {
                    if (list.Contains(screen))
                    {
                        list.Remove(screen);
                        await TeardownAsync(screen, CancellationToken.None);
                    }
                    throw;
                }
            }

            /// <summary>
            /// 画面への入場演出・処理を、トランジションハンドルとコンテキストを内部で自動生成し、フォールバック再生とあわせて非同期で実行します。
            /// </summary>
            /// <param name="screen">入場させる画面オブジェクト</param>
            /// <param name="enterType">入場遷移の種類</param>
            /// <param name="previousScreenType">遷移元（手前）の画面 of 型</param>
            /// <param name="transition">使用するトランジション演出</param>
            /// <param name="isReverse">トランジション演出を逆再生するかどうか</param>
            /// <param name="cancellationToken">キャンセル用トークン</param>
            /// <returns>非同期タスク</returns>
            internal static async UniTask ExecuteEnterWithTransitionAsync(
                IGameScreenInternal screen,
                EnterType enterType,
                Type previousScreenType,
                ITransition transition,
                bool isReverse,
                CancellationToken cancellationToken
            )
            {
                var transitionHandle = new TransitionHandle(transition, isReverse);
                var context = new EnterContext(enterType, previousScreenType, transitionHandle);
                await screen.ExecuteEnterAsync(context, cancellationToken);
                if (!context.Transition.IsPlayed && !screen.IsViewless)
                {
                    await context.Transition.PlayAsync(cancellationToken);
                }
            }

            /// <summary>
            /// 画面からの退場演出・処理を、トランジションハンドルとコンテキストを内部で自動生成し、フォールバック再生とあわせて非同期で実行します。
            /// </summary>
            /// <param name="screen">退場させる画面オブジェクト</param>
            /// <param name="exitType">退場遷移の種類</param>
            /// <param name="nextScreenType">遷移先（次）の画面 of 型</param>
            /// <param name="transition">使用するトランジション演出</param>
            /// <param name="isReverse">トランジション演出を逆再生するかどうか</param>
            /// <param name="cancellationToken">キャンセル用トークン</param>
            /// <returns>非同期タスク</returns>
            internal static async UniTask ExecuteExitWithTransitionAsync(
                IGameScreenInternal screen,
                ExitType exitType,
                Type nextScreenType,
                ITransition transition,
                bool isReverse,
                CancellationToken cancellationToken
            )
            {
                var transitionHandle = new TransitionHandle(transition, isReverse);
                var context = new ExitContext(exitType, nextScreenType, transitionHandle);
                await screen.ExecuteExitAsync(context, cancellationToken);
                if (!context.Transition.IsPlayed && !screen.IsViewless)
                {
                    await context.Transition.PlayAsync(cancellationToken);
                }
            }
        }
    }
}
