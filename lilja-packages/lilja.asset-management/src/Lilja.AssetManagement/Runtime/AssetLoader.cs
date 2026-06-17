namespace Lilja.AssetManagement
{
    /// <summary>
    /// アセット管理のグローバルアクセスポイント
    /// </summary>
    public static class AssetLoader
    {
        /// <summary>
        /// グローバルで使用されるIAssetLoaderのインスタンスを取得または設定します
        /// デフォルトではResourcesAssetLoaderが使用されます
        /// </summary>
        public static IAssetLoader Global
        {
            get => _global ??= new ResourcesAssetLoader();
            set => _global = value;
        }

        private static IAssetLoader _global;
    }
}