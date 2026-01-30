using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// UnityView の注入と解放を行うユーティリティ
    /// </summary>
    internal static class UnityViewUtility
    {
        /// <summary>
        /// View を注入します
        /// </summary>
        /// <param name="target">注入対象のオブジェクト</param>
        /// <param name="rootObjects">対象のルートGameObject配列</param>
        /// <param name="unityViewFields">キャッシュされたフィールド情報</param>
        public static void Inject(ScreenBase target, GameObject[] rootObjects, List<FieldInfo> unityViewFields)
        {
            foreach (var unityViewField in unityViewFields)
            {
                foreach (var rootObject in rootObjects)
                {
                    var unityView = rootObject.GetComponentInChildren(unityViewField.FieldType, true);

                    if (unityView != null)
                    {
                        // View を注入
                        unityViewField.SetValue(target, unityView);
                        break; // 見つかったらこのフィールドの検索は終了
                    }
                }
            }
        }

        /// <summary>
        /// 注入された View を null で上書きします
        /// </summary>
        /// <param name="target">対象のオブジェクト</param>
        /// <param name="unityViewFields">キャッシュされたフィールド情報</param>
        public static void Nullify(ScreenBase target, List<FieldInfo> unityViewFields)
        {
            foreach (var unityViewField in unityViewFields)
            {
                // null を代入
                unityViewField.SetValue(target, null);
            }
        }

        /// <summary>
        /// 継承階層をたどって [UnityView] 属性を持つ全てのフィールドを取得する
        /// </summary>
        public static List<FieldInfo> GetFields(Type type)
        {
            var fields = new List<FieldInfo>();
            var currentType = type;

            while (currentType != null && currentType != typeof(object))
            {
                var declaredFields = currentType.GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

                foreach (var field in declaredFields)
                {
                    if (field.GetCustomAttribute<UnityViewAttribute>() != null)
                    {
                        fields.Add(field);
                    }
                }

                currentType = currentType.BaseType;
            }

            return fields;
        }
    }
}
