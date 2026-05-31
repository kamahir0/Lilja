using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトの表示/非表示アニメーション。
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

        /// <inheritdoc />
        public void OnViewLoaded(RectTransform frame)
        {
            _target = frame;
        }

        /// <inheritdoc />
        public void OnViewUnload()
        {
            _target = null;
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">アニメーションターゲットがロードされていない場合にスローされます。</exception>
        public async UniTask ShowAsync(CancellationToken cancellationToken)
        {
            if (_target == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] アニメーション対象のビュー RectTransform がロードされていません。OnViewLoaded が正しく実行されているか確認してください。アニメーションをスキップします。"
                );
                return;
            }

            var canvasGroup = GetOrAddCanvasGroup(_target);
            var startPos = _target.anchoredPosition - new Vector2(0, MoveDistance);
            var endPos = _target.anchoredPosition;

            canvasGroup.alpha = 0f;
            _target.anchoredPosition = startPos;

            await AnimateAsync(
                _target,
                canvasGroup,
                startPos,
                endPos,
                0f,
                1f,
                ShowDuration,
                ShowEase,
                cancellationToken
            );
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">アニメーションターゲットがロードされていない場合にスローされます。</exception>
        public async UniTask HideAsync(CancellationToken cancellationToken)
        {
            if (_target == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] アニメーション対象のビュー RectTransform がロードされていません。OnViewLoaded が正しく実行されているか確認してください。アニメーションをスキップします。"
                );
                return;
            }

            var canvasGroup = GetOrAddCanvasGroup(_target);
            var startPos = _target.anchoredPosition;
            var endPos = startPos - new Vector2(0, MoveDistance);

            await AnimateAsync(
                _target,
                canvasGroup,
                startPos,
                endPos,
                1f,
                0f,
                HideDuration,
                HideEase,
                cancellationToken
            );
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
            CancellationToken cancellationToken
        )
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

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = curve.Evaluate(t);

                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, easedT);
                canvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, endAlpha, easedT);

                await UniTask.Yield(cancellationToken);
            }

            target.anchoredPosition = endPos;
            canvasGroup.alpha = endAlpha;
        }

        private RectTransform _target;
    }

    /// <summary>
    /// デフォルトのスタック演出アニメーション。
    /// </summary>
    public class DefaultStackAnimation : IDialogStackAnimation
    {
        /// <summary> 退避（奥に引っ込む）時の移動距離 </summary>
        public float PushDistance { get; set; } = 800f;

        /// <summary> 退避アニメーションの時間 </summary>
        public float PushDuration { get; set; } = 0.3f;

        /// <summary> 復帰アニメーションの時間 </summary>
        public float PopDuration { get; set; } = 0.3f;

        /// <summary> 退避時のイージング </summary>
        public AnimationCurve PushEase { get; set; } = CreateEaseOutCurve();

        /// <summary> 復帰時のイージング </summary>
        public AnimationCurve PopEase { get; set; } = CreateEaseOutCurve();

        /// <inheritdoc />
        public void OnViewLoaded(RectTransform frame)
        {
            _target = frame;

            if (_isPushed && _target != null)
            {
                _target.anchoredPosition = _pushedPosition;
            }
        }

        /// <inheritdoc />
        public void OnViewUnload()
        {
            _target = null;
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">アニメーションターゲットがロードされていない場合にスローされます。</exception>
        public async UniTask PushAsync(CancellationToken cancellationToken)
        {
            if (_target == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] アニメーション対象のビュー RectTransform がロードされていません。OnViewLoaded が正しく実行されているか確認してください。スタックアニメーションをスキップします。"
                );
                return;
            }

            var startPos = _target.anchoredPosition;
            var endPos = startPos + new Vector2(0, PushDistance);

            await AnimatePositionAsync(
                _target,
                startPos,
                endPos,
                PushDuration,
                PushEase,
                cancellationToken
            );

            _isPushed = true;
            _pushedPosition = _target.anchoredPosition;
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">アニメーションターゲットがロードされていない場合にスローされます。</exception>
        public async UniTask PopAsync(CancellationToken cancellationToken)
        {
            if (_target == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] アニメーション対象のビュー RectTransform がロードされていません。OnViewLoaded が正しく実行されているか確認してください。スタックアニメーションをスキップします。"
                );
                return;
            }

            var startPos = _target.anchoredPosition;
            var endPos = startPos - new Vector2(0, PushDistance);

            await AnimatePositionAsync(
                _target,
                startPos,
                endPos,
                PopDuration,
                PopEase,
                cancellationToken
            );

            _isPushed = false;
        }

        private static async UniTask AnimatePositionAsync(
            RectTransform target,
            Vector2 startPos,
            Vector2 endPos,
            float duration,
            AnimationCurve curve,
            CancellationToken cancellationToken
        )
        {
            if (duration <= 0)
            {
                target.anchoredPosition = endPos;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = curve.Evaluate(t);
                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, easedT);

                await UniTask.Yield(cancellationToken);
            }

            target.anchoredPosition = endPos;
        }

        private static AnimationCurve CreateEaseOutCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        }

        private RectTransform _target;
        private bool _isPushed;
        private Vector2 _pushedPosition;
    }
}
