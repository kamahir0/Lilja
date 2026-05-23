using System;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// 確認用ダイアログ (Yes/No)
    /// </summary>
    public sealed class ConfirmDialog : SimpleDialogBase<ValueTuple, bool>
    {
        private readonly string _title;
        private readonly string _body;
        private readonly string _yesButtonText;
        private readonly string _noButtonText;

        /// <summary>
        /// ConfirmDialog を作成します
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="body">ダイアログの本文</param>
        /// <param name="yesButtonText">Yes ボタンのテキスト（デフォルト: はい）</param>
        /// <param name="noButtonText">No ボタンのテキスト（デフォルト: いいえ）</param>
        public ConfirmDialog(
            string title,
            string body,
            string yesButtonText = "はい",
            string noButtonText = "いいえ")
        {
            _title = title;
            _body = body;
            _yesButtonText = yesButtonText;
            _noButtonText = noButtonText;
        }

        /// <inheritdoc/>
        protected override void Build()
        {
            Frame.SetTitle(_title);
            Content.AddText(_body);
            Frame.AddButton(_noButtonText, () => Close(false));
            Frame.AddButton(_yesButtonText, () => Close(true));
        }
    }
}
