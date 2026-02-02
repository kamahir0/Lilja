using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lilja.ScreenManagement.Dialog;
using UnityEngine;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// DOTweenを使用したリッチなダイアログ表示/非表示アニメーション
    /// </summary>
    public class DOTweenDialogAnimation : IDialogAnimation
    {
        /// <summary> 表示アニメーションの時間 </summary>
        public float ShowDuration { get; set; } = 0.3f;

        /// <summary> 非表示アニメーションの時間 </summary>
        public float HideDuration { get; set; } = 0.2f;

        /// <summary> 表示開始時のスケール </summary>
        public float StartScale { get; set; } = 0.8f;

        /// <summary> 表示開始時のY方向オフセット </summary>
        public float StartOffsetY { get; set; } = 30f;

        // 内部状態
        private RectTransform _target;
        private CanvasGroup _canvasGroup;
        private Vector2 _originalPosition;
        private Sequence _currentSequence;

        /// <inheritdoc/>
        public void OnViewInstanced(RectTransform frame)
        {
            _target = frame;
            _originalPosition = frame.anchoredPosition;

            // CanvasGroupを取得または追加
            _canvasGroup = _target.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = _target.gameObject.AddComponent<CanvasGroup>();
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
        public async UniTask ShowAsync(CancellationToken cancellationToken)
        {
            if (_target == null) return;

            // 既存のアニメーションをキル
            _currentSequence?.Kill();

            // 初期状態を設定
            _target.localScale = Vector3.one * StartScale;
            _target.anchoredPosition = _originalPosition - new Vector2(0, StartOffsetY);
            _canvasGroup.alpha = 0f;

            // DOTweenでアニメーション
            _currentSequence = DOTween.Sequence()
                .Join(_target.DOScale(Vector3.one, ShowDuration).SetEase(Ease.OutBack))
                .Join(_target.DOAnchorPos(_originalPosition, ShowDuration).SetEase(Ease.OutQuart))
                .Join(_canvasGroup.DOFade(1f, ShowDuration * 0.7f).SetEase(Ease.OutQuad))
                .SetLink(_target.gameObject);

            await _currentSequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        /// <inheritdoc/>
        public async UniTask HideAsync(CancellationToken cancellationToken)
        {
            if (_target == null) return;

            // 既存のアニメーションをキル
            _currentSequence?.Kill();

            var endPos = _originalPosition - new Vector2(0, StartOffsetY);

            // DOTweenでアニメーション
            _currentSequence = DOTween.Sequence()
                .Join(_target.DOScale(Vector3.one * StartScale, HideDuration).SetEase(Ease.InBack))
                .Join(_target.DOAnchorPos(endPos, HideDuration).SetEase(Ease.InQuart))
                .Join(_canvasGroup.DOFade(0f, HideDuration).SetEase(Ease.InQuad))
                .SetLink(_target.gameObject);

            await _currentSequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }
    }
}
