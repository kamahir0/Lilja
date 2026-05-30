using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// ロードされたビュー（GameObjectツリー）から、画面オブジェクト（GameScreenBase）の [View] 属性付きフィールドまたはプロパティへ参照をリフレクションを用いて動的自動注入・ null クリアするユーティリティクラス。
    /// </summary>
    public static class ViewInjectUtility
    {
        private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache =
            new Dictionary<Type, List<FieldInfo>>();
        private static readonly Dictionary<Type, List<PropertyInfo>> _propertyCache =
            new Dictionary<Type, List<PropertyInfo>>();

        /// <summary>
        /// 指定されたターゲットオブジェクトの [View] 属性付きフィールドおよびプロパティに対して、ルートオブジェクト配下の適合するコンポーネント参照を自動注入（インジェクション）します。
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
            var properties = GetProperties(type);

            // フィールドへのインジェクション
            foreach (var field in fields)
            {
                var component = default(Component);
                foreach (var root in rootObjects)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    component = root.GetComponentInChildren(field.FieldType, true);
                    if (component != null)
                    {
                        field.SetValue(target, component);
                        break;
                    }
                }

                if (component == null)
                {
                    Debug.LogWarning(
                        $"[Lilja.ScreenManagement] [View] 注入失敗: ターゲット '{type.Name}' のフィールド '{field.Name}' (型: '{field.FieldType.Name}') に適合するコンポーネントがビューオブジェクト内に見つかりません。"
                    );
                }
            }

            // プロパティへのインジェクション
            foreach (var property in properties)
            {
                var component = default(Component);
                foreach (var root in rootObjects)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    component = root.GetComponentInChildren(property.PropertyType, true);
                    if (component != null)
                    {
                        property.SetValue(target, component, null);
                        break;
                    }
                }

                if (component == null)
                {
                    Debug.LogWarning(
                        $"[Lilja.ScreenManagement] [View] 注入失敗: ターゲット '{type.Name}' のプロパティ '{property.Name}' (型: '{property.PropertyType.Name}') に適合するコンポーネントがビューオブジェクト内に見つかりません。"
                    );
                }
            }
        }

        /// <summary>
        /// 指定されたターゲットオブジェクトのすべての [View] 属性付きフィールドおよびプロパティの参照を null でクリアし、メモリリークを防止します。
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

            var properties = GetProperties(type);
            foreach (var property in properties)
            {
                property.SetValue(target, null, null);
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

        private static List<PropertyInfo> GetProperties(Type type)
        {
            lock (_propertyCache)
            {
                if (_propertyCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                var list = new List<PropertyInfo>();
                var currentType = type;

                while (currentType != null && currentType != typeof(object))
                {
                    var declaredProperties = currentType.GetProperties(
                        BindingFlags.Instance
                            | BindingFlags.NonPublic
                            | BindingFlags.Public
                            | BindingFlags.DeclaredOnly
                    );

                    foreach (var property in declaredProperties)
                    {
                        if (property.GetCustomAttribute<ViewAttribute>() != null)
                        {
                            // 値の書き込み（代入）が可能なプロパティのみ登録
                            if (property.CanWrite)
                            {
                                list.Add(property);
                            }
                        }
                    }

                    currentType = currentType.BaseType;
                }

                _propertyCache[type] = list;
                return list;
            }
        }
    }
}
