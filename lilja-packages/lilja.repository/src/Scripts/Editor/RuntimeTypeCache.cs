using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lilja.Repository.Editor
{
    internal static class RuntimeTypeCache
    {
        private static Dictionary<string, List<Type>> _typesByName;

        public static Type FindType(string typeName)
        {
            EnsureCache();
            return _typesByName.TryGetValue(typeName, out var matches) ? matches[0] : null;
        }

        public static MethodInfo FindEntityDtoKeyAccessor(string entityName, Type dtoType)
        {
            EnsureCache();
            if (!_typesByName.TryGetValue(entityName, out var matches))
            {
                return null;
            }

            foreach (var type in matches)
            {
                var method = type.GetMethod(
                    "GetKeyFromDto",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { dtoType },
                    null);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static void EnsureCache()
        {
            if (_typesByName != null)
            {
                return;
            }

            _typesByName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(type => type != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (!_typesByName.TryGetValue(type.Name, out var matches))
                    {
                        matches = new List<Type>();
                        _typesByName[type.Name] = matches;
                    }

                    matches.Add(type);
                }
            }
        }
    }
}
