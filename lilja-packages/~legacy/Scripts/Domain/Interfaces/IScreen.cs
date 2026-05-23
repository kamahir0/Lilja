using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// Screen の I/F
    /// </summary>
    public interface IScreen : IDisposable
    {
        /// <summary>
        /// ビューをロードします
        /// </summary>
        UniTask LoadViewAsync(CancellationToken cancellationToken);

        /// <summary>
        /// ビューをアンロードします
        /// </summary>
        void UnloadView();

        /// <summary>
        /// 画面を開きます
        /// </summary>
        UniTask OpenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面を閉じます
        /// </summary>
        UniTask CloseAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 画面を一時停止します
        /// </summary>
        UniTask PauseAsync(PauseContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 画面を再開します
        /// </summary>
        UniTask ResumeAsync(ResumeContext context, CancellationToken cancellationToken);
    }
}