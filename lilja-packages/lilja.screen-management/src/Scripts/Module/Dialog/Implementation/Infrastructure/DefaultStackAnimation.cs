using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトのスタックアニメーション
    /// </summary>
    public class DefaultStackAnimation : IDialogStackAnimation
    {
        /// <summary> 退避時の移動距離 </summary>
        public float PushDistance { get; set; } = 800f;

        /// <summary> 退避アニメーションの時間 </summary>
        public float PushDuration { get; set; } = 0.3f;

        /// <summary> 復帰アニメーションの時間 </summary>
        public float PopDuration { get; set; } = 0.3f;

        /// <summary> 退避時のイージング </summary>
        public AnimationCurve PushEase { get; set; } = CreateEaseOutCurve();

        /// <summary> 復帰時のイージング </summary>
        public AnimationCurve PopEase { get; set; } = CreateEaseOutCurve();

        // 内部状態（Pure C# オブジェクトの寿命に紐づく）
        private RectTransform _target;
        private bool _isPushed;
        private Vector2 _pushedPosition;

        /// <inheritdoc/>
        public void OnViewInstanced(RectTransform frame)
        {
            _target = frame;

            // Pushed 状態であれば、保存された位置を復元
            if (_isPushed && _target != null)
            {
                _target.anchoredPosition = _pushedPosition;
            }
        }

        /// <inheritdoc/>
        public void OnViewDestroy()
        {
            _target = null;
        }

        /// <inheritdoc/>
        public async UniTask PushAsync(CancellationToken ct)
        {
            if (_target == null) return;

            var startPos = _target.anchoredPosition;
            var endPos = startPos + new Vector2(0, PushDistance);

            await AnimatePositionAsync(_target, startPos, endPos, PushDuration, PushEase, ct);

            // Pushed 状態を記録
            _isPushed = true;
            _pushedPosition = _target.anchoredPosition;
        }

        /// <inheritdoc/>
        public async UniTask PopAsync(CancellationToken ct)
        {
            if (_target == null) return;

            var startPos = _target.anchoredPosition;
            var endPos = startPos - new Vector2(0, PushDistance);

            await AnimatePositionAsync(_target, startPos, endPos, PopDuration, PopEase, ct);

            // Pushed 状態を解除
            _isPushed = false;
        }

        private static async UniTask AnimatePositionAsync(
            RectTransform target,
            Vector2 startPos,
            Vector2 endPos,
            float duration,
            AnimationCurve curve,
            CancellationToken ct)
        {
            if (duration <= 0)
            {
                target.anchoredPosition = endPos;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = curve.Evaluate(t);
                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, easedT);
                await UniTask.Yield(ct);
            }

            target.anchoredPosition = endPos;
        }

        private static AnimationCurve CreateEaseOutCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f),
                new Keyframe(1f, 1f, 0f, 0f)
            );
        }
    }
}
