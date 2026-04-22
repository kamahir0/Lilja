#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Lilja.Repository.Diagnostics
{
/// <summary>
/// Tracks live repository instances so editor tooling can inspect them during play mode.
/// </summary>
public static class RepositoryTracker
{
    private static readonly object SyncRoot = new object();
    private static readonly Dictionary<RepositoryType, List<WeakReference>> Repositories = new Dictionary<RepositoryType, List<WeakReference>>
    {
        { RepositoryType.InMemory, new List<WeakReference>() },
        { RepositoryType.Json, new List<WeakReference>() },
        { RepositoryType.MessagePack, new List<WeakReference>() },
    };

    /// <summary>
    /// Identifies the backing storage strategy used by a repository.
    /// </summary>
    public enum RepositoryType
    {
        InMemory,
        Json,
        MessagePack,
    }

    /// <summary>
    /// Registers a repository instance for diagnostics.
    /// </summary>
    /// <param name="repository">The repository instance to track.</param>
    /// <param name="type">The repository storage type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
    public static void Track(object repository, RepositoryType type)
    {
        if (repository is null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        lock (SyncRoot)
        {
            var references = Repositories[type];
            Cleanup(references);
            references.Add(new WeakReference(repository));
        }
    }

    /// <summary>
    /// Returns all currently live repository instances for the requested storage type.
    /// </summary>
    /// <param name="type">The repository storage type.</param>
    /// <returns>A snapshot of live repository instances.</returns>
    public static IEnumerable<object> GetAll(RepositoryType type)
    {
        lock (SyncRoot)
        {
            var liveObjects = new List<object>();
            foreach (var reference in Repositories[type])
            {
                if (reference.Target is object repository)
                {
                    liveObjects.Add(repository);
                }
            }

            return liveObjects;
        }
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
#endif
