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
using System.Threading;
using Lilja.Repository.Diagnostics;
using UnityEngine;

namespace Lilja.Repository.Editor
{
    internal sealed class RepositoryViewerDataSource
    {
        private const int PreviewLimit = 120;
        private static readonly string[] FallbackKeyMemberNames = { "Id", "Key", "Name", "id", "key", "name" };
        private static readonly string[] SystemAssemblyPrefixes =
        {
            "System",
            "mscorlib",
            "Unity",
            "UnityEngine",
            "UnityEditor",
            "Mono.",
            "Newtonsoft",
            "Microsoft",
            "nunit",
            "Gradle",
            "ExCSS",
            "JetBrains",
            "log4net",
            "Bee.",
            "ReportGenerator",
            "roslyn",
        };
        private static Type[]? cachedDtoFormatterTypes;

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

        public IReadOnlyList<RepositorySnapshot> LoadRepositories(RepositoryTracker.RepositoryType repositoryType)
        {
            return Application.isPlaying
                ? LoadLiveRepositories(repositoryType)
                : LoadPersistedRepositories(repositoryType);
        }

        public string GetReloadFingerprint(RepositoryTracker.RepositoryType repositoryType)
        {
            return Application.isPlaying
                ? $"play:{repositoryType}:{RepositoryTracker.GetVersion(repositoryType)}"
                : $"edit:{repositoryType}:{GetPersistedFingerprint(repositoryType)}";
        }

        public string LoadRecordDetail(RecordSnapshot record)
        {
            if (!record.HasLazyDetail)
            {
                return record.DetailText;
            }

            try
            {
                var detail = LoadPersistedRecordDetail(record.PersistedReference!);
                record.SetLoadedDetail(detail);
                return record.DetailText;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                var detail = ex.Message;
                record.SetLoadedDetail(detail);
                return detail;
            }
        }

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
            var stateSnapshots = RepositoryTracker.GetSnapshots(repositoryType);
            return stateSnapshots.Select(CreateRepositorySnapshot).ToList();
        }

        private static RepositorySnapshot CreateRepositorySnapshot(RepositoryTracker.RepositoryStateSnapshot state)
        {
            if (state.IsKeyed)
            {
                var records = state.Records
                    .Select(record => new RecordSnapshot(
                        $"keyed:{EscapeString(record.Key)}",
                        record.Key,
                        BuildValuePreview(record.Value),
                        FormatDetail(record.Value),
                        record.Value?.GetType().FullName ?? "null"))
                    .ToList();
                return new RepositorySnapshot(
                    state.StableId,
                    state.DisplayName,
                    state.StorageIdentifier,
                    BuildCountSummary(records.Count),
                    records,
                    "No records found.");
            }

            var singletonRecords = BuildSingletonRecords(state.Value, state.DisplayName);
            return new RepositorySnapshot(
                state.StableId,
                state.DisplayName,
                state.StorageIdentifier,
                singletonRecords.Count > 0 ? "1 value" : "No value",
                singletonRecords,
                "No record found.");
        }

