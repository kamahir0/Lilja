using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// タイトル画面のView（MVP - 表示のみ担当）
    /// DOTweenによるリッチなアニメーション
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [Header("UI")] [SerializeField] private Button _startButton;

        [Header("3D")] [SerializeField] private Transform _decorationModel;
        [SerializeField] private float _rotationSpeed = 30f;
        [SerializeField] private float _floatHeight = 0.3f;
        [SerializeField] private float _floatDuration = 1.5f;

        [Header("ボタンアニメーション")] [SerializeField]
        private float _pulseScale = 1.05f;

        [SerializeField] private float _pulseDuration = 0.8f;

        private Vector3 _decorationOriginalPosition;
        private Tweener _buttonPulseTween;
        private Sequence _decorationSequence;

        /// <summary> スタートボタン </summary>
        public Button StartButton => _startButton;

        private void Awake()
        {
            if (_decorationModel != null)
            {
                _decorationOriginalPosition = _decorationModel.position;
            }
        }

        private void Start()
        {
            // スタートボタンのパルスアニメーション
            if (_startButton != null)
            {
                _buttonPulseTween = _startButton.transform
                    .DOScale(Vector3.one * _pulseScale, _pulseDuration)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(_startButton.gameObject);

                // クリック時のバウンスフィードバック
                _startButton.onClick.AddListener(OnStartButtonClicked);
            }

            // 装飾モデルの回転 + 浮遊アニメーション
            if (_decorationModel != null)
            {
                // 無限ループの浮遊
                _decorationSequence = DOTween.Sequence()
                    .Append(_decorationModel.DOMoveY(_decorationOriginalPosition.y + _floatHeight, _floatDuration).SetEase(Ease.InOutSine))
                    .Append(_decorationModel.DOMoveY(_decorationOriginalPosition.y, _floatDuration).SetEase(Ease.InOutSine))
                    .SetLoops(-1)
                    .SetLink(_decorationModel.gameObject);

                // 回転（別Tween）
                _decorationModel
                    .DORotate(new Vector3(0, 360, 0), 360f / _rotationSpeed, RotateMode.FastBeyond360)
                    .SetLoops(-1, LoopType.Restart)
                    .SetEase(Ease.Linear)
                    .SetLink(_decorationModel.gameObject);
            }
        }

        /// <summary>
        /// スタートボタンクリック時のフィードバック
        /// </summary>
        private void OnStartButtonClicked()
        {
            // パルスを停止してバウンス
            _buttonPulseTween?.Kill();
            _startButton.transform.localScale = Vector3.one;

            _startButton.transform
                .DOPunchScale(Vector3.one * 0.2f, 0.3f, 8, 0.5f)
                .SetLink(_startButton.gameObject);
        }

        private void OnDestroy()
        {
            _buttonPulseTween?.Kill();
            _decorationSequence?.Kill();

            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartButtonClicked);
            }
        }
    }
}

