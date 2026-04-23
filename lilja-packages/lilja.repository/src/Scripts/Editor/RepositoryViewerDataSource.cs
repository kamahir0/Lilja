#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Lilja.Repository.Diagnostics;
using UnityEngine;

namespace Lilja.Repository.Editor
{
    /// <summary>
    /// RepositoryViewer 向けに、live repository と persisted file を UI 非依存のスナップショットへ変換します。
    /// </summary>
    internal sealed class RepositoryViewerDataSource
    {
        private const int PreviewLimit = 120;
        private static readonly string[] FallbackKeyMemberNames = { "Id", "Key", "Name", "id", "key", "name" };

        /// <summary>
        /// 現在のプロジェクトで利用可能な保存方式一覧を返します。
        /// </summary>
        public IReadOnlyList<RepositoryTracker.RepositoryType> GetAvailableRepositoryTypes()
        {
            var types = new List<RepositoryTracker.RepositoryType>
            {
                RepositoryTracker.RepositoryType.InMemory,
                RepositoryTracker.RepositoryType.Json,
            };

            if (MessagePackReflectionBridge.IsAvailable)
            {
                types.Add(RepositoryTracker.RepositoryType.MessagePack);
            }

            return types;
        }

        /// <summary>
        /// 指定された保存方式に対応する repository 一覧を読み込みます。
        /// </summary>
        public IReadOnlyList<RepositorySnapshot> LoadRepositories(RepositoryTracker.RepositoryType repositoryType)
        {
            return Application.isPlaying
                ? LoadLiveRepositories(repositoryType)
                : LoadPersistedRepositories(repositoryType);
        }

        /// <summary>
        /// Repository 一覧が空のときに表示するメッセージを返します。
        /// </summary>
        public string GetRepositoryEmptyMessage(RepositoryTracker.RepositoryType repositoryType)
        {
            if (!Application.isPlaying && repositoryType == RepositoryTracker.RepositoryType.InMemory)
            {
                return "InMemory repositories are only shown during play mode.";
            }

            return Application.isPlaying
                ? "No live repositories found."
                : "No persisted repositories found.";
        }

        private static IReadOnlyList<RepositorySnapshot> LoadLiveRepositories(RepositoryTracker.RepositoryType repositoryType)
        {
            var repositories = new List<RepositorySnapshot>();

            foreach (var repository in RepositoryTracker.GetAll(repositoryType))
            {
                var runtimeType = repository.GetType();
                var title = runtimeType.Name;
                var kind = runtimeType.FullName ?? runtimeType.Name;
                var stableId = BuildLiveRepositoryStableId(runtimeType, repository);

                try
                {
                    var reader = new ViewerReadOnlyTx();
                    var allMethod = FindAllMethod(runtimeType);
                    if (allMethod is not null)
                    {
                        var items = ToObjectList(allMethod.Invoke(repository, new object[] { reader }));
                        var records = BuildKeyedRecords(items, item => GetLiveRecordKey(runtimeType, repository, item));
                        repositories.Add(new RepositorySnapshot(
                            stableId,
                            title,
                            kind,
                            BuildCountSummary(records.Count),
                            records,
                            "No records found."));
                        continue;
                    }

                    var readMethod = FindSingletonReadMethod(runtimeType);
                    if (readMethod is not null)
                    {
                        var value = readMethod.Invoke(repository, new object[] { reader });
                        var records = BuildSingletonRecords(value, runtimeType.Name);
                        repositories.Add(new RepositorySnapshot(
                            stableId,
                            title,
                            kind,
                            records.Count > 0 ? "1 value" : "No value",
                            records,
                            "No record found."));
                        continue;
                    }

                    var fallbackValue = repository.ToString() ?? runtimeType.Name;
                    var fallbackRecords = CreateInfoRecords("Value", fallbackValue, fallbackValue);
                    repositories.Add(new RepositorySnapshot(
                        stableId,
                        title,
                        kind,
                        BuildCountSummary(fallbackRecords.Count),
                        fallbackRecords,
                        "No records found."));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(ex);
                    repositories.Add(CreateErrorRepositorySnapshot(stableId, title, kind, ex.Message));
                }
            }

            return repositories;
        }

