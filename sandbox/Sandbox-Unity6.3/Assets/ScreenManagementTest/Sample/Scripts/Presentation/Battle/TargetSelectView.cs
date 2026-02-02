using System;
using System.Collections.Generic;
using DG.Tweening;
using ScreenManagementSample.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// ターゲット選択画面のView（MVP - 表示のみ担当）
    /// DOTweenによるボタンアニメーション
    /// </summary>
    public class TargetSelectView : MonoBehaviour
    {
        [Header("UI")] [SerializeField] private Transform _buttonContainer;
        [SerializeField] private Button _targetButtonPrefab;
        [SerializeField] private Button _backButton;
        [SerializeField] private Text _descriptionText;

        [Header("アニメーション設定")] [SerializeField] private float _staggerDelay = 0.08f;
        [SerializeField] private float _appearDuration = 0.2f;

        private readonly List<Button> _createdButtons = new List<Button>();

        /// <summary> 戻るボタン </summary>
        public Button BackButton => _backButton;

        private void Awake()
        {
            // 戻るボタンのフィードバック
            if (_backButton != null)
            {
                _backButton.onClick.AddListener(() =>
                {
                    _backButton.transform
                        .DOPunchScale(Vector3.one * 0.1f, 0.15f, 8, 0.5f)
                        .SetLink(_backButton.gameObject);
                });
            }
        }

        /// <summary>
        /// ターゲットボタンを動的に生成（スタガーアニメーション付き）
        /// </summary>
        public void SetTargets(Enemy[] enemies, Action<Enemy> onTargetSelected)
        {
            ClearTargetButtons();

            int buttonIndex = 0;
            foreach (var enemy in enemies)
            {
                // 死亡している敵はスキップ
                if (!enemy.IsAlive) continue;

                var button = Instantiate(_targetButtonPrefab, _buttonContainer);

                // 初期状態：非表示
                button.transform.localScale = Vector3.zero;
                button.gameObject.SetActive(true);

                // ボタンテキストを設定（敵名 + HP）
                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = $"{enemy.Name} (HP:{enemy.CurrentHp}/{enemy.MaxHp})";
                }

                // スタガーアニメーション
                button.transform
                    .DOScale(Vector3.one, _appearDuration)
                    .SetEase(Ease.OutBack)
                    .SetDelay(buttonIndex * _staggerDelay)
                    .SetLink(button.gameObject);

                // クリックイベントを設定
                var capturedEnemy = enemy;
                button.onClick.AddListener(() =>
                {
                    // クリック時のバウンス
                    button.transform
                        .DOPunchScale(Vector3.one * 0.15f, 0.15f, 8, 0.5f)
                        .SetLink(button.gameObject)
                        .OnComplete(() => onTargetSelected?.Invoke(capturedEnemy));
                });

                _createdButtons.Add(button);
                buttonIndex++;
            }
        }

        /// <summary>
        /// 生成したターゲットボタンをクリア
        /// </summary>
        public void ClearTargetButtons()
        {
            foreach (var button in _createdButtons)
            {
                if (button != null)
                {
                    DOTween.Kill(button.transform);
                    Destroy(button.gameObject);
                }
            }

            _createdButtons.Clear();
        }

        #region UI表示（即座に反映）

        /// <summary>
        /// 説明テキストを設定
        /// </summary>
        public void SetDescription(string description)
        {
            if (_descriptionText != null)
            {
                _descriptionText.text = description;
            }
        }

        #endregion

        private void OnDestroy()
        {
            DOTween.Kill(transform);
        }
    }
}

