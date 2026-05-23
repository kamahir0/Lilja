using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面の階層木構造（ツリー構造）上で、親子関係や共通の設定パラメータを伝播するためのコンテキストクラス。
    /// </summary>
    public sealed class GameScreenContext
    {
        internal GameScreenContext(GameScreenConnector connector)
        {
            Connector = connector ?? throw new ArgumentNullException(nameof(connector));
        }

        /// <summary>
        /// ランタイムツリー上のこの画面ノードのコネクタを取得します。
        /// </summary>
        internal GameScreenConnector Connector { get; }

        /// <summary>
        /// この画面に割り当てられたソート順などの描画レイヤー値。
        /// </summary>
        public int Layer { get; internal set; }

        /// <summary>
        /// この遷移ツリー全体で共有・伝播される、トランジションやアセットプロバイダーなどの依存関係オプション。
        /// </summary>
        public GameScreenOptions Options { get; internal set; }
    }

    /// <summary>
    /// 画面遷移システムが動的に構築するランタイム木構造（ツリー構造）上の各ノードを繋ぐ双方向リンクコネクタ。
    /// </summary>
    internal sealed class GameScreenConnector
    {
        /// <summary>
        /// 親画面ノードのコネクタ。
        /// </summary>
        internal GameScreenConnector Parent { get; set; }

        /// <summary>
        /// 子画面ノードのコネクタ。
        /// </summary>
        internal GameScreenConnector Child { get; set; }

        /// <summary>
        /// このコネクタが表す画面ノードの所有者オブジェクト（GameScreenBase または GameScreenGroup）。
        /// </summary>
        internal object Owner { get; set; }

        /// <summary>
        /// このノード以下のサブツリーがクローズ処理中（Teardown中）であるかを示すフラグ。
        /// </summary>
        internal bool IsClosing { get; set; }
    }
}
