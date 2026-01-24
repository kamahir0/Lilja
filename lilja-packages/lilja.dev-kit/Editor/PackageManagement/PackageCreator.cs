using System.Collections.Generic;
using System.IO;
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
            CreatePackageStructure(packageRoot, displayName, packageName, parameters);

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

        private static void CreatePackageStructure(
            string packageRoot,
            string displayName,
            string packageName,
            PackageCreatorParameters parameters)
        {
            // 1. 基本パッケージテンプレートの展開
            string packageTemplatePath = GetTemplatePath("Editor/Templates~/Package", parameters);
            if (Directory.Exists(packageTemplatePath))
            {
                Debug.Log($"[PackageCreator] Copying package template from: {packageTemplatePath}");
                CopyAndReplaceTemplate(packageTemplatePath, packageRoot, displayName);
            }
            else
            {
                Debug.LogError($"[PackageCreator] Package template not found at: {packageTemplatePath}");
                return;
            }

            // 2. package.json のプレースホルダーを追加置換
            // CopyAndReplaceTemplateですでに #DISPLAY_NAME# は置換されているが、
            // #PACKAGE_NAME#, #UNITY_VERSION#, #AUTHOR_SECTION# は残っているためここで処理する
            ProcessPackageJson(packageRoot, packageName, displayName, parameters);

            // 3. アナライザ生成
            if (parameters.WithAnalyzer)
            {
                CreateAnalyzerSolution(packageRoot, displayName, parameters);
            }
        }

        private static void ProcessPackageJson(
            string packageRoot,
            string packageName,
            string displayName,
            PackageCreatorParameters parameters)
        {
            string packageJsonPath = Path.Combine(packageRoot, "package.json");
            if (!File.Exists(packageJsonPath)) return;

            string content = File.ReadAllText(packageJsonPath);

            // 現在のUnityバージョンからメジャー.マイナーを取得
            string unityVersion = Application.unityVersion;
            string[] versionParts = unityVersion.Split('.');
            string majorMinor = versionParts.Length >= 2 ? $"{versionParts[0]}.{versionParts[1]}" : unityVersion;

            // Author部分を構築
            string authorSection = BuildAuthorSection(parameters);

            // 置換実行
            content = content.Replace("#PACKAGE_NAME#", packageName)
                .Replace("#UNITY_VERSION#", majorMinor)
                .Replace("#AUTHOR_SECTION#", authorSection);

            File.WriteAllText(packageJsonPath, content);
        }

        private static string GetTemplatePath(string relativePath, PackageCreatorParameters parameters)
        {
            // テンプレートパスの解決 (簡易実装: Packages/com.kamahir0.lilja.dev-kit が存在するかチェックし、なければローカル開発用パスと仮定)
            string devKitPackagePath = "Packages/com.kamahir0.lilja.dev-kit";
            string fullPathInPackages = Path.GetFullPath(devKitPackagePath);

            if (Directory.Exists(fullPathInPackages))
            {
                return Path.Combine(devKitPackagePath, relativePath);
            }
            else
            {
                // ローカル開発時 (lilja-packages/lilja.dev-kit)
                string devKitDir = Path.Combine(parameters.LiljaPackagesDirectory, "lilja.dev-kit");
                return Path.Combine(devKitDir, relativePath);
            }
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

        private static void CreateAnalyzerSolution(
            string packageRoot,
            string displayName,
            PackageCreatorParameters parameters)
        {
            string templatePath = GetTemplatePath("Editor/Templates~/Analyzer", parameters);

            string targetDir = Path.Combine(packageRoot, "Analyzer~");

            // ディレクトリコピー & 置換
            if (Directory.Exists(templatePath))
            {
                Debug.Log($"[PackageCreator] Copying analyzer template from: {templatePath}");
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
