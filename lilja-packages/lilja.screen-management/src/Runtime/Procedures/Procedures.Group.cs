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
            public static async UniTask CallAsync<TArgs>(
                GameScreenContext callerContext,
                GameScreenGroup calleeGroup,
                string initialScreenKey,
                TArgs initialScreenArgs,
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

                ExceptionDispatchInfo signalException = null;
                var initialScreenType = calleeGroup.Builder.GetScreenType(initialScreenKey);

                try
                {
                    if (callerScreen != null)
                    {
                        await callerScreen.PauseAsync(initialScreenType, null, cancellationToken);
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
                    Type previousScreenType = null;
                    if (list.Count > 0)
                    {
                        previousScreenType = list[^1].GetType();
                    }

                    // グループによって追加された画面があれば破棄
                    // グループ開始時のリストの状態に戻す
                    if (calleeGroup.Builder.Contains(initialScreenKey))
                    {
                        // calleeGroup.SwitchAsyncによって最初に追加された初期画面から末尾までを破棄
                        // 初期画面は initialScreenType の型を持つ
                        IGameScreenInternal groupInitialScreen = null;
                        for (var i = list.Count - 1; i >= 0; i--)
                        {
                            if (list[i].GetType() == initialScreenType)
                            {
                                groupInitialScreen = list[i];
                                break;
                            }
                        }

                        if (groupInitialScreen != null)
                        {
                            var nextType = callerScreen?.GetType();
                            await DropSubtreeAsync(
                                callerContext,
                                groupInitialScreen,
                                nextType,
                                null,
                                CancellationToken.None
                            );
                        }
                    }

                    if (callerScreen != null)
                    {
                        await callerScreen.ResumeAsync(previousScreenType, null, cancellationToken);
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

                    var needsTempScene = SceneManager.sceneCount <= 1;
                    using var tempSceneScope = needsTempScene
                        ? TempSceneUtility.CreateTempSceneScope()
                        : default;

                    var nextScreenType = group.Builder.GetScreenType(key);
                    var list = context.ActiveScreensInternal;

                    Type previousScreenType = null;
                    ITransition customTransition = null;
                    if (list.Count > 0)
                    {
                        var oldScreen = list[^1];
                        previousScreenType = oldScreen.GetType();

                        // 遷移元と遷移先のペアから一時差し替えトランジションを検索
                        if (group.OverrideTransitionMap.TryGetValue((previousScreenType, nextScreenType), out var t))
                        {
                            customTransition = t;
                        }

                        // 現在のアクティブな画面（末尾）を破棄
                        await TeardownAsyncInternal(context, oldScreen, nextScreenType, customTransition, cancellationToken);
                    }
                    else
                    {
                        // 遷移元がnull（初期画面）時のToへの差し替え
                        if (group.OverrideTransitionMap.TryGetValue((null, nextScreenType), out var t))
                        {
                            customTransition = t;
                        }
                    }

                    var nextScreenObj = group.Builder.Create(key);
                    var nextScreen = (GameScreen<TArgs>)nextScreenObj;

                    nextScreen.Context = context;
                    nextScreen.Layer = group.Layer;
                    nextScreen.Group = group;

                    list.Add(nextScreen);

                    try
                    {
                        await Screen.PrepareAsync(nextScreen, cancellationToken);

                        await ((IGameScreenInternal<TArgs>)nextScreen).OpenAsync(
                            args,
                            previousScreenType,
                            customTransition,
                            cancellationToken
                        );
                    }
                    catch
                    {
                        if (list.Contains(nextScreen))
                        {
                            await TeardownAsyncInternal(context, nextScreen, null, null, CancellationToken.None);
                        }
                        throw;
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

                var needsTempScene = SceneManager.sceneCount <= 1;
                using var tempSceneScope = needsTempScene
                    ? TempSceneUtility.CreateTempSceneScope()
                    : default;

                // 破棄対象の全画面を Closing マーク
                for (var i = list.Count - 1; i >= index; i--)
                {
                    list[i].IsClosing = true;
                }

                var frontScreen = list[^1];
                ExceptionDispatchInfo closeException = null;

                try
                {
                    await frontScreen.CloseAsync(nextScreenType, overrideTransition, cancellationToken);
                }
                catch (Exception exception)
                {
                    closeException = ExceptionDispatchInfo.Capture(exception);
                }

                var previousScreenType = frontScreen.GetType();

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
                    var firstEx = closeException?.SourceException ?? teardownException?.SourceException;
                    var secondEx = closeException != null ? teardownException?.SourceException : null;
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
