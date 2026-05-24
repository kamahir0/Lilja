using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog のマーカーI/F。
    /// </summary>
    public interface IDialog { }

    /// <summary>
    /// DialogFrame のI/F。
    /// </summary>
    public interface IDialogFrame
    {
        /// <summary>
        /// DialogContent を格納する親 RectTransform を取得します。
        /// </summary>
        RectTransform ContentContainer { get; }
    }

    /// <summary>
    /// 表示/非表示アニメーションのI/F。
    /// </summary>
    public interface IDialogAnimation
    {
        /// <summary>
        /// ビューアセットがロードされ、インスタンス化された直後に呼び出されます。
        /// </summary>
        /// <param name="frame">フレームの RectTransform</param>
        void OnViewLoaded(RectTransform frame);

        /// <summary>
        /// ビューアセットがアンロード・破棄される直前に呼び出されます。
        /// </summary>
        void OnViewUnloaded();

        /// <summary>
        /// 表示アニメーションを再生します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask ShowAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 非表示アニメーションを再生します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask HideAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Dialog のスタックアニメーションのI/F。
    /// </summary>
    public interface IDialogStackAnimation
    {
        /// <summary>
        /// ビューアセットがロードされ、インスタンス化された直後に呼び出されます。
        /// </summary>
        /// <param name="frame">フレームの RectTransform</param>
        void OnViewLoaded(RectTransform frame);

        /// <summary>
        /// ビューアセットがアンロード・破棄される直前に呼び出されます。
        /// </summary>
        void OnViewUnloaded();

        /// <summary>
        /// 退避（奥に引っ込む）アニメーションを再生します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask PushAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 復帰（手前に戻る）アニメーションを再生します。
        /// </summary>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>非同期タスク</returns>
        UniTask PopAsync(CancellationToken cancellationToken);
    }
}
