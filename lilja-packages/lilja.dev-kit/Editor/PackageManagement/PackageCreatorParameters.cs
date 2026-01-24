using System;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// パッケージ作成パラメータ
    /// </summary>
    [Serializable]
    public class PackageCreatorParameters
    {
        /// <summary>
        /// lilja-packagesディレクトリの絶対パス
        /// </summary>
        public string LiljaPackagesDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 組織名（com.は含まない形式）
        /// 例: kamahir0
        /// </summary>
        public string OrganizationName { get; set; } = "kamahir0";

        /// <summary>
        /// パッケージ基本名（PascalCase）
        /// 例: FooBar
        /// </summary>
        public string PackageBaseName { get; set; } = "NewPackage";

        /// <summary>
        /// 作者名（任意）
        /// </summary>
        public string AuthorName { get; set; } = string.Empty;

        /// <summary>
        /// 作者URL（任意）
        /// </summary>
        public string AuthorUrl { get; set; } = string.Empty;

        /// <summary>
        /// 作者メールアドレス（任意）
        /// </summary>
        public string AuthorEmail { get; set; } = string.Empty;
    }
}