        private static IReadOnlyList<RepositorySnapshot> LoadPersistedRepositories(RepositoryTracker.RepositoryType repositoryType)
        {
            if (repositoryType == RepositoryTracker.RepositoryType.InMemory)
            {
                return Array.Empty<RepositorySnapshot>();
            }

            Directory.CreateDirectory(Application.persistentDataPath);
            var searchPattern = repositoryType == RepositoryTracker.RepositoryType.Json ? "*.json" : "*.msgpack";

            return Directory
                .GetFiles(Application.persistentDataPath, searchPattern)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .Select(path => LoadPersistedRepository(path, repositoryType))
                .ToList();
        }

        private static RepositorySnapshot LoadPersistedRepository(string filePath, RepositoryTracker.RepositoryType repositoryType)
        {
            var storageIdentifier = Path.GetFileNameWithoutExtension(filePath);
            var title = storageIdentifier;
            var kind = Path.GetFileName(filePath);
            var stableId = NormalizePath(filePath);

            try
            {
                var metadata = RepositoryMetadata.Resolve(storageIdentifier);
                if (repositoryType == RepositoryTracker.RepositoryType.Json)
                {
                    return LoadJsonRepository(filePath, stableId, title, metadata);
                }

                return LoadMessagePackRepository(filePath, stableId, title, metadata);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                return CreateErrorRepositorySnapshot(stableId, title, kind, ex.Message);
            }
        }

        private static RepositorySnapshot LoadJsonRepository(string filePath, string stableId, string title, RepositoryMetadata? metadata)
        {
            var raw = File.ReadAllText(filePath);
            if (metadata is null)
            {
                var records = CreateInfoRecords("Content", raw, raw);
                return new RepositorySnapshot(stableId, title, "Unknown", TrimPreview(raw), records, "No records found.");
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return new RepositorySnapshot(
                    stableId,
                    title,
                    metadata.EntityType.FullName ?? metadata.EntityType.Name,
                    metadata.IsKeyed ? "0 record(s)" : "No value",
                    Array.Empty<RecordSnapshot>(),
                    metadata.IsKeyed ? "No records found." : "No record found.");
            }

            var envelope = JsonUtility.FromJson(raw, metadata.EnvelopeType);
            if (envelope is null)
            {
                return CreateErrorRepositorySnapshot(stableId, title, metadata.EntityType.FullName ?? metadata.EntityType.Name, "Failed to deserialize JSON file.");
            }

            return CreateMetadataBackedSnapshot(stableId, title, metadata, envelope, raw);
        }

        private static RepositorySnapshot LoadMessagePackRepository(string filePath, string stableId, string title, RepositoryMetadata? metadata)
        {
            if (!MessagePackReflectionBridge.IsAvailable)
            {
                var message = "Binary Data\nMessagePack runtime was not found in the current project.";
                var records = CreateInfoRecords("Binary Data", "Binary Data", message);
                return new RepositorySnapshot(stableId, title, "Unknown", "Binary Data", records, "No records found.");
            }

            if (metadata is null)
            {
                var message = "Binary Data\nMetadata could not be resolved for this MessagePack file.";
                var records = CreateInfoRecords("Binary Data", "Binary Data", message);
                return new RepositorySnapshot(stableId, title, "Unknown", "Binary Data", records, "No records found.");
            }

            var bytes = File.ReadAllBytes(filePath);
            var options = MessagePackReflectionBridge.CreateOptions(metadata.EnvelopeFormatterType, metadata.DtoFormatterType);
            var envelope = MessagePackReflectionBridge.Deserialize(bytes, metadata.EnvelopeType, options);
            if (envelope is null)
            {
                return CreateErrorRepositorySnapshot(stableId, title, metadata.EntityType.FullName ?? metadata.EntityType.Name, "Failed to deserialize MessagePack file.");
            }

            return CreateMetadataBackedSnapshot(stableId, title, metadata, envelope, "Binary Data");
        }

