using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログの外枠（フレーム）コンポーネントが実装する、コンテンツの配置先制御用のI/F。
    /// </summary>
    public interface IDialogFrame
    {
        /// <summary>
        /// ダイアログのコンテンツ（中身）オブジェクトを格納するための親の <see cref="RectTransform"/> を取得します。
        /// </summary>
        RectTransform ContentContainer { get; }
    }
}
