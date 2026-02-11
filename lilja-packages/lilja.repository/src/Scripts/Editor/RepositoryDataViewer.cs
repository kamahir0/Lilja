using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Lilja.Repository.Diagnostics;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;

namespace Lilja.Repository.Editor
{
    public class RepositoryDataViewer : EditorWindow
    {
        [MenuItem("Lilja/Repository Viewer")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<RepositoryDataViewer>();
            wnd.titleContent = new GUIContent("Repository Viewer");
        }

        private RepositoryTracker.RepositoryType _currentType = RepositoryTracker.RepositoryType.InMemory;
        private MultiColumnTreeView _treeView;
        private List<TreeViewItemData<object>> _treeData;
        private bool _autoReload;

        // Visual Elements
        private ToolbarMenu _typeMenu;
        private Label _statusLabel;

        public void CreateGUI()
        {
            var root = rootVisualElement;

            // Load USS
            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/com.kamahir0.lilja.repository/Scripts/Editor/RepositoryDataViewer.uss");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // Toolbar
            var toolbar = new Toolbar();
            toolbar.AddToClassList("toolbar");

            _typeMenu = new ToolbarMenu();
            _typeMenu.text = _currentType.ToString();
            foreach (RepositoryTracker.RepositoryType type in Enum.GetValues(typeof(RepositoryTracker.RepositoryType)))
            {
                _typeMenu.menu.AppendAction(type.ToString(), a =>
                {
                    _currentType =
                        (RepositoryTracker.RepositoryType)Enum.Parse(typeof(RepositoryTracker.RepositoryType), a.name);
                    _typeMenu.text = _currentType.ToString();
                    Reload();
                });
            }

            toolbar.Add(_typeMenu);

            var reloadBtn = new ToolbarButton(Reload) { text = "Reload" };
            toolbar.Add(reloadBtn);

            var autoReloadToggle = new ToolbarToggle { text = "Auto Reload" };
            autoReloadToggle.RegisterValueChangedCallback(evt => _autoReload = evt.newValue);
            toolbar.Add(autoReloadToggle);

            root.Add(toolbar);

            // Status Label
            _statusLabel = new Label();
            _statusLabel.AddToClassList("status-label");
            root.Add(_statusLabel);

            // TreeView
            _treeView = new MultiColumnTreeView();
            _treeView.AddToClassList("tree-view");

            var nameColumn = new Column { title = "Name/Key", width = 200 };
            nameColumn.makeCell = () => new Label();
            nameColumn.bindCell = (e, i) => (e as Label).text = GetName(GetItem(i));

            var valueColumn = new Column { title = "Value", width = 400 };
            valueColumn.makeCell = () => new Label();
            valueColumn.bindCell = (e, i) => (e as Label).text = GetValue(GetItem(i));

            _treeView.columns.Add(nameColumn);
            _treeView.columns.Add(valueColumn);

            _treeView.SetRootItems(new List<TreeViewItemData<object>>());
            root.Add(_treeView);

            Reload();
        }

        private void Update()
        {
            if (_autoReload && Application.isPlaying)
            {
                // Simple throttling or check could be added here
                if (Time.frameCount % 60 == 0)
                {
                    Reload();
                }
            }
        }

        private void Reload()
        {
            _treeData = new List<TreeViewItemData<object>>();
            _statusLabel.text = "";

            if (Application.isPlaying)
            {
                LoadFromTracker();
            }
            else
            {
                if (_currentType == RepositoryTracker.RepositoryType.InMemory)
                {
                    _statusLabel.text = "InMemory repositories are only available in Play Mode.";
                }
                else
                {
                    LoadFromFiles();
                }
            }

            _treeView.SetRootItems(_treeData);
            _treeView.Rebuild();
        }

