using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// DialogFrame の I/F
    /// </summary>
    public interface IDialogFrame
    {
        /// <summary> DialogContent を格納する親 RectTransform </summary>
        RectTransform ContentContainer { get; }
    }
}
