using System;

namespace Lilja.ScreenManagement
{
    /// <summary>
    /// 画面グループによって排他管理される画面の基底クラス。
    /// </summary>
    /// <typeparam name="TArgs">初期化引数の型</typeparam>
    public abstract class GameScreen<TArgs> : GameScreenBase<TArgs>
    {
        /// <summary>
        /// この画面を所有している実行中の画面グループを取得します。
        /// </summary>
        protected internal GameScreenGroup Group
        {
            get =>
                _group
                ?? throw new InvalidOperationException(
                    "[Lilja.ScreenManagement] この画面はグループ内で実行されていません。"
                );
            internal set => _group = value;
        }

        /// <inheritdoc />
        protected override void OnDispose()
        {
            _group = null;
            base.OnDispose();
        }

        private GameScreenGroup _group;
    }
}
