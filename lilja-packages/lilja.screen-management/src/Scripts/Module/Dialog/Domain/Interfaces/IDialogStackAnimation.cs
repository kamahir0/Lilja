using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog のスタックアニメーションの I/F
    /// </summary>
    public interface IDialogStackAnimation
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
        /// 退避アニメーションを再生します
        /// </summary>
        UniTask PushAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 復帰アニメーションを再生します
        /// </summary>
        UniTask PopAsync(CancellationToken cancellationToken);
    }
}
