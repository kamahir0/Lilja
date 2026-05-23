using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Overlay 関連の操作を提供する Service
    /// </summary>
    public static class OverlayService
    {
        /// <summary> 処理中フラグ </summary>
        private static bool _isProcessing;

        /// <summary>
        /// Overlayを呼び出します
        /// </summary>
        public static async UniTask<TResult> CallOverlayAsync<TArgs, TResult>(
            IOverlay<TArgs, TResult> overlay,
            TArgs args,
            CancellationToken cancellationToken)
        {
            if (_isProcessing) throw new InvalidOperationException("CallOverlayAsync is already processing.");

            Scene tempScene = default;

            try
            {
                using (ProcessingScope.Create())
                {
                    await PreCallAsync();
                }

                // 結果が返るまで待機
                var result = await overlay.WaitForResultAsync(cancellationToken);

                using (ProcessingScope.Create())
                {
                    await PostCallAsync();
                }

                return result;
            }
            finally
            {
                using var _ = ProcessingScope.Create();
                await FinallyAsync();
            }

            // Overlay呼び出し前の処理
            async UniTask PreCallAsync()
            {
                // 一番上のScreenを一時停止
                var pauseContext = new PauseContext(overlay);
                await Repository.Instance.ScreenStack.TopScreen.PauseAsync(pauseContext, cancellationToken);


                // 重いOverlayの場合は他のビューをすべて破棄する
                if (overlay.IsHeavy)
                {
                    if (Repository.Instance.Transition != null)
                    {
                        await Repository.Instance.Transition.OutAsync(cancellationToken);
                    }

                    // シーン0状態を防ぐための一時シーン
                    tempScene = overlay.IsHeavy && !TempSceneUtility.Exists()
                        ? TempSceneUtility.Create()
                        : default;

                    foreach (var screen in Repository.Instance.ScreenStack)
                    {
                        screen.UnloadView();
                    }
                }

                // 初期化
                await overlay.InitializeAsync(args, cancellationToken);

                // Overlayをスタックに追加
                Repository.Instance.ScreenStack.OverlayStack.Push(overlay);
                overlay.SetLayerIndex(Repository.Instance.ScreenStack.OverlayStack.Count);

                // Viewをロード
                await overlay.LoadViewAsync(cancellationToken);

                // 一時シーンを破棄（Overlayのシーンがロードされた後）
                if (tempScene.IsValid()) TempSceneUtility.Destroy();

                // 重いOverlayの場合はトランジション解除
                if (overlay.IsHeavy && Repository.Instance.Transition != null)
                {
                    await Repository.Instance.Transition.InAsync(cancellationToken);
                }

                // 入場演出
                await overlay.OpenAsync(cancellationToken);
            }

            // Overlay呼び出し後の処理（結果取得後）
            async UniTask PostCallAsync()
            {
                // 退場演出
                await overlay.CloseAsync(cancellationToken);
            }

            // 終了処理
            async UniTask FinallyAsync()
            {
                // WorldService.SwitchAsync によって既に破棄されている場合はスキップ
                // （SwitchAsync は破棄前に OverlayStack.Clear() を呼ぶため、スタックに存在しない = 処理済み）
                if (!Repository.Instance.ScreenStack.OverlayStack.Contains(overlay)) return;

                // Overlayのビュー破棄が始まるより前にトランジション完了
                if (overlay.IsHeavy && Repository.Instance.Transition != null)
                {
                    await Repository.Instance.Transition.OutAsync(cancellationToken);
                }

                // シーン0状態を防ぐための一時シーン
                tempScene = overlay.IsHeavy && !TempSceneUtility.Exists()
                    ? TempSceneUtility.Create()
                    : default;

                // Viewを解放
                overlay.UnloadView();

                // 破棄
                overlay.Dispose();

                Repository.Instance.ScreenStack.OverlayStack.Pop();

                // 重いOverlayの場合は他のビューをすべて復元する
                if (overlay.IsHeavy)
                {
                    // スタックの下から順に復元
                    foreach (var screen in Repository.Instance.ScreenStack.Reverse())
                    {
                        await screen.LoadViewAsync(cancellationToken);
                    }
                }

                // 一時シーンを破棄（ビューが復元された後）
                if (tempScene.IsValid()) TempSceneUtility.Destroy();

                // 重いOverlayの場合はトランジション解除
                if (overlay.IsHeavy && Repository.Instance.Transition != null)
                {
                    await Repository.Instance.Transition.InAsync(cancellationToken);
                }

                // 一番上のScreenを再開
                var resumeContext = new ResumeContext(overlay);
                await Repository.Instance.ScreenStack.TopScreen.ResumeAsync(resumeContext, cancellationToken);
            }
        }

        /// <summary>
        /// 処理中であることを示すスコープ
        /// </summary>
        private readonly struct ProcessingScope : IDisposable
        {
            public static ProcessingScope Create()
            {
                _isProcessing = true;
                return default;
            }

            public void Dispose()
            {
                _isProcessing = false;
            }
        }
    }
}
