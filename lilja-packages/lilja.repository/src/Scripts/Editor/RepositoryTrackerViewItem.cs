using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Lilja.Repository.Diagnostics;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;

namespace Lilja.Repository.Editor
{
    public class RepositoryTrackerViewItem : TreeViewItem<int>
    {
        public string RepositoryName { get; set; }
        public string Key { get; set; }
        public string Type { get; set; }
        public string ValuePreview { get; set; }
        public object FullValue { get; set; }
        public bool IsRepository { get; set; }
        public int ItemCount { get; set; }

        public RepositoryTrackerViewItem(int id) : base(id)
        {
        }
    }

    public class RepositoryTrackerTreeView : TreeView<int>
    {
        const string sortedColumnIndexStateKey = "RepositoryTrackerTreeView_sortedColumnIndex";

        public IReadOnlyList<TreeViewItem<int>> CurrentBindingItems;
        private RepositoryTracker.RepositoryType _currentType;

        public RepositoryTrackerTreeView(RepositoryTracker.RepositoryType type)
            : this(new TreeViewState<int>(), new MultiColumnHeader(new MultiColumnHeaderState(new[]
            {
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Entity/Key"),
                    width = 250,
                    minWidth = 100,
                    autoResize = true
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Value Preview"),
                    width = 400,
                    minWidth = 100,
                    autoResize = true
                },
            })), type)
        {
        }

