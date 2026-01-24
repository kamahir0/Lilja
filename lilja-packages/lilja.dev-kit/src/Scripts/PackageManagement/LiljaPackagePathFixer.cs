using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// Liljaパッケージのパス修正ユーティリティ
    /// </summary>
    public static class LiljaPackagePathFixer
    {
        private const string ManifestPath = "Packages/manifest.json";
        private const string LiljaPackagePrefix = "com.kamahir0.lilja.";

        /// <summary>
        /// このプロジェクトにローカルパッケージとしてインポートしているLiljaパッケージのパスを全て相対パスに修正する
        /// </summary>
        /// <returns>修正されたパッケージ数</returns>
        [MenuItem("Lilja/Package Management/Fix Lilja Package Paths to Relative")]
        public static int FixAllLiljaPackagePathsToRelative()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[LiljaPackagePathFixer] manifest.jsonが見つかりません: {ManifestPath}");
                return 0;
            }

            int fixedCount = 0;

            try
            {
                string content = File.ReadAllText(ManifestPath);
                string originalContent = content;

                // プロジェクトルートのPackagesフォルダパスを取得
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string packagesDir = Path.Combine(projectRoot!, "Packages");

                // Liljaパッケージのパターンにマッチするエントリを検索
                // 絶対パス (file:C:/ または file:/ で始まるもの) を相対パスに変換
                string pattern = $@"(""{LiljaPackagePrefix}[^""]+"")\s*:\s*""file:([A-Za-z]:[^""]+|/[^""]+)""";

                content = Regex.Replace(content, pattern, match =>
                {
                    string packageName = match.Groups[1].Value;
                    string absolutePath = match.Groups[2].Value;

                    // 既に相対パスの場合はスキップ（ドライブレターや / で始まらない場合）
                    if (!IsAbsolutePath(absolutePath))
                    {
                        return match.Value;
                    }

                    // パス区切り文字を正規化
                    string normalizedPath = absolutePath.Replace('/', Path.DirectorySeparatorChar);

                    // ディレクトリが存在するか確認
                    if (!Directory.Exists(normalizedPath))
                    {
                        Debug.LogWarning($"[LiljaPackagePathFixer] パッケージディレクトリが存在しません: {absolutePath}");
                        return match.Value;
                    }

                    // 相対パスを計算
                    string relativePath = Path.GetRelativePath(packagesDir, normalizedPath);

                    // Unixスタイルのパス区切り文字に変換
                    relativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');

                    fixedCount++;
                    Debug.Log($"[LiljaPackagePathFixer] パスを修正: {packageName} : {absolutePath} → {relativePath}");

                    return $"{packageName}: \"file:{relativePath}\"";
                });

                // 変更があった場合のみファイルを更新
                if (content != originalContent)
                {
                    File.WriteAllText(ManifestPath, content);

                    // パッケージマネージャーを更新
                    Client.Resolve();

                    Debug.Log($"✅ [LiljaPackagePathFixer] {fixedCount}個のLiljaパッケージパスを相対パスに修正しました。");
                }
                else
                {
                    Debug.Log("[LiljaPackagePathFixer] 修正が必要なLiljaパッケージはありませんでした。");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LiljaPackagePathFixer] manifest.jsonの更新に失敗: {e.Message}");
                return 0;
            }

            return fixedCount;
        }

        /// <summary>
        /// 指定されたパスが絶対パスかどうかを判定
        /// </summary>
        private static bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // Windowsの絶対パス (C:/ など)
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                return true;
            }

            // Unix系の絶対パス (/ で始まる)
            if (path.StartsWith("/"))
            {
                return true;
            }

            return false;
        }
    }
}
