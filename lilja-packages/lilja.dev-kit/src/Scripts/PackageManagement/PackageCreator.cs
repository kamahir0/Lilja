using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// Liljaパッケージ作成ロジックを提供する静的クラス
    /// </summary>
    public static class PackageCreator
    {
        /// <summary>
        /// Liljaパッケージを作成する
        /// </summary>
        /// <param name="parameters">パッケージ作成パラメータ</param>
        /// <returns>作成したパッケージのパス（失敗時はnull）</returns>
        public static string Create(PackageCreatorParameters parameters)
        {
            // パラメータの検証
            if (string.IsNullOrEmpty(parameters.LiljaPackagesDirectory))
            {
                Debug.LogError("lilja-packagesディレクトリが指定されていません。");
                return null;
            }

            if (string.IsNullOrEmpty(parameters.PackageBaseName))
            {
                Debug.LogError("パッケージ基本名が指定されていません。");
                return null;
            }

            // 命名規則に従って各名前を生成
            string displayName = $"Lilja.{parameters.PackageBaseName}";
            string kebabName = ConvertToKebabCase(parameters.PackageBaseName);
            string packageName = $"com.{parameters.OrganizationName}.lilja.{kebabName}";

            // ディレクトリ名: lilja.package-name (kebab-case)
            string directoryName = $"lilja.{kebabName}";

            // 出力先パス (パッケージルート)
            string packageRoot = Path.Combine(parameters.LiljaPackagesDirectory, directoryName);

            // ディレクトリ存在チェック
            if (Directory.Exists(packageRoot))
            {
                Debug.LogError($"ディレクトリが既に存在します: {packageRoot}");
                return null;
            }

            // パッケージ構造を作成
            if (!CreatePackageStructure(packageRoot, displayName, packageName, parameters))
            {
                // 作成失敗時は（可能なら）クリーンアップしてnullを返す
                // 現状はディレクトリが残る可能性があるが、エラーログが出ているのでユーザーに任せる
                return null;
            }

            Debug.Log($"✨ Created Lilja Package: {packageRoot}");

            return packageRoot;
        }

        /// <summary>
        /// PascalCaseをkebab-caseに変換
        /// </summary>
        public static string ConvertToKebabCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            string kebab = Regex.Replace(input, "(?<!^)([A-Z])", "-$1").ToLower();
            return kebab;
        }

        /// <summary>
        /// DisplayNameを生成
        /// </summary>
        public static string GenerateDisplayName(string packageBaseName)
        {
            return $"Lilja.{packageBaseName}";
        }

        /// <summary>
        /// TechnicalNameを生成
        /// </summary>
        public static string GenerateTechnicalName(string packageBaseName)
        {
            return $"lilja-{ConvertToKebabCase(packageBaseName)}";
        }

        /// <summary>
        /// パッケージ名を生成
        /// </summary>
        public static string GeneratePackageName(string organizationName, string packageBaseName)
        {
            string kebabName = ConvertToKebabCase(packageBaseName);
            return $"com.{organizationName}.lilja.{kebabName}";
        }

        #region Private Methods

        private static bool CreatePackageStructure(
            string packageRoot,
            string displayName,
            string packageName,
            PackageCreatorParameters parameters)
        {
            // 1. 基本パッケージテンプレートの展開
            // DevKit自体の構造変更により Scripts/Templates~ に移動
            string packageTemplatePath = GetTemplatePath("Package");
            if (Directory.Exists(packageTemplatePath))
            {
                CopyAndReplaceTemplate(packageTemplatePath, packageRoot, displayName);
            }
            else
            {
                Debug.LogError($"[PackageCreator] Package template not found at: {packageTemplatePath}");
                return false;
            }

            // 2. package.json のプレースホルダーを追加置換
            // CopyAndReplaceTemplateですでに #DISPLAY_NAME# は置換されているが、
            // #PACKAGE_NAME#, #UNITY_VERSION#, #AUTHOR_SECTION# は残っているためここで処理する
            ProcessPackageJson(packageRoot, packageName, parameters);

            // 3. アナライザ生成
            if (parameters.UseAnalyzer)
            {
                CreateAnalyzerSolution(packageRoot, displayName);
            }

            return true;
        }

        private static void ProcessPackageJson(
            string packageRoot,
            string packageName,
            PackageCreatorParameters parameters)
        {
            string packageJsonPath = Path.Combine(packageRoot, "src", "package.json");
            if (!File.Exists(packageJsonPath)) return;

            string content = File.ReadAllText(packageJsonPath);

            // 現在のUnityバージョンからメジャー.マイナーを取得
            string unityVersion = Application.unityVersion;
            string[] versionParts = unityVersion.Split('.');
            string majorMinor = versionParts.Length >= 2 ? $"{versionParts[0]}.{versionParts[1]}" : unityVersion;

            // Author部分を構築
            string authorSection = BuildAuthorSection(parameters);

            // URL部分を構築
            string urlSection = BuildUrlSection(packageRoot, parameters);

            // 置換実行
            content = content.Replace("#PACKAGE_NAME#", packageName)
                .Replace("#UNITY_VERSION#", majorMinor)
                .Replace("#AUTHOR_SECTION#", authorSection)
                .Replace("#URL_SECTION#", urlSection);

            File.WriteAllText(packageJsonPath, content);
        }

        private static string GetTemplatePath(string templateName, [CallerFilePath] string callerPath = "")
        {
            string packageRoot = GetDevKitPackageRoot(callerPath);
            if (string.IsNullOrEmpty(packageRoot))
            {
                Debug.LogError("[PackageCreator] Could not find DevKit package root.");
                return string.Empty;
            }

            string templatesDir = FindTemplatesDirectory(packageRoot);
            if (string.IsNullOrEmpty(templatesDir))
            {
                Debug.LogError($"[PackageCreator] Templates~ directory not found in {packageRoot}");
                return string.Empty;
            }

            return Path.Combine(templatesDir, templateName);
        }

        private static string GetDevKitPackageRoot(string callerPath)
        {
            string directory = Path.GetDirectoryName(callerPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory, "package.json")))
                    return directory;
                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        private static string FindTemplatesDirectory(string packageRoot)
        {
            // Search for "Templates~" directory
            // Note: This might be slow if the package is huge, but it's executed only once per creation
            var dirs = Directory.GetDirectories(packageRoot, "Templates~", SearchOption.AllDirectories);
            return dirs.FirstOrDefault();
        }

        private static string BuildAuthorSection(PackageCreatorParameters parameters)
        {
            bool hasName = !string.IsNullOrEmpty(parameters.AuthorName);
            bool hasUrl = !string.IsNullOrEmpty(parameters.AuthorUrl);
            bool hasEmail = !string.IsNullOrEmpty(parameters.AuthorEmail);

            // 全て空の場合はAuthorセクション自体を省略
            if (!hasName && !hasUrl && !hasEmail)
            {
                return string.Empty;
            }

            var fields = new List<string>();
            if (hasName)
            {
                fields.Add($"    \"name\": \"{parameters.AuthorName}\"");
            }

            if (hasUrl)
            {
                fields.Add($"    \"url\": \"{parameters.AuthorUrl}\"");
            }

            if (hasEmail)
            {
                fields.Add($"    \"email\": \"{parameters.AuthorEmail}\"");
            }

            return ",\n  \"author\": {\n" + string.Join(",\n", fields) + "\n  }";
        }

        private static string BuildUrlSection(string packageRoot, PackageCreatorParameters parameters)
        {
            string repoRoot = GetRepoRoot(packageRoot);
            if (string.IsNullOrEmpty(repoRoot))
            {
                return string.Empty;
            }

            // リポジトリルートからの相対パスを取得
            string relativePath = Path.GetRelativePath(repoRoot, packageRoot).Replace("\\", "/");

            // OrganizationName（Githubのユーザー名/Org名と一致すると仮定）を使用してURLを構築
            // RepositoryNameは現状 "Lilja" 固定だが、必要ならこれもパラメータ化検討
            string baseUrl = $"https://github.com/{parameters.OrganizationName}/Lilja/blob/main";
            string licenseUrl = $"{baseUrl}/LICENSE";
            string docUrl = $"{baseUrl}/{relativePath}/README.md";
            string changelogUrl = $"{baseUrl}/{relativePath}/CHANGELOG.md";

            var fields = new List<string>
            {
                $"    \"documentationUrl\": \"{docUrl}\"",
                $"    \"changelogUrl\": \"{changelogUrl}\"",
                $"    \"licensesUrl\": \"{licenseUrl}\""
            };

            return ",\n" + string.Join(",\n", fields);
        }

        private static string GetRepoRoot(string path)
        {
            string directory = path;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory, ".git")))
                {
                    return directory;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        private static void CreateAnalyzerSolution(
            string packageRoot,
            string displayName)
        {
            string templatePath = GetTemplatePath("Analyzer");

            string targetDir = Path.Combine(packageRoot, "Analyzer");

            // ディレクトリコピー & 置換
            if (Directory.Exists(templatePath))
            {
                CopyAndReplaceTemplate(templatePath, targetDir, displayName);
            }
            else
            {
                Debug.LogError($"[PackageCreator] Analyzer template not found at: {templatePath}");
            }
        }

        private static void CopyAndReplaceTemplate(string sourceDir, string targetDir, string displayName)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta")) continue;

                // ファイル名の置換
                string newFileName = fileName.Replace("#DISPLAY_NAME#", displayName);
                string destFile = Path.Combine(targetDir, newFileName);

                // 内容の読み込みと置換
                string content = File.ReadAllText(file);
                content = content.Replace("#DISPLAY_NAME#", displayName);

                File.WriteAllText(destFile, content);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);

                // ディレクトリ名の置換
                string newDirName = dirName.Replace("#DISPLAY_NAME#", displayName);
                string destDir = Path.Combine(targetDir, newDirName);

                CopyAndReplaceTemplate(directory, destDir, displayName);
            }
        }

        #endregion
    }
}
