using System;
using System.Collections.Generic;

namespace Lilja.DebugUI
{
    /// <summary>
    /// ページ名をキーとして、ページインスタンスを1つキャッシュする。
    /// 同一ページ型のインスタンスは常にただ1つ。
    /// </summary>
    internal sealed class DebugPageCache
    {
        private readonly Dictionary<string, DebugPage> _cache = new();

        internal bool Contains(string pageName) => _cache.ContainsKey(pageName);

        /// <summary>
        /// ページを登録する。インスタンスを1つ生成してキャッシュする。
        /// 既に登録済みの場合は何もしない。
        /// </summary>
        internal void Register(string pageName, Func<DebugPage> factory)
        {
            if (string.IsNullOrEmpty(pageName))
                throw new ArgumentException("[DebugMenu] pageName must not be null or empty.", nameof(pageName));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (Contains(pageName)) return;

            // インスタンス生成前にスロットを確保し、Configure() 内の再帰的な Register 呼び出しをガードする
            _cache[pageName] = null;
            try
            {
                var page = factory();
                if (page == null)
                    throw new InvalidOperationException($"[DebugMenu] Page factory for '{pageName}' returned null.");

                page.name = pageName;
                PreparePage(page);
                _cache[pageName] = page;
            }
            catch
            {
                _cache.Remove(pageName);
                throw;
            }
        }

        /// <summary>
        /// 外部から渡された既存ページを初期化する。
        /// ルートページや一時ページなど、キャッシュ経由でない場合にも統一的な初期化を行う。
        /// </summary>
        internal void PreparePage(DebugPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            page.Configure(new DebugUIBuilder(page, this));
        }

        /// <summary>
        /// 既に生成・初期化済みのページをキャッシュに登録する。
        /// ルートページのように外部でインスタンス化されたページを GetPage/NavigateTo の対象にする。
        /// 既に同名が登録済みの場合は何もしない。
        /// </summary>
        internal void RegisterExisting(DebugPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (string.IsNullOrEmpty(page.name))
                throw new ArgumentException("[DebugMenu] Existing page must have a non-empty name.", nameof(page));

            if (Contains(page.name)) return;
            _cache[page.name] = page;
        }

        /// <summary>
        /// キャッシュされたインスタンスを返す。未登録なら null を返す。
        /// </summary>
        internal DebugPage Get(string pageName)
        {
            return _cache.TryGetValue(pageName, out var page) ? page : null;
        }

        /// <summary>
        /// キャッシュされている全ページを列挙する。
        /// </summary>
        internal IEnumerable<DebugPage> GetAllCachedPages() => _cache.Values;

        /// <summary>
        /// 登録済みページ名の一覧を返す。
        /// </summary>
        internal IReadOnlyList<string> GetPageNames() => new List<string>(_cache.Keys);

        /// <summary>
        /// ページ使用完了時の処理。スクロール位置をリセットする。
        /// </summary>
        internal void Return(DebugPage page)
        {
            page.ResetScrollPosition();
        }
    }
}
