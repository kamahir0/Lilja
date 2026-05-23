using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// <see cref="GameScreenGroup"/> によって排他管理される画面の基底クラス。
    /// 兄弟関係の画面への遷移は、自分を所有しているグループへの切り替え要求（SwitchAsync）として表現します。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    public abstract class GameScreen<TArgs> : GameScreenBase<TArgs>
    {
        private GameScreenGroup _group;

        /// <summary>
        /// この画面を所有している実行中の画面グループを取得します。
        /// </summary>
        protected internal GameScreenGroup Group
        {
            get =>
                _group ?? throw new InvalidOperationException("This group screen is not running.");
            internal set => _group = value;
        }

        /// <summary>
        /// 破棄時の内部クリーンアップ処理を行います。
        /// </summary>
        protected override void OnDispose()
        {
            _group = null;
        }
    }
}
