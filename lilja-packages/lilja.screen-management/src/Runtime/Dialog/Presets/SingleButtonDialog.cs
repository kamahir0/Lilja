using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// ボタンが1つだけのダイアログ（OK/閉じるなど）のプリセット。
    /// </summary>
    public sealed class SingleButtonDialog : DefaultDialog<ValueTuple, ValueTuple>
    {
        #region Public / Protected Members

        // --- Fields ---
        // (No public or protected fields)

        // --- Properties ---
        // (No public or protected properties)

        // --- Constructors & Methods ---

        /// <summary>
        /// SingleButtonDialog の新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="title">ダイアログのタイトル。</param>
        /// <param name="body">ダイアログの本文。</param>
        /// <param name="buttonText">ボタンのラベルテキスト（デフォルト: "OK"）。</param>
        public SingleButtonDialog(string title, string body, string buttonText = "OK")
        {
            _title = title;
            _body = body;
            _buttonText = buttonText;
        }

        /// <summary>
        /// 指定された呼び出し元のコンテキストの下でこの画面をロード・表示し、結果が確定するまで非同期で待機します。
        /// </summary>
        /// <param name="callerContext">呼び出し側の画面コンテキスト</param>
        /// <param name="title">ダイアログのタイトル。</param>
        /// <param name="body">ダイアログの本文。</param>
        /// <param name="buttonText">ボタンのテキスト</param>
        /// <param name="cancellationToken">キャンセル用トークン</param>
        /// <returns>画面の結果を返す non-static な非同期タスク</returns>
        public UniTask CallAsync(
            GameScreenContext callerContext,
            string title,
            string body,
            string buttonText = "OK",
            CancellationToken cancellationToken = default
        )
        {
            return new SingleButtonDialog(title, body, buttonText).CallAsync(
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
            Frame.AddButton(_buttonText, () => Complete(default));
        }

        #endregion

        #region Internal / Private Members

        // --- Fields ---
        private readonly string _title;
        private readonly string _body;
        private readonly string _buttonText;

        #endregion
    }
}
