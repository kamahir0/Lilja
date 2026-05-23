using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Dialog の設定を保持するリポジトリ
    /// </summary>
    internal static class Repository
    {
        /// <summary> Backdrop の色 </summary>
        public static Color BackdropColor { get; set; } = new Color(0, 0, 0, 0.5f);
    }
}
