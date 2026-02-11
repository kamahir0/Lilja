#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Lilja.Repository.Diagnostics
{
    /// <summary>
    /// エディタデバッグ用リポジトリ追跡クラス。
    /// 全てのリポジトリインスタンスへの弱参照を保持し、エディタ拡張からのアクセスを提供する。
    /// </summary>
    public static class RepositoryTracker
    {
        public enum RepositoryType
        {
            InMemory,
            Json,
            MessagePack
        }

        private static readonly List<(WeakReference<object> Repo, RepositoryType Type)> _repositories =
            new List<(WeakReference<object>, RepositoryType)>();

        /// <summary>
        /// リポジトリを追跡対象に追加する。
        /// コンストラクタから呼び出されることを想定。
        /// </summary>
        public static void Track(object repository, RepositoryType type)
        {
            // 既に死んでいる参照を掃除する（簡易的なGC）
            _repositories.RemoveAll(t => !t.Repo.TryGetTarget(out _));

            _repositories.Add((new WeakReference<object>(repository), type));
        }

        /// <summary>
        /// 現在生存している全てのリポジトリインスタンスを取得する。
        /// </summary>
        public static IEnumerable<object> GetAll(RepositoryType type)
        {
            foreach (var (weakRef, t) in _repositories)
            {
                if (t == type && weakRef.TryGetTarget(out var repo))
                {
                    yield return repo;
                }
            }
        }
    }
}
#endif
