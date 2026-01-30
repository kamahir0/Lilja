using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// マップ画面のView（MVP - 表示のみ担当）
    /// DOTweenによるスムーズなアニメーション
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Header("UI")] [SerializeField] private Button _menuButton;
        [SerializeField] private Button _upButton;
        [SerializeField] private Button _downButton;
        [SerializeField] private Button _leftButton;
        [SerializeField] private Button _rightButton;
        [SerializeField] private Button _interactButton;
        [SerializeField] private Text _positionText;
        [SerializeField] private Text _hpText;

        [Header("3D")] [SerializeField] private Transform _playerModel;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private float _tileSize = 1.5f;
        [SerializeField] private float _moveDuration = 0.25f;

        [Header("インタラクト表示")] [SerializeField] private Color _interactTileColor = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color _normalTileColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private float _pulseIntensity = 0.3f;

        private bool _isMoving;
        private Dictionary<(int, int), Renderer> _tileRenderers;
        private Dictionary<(int, int), Tweener> _tilePulseTweens;

        /// <summary> メニューボタン </summary>
        public Button MenuButton => _menuButton;

        /// <summary> 上移動ボタン </summary>
        public Button UpButton => _upButton;

        /// <summary> 下移動ボタン </summary>
        public Button DownButton => _downButton;

        /// <summary> 左移動ボタン </summary>
        public Button LeftButton => _leftButton;

        /// <summary> 右移動ボタン </summary>
        public Button RightButton => _rightButton;

        /// <summary> インタラクトボタン </summary>
        public Button InteractButton => _interactButton;

        /// <summary> 移動中かどうか </summary>
        public bool IsMoving => _isMoving;

        private void Awake()
        {
            // タイルレンダラーをキャッシュ
            CacheTileRenderers();
            _tilePulseTweens = new Dictionary<(int, int), Tweener>();

            // ボタンのクリックフィードバックを設定
            SetupButtonFeedback(_menuButton);
            SetupButtonFeedback(_upButton);
            SetupButtonFeedback(_downButton);
            SetupButtonFeedback(_leftButton);
            SetupButtonFeedback(_rightButton);
            SetupButtonFeedback(_interactButton);
        }

        /// <summary>
        /// ボタンにクリック時のバウンスフィードバックを設定
        /// </summary>
        private void SetupButtonFeedback(Button button)
        {
            if (button == null) return;

            button.onClick.AddListener(() =>
            {
                button.transform
                    .DOPunchScale(Vector3.one * 0.1f, 0.15f, 8, 0.5f)
                    .SetLink(button.gameObject);
            });
        }

        /// <summary>
        /// タイルのRendererをキャッシュする
        /// </summary>
        private void CacheTileRenderers()
        {
            _tileRenderers = new Dictionary<(int, int), Renderer>();

            if (_gridContainer == null)
            {
                return;
            }

            // グリッドコンテナの子オブジェクトからタイルを探す
            foreach (Transform child in _gridContainer)
            {
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 位置からタイル座標を計算
                    var x = Mathf.RoundToInt(child.localPosition.x / _tileSize);
                    var y = Mathf.RoundToInt(child.localPosition.z / _tileSize);
                    _tileRenderers[(x, y)] = renderer;
                }
            }
        }

        #region UI表示（即座に反映）

        /// <summary>
        /// 位置テキストを設定（アニメなし）
        /// </summary>
        public void SetPositionText(int x, int y)
        {
            if (_positionText != null)
            {
                _positionText.text = $"位置: ({x}, {y})";
            }
        }

        /// <summary>
        /// HPテキストを設定（アニメなし）
        /// </summary>
        public void SetHpText(int current, int max)
        {
            if (_hpText != null)
            {
                _hpText.text = $"HP: {current}/{max}";
            }
        }

        /// <summary>
        /// インタラクトボタンの有効/無効を設定（アニメ付き）
        /// </summary>
        public void SetInteractButtonActive(bool active)
        {
            if (_interactButton == null) return;

            if (active && !_interactButton.gameObject.activeSelf)
            {
                _interactButton.gameObject.SetActive(true);
                _interactButton.transform.localScale = Vector3.zero;
                _interactButton.transform
                    .DOScale(Vector3.one, 0.2f)
                    .SetEase(Ease.OutBack)
                    .SetLink(_interactButton.gameObject);
            }
            else if (!active && _interactButton.gameObject.activeSelf)
            {
                _interactButton.transform
                    .DOScale(Vector3.zero, 0.15f)
                    .SetEase(Ease.InBack)
                    .SetLink(_interactButton.gameObject)
                    .OnComplete(() => _interactButton.gameObject.SetActive(false));
            }
        }

        #endregion

        #region 3D表示（即座に反映）

        /// <summary>
        /// プレイヤーの3D位置を即座に設定（アニメなし）
        /// </summary>
        public void SetPlayerPosition(int x, int y)
        {
            if (_playerModel != null)
            {
                DOTween.Kill(_playerModel);
                var position = new Vector3(x * _tileSize, 0.5f, y * _tileSize);
                _playerModel.position = position;
                _isMoving = false;
            }
        }

        /// <summary>
        /// タイルの色を設定（DOTweenでフェード遷移）
        /// </summary>
        public void SetTileColor(int x, int y, bool isInteractable)
        {
            if (_tileRenderers == null || !_tileRenderers.TryGetValue((x, y), out var renderer))
            {
                return;
            }

            // 既存のパルスアニメをキル
            if (_tilePulseTweens.TryGetValue((x, y), out var existingTween))
            {
                existingTween.Kill();
                _tilePulseTweens.Remove((x, y));
            }

            var targetColor = isInteractable ? _interactTileColor : _normalTileColor;

            // 色をフェードで変更
            renderer.material
                .DOColor(targetColor, 0.3f)
                .SetLink(renderer.gameObject);

            // インタラクト可能ならパルスアニメーションを追加
            if (isInteractable)
            {
                var pulseColor = new Color(
                    _interactTileColor.r + _pulseIntensity,
                    _interactTileColor.g + _pulseIntensity,
                    _interactTileColor.b + _pulseIntensity,
                    _interactTileColor.a
                );

                var pulseTween = renderer.material
                    .DOColor(pulseColor, 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(renderer.gameObject);

                _tilePulseTweens[(x, y)] = pulseTween;
            }
        }

        /// <summary>
        /// 全タイルの色をリセット（通常色に戻す）
        /// </summary>
        public void ResetAllTileColors()
        {
            if (_tileRenderers == null)
            {
                return;
            }

            // すべてのパルスアニメをキル
            foreach (var tween in _tilePulseTweens.Values)
            {
                tween.Kill();
            }

            _tilePulseTweens.Clear();

            foreach (var kvp in _tileRenderers)
            {
                kvp.Value.material
                    .DOColor(_normalTileColor, 0.3f)
                    .SetLink(kvp.Value.gameObject);
            }
        }

        #endregion

        #region 3Dアニメーション（アニメ付き）

        /// <summary>
        /// プレイヤーを指定位置へ移動（DOTween版）
        /// </summary>
        public void MovePlayerTo(int x, int y)
        {
            if (_playerModel == null) return;

            _isMoving = true;
            var targetPosition = new Vector3(x * _tileSize, 0.5f, y * _tileSize);

            _playerModel
                .DOMove(targetPosition, _moveDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(_playerModel.gameObject)
                .OnComplete(() => _isMoving = false);
        }

        #endregion

        private void OnDestroy()
        {
            // すべてのTweenをクリーンアップ
            DOTween.Kill(transform);
            foreach (var tween in _tilePulseTweens.Values)
            {
                tween.Kill();
            }
        }
    }
}


