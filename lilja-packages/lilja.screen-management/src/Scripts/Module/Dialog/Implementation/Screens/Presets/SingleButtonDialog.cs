using System;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// シングルボタンダイアログ
    /// </summary>
    public sealed class SingleButtonDialog : SimpleDialogBase<ValueTuple, ValueTuple>
    {
        private readonly string _title;
        private readonly string _body;
        private readonly string _buttonText;

        /// <summary>
        /// SingleButtonDialog を作成します
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="body">ダイアログの本文</param>
        /// <param name="buttonText">ボタンのテキスト（デフォルト: OK）</param>
        public SingleButtonDialog(string title, string body, string buttonText = "OK")
        {
            _title = title;
            _body = body;
            _buttonText = buttonText;
        }

        /// <inheritdoc/>
        protected override void Build()
        {
            Frame.SetTitle(_title);
            Content.AddText(_body);
            Frame.AddButton(_buttonText, () => Close(default));
        }
    }
}
