using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Lilja.Repository.Diagnostics;

namespace Lilja.Repository.Editor
{
    internal static class RepositoryTreeDataLoader
    {
        private static readonly IReadOnlyTx TrackerReadOnlyTx = new ViewerReadOnlyTx();

        public static List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> Load(RepositoryTracker.RepositoryType repositoryType, ref int id)
        {
            var children = new List<UnityEditor.IMGUI.Controls.TreeViewItem<int>>();
            if (Application.isPlaying)
            {
                LoadFromTracker(repositoryType, children, ref id);
            }
            else if (repositoryType != RepositoryTracker.RepositoryType.InMemory)
            {
                LoadFromFiles(repositoryType, children, ref id);
            }

            return children;
        }

        private static void LoadFromFiles(RepositoryTracker.RepositoryType repositoryType, List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> children, ref int id)
        {
            if (repositoryType == RepositoryTracker.RepositoryType.MessagePack && !MessagePackReflectionBridge.IsAvailable)
            {
                return;
            }

            var path = Application.persistentDataPath;
            if (!Directory.Exists(path))
            {
                return;
            }

            var filePattern = repositoryType == RepositoryTracker.RepositoryType.Json ? "*.json" : "*.msgpack";
            foreach (var file in Directory.GetFiles(path, filePattern))
            {
                LoadPersistedFileRepository(repositoryType, file, children, ref id);
            }
        }

        private static void LoadPersistedFileRepository(
            RepositoryTracker.RepositoryType repositoryType,
            string file,
            List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> children,
            ref int id)
        {
            var fileName = Path.GetFileName(file);
            var entityName = Path.GetFileNameWithoutExtension(file);
            var dtoType = RuntimeTypeCache.FindType(entityName + "Dto");
            var envelopeType = RuntimeTypeCache.FindType(entityName + "StorageEnvelope");
            if (dtoType == null || envelopeType == null)
            {
                children.Add(new RepositoryTrackerViewItem(++id)
                {
                    RepositoryName = fileName,
                    Type = "Unknown",
                    ValuePreview = repositoryType == RepositoryTracker.RepositoryType.Json
                        ? LoadRawJsonPreview(file)
                        : "Binary Data",
                    IsRepository = true,
                    ItemCount = 0,
                });
                return;
            }

            var itemChildren = new List<UnityEditor.IMGUI.Controls.TreeViewItem<int>>();
            try
            {
                if (repositoryType == RepositoryTracker.RepositoryType.Json)
                {
                    LoadJsonFile(file, entityName, dtoType, envelopeType, itemChildren, ref id);
                }
                else
                {
                    LoadMessagePackFile(file, entityName, dtoType, envelopeType, itemChildren, ref id);
                }

                AddRepositoryNode(children, ref id, fileName, dtoType.Name, itemChildren);
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
                    ItemCount = 0,
                });
            }
        }

        private static void AddRepositoryNode(
            List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> children,
            ref int id,
            string repositoryName,
            string fallbackTypeName,
            List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> itemChildren)
        {
            var entityTypeName = (itemChildren.FirstOrDefault() as RepositoryTrackerViewItem)?.Type ?? fallbackTypeName;
            children.Add(new RepositoryTrackerViewItem(++id)
            {
                RepositoryName = repositoryName,
                Type = entityTypeName,
                ValuePreview = string.Empty,
                IsRepository = true,
                ItemCount = itemChildren.Count,
                children = itemChildren,
            });
        }

        private static void LoadJsonFile(string file, string entityName, Type dtoType, Type envelopeType, List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> itemChildren, ref int id)
        {
            var json = File.ReadAllText(file);
            var envelope = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson(json, envelopeType);
            LoadEnvelopeItems(entityName, dtoType, envelopeType, envelope, itemChildren, ref id);
        }

        private static void LoadMessagePackFile(string file, string entityName, Type dtoType, Type envelopeType, List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> itemChildren, ref int id)
        {
            var bytes = File.ReadAllBytes(file);
            var formatterType = RuntimeTypeCache.FindType(entityName + "DtoFormatter");
            var envelopeFormatterType = RuntimeTypeCache.FindType(entityName + "StorageEnvelopeFormatter");
            var options = MessagePackReflectionBridge.CreateOptions(envelopeFormatterType, formatterType);
            if (options == null)
            {
                throw new InvalidOperationException("MessagePack runtime could not be initialized.");
            }

            var envelope = MessagePackReflectionBridge.Deserialize(bytes, envelopeType, options);
            LoadEnvelopeItems(entityName, dtoType, envelopeType, envelope, itemChildren, ref id);
        }

        private static void LoadEnvelopeItems(
            string entityName,
            Type dtoType,
            Type envelopeType,
            object envelope,
            List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> itemChildren,
            ref int id)
        {
            if (envelope == null)
            {
                return;
            }

            var itemsField = envelopeType.GetField("Items");
            if (itemsField != null)
            {
                var items = itemsField.GetValue(envelope) as System.Collections.IList;
                if (items == null)
                {
                    return;
                }

                var index = 0;
                foreach (var item in items)
                {
                    AddDtoItem(entityName, dtoType, item, itemChildren, ref id, index++);
                }

                return;
            }

            var hasValueField = envelopeType.GetField("HasValue");
            var itemField = envelopeType.GetField("Item");
            var hasValue = (bool?)hasValueField?.GetValue(envelope) ?? false;
            if (!hasValue)
            {
                return;
            }

            var singleton = itemField?.GetValue(envelope);
            itemChildren.Add(new RepositoryTrackerViewItem(++id)
            {
                Key = "Singleton",
                Type = dtoType.Name,
                ValuePreview = GetValuePreview(singleton),
                FullValue = singleton,
                IsRepository = false,
                ItemCount = 0,
            });
        }

        private static void AddDtoItem(string entityName, Type dtoType, object item, List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> itemChildren, ref int id, int fallbackIndex)
        {
            itemChildren.Add(new RepositoryTrackerViewItem(++id)
            {
                Key = ExtractKeyFromDto(entityName, item, dtoType, fallbackIndex),
                Type = dtoType.Name,
                ValuePreview = GetValuePreview(item),
                FullValue = item,
                IsRepository = false,
                ItemCount = 0,
            });
        }

        private static string ExtractKeyFromDto(string entityName, object dto, Type dtoType, int fallbackIndex)
        {
            if (dto == null)
            {
                return $"Item {fallbackIndex}";
            }

            var keyAccessor = RuntimeTypeCache.FindEntityDtoKeyAccessor(entityName, dtoType);
            if (keyAccessor != null)
            {
                try
                {
                    var value = keyAccessor.Invoke(null, new[] { dto });
                    if (value != null)
                    {
                        return value.ToString();
                    }
                }
                catch
                {
                }
            }

            foreach (var fieldName in new[] { "Id", "Key", "Name", "id", "key", "name" })
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

            return $"Item {fallbackIndex}";
        }

        private static void LoadFromTracker(
            RepositoryTracker.RepositoryType repositoryType,
            List<UnityEditor.IMGUI.Controls.TreeViewItem<int>> children,
            ref int id)
        {
            var repositories = RepositoryTracker.GetAll(repositoryType).ToList();
            foreach (var repo in repositories)
            {
                var repoType = repo.GetType();
                var repoItem = new RepositoryTrackerViewItem(++id)
                {
                    RepositoryName = repoType.Name,
                    Type = repoType.Name,
                    ValuePreview = string.Empty,
                    IsRepository = true,
                };

                var itemChildren = new List<UnityEditor.IMGUI.Controls.TreeViewItem<int>>();
                try
                {
                    var allMethod = repoType.GetMethod("All");
                    if (allMethod != null)
                    {
                        var result = allMethod.Invoke(repo, new object[] { TrackerReadOnlyTx }) as System.Collections.IEnumerable;
                        var entityType = allMethod.ReturnType.GetGenericArguments()[0];
                        var getKeyMethod = entityType.GetMethod("GetKey", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        var toDtoMethod = entityType.GetMethod("ToDto", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                        var count = 0;
                        if (result != null)
                        {
                            foreach (var entity in result)
                            {
                                count++;
                                var key = getKeyMethod?.Invoke(null, new[] { entity })?.ToString() ?? $"Item {count}";
                                var displayValue = toDtoMethod?.Invoke(null, new[] { entity }) ?? entity;
                                itemChildren.Add(new RepositoryTrackerViewItem(++id)
                                {
                                    Key = key,
                                    Type = entityType.Name,
                                    ValuePreview = GetValuePreview(displayValue),
                                    FullValue = displayValue,
                                    IsRepository = false,
                                    ItemCount = 0,
                                });
                            }
                        }

                        repoItem.ItemCount = count;
                    }
                    else
                    {
                        var readMethod = repoType.GetMethod("Read", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(IReadOnlyTx) }, null);
                        var entity = readMethod?.Invoke(repo, new object[] { TrackerReadOnlyTx });
                        if (entity != null)
                        {
                            var entityType = entity.GetType();
                            var toDtoMethod = entityType.GetMethod("ToDto", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            var displayValue = toDtoMethod?.Invoke(null, new[] { entity }) ?? entity;
                            itemChildren.Add(new RepositoryTrackerViewItem(++id)
                            {
                                Key = "Singleton",
                                Type = entityType.Name,
                                ValuePreview = GetValuePreview(displayValue),
                                FullValue = displayValue,
                                IsRepository = false,
                                ItemCount = 0,
                            });
                            repoItem.ItemCount = 1;
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

        private static string LoadRawJsonPreview(string file)
        {
            try
            {
                var json = File.ReadAllText(file).Replace("\r", string.Empty).Replace("\n", " ");
                return json.Length > 200 ? json.Substring(0, 200) + "..." : json;
            }
            catch
            {
                return "Error reading file";
            }
        }

        private static string GetValuePreview(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string text)
            {
                return text;
            }

            try
            {
                var json = JsonUtility.ToJson(value);
                return json.Length > 200 ? json.Substring(0, 200) + "..." : json;
            }
            catch
            {
                return value.ToString();
            }
        }

        private sealed class ViewerReadOnlyTx : IReadOnlyTx
        {
            public void Dispose()
            {
            }
        }
    }
}
