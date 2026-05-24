using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// メソッドチェーンによりデフォルトダイアログの設定を流れるように構築し、非同期に起動・待機するビルダー。
    /// </summary>
    /// <typeparam name="TResult">ダイアログが返却する結果の型。</typeparam>
    public sealed class DefaultDialogBuilder<TResult>
    {
        private readonly string _title;
        private readonly List<string> _texts = new();
        private readonly List<(string Label, TResult Result)> _buttons = new();
        private bool _enableOutsideButton = true;
        private TResult _outsideButtonResult;
        private IDialogAnimation _animation = new DefaultDialogAnimation();
        private IDialogStackAnimation _stackAnimation = new DefaultStackAnimation();

        /// <summary>
        /// 指定したタイトルでビルダーの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="title">ダイアログのタイトル文字列。</param>
        public DefaultDialogBuilder(string title)
        {
            _title = title;
        }

        /// <summary>
        /// ダイアログの本文テキストを追加します。複数回呼び出すと改行で連結されます。
        /// </summary>
        /// <param name="text">表示するテキストメッセージ。</param>
        /// <returns>自分自身のビルダーインスタンス。</returns>
        public DefaultDialogBuilder<TResult> AddText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _texts.Add(text);
            }
            return this;
        }

        /// <summary>
        /// ダイアログにボタンと、それが押された際に確定される返却結果を追加します。
        /// </summary>
        /// <param name="label">ボタンの表示テキスト。</param>
        /// <param name="result">ボタン押下時に確定する結果値。</param>
        /// <returns>自分自身のビルダーインスタンス。</returns>
        public DefaultDialogBuilder<TResult> AddButton(string label, TResult result)
        {
            _buttons.Add((label, result));
            return this;
        }

        /// <summary>
        /// ダイアログの枠外をクリックした際の挙動を設定します。
        /// </summary>
        /// <param name="clickable">枠外クリックによるクローズを許可する場合は true。</param>
        /// <param name="result">枠外クリックによってクローズした際に返却する結果値。</param>
        /// <returns>自分自身のビルダーインスタンス。</returns>
        public DefaultDialogBuilder<TResult> SetOutsideClickable(
            bool clickable,
            TResult result = default
        )
        {
            _enableOutsideButton = clickable;
            _outsideButtonResult = result;
            return this;
        }

        /// <summary>
        /// ダイアログの表示・非表示時アニメーションを設定します。
        /// </summary>
        /// <param name="animation">使用するダイアログアニメーション。</param>
        /// <returns>自分自身のビルダーインスタンス。</returns>
        public DefaultDialogBuilder<TResult> SetAnimation(IDialogAnimation animation)
        {
            _animation = animation;
            return this;
        }

        /// <summary>
        /// ダイアログが別のダイアログの上にスタック表示された際、またはスタックから復帰した際のアニメーションを設定します。
        /// </summary>
        /// <param name="stackAnimation">使用するスタックアニメーション。</param>
        /// <returns>自分自身のビルダーインスタンス。</returns>
        public DefaultDialogBuilder<TResult> SetStackAnimation(IDialogStackAnimation stackAnimation)
        {
            _stackAnimation = stackAnimation;
            return this;
        }

        /// <summary>
        /// 構築されたダイアログを起動し、その完了と結果の確定を非同期で待機します。
        /// </summary>
        /// <param name="callerContext">呼び出し元の画面のコンテキスト。</param>
        /// <param name="cancellationToken">非同期処理をキャンセルするためのトークン。</param>
        /// <returns>ダイアログの結果タスク。</returns>
        public UniTask<TResult> CallAsync(
            GameScreenContext callerContext,
            CancellationToken cancellationToken = default
        )
        {
            // 具象クラスとなった DefaultDialog を直接 new してデータを流し込む！
            var dialog = new DefaultDialog<ValueTuple, TResult>();

            dialog.DynamicEnableOutsideButton = _enableOutsideButton;
            dialog.DynamicOutsideButtonResult = _outsideButtonResult;
            dialog.DynamicAnimation = _animation;
            dialog.DynamicStackAnimation = _stackAnimation;

            if (!string.IsNullOrEmpty(_title))
            {
                dialog.SetDynamicTitle(_title);
            }

            foreach (var text in _texts)
            {
                dialog.AddDynamicText(text);
            }

            foreach (var (label, result) in _buttons)
            {
                dialog.AddDynamicButton(label, result);
            }

            // コアの Awaitable 手続きを呼び出し、args は ValueTuple (Unit) として起動
            return Procedures.Awaitable.CallAsync(
                callerContext,
                dialog,
                default,
                cancellationToken
            );
        }
    }
}
