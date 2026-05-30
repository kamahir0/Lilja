using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ダイアログの背面を覆う半透明背景（Backdrop）に関するグローバルな静的設定クラス。
    /// </summary>
    public static class Backdrop
    {
        /// <summary>
        /// 背景の透過色を取得または設定します。デフォルト値は半透明の黒（Alpha 0.5f）です。
        /// </summary>
        public static Color Color { get; set; } = new Color(0f, 0f, 0f, 0.5f);
    }
}
