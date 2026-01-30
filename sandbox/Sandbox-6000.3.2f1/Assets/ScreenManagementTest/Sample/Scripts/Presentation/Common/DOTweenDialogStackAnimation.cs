using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lilja.ScreenManagement.Dialog;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// DOTweenを使用したリッチなダイアログスタックアニメーション
    /// </summary>
    public class DOTweenDialogStackAnimation : IDialogStackAnimation
    {
        /// <summary> 退避時の移動距離 </summary>
        public float PushDistance { get; set; } = 100f;

        /// <summary> 退避時のスケール </summary>
        public float PushScale { get; set; } = 0.9f;

        /// <summary> 退避時のアルファ </summary>
        public float PushAlpha { get; set; } = 0.5f;

        /// <summary> 退避アニメーションの時間 </summary>
        public float PushDuration { get; set; } = 0.25f;

        /// <summary> 復帰アニメーションの時間 </summary>
        public float PopDuration { get; set; } = 0.25f;

        // 内部状態（Pure C# オブジェクトの寿命に紐づく）
        private RectTransform _target;
        private CanvasGroup _canvasGroup;
        private bool _isPushed;
        private Vector2 _originalPosition;
        private Vector2 _pushedPosition;
        private Sequence _currentSequence;

        /// <inheritdoc/>
        public void OnViewInstanced(RectTransform frame)
        {
            _target = frame;

            // Pushed 状態であれば、保存された位置を復元
            if (_isPushed && _target != null)
            {
                _target.anchoredPosition = _pushedPosition;
                _target.localScale = Vector3.one * PushScale;

                // CanvasGroupの状態も復元
                _canvasGroup = _target.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _target.gameObject.AddComponent<CanvasGroup>();
                }
                _canvasGroup.alpha = PushAlpha;
            }
            else
            {
                _originalPosition = frame.anchoredPosition;
                _canvasGroup = _target.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _target.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        /// <inheritdoc/>
        public void OnViewDestroy()
        {
            _currentSequence?.Kill();
            _currentSequence = null;
            _target = null;
            _canvasGroup = null;
        }

        /// <inheritdoc/>
        public async UniTask PushAsync(CancellationToken ct)
        {
            if (_target == null) return;

            // 既存のアニメーションをキル
            _currentSequence?.Kill();

            // 現在位置から退避
            _originalPosition = _target.anchoredPosition;
            var endPos = _originalPosition + new Vector2(0, PushDistance);

            // DOTweenでアニメーション
            _currentSequence = DOTween.Sequence()
                .Join(_target.DOAnchorPos(endPos, PushDuration).SetEase(Ease.OutQuad))
                .Join(_target.DOScale(Vector3.one * PushScale, PushDuration).SetEase(Ease.OutQuad))
                .Join(_canvasGroup.DOFade(PushAlpha, PushDuration).SetEase(Ease.OutQuad))
                .SetLink(_target.gameObject);

            await _currentSequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(ct);

            // Pushed 状態を記録
            _isPushed = true;
            _pushedPosition = _target.anchoredPosition;
        }

        /// <inheritdoc/>
        public async UniTask PopAsync(CancellationToken ct)
        {
            if (_target == null) return;

            // 既存のアニメーションをキル
            _currentSequence?.Kill();

            // DOTweenでアニメーション
            _currentSequence = DOTween.Sequence()
                .Join(_target.DOAnchorPos(_originalPosition, PopDuration).SetEase(Ease.OutBack))
                .Join(_target.DOScale(Vector3.one, PopDuration).SetEase(Ease.OutBack))
                .Join(_canvasGroup.DOFade(1f, PopDuration).SetEase(Ease.OutQuad))
                .SetLink(_target.gameObject);

            await _currentSequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(ct);

            // Pushed 状態を解除
            _isPushed = false;
        }
    }
}