        private static RepositorySnapshot CreateMetadataBackedSnapshot(
            string stableId,
            string title,
            RepositoryMetadata metadata,
            object envelope,
            string fallbackPreview)
        {
            var records = metadata.IsKeyed
                ? BuildKeyedRecords(ExtractKeyedItems(envelope, metadata), item => GetPersistedRecordKey(metadata, item))
                : BuildSingletonRecords(ExtractSingletonItem(envelope, metadata), metadata.EntityType.Name);

            var preview = metadata.IsKeyed
                ? BuildCountSummary(records.Count)
                : records.Count > 0 ? TrimPreview(records[0].Preview) : "No value";

            return new RepositorySnapshot(
                stableId,
                title,
                metadata.EntityType.FullName ?? metadata.EntityType.Name,
                string.IsNullOrEmpty(preview) ? fallbackPreview : preview,
                records,
                metadata.IsKeyed ? "No records found." : "No record found.");
        }

        private static RepositorySnapshot CreateErrorRepositorySnapshot(string stableId, string title, string kind, string message)
        {
            var records = CreateInfoRecords("Error", "Error", message);
            return new RepositorySnapshot(stableId, title, kind, message, records, "No records found.");
        }

        private static IReadOnlyList<RecordSnapshot> BuildKeyedRecords(
            IReadOnlyList<object?> items,
            Func<object?, string> keySelector)
        {
            if (items.Count == 0)
            {
                return Array.Empty<RecordSnapshot>();
            }

            var records = new List<RecordSnapshot>(items.Count);
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var title = keySelector(item);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Item {index}";
                }

                var preview = BuildValuePreview(item);
                var stableId = $"keyed:{EscapeString(title)}";
                records.Add(new RecordSnapshot(
                    stableId,
                    title,
                    preview,
                    FormatDetail(item),
                    item?.GetType().FullName ?? "null"));
            }

