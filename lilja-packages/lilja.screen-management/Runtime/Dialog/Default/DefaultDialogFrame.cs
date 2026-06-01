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
        /// <summary>
        /// コンテンツ配置用コンテナ。Inspector またはコードから直接設定します。
        /// </summary>
        public RectTransform ContentContainerRect;

        /// <summary>
        /// タイトルテキストコンポーネント。Inspector またはコードから直接設定します。
        /// </summary>
        public Text TitleText;

        /// <summary>
        /// ボタン配置用コンテナ。Inspector またはコードから直接設定します。
        /// </summary>
        public Transform ButtonContainer;

        /// <summary>
        /// ボタンのプレハブ。Inspector またはコードから直接設定します。
        /// </summary>
        public GameObject ButtonPrefab;

        /// <summary>
        /// フレーム全体の RectTransform。Inspector またはコードから直接設定します。
        /// </summary>
        public RectTransform FrameRect;

        /// <inheritdoc />
        RectTransform IDialogFrame.ContentContainer => ContentContainerRect;

        /// <summary>
        /// タイトルの文字列を設定します。
        /// </summary>
        /// <param name="title">表示するタイトル文字列。</param>
        public void SetTitle(string title)
        {
            if (TitleText == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame のタイトルテキストコンポーネント（TitleText）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }
            TitleText.text = title;
        }

        /// <summary>
        /// ダイアログにボタンを追加します。
        /// </summary>
        /// <param name="label">ボタンのラベルテキスト。</param>
        /// <param name="onClick">ボタンが押下された際に実行されるコールバックアクション。</param>
        public void AddButton(string label, Action onClick)
        {
            if (ButtonContainer == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame のボタン配置コンテナ（ButtonContainer）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }

            if (ButtonPrefab == null)
            {
                Debug.LogError(
                    "[Lilja.ScreenManagement.Dialog] DefaultDialogFrame のボタンプレハブ（ButtonPrefab）が設定されていません。プレハブのシリアライズ参照を確認してください。"
                );
                return;
            }

            var buttonObj = Instantiate(ButtonPrefab, ButtonContainer);
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

        /// <summary>
        /// ダイアログのコンテンツとボタンの追加状態に基づいて、ダイアログ全体のレイアウトと高さを動的に自動調整します。
        /// </summary>
        /// <remarks>
        /// タイトル領域・ボタン領域の高さはプレハブの RectTransform から実測します。
        /// コンテンツ（ScrollRect）の高さは ForceRebuildLayoutImmediate で先に確定させてから参照するため、
        /// CanvasScaler の解像度設定に依存したマジックナンバーによる計算誤差が発生しません。
        /// </remarks>
        /// <param name="content">ダイアログのコンテンツコンポーネント。</param>
        public void AdjustLayout(DefaultDialogContent content)
        {
            // 1. 対象のフレーム RectTransform を決定 (未設定なら自身を使用)
            var frameRect = FrameRect != null ? FrameRect : GetComponent<RectTransform>();
            if (frameRect == null)
            {
                return;
            }

            // 2. プレハブのオリジナル幅と最大高さを記録（変更前の値を基準とする）
            var originalWidth = frameRect.sizeDelta.x;
            var maxFrameHeight = frameRect.sizeDelta.y;

            // 3. タイトル領域の高さを RectTransform から実測する。
            //    SerializeField が未設定の場合は保守的なフォールバック値を使用する。
            const float headerHeightFallback = 70f;
            var headerHeight = headerHeightFallback;
            if (TitleText != null)
            {
                var titleRect = TitleText.GetComponent<RectTransform>();
                if (titleRect != null)
                {
                    // タイトルテキストの高さ + 下端位置（anchoredPosition.y が負値の場合は上への余白）
                    // sizeDelta.y + |anchoredPosition.y| でヘッダーエリア全体を実測
                    var absY = Mathf.Abs(titleRect.anchoredPosition.y);
                    headerHeight = titleRect.sizeDelta.y + absY;
                }
            }

            // 4. ボタンの存在確認とカウント
            var buttonCount = 0;
            if (ButtonContainer != null)
            {
                foreach (Transform child in ButtonContainer)
                {
                    if (child.gameObject.activeSelf)
                    {
                        buttonCount++;
                    }
                }
            }

            // 5. ボタン領域の高さを算出
            //    ButtonPrefab の RectTransform から実測し、コンテナのパディング分を加算する。
            const float buttonContainerPadding = 30f; // コンテナ上下パディング合計
            const float buttonPrefabHeightFallback = 50f;
            var buttonPrefabHeight = buttonPrefabHeightFallback;
            if (ButtonPrefab != null)
            {
                var buttonPrefabRect = ButtonPrefab.GetComponent<RectTransform>();
                if (buttonPrefabRect != null && buttonPrefabRect.rect.height > 0f)
                {
                    buttonPrefabHeight = buttonPrefabRect.rect.height;
                }
            }

            var buttonRowHeight =
                buttonCount > 0 ? buttonPrefabHeight + buttonContainerPadding : 0f;

            // 6. ボタンコンテナの表示状態とアンカーを先に確定する。
            //    コンテンツのレイアウト再計算（Step 7）の前に行うことで正確な底部オフセットを算出できる。
            if (ButtonContainer != null)
            {
                var buttonContainerRect = ButtonContainer.GetComponent<RectTransform>();
                if (buttonContainerRect != null)
                {
                    if (buttonCount > 0)
                    {
                        ButtonContainer.gameObject.SetActive(true);
                        // 下部アンカー固定・ボタンプレハブの高さに基づいてサイズを確定
                        buttonContainerRect.anchorMin = new Vector2(0f, 0f);
                        buttonContainerRect.anchorMax = new Vector2(1f, 0f);
                        buttonContainerRect.pivot = new Vector2(0.5f, 0f);
                        buttonContainerRect.anchoredPosition = new Vector2(0f, 15f);
                        buttonContainerRect.sizeDelta = new Vector2(-60f, buttonPrefabHeight);
                    }
                    else
                    {
                        ButtonContainer.gameObject.SetActive(false);
                    }
                }
            }

            // 7. コンテンツの ScrollRect / VerticalLayoutGroup / ContentSizeFitter を強制再計算して
            //    実際に必要なコンテンツ高さを確定する。
            //    ネストした ContentSizeFitter と親フレームの ContentSizeFitter が同時に存在すると
            //    Layout Loop が発生するため、フレーム側には ContentSizeFitter を置かずに
            //    ここで手動サイズ適用する設計とする。
            var contentNeededHeight = 0f;
            if (content != null)
            {
                var contentRect = content.GetComponent<RectTransform>();
                if (contentRect != null)
                {
                    // まずコンテンツの横幅をコンテンツコンテナ幅に合わせてから再計算する
                    LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                    contentNeededHeight = LayoutUtility.GetPreferredHeight(contentRect);
                }
            }

            // 8. コンテンツコンテナ（ContentContainerRect）のオフセットを確定する。
            //    底部オフセットにはボタン領域の実測高さを使用する。
            if (ContentContainerRect != null)
            {
                ContentContainerRect.anchorMin = new Vector2(0f, 0f);
                ContentContainerRect.anchorMax = new Vector2(1f, 1f);
                ContentContainerRect.pivot = new Vector2(0.5f, 0.5f);

                var bottomOffset = buttonCount > 0 ? buttonRowHeight : 15f;
                ContentContainerRect.offsetMin = new Vector2(20f, bottomOffset);
                ContentContainerRect.offsetMax = new Vector2(-20f, -headerHeight);
            }

            // 9. フレーム全体の高さを決定してサイズを適用する。
            //    コンテンツコンテナの上下オフセット（headerHeight + buttonRowHeight + 余白）を合算して
            //    フレームとして必要な最小高さを算出し、maxFrameHeight でクランプする。
            const float contentPaddingVertical = 30f; // コンテンツコンテナ内の上下余白
            var bottomAreaHeight = buttonCount > 0 ? buttonRowHeight : 15f;
            var totalNeededHeight =
                headerHeight + contentNeededHeight + bottomAreaHeight + contentPaddingVertical;
            var finalFrameHeight = Mathf.Min(totalNeededHeight, maxFrameHeight);

            frameRect.sizeDelta = new Vector2(originalWidth, finalFrameHeight);
        }
    }
}
