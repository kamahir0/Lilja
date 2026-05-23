using System;

namespace Lilja.ScreenManagement
{

    public sealed class GameScreenContext
    {
        internal GameScreenContext(GameScreenConnector connector)
        {
            Connector = connector ?? throw new ArgumentNullException(nameof(connector));
        }

        internal GameScreenConnector Connector { get; }

        public int Layer { get; internal set; }

        public GameScreenOptions Options { get; internal set; }
    }

    internal sealed class GameScreenConnector
    {

        internal GameScreenConnector Parent { get; set; }

        internal GameScreenConnector Child { get; set; }

        internal object Owner { get; set; }

        internal bool IsClosing { get; set; }
    }
}
