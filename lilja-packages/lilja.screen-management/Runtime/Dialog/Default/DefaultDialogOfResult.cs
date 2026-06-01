using System;

namespace Lilja.ScreenManagement.Dialog
{
    /// <summary>
    /// メソッドチェーン（Fluent API）を使用してデフォルトデザインのダイアログを動的に作成・表示するための、
    /// 引数なし（ValueTuple）にバインドされたデフォルトダイアログクラス。
    /// </summary>
    /// <typeparam name="TResult">ダイアログが返却する結果の型。</typeparam>
    public class DefaultDialog<TResult> : DefaultDialog<ValueTuple, TResult>
    {
        /// <summary>
        /// 指定したタイトルを持つデフォルトダイアログのビルダーを作成します。
        /// </summary>
        /// <param name="title">ダイアログのタイトル文字列。</param>
        /// <returns>ダイアログの設定を構築するビルダーインスタンス。</returns>
        public static DefaultDialogBuilder<TResult> Create(string title)
        {
            return new DefaultDialogBuilder<TResult>(title);
        }
    }
}