        private static IReadOnlyList<RepositorySnapshot> LoadPersistedRepositories(RepositoryTracker.RepositoryType repositoryType)
        {
            if (repositoryType == RepositoryTracker.RepositoryType.InMemory)
            {
                return Array.Empty<RepositorySnapshot>();
            }

            Directory.CreateDirectory(Application.persistentDataPath);
            var extension = repositoryType == RepositoryTracker.RepositoryType.Json ? ".json" : ".msgpack";
            var repositories = new List<RepositorySnapshot>();

            foreach (var file in Directory.GetFiles(Application.persistentDataPath, "*" + extension, SearchOption.TopDirectoryOnly)
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                repositories.Add(LoadSingletonRepository(file, repositoryType));
            }

            foreach (var directory in Directory.GetDirectories(Application.persistentDataPath)
                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.EnumerateFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly).Any())
                {
                    repositories.Add(LoadKeyedRepository(directory, repositoryType));
                }
            }

            return repositories;
        }

        private static RepositorySnapshot LoadSingletonRepository(string filePath, RepositoryTracker.RepositoryType repositoryType)
        {
            var storageIdentifier = Path.GetFileNameWithoutExtension(filePath);
            var stableId = NormalizePath(filePath);
            var metadata = RepositoryMetadata.Resolve(storageIdentifier);
            var title = storageIdentifier;
            var kind = Path.GetFileName(filePath);

            try
            {
                if (metadata is null)
                {
                    return LoadUnknownFile(filePath, stableId, title, kind, repositoryType);
                }

                var records = new[]
                {
                    CreatePersistedRecord(
                        filePath,
                        repositoryType,
                        storageIdentifier,
                        "singleton",
                        metadata.EntityType.Name,
                        metadata.EntityType.FullName ?? metadata.EntityType.Name),
                };
                return new RepositorySnapshot(
                    stableId,
                    title,
                    metadata.EntityType.FullName ?? metadata.EntityType.Name,
                    records.Length > 0 ? records[0].Preview : "No value",
                    records,
                    "No record found.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                return CreateErrorRepositorySnapshot(stableId, title, kind, ex.Message);
            }
        }

        private static RepositorySnapshot LoadKeyedRepository(string directoryPath, RepositoryTracker.RepositoryType repositoryType)
        {
            var storageIdentifier = Path.GetFileName(directoryPath);
            var stableId = NormalizePath(directoryPath);
            var metadata = RepositoryMetadata.Resolve(storageIdentifier);
            var extension = repositoryType == RepositoryTracker.RepositoryType.Json ? ".json" : ".msgpack";

            try
            {
                if (metadata is null)
                {
                    var unknownRecords = Directory.GetFiles(directoryPath, "*" + extension, SearchOption.TopDirectoryOnly)
                        .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                        .Select(file => CreateFileRecord(file, repositoryType))
                        .ToList();
                    return new RepositorySnapshot(stableId, storageIdentifier, "Unknown", BuildCountSummary(unknownRecords.Count), unknownRecords, "No records found.");
                }

                var records = Directory.GetFiles(directoryPath, "*" + extension, SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .Select(file => CreatePersistedRecord(
                        file,
                        repositoryType,
                        storageIdentifier,
                        $"file:{NormalizePath(file)}",
                        Path.GetFileNameWithoutExtension(file),
                        metadata.EntityType.FullName ?? metadata.EntityType.Name))
                    .ToList();
                return new RepositorySnapshot(
                    stableId,
                    storageIdentifier,
                    metadata.EntityType.FullName ?? metadata.EntityType.Name,
                    BuildCountSummary(records.Count),
                    records,
                    "No records found.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                return CreateErrorRepositorySnapshot(stableId, storageIdentifier, storageIdentifier, ex.Message);
            }
        }

        private static RepositorySnapshot LoadUnknownFile(string filePath, string stableId, string title, string kind, RepositoryTracker.RepositoryType repositoryType)
        {
            var records = new[] { CreateFileRecord(filePath, repositoryType) };
            return new RepositorySnapshot(stableId, title, "Unknown", records[0].Preview, records, "No records found.");
        }

        private static RecordSnapshot CreateFileRecord(string filePath, RepositoryTracker.RepositoryType repositoryType)
        {
            var title = Path.GetFileNameWithoutExtension(filePath);
            return CreatePersistedRecord(
                filePath,
                repositoryType,
                Path.GetFileNameWithoutExtension(filePath),
                $"file:{NormalizePath(filePath)}",
                title,
                Path.GetFileName(filePath));
        }

        private static RecordSnapshot CreatePersistedRecord(
            string filePath,
            RepositoryTracker.RepositoryType repositoryType,
            string storageIdentifier,
            string stableId,
            string title,
            string kind)
        {
            var preview = FormatFilePreview(filePath);
            var detail = $"Select to load detail.\n{preview}\n{NormalizePath(filePath)}";
            var reference = new PersistedRecordReference(filePath, repositoryType, storageIdentifier);
            return new RecordSnapshot(stableId, title, preview, detail, kind, reference);
        }

        private static object? LoadDtoFile(string filePath, RepositoryMetadata metadata, RepositoryTracker.RepositoryType repositoryType)
        {
            if (repositoryType == RepositoryTracker.RepositoryType.Json)
            {
                var raw = File.ReadAllText(filePath);
                return string.IsNullOrWhiteSpace(raw) ? null : JsonUtility.FromJson(raw, metadata.DtoType);
            }

            if (!MessagePackReflectionBridge.IsAvailable)
            {
                return null;
            }

            var options = MessagePackReflectionBridge.CreateOptions(GetAllDtoFormatterTypes());
            return MessagePackReflectionBridge.Deserialize(File.ReadAllBytes(filePath), metadata.DtoType, options);
        }

        private static string LoadPersistedRecordDetail(PersistedRecordReference reference)
        {
            var metadata = RepositoryMetadata.Resolve(reference.StorageIdentifier);
            if (metadata is null)
            {
                if (reference.RepositoryType == RepositoryTracker.RepositoryType.Json)
                {
                    return File.Exists(reference.FilePath) ? File.ReadAllText(reference.FilePath) : "File was not found.";
                }

                return MessagePackReflectionBridge.IsAvailable
                    ? "Binary Data\nMetadata could not be resolved for this MessagePack file."
                    : "Binary Data\nMessagePack runtime was not found in the current project.";
            }

            var dto = LoadDtoFile(reference.FilePath, metadata, reference.RepositoryType);
            return FormatDetail(dto);
        }

        private static string GetPersistedFingerprint(RepositoryTracker.RepositoryType repositoryType)
        {
            if (repositoryType == RepositoryTracker.RepositoryType.InMemory)
            {
                return "inmemory";
            }

            var root = Application.persistentDataPath;
            if (!Directory.Exists(root))
            {
                return "missing";
            }

            var extension = repositoryType == RepositoryTracker.RepositoryType.Json ? ".json" : ".msgpack";
            var parts = Directory.EnumerateFiles(root, "*" + extension, SearchOption.AllDirectories)
                .OrderBy(NormalizePath, StringComparer.Ordinal)
                .Select(static file =>
                {
                    var info = new FileInfo(file);
                    return $"{NormalizePath(file)}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
                });
            return string.Join("|", parts);
        }

        private static Type[] GetAllDtoFormatterTypes()
        {
            if (cachedDtoFormatterTypes is not null)
            {
                return cachedDtoFormatterTypes;
            }

            cachedDtoFormatterTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(static assembly =>
                {
                    var name = assembly.GetName().Name;
                    if (string.IsNullOrEmpty(name))
                    {
                        return false;
                    }
                    foreach (var prefix in SystemAssemblyPrefixes)
                    {
                        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                    return true;
                })
                .SelectMany(GetTypesSafe)
                .Where(static type =>
                    type.FullName is not null &&
                    type.FullName.StartsWith("Lilja.Repository.Generated.Formatters.", StringComparison.Ordinal) &&
                    type.Name.EndsWith("DtoFormatter", StringComparison.Ordinal))
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            return cachedDtoFormatterTypes;
        }

        private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(static type => type is not null)!;
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static RepositorySnapshot CreateErrorRepositorySnapshot(string stableId, string title, string kind, string message)
        {
            var records = CreateInfoRecords("Error", "Error", message);
            return new RepositorySnapshot(stableId, title, kind, message, records, "No records found.");
        }

        private static IReadOnlyList<RecordSnapshot> BuildKeyedRecords(IReadOnlyList<object?> items, Func<object?, string> keySelector)
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
                records.Add(new RecordSnapshot($"keyed:{EscapeString(title)}", title, preview, FormatDetail(item), item?.GetType().FullName ?? "null"));
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
                new RecordSnapshot("singleton", title, BuildValuePreview(value), FormatDetail(value), value.GetType().FullName ?? value.GetType().Name),
            };
        }

        private static IReadOnlyList<RecordSnapshot> CreateInfoRecords(string title, string preview, string detailText)
        {
            return new[]
            {
                new RecordSnapshot(title, title, preview, detailText, title),
            };
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

        private static string GetFallbackKey(object? item)
        {
            if (item is null)
            {
                return "null";
            }

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

        private static string FormatFilePreview(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return "Missing file";
            }

            var info = new FileInfo(filePath);
            return $"{info.Length} bytes / {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
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

            return TryFormatScalar(value, false, out var formatted)
                ? formatted
                : value.ToString() ?? value.GetType().Name;
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

            if (!value.GetType().IsValueType && !visited.Add(value))
            {
                return "\"<cyclic reference>\"";
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
            members.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(static field => !field.IsStatic).Cast<MemberInfo>());
            members.AddRange(type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(static property => property.CanRead && property.GetIndexParameters().Length == 0).Cast<MemberInfo>());
            return members.OrderBy(static member => member.Name, StringComparer.Ordinal).ToList();
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
            return text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        }

        private static string NormalizePath(string filePath)
        {
            return Path.GetFullPath(filePath).Replace('\\', '/');
        }

        private sealed class RepositoryMetadata
        {
            private RepositoryMetadata(string storageIdentifier, Type entityType, Type dtoType, Type dtoFormatterType)
            {
                StorageIdentifier = storageIdentifier;
                EntityType = entityType;
                DtoType = dtoType;
                DtoFormatterType = dtoFormatterType;
                GetKeyFromDtoMethod = FindGetKeyFromDtoMethod(entityType, dtoType);
            }

            public string StorageIdentifier { get; }

            public Type EntityType { get; }

            public Type DtoType { get; }

            public Type DtoFormatterType { get; }

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
                var formatterNamespace = string.IsNullOrEmpty(entityNamespace) ? "Lilja.Repository.Generated.Formatters" : $"Lilja.Repository.Generated.Formatters.{entityNamespace}";
                var dtoType = FindType($"{dtoNamespace}.{entityName}Dto");
                var dtoFormatterType = FindType($"{formatterNamespace}.{entityName}DtoFormatter") ?? typeof(object);
                return entityType is null || dtoType is null
                    ? null
                    : new RepositoryMetadata(storageIdentifier, entityType, dtoType, dtoFormatterType);
            }

            private static MethodInfo? FindGetKeyFromDtoMethod(Type entityType, Type dtoType)
            {
                return entityType
                    .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method =>
                        method.Name == "__RepositoryGetKeyFromDto" &&
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

    internal sealed class RepositorySnapshot
    {
        public RepositorySnapshot(string stableId, string title, string kind, string preview, IReadOnlyList<RecordSnapshot> records, string emptyMessage)
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

        public string Tooltip => string.IsNullOrWhiteSpace(Kind) ? Title : $"{Title}\n{Kind}";
    }

    internal sealed class RecordSnapshot
    {
        private string? loadedDetailText;

        public RecordSnapshot(
            string stableId,
            string title,
            string preview,
            string detailText,
            string kind,
            PersistedRecordReference? persistedReference = null)
        {
            StableId = stableId;
            Title = title;
            Preview = preview;
            InitialDetailText = detailText;
            Kind = kind;
            PersistedReference = persistedReference;
        }

        public string StableId { get; }

        public string Title { get; }

        public string Preview { get; }

        public string DetailText => loadedDetailText ?? InitialDetailText;

        private string InitialDetailText { get; }

        public string Kind { get; }

        public PersistedRecordReference? PersistedReference { get; }

        public bool HasLazyDetail => PersistedReference is not null && loadedDetailText is null;

        public string Tooltip => string.IsNullOrWhiteSpace(Kind) ? Title : $"{Title}\n{Kind}";

        public void SetLoadedDetail(string detailText)
        {
            loadedDetailText = detailText;
        }
    }

    internal sealed class PersistedRecordReference
    {
        public PersistedRecordReference(string filePath, RepositoryTracker.RepositoryType repositoryType, string storageIdentifier)
        {
            FilePath = filePath;
            RepositoryType = repositoryType;
            StorageIdentifier = storageIdentifier;
        }

        public string FilePath { get; }

        public RepositoryTracker.RepositoryType RepositoryType { get; }

        public string StorageIdentifier { get; }
    }
}
#endif
