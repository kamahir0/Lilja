namespace Lilja.DebugUI
{
    internal enum HostKind { None, Runtime, Editor }

    /// <summary>
    /// DebugPage の VisualElement ツリーを所有する側（Runtime / Editor）が実装するインターフェース。
    /// 所有権の取得・解放は HostRegistry 経由でのみ行われる。
    /// </summary>
    internal interface IPageHost
    {
        HostKind Kind { get; }

        /// <summary>所有権が付与されたとき。ページの表示状態を確立する。</summary>
        void OnOwnershipGranted();

        /// <summary>所有権が剥奪されたとき。全ページを detach し、Navigator を中立化する。</summary>
        void OnOwnershipRevoked();
    }
}
