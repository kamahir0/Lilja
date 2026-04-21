using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lilja.Repository.Diagnostics
{
    /// <summary>
    /// 正常系では 1 instance を前提とする runtime コンポーネントの重複生成を検知する。
    /// 重複は例外ではなく warning で通知する。
    /// </summary>
    internal static class RuntimeInstanceMonitor
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<WeakReference<object>> TxManagers = new List<WeakReference<object>>();
        private static readonly Dictionary<string, List<WeakReference<object>>> PersistedRepositories =
            new Dictionary<string, List<WeakReference<object>>>(StringComparer.Ordinal);
        private static bool _txManagerWarningIssued;
        private static readonly HashSet<string> WarnedRepositoryKeys = new HashSet<string>(StringComparer.Ordinal);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void TrackTxManager(object txManager)
        {
            if (txManager == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                CleanupList(TxManagers);
                if (HasAnyLiveReference(TxManagers) && !_txManagerWarningIssued)
                {
                    _txManagerWarningIssued = true;
                    Debug.LogWarning(
                        "Multiple TxManager instances were detected. Lilja.Repository assumes a single TxManager in the normal runtime path; multiple instances are unsupported outside debugging scenarios.");
                }

                TxManagers.Add(new WeakReference<object>(txManager));
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal static void TrackPersistedRepository(object repository, string filePath)
        {
            if (repository == null || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var repositoryType = repository.GetType().FullName ?? repository.GetType().Name;
            var normalizedPath = Path.GetFullPath(filePath);
            var key = $"{repositoryType}|{normalizedPath}";

            lock (SyncRoot)
            {
                if (!PersistedRepositories.TryGetValue(key, out var entries))
                {
                    entries = new List<WeakReference<object>>();
                    PersistedRepositories[key] = entries;
                }

                CleanupList(entries);
                if (HasAnyLiveReference(entries) && WarnedRepositoryKeys.Add(key))
                {
                    Debug.LogWarning(
                        $"Multiple persisted repository instances were detected for '{repositoryType}' at '{normalizedPath}'. Lilja.Repository assumes one live persisted repository per file path in the normal runtime path; duplicate instances are unsupported outside debugging scenarios.");
                }

                entries.Add(new WeakReference<object>(repository));
            }
        }

        private static bool HasAnyLiveReference(List<WeakReference<object>> references)
        {
            foreach (var reference in references)
            {
                if (reference.TryGetTarget(out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CleanupList(List<WeakReference<object>> references)
        {
            references.RemoveAll(reference => !reference.TryGetTarget(out _));
        }

        internal static void ResetForTests()
        {
            lock (SyncRoot)
            {
                TxManagers.Clear();
                PersistedRepositories.Clear();
                WarnedRepositoryKeys.Clear();
                _txManagerWarningIssued = false;
            }
        }
    }
}