            return records;
        }

        private static IReadOnlyList<RecordSnapshot> BuildSingletonRecords(object? value, string title)
        {
            if (value is null)
            {
                return Array.Empty<RecordSnapshot>();
            }

            return new[]
            {
                new RecordSnapshot(
                    "singleton",
                    title,
                    BuildValuePreview(value),
                    FormatDetail(value),
                    value.GetType().FullName ?? value.GetType().Name),
            };
        }

        private static IReadOnlyList<RecordSnapshot> CreateInfoRecords(string title, string preview, string detailText)
        {
            return new[]
            {
                new RecordSnapshot(title, title, preview, detailText, title),
            };
        }

        private static IReadOnlyList<object?> ExtractKeyedItems(object envelope, RepositoryMetadata metadata)
        {
            var itemsField = metadata.EnvelopeType.GetField("Items", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return ToObjectList(itemsField?.GetValue(envelope));
        }

        private static object? ExtractSingletonItem(object envelope, RepositoryMetadata metadata)
        {
            var hasValueField = metadata.EnvelopeType.GetField("HasValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var itemField = metadata.EnvelopeType.GetField("Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var hasValue = hasValueField is not null && (bool)(hasValueField.GetValue(envelope) ?? false);
            return hasValue ? itemField?.GetValue(envelope) : null;
        }

        private static MethodInfo? FindAllMethod(Type repositoryType)
        {
            return repositoryType.GetMethod("All", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(IReadOnlyTx) }, null);
        }

        private static MethodInfo? FindSingletonReadMethod(Type repositoryType)
        {
            return repositoryType.GetMethod("Read", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(IReadOnlyTx) }, null);
        }

        private static string GetLiveRecordKey(Type repositoryType, object repository, object? item)
        {
            if (item is null)
            {
                return "null";
            }

            var keyMethod = FindInstanceMethod(repositoryType, "GetKey");

            if (keyMethod is not null)
            {
                try
                {
                    var key = keyMethod.Invoke(repository, new[] { item });
                    if (key is not null)
                    {
                        return key.ToString() ?? item.GetType().Name;
                    }
                }
                catch
                {
                }
            }

            return GetFallbackKey(item);
        }

        private static string GetPersistedRecordKey(RepositoryMetadata metadata, object? item)
        {
            if (item is null)
            {
                return "null";
            }

            if (metadata.TryGetKeyFromDto(item, out var key) && key is not null)
            {
                return key.ToString() ?? item.GetType().Name;
            }

            return GetFallbackKey(item);
        }

        private static string GetFallbackKey(object item)
        {
            var type = item.GetType();
            foreach (var memberName in FallbackKeyMemberNames)
            {
                var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field is not null)
                {
                    return field.GetValue(item)?.ToString() ?? type.Name;
                }

                var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property is not null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(item)?.ToString() ?? type.Name;
                }
            }

            return type.Name;
        }

        private static string BuildCountSummary(int count)
        {
            return $"{count} record(s)";
        }

        private static string BuildValuePreview(object? value)
        {
            if (value is null)
            {
                return "null";
            }

            if (TryFormatScalar(value, true, out var scalar))
            {
                return TrimPreview(scalar);
            }

            try
            {
                var json = JsonUtility.ToJson(value, false);
                if (!string.IsNullOrWhiteSpace(json) && json != "{}")
                {
                    return TrimPreview(json);
                }
            }
            catch
            {
            }

            var visited = new HashSet<object>(ReferenceComparer.Instance);
            return TrimPreview(ToSingleLinePreview(FormatJsonLikeValue(value, 0, visited)));
        }

        private static string BuildScalarSummary(object? value)
        {
            if (value is null)
            {
                return "null";
            }

            if (TryFormatScalar(value, false, out var formatted))
            {
                return formatted;
            }

            return value.ToString() ?? value.GetType().Name;
        }

        private static string FormatDetail(object? value)
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
                var json = JsonUtility.ToJson(value, true);
                if (!string.IsNullOrWhiteSpace(json) && json != "{}")
                {
                    return json;
                }
            }
            catch
            {
            }

            var visited = new HashSet<object>(ReferenceComparer.Instance);
            return FormatJsonLikeValue(value, 0, visited);
        }

        private static string FormatJsonLikeValue(object? value, int depth, HashSet<object> visited)
        {
            if (value is null)
            {
                return "null";
            }

            if (TryFormatScalar(value, true, out var scalar))
            {
                return scalar;
            }

            if (!value.GetType().IsValueType)
            {
                if (!visited.Add(value))
                {
                    return "\"<cyclic reference>\"";
                }
            }

            try
            {
                if (value is IDictionary dictionary)
                {
                    return FormatDictionary(dictionary, depth, visited);
                }

                if (value is IEnumerable enumerable)
                {
                    return FormatEnumerable(enumerable, depth, visited);
                }

                return FormatObject(value, depth, visited);
            }
            finally
            {
                if (!value.GetType().IsValueType)
                {
                    visited.Remove(value);
                }
            }
        }

        private static string FormatDictionary(IDictionary dictionary, int depth, HashSet<object> visited)
        {
            if (dictionary.Count == 0)
            {
                return "{}";
            }

            var indent = new string(' ', depth * 2);
            var childIndent = new string(' ', (depth + 1) * 2);
            var lines = new List<string> { "{" };
            foreach (DictionaryEntry entry in dictionary)
            {
                lines.Add($"{childIndent}\"{EscapeString(entry.Key?.ToString() ?? "null")}\": {FormatJsonLikeValue(entry.Value, depth + 1, visited)},");
            }

            lines[lines.Count - 1] = lines[lines.Count - 1].TrimEnd(',');
            lines.Add($"{indent}}}");
            return string.Join("\n", lines);
        }

        private static string FormatEnumerable(IEnumerable enumerable, int depth, HashSet<object> visited)
        {
            var values = enumerable.Cast<object?>().ToList();
            if (values.Count == 0)
            {
                return "[]";
            }

            var indent = new string(' ', depth * 2);
            var childIndent = new string(' ', (depth + 1) * 2);
            var lines = new List<string> { "[" };
            foreach (var item in values)
            {
                lines.Add($"{childIndent}{FormatJsonLikeValue(item, depth + 1, visited)},");
            }

            lines[lines.Count - 1] = lines[lines.Count - 1].TrimEnd(',');
            lines.Add($"{indent}]");
            return string.Join("\n", lines);
        }

        private static string FormatObject(object value, int depth, HashSet<object> visited)
        {
            var members = GetReadableMembers(value.GetType());
            if (members.Count == 0)
            {
                return BuildScalarSummary(value);
            }

            var indent = new string(' ', depth * 2);
            var childIndent = new string(' ', (depth + 1) * 2);
            var lines = new List<string> { "{" };
            foreach (var member in members)
            {
                object? memberValue;
                try
                {
                    memberValue = member switch
                    {
                        FieldInfo field => field.GetValue(value),
                        PropertyInfo property => property.GetValue(value),
                        _ => null,
                    };
                }
                catch
                {
                    memberValue = "<unavailable>";
                }

                lines.Add($"{childIndent}\"{EscapeString(member.Name)}\": {FormatJsonLikeValue(memberValue, depth + 1, visited)},");
            }

            lines[lines.Count - 1] = lines[lines.Count - 1].TrimEnd(',');
            lines.Add($"{indent}}}");
            return string.Join("\n", lines);
        }

        private static List<MemberInfo> GetReadableMembers(Type type)
        {
            var members = new List<MemberInfo>();
            members.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsStatic)
                .Cast<MemberInfo>());
            members.AddRange(type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Cast<MemberInfo>());
            return members
                .OrderBy(member => member.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static bool TryFormatScalar(object value, bool quoteStrings, out string formatted)
        {
            switch (value)
            {
                case string text:
                    formatted = quoteStrings ? $"\"{EscapeString(text)}\"" : text;
                    return true;
                case char character:
                    formatted = quoteStrings ? $"\"{EscapeString(character.ToString())}\"" : character.ToString();
                    return true;
                case bool boolean:
                    formatted = boolean ? "true" : "false";
                    return true;
                case Enum enumValue:
                    formatted = quoteStrings ? $"\"{enumValue}\"" : enumValue.ToString();
                    return true;
                case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    formatted = Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString() ?? "0";
                    return true;
                case DateTime dateTime:
                    formatted = quoteStrings ? $"\"{dateTime:O}\"" : dateTime.ToString("O", CultureInfo.InvariantCulture);
                    return true;
                case DateTimeOffset dateTimeOffset:
                    formatted = quoteStrings ? $"\"{dateTimeOffset:O}\"" : dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
                    return true;
                case Guid guid:
                    formatted = quoteStrings ? $"\"{guid:D}\"" : guid.ToString("D");
                    return true;
                default:
                    formatted = string.Empty;
                    return false;
            }
        }

        private static string EscapeString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string TrimPreview(string text)
        {
            var singleLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return singleLine.Length <= PreviewLimit ? singleLine : singleLine.Substring(0, PreviewLimit) + "...";
        }

        private static string ToSingleLinePreview(string text)
        {
            return text
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Trim();
        }

        private static List<object?> ToObjectList(object? value)
        {
            var items = new List<object?>();
            if (value is not IEnumerable enumerable || value is string)
            {
                return items;
            }

            foreach (var item in enumerable)
            {
                items.Add(item);
            }

            return items;
        }

        private static string BuildLiveRepositoryStableId(Type repositoryType, object repository)
        {
            var filePath = TryGetRepositoryFilePath(repositoryType, repository);
            if (!string.IsNullOrEmpty(filePath))
            {
                return $"{repositoryType.FullName}:{NormalizePath(filePath)}";
            }

            return $"{repositoryType.FullName}:{RuntimeHelpers.GetHashCode(repository)}";
        }

        private static string? TryGetRepositoryFilePath(Type repositoryType, object repository)
        {
            var filePathProperty = FindInstanceProperty(repositoryType, "FilePath");
            if (filePathProperty is null || filePathProperty.PropertyType != typeof(string))
            {
                return null;
            }

            return filePathProperty.GetValue(repository) as string;
        }

        private static MethodInfo? FindInstanceMethod(Type type, string methodName)
        {
            for (var currentType = type; currentType is not null; currentType = currentType.BaseType)
            {
                var method = currentType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(candidate =>
                        candidate.Name == methodName &&
                        candidate.GetParameters().Length == 1 &&
                        !candidate.IsStatic);

                if (method is not null)
                {
                    return method;
                }
            }

            return null;
        }

        private static PropertyInfo? FindInstanceProperty(Type type, string propertyName)
        {
            for (var currentType = type; currentType is not null; currentType = currentType.BaseType)
            {
                var property = currentType.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property is not null)
                {
                    return property;
                }
            }

            return null;
        }

        private static string NormalizePath(string filePath)
        {
            return Path.GetFullPath(filePath).Replace('\\', '/');
        }

        private sealed class ViewerReadOnlyTx : IReadOnlyTx
        {
            public void Dispose()
            {
            }
        }

        private sealed class RepositoryMetadata
        {
            private RepositoryMetadata(
                string storageIdentifier,
                Type entityType,
                Type dtoType,
                Type envelopeType,
                Type dtoFormatterType,
                Type envelopeFormatterType,
                bool isKeyed)
            {
                StorageIdentifier = storageIdentifier;
                EntityType = entityType;
                DtoType = dtoType;
                EnvelopeType = envelopeType;
                DtoFormatterType = dtoFormatterType;
                EnvelopeFormatterType = envelopeFormatterType;
                IsKeyed = isKeyed;
                GetKeyFromDtoMethod = FindGetKeyFromDtoMethod(entityType, dtoType);
            }

            public string StorageIdentifier { get; }

            public Type EntityType { get; }

            public Type DtoType { get; }

            public Type EnvelopeType { get; }

            public Type DtoFormatterType { get; }

            public Type EnvelopeFormatterType { get; }

            public bool IsKeyed { get; }

            private MethodInfo? GetKeyFromDtoMethod { get; }

            public bool TryGetKeyFromDto(object dto, out object? key)
            {
                key = null;
                if (GetKeyFromDtoMethod is null || !DtoType.IsInstanceOfType(dto))
                {
                    return false;
                }

                try
                {
                    key = GetKeyFromDtoMethod.Invoke(null, new[] { dto });
                    return true;
                }
                catch
                {
                    return false;
                }
            }

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

            private static MethodInfo? FindGetKeyFromDtoMethod(Type entityType, Type dtoType)
            {
                return entityType
                    .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method =>
                        method.Name == "GetKeyFromDto" &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType == dtoType);
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

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object? x, object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    /// <summary>
    /// UI が描画に使う repository 単位のスナップショットです。
    /// </summary>
    internal sealed class RepositorySnapshot
    {
        public RepositorySnapshot(
            string stableId,
            string title,
            string kind,
            string preview,
            IReadOnlyList<RecordSnapshot> records,
            string emptyMessage)
        {
            StableId = stableId;
            Title = title;
            Kind = kind;
            Preview = preview;
            Records = records;
            EmptyMessage = emptyMessage;
        }

        public string StableId { get; }

        public string Title { get; }

        public string Kind { get; }

        public string Preview { get; }

        public IReadOnlyList<RecordSnapshot> Records { get; }

        public string EmptyMessage { get; }

        public string ButtonText => string.IsNullOrWhiteSpace(Preview) ? Title : $"{Title}\n{Preview}";

        public string Tooltip => string.IsNullOrWhiteSpace(Kind) ? Title : $"{Title}\n{Kind}";
    }

    /// <summary>
    /// UI が描画に使う record 単位のスナップショットです。
    /// </summary>
    internal sealed class RecordSnapshot
    {
        public RecordSnapshot(string stableId, string title, string preview, string detailText, string kind)
        {
            StableId = stableId;
            Title = title;
            Preview = preview;
            DetailText = detailText;
            Kind = kind;
        }

        public string StableId { get; }

        public string Title { get; }

        public string Preview { get; }

        public string DetailText { get; }

        public string Kind { get; }

        public string ButtonText => string.IsNullOrWhiteSpace(Preview) ? Title : $"{Title}\n{Preview}";

        public string Tooltip => string.IsNullOrWhiteSpace(Kind) ? Title : $"{Title}\n{Kind}";
    }
}
#endif
