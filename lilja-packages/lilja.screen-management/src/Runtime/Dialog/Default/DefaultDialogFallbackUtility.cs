using UnityEngine;
using UnityEngine.UI;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// デフォルトダイアログ用のフォールバック UI を実行時に動的生成するユーティリティ。
    /// </summary>
    internal static class DefaultDialogFallbackUtility
    {
        private static Font _defaultFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _defaultFont = null;
        }

        private static Font GetDefaultFont()
        {
            // Unity ビルトインのデフォルトフォントを取得します。複数のフォールバック経路を試すことで NullReferenceException を防止します。
            if (_defaultFont == null)
            {
                // 1. Unity 2022.3+ 等の一部の環境のデフォルトフォントを試行
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                // 2. 伝統的なビルトイン Arial.ttf を試行
                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }

                // 3. メモリ上にロードされている任意のフォントを試行して null を絶対に回避
                if (_defaultFont == null)
                {
                    var activeFonts = Resources.FindObjectsOfTypeAll<Font>();
                    if (activeFonts != null && activeFonts.Length > 0)
                    {
                        _defaultFont = activeFonts[0];
                    }
                }

                if (_defaultFont == null)
                {
                    Debug.LogError(
                        "[Lilja.ScreenManagement.Dialog] デフォルトの UI フォントの取得に失敗しました。Text のレイアウト計算で例外が発生する可能性があります。"
                    );
                }
            }

            return _defaultFont;
        }

        /// <summary>
        /// フォールバック用のフレームオブジェクトを生成します。
        /// </summary>
        /// <returns>生成されたフレーム GameObject。</returns>
        public static GameObject CreateFrame()
        {
            var root = new GameObject("DefaultDialogFrame", typeof(RectTransform));

            // フレーム本体
            var frame = CreateUiElement("Frame", root.transform);
            var frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(600f, 500f);
            var frameImage = frame.AddComponent<Image>();
            frameImage.color = Color.white;

            // DefaultDialogFrame コンポーネントの追加
            var dialogFrame = root.AddComponent<DefaultDialogFrame>();

            // タイトル Text
            var titleObj = CreateUiElement("Title", frame.transform);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(-40f, 50f);
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
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.offsetMin = new Vector2(20f, 90f); // 下部はボタンエリア分空ける
            contentRect.offsetMax = new Vector2(-20f, -60f); // 上部はタイトル分空ける

            // ボタンコンテナ
            var buttonContainer = CreateUiElement("ButtonContainer", frame.transform);
            var containerRect = buttonContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 0f);
            containerRect.pivot = new Vector2(0.5f, 0f);
            containerRect.anchoredPosition = new Vector2(0f, 20f);
            containerRect.sizeDelta = new Vector2(-60f, 60f);
            var layout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(10, 10, 5, 5);

            // ボタンテンプレートの生成
            var buttonPrefab = CreateButtonPrefab(buttonContainer.transform);

            dialogFrame.TitleText = titleText;
            dialogFrame.ButtonContainer = buttonContainer.transform;
            dialogFrame.ButtonPrefab = buttonPrefab;
            dialogFrame.ContentContainerRect = contentRect;
            dialogFrame.FrameRect = frameRect;

            return root;
        }

        /// <summary>
        /// フォールバック用のコンテンツオブジェクトを生成します（長い文章に対応するため ScrollView 構造を動的に組み立てます）。
        /// </summary>
        /// <returns>生成されたコンテンツ GameObject。</returns>
        public static GameObject CreateContent()
        {
            var root = new GameObject("DefaultDialogContent", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            BackdropUtility.SetFullStretch(rootRect);

            // 1. ScrollRect (スクロールコンポーネント) をルートに追加
            var scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 15f; // PCでのホイールスクロール感度を大幅に改善

            // 2. Viewport の生成（はみ出たテキストのクリッピングマスク用）
            var viewport = CreateUiElement("Viewport", root.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            BackdropUtility.SetFullStretch(viewportRect);
            viewport.AddComponent<RectMask2D>(); // 描画軽量マスク

            // 3. ScrollContent の生成（縦スクロール用コンテナ）
            var scrollContent = CreateUiElement("ScrollContent", viewport.transform);
            var scrollContentRect = scrollContent.GetComponent<RectTransform>();
            scrollContentRect.anchorMin = new Vector2(0f, 1f); // 上部基準で縦に並べる
            scrollContentRect.anchorMax = new Vector2(1f, 1f);
            scrollContentRect.pivot = new Vector2(0.5f, 1f); // ピボット上中央
            scrollContentRect.sizeDelta = new Vector2(0f, 0f);

            // 自動的に中身を縦並びにするレイアウトグループ
            var verticalLayout = scrollContent.AddComponent<VerticalLayoutGroup>();
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.spacing = 15f;
            verticalLayout.padding = new RectOffset(10, 10, 10, 10);

            // 中身のテキスト量に合わせて自動的に高さを広げる
            var contentFitter = scrollContent.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ScrollRect への関連付け
            scrollRect.viewport = viewportRect;
            scrollRect.content = scrollContentRect;

            // 4. 本文テキストの生成
            var textObj = CreateUiElement("Message", scrollContent.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            var text = textObj.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.text = string.Empty;
            text.color = Color.black;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; // 自動折り返し
            text.verticalOverflow = VerticalWrapMode.Overflow; // 長文スクロールのため切り捨てずにはみ出し表示する

            // ※注意: Text自身がILayoutElementを実装しており、VerticalLayoutGroupが
            // childControlHeight = true になっているため、子要素への ContentSizeFitter の
            // アタッチはレイアウト競合（Layout Loop）とコンソール警告の原因となるため行いません。

            // DefaultDialogContent コンポーネントの追加と参照の設定
            var contentComp = root.AddComponent<DefaultDialogContent>();
            contentComp.BodyText = text;

            return root;
        }

        private static GameObject CreateButtonPrefab(Transform parent)
        {
            // ボタンのテンプレートオブジェクトを生成します。
            var button = CreateUiElement("ButtonPrefab", parent);
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(120f, 50f);

            var image = button.AddComponent<Image>();
            image.color = new Color(0.3f, 0.5f, 0.9f);

            var buttonComponent = button.AddComponent<Button>();
            var colors = buttonComponent.colors;
            colors.highlightedColor = new Color(0.4f, 0.6f, 1.0f);
            colors.pressedColor = new Color(0.2f, 0.4f, 0.8f);
            buttonComponent.colors = colors;

            // ボタンテキスト
            var textObj = CreateUiElement("Text", button.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            BackdropUtility.SetFullStretch(textRect);
            var text = textObj.AddComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            // テンプレート用のため初期状態は非アクティブ化
            button.SetActive(false);

            return button;
        }

        private static GameObject CreateUiElement(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }
    }
}
