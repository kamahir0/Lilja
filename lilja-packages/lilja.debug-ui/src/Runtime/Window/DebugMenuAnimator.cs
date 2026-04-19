using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    internal static class DebugMenuAnimator
    {
        /// <summary>
        /// スケール + オパシティのアニメーション。shouldCancel が true を返したら即座に停止する。
        /// </summary>
        internal static void AnimateScaleOpacity(
            VisualElement target,
            float scaleFrom, float scaleTo,
            float opacityFrom, float opacityTo,
            float duration,
            Func<float, float> easing,
            Func<bool> shouldCancel,
            Action onComplete)
        {
            var elapsed = 0f;

            target.style.scale = new Scale(new Vector3(scaleFrom, scaleFrom, 1f));
            target.style.opacity = opacityFrom;

            target.schedule.Execute(timer =>
            {
                if (shouldCancel()) return;

                elapsed += timer.deltaTime / 1000f;
                var t = easing(Mathf.Clamp01(elapsed / duration));

                var s = Mathf.Lerp(scaleFrom, scaleTo, t);
                target.style.scale = new Scale(new Vector3(s, s, 1f));
                target.style.opacity = Mathf.Lerp(opacityFrom, opacityTo, t);

                if (elapsed >= duration)
                {
                    target.style.scale = new Scale(new Vector3(scaleTo, scaleTo, 1f));
                    target.style.opacity = opacityTo;
                    onComplete?.Invoke();
                }
            }).Every(0).Until(() => elapsed >= duration || shouldCancel());
        }

        /// <summary>
        /// 水平スライドアニメーション（% 単位）。shouldCancel が true を返したら即座に停止する。
        /// </summary>
        internal static void Slide(
            VisualElement target,
            VisualElement scheduler,
            float fromPercent, float toPercent,
            float duration,
            Func<bool> shouldCancel,
            Action onComplete)
        {
            target.style.left = new StyleLength(new Length(fromPercent, LengthUnit.Percent));

            float elapsed = 0f;
            scheduler.schedule.Execute(timer =>
            {
                if (shouldCancel()) return;

                elapsed += timer.deltaTime / 1000f;
                var t = EaseInOutCubic(Mathf.Clamp01(elapsed / duration));
                target.style.left = new StyleLength(
                    new Length(Mathf.Lerp(fromPercent, toPercent, t), LengthUnit.Percent));

                if (elapsed >= duration)
                {
                    target.style.left = new StyleLength(new Length(toPercent, LengthUnit.Percent));
                    onComplete?.Invoke();
                }
            }).Every(0).Until(() => elapsed >= duration || shouldCancel());
        }

        internal static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        internal static float EaseInCubic(float t) => t * t * t;

        private static float EaseInOutCubic(float t) =>
            t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
