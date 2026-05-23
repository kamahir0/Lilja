using System;

namespace Lilja.ScreenManagement
{

    public abstract class GameScreen<TArgs> : GameScreenBase<TArgs>
    {
        private GameScreenGroup _group;

        protected internal GameScreenGroup Group
        {
            get =>
                _group ?? throw new InvalidOperationException("This group screen is not running.");
            internal set => _group = value;
        }

        protected override void OnDispose()
        {
            _group = null;
        }
    }
}
