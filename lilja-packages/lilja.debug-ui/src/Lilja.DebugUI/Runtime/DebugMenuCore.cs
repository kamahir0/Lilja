namespace Lilja.DebugUI
{
    /// <summary>
    /// DebugMenu のランタイム状態を保持する internal singleton。
    /// PageCache・HostRegistry・RootPageName を集約し、Runtime/Editor 両 Navigator が共有する。
    /// DebugMenu.Initialize() で生成され、PlayMode 終了時に破棄される。
    /// </summary>
    internal sealed class DebugMenuCore
    {
        /// <summary>現在有効なシングルトン。Initialize 前・PlayMode 終了後は null。</summary>
        internal static DebugMenuCore Shared { get; private set; }

        internal DebugPageCache PageCache { get; } = new();
        internal HostRegistry HostRegistry { get; } = new();

        /// <summary>ルートページ名。InitRootPage 後に確定する。</summary>
        internal string RootPageName { get; set; }

        internal static void Create()
        {
            Shared = new DebugMenuCore();
        }

        internal static void Destroy()
        {
            Shared = null;
        }
    }
}
