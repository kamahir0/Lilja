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
        protected override IViewHandle ViewHandle => _viewHandle ??= CreateViewHandle();

        /// <summary>
        /// ダイアログ専用の合成ビューハンドルを生成・遅延初期化します。
        /// </summary>
        private DialogViewHandle CreateViewHandle()
        {
            // 重ね合わせ（スタック最前面が別のIDialog）かどうかに基づいて Backdrop を使うか決定
            var parent = Context.Connector.Parent;
            var useBackdrop = !(parent != null && parent.Owner is IDialog);

            // ダイアログ専用のアニメーションを正式版の Transition システムにマッピング
            Context.Options = new GameScreenOptions
            {
                Transition = new DialogTransition(GetAnimation()),
                PrefabProvider = Context.Options.PrefabProvider,
                SceneLoader = Context.Options.SceneLoader,
            };

            return new DialogViewHandle(
                GetFrameKey(),
                GetContentKey(),
                useBackdrop,
                CreateFallbackFrame,
                CreateFallbackContent
            );
        }

        /// <inheritdoc />
        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();

            if (_outsideButton != null)
            {
                _outsideButton.onClick.AddListener(OnClickOutside);
            }

            GetAnimation()?.OnViewLoaded(_viewHandle.FrameRectTransform);
            GetStackAnimation()?.OnViewLoaded(_viewHandle.FrameRectTransform);
        }

        /// <inheritdoc />
        protected override void OnViewUnloaded()
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

            base.OnViewUnloaded();
        }

        private void OnClickOutside()
        {
            if (EnableOutsideButton)
            {
                Complete(OutsideButtonResult);
            }
        }

        /// <inheritdoc />
        protected override async UniTask EnterAsync(
            EnterContext context,
            CancellationToken cancellationToken
        )
        {
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
        }

        /// <inheritdoc />
        protected override async UniTask ExitAsync(
            ExitContext context,
            CancellationToken cancellationToken
        )
        {
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
        }
    }

    /// <summary>
    /// IDialogAnimation を正式版の ITransition にマッピングするブリッジクラス。
    /// </summary>
    internal sealed class DialogTransition : ITransition
    {
        private readonly IDialogAnimation _animation;

        public DialogTransition(IDialogAnimation animation)
        {
            _animation = animation;
        }

        public UniTask OutAsync(CancellationToken cancellationToken)
        {
            return _animation?.HideAsync(cancellationToken) ?? UniTask.CompletedTask;
        }

        public UniTask InAsync(CancellationToken cancellationToken)
        {
            return _animation?.ShowAsync(cancellationToken) ?? UniTask.CompletedTask;
        }
    }
}
