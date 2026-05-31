using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    public static partial class Procedures
    {
        /// <summary>
        /// GameScreenGroup（画面グループ）に関する手続きモジュール。
        /// </summary>
        internal static class Group
        {
            /// <summary>
            /// 画面グループを起動し、初期画面を表示してグループの寿命が終了するまで非同期待機します。
            /// </summary>
            public static GameScreenGroupHandle CallAsync<TArgs>(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup,
                string initialScreenKey,
                TArgs initialScreenArgs,
                CancellationToken cancellationToken
            )
            {
                var handle = new GameScreenGroupHandle();
                CallAsyncInternal(
                        callerContext,
                        calleeGroup,
                        initialScreenKey,
                        initialScreenArgs,
                        handle,
                        cancellationToken
                    )
                    .Forget();
                return handle;
            }

            private static async UniTaskVoid CallAsyncInternal<TArgs>(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup,
                string initialScreenKey,
                TArgs initialScreenArgs,
                GameScreenGroupHandle handle,
                CancellationToken cancellationToken
            )
            {
                // グループ側のコンテキストとして、呼び出し元のコンテキスト参照を共有
                calleeGroup.Context = callerContext;
                calleeGroup.ConfigureInternal();

                IGameScreenInternal callerScreen = null;
                var list = callerContext.ActiveScreensInternal;
                if (list.Count > 0)
                {
                    callerScreen = list[^1];
                }

                // 呼び出し元の画面レイヤーを基準に、グループのレイヤーをインクリメント初期化する
                calleeGroup.Layer = callerScreen != null ? callerScreen.Layer + 1 : 0;

                ExceptionDispatchInfo signalException = null;
                var initialScreenType = calleeGroup.GetScreenType(initialScreenKey);
                var insertIndex = -1;

                try
                {
                    if (callerScreen != null)
                    {
                        await callerScreen.PauseAsync(initialScreenType, null, cancellationToken);
                    }

                    // --- 初回表示フローの完全分離 ---
                    var initialScreenObj = calleeGroup.Create(initialScreenKey);
                    var initialScreen = (GameScreen<TArgs>)initialScreenObj;

                    initialScreen.Context = callerContext;
                    initialScreen.Layer = calleeGroup.Layer;
                    initialScreen.Group = calleeGroup;

                    insertIndex = list.Count;
                    calleeGroup.SetCurrent(initialScreenKey, initialScreenArgs);

                    // 初期表示時のカスタムトランジション検索
                    ITransition customTransition = null;
                    if (calleeGroup.OverrideTransitionMap.TryGetValue((null, initialScreenType), out var t))
                    {
                        customTransition = t;
                    }

                    await PrepareAndOpenAsync(
                        callerContext,
                        initialScreen,
                        initialScreenArgs,
                        callerScreen?.GetType(),
                        customTransition,
                        cancellationToken
                    );

                    handle.SignalInitialScreenEntered();

                    var completionSource = calleeGroup.CompletionSource;
                    using (
                        cancellationToken.RegisterWithoutCaptureExecutionContext(() =>
                            completionSource.TrySetCanceled()
                        )
                    )
                    {
                        await completionSource.Task;
                    }

                    handle.SignalGroupLifetimeCompleted();
                }
                catch (OperationCanceledException)
                {
                    handle.SignalGroupLifetimeCanceled();
                }
                catch (Exception exception)
                {
                    signalException = ExceptionDispatchInfo.Capture(exception);
                    handle.SignalGroupLifetimeFailed(exception);
                }

                try
                {
                    Type previousScreenType = null;
                    if (list.Count > 0)
                    {
                        previousScreenType = list[^1].GetType();
                    }

                    // グループによって追加された画面があれば破棄
                    // グループ開始時のリストの状態に戻す
                    if (insertIndex >= 0 && insertIndex < list.Count)
                    {
                        var groupActiveScreen = list[insertIndex];
                        var nextType = callerScreen?.GetType();
                        await DropSubtreeAsync(
                            callerContext,
                            groupActiveScreen,
                            nextType,
                            null,
                            CancellationToken.None
                        );
                    }

                    if (callerScreen != null)
                    {
                        await callerScreen.ResumeAsync(previousScreenType, null, cancellationToken);
                    }
                }
                catch (Exception teardownException)
                {
                    UnityEngine.Debug.LogException(
                        new Exception(
                            $"[Lilja.ScreenManagement] 画面グループ '{calleeGroup.GetType().Name}' の終了後処理（ティアダウンまたは親画面の復元）中に例外が発生しました。",
                            teardownException
                        )
                    );
                }
            }

            /// <summary>
            /// 画面グループが排他所有している現在のアクティブ画面を破棄し、新しい画面へ切り替えます。
            /// </summary>
            public static async UniTask SwitchAsync<TArgs>(
                GameScreenGroup group,
                string key,
                TArgs args,
                CancellationToken cancellationToken
            )
            {
                var context = group.Context;
                await context.Gate.WaitAsync(cancellationToken);

                try
                {
                    if (context.IsClosing)
                    {
                        throw new InvalidOperationException(
                            "[Lilja.ScreenManagement] この画面グループはクローズ処理中です。"
                        );
                    }

                    var nextScreenType = group.GetScreenType(key);
                    var list = context.ActiveScreensInternal;

                    Type previousScreenType = null;
                    ITransition customTransition = null;
                    TempSceneUtility.TempSceneScope tempSceneScope = default;

                    try
                    {
                        if (list.Count > 0)
                        {
                            var oldScreen = list[^1];
                            previousScreenType = oldScreen.GetType();

                            // 遷移元と遷移先のペアから一時差し替えトランジションを検索
                            if (
                                group.OverrideTransitionMap.TryGetValue(
                                    (previousScreenType, nextScreenType),
                                    out var t
                                )
                            )
                            {
                                customTransition = t;
                            }

                            // 1. まず CloseAsync を呼び、画面を完全に覆う（暗転完了を待つ）
                            oldScreen.IsClosing = true;
                            try
                            {
                                await oldScreen.CloseAsync(
                                    nextScreenType,
                                    customTransition,
                                    cancellationToken
                                );
                            }
                            catch
                            {
                                // CloseAsync中に例外・キャンセルが発生した場合でも、
                                // ゾンビ画面がリストに残らないようにリストから削除し物理的に破棄する
                                list.Remove(oldScreen);
                                await Screen.TeardownAsync(oldScreen, CancellationToken.None);
                                throw;
                            }

                            // 2. 画面が完全に覆われたので、ここで初めて TempScene 防衛を開始する
                            var needsTempScene = SceneManager.sceneCount <= 1;
                            if (needsTempScene)
                            {
                                tempSceneScope = TempSceneUtility.CreateTempSceneScope();
                            }

                            // 3. 旧画面を物理的にアンロード
                            list.Remove(oldScreen);
                            await Screen.TeardownAsync(oldScreen, cancellationToken);
                        }
                        else
                        {
                            // 遷移元がnull（初期画面）時のToへの差し替え
                            if (
                                group.OverrideTransitionMap.TryGetValue(
                                    (null, nextScreenType),
                                    out var t
                                )
                            )
                            {
                                customTransition = t;
                            }
                        }

                        var nextScreenObj = group.Create(key);
                        var nextScreen = (GameScreen<TArgs>)nextScreenObj;

                        nextScreen.Context = context;
                        nextScreen.Layer = group.Layer;
                        nextScreen.Group = group;

                        try
                        {
                            await PrepareAndOpenAsync(
                                context,
                                nextScreen,
                                args,
                                previousScreenType,
                                customTransition,
                                cancellationToken
                            );

                            // 新シーンが正常にアクティブになったので、ここで TempScene を破棄する
                            tempSceneScope.Dispose();
                            tempSceneScope = default;
                        }
                        catch
                        {
                            tempSceneScope.Dispose();
                            throw;
                        }
                    }
                    finally
                    {
                        // 異常系での破棄漏れ防止
                        tempSceneScope.Dispose();
                    }
                }
                finally
                {
                    context.Gate.Release();
                }
            }

            /// <summary>
            /// 指定された画面から始まるサブツリーを安全に破棄し、先祖を復元します。
            /// </summary>
            public static async UniTask DropSubtreeAsync(
                GameScreenContext context,
                IGameScreenInternal rootScreen,
                Type nextScreenType,
                ITransition overrideTransition,
                CancellationToken cancellationToken
            )
            {
                var list = context.ActiveScreensInternal;
                var index = list.IndexOf(rootScreen);
                if (index < 0)
                {
                    return;
                }

                // 破棄対象の全画面を Closing マーク
                for (var i = list.Count - 1; i >= index; i--)
                {
                    list[i].IsClosing = true;
                }

                var frontScreen = list[^1];
                ExceptionDispatchInfo closeException = null;

                try
                {
                    await frontScreen.CloseAsync(
                        nextScreenType,
                        overrideTransition,
                        cancellationToken
                    );
                }
                catch (Exception exception)
                {
                    closeException = ExceptionDispatchInfo.Capture(exception);
                }

                var previousScreenType = frontScreen.GetType();
                TempSceneUtility.TempSceneScope tempSceneScope = default;

                try
                {
                    var needsTempScene = SceneManager.sceneCount <= 1;
                    if (needsTempScene)
                    {
                        tempSceneScope = TempSceneUtility.CreateTempSceneScope();
                    }

                    // 起点画面から末尾画面までの破棄チェーンをクリーンアップしてリストから削除
                    ExceptionDispatchInfo teardownException = null;
                    for (var i = list.Count - 1; i >= index; i--)
                    {
                        var screen = list[i];
                        try
                        {
                            await Screen.TeardownAsync(screen, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogException(
                                new Exception(
                                    $"[Lilja.ScreenManagement] DropSubtree: 画面 '{screen.GetType().Name}' の TeardownAsync 中に例外が発生しました。",
                                    ex
                                )
                            );
                            if (teardownException == null)
                            {
                                teardownException = ExceptionDispatchInfo.Capture(ex);
                            }
                        }
                        finally
                        {
                            list.RemoveAt(i);
                        }
                    }

                    if (closeException != null || teardownException != null)
                    {
                        var firstEx =
                            closeException?.SourceException ?? teardownException?.SourceException;
                        var secondEx =
                            closeException != null ? teardownException?.SourceException : null;
                        if (secondEx != null)
                        {
                            throw new AggregateException(firstEx, secondEx);
                        }
                        throw firstEx;
                    }

                    // 破棄後に親が残っていれば復元
                    if (list.Count > 0)
                    {
                        var parentToRestore = list[^1];
                        await Screen.RestoreAncestorsAsync(
                            context,
                            parentToRestore,
                            previousScreenType,
                            cancellationToken
                        );
                    }

                    tempSceneScope.Dispose();
                    tempSceneScope = default;
                }
                finally
                {
                    tempSceneScope.Dispose();
                }
            }

            private static async UniTask TeardownAsyncInternal(
                GameScreenContext context,
                IGameScreenInternal screen,
                Type nextScreenType,
                ITransition overrideTransition,
                CancellationToken cancellationToken
            )
            {
                screen.IsClosing = true;
                try
                {
                    await screen.CloseAsync(nextScreenType, overrideTransition, cancellationToken);
                }
                finally
                {
                    var list = context.ActiveScreensInternal;
                    list.Remove(screen);
                    await Screen.TeardownAsync(screen, cancellationToken);
                }
            }
        }
    }
}
