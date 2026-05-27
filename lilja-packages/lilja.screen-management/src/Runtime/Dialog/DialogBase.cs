using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// すべてのダイアログの抽象基底クラス。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    /// <typeparam name="TResult">返却する結果の型</typeparam>
    /// <typeparam name="TFrame">フレーム（外枠）のコンポーネント型</typeparam>
    /// <typeparam name="TContent">コンテンツ（中身）のコンポーネント型</typeparam>
    public abstract class DialogBase<TArgs, TResult, TFrame, TContent>
        : AwaitableGameScreen<TArgs, TResult>,
            IDialog
        where TFrame : MonoBehaviour, IDialogFrame
        where TContent : MonoBehaviour
    {
        #region For Implementers

        /// <summary>
        /// ダイアログの「枠」コンポーネントを取得します。
        /// </summary>
        [View]
        protected TFrame Frame { get; private set; }

        /// <summary>
        /// ダイアログの「中身」コンポーネントを取得します。
        /// </summary>
        [View]
        protected TContent Content { get; private set; }

        /// <summary> 枠外クリック検知ボタン </summary>
        [View]
        private OutsideButton _outsideButton;

        /// <summary> 表示時のアニメーション </summary>
        protected virtual IDialogAnimation Animation => new DefaultDialogAnimation();

        /// <summary> スタック表示時の演出アニメーション </summary>
        protected virtual IDialogStackAnimation StackAnimation => new DefaultStackAnimation();

        /// <summary> 枠外部分をクリックした際にクローズを許容するか </summary>
        protected virtual bool EnableOutsideButton => false;

        /// <summary> 枠外クリックでクローズした際に返却するデフォルト結果 </summary>
        protected virtual TResult OutsideButtonResult => default;

        /// <summary> プレハブが見つからなかった場合のフォールバック用フレーム生成ファクトリ </summary>
        protected virtual GameObject CreateFallbackFrame() => null;

        /// <summary> プレハブが見つからなかった場合のフォールバック用コンテンツ生成ファクトリ </summary>
        protected virtual GameObject CreateFallbackContent() => null;

        #endregion

        private DialogViewHandle _viewHandle;
        private IDialogAnimation _cachedAnimation;
        private IDialogStackAnimation _cachedStackAnimation;

        /// <summary>
        /// フレームの Resources/Addressable アセットキーを取得します。
        /// </summary>
        private static string GetFrameKey()
        {
            return $"DialogFrame/{typeof(TFrame).Name}";
        }

        /// <summary>
        /// コンテンツの Resources/Addressable アセットキーを取得します。
        /// </summary>
        private static string GetContentKey()
        {
            return $"DialogContent/{typeof(TContent).Name}";
        }

        /// <summary>
        /// キャッシュ保護された表示アニメーションインスタンスを取得します（GameScreenBase.GetViewHandle を参考にした設計）。
        /// </summary>
        private IDialogAnimation GetAnimation()
        {
            _cachedAnimation ??= Animation;
            return _cachedAnimation;
        }

        /// <summary>
        /// キャッシュ保護されたスタックアニメーションインスタンスを取得します（GameScreenBase.GetViewHandle を参考にした設計）。
        /// </summary>
        private IDialogStackAnimation GetStackAnimation()
        {
            _cachedStackAnimation ??= StackAnimation;
            return _cachedStackAnimation;
        }

        /// <inheritdoc />
        protected sealed override async UniTask TriggerInitializeAsync(
            TArgs args,
            CancellationToken cancellationToken
        )
        {
            // 自分自身のコンテキストの一時オーバーライドオプションを自己完結的にセット！
            Context.OverrideOptions = Context.Options with
            {
                // ダイアログ表示時は、システム全体のフェード等の割り込みを自動でブロックする
                Transition = null,
            };

            await base.TriggerInitializeAsync(args, cancellationToken);
        }

        /// <inheritdoc />
        protected override IViewHandle ViewHandle => _viewHandle ??= CreateViewHandle();

        /// <summary>
        /// ダイアログ専用の合成ビューハンドルを生成・遅延初期化します。
        /// </summary>
        /// <returns>初期化されたダイアログビューハンドルインスタンス。</returns>
        private DialogViewHandle CreateViewHandle()
        {
            // 重ね合わせ（スタック最前面が別のIDialog）かどうかに基づいて Backdrop を使うか決定
            var parent = Context.Connector.Parent;
            var useBackdrop = !(parent != null && parent.Owner is IDialog);

            return new DialogViewHandle(
                GetFrameKey(),
                GetContentKey(),
                useBackdrop,
                CreateFallbackFrame,
                CreateFallbackContent
            );
        }

        /// <inheritdoc />
        protected sealed override void TriggerOnViewLoaded()
        {
            if (_outsideButton != null)
            {
                _outsideButton.onClick.AddListener(OnClickOutside);
            }

            GetAnimation()?.OnViewLoaded(_viewHandle.FrameRectTransform);
            GetStackAnimation()?.OnViewLoaded(_viewHandle.FrameRectTransform);

            base.TriggerOnViewLoaded();
        }

        /// <inheritdoc />
        protected sealed override void TriggerOnViewUnloaded()
        {
            GetAnimation()?.OnViewUnloaded();
            GetStackAnimation()?.OnViewUnloaded();

            if (_outsideButton != null)
            {
                _outsideButton.onClick.RemoveListener(OnClickOutside);
            }

            // 次回ロードに備えてキャッシュ参照もクリアし、メモリリークを完璧に防止
            _cachedAnimation = null;
            _cachedStackAnimation = null;

            base.TriggerOnViewUnloaded();
        }

        private void OnClickOutside()
        {
            if (EnableOutsideButton)
            {
                Complete(OutsideButtonResult);
            }
        }

        /// <inheritdoc />
        protected sealed override async UniTask TriggerEnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
            // 通常の入場（新規オープン）のとき、ダイアログのポップアップアニメーションを実行
            if (context.EnterType == EnterType.OnOpen)
            {
                var anim = GetAnimation();
                if (anim != null)
                {
                    await anim.ShowAsync(cancellationToken);
                }
            }

            if (context.EnterType == EnterType.OnResume)
            {
                var stackAnim = GetStackAnimation();

                // 背後から復帰した相手が IDialog の場合のみ Pop アニメーションを実行
                if (
                    context.PreviousScreenType != null
                    && typeof(IDialog).IsAssignableFrom(context.PreviousScreenType)
                    && stackAnim != null
                )
                {
                    await stackAnim.PopAsync(cancellationToken);
                }
            }

            await base.TriggerEnterAsync(context, cancellationToken);
        }

        /// <inheritdoc />
        protected sealed override async UniTask TriggerExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        )
        {
            // 通常の退場（クローズ）のとき、ダイアログの非表示アニメーションを実行
            if (context.ExitType == ExitType.OnClose)
            {
                var anim = GetAnimation();
                if (anim != null)
                {
                    await anim.HideAsync(cancellationToken);
                }
            }

            if (context.ExitType == ExitType.OnPause)
            {
                var stackAnim = GetStackAnimation();

                // 新しく上に重ねられた相手が IDialog の場合のみ Push アニメーションを実行
                if (
                    context.NextScreenType != null
                    && typeof(IDialog).IsAssignableFrom(context.NextScreenType)
                    && stackAnim != null
                )
                {
                    await stackAnim.PushAsync(cancellationToken);
                }
            }

            await base.TriggerExitAsync(context, cancellationToken);
        }
    }
}
