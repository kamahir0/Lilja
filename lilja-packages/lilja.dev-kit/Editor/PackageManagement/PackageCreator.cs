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
            // パッケージルートディレクトリ作成
            Directory.CreateDirectory(packageRoot);

            // サブディレクトリ作成
            Directory.CreateDirectory(Path.Combine(packageRoot, "Runtime"));
            Directory.CreateDirectory(Path.Combine(packageRoot, "Editor"));

            // package.json 作成
            CreatePackageJson(packageRoot, packageName, displayName, parameters);

            // Runtime/Editor asmdef 作成
            CreateRuntimeAsmdef(packageRoot, displayName);
            CreateEditorAsmdef(packageRoot, displayName);

            // .gitignore 作成
            CreateGitignore(packageRoot);
        }

        private static void CreatePackageJson(
            string packagePath,
            string packageName,
            string displayName,
            PackageCreatorParameters parameters)
        {
            // 現在のUnityバージョンからメジャー.マイナーを取得
            string unityVersion = Application.unityVersion;
            string[] versionParts = unityVersion.Split('.');
            string majorMinor = versionParts.Length >= 2 ? $"{versionParts[0]}.{versionParts[1]}" : unityVersion;

            // Author部分を構築（空でないフィールドのみ含める）
            string authorSection = BuildAuthorSection(parameters);

            string jsonContent = $@"{{
  ""name"": ""{packageName}"",
  ""displayName"": ""{displayName}"",
  ""version"": ""0.1.0"",
  ""unity"": ""{majorMinor}"",
  ""description"": ""Lilja package created by DevKit."",
  ""keywords"": [
    ""lilja""
  ]{authorSection}
}}";
            File.WriteAllText(Path.Combine(packagePath, "package.json"), jsonContent);
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

        private static void CreateRuntimeAsmdef(string packagePath, string displayName)
        {
            string asmdefPath = Path.Combine(packagePath, "Runtime", $"{displayName}.asmdef");
            string asmdefContent = $@"{{
    ""name"": ""{displayName}"",
    ""rootNamespace"": ""{displayName}"",
    ""references"": [],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
            File.WriteAllText(asmdefPath, asmdefContent);
        }

        private static void CreateEditorAsmdef(string packagePath, string displayName)
        {
            string asmdefPath = Path.Combine(packagePath, "Editor", $"{displayName}.Editor.asmdef");
            string asmdefContent = $@"{{
    ""name"": ""{displayName}.Editor"",
    ""rootNamespace"": ""{displayName}.Editor"",
    ""references"": [
        ""{displayName}""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
            File.WriteAllText(asmdefPath, asmdefContent);
        }

        private static void CreateGitignore(string projectPath)
        {
            string gitignore = @"# Unity generated
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/

# Visual Studio cache directory
.vs/

# IDE
*.csproj
*.sln
/.idea/
";
            File.WriteAllText(Path.Combine(projectPath, ".gitignore"), gitignore);
        }

        #endregion
    }
}
