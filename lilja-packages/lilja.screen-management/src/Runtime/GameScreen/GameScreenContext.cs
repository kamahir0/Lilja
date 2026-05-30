using System.Collections.Generic;
using System.Threading;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面スタックの共通の設定パラメータや、アクティブな画面リスト、多重遷移防止などを一元管理するコンテキストクラス。
    /// </summary>
    public sealed class GameScreenContext
    {
        private readonly List<IGameScreenInternal> _activeScreens = new();

        internal GameScreenContext()
        {
            Gate = new SemaphoreSlim(1, 1);
            Options = GameScreenOptions.Default;
        }

        /// <summary>
        /// 新規のルートコンテキストを生成します。
        /// </summary>
        /// <param name="options">この遷移スタック全体で共有・伝播されるカスタムオプション。指定しない場合はデフォルト設定が適用されます。</param>
        /// <returns>安全に初期化されたルートコンテキストインスタンス</returns>
        public static GameScreenContext CreateRoot(GameScreenOptions options = null)
        {
            return new GameScreenContext { Options = options ?? GameScreenOptions.Default };
        }

        /// <summary>
        /// このコンテキスト（スタック）内で現在アクティブな画面の読み取り専用リスト。
        /// サードパーティの拡張アセンブリからも安全に参照可能です。
        /// </summary>
        public IReadOnlyList<IGameScreen> ActiveScreens => _activeScreens;

        /// <summary>
        /// 内部（Procedures等）での遷移・破棄操作に使用する実体リスト。
        /// </summary>
        internal List<IGameScreenInternal> ActiveScreensInternal => _activeScreens;

        /// <summary>
        /// 多重遷移を防ぐための非同期セマフォ。
        /// </summary>
        internal SemaphoreSlim Gate { get; }

        /// <summary>
        /// このグループ全体がクローズ処理中であるかを示すフラグ。
        /// </summary>
        internal bool IsClosing { get; set; }

        /// <summary>
        /// 現在有効な依存関係オプション。
        /// </summary>
        public GameScreenOptions Options { get; internal set; }
    }
}
