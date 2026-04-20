using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lilja.Repository.Editor
{
    internal static class RuntimeTypeCache
    {
        internal sealed class PersistedTypeMetadata
        {
            public Type? DtoType { get; set; }

            public Type? EnvelopeType { get; set; }

            public Type? DtoFormatterType { get; set; }

            public Type? EnvelopeFormatterType { get; set; }

            public MethodInfo? EntityDtoKeyAccessor { get; set; }
        }

        private const string DtoNamespacePrefix = "Lilja.Repository.Generated.Dtos";
        private const string StorageNamespacePrefix = "Lilja.Repository.Generated.Storage";
        private const string FormatterNamespacePrefix = "Lilja.Repository.Generated.Formatters";

        private static Dictionary<string, List<Type>>? _typesByName;
        private static Dictionary<string, PersistedTypeMetadata>? _persistedTypesByStorageIdentifier;

        public static Type? FindType(string typeName)
        {
            EnsureCache();
            return _typesByName!.TryGetValue(typeName, out var matches) ? matches[0] : null;
        }

        public static PersistedTypeMetadata? FindPersistedTypeMetadata(string storageIdentifier)
        {
            EnsureCache();
            return _persistedTypesByStorageIdentifier!.TryGetValue(storageIdentifier, out var metadata) ? metadata : null;
        }

        private static void EnsureCache()
        {
            if (_typesByName != null)
            {
                return;
            }

            _typesByName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
            _persistedTypesByStorageIdentifier = new Dictionary<string, PersistedTypeMetadata>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(type => type != null).Select(type => type!).ToArray();
                }

                foreach (var type in types)
                {
                    if (!_typesByName.TryGetValue(type.Name, out var matches))
                    {
                        matches = new List<Type>();
                        _typesByName[type.Name] = matches;
                    }

                    matches.Add(type);
                    RegisterPersistedType(type);
                }
            }

            foreach (var entry in _persistedTypesByStorageIdentifier)
            {
                if (entry.Value.DtoType == null)
                {
                    continue;
                }

                if (!_typesByName.TryGetValue(GetEntityClassName(entry.Key), out var matches))
                {
                    continue;
                }

                foreach (var type in matches)
                {
                    if (GetStorageIdentifier(type) != entry.Key)
                    {
                        continue;
                    }

                    var method = type.GetMethod(
                        "GetKeyFromDto",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] { entry.Value.DtoType },
                        null);
                    if (method != null)
                    {
                        entry.Value.EntityDtoKeyAccessor = method;
                        break;
                    }
                }
            }
        }

        private static void RegisterPersistedType(Type type)
        {
            if (TryGetStorageIdentifierFromGeneratedType(type, DtoNamespacePrefix, "Dto", out var dtoStorageIdentifier))
            {
                GetOrAddPersistedTypeMetadata(dtoStorageIdentifier).DtoType = type;
                return;
            }

            if (TryGetStorageIdentifierFromGeneratedType(type, StorageNamespacePrefix, "StorageEnvelope", out var envelopeStorageIdentifier))
            {
                GetOrAddPersistedTypeMetadata(envelopeStorageIdentifier).EnvelopeType = type;
                return;
            }

            if (TryGetStorageIdentifierFromGeneratedType(type, FormatterNamespacePrefix, "DtoFormatter", out var dtoFormatterStorageIdentifier))
            {
                GetOrAddPersistedTypeMetadata(dtoFormatterStorageIdentifier).DtoFormatterType = type;
                return;
            }

            if (TryGetStorageIdentifierFromGeneratedType(type, FormatterNamespacePrefix, "StorageEnvelopeFormatter", out var envelopeFormatterStorageIdentifier))
            {
                GetOrAddPersistedTypeMetadata(envelopeFormatterStorageIdentifier).EnvelopeFormatterType = type;
            }
        }

        private static PersistedTypeMetadata GetOrAddPersistedTypeMetadata(string storageIdentifier)
        {
            if (!_persistedTypesByStorageIdentifier!.TryGetValue(storageIdentifier, out var metadata))
            {
                metadata = new PersistedTypeMetadata();
                _persistedTypesByStorageIdentifier[storageIdentifier] = metadata;
            }

            return metadata;
        }

        private static bool TryGetStorageIdentifierFromGeneratedType(
            Type type,
            string namespacePrefix,
            string typeSuffix,
            out string storageIdentifier)
        {
            storageIdentifier = string.Empty;

            if (type == null || string.IsNullOrEmpty(type.Name) || !type.Name.EndsWith(typeSuffix, StringComparison.Ordinal))
            {
                return false;
            }

            var typeNamespace = type.Namespace;
            if (typeNamespace == null)
            {
                return false;
            }

            var className = type.Name.Substring(0, type.Name.Length - typeSuffix.Length);
            if (typeNamespace == namespacePrefix)
            {
                storageIdentifier = className;
                return true;
            }

            if (!typeNamespace.StartsWith(namespacePrefix + ".", StringComparison.Ordinal))
            {
                return false;
            }

            storageIdentifier = typeNamespace.Substring(namespacePrefix.Length + 1) + "." + className;
            return true;
        }

        private static string GetEntityClassName(string storageIdentifier)
        {
            var separatorIndex = storageIdentifier.LastIndexOf('.');
            return separatorIndex < 0
                ? storageIdentifier
                : storageIdentifier.Substring(separatorIndex + 1);
        }

        private static string? GetStorageIdentifier(Type? type)
        {
            if (type == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(type.Namespace)
                ? type.Name
                : type.Namespace + "." + type.Name;
        }
    }
}
