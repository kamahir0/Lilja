using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// SimpleDialog 用のフォールバック UI を生成する Service
    /// </summary>
    internal static class SimpleDialogFallbackUtility
    {
        /// <summary> デフォルトフォントのキャッシュ </summary>
        private static Font _defaultFont;

        /// <summary> デフォルトフォントを取得します </summary>
        private static Font GetDefaultFont()
        {
            if (_defaultFont == null)
            {
                // Unityビルトインのデフォルトフォントを取得
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _defaultFont;
        }

        /// <summary>
        /// フォールバック用の Frame を生成します
        /// </summary>
        public static GameObject CreateFrame()
        {
            var root = new GameObject("DefaultSimpleFrame", typeof(RectTransform));

            // フレーム本体
            var frame = CreateUiElement("Frame", root.transform);
            var frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(600, 350);
            var frameImage = frame.AddComponent<Image>();
            frameImage.color = Color.white;

            // SimpleDialogFrameコンポーネント追加
            var dialogFrame = root.AddComponent<SimpleDialogFrame>();

            // タイトル Text
            var titleObj = CreateUiElement("Title", frame.transform);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -10);
            titleRect.sizeDelta = new Vector2(-40, 50);
            var titleText = titleObj.AddComponent<Text>();
            titleText.font = GetDefaultFont();
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.color = Color.black;

            // コンテンツコンテナ
            var contentContainer = CreateUiElement("ContentContainer", frame.transform);
            var contentRect = contentContainer.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0); // 全体に広げる
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.offsetMin = new Vector2(20, 90); // マージン（下部はボタンエリア分空ける）
            contentRect.offsetMax = new Vector2(-20, -60); // 上部はタイトル分空ける

            // ボタンコンテナ
            var buttonContainer = CreateUiElement("ButtonContainer", frame.transform);
            var containerRect = buttonContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(1, 0);
            containerRect.pivot = new Vector2(0.5f, 0);
            containerRect.anchoredPosition = new Vector2(0, 20);
            containerRect.sizeDelta = new Vector2(-60, 60);
            var layout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(10, 10, 5, 5);

            // ボタン Prefab
            var buttonPrefab = CreateButtonPrefab(buttonContainer.transform);

            // SimpleDialogFrame に参照を設定 (Reflection)
            SetPrivateField(dialogFrame, "_titleText", titleText);
            SetPrivateField(dialogFrame, "_buttonContainer", buttonContainer.transform);
            SetPrivateField(dialogFrame, "_buttonPrefab", buttonPrefab);
            SetPrivateField(dialogFrame, "_contentContainer", contentRect);

            return root;
        }

        /// <summary>
        /// フォールバック用の Content を生成します
        /// </summary>
        public static GameObject CreateContent()
        {
            var root = new GameObject("DefaultSimpleContent", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            SetFullStretch(rect);

            // 本文テキスト
            var textObj = CreateUiElement("Message", root.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            SetFullStretch(textRect);

            var text = textObj.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = "";
            text.color = Color.black;
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;

            // 画像コンテナ
            var imageObj = CreateUiElement("Image", root.transform);
            var imageRect = imageObj.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(100, 100);
            var image = imageObj.AddComponent<Image>();
            imageObj.SetActive(false);

            // SimpleDialogContent コンポーネントを追加し、参照を紐付け
            var contentComp = root.AddComponent<SimpleDialogContent>();
            SetPrivateField(contentComp, "_bodyText", text);
            SetPrivateField(contentComp, "_imageContainer", image);

            return root;
        }

        private static GameObject CreateButtonPrefab(Transform parent)
        {
            var button = CreateUiElement("ButtonPrefab", parent);
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(120, 50);

            var image = button.AddComponent<Image>();
            image.color = new Color(0.3f, 0.5f, 0.9f);

            var buttonComponent = button.AddComponent<Button>();
            var colors = buttonComponent.colors;
            colors.highlightedColor = new Color(0.4f, 0.6f, 1f);
            colors.pressedColor = new Color(0.2f, 0.4f, 0.8f);
            buttonComponent.colors = colors;

            // 通常の Text を使用
            var textObj = CreateUiElement("Text", button.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            SetFullStretch(textRect);
            var text = textObj.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            // 非アクティブにしてテンプレートとして保持
            button.SetActive(false);

            return button;
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static GameObject CreateUiElement(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
