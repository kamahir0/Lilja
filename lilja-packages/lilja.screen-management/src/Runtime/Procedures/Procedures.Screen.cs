using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

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

                if (viewHandle.UnloadsAncestors && screen.Context != null)
                {
                    await UnloadAncestorsAsync(screen.Context, screen, cancellationToken);
                }

                await viewHandle.LoadAsync(screen.Context, cancellationToken);

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
        }
    }
}
