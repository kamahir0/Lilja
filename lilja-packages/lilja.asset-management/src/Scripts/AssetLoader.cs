namespace Lilja.AssetManagement
{
    public static class AssetLoader
    {
        public static IAssetLoader Global
        {
            get => _global ??= new ResourcesAssetLoader();
            set => _global = value;
        }

        private static IAssetLoader _global;
    }
}