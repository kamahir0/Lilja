namespace Lilja.DebugUI
{
    /// <summary>
    /// どちらのホスト（Runtime / Editor）が DebugPage を現在所有しているかを一元管理する。
    /// 所有権の移譲は必ずこのクラスを経由し、旧所有者の OnOwnershipRevoked → 新所有者の OnOwnershipGranted を保証する。
    /// </summary>
    internal sealed class HostRegistry
    {
        private IPageHost _runtimeHost;
        private IPageHost _editorHost;

        internal HostKind CurrentOwner { get; private set; } = HostKind.None;

        internal void RegisterRuntimeHost(IPageHost host)
        {
            _runtimeHost = host;
        }

        /// <summary>null を渡すとエディタホストの登録を解除する。</summary>
        internal void RegisterEditorHost(IPageHost host)
        {
            _editorHost = host;
        }

        /// <summary>
        /// 指定した側に所有権を要求する。
        /// 既に同じ側が所有している場合は no-op。
        /// 異なる側が所有している場合は旧所有者に OnOwnershipRevoked を呼んでから所有権を移す。
        /// </summary>
        internal void RequestOwnership(HostKind requester)
        {
            if (CurrentOwner == requester) return;

            if (CurrentOwner != HostKind.None)
            {
                GetHost(CurrentOwner)?.OnOwnershipRevoked();
            }

            CurrentOwner = requester;

            if (requester != HostKind.None)
            {
                GetHost(requester)?.OnOwnershipGranted();
            }
        }

        private IPageHost GetHost(HostKind kind) => kind switch
        {
            HostKind.Runtime => _runtimeHost,
            HostKind.Editor => _editorHost,
            _ => null
        };
    }
}
