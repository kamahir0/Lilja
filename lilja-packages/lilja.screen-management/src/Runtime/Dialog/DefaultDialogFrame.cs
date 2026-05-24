using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトデザインのダイアログ用フレームコンポーネント。
    /// </summary>
    public class DefaultDialogFrame : MonoBehaviour, IDialogFrame
    {
        [SerializeField]
        private RectTransform _contentContainer;

        [SerializeField]
        private Text _titleText;

        [SerializeField]
        private Transform _buttonContainer;

        [SerializeField]
        private GameObject _buttonPrefab;

        [SerializeField]
        private RectTransform _frameRect;

        /// <inheritdoc />
        public RectTransform ContentContainer => _contentContainer;

        /// <summary>
        /// タイトルテキストコンポーネントを取得します。
        /// </summary>
        public Text TitleText => _titleText;

        /// <summary>
        /// ボタン配置用のコンテナを取得します。
        /// </summary>
        public Transform ButtonContainer => _buttonContainer;

        /// <summary>
        /// ボタンのプレハブを取得します。
        /// </summary>
        public GameObject ButtonPrefab => _buttonPrefab;

        /// <summary>
        /// タイトルの文字列を設定します。
        /// </summary>
        /// <param name="title">表示するタイトル文字列。</param>
        public void SetTitle(string title)
        {
            if (_titleText == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame のタイトルテキストコンポーネント（_titleText）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }
            _titleText.text = title;
        }

        /// <summary>
        /// ダイアログにボタンを追加します。
        /// </summary>
        /// <param name="label">ボタンのラベルテキスト。</param>
        /// <param name="onClick">ボタンが押下された際に実行されるコールバックアクション。</param>
        public void AddButton(string label, Action onClick)
        {
            if (_buttonContainer == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame のボタン配置コンテナ（_buttonContainer）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }

            if (_buttonPrefab == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame のボタンプレハブ（_buttonPrefab）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }

            var buttonObj = Instantiate(_buttonPrefab, _buttonContainer);
            if (buttonObj == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame でボタンプレハブの生成に失敗しました。"
                );
                return;
            }
            buttonObj.SetActive(true);

            var button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError(
                    $"[Lilja.ScreenManagement.Dialog] 生成されたボタンオブジェクトに Button コンポーネントが見つかりません。ラベル: {label}"
                );
            }
            else
            {
                button.onClick.AddListener(() => onClick?.Invoke());
            }

            // ボタンのラベルテキストを設定
            var text = buttonObj.GetComponentInChildren<Text>();
            if (text == null)
            {
                Debug.LogError(
                    $"[Lilja.ScreenManagement.Dialog] 生成されたボタンオブジェクトに Text コンポーネントが見つかりません。ラベル: {label}"
                );
            }
            else
            {
                text.text = label;
            }
        }

        private float _originalWidth = 600f;
        private float _maxFrameHeight = 500f;
        private bool _isSizeInitialized;

        private void InitializeOriginalSizes()
        {
            if (_isSizeInitialized)
            {
                return;
            }

            var rectTransform = _frameRect != null ? _frameRect : GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                _originalWidth = rectTransform.sizeDelta.x;
                _maxFrameHeight = rectTransform.sizeDelta.y;
            }
            _isSizeInitialized = true;
        }

        /// <summary>
        /// ダイアログのコンテンツとボタンの追加状態に基づいて、ダイアログ全体のレイアウトと高さを動的に自動調整します。
        /// </summary>
        /// <param name="content">ダイアログのコンテンツコンポーネント。</param>
        public void AdjustLayout(DefaultDialogContent content)
        {
            // インスタンス化時のプレハブ設定サイズ（幅と高さの初期値）を自動キャッシュ
            InitializeOriginalSizes();

            // 1. 対象のフレーム RectTransform を決定 (未設定なら自身を使用)
            var rectTransform = _frameRect != null ? _frameRect : GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }

            // 2. 定数・サイズ定義
            float maxHeight = _maxFrameHeight; // プレハブ側の設定サイズ（高さ）を最大高さとしてそのまま採用
            const float headerHeight = 70f; // タイトルエリアの高さ (上余白込み)
            const float padding = 30f; // コンテンツ上下の最小パディング

            // 3. ボタンの存在確認とカウント
            int buttonCount = 0;
            if (_buttonContainer != null)
            {
                foreach (Transform child in _buttonContainer)
                {
                    if (child.gameObject.activeSelf)
                    {
                        buttonCount++;
                    }
                }
            }

            // 4. ボタン行の高さの算出（ButtonPrefab の height から自動計算）
            float buttonPrefabHeight = 50f; // フォールバック用の初期高さ
            if (_buttonPrefab != null)
            {
                var buttonPrefabRect = _buttonPrefab.GetComponent<RectTransform>();
                if (buttonPrefabRect != null)
                {
                    buttonPrefabHeight = buttonPrefabRect.rect.height;
                }
            }

            // ボタンコンテナ自体の上下パディング（各15f、合計30f）を考慮した総高さ
            float buttonRowHeight = buttonPrefabHeight + 30f;
            float buttonHeight = buttonCount > 0 ? buttonRowHeight : 0f;

            // 5. テキストが必要とする高さの算出 (preferredHeight)
            float textNeededHeight = 0f;
            if (
                content != null
                && content.BodyText != null
                && !string.IsNullOrEmpty(content.BodyText.text)
            )
            {
                textNeededHeight = content.BodyText.preferredHeight;
            }

            // 6. ダイアログとして必要な全体の高さを算出
            float totalNeededHeight = headerHeight + textNeededHeight + buttonHeight + padding;

            // 7. フレーム全体の高さを決定 (最大値でクランプ)
            float finalFrameHeight = Mathf.Min(totalNeededHeight, maxHeight);

            // フレームサイズを設定 (幅はプレハブで指定されたオリジナル幅を採用)
            rectTransform.sizeDelta = new Vector2(_originalWidth, finalFrameHeight);

            // 8. ボタンコンテナの表示状態とアンカー位置の調整
            if (_buttonContainer != null)
            {
                var buttonRect = _buttonContainer.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    if (buttonCount > 0)
                    {
                        _buttonContainer.gameObject.SetActive(true);
                        // 下部アンカー固定
                        buttonRect.anchorMin = new Vector2(0f, 0f);
                        buttonRect.anchorMax = new Vector2(1f, 0f);
                        buttonRect.pivot = new Vector2(0.5f, 0f);
                        buttonRect.anchoredPosition = new Vector2(0f, 15f); // 下から15fの余白
                        buttonRect.sizeDelta = new Vector2(-60f, buttonPrefabHeight); // プレハブの高さに基づく自動設定
                    }
                    else
                    {
                        _buttonContainer.gameObject.SetActive(false);
                    }
                }
            }

            // 9. コンテンツコンテナ（_contentContainer）のオフセット調整
            if (_contentContainer != null)
            {
                _contentContainer.anchorMin = new Vector2(0f, 0f);
                _contentContainer.anchorMax = new Vector2(1f, 1f);
                _contentContainer.pivot = new Vector2(0.5f, 0.5f);

                // ボタンがある場合はボタンエリアの総高さ（buttonRowHeight）分空け、ない場合は最小余白でアンカーを伸縮
                float bottomOffset = buttonCount > 0 ? buttonRowHeight : 15f;
                _contentContainer.offsetMin = new Vector2(20f, bottomOffset); // 横パディング20f、下オフセット
                _contentContainer.offsetMax = new Vector2(-20f, -headerHeight); // 横パディング-20f、上オフセット（ヘッダー分）
            }
        }
    }
}
