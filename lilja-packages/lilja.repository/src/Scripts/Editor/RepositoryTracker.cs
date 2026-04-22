#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Lilja.Repository.Diagnostics
{
    /// <summary>
    /// プレイモード中にエディターツールから確認できるよう、生存中のリポジトリインスタンスを追跡します。
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
        /// リポジトリが使用するバックエンドの保存方式を識別します。
        /// </summary>
        public enum RepositoryType
        {
            InMemory,
            Json,
            MessagePack,
        }

        /// <summary>
        /// 診断用にリポジトリインスタンスを登録します。
        /// </summary>
        /// <param name="repository">追跡するリポジトリインスタンス。</param>
        /// <param name="type">リポジトリの保存方式。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repository"/> が <see langword="null"/> です。</exception>
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
        /// 指定された保存方式に属する、現在生存中のすべてのリポジトリインスタンスを返します。
        /// </summary>
        /// <param name="type">リポジトリの保存方式。</param>
        /// <returns>生存中のリポジトリインスタンスのスナップショット。</returns>
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
