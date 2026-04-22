using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lilja.Repository.Internal
{
/// <summary>
/// Tracks live runtime instances in development builds to surface accidental duplication.
/// </summary>
internal static class RuntimeInstanceMonitor
{
    private static readonly object SyncRoot = new object();
    private static readonly List<WeakReference> TxManagers = new List<WeakReference>();
    private static readonly Dictionary<string, List<WeakReference>> PersistedRepositories = new Dictionary<string, List<WeakReference>>();

    /// <summary>
    /// Tracks a <see cref="TxManager"/> instance and warns when more than one is live.
    /// </summary>
    /// <param name="instance">The instance to register.</param>
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
    /// Tracks a persisted repository instance and warns when the same store is opened multiple times.
    /// </summary>
    /// <param name="repositoryType">The runtime repository type.</param>
    /// <param name="filePath">The storage path used by the repository.</param>
    /// <param name="instance">The instance to register.</param>
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
