#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lilja.Repository.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace Lilja.Repository.Editor
{
    /// <summary>
    /// Unity エディター内で稼働中のリポジトリと永続化されたリポジトリファイルを表示します。
    /// </summary>
    public sealed class RepositoryViewer : EditorWindow
    {
        private const int PreviewLimit = 200;

        private readonly List<RepositoryEntry> _entries = new List<RepositoryEntry>();
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private RepositoryTracker.RepositoryType _selectedType;
        private int _selectedEntryIndex = -1;
        private object? _selectedDetailValue;
        private string _selectedDetailText = string.Empty;

        /// <summary>
        /// Repository Viewer ウィンドウを開きます。
        /// </summary>
        [MenuItem("Lilja/Repository/Repository Viewer")]
        public static void Open()
        {
            GetWindow<RepositoryViewer>("Repository Viewer");
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawEntriesPane();
                DrawDetailPane();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var nextType = (RepositoryTracker.RepositoryType)EditorGUILayout.EnumPopup(_selectedType, EditorStyles.toolbarPopup, GUILayout.Width(140f));
                if (nextType != _selectedType)
                {
                    _selectedType = nextType;
                    Reload();
                }

                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Reload();
                }

                GUILayout.FlexibleSpace();

                if (_selectedType != RepositoryTracker.RepositoryType.InMemory &&
                    GUILayout.Button("Open Directory", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    Directory.CreateDirectory(Application.persistentDataPath);
                    EditorUtility.RevealInFinder(Application.persistentDataPath);
                }
            }
        }

        private void DrawEntriesPane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.45f)))
            {
                EditorGUILayout.LabelField(Application.isPlaying ? "Live Repositories" : "Persisted Files", EditorStyles.boldLabel);

                if (!Application.isPlaying && _selectedType == RepositoryTracker.RepositoryType.InMemory)
                {
                    EditorGUILayout.HelpBox("InMemory repositories are only shown during play mode.", MessageType.Info);
                    return;
                }

                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                for (var index = 0; index < _entries.Count; index++)
                {
                    var entry = _entries[index];
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        if (GUILayout.Toggle(_selectedEntryIndex == index, entry.Title, "Button"))
                        {
                            if (_selectedEntryIndex != index)
                            {
                                SelectEntry(index);
                            }
                        }

                        EditorGUILayout.LabelField("Type", entry.Kind);
                        EditorGUILayout.LabelField("Preview", entry.Preview);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetailPane()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Detail", EditorStyles.boldLabel);

                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                EditorGUILayout.TextArea(_selectedDetailText, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (_selectedDetailValue is not null && GUILayout.Button("Refresh Selected Detail"))
                {
                    _selectedDetailText = FormatDetail(_selectedDetailValue);
                }
            }
        }

        private void Reload()
        {
            _entries.Clear();
            _selectedEntryIndex = -1;
            _selectedDetailValue = null;
            _selectedDetailText = string.Empty;

            if (Application.isPlaying)
            {
                LoadLiveRepositories();
            }
            else
            {
                LoadPersistedFiles();
            }

            if (_entries.Count > 0)
            {
                SelectEntry(0);
            }

            Repaint();
        }

        private void LoadLiveRepositories()
        {
            foreach (var repository in RepositoryTracker.GetAll(_selectedType))
            {
                var repositoryType = repository.GetType();
                try
                {
                    var reader = new ViewerReadOnlyTx();
                    var detail = ReadRepositoryValue(repository, repositoryType, reader);
                    _entries.Add(new RepositoryEntry(
                        repositoryType.Name,
                        repositoryType.FullName ?? repositoryType.Name,
                        BuildPreview(detail),
                        detail));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(ex);
                    _entries.Add(new RepositoryEntry(
                        repositoryType.Name,
                        "Error",
                        ex.Message,
                        ex.Message));
                }
            }
        }

        private void LoadPersistedFiles()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            var searchPattern = _selectedType == RepositoryTracker.RepositoryType.Json ? "*.json" : "*.msgpack";
            foreach (var filePath in Directory.GetFiles(Application.persistentDataPath, searchPattern))
            {
                var fileEntry = LoadPersistedFile(filePath, _selectedType);
                _entries.Add(fileEntry);
            }
        }

        private RepositoryEntry LoadPersistedFile(string filePath, RepositoryTracker.RepositoryType repositoryType)
        {
            try
            {
                var storageIdentifier = Path.GetFileNameWithoutExtension(filePath);
                var metadata = RepositoryMetadata.Resolve(storageIdentifier);

                if (repositoryType == RepositoryTracker.RepositoryType.Json)
                {
                    var raw = File.ReadAllText(filePath);
                    if (metadata is null)
                    {
                        return new RepositoryEntry(Path.GetFileName(filePath), "Unknown", TrimPreview(raw), raw);
                    }

                    var detail = DeserializeJson(raw, metadata);
                    return detail;
                }

                if (!MessagePackReflectionBridge.IsAvailable)
                {
                    return new RepositoryEntry(Path.GetFileName(filePath), "Unknown", "Binary Data", "Binary Data");
                }

                if (metadata is null)
                {
                    return new RepositoryEntry(Path.GetFileName(filePath), "Unknown", "Binary Data", "Binary Data");
                }

                var bytes = File.ReadAllBytes(filePath);
                var options = MessagePackReflectionBridge.CreateOptions(metadata.EnvelopeFormatterType, metadata.DtoFormatterType);
                var envelope = MessagePackReflectionBridge.Deserialize(bytes, metadata.EnvelopeType, options);
                if (envelope is null)
                {
                    return new RepositoryEntry(Path.GetFileName(filePath), "Error", "Failed to deserialize MessagePack file.", "Failed to deserialize MessagePack file.");
                }

                var detailValue = ExtractEnvelopeValue(envelope, metadata);
                return new RepositoryEntry(Path.GetFileName(filePath), metadata.EntityType.FullName ?? metadata.EntityType.Name, BuildPreview(detailValue), detailValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                return new RepositoryEntry(Path.GetFileName(filePath), "Error", ex.Message, ex.Message);
            }
        }

        private RepositoryEntry DeserializeJson(string raw, RepositoryMetadata metadata)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return new RepositoryEntry(metadata.StorageIdentifier, metadata.EntityType.FullName ?? metadata.EntityType.Name, string.Empty, string.Empty);
                }

                var envelope = JsonUtility.FromJson(raw, metadata.EnvelopeType);
                if (envelope is null)
                {
                    return new RepositoryEntry(metadata.StorageIdentifier, "Error", "Failed to deserialize JSON file.", "Failed to deserialize JSON file.");
                }

                var detailValue = ExtractEnvelopeValue(envelope, metadata);
                return new RepositoryEntry(metadata.StorageIdentifier, metadata.EntityType.FullName ?? metadata.EntityType.Name, TrimPreview(raw), detailValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                return new RepositoryEntry(metadata.StorageIdentifier, "Error", ex.Message, ex.Message);
            }
        }

        private object ExtractEnvelopeValue(object envelope, RepositoryMetadata metadata)
        {
            if (metadata.IsKeyed)
            {
                var itemsField = metadata.EnvelopeType.GetField("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return itemsField?.GetValue(envelope) ?? Array.Empty<object>();
            }

            var hasValueField = metadata.EnvelopeType.GetField("HasValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var itemField = metadata.EnvelopeType.GetField("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var hasValue = hasValueField is not null && (bool)(hasValueField.GetValue(envelope) ?? false);
            return hasValue ? itemField?.GetValue(envelope) ?? "null" : "No Value";
        }

        private object ReadRepositoryValue(object repository, Type repositoryType, IReadOnlyTx tx)
        {
            var allMethod = repositoryType.GetMethod("All", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(IReadOnlyTx) }, null);
            if (allMethod is not null)
            {
                return allMethod.Invoke(repository, new object[] { tx }) ?? Array.Empty<object>();
            }

            var readMethod = repositoryType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(IReadOnlyTx) }, null);
            if (readMethod is not null)
            {
                return readMethod.Invoke(repository, new object[] { tx }) ?? "No Value";
            }

            return repository.ToString() ?? repositoryType.Name;
        }

        private string BuildPreview(object? value)
        {
            if (value is null)
            {
                return "null";
            }

            if (value is string text)
            {
                return TrimPreview(text);
            }

            if (value is IList list)
            {
                return $"{list.Count} item(s)";
            }

            return TrimPreview(value.ToString() ?? value.GetType().Name);
        }

        private void SelectEntry(int index)
        {
            _selectedEntryIndex = index;
            _selectedDetailValue = _entries[index].Value;
            _selectedDetailText = FormatDetail(_entries[index].Value);
        }

        private string FormatDetail(object? value)
        {
            if (value is null)
            {
                return "null";
            }

            if (value is string text)
            {
                return text;
            }

            try
            {
                return JsonUtility.ToJson(value, true);
            }
            catch
            {
                if (value is IList list)
                {
                    var lines = new List<string>();
                    for (var index = 0; index < list.Count; index++)
                    {
                        lines.Add(GetItemLabel(list[index], index));
                        lines.Add(FormatDetail(list[index]));
                    }

                    return string.Join("\n\n", lines);
                }

                return value.ToString() ?? value.GetType().FullName ?? value.GetType().Name;
            }
        }

        private string GetItemLabel(object? item, int index)
        {
            if (item is null)
            {
                return $"Item {index}";
            }

            var type = item.GetType();
            foreach (var memberName in new[] { "Id", "Key", "Name", "id", "key", "name" })
            {
                var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is not null)
                {
                    return field.GetValue(item)?.ToString() ?? $"Item {index}";
                }

                var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is not null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(item)?.ToString() ?? $"Item {index}";
                }
            }

            return $"Item {index}";
        }

        private static string TrimPreview(string text)
        {
            return text.Length <= PreviewLimit ? text : text.Substring(0, PreviewLimit) + "...";
        }

        private sealed class ViewerReadOnlyTx : IReadOnlyTx
        {
            public void Dispose()
            {
            }
        }

        private sealed class RepositoryEntry
        {
            public RepositoryEntry(string title, string kind, string preview, object? value)
            {
                Title = title;
                Kind = kind;
                Preview = preview;
                Value = value;
            }

            public string Title { get; }

            public string Kind { get; }

            public string Preview { get; }

            public object? Value { get; }
        }

        private sealed class RepositoryMetadata
        {
            private RepositoryMetadata(string storageIdentifier, Type entityType, Type dtoType, Type envelopeType, Type dtoFormatterType, Type envelopeFormatterType, bool isKeyed)
            {
                StorageIdentifier = storageIdentifier;
                EntityType = entityType;
                DtoType = dtoType;
                EnvelopeType = envelopeType;
                DtoFormatterType = dtoFormatterType;
                EnvelopeFormatterType = envelopeFormatterType;
                IsKeyed = isKeyed;
            }

            public string StorageIdentifier { get; }

            public Type EntityType { get; }

            public Type DtoType { get; }

            public Type EnvelopeType { get; }

            public Type DtoFormatterType { get; }

            public Type EnvelopeFormatterType { get; }

            public bool IsKeyed { get; }

            public static RepositoryMetadata? Resolve(string storageIdentifier)
            {
                var separator = storageIdentifier.LastIndexOf('.');
                var entityName = separator >= 0 ? storageIdentifier.Substring(separator + 1) : storageIdentifier;
                var entityNamespace = separator >= 0 ? storageIdentifier.Substring(0, separator) : string.Empty;
                var entityFullName = string.IsNullOrEmpty(entityNamespace) ? entityName : $"{entityNamespace}.{entityName}";

                var entityType = FindType(entityFullName);
                var dtoNamespace = string.IsNullOrEmpty(entityNamespace) ? "Lilja.Repository.Generated.Dtos" : $"Lilja.Repository.Generated.Dtos.{entityNamespace}";
                var storageNamespace = string.IsNullOrEmpty(entityNamespace) ? "Lilja.Repository.Generated.Storage" : $"Lilja.Repository.Generated.Storage.{entityNamespace}";
                var formatterNamespace = string.IsNullOrEmpty(entityNamespace) ? "Lilja.Repository.Generated.Formatters" : $"Lilja.Repository.Generated.Formatters.{entityNamespace}";
                var dtoType = FindType($"{dtoNamespace}.{entityName}Dto");
                var envelopeType = FindType($"{storageNamespace}.{entityName}StorageEnvelope");
                var dtoFormatterType = FindType($"{formatterNamespace}.{entityName}DtoFormatter");
                var envelopeFormatterType = FindType($"{formatterNamespace}.{entityName}StorageEnvelopeFormatter");

                if (entityType is null || dtoType is null || envelopeType is null)
                {
                    return null;
                }

                var itemsField = envelopeType.GetField("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return new RepositoryMetadata(
                    storageIdentifier,
                    entityType,
                    dtoType,
                    envelopeType,
                    dtoFormatterType ?? typeof(object),
                    envelopeFormatterType ?? typeof(object),
                    itemsField is not null);
            }

            private static Type? FindType(string fullName)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(fullName, false);
                    if (type is not null)
                    {
                        return type;
                    }
                }

                return null;
            }
        }
    }
}
#endif
