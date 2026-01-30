using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// 簡易ダイアログの基底クラス
    /// </summary>
    public abstract class SimpleDialogBase<TArgs, TResult> : DialogBase<TArgs, TResult, SimpleDialogFrame, SimpleDialogContent>
    {
        /// <inheritdoc/>
        protected override void OnViewLoaded()
        {
            Build();
        }

        /// <summary>
        /// ダイアログを構築します
        /// </summary>
        protected abstract void Build();

        /// <inheritdoc/>
        protected override GameObject CreateFallbackFrame() => SimpleDialogFallbackUtility.CreateFrame();

        /// <inheritdoc/>
        protected override GameObject CreateFallbackContent() => SimpleDialogFallbackUtility.CreateContent();
    }
}
