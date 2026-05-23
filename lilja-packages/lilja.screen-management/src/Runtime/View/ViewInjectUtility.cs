using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// [View] 属性が付与されたフィールドに対して、コンポーネントの自動依存注入（Dependency Injection）および
    /// メモリ解放のための null クリアを行うユーティリティ静的クラス。
    /// </summary>
    internal static class ViewInjectUtility
    {
        // リフレクションの走査コストを抑えるため、型ごとの[View]フィールドリストをキャッシュします
        private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache = new();

        /// <summary>
        /// ビューアセット内のコンポーネントを、対象画面オブジェクトの [View] フィールドに注入します。
        /// </summary>
        /// <param name="target">注入対象 of 画面オブジェクト</param>
        /// <param name="rootObjects">ロードされたビューのルートGameObject配列</param>
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

                    // 非アクティブなGameObject内のコンポーネントも対象にするため includeInactive: true
                    var component = root.GetComponentInChildren(field.FieldType, true);

                    if (component != null)
                    {
                        field.SetValue(target, component);
                        break; // このフィールドに対する注入は完了
                    }
                }
            }
        }

        /// <summary>
        /// 注入された [View] フィールドの参照をすべて null でクリアし、強参照によるメモリリークを完全に防止します。
        /// </summary>
        /// <param name="target">対象の画面オブジェクト</param>
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

        /// <summary>
        /// 指定された型の継承階層をすべてたどり、[View] 属性を持つフィールドをキャッシュから（なければリフレクションで）取得します。
        /// </summary>
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

                // object に到達するまで親クラスを遡って declared フィールドを検索
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
