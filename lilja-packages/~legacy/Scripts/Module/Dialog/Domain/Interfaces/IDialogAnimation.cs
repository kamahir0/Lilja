using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// 表示/非表示アニメーションの I/F
    /// </summary>
    public interface IDialogAnimation
    {
        /// <summary>
        /// View インスタンス化時の処理
        /// </summary>
        void OnViewInstanced(RectTransform frame);

        /// <summary>
        /// View 破棄時の処理
        /// </summary>
        void OnViewDestroy();

        /// <summary>
        /// 表示アニメーションを再生します
        /// </summary>
        UniTask ShowAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 非表示アニメーションを再生します
        /// </summary>
        UniTask HideAsync(CancellationToken cancellationToken);
    }
}
