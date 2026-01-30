using System;
using System.Collections.Generic;

namespace Lilja.ScreenManagement.Dialog
{
    public static class VariableDialog
    {
        public static VariableDialog<TArgs, TResult> Create<TArgs, TResult>(
            string title,
            bool enableOutsideButton = true,
            TResult outsideButtonResult = default)
        {
            return VariableDialog<TArgs, TResult>.Create(title, enableOutsideButton, outsideButtonResult);
        }
    }

    /// <summary>
    /// メソッドチェーンで動的に構築可能なダイアログ
    /// </summary>
    public sealed class VariableDialog<TArgs, TResult> : SimpleDialogBase<TArgs, TResult>
    {
        private string _title;
        private readonly List<string> _texts = new();
        private readonly List<(string Label, Action Action)> _buttons = new();
        private bool _enableOutsideButton;
        private TResult _outsideButtonResult;
        private IDialogAnimation _animation = new DefaultDialogAnimation();
        private IDialogStackAnimation _stackAnimation = new DefaultStackAnimation();

        /// <summary> privateコンストラクタ </summary>
        private VariableDialog() { }

        /// <inheritdoc/>
        protected override bool EnableOutsideButton => _enableOutsideButton;

        /// <inheritdoc/>
        protected override TResult OutsideButtonResult => _outsideButtonResult;

        /// <inheritdoc/>
        protected override IDialogAnimation Animation => _animation;

        /// <inheritdoc/>
        protected override IDialogStackAnimation StackAnimation => _stackAnimation;

        /// <summary>
        /// VariableDialog を作成します
        /// </summary>
        /// <param name="title">タイトル</param>
        /// <param name="enableOutsideButton">Outside クリックを有効にするか</param>
        /// <param name="outsideButtonResult">Outside クリック時の結果</param>
        /// <returns>VariableDialog のインスタンス</returns>
        public static VariableDialog<TArgs, TResult> Create(
            string title,
            bool enableOutsideButton = true,
            TResult outsideButtonResult = default)
        {
            var dialog = new VariableDialog<TArgs, TResult>
            {
                _title = title,
                _enableOutsideButton = enableOutsideButton,
                _outsideButtonResult = outsideButtonResult
            };
            return dialog;
        }

        /// <summary>
        /// 本文を追加します
        /// </summary>
        public VariableDialog<TArgs, TResult> AddText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _texts.Add(text);
            }

            return this;
        }

        /// <summary>
        /// ボタンを追加します
        /// </summary>
        public VariableDialog<TArgs, TResult> AddButton(string label, TResult result)
        {
            _buttons.Add((label, () => Close(result)));
            return this;
        }

        /// <summary>
        /// 表示アニメーションを設定します
        /// </summary>
        /// <param name="animation">アニメーションインスタンス（nullでアニメーション無し）</param>
        public VariableDialog<TArgs, TResult> SetAnimation(IDialogAnimation animation)
        {
            _animation = animation;
            return this;
        }

        /// <summary>
        /// スタックアニメーションを設定します
        /// </summary>
        /// <param name="stackAnimation">スタックアニメーションインスタンス（nullでアニメーション無し）</param>
        public VariableDialog<TArgs, TResult> SetStackAnimation(IDialogStackAnimation stackAnimation)
        {
            _stackAnimation = stackAnimation;
            return this;
        }

        /// <inheritdoc/>
        protected override void Build()
        {
            if (!string.IsNullOrEmpty(_title)) Frame.SetTitle(_title);

            foreach (var text in _texts)
            {
                Content.AddText(text);
            }

            foreach (var (label, action) in _buttons)
            {
                Frame.AddButton(label, action);
            }
        }
    }
}
