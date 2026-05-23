using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// Backdrop の設定を行うクラス
    /// </summary>
    public static class Backdrop
    {
        /// <summary> Backdrop の色 </summary>
        public static Color Color
        {
            get => Repository.BackdropColor;
            set => Repository.BackdropColor = value;
        }
    }
}