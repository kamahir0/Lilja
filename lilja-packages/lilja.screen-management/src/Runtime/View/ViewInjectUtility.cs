using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lilja.ScreenManagement
{

    internal static class ViewInjectUtility
    {

        private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache = new();

        public static void Inject(object target, GameObject[] rootObjects)
        {
            if (target == null || rootObjects == null || rootObjects.Length == 0)
            {
                return;
            }

            var type = target.GetType();
            var fields = GetFields(type);

            foreach (var field in fields)
            {
                foreach (var root in rootObjects)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    var component = root.GetComponentInChildren(field.FieldType, true);

                    if (component != null)
                    {
                        field.SetValue(target, component);
                        break;
                    }
                }
            }
        }

        public static void Nullify(object target)
        {
            if (target == null)
            {
                return;
            }

            var type = target.GetType();
            var fields = GetFields(type);

            foreach (var field in fields)
            {
                field.SetValue(target, null);
            }
        }

        private static List<FieldInfo> GetFields(Type type)
        {
            lock (_fieldCache)
            {
                if (_fieldCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                var list = new List<FieldInfo>();
                var currentType = type;

                while (currentType != null && currentType != typeof(object))
                {
                    var declaredFields = currentType.GetFields(
                        BindingFlags.Instance
                            | BindingFlags.NonPublic
                            | BindingFlags.Public
                            | BindingFlags.DeclaredOnly
                    );

                    foreach (var field in declaredFields)
                    {
                        if (field.GetCustomAttribute<ViewAttribute>() != null)
                        {
                            list.Add(field);
                        }
                    }

                    currentType = currentType.BaseType;
                }

                _fieldCache[type] = list;
                return list;
            }
        }
    }
}
