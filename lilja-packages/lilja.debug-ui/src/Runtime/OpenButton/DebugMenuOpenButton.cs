using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.DebugUI
{
    public enum DebugMenuButtonPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        CenterLeft,
        CenterRight,
    }

    /// <summary>
    /// 指定回数タップしたらデバッグメニューを開くボタン。
    /// シーンに配置するか、Instantiate() で生成して使う。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugMenuOpenButton : MonoBehaviour
    {
        /// <summary>
        /// インスタンスを生成する
        /// </summary>
        public static DebugMenuOpenButton Instantiate()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("[DebugMenuOpenButton]");
            go.AddComponent<UIDocument>();
            return go.AddComponent<DebugMenuOpenButton>();
        }

        [SerializeField] private DebugMenuButtonPosition _buttonPosition = DebugMenuButtonPosition.BottomLeft;
        [SerializeField, Min(0.01f)] private float _thresholdSeconds = 0.75f;
        [SerializeField, Min(2)] private int _requiredClicks = 3;

        private Button _button;
        private int _clickCount;
        private float _firstClickTime;

        private const string ButtonName = "debug-menu-open-button";
        private const string PressedClass = "c-open-button--pressed";
        private const string OverlayClass = "c-open-button__overlay";

        private static DebugMenuOpenButton _instance;

        /// <summary>
        /// ボタンの配置場所
        /// </summary>
        public DebugMenuButtonPosition ButtonPosition
        {
            get => _buttonPosition;
            set
            {
                if (_buttonPosition == value) return;
                _buttonPosition = value;
                ApplyButtonPositionIfReady();
            }
        }

        /// <summary>
        /// タップ間隔(秒)
        /// </summary>
        public float ThresholdSeconds
        {
            get => _thresholdSeconds;
            set
            {
                var clampedValue = Mathf.Max(0.01f, value);
                if (Mathf.Approximately(_thresholdSeconds, clampedValue)) return;
                _thresholdSeconds = clampedValue;
                ResetClickSequence();
            }
        }

        /// <summary>
        /// タップ回数
        /// </summary>
        public int RequiredClicks
        {
            get => _requiredClicks;
            set
            {
                var clampedValue = Mathf.Max(2, value);
                if (_requiredClicks == clampedValue) return;
                _requiredClicks = clampedValue;
                ResetClickSequence();
            }
        }

        private void Start()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            ConfigureDocument();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Reset()
        {
            ConfigureDocument(force: true);
            NormalizeSerializedFields();
            ResetClickSequence();
            ApplyButtonPositionIfReady();
        }

        private void OnValidate()
        {
            ConfigureDocument();
            NormalizeSerializedFields();
            ResetClickSequence();
            ApplyButtonPositionIfReady();
        }

        private void OnEnable()
        {
            ConfigureDocument();

            var uiDocument = GetComponent<UIDocument>();
            var root = uiDocument.rootVisualElement;
            _button = root.Q<Button>(ButtonName);

            if (_button == null)
            {
                Debug.LogError($"[DebugMenuOpenButton] Button '{ButtonName}' not found. UXML に DebugMenuOpenButton.uxml を設定してください。");
                return;
            }

            ApplyButtonPosition(root);

            _button.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _button.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _button.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _button.clicked += OnClicked;
        }

        private void OnDisable()
        {
            if (_button == null) return;

            _button.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _button.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _button.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            _button.clicked -= OnClicked;
        }

        private void OnPointerDown(PointerDownEvent evt) => _button.AddToClassList(PressedClass);
        private void OnPointerUp(PointerUpEvent evt) => _button.RemoveFromClassList(PressedClass);
        private void OnPointerLeave(PointerLeaveEvent evt) => _button.RemoveFromClassList(PressedClass);

        private void OnClicked()
        {
            var now = Time.unscaledTime;

            if (_clickCount == 0 || now - _firstClickTime > _thresholdSeconds)
            {
                _clickCount = 1;
                _firstClickTime = now;
                return;
            }

            _clickCount++;

            if (_clickCount >= _requiredClicks)
            {
                _clickCount = 0;
                DebugMenu.Show();
            }
        }

        private void ConfigureDocument(bool force = false)
        {
            if (!TryGetComponent<UIDocument>(out var uiDoc)) return;

            if (force || uiDoc.panelSettings == null)
                uiDoc.panelSettings = DebugMenuResources.LoadDefaultPanelSettings();

            if (force || uiDoc.visualTreeAsset == null)
                uiDoc.visualTreeAsset = DebugMenuResources.LoadOpenButtonVisualTree();
        }

        private void NormalizeSerializedFields()
        {
            _thresholdSeconds = Mathf.Max(0.01f, _thresholdSeconds);
            _requiredClicks = Mathf.Max(2, _requiredClicks);
        }

        private void ResetClickSequence()
        {
            _clickCount = 0;
            _firstClickTime = 0f;
        }

        private void ApplyButtonPositionIfReady()
        {
            if (!isActiveAndEnabled) return;
            if (!TryGetComponent<UIDocument>(out var uiDocument)) return;
            if (uiDocument.rootVisualElement == null) return;

            ApplyButtonPosition(uiDocument.rootVisualElement);
        }

        private void ApplyButtonPosition(VisualElement root)
        {
            if (root == null) return;

            var overlay = root.Q<VisualElement>(className: OverlayClass);
            if (overlay == null) return;

            var (justify, align) = _buttonPosition switch
            {
                DebugMenuButtonPosition.TopLeft => (Justify.FlexStart, Align.FlexStart),
                DebugMenuButtonPosition.TopRight => (Justify.FlexStart, Align.FlexEnd),
                DebugMenuButtonPosition.BottomLeft => (Justify.FlexEnd, Align.FlexStart),
                DebugMenuButtonPosition.BottomRight => (Justify.FlexEnd, Align.FlexEnd),
                DebugMenuButtonPosition.CenterLeft => (Justify.Center, Align.FlexStart),
                DebugMenuButtonPosition.CenterRight => (Justify.Center, Align.FlexEnd),
                _ => (Justify.FlexEnd, Align.FlexStart),
            };

            overlay.style.justifyContent = justify;
            overlay.style.alignItems = align;
        }
    }
}
