using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面遷移とロードのインフラストラクチャおよび手続きを管理するモジュール。
    /// </summary>
    public static partial class Procedures
    {
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
                // アセットのロード＆配置（低レイヤーインフラへの委譲）
                await Screen.PrepareAsync(screen, cancellationToken);

                // オープン演出（フェードイン等）
                await screen.OpenAsync(
                    args,
                    previousScreenType,
                    transition,
                    cancellationToken
                );
            }
            catch
            {
                if (list.Contains(screen))
                {
                    list.Remove(screen);
                    await Screen.TeardownAsync(screen, CancellationToken.None);
                }
                throw;
            }
        }
    }
}
