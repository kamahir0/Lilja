using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログが他のダイアログの上に重ねて表示された（スタック）際、およびそこから手前に復帰した際のアニメーションを定義するI/F。
    /// </summary>
    public interface IDialogStackAnimation
    {
        /// <summary>
        /// ダイアログビューのアセットがロードされ、UIインスタンスが生成された直後にアニメーション対象フレームを受け取るために呼び出されます。
        /// </summary>
        /// <param name="frame">アニメーションのターゲットとなるダイアログ外枠の <see cref="RectTransform"/>。</param>
        void OnViewLoaded(RectTransform frame);

        /// <summary>
        /// ダイアログビューのアセットが破棄・アンロードされる直前に、参照解放やクリーンアップを行うために呼び出されます。
        /// </summary>
        void OnViewUnloaded();

        /// <summary>
        /// 新しいダイアログが前面に重ねられ、自身が一時的に奥へ退避する（Push）際のアニメーションを非同期実行します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>非同期タスク。</returns>
        UniTask PushAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 前面のダイアログが閉じられ、自身が再び手前へ復帰する（Pop）際のアニメーションを非同期実行します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン。</param>
        /// <returns>非同期タスク。</returns>
        UniTask PopAsync(CancellationToken cancellationToken);
    }
}
