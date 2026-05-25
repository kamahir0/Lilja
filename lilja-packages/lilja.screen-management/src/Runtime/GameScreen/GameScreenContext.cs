using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面の階層木構造上で、親子関係や共通の設定パラメータを伝播するためのコンテキストクラス。
    /// </summary>
    public sealed class GameScreenContext
    {
        internal GameScreenContext(GameScreenConnector connector)
        {
            Connector = connector ?? throw new ArgumentNullException(nameof(connector));
        }

        /// <summary>
        /// 画面階層の最上部（Root）として機能する、新規のルートコンテキストを生成します。
        /// </summary>
        /// <param name="options">この遷移ツリー全体で共有・伝播されるカスタムオプション。指定しない場合はデフォルト設定が適用されます。</param>
        /// <returns>安全に初期化されたルートコンテキストインスタンス</returns>
        public static GameScreenContext CreateRoot(GameScreenOptions options = null)
        {
            var connector = new GameScreenConnector();
            return new GameScreenContext(connector)
            {
                Layer = 0,
                BaseOptions = options ?? GameScreenOptions.Default,
            };
        }

        /// <summary>
        /// ランタイムツリー上のこの画面ノードのコネクタを取得します。
        /// </summary>
        public GameScreenConnector Connector { get; }

        /// <summary>
        /// この画面に割り当てられたソート順などの描画レイヤー値。
        /// </summary>
        public int Layer { get; internal set; }

        /// <summary>
        /// 現在有効な依存関係オプション。一時オーバーライドが設定されている場合はそれを優先し、それ以外はツリー共通のベースオプションを返します。
        /// </summary>
        public GameScreenOptions Options =>
            OverrideOptions ?? BaseOptions ?? GameScreenOptions.Default;

        /// <summary>
        /// 画面ツリー共通で伝播されるベースのオプション。
        /// </summary>
        internal GameScreenOptions BaseOptions { get; set; }

        /// <summary>
        /// この画面ノード固有の一時的なオーバーライドオプション（Screen内部から自由に設定・変更可能です）。
        /// </summary>
        public GameScreenOptions OverrideOptions { get; set; }
    }

    /// <summary>
    /// 画面遷移システムが動的に構築するランタイム木構造上の各ノードを繋ぐ双方向リンクコネクタ。
    /// </summary>
    public sealed class GameScreenConnector
    {
        /// <summary>
        /// 親画面ノードのコネクタ。
        /// </summary>
        public GameScreenConnector Parent { get; internal set; }

        /// <summary>
        /// 子画面ノードのコネクタ。
        /// </summary>
        public GameScreenConnector Child { get; internal set; }

        /// <summary>
        /// このコネクタが表す画面ノードの所有者オブジェクト。
        /// </summary>
        public object Owner { get; internal set; }

        /// <summary>
        /// このノード以下のサブツリーがクローズ処理中であるかを示すフラグ。
        /// </summary>
        public bool IsClosing { get; internal set; }
    }
}
