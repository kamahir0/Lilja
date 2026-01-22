using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// パッケージをプロジェクトにインポートするユーティリティ
    /// </summary>
    public static class PackageImporter
    {
        private const string ManifestPath = "Packages/manifest.json";

        /// <summary>
        /// 指定したパスのパッケージをプロジェクトにインポートする
        /// manifest.jsonに相対パスで追加
        /// </summary>
        /// <param name="packagePath">パッケージのルートディレクトリへの絶対パス</param>
        /// <returns>成功した場合true</returns>
        public static bool Import(string packagePath)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError("[PackageImporter] パッケージパスが指定されていません。");
                return false;
            }

            if (!Directory.Exists(packagePath))
            {
                Debug.LogError($"[PackageImporter] パッケージディレクトリが存在しません: {packagePath}");
                return false;
            }

            // package.jsonからパッケージ名を取得
            string packageJsonPath = Path.Combine(packagePath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                Debug.LogError($"[PackageImporter] package.jsonが見つかりません: {packageJsonPath}");
                return false;
            }

            string packageName = ExtractPackageNameFromJson(packageJsonPath);
            if (string.IsNullOrEmpty(packageName))
            {
                Debug.LogError("[PackageImporter] package.jsonからパッケージ名を取得できませんでした。");
                return false;
            }

            // 相対パスを計算（Packagesフォルダからの相対パス）
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string packagesDir = Path.Combine(projectRoot, "Packages");
            string relativePath = Path.GetRelativePath(packagesDir, packagePath);

            // Unixスタイルのパス区切り文字に変換
            relativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');

            // manifest.jsonに追加
            if (!AddToManifest(packageName, relativePath))
            {
                return false;
            }

            // パッケージマネージャーを更新
            Client.Resolve();

            Debug.Log($"✅ [PackageImporter] パッケージをインポートしました: {packageName} ({relativePath})");
            return true;
        }

        /// <summary>
        /// package.jsonからパッケージ名（name）を抽出
        /// </summary>
        private static string ExtractPackageNameFromJson(string packageJsonPath)
        {
            try
            {
                string json = File.ReadAllText(packageJsonPath);
                // シンプルな正規表現でnameフィールドを抽出
                var match = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PackageImporter] package.jsonの読み取りに失敗: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// manifest.jsonにパッケージを追加
        /// </summary>
        private static bool AddToManifest(string packageName, string relativePath)
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[PackageImporter] manifest.jsonが見つかりません: {ManifestPath}");
                return false;
            }

            try
            {
                string content = File.ReadAllText(ManifestPath);

                // 既に登録されているかチェック
                if (content.Contains($"\"{packageName}\""))
                {
                    Debug.LogWarning($"[PackageImporter] パッケージは既に登録されています: {packageName}");
                    return true; // 既存の場合は成功として扱う
                }

                // "dependencies": { の直後に新しいエントリを追加
                string newEntry = $"\"{packageName}\": \"file:{relativePath}\",";
                string pattern = @"(""dependencies""\s*:\s*\{)";
                string replacement = $"$1\n    {newEntry}";

                content = Regex.Replace(content, pattern, replacement);

                File.WriteAllText(ManifestPath, content);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PackageImporter] manifest.jsonの更新に失敗: {e.Message}");
                return false;
            }
        }
    }
}
