using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログの入場（表示）および退場（非表示）時におけるビジュアルアニメーションを定義するI/F。
    /// </summary>
    public interface IDialogAnimation
    {
        /// <summary>
        /// ダイアログビューのアセットがロードされ、UIインスタンスが生成された直後にアニメーション対象フレームを受け取るために呼び出されます。
        /// </summary>
        /// <param name="frame">アニメーションのターゲットとなるダイアログ外枠の <see cref="RectTransform"/>。</param>
        void OnViewLoaded(RectTransform frame);

        /// <summary>
        /// ダイアログビューのアセットが破棄・アンロードされる直前に、参照解放やクリーンアップを行うために呼び出されます。
        /// </summary>
        void OnViewUnload();

        /// <summary>
        /// ダイアログ画面が表示される際（入場時）のアニメーションを非同期実行します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>非同期タスク。</returns>
        UniTask ShowAsync(CancellationToken cancellationToken);

        /// <summary>
        /// ダイアログ画面が閉じられる際（退場時）のアニメーションを非同期実行します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>非同期タスク。</returns>
        UniTask HideAsync(CancellationToken cancellationToken);
    }
}
