using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// 確認用ダイアログ (はい/いいえの二択) のプリセット。
    /// </summary>
    public sealed class ConfirmDialog : DefaultDialog<ValueTuple, bool>
    {
        /// <summary>
        /// ConfirmDialog の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="title">ダイアログのタイトル。</param>
        /// <param name="body">ダイアログの本文。</param>
        /// <param name="yesButtonText">決定ボタンのラベルテキスト（デフォルト: "はい"）。</param>
        /// <param name="noButtonText">キャンセルボタンのラベルテキスト（デフォルト: "いいえ"）。</param>
        public ConfirmDialog(
            string title,
            string body,
            string yesButtonText = "はい",
            string noButtonText = "いいえ"
        )
        {
            _title = title;
            _body = body;
            _yesButtonText = yesButtonText;
            _noButtonText = noButtonText;
        }

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこの画面をロード・表示し、結果が確定するまで非同期で待機します。
        /// </summary>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="title">ダイアログのタイトル。</param>
        /// <param name="body">ダイアログの本文。</param>
        /// <param name="yesButtonText">決定ボタンのラベルテキスト（デフォルト: "はい"）。</param>
        /// <param name="noButtonText">キャンセルボタンのラベルテキスト（デフォルト: "いいえ"）。</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>画面の結果を返す非同期タスク</returns>
        public static UniTask<bool> CallAsync(
            GameScreenContext callerContext,
            string title,
            string body,
            string yesButtonText = "はい",
            string noButtonText = "いいえ",
            CancellationToken cancellationToken = default
        )
        {
            return new ConfirmDialog(title, body, yesButtonText, noButtonText).CallAsync(
                callerContext,
                default,
                cancellationToken
            );
        }

        /// <inheritdoc />
        protected override void Build()
        {
            Frame.SetTitle(_title);
            Content.AddText(_body);
            Frame.AddButton(_noButtonText, () => Complete(false));
            Frame.AddButton(_yesButtonText, () => Complete(true));
        }

        private readonly string _title;
        private readonly string _body;
        private readonly string _yesButtonText;
        private readonly string _noButtonText;
    }
}