        private void LoadFromTracker()
        {
            var repositories = RepositoryTracker.GetAll(_currentType).ToList();
            var id = 0;
            // ... (rest of LoadFromTracker)

            foreach (var repo in repositories)
            {
                var repoType = repo.GetType();
                var children = new List<TreeViewItemData<object>>();

                // Reflection to get internal storage
                // InMemory: _storage (Dictionary) or _entity (Single)
                // Json/MessagePack: _cache (Dictionary or Single Dto)

                try
                {
                    if (_currentType == RepositoryTracker.RepositoryType.InMemory)
                    {
                        var storageField =
                            repoType.GetField("_storage", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (storageField != null)
                        {
                            var dict = storageField.GetValue(repo) as System.Collections.IDictionary;
                            if (dict != null)
                            {
                                foreach (System.Collections.DictionaryEntry entry in dict)
                                {
                                    children.Add(new TreeViewItemData<object>(++id,
                                        new DataItem { Key = entry.Key.ToString(), Value = entry.Value }));
                                }
                            }
                        }
                        else
                        {
                            var entityField =
                                repoType.GetField("_entity", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (entityField != null)
                            {
                                var val = entityField.GetValue(repo);
                                children.Add(new TreeViewItemData<object>(++id,
                                    new DataItem { Key = "Singleton", Value = val }));
                            }
                        }
                    }
                    else // Json or MessagePack
                    {
                        var cacheField = repoType.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (cacheField != null)
                        {
                            var cache = cacheField.GetValue(repo);
                            if (cache is System.Collections.IDictionary dict)
                            {
                                foreach (System.Collections.DictionaryEntry entry in dict)
                                {
                                    children.Add(new TreeViewItemData<object>(++id,
                                        new DataItem { Key = entry.Key.ToString(), Value = entry.Value }));
                                }
                            }
                            else if (cache != null)
                            {
                                children.Add(new TreeViewItemData<object>(++id,
                                    new DataItem { Key = "Singleton", Value = cache }));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to read repository {repoType.Name}: {e.Message}");
                }

                _treeData.Add(new TreeViewItemData<object>(++id,
                    new DataItem { Key = repoType.Name, Value = $"Items: {children.Count}" }, children));
            }
        }

        private void LoadFromFiles()
        {
            var path = Application.persistentDataPath;
            var pattern = _currentType == RepositoryTracker.RepositoryType.Json ? "*.json" : "*.msgpack";
            var files = Directory.GetFiles(path, pattern);
            var id = 0;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var entityName = Path.GetFileNameWithoutExtension(file);
                var dtoType = GetTypeByName(entityName + "Dto");

                if (dtoType == null)
                {
                    // Fallback to raw text if DTO not found
                    var rawContent = "Binary Data";
                    if (_currentType == RepositoryTracker.RepositoryType.Json)
                    {
                        rawContent = File.ReadAllText(file);
                        // Single line for display
                        rawContent = rawContent.Replace("\r", "").Replace("\n", " ");
                        if (rawContent.Length > 5000) rawContent = rawContent.Substring(0, 5000) + "...";
                    }

                    _treeData.Add(new TreeViewItemData<object>(++id,
                        new DataItem { Key = fileName, Value = rawContent }));
                    continue;
                }

                var children = new List<TreeViewItemData<object>>();

                try
                {
                    if (_currentType == RepositoryTracker.RepositoryType.Json)
                    {
                        var json = File.ReadAllText(file);
                        // Heuristic: Check if keyed (wrapped) or singleton
                        if (json.Contains("\"Items\":"))
                        {
                            var wrapperType = typeof(JsonStorageWrapper<>).MakeGenericType(dtoType);
                            var wrapper = JsonUtility.FromJson(json, wrapperType);
                            var itemsField = wrapperType.GetField("Items");
                            var items = itemsField.GetValue(wrapper) as System.Collections.IList;
                            if (items != null)
                            {
                                int idx = 0;
                                foreach (var item in items)
                                {
                                    children.Add(new TreeViewItemData<object>(++id,
                                        new DataItem { Key = $"Item {idx++}", Value = item }));
                                }
                            }
                        }
                        else
                        {
                            var singleton = JsonUtility.FromJson(json, dtoType);
                            children.Add(new TreeViewItemData<object>(++id,
                                new DataItem { Key = "Singleton", Value = singleton }));
                        }
                    }
                    else // MessagePack
                    {
                        var bytes = File.ReadAllBytes(file);
                        var options = MessagePackSerializerOptions.Standard;

                        // Try to find generated formatter
                        var formatterType = GetTypeByName(entityName + "DtoFormatter");
                        if (formatterType != null)
                        {
                            try
                            {
                                var formatter = Activator.CreateInstance(formatterType) as IMessagePackFormatter;
                                if (formatter != null)
                                {
                                    var resolver = CompositeResolver.Create(new[] { formatter },
                                        new[] { StandardResolver.Instance });
                                    options = options.WithResolver(resolver);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"Failed to create formatter {formatterType.Name}: {ex.Message}");
                            }
                        }

                        try
                        {
                            // Try List (Keyed)
                            var listType = typeof(List<>).MakeGenericType(dtoType);
                            // Invoke helper: DeserializeMsgPack<List<Dto>>(bytes, options)
                            var method = typeof(RepositoryDataViewer).GetMethod("DeserializeMsgPack",
                                    BindingFlags.NonPublic | BindingFlags.Static)
                                .MakeGenericMethod(listType);

                            var list = method.Invoke(null, new object[] { bytes, options }) as System.Collections.IList;

                            if (list != null)
                            {
                                int idx = 0;
                                foreach (var item in list)
                                {
                                    children.Add(new TreeViewItemData<object>(++id,
                                        new DataItem { Key = $"Item {idx++}", Value = item }));
                                }
                            }
                        }
                        catch
                        {
                            // Try Singleton
                            try
                            {
                                var method = typeof(RepositoryDataViewer).GetMethod("DeserializeMsgPack",
                                        BindingFlags.NonPublic | BindingFlags.Static)
                                    .MakeGenericMethod(dtoType);

                                var singleton = method.Invoke(null, new object[] { bytes, options });
                                children.Add(new TreeViewItemData<object>(++id,
                                    new DataItem { Key = "Singleton", Value = singleton }));
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning(
                                    $"Failed to deserialize MessagePack Singleton {fileName}: {ex.Message} Inner: {ex.InnerException?.Message}");
                                throw;
                            }
                        }
                    }

                    _treeData.Add(new TreeViewItemData<object>(++id,
                        new DataItem { Key = fileName, Value = $"Items: {children.Count}" }, children));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to deserialize {fileName}: {e.Message}");
                    _treeData.Add(new TreeViewItemData<object>(++id,
                        new DataItem { Key = fileName, Value = $"Error: {e.Message}" }));
                }
            }
        }

        private object GetItem(int index)
        {
            return _treeView.GetItemDataForIndex<object>(index);
        }

        private string GetName(object item)
        {
            if (item is DataItem d) return d.Key;
            return item?.ToString();
        }

        private string GetValue(object item)
        {
            if (item is DataItem d)
            {
                if (d.Value == null) return "null";
                if (d.Value is string s) return s;
                // Simple JSON serialization for preview
                return JsonUtility.ToJson(d.Value);
            }

            return "";
        }

        [Serializable]
        private class JsonStorageWrapper<T>
        {
            public List<T> Items = new List<T>();
        }

        private class DataItem
        {
            public string Key;
            public object Value;
        }

        private static Type GetTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == typeName)
                        return type;
                }
            }

            return null;
        }

        private static T DeserializeMsgPack<T>(byte[] bytes, MessagePackSerializerOptions options)
        {
            return MessagePackSerializer.Deserialize<T>(bytes, options);
        }
    }
}
