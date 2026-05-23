using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// ロードされたビュー（GameObjectツリー）から、画面オブジェクト（GameScreenBase）の [View] 属性付きフィールドへ参照をリフレクションを用いて動的自動注入・ null クリアするユーティリティクラス。
    /// </summary>
    internal static class ViewInjectUtility
    {
        private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache = new();

        /// <summary>
        /// 指定されたターゲットオブジェクトの [View] 属性付きフィールドに対して、ルートオブジェクト配下の適合するコンポーネント参照を自動注入（インジェクション）します。
        /// </summary>
        /// <param name="target">注入対象の画面オブジェクト</param>
        /// <param name="rootObjects">検索範囲となるビューのルート GameObject 群</param>
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

        /// <summary>
        /// 指定されたターゲットオブジェクトのすべての [View] 属性付きフィールドの参照を null でクリアし、メモリリークを防止します。
        /// </summary>
        /// <param name="target">クリア対象の画面オブジェクト</param>
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
