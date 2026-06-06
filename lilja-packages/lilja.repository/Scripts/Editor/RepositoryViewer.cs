#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Lilja.Repository.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lilja.Repository.Editor
{
    /// <summary>
    /// Unity エディター内で稼働中のリポジトリと永続化されたリポジトリファイルを表示します。
    /// </summary>
    public sealed class RepositoryViewer : EditorWindow
    {
        private const double AutoReloadIntervalSeconds = 1.5d;
        private const float DefaultRepositoryPaneWidth = 260f;
        private const float DefaultTopPaneHeight = 320f;
        private const float ListItemHeight = 24f;

        private static Color SecondaryTextColor => EditorGUIUtility.isProSkin
            ? new Color(0.72f, 0.72f, 0.72f, 1f)
            : new Color(0.32f, 0.32f, 0.32f, 1f);

        private static Color AccentColor => EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.49f, 0.90f, 1f)
            : new Color(0.15f, 0.41f, 0.83f, 1f);

        private static Color SelectedRowBackgroundColor => EditorGUIUtility.isProSkin
            ? new Color(0.24f, 0.49f, 0.90f, 0.20f)
            : new Color(0.15f, 0.41f, 0.83f, 0.12f);

        [SerializeField] private RepositoryTracker.RepositoryType _selectedType;
        [SerializeField] private bool _autoReloadEnabled = true;
        [SerializeField] private string _selectedRepositoryStableId = string.Empty;
        [SerializeField] private string _selectedRecordStableId = string.Empty;

        private readonly RepositoryViewerDataSource _dataSource = new RepositoryViewerDataSource();
        private readonly List<RepositorySnapshot> _repositories = new List<RepositorySnapshot>();
        private readonly List<RecordSnapshot> _records = new List<RecordSnapshot>();
        private IReadOnlyList<RepositoryTracker.RepositoryType> _availableTypes = Array.Empty<RepositoryTracker.RepositoryType>();
        private DropdownField? _backendDropdown;
        private Toggle? _autoReloadToggle;
        private Button? _openDirectoryButton;
        private ListView? _repositoryListView;
        private ListView? _recordListView;
        private HelpBox? _repositoryEmptyStateLabel;
        private HelpBox? _recordEmptyStateLabel;
        private Label? _previewStatusLabel;
        private TextField? _previewField;
        private int _selectedRepositoryIndex = -1;
        private int _selectedRecordIndex = -1;
        private double _nextAutoReloadTime;
        private bool _isReloading;
        private string _lastReloadFingerprint = string.Empty;

        /// <summary>
        /// Repository Viewer ウィンドウを開きます。
        /// </summary>
        [MenuItem("Lilja/Repository/Repository Viewer")]
        public static void Open()
        {
            var window = GetWindow<RepositoryViewer>("Repository Viewer");
            window.titleContent = new GUIContent("Repository Viewer");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            ScheduleNextAutoReload();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnFocus()
        {
            if (rootVisualElement.childCount > 0)
            {
                ReloadData();
            }
        }

        private void CreateGUI()
        {
            titleContent = new GUIContent("Repository Viewer");
            minSize = new Vector2(720f, 420f);

            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1f;

            root.Add(BuildHeader());
            root.Add(BuildContent());

            RefreshAvailableTypes();
            ReloadData();
        }

        private VisualElement BuildHeader()
        {
            var header = new Toolbar();
            header.style.flexShrink = 0f;
            header.style.paddingLeft = 4f;
            header.style.paddingRight = 4f;

            _backendDropdown = new DropdownField();
            _backendDropdown.label = string.Empty;
            _backendDropdown.style.minWidth = 160f;
            _backendDropdown.style.marginLeft = 0f;
            _backendDropdown.style.marginRight = 0f;
            _backendDropdown.RegisterValueChangedCallback(OnBackendChanged);
            header.Add(_backendDropdown);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            header.Add(spacer);

            _autoReloadToggle = new ToolbarToggle { text = "AutoReload" };
            _autoReloadToggle.value = _autoReloadEnabled;
            _autoReloadToggle.style.marginLeft = 0f;
            _autoReloadToggle.style.marginRight = 0f;
            _autoReloadToggle.RegisterValueChangedCallback(evt =>
            {
                _autoReloadEnabled = evt.newValue;
                if (_autoReloadEnabled)
                {
                    ReloadData();
                }
            });
            header.Add(_autoReloadToggle);

            var reloadButton = new ToolbarButton(ReloadData) { text = "Reload" };
            reloadButton.style.marginLeft = 0f;
            reloadButton.style.marginRight = 0f;
            header.Add(reloadButton);

            _openDirectoryButton = new ToolbarButton(OpenDirectory) { text = "OpenDirectory" };
            _openDirectoryButton.style.marginLeft = 0f;
            _openDirectoryButton.style.marginRight = 0f;
            header.Add(_openDirectoryButton);

            return header;
        }

        private VisualElement BuildContent()
        {
            var body = new TwoPaneSplitView(0, DefaultTopPaneHeight, TwoPaneSplitViewOrientation.Vertical)
            {
                viewDataKey = "Lilja.Repository.RepositoryViewer.BodySplit",
            };
            body.style.flexGrow = 1f;

            var topArea = new TwoPaneSplitView(0, DefaultRepositoryPaneWidth, TwoPaneSplitViewOrientation.Horizontal)
            {
                viewDataKey = "Lilja.Repository.RepositoryViewer.TopSplit",
            };
            topArea.style.flexGrow = 1f;

            topArea.Add(BuildRepositoryPane());
            topArea.Add(BuildRecordPane());
            body.Add(topArea);
            body.Add(BuildPreviewPane());

            return body;
        }

        private VisualElement BuildRepositoryPane()
        {
            return BuildListPane(
                "Repositories",
                SelectRepositoryAt,
                out _repositoryListView,
                out _repositoryEmptyStateLabel,
                BindRepositoryItem);
        }

        private VisualElement BuildRecordPane()
        {
            return BuildListPane(
                "Records",
                SelectRecordAt,
                out _recordListView,
                out _recordEmptyStateLabel,
                BindRecordItem);
        }

        private VisualElement BuildListPane(
            string title,
            Action<int> selectAction,
            out ListView listView,
            out HelpBox emptyStateLabel,
            Action<VisualElement, int> bindItem)
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1f;
            pane.style.minWidth = 200f;
            pane.style.paddingBottom = 4f;

            var header = BuildPaneHeader(title);
            pane.Add(header);

            listView = new ListView
            {
                selectionType = SelectionType.None,
                fixedItemHeight = ListItemHeight,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
            };
            listView.style.flexGrow = 1f;
            listView.style.marginLeft = 4f;
            listView.style.marginRight = 4f;
            listView.style.marginTop = 4f;
            listView.makeItem = () => new SnapshotButtonListItem(selectAction);
            listView.bindItem = bindItem;
            pane.Add(listView);

            emptyStateLabel = BuildEmptyStateLabel();
            pane.Add(emptyStateLabel);

            return pane;
        }

        private VisualElement BuildPreviewPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1f;
            pane.style.minHeight = 140f;
            pane.style.paddingBottom = 4f;

            pane.Add(BuildPaneHeader("Preview"));

            _previewStatusLabel = new Label("Select a record.");
            _previewStatusLabel.style.paddingLeft = 8f;
            _previewStatusLabel.style.paddingRight = 8f;
            _previewStatusLabel.style.paddingTop = 8f;
            _previewStatusLabel.style.paddingBottom = 6f;
            _previewStatusLabel.style.color = SecondaryTextColor;
            pane.Add(_previewStatusLabel);

            var previewFrame = new Box();
            previewFrame.style.flexGrow = 1f;
            previewFrame.style.marginLeft = 4f;
            previewFrame.style.marginRight = 4f;
            previewFrame.style.marginBottom = 0f;
            previewFrame.style.paddingLeft = 0f;
            previewFrame.style.paddingRight = 0f;
            previewFrame.style.paddingTop = 0f;
            previewFrame.style.paddingBottom = 0f;

            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.style.flexGrow = 1f;

            _previewField = new TextField
            {
                multiline = true,
                isReadOnly = true,
            };
            _previewField.style.flexGrow = 1f;
            _previewField.style.marginLeft = 0f;
            _previewField.style.marginRight = 0f;
            _previewField.style.marginTop = 0f;
            _previewField.style.marginBottom = 0f;
            _previewField.style.paddingLeft = 4f;
            _previewField.style.paddingRight = 4f;
            _previewField.style.paddingTop = 4f;
            _previewField.style.paddingBottom = 4f;
            _previewField.style.borderLeftWidth = 0f;
            _previewField.style.borderRightWidth = 0f;
            _previewField.style.borderTopWidth = 0f;
            _previewField.style.borderBottomWidth = 0f;
            _previewField.style.unityTextAlign = TextAnchor.UpperLeft;
            _previewField.style.height = StyleKeyword.Auto;
            _previewField.style.minHeight = Length.Percent(100);
            
            scrollView.Add(_previewField);
            previewFrame.Add(scrollView);
            pane.Add(previewFrame);

            return pane;
        }

        private static VisualElement BuildPaneHeader(string title)
        {
            var header = new Toolbar();
            header.style.flexShrink = 0f;
            header.style.alignItems = Align.Center;
            header.style.minHeight = 22f;
            header.style.paddingLeft = 2f;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.marginLeft = 4f;
            titleLabel.style.marginTop = 0f;
            titleLabel.style.marginBottom = 0f;
            titleLabel.style.paddingTop = 0f;
            titleLabel.style.paddingBottom = 0f;
            header.Add(titleLabel);

            return header;
        }

        private static HelpBox BuildEmptyStateLabel()
        {
            var helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            helpBox.style.display = DisplayStyle.None;
            helpBox.style.marginLeft = 4f;
            helpBox.style.marginRight = 4f;
            helpBox.style.marginTop = 4f;
            return helpBox;
        }

        private void OnBackendChanged(ChangeEvent<string> evt)
        {
            var nextType = ParseRepositoryType(evt.newValue);
            if (nextType == _selectedType)
            {
                return;
            }

            _selectedType = nextType;
            _selectedRepositoryStableId = string.Empty;
            _selectedRecordStableId = string.Empty;
            ReloadData();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    ReloadData();
                    break;
            }
        }

        private void OnEditorUpdate()
        {
            if (!_autoReloadEnabled || _isReloading || rootVisualElement.childCount == 0)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextAutoReloadTime)
            {
                return;
            }

            ReloadData(false);
        }

        private void ReloadData()
        {
            ReloadData(true);
        }

        private void ReloadData(bool force)
        {
            if (_isReloading)
            {
                return;
            }

            _isReloading = true;
            try
            {
                RefreshAvailableTypes();
                var reloadFingerprint = _dataSource.GetReloadFingerprint(_selectedType);
                if (!force && reloadFingerprint == _lastReloadFingerprint)
                {
                    return;
                }

                var previousRepositoryStableId = _selectedRepositoryStableId;
                var previousRecordStableId = _selectedRecordStableId;

                _repositories.Clear();
                _repositories.AddRange(_dataSource.LoadRepositories(_selectedType));
                _lastReloadFingerprint = reloadFingerprint;

                _selectedRepositoryIndex = FindRepositoryIndex(previousRepositoryStableId);
                if (_selectedRepositoryIndex < 0 && _repositories.Count > 0)
                {
                    _selectedRepositoryIndex = 0;
                }

                _selectedRepositoryStableId = _selectedRepositoryIndex >= 0
                    ? _repositories[_selectedRepositoryIndex].StableId
                    : string.Empty;

                RefreshRepositoryList();
                RefreshRecordList(_selectedRepositoryStableId == previousRepositoryStableId ? previousRecordStableId : string.Empty);
                RefreshPreview();
                RefreshEmptyStates();
                RefreshHeaderState();
            }
            finally
            {
                _isReloading = false;
                ScheduleNextAutoReload();
            }
        }

        private void RefreshAvailableTypes()
        {
            _availableTypes = _dataSource.GetAvailableRepositoryTypes();
            if (!_availableTypes.Contains(_selectedType))
            {
                _selectedType = _availableTypes.Contains(RepositoryTracker.RepositoryType.Json)
                    ? RepositoryTracker.RepositoryType.Json
                    : _availableTypes.First();
            }

            if (_backendDropdown is not null)
            {
                _backendDropdown.choices = _availableTypes.Select(ToDisplayName).ToList();
                _backendDropdown.SetValueWithoutNotify(ToDisplayName(_selectedType));
            }
        }

        private void RefreshRepositoryList()
        {
            if (_repositoryListView is null)
            {
                return;
            }

            _repositoryListView.itemsSource = _repositories;
            _repositoryListView.Rebuild();
        }

        private void RefreshRecordList(string preferredRecordStableId)
        {
            _records.Clear();
            if (_selectedRepositoryIndex >= 0 && _selectedRepositoryIndex < _repositories.Count)
            {
                _records.AddRange(_repositories[_selectedRepositoryIndex].Records);
            }

            _selectedRecordIndex = FindRecordIndex(preferredRecordStableId);
            if (_selectedRecordIndex < 0 && _records.Count > 0)
            {
                _selectedRecordIndex = 0;
            }

            _selectedRecordStableId = _selectedRecordIndex >= 0
                ? _records[_selectedRecordIndex].StableId
                : string.Empty;

            if (_recordListView is not null)
            {
                _recordListView.itemsSource = _records;
                _recordListView.Rebuild();
            }
        }

        private async void RefreshPreview()
        {
            if (_previewStatusLabel is null || _previewField is null)
            {
                return;
            }

            if (_selectedRecordIndex < 0 || _selectedRecordIndex >= _records.Count)
            {
                _previewStatusLabel.text = _selectedRepositoryIndex >= 0 ? "Select a record." : "Select a repository.";
                _previewField.SetValueWithoutNotify(string.Empty);
                return;
            }

            var record = _records[_selectedRecordIndex];
            var currentRecordStableId = _selectedRecordStableId;

            _previewStatusLabel.text = $"{record.Title} (Loading...)";
            _previewField.SetValueWithoutNotify("Loading...");

            try
            {
                var detail = await System.Threading.Tasks.Task.Run(() => _dataSource.LoadRecordDetail(record));
                
                if (_selectedRecordStableId == currentRecordStableId)
                {
                    _previewStatusLabel.text = record.Title;
                    _previewField.SetValueWithoutNotify(detail);
                }
            }
            catch (Exception ex)
            {
                if (_selectedRecordStableId == currentRecordStableId)
                {
                    _previewStatusLabel.text = $"{record.Title} (Error)";
                    _previewField.SetValueWithoutNotify(ex.ToString());
                }
            }
        }

        private void RefreshEmptyStates()
        {
            if (_repositoryEmptyStateLabel is not null && _repositoryListView is not null)
            {
                var hasRepositories = _repositories.Count > 0;
                _repositoryEmptyStateLabel.text = _dataSource.GetRepositoryEmptyMessage(_selectedType);
                _repositoryEmptyStateLabel.style.display = hasRepositories ? DisplayStyle.None : DisplayStyle.Flex;
                _repositoryListView.style.display = hasRepositories ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_recordEmptyStateLabel is not null && _recordListView is not null)
            {
                var message = _selectedRepositoryIndex < 0
                    ? "Select a repository."
                    : _repositories[_selectedRepositoryIndex].EmptyMessage;
                var hasRecords = _records.Count > 0;
                _recordEmptyStateLabel.text = message;
                _recordEmptyStateLabel.style.display = hasRecords ? DisplayStyle.None : DisplayStyle.Flex;
                _recordListView.style.display = hasRecords ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshHeaderState()
        {
            if (_autoReloadToggle is not null)
            {
                _autoReloadToggle.SetValueWithoutNotify(_autoReloadEnabled);
            }

            if (_openDirectoryButton is not null)
            {
                _openDirectoryButton.SetEnabled(_selectedType != RepositoryTracker.RepositoryType.InMemory);
            }
        }

        private void SelectRepositoryAt(int index)
        {
            if (index < 0 || index >= _repositories.Count)
            {
                return;
            }

            _selectedRepositoryIndex = index;
            _selectedRepositoryStableId = _repositories[index].StableId;
            _selectedRecordStableId = string.Empty;
            _selectedRecordIndex = -1;

            RefreshRepositoryList();
            RefreshRecordList(string.Empty);
            RefreshPreview();
            RefreshEmptyStates();
        }

        private void SelectRecordAt(int index)
        {
            if (index < 0 || index >= _records.Count)
            {
                return;
            }

            _selectedRecordIndex = index;
            _selectedRecordStableId = _records[index].StableId;

            if (_recordListView is not null)
            {
                _recordListView.Rebuild();
            }

            RefreshPreview();
        }

        private void BindRepositoryItem(VisualElement element, int index)
        {
            var item = (SnapshotButtonListItem)element;
            var snapshot = _repositories[index];
            item.Bind(index, snapshot.Title, $"({snapshot.Records.Count})", snapshot.Tooltip, index == _selectedRepositoryIndex);
        }

        private void BindRecordItem(VisualElement element, int index)
        {
            var item = (SnapshotButtonListItem)element;
            var snapshot = _records[index];
            item.Bind(index, snapshot.Title, snapshot.Preview, snapshot.Tooltip, index == _selectedRecordIndex);
        }

        private int FindRepositoryIndex(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return -1;
            }

            for (var index = 0; index < _repositories.Count; index++)
            {
                if (_repositories[index].StableId == stableId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindRecordIndex(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return -1;
            }

            for (var index = 0; index < _records.Count; index++)
            {
                if (_records[index].StableId == stableId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void OpenDirectory()
        {
            if (_selectedType == RepositoryTracker.RepositoryType.InMemory)
            {
                return;
            }

            System.IO.Directory.CreateDirectory(Application.persistentDataPath);
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        private void ScheduleNextAutoReload()
        {
            _nextAutoReloadTime = EditorApplication.timeSinceStartup + AutoReloadIntervalSeconds;
        }

        private static string ToDisplayName(RepositoryTracker.RepositoryType repositoryType)
        {
            return repositoryType switch
            {
                RepositoryTracker.RepositoryType.InMemory => "InMemory",
                RepositoryTracker.RepositoryType.Json => "Json",
                RepositoryTracker.RepositoryType.MessagePack => "MessagePack",
                _ => repositoryType.ToString(),
            };
        }

        private static RepositoryTracker.RepositoryType ParseRepositoryType(string value)
        {
            return value switch
            {
                "Json" => RepositoryTracker.RepositoryType.Json,
                "MessagePack" => RepositoryTracker.RepositoryType.MessagePack,
                _ => RepositoryTracker.RepositoryType.InMemory,
            };
        }

        private sealed class SnapshotButtonListItem : VisualElement
        {
            private readonly Action<int> _onClick;
            private readonly Button _button;
            private readonly VisualElement _accent;
            private readonly VisualElement _textRow;
            private readonly Label _titleLabel;
            private readonly Label _previewLabel;
            private int _index;

            public SnapshotButtonListItem(Action<int> onClick)
            {
                _onClick = onClick;
                style.paddingLeft = 4f;
                style.paddingRight = 4f;
                style.paddingTop = 1f;
                style.paddingBottom = 1f;

                _button = new Button(HandleClick);
                _button.Clear();
                _button.style.flexGrow = 1f;
                _button.style.minHeight = ListItemHeight - 2f;
                _button.style.paddingLeft = 0f;
                _button.style.paddingRight = 0f;
                _button.style.paddingTop = 0f;
                _button.style.paddingBottom = 0f;
                _button.style.borderLeftWidth = 0f;
                _button.style.borderRightWidth = 0f;
                _button.style.borderTopWidth = 0f;
                _button.style.borderBottomWidth = 0f;
                _button.style.borderTopLeftRadius = 0f;
                _button.style.borderTopRightRadius = 0f;
                _button.style.borderBottomLeftRadius = 0f;
                _button.style.borderBottomRightRadius = 0f;
                _button.style.unityTextAlign = TextAnchor.MiddleLeft;
                Add(_button);

                var content = new VisualElement();
                content.style.flexDirection = FlexDirection.Row;
                content.style.flexGrow = 1f;
                content.pickingMode = PickingMode.Ignore;
                _button.Add(content);

                _accent = new VisualElement();
                _accent.style.width = 3f;
                _accent.style.marginLeft = 6f;
                _accent.style.marginRight = 8f;
                _accent.style.marginTop = 6f;
                _accent.style.marginBottom = 6f;
                _accent.style.backgroundColor = AccentColor;
                _accent.style.visibility = Visibility.Hidden;
                content.Add(_accent);

                var textColumn = new VisualElement();
                textColumn.style.flexGrow = 1f;
                textColumn.style.justifyContent = Justify.Center;
                textColumn.style.paddingTop = 0f;
                textColumn.style.paddingBottom = 0f;
                textColumn.style.paddingRight = 10f;
                textColumn.pickingMode = PickingMode.Ignore;
                content.Add(textColumn);

                _textRow = new VisualElement();
                _textRow.style.flexDirection = FlexDirection.Row;
                _textRow.style.alignItems = Align.Center;
                _textRow.style.flexGrow = 1f;
                _textRow.pickingMode = PickingMode.Ignore;
                textColumn.Add(_textRow);

                _titleLabel = new Label();
                _titleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                _titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                _titleLabel.style.overflow = Overflow.Hidden;
                _titleLabel.style.flexShrink = 0f;
                _textRow.Add(_titleLabel);

                _previewLabel = new Label();
                _previewLabel.style.fontSize = 10f;
                _previewLabel.style.color = SecondaryTextColor;
                _previewLabel.style.marginLeft = 6f;
                _previewLabel.style.whiteSpace = WhiteSpace.NoWrap;
                _previewLabel.style.overflow = Overflow.Hidden;
                _previewLabel.style.flexGrow = 1f;
                _previewLabel.style.flexShrink = 1f;
                _textRow.Add(_previewLabel);
            }

            public void Bind(int index, string title, string preview, string tooltip, bool selected)
            {
                _index = index;
                _button.tooltip = tooltip;
                _button.style.backgroundColor = selected ? SelectedRowBackgroundColor : Color.clear;
                _titleLabel.text = title;
                _titleLabel.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
                _previewLabel.text = preview;
                _previewLabel.style.display = string.IsNullOrWhiteSpace(preview) ? DisplayStyle.None : DisplayStyle.Flex;
                _accent.style.visibility = selected ? Visibility.Visible : Visibility.Hidden;
            }

            private void HandleClick()
            {
                _onClick(_index);
            }
        }
    }
}
#endif