        RepositoryTrackerTreeView(TreeViewState<int> state, MultiColumnHeader header,
            RepositoryTracker.RepositoryType type)
            : base(state, header)
        {
            _currentType = type;
            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = true;

            // Enable foldout functionality
            useScrollView = true;

            header.sortingChanged += Header_sortingChanged;

            header.ResizeToFit();
            Reload();

            header.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, 0);
        }

        public void SetRepositoryType(RepositoryTracker.RepositoryType type)
        {
            _currentType = type;
        }

        public void ReloadAndSort()
        {
            var currentSelected = this.state.selectedIDs;
            Reload();
            Header_sortingChanged(this.multiColumnHeader);
            this.state.selectedIDs = currentSelected;
        }

        private void Header_sortingChanged(MultiColumnHeader mch)
        {
            SessionState.SetInt(sortedColumnIndexStateKey, mch.sortedColumnIndex);
            var index = mch.sortedColumnIndex;
            var ascending = mch.IsSortedAscending(mch.sortedColumnIndex);

            var items = rootItem.children.Cast<RepositoryTrackerViewItem>();

            IOrderedEnumerable<RepositoryTrackerViewItem> orderedEnumerable;
            switch (index)
            {
                case 0:
                    orderedEnumerable = ascending
                        ? items.OrderBy(item => item.IsRepository).ThenBy(item => item.RepositoryName ?? item.Key)
                        : items.OrderByDescending(item => item.IsRepository)
                            .ThenByDescending(item => item.RepositoryName ?? item.Key);
                    break;
                case 1:
                    orderedEnumerable = ascending
                        ? items.OrderBy(item => item.ValuePreview)
                        : items.OrderByDescending(item => item.ValuePreview);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }

            CurrentBindingItems = rootItem.children = orderedEnumerable.Cast<TreeViewItem<int>>().ToList();
            BuildRows(rootItem);
        }

        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int> { depth = -1 };
            var children = new List<TreeViewItem<int>>();
            var id = 0;

            if (Application.isPlaying)
            {
                LoadFromTracker(children, ref id);
            }
            else
            {
                if (_currentType == RepositoryTracker.RepositoryType.InMemory)
                {
                    // InMemory is only available in play mode
                }
                else
                {
                    LoadFromFiles(children, ref id);
                }
            }

            CurrentBindingItems = children;
            root.children = children;
            return root;
        }

        private void LoadFromFiles(List<TreeViewItem<int>> children, ref int id)
        {
            var path = Application.persistentDataPath;
            var pattern = _currentType == RepositoryTracker.RepositoryType.Json ? "*.json" : "*.msgpack";

            if (!Directory.Exists(path))
            {
                return;
            }

            var files = Directory.GetFiles(path, pattern);

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
                        try
                        {
                            rawContent = File.ReadAllText(file);
                            rawContent = rawContent.Replace("\r", "").Replace("\n", " ");
                            if (rawContent.Length > 200) rawContent = rawContent.Substring(0, 200) + "...";
                        }
                        catch
                        {
                            rawContent = "Error reading file";
                        }
                    }

                    children.Add(new RepositoryTrackerViewItem(++id)
                    {
                        RepositoryName = fileName,
                        Type = "Unknown",
                        ValuePreview = rawContent,
                        IsRepository = true,
                        ItemCount = 0
                    });
                    continue;
                }

                var itemChildren = new List<TreeViewItem<int>>();

                try
                {
                    if (_currentType == RepositoryTracker.RepositoryType.Json)
                    {
                        LoadJsonFile(file, dtoType, itemChildren, ref id);
                    }
                    else // MessagePack
                    {
                        LoadMessagePackFile(file, entityName, dtoType, itemChildren, ref id);
                    }

                    var repoItem = new RepositoryTrackerViewItem(++id)
                    {
                        RepositoryName = fileName,
                        Type = dtoType.Name,
                        ValuePreview = "",
                        IsRepository = true,
                        ItemCount = itemChildren.Count,
                        children = itemChildren
                    };

                    children.Add(repoItem);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to deserialize {fileName}: {e.Message}");
                    children.Add(new RepositoryTrackerViewItem(++id)
                    {
                        RepositoryName = fileName,
                        Type = "Error",
                        ValuePreview = $"Error: {e.Message}",
                        IsRepository = true,
                        ItemCount = 0
                    });
                }
            }
        }

        private void LoadJsonFile(string file, Type dtoType, List<TreeViewItem<int>> itemChildren, ref int id)
        {
            var json = File.ReadAllText(file);

            if (json.Contains("\"Items\":"))
            {
                // Keyed repository
                var wrapperType = typeof(JsonStorageWrapper<>).MakeGenericType(dtoType);
                var wrapper = JsonUtility.FromJson(json, wrapperType);
                var itemsField = wrapperType.GetField("Items");
                var items = itemsField.GetValue(wrapper) as System.Collections.IList;

                if (items != null)
                {
                    int idx = 0;
                    foreach (var item in items)
                    {
                        var keyStr = ExtractKeyFromDto(item, dtoType, idx);
                        var preview = GetValuePreview(item);
                        itemChildren.Add(new RepositoryTrackerViewItem(++id)
                        {
                            Key = keyStr,
                            Type = dtoType.Name,
                            ValuePreview = preview,
                            FullValue = item,
                            IsRepository = false,
                            ItemCount = 0
                        });
                        idx++;
                    }
                }
            }
            else
            {
                // Singleton
                var singleton = JsonUtility.FromJson(json, dtoType);
                var preview = GetValuePreview(singleton);
                itemChildren.Add(new RepositoryTrackerViewItem(++id)
                {
                    Key = "Singleton",
                    Type = dtoType.Name,
                    ValuePreview = preview,
                    FullValue = singleton,
                    IsRepository = false,
                    ItemCount = 0
                });
            }
        }

        private void LoadMessagePackFile(string file, string entityName, Type dtoType,
            List<TreeViewItem<int>> itemChildren,
            ref int id)
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
                object list = null;

                try
                {
                    // Use the helper method via reflection
                    var method = typeof(RepositoryTrackerTreeView)
                        .GetMethod("DeserializeMsgPack", BindingFlags.NonPublic | BindingFlags.Static)
                        ?.MakeGenericMethod(listType);
                    list = method!.Invoke(null, new object[] { bytes, options });
                }
                catch (Exception ex)
                {
                    Debug.Log($"Not a list format, trying singleton: {ex.InnerException?.Message ?? ex.Message}");
                }

                if (list != null && list is System.Collections.IList listItems)
                {
                    int idx = 0;
                    foreach (var item in listItems)
                    {
                        var keyStr = ExtractKeyFromDto(item, dtoType, idx);
                        var preview = GetValuePreview(item);
                        itemChildren.Add(new RepositoryTrackerViewItem(++id)
                        {
                            Key = keyStr,
                            Type = dtoType.Name,
                            ValuePreview = preview,
                            FullValue = item,
                            IsRepository = false,
                            ItemCount = 0
                        });
                        idx++;
                    }
                }
                else
                {
                    // Try Singleton
                    try
                    {
                        var method = typeof(RepositoryTrackerTreeView)
                            .GetMethod("DeserializeMsgPack", BindingFlags.NonPublic | BindingFlags.Static)
                            ?.MakeGenericMethod(dtoType);
                        var singleton = method!.Invoke(null, new object[] { bytes, options });

                        var preview = GetValuePreview(singleton);
                        itemChildren.Add(new RepositoryTrackerViewItem(++id)
                        {
                            Key = "Singleton",
                            Type = dtoType.Name,
                            ValuePreview = preview,
                            FullValue = singleton,
                            IsRepository = false,
                            ItemCount = 0
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Failed to deserialize MessagePack Singleton {Path.GetFileName(file)}: {ex.InnerException?.Message ?? ex.Message}\nStack: {ex.StackTrace}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load MessagePack file {Path.GetFileName(file)}: {ex.Message}");
                throw;
            }
        }

        private static T DeserializeMsgPack<T>(byte[] bytes, MessagePackSerializerOptions options)
        {
            return MessagePackSerializer.Deserialize<T>(bytes, options);
        }

        private string ExtractKeyFromDto(object dto, Type dtoType, int fallbackIndex)
        {
            if (dto == null) return $"Item {fallbackIndex}";

            // Try common key field names
            var keyFieldNames = new[] { "Id", "Key", "Name", "key", "id", "name" };

            foreach (var fieldName in keyFieldNames)
            {
                var field = dtoType.GetField(fieldName);
                if (field != null)
                {
                    var value = field.GetValue(dto);
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }

                var property = dtoType.GetProperty(fieldName);
                if (property != null)
                {
                    var value = property.GetValue(dto);
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
            }

            // Fallback to index
            return $"Item {fallbackIndex}";
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

        [Serializable]
        private class JsonStorageWrapper<T>
        {
            public List<T> Items = new List<T>();
        }

        private void LoadFromTracker(List<TreeViewItem<int>> children, ref int id)
        {
            var repositories = RepositoryTracker.GetAll(_currentType).ToList();

            foreach (var repo in repositories)
            {
                var repoType = repo.GetType();
                var repoItem = new RepositoryTrackerViewItem(++id)
                {
                    RepositoryName = repoType.Name,
                    Type = repoType.Name,
                    ValuePreview = "",
                    IsRepository = true,
                    ItemCount = 0
                };

                var itemChildren = new List<TreeViewItem<int>>();

                try
                {
                    var allMethod = repoType.GetMethod("All");
                    if (allMethod != null)
                    {
                        var result = allMethod.Invoke(repo, new object[] { null });
                        if (result is System.Collections.IEnumerable enumerable)
                        {
                            var entityType = allMethod.ReturnType.GetGenericArguments()[0];
                            var getKeyMethod = entityType.GetMethod("GetKey",
                                BindingFlags.Static |
                                BindingFlags.NonPublic |
                                BindingFlags.Public);
                            var toDtoMethod = entityType.GetMethod("ToDto",
                                BindingFlags.Static |
                                BindingFlags.NonPublic |
                                BindingFlags.Public);

                            var count = 0;
                            foreach (var entity in enumerable)
                            {
                                count++;
                                var keyStr = getKeyMethod != null
                                    ? getKeyMethod.Invoke(null, new[] { entity })?.ToString() ?? "?"
                                    : $"Item {count}";

                                object displayValue = toDtoMethod != null
                                    ? toDtoMethod.Invoke(null, new[] { entity })
                                    : entity;

                                var preview = GetValuePreview(displayValue);

                                itemChildren.Add(new RepositoryTrackerViewItem(++id)
                                {
                                    Key = keyStr,
                                    Type = entityType.Name,
                                    ValuePreview = preview,
                                    FullValue = displayValue,
                                    IsRepository = false,
                                    ItemCount = 0
                                });
                            }

                            repoItem.ItemCount = count;
                        }
                    }
                    else
                    {
                        // Singleton repository
                        var cacheField = repoType.GetField("cache", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (cacheField != null)
                        {
                            var cache = cacheField.GetValue(repo);
                            if (cache != null)
                            {
                                var preview = GetValuePreview(cache);
                                itemChildren.Add(new RepositoryTrackerViewItem(++id)
                                {
                                    Key = "Singleton",
                                    Type = cache.GetType().Name,
                                    ValuePreview = preview,
                                    FullValue = cache,
                                    IsRepository = false,
                                    ItemCount = 0
                                });
                                repoItem.ItemCount = 1;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to read repository {repoType.Name}: {e.Message}");
                }

                repoItem.children = itemChildren;
                children.Add(repoItem);
            }
        }

        private string GetValuePreview(object value)
        {
            if (value == null) return "null";
            if (value is string s) return s;

            try
            {
                var json = JsonUtility.ToJson(value);
                if (json.Length > 200)
                {
                    json = json.Substring(0, 200) + "...";
                }

                return json;
            }
            catch
            {
                return value.ToString();
            }
        }

        protected override bool CanMultiSelect(TreeViewItem<int> item)
        {
            return false;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as RepositoryTrackerViewItem;
            if (item == null) return;

            for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); visibleColumnIndex++)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                var columnIndex = args.GetColumn(visibleColumnIndex);

                var labelStyle = args.selected ? EditorStyles.whiteLabel : EditorStyles.label;
                labelStyle.alignment = TextAnchor.MiddleLeft;

                switch (columnIndex)
                {
                    case 0:
                        // First column - show foldout for repositories
                        if (item.IsRepository)
                        {
                            var displayName = item.RepositoryName;
                            // Remove "Repository" suffix if present
                            if (displayName.EndsWith("Repository"))
                            {
                                displayName = displayName.Substring(0, displayName.Length - "Repository".Length);
                            }

                            displayName += $" ({item.ItemCount})";

                            // Make entire row clickable for expand/collapse
                            var toggleRect = rect;
                            var wasExpanded = IsExpanded(item.id);

                            // Check if clicked anywhere in the row
                            if (Event.current.type == EventType.MouseDown &&
                                toggleRect.Contains(Event.current.mousePosition))
                            {
                                SetExpanded(item.id, !wasExpanded);
                                Event.current.Use();
                            }

                            // Draw the foldout triangle
                            var foldoutRect = new Rect(rect.x, rect.y, 12, rect.height);
                            EditorGUI.Foldout(foldoutRect, wasExpanded, GUIContent.none, true);

                            // Draw the label after the foldout
                            var labelRect = new Rect(rect.x + 14, rect.y, rect.width - 14, rect.height);
                            var boldStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
                            EditorGUI.LabelField(labelRect, displayName, boldStyle);
                        }
                        else
                        {
                            // Child items - just show the key
                            var indentRect = rect;
                            indentRect.xMin += 15f;
                            EditorGUI.LabelField(indentRect, item.Key, labelStyle);
                        }

                        break;
                    case 1:
                        EditorGUI.LabelField(rect, item.ValuePreview, labelStyle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, null);
                }
            }
        }
    }
}