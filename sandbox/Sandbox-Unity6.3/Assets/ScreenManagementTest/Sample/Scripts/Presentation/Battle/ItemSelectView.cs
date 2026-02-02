using System;
using System.Collections.Generic;
using DG.Tweening;
using ScreenManagementSample.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenManagementSample.Presentation
{
    /// <summary>
    /// アイテム選択画面のView（MVP - 表示のみ担当）
    /// DOTweenによるボタンアニメーション
    /// </summary>
    public class ItemSelectView : MonoBehaviour
    {
        [Header("UI")] [SerializeField] private Transform _buttonContainer;
        [SerializeField] private Button _itemButtonPrefab;
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
        /// アイテムボタンを動的に生成（スタガーアニメーション付き）
        /// </summary>
        public void SetItems(InventoryItem[] items, Action<InventoryItem> onItemSelected)
        {
            ClearItemButtons();

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var button = Instantiate(_itemButtonPrefab, _buttonContainer);

                // 初期状態：非表示
                button.transform.localScale = Vector3.zero;
                button.gameObject.SetActive(true);

                // ボタンテキストを設定（アイテム名 x 所持数）
                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = $"{item.Definition.Name} x{item.Count}";
                }

                // スタガーアニメーション
                button.transform
                    .DOScale(Vector3.one, _appearDuration)
                    .SetEase(Ease.OutBack)
                    .SetDelay(i * _staggerDelay)
                    .SetLink(button.gameObject);

                // クリックイベントを設定
                var capturedItem = item;
                button.onClick.AddListener(() =>
                {
                    // クリック時のバウンス
                    button.transform
                        .DOPunchScale(Vector3.one * 0.15f, 0.15f, 8, 0.5f)
                        .SetLink(button.gameObject)
                        .OnComplete(() => onItemSelected?.Invoke(capturedItem));
                });

                _createdButtons.Add(button);
            }
        }

        /// <summary>
        /// 生成したアイテムボタンをクリア
        /// </summary>
        public void ClearItemButtons()
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

