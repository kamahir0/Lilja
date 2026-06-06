#nullable enable
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lilja.Repository.Editor
{
    /// <summary>
    /// パッケージが任意導入でもエディターツールが動作できるよう、リフレクション経由で MessagePack API にアクセスします。
    /// </summary>
    internal static class MessagePackReflectionBridge
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, object?> OptionsCache = new Dictionary<string, object?>();
        private static bool probeInitialized;
        private static MessagePackCompatibilityProbe? cachedProbe;

        /// <summary>
        /// 必要な MessagePack 実行時型が利用可能かどうかを示す値を取得します。
        /// </summary>
        public static bool IsAvailable => TryCreateProbe() is not null;

        /// <summary>
        /// MessagePack が利用可能な場合に、指定されたフォーマッター型を含むシリアライザーオプションを作成します。
        /// </summary>
        /// <param name="formatterTypes">標準リゾルバーより前に登録すべきフォーマッター型。</param>
        /// <returns>構成済みのオプションオブジェクト。カスタム登録が使えない場合は標準オプション。</returns>
        public static object? CreateOptions(params Type[] formatterTypes)
        {
            var probe = TryCreateProbe();
            if (probe is null)
            {
                return null;
            }

            var key = CreateFormatterCacheKey(formatterTypes);
            lock (SyncRoot)
            {
                if (!OptionsCache.TryGetValue(key, out var options))
                {
                    options = probe.CreateOptions(formatterTypes);
                    OptionsCache[key] = options;
                }

                return options;
            }
        }

        /// <summary>
        /// MessagePack のバイト列を、要求された実行時型へ逆シリアライズします。
        /// </summary>
        /// <param name="bytes">シリアライズ済みペイロード。</param>
        /// <param name="targetType">逆シリアライズ先の実行時型。</param>
        /// <param name="options">使用するシリアライザーオプション。</param>
        /// <returns>逆シリアライズされた値。逆シリアライズできない場合は <see langword="null"/>。</returns>
        public static object? Deserialize(byte[] bytes, Type targetType, object? options)
        {
            return TryCreateProbe()?.Deserialize(bytes, targetType, options);
        }

        private static MessagePackCompatibilityProbe? TryCreateProbe()
        {
            lock (SyncRoot)
            {
                if (!probeInitialized)
                {
                    cachedProbe = MessagePackCompatibilityProbe.Create(GetCandidateAssemblies());
                    probeInitialized = true;
                }

                return cachedProbe;
            }
        }

        private static string CreateFormatterCacheKey(Type[] formatterTypes)
        {
            if (formatterTypes.Length == 0)
            {
                return string.Empty;
            }

            return string.Join("|", formatterTypes
                .Select(static type => type.AssemblyQualifiedName ?? type.FullName ?? type.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        }

        private static Assembly[] GetCandidateAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var getLoadContextMethod = FindGetLoadContextMethod();
            if (getLoadContextMethod is null)
            {
                return assemblies;
            }

            object? currentLoadContext;
            try
            {
                currentLoadContext = getLoadContextMethod.Invoke(null, new object[] { typeof(MessagePackReflectionBridge).Assembly });
            }
            catch
            {
                return assemblies;
            }

            if (currentLoadContext is null)
            {
                return assemblies;
            }

            return assemblies
                .Where(assembly => IsInSameLoadContext(assembly, getLoadContextMethod, currentLoadContext))
                .ToArray();
        }

        private static MethodInfo? FindGetLoadContextMethod()
        {
            var loadContextType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader", false);
            if (loadContextType is null)
            {
                return null;
            }

            return loadContextType.GetMethod(
                "GetLoadContext",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Assembly) },
                null);
        }

        private static bool IsInSameLoadContext(Assembly assembly, MethodInfo getLoadContextMethod, object currentLoadContext)
        {
            try
            {
                return Equals(getLoadContextMethod.Invoke(null, new object[] { assembly }), currentLoadContext);
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
