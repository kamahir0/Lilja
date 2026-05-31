using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログの枠外（グレーアウト部分）に対するクリック操作を検知し、ダイアログを自動クローズするためのボタンを動的生成・配置するユーティリティクラス。
    /// </summary>
    internal static class OutsideButtonUtility
    {
        /// <summary>
        /// 指定された親オブジェクト（DialogRoot）の直下に、不可視で入力判定のみを持つ枠外検知用 <see cref="OutsideButton"/> を生成し、適切な描画オーダー（SiblingIndex）でアタッチします。
        /// </summary>
        /// <param name="parent">作成先となる親の <see cref="Transform"/>。</param>
        /// <param name="hasBackdrop">背面に半透明 Backdrop が既に生成されているかどうか。</param>
        /// <returns>生成された <see cref="OutsideButton"/> コンポーネントインスタンス。</returns>
        public static OutsideButton Create(Transform parent, bool hasBackdrop)
        {
            var outside = new GameObject("Outside", typeof(RectTransform));
            outside.transform.SetParent(parent, false);

            // Backdrop の前面、かつダイアログフレーム（Frame）の背面に配置してクリック順番を保証
            // Backdrop があればインデックス 1、なければ最背面のインデックス 0
            var siblingIndex = hasBackdrop ? 1 : 0;
            outside.transform.SetSiblingIndex(siblingIndex);

            var rect = outside.GetComponent<RectTransform>();
            BackdropUtility.SetFullStretch(rect);

            // 描画オーバーヘッドなしでクリック入力を遮断・検知する独自の InvisibleGraphic コンポーネントをアタッチ
            var graphic = outside.AddComponent<InvisibleGraphic>();
            graphic.raycastTarget = true;

            // ボタン機能を動的アタッチし、無駄な視覚変化（カラーアニメーション等）を無効化
            var button = outside.AddComponent<OutsideButton>();
            button.transition = Selectable.Transition.None;

            return button;
        }
    }
}
