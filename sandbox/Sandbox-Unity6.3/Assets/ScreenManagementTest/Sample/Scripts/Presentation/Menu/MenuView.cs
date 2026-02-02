using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// メニュー画面のView（MVP - 表示のみ担当）
    /// DOTweenによるパネルアニメーション
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _statusText;
        [SerializeField] private RectTransform _panelTransform;

        [Header("アニメーション設定")] [SerializeField] private float _showDuration = 0.3f;
        [SerializeField] private float _hideDuration = 0.2f;

        private CanvasGroup _canvasGroup;

        /// <summary> 閉じるボタン </summary>
        public Button CloseButton => _closeButton;

        private void Awake()
        {
            // CanvasGroupを取得または追加
            if (_panelTransform != null)
            {
                _canvasGroup = _panelTransform.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = _panelTransform.gameObject.AddComponent<CanvasGroup>();
                }
            }

            // ボタンのクリックフィードバック
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(() =>
                {
                    _closeButton.transform
                        .DOPunchScale(Vector3.one * 0.1f, 0.15f, 8, 0.5f)
                        .SetLink(_closeButton.gameObject);
                });
            }
        }

        #region UI表示（即座に反映）

        /// <summary>
        /// ステータスを設定
        /// </summary>
        public void SetStatus(string name, int currentHp, int maxHp, int attack, int defense)
        {
            if (_statusText != null)
            {
                _statusText.text = $"名前: {name}\n" +
                                   $"HP: {currentHp}/{maxHp}\n" +
                                   $"攻撃力: {attack}\n" +
                                   $"防御力: {defense}";
            }
        }

        #endregion

        #region アニメーション

        /// <summary>
        /// パネルを表示（アニメーション付き）
        /// </summary>
        public async UniTask ShowAsync(CancellationToken cancellationToken)
        {
            if (_panelTransform == null) return;

            // 初期状態
            _panelTransform.localScale = Vector3.zero;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            // スケール + フェード
            var sequence = DOTween.Sequence()
                .Join(_panelTransform.DOScale(Vector3.one, _showDuration).SetEase(Ease.OutBack))
                .Join(_canvasGroup != null
                    ? _canvasGroup.DOFade(1f, _showDuration)
                    : DOTween.Sequence())
                .SetLink(_panelTransform.gameObject);

            await sequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        /// <summary>
        /// パネルを非表示（アニメーション付き）
        /// </summary>
        public async UniTask HideAsync(CancellationToken cancellationToken)
        {
            if (_panelTransform == null) return;

            var sequence = DOTween.Sequence()
                .Join(_panelTransform.DOScale(Vector3.zero, _hideDuration).SetEase(Ease.InBack))
                .Join(_canvasGroup != null
                    ? _canvasGroup.DOFade(0f, _hideDuration)
                    : DOTween.Sequence())
                .SetLink(_panelTransform.gameObject);

            await sequence.AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        #endregion

        private void OnDestroy()
        {
            DOTween.Kill(_panelTransform);
        }
    }
}

