using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lilja.Repository.Internal
{
    /// <summary>
    /// 開発ビルドで生存中の実行時インスタンスを追跡し、意図しない重複を可視化します。
    /// </summary>
    internal static class RuntimeInstanceMonitor
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<WeakReference> TxManagers = new List<WeakReference>();
        private static readonly Dictionary<string, List<WeakReference>> PersistedRepositories = new Dictionary<string, List<WeakReference>>();

        /// <summary>
        /// <see cref="TxManager"/> インスタンスを追跡し、複数が生存している場合は警告します。
        /// </summary>
        /// <param name="instance">登録するインスタンス。</param>
        public static void TrackTxManager(object instance)
        {
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (SyncRoot)
            {
                Cleanup(TxManagers);
                if (HasLiveInstance(TxManagers))
                {
                    Debug.LogWarning("Multiple live TxManager instances were detected.");
                }

                TxManagers.Add(new WeakReference(instance));
            }
    #endif
        }

        /// <summary>
        /// 永続化リポジトリインスタンスを追跡し、同じ保存先が複数回開かれた場合は警告します。
        /// </summary>
        /// <param name="repositoryType">実行時のリポジトリ型。</param>
        /// <param name="filePath">リポジトリが使う保存先パス。</param>
        /// <param name="instance">登録するインスタンス。</param>
        public static void TrackPersistedRepository(Type repositoryType, string filePath, object instance)
        {
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (SyncRoot)
            {
                var key = GetRepositoryKey(repositoryType, filePath);
                if (!PersistedRepositories.TryGetValue(key, out var references))
                {
                    references = new List<WeakReference>();
                    PersistedRepositories.Add(key, references);
                }

                Cleanup(references);
                if (HasLiveInstance(references))
                {
                    Debug.LogWarning($"Multiple live persisted repository instances were detected for {repositoryType.FullName} at {filePath}.");
                }

                references.Add(new WeakReference(instance));
            }
    #endif
        }

        private static string GetRepositoryKey(Type repositoryType, string filePath)
        {
            var normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
            return $"{repositoryType.FullName}:{normalizedPath}";
        }

        private static bool HasLiveInstance(List<WeakReference> references)
        {
            foreach (var reference in references)
            {
                if (reference.IsAlive)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Cleanup(List<WeakReference> references)
        {
            for (var index = references.Count - 1; index >= 0; index--)
            {
                if (!references[index].IsAlive)
                {
                    references.RemoveAt(index);
                }
            }
        }
    }
}
