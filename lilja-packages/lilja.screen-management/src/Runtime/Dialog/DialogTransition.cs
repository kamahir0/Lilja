using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// <see cref="IDialogAnimation"/> を正式版の画面遷移インターフェース <see cref="ITransition"/> にマッピングするためのブリッジクラス。
    /// </summary>
    internal sealed class DialogTransition : ITransition
    {
        private readonly IDialogAnimation _animation;

        /// <summary>
        /// 新しい <see cref="DialogTransition"/> インスタンスを初期化します。
        /// </summary>
        /// <param name="animation">マッピング対象のダイアログアニメーション。</param>
        public DialogTransition(IDialogAnimation animation)
        {
            _animation = animation;
        }

        /// <summary>
        /// ダイアログの退場演出（非表示）を非同期実行します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>非同期タスク。</returns>
        public UniTask OutAsync(CancellationToken cancellationToken)
        {
            return _animation?.HideAsync(cancellationToken) ?? UniTask.CompletedTask;
        }

        /// <summary>
        /// ダイアログの入場演出（表示）を非同期実行します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>非同期タスク。</returns>
        public UniTask InAsync(CancellationToken cancellationToken)
        {
            return _animation?.ShowAsync(cancellationToken) ?? UniTask.CompletedTask;
        }
    }
}
