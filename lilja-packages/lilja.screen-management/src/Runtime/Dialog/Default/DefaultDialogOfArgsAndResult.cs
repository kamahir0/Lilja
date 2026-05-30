using System.Collections.Generic;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトのフレームとコンテンツ構造を使用する標準ダイアログクラス。
    /// 非abstractクラスのため、継承せずにそのままアタッチ・インスタンス化して使用可能です。
    /// </summary>
    /// <typeparam name="TArgs">ダイアログ起動引数の型。</typeparam>
    /// <typeparam name="TResult">ダイアログ返却値 of 型。</typeparam>
    public class DefaultDialog<TArgs, TResult>
        : DialogBase<TArgs, TResult, DefaultDialogFrame, DefaultDialogContent>
    {
        private string _dynamicTitle;
        private readonly List<string> _dynamicTexts = new();
        private readonly List<(string Label, TResult Result)> _dynamicButtons = new();

        private bool _enableOutsideButton;
        private TResult _outsideButtonResult;
        private IDialogAnimation _animation = new DefaultDialogAnimation();
        private IDialogStackAnimation _stackAnimation = new DefaultStackAnimation();

        /// <summary>
        /// 枠外部分をクリックした際にクローズを許容するかどうかを取得または設定します。
        /// </summary>
        public bool DynamicEnableOutsideButton
        {
            get => _enableOutsideButton;
            set => _enableOutsideButton = value;
        }

        /// <summary>
        /// 枠外クリックでクローズした際に返却するデフォルト結果を取得または設定します。
        /// </summary>
        public TResult DynamicOutsideButtonResult
        {
            get => _outsideButtonResult;
            set => _outsideButtonResult = value;
        }

        /// <summary>
        /// ダイアログ表示時のアニメーションを取得または設定します。
        /// </summary>
        public IDialogAnimation DynamicAnimation
        {
            get => _animation;
            set => _animation = value;
        }

        /// <summary>
        /// ダイアログが重ね合わされた（スタック）際のアニメーションを取得または設定します。
        /// </summary>
        public IDialogStackAnimation DynamicStackAnimation
        {
            get => _stackAnimation;
            set => _stackAnimation = value;
        }

        /// <inheritdoc />
        protected override bool EnableOutsideButton => _enableOutsideButton;

        /// <inheritdoc />
        protected override TResult OutsideButtonResult => _outsideButtonResult;

        /// <inheritdoc />
        protected override IDialogAnimation Animation => _animation;

        /// <inheritdoc />
        protected override IDialogStackAnimation StackAnimation => _stackAnimation;

        /// <summary>
        /// 動的構築用のタイトルを設定します。
        /// </summary>
        /// <param name="title">設定するタイトル文字列。</param>
        public void SetDynamicTitle(string title)
        {
            _dynamicTitle = title;
        }

        /// <summary>
        /// 動的構築用のテキストを追加します。
        /// </summary>
        /// <param name="text">追加するテキスト文字列。</param>
        public void AddDynamicText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _dynamicTexts.Add(text);
            }
        }

        /// <summary>
        /// 動的構築用のボタンを追加します。
        /// </summary>
        /// <param name="label">ボタンの表示ラベル。</param>
        /// <param name="result">確定する結果値。</param>
        public void AddDynamicButton(string label, TResult result)
        {
            _dynamicButtons.Add((label, result));
        }

        /// <inheritdoc />
        protected override void OnViewLoaded()
        {
            Build();
            if (Frame != null)
            {
                Frame.AdjustLayout(Content);
            }
        }

        /// <summary>
        /// ダイアログビューを構築します。デフォルトでは動的に設定されたタイトル、テキスト、ボタンを流し込みます。
        /// </summary>
        protected virtual void Build()
        {
            if (Frame == null || Content == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialog.Build: Frame または Content が設定されていません。ダイアログの表示をスキップします。"
                );
                return;
            }

            if (!string.IsNullOrEmpty(_dynamicTitle))
            {
                Frame.SetTitle(_dynamicTitle);
            }

            foreach (var text in _dynamicTexts)
            {
                Content.AddText(text);
            }

            foreach (var (label, result) in _dynamicButtons)
            {
                Frame.AddButton(label, () => Complete(result));
            }
        }

        /// <inheritdoc />
        protected override GameObject CreateFallbackFrame()
        {
            return DefaultDialogFallbackUtility.CreateFrame();
        }

        /// <inheritdoc />
        protected override GameObject CreateFallbackContent()
        {
            return DefaultDialogFallbackUtility.CreateContent();
        }
    }
}
