using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// ゲームオーバー画面のView（MVP - 表示のみ担当）
    /// DOTweenによるリッチなアニメーション
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        [Header("UI")] [SerializeField] private Button _titleButton;
        [SerializeField] private Text _gameOverText;

        [Header("3D")] [SerializeField] private Transform _fallingCubesContainer;
        [SerializeField] private float _fallSpeed = 5f;

        [Header("アニメーション設定")] [SerializeField] private float _textAppearDelay = 0.5f;
        [SerializeField] private float _buttonAppearDelay = 1.5f;

        /// <summary> タイトルへ戻るボタン </summary>
        public Button TitleButton => _titleButton;

        private void Start()
        {
            // テキストのフェードイン + スケールバウンス
            if (_gameOverText != null)
            {
                var canvasGroup = _gameOverText.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = _gameOverText.gameObject.AddComponent<CanvasGroup>();
                }

                canvasGroup.alpha = 0f;
                _gameOverText.transform.localScale = Vector3.one * 0.5f;

                DOTween.Sequence()
                    .AppendInterval(_textAppearDelay)
                    .Append(canvasGroup.DOFade(1f, 0.5f))
                    .Join(_gameOverText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack))
                    .SetLink(_gameOverText.gameObject);
            }

            // ボタンの遅延表示 + パルス
            if (_titleButton != null)
            {
                _titleButton.transform.localScale = Vector3.zero;

                DOTween.Sequence()
                    .AppendInterval(_buttonAppearDelay)
                    .Append(_titleButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack))
                    .AppendCallback(() =>
                    {
                        // パルスアニメーション開始
                        _titleButton.transform
                            .DOScale(Vector3.one * 1.05f, 0.8f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine)
                            .SetLink(_titleButton.gameObject);
                    })
                    .SetLink(_titleButton.gameObject);

                // クリック時のバウンスフィードバック
                _titleButton.onClick.AddListener(() =>
                {
                    DOTween.Kill(_titleButton.transform);
                    _titleButton.transform.localScale = Vector3.one;
                    _titleButton.transform
                        .DOPunchScale(Vector3.one * 0.2f, 0.3f, 8, 0.5f)
                        .SetLink(_titleButton.gameObject);
                });
            }

            // 落下キューブの回転アニメーション
            if (_fallingCubesContainer != null)
            {
                foreach (Transform cube in _fallingCubesContainer)
                {
                    // ランダムな回転
                    cube.DORotate(new Vector3(360, 360, 360), Random.Range(2f, 4f), RotateMode.FastBeyond360)
                        .SetLoops(-1)
                        .SetEase(Ease.Linear)
                        .SetLink(cube.gameObject);
                }
            }
        }

        private void Update()
        {
            // 落下アニメーション（常時アニメーション）
            if (_fallingCubesContainer != null)
            {
                foreach (Transform cube in _fallingCubesContainer)
                {
                    cube.position += Vector3.down * _fallSpeed * Time.deltaTime;

                    // 画面下に落ちたら上にリセット
                    if (cube.position.y < -10f)
                    {
                        cube.position = new Vector3(
                            cube.position.x,
                            10f,
                            cube.position.z
                        );
                    }
                }
            }
        }

        private void OnDestroy()
        {
            DOTween.Kill(transform);
        }
    }
}

