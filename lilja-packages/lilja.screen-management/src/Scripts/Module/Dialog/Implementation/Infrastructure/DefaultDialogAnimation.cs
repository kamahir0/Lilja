using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトの表示/非表示アニメーション
    /// </summary>
    public class DefaultDialogAnimation : IDialogAnimation
    {
        /// <summary> 表示アニメーションの時間 </summary>
        public float ShowDuration { get; set; } = 0.25f;

        /// <summary> 非表示アニメーションの時間 </summary>
        public float HideDuration { get; set; } = 0.2f;

        /// <summary> 移動距離 </summary>
        public float MoveDistance { get; set; } = 50f;

        /// <summary> 表示時のイージング </summary>
        public AnimationCurve ShowEase { get; set; } = AnimationCurve.EaseInOut(0, 0, 1, 1);

        /// <summary> 非表示時のイージング </summary>
        public AnimationCurve HideEase { get; set; } = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 内部状態
        private RectTransform _target;

        /// <inheritdoc/>
        public void OnViewInstanced(RectTransform frame)
        {
            _target = frame;
        }

        /// <inheritdoc/>
        public void OnViewDestroy()
        {
            _target = null;
        }

        /// <inheritdoc/>
        public async UniTask ShowAsync(CancellationToken cancellationToken)
        {
            if (_target == null) return;

            var canvasGroup = GetOrAddCanvasGroup(_target);
            var startPos = _target.anchoredPosition - new Vector2(0, MoveDistance);
            var endPos = _target.anchoredPosition;

            // 初期状態
            canvasGroup.alpha = 0f;
            _target.anchoredPosition = startPos;

            await AnimateAsync(_target, canvasGroup, startPos, endPos, 0f, 1f, ShowDuration, ShowEase, cancellationToken);
        }

        /// <inheritdoc/>
        public async UniTask HideAsync(CancellationToken cancellationToken)
        {
            if (_target == null) return;

            var canvasGroup = GetOrAddCanvasGroup(_target);
            var startPos = _target.anchoredPosition;
            var endPos = startPos - new Vector2(0, MoveDistance);

            await AnimateAsync(_target, canvasGroup, startPos, endPos, 1f, 0f, HideDuration, HideEase, cancellationToken);
        }

        private static CanvasGroup GetOrAddCanvasGroup(RectTransform target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }

        private static async UniTask AnimateAsync(
            RectTransform target,
            CanvasGroup canvasGroup,
            Vector2 startPos,
            Vector2 endPos,
            float startAlpha,
            float endAlpha,
            float duration,
            AnimationCurve curve,
            CancellationToken cancellationToken)
        {
            if (duration <= 0)
            {
                target.anchoredPosition = endPos;
                canvasGroup.alpha = endAlpha;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = curve.Evaluate(t);

                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, easedT);
                canvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, endAlpha, easedT);

                await UniTask.Yield(cancellationToken);
            }

            target.anchoredPosition = endPos;
            canvasGroup.alpha = endAlpha;
        }
    }
}
