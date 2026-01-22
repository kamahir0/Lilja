using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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
                UnityEngine.Debug.LogError("lilja-packagesディレクトリが指定されていません。");
                return null;
            }

            if (string.IsNullOrEmpty(parameters.PackageBaseName))
            {
                UnityEngine.Debug.LogError("パッケージ基本名が指定されていません。");
                return null;
            }

            // 命名規則に従って各名前を生成
            string displayName = $"Lilja.{parameters.PackageBaseName}";
            string kebabName = ConvertToKebabCase(parameters.PackageBaseName);
            string technicalName = $"lilja-{kebabName}";
            string packageName = $"com.{parameters.OrganizationName}.lilja.{kebabName}";

            // 出力先パス
            string projectPath = Path.Combine(parameters.LiljaPackagesDirectory, displayName);
            string packagePath = Path.Combine(projectPath, "Packages", packageName);

            // ディレクトリ存在チェック
            if (Directory.Exists(projectPath))
            {
                UnityEngine.Debug.LogError($"ディレクトリが既に存在します: {projectPath}");
                return null;
            }

            // Unityプロジェクト構造を作成
            CreateUnityProjectStructure(projectPath, packagePath, displayName, packageName, parameters);

            UnityEngine.Debug.Log($"✨ Created Lilja Package: {projectPath}");

            return projectPath;
        }

        /// <summary>
        /// PascalCaseをkebab-caseに変換
        /// </summary>
        public static string ConvertToKebabCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
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

        private static void CreateUnityProjectStructure(
            string projectPath,
            string packagePath,
            string displayName,
            string packageName,
            PackageCreatorParameters parameters)
        {
            // プロジェクトディレクトリ作成
            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(Path.Combine(projectPath, "Assets"));
            Directory.CreateDirectory(Path.Combine(projectPath, "ProjectSettings"));

            // パッケージディレクトリ作成
            Directory.CreateDirectory(packagePath);
            Directory.CreateDirectory(Path.Combine(packagePath, "Runtime"));
            Directory.CreateDirectory(Path.Combine(packagePath, "Editor"));

            // package.json 作成
            CreatePackageJson(packagePath, packageName, displayName, parameters);

            // Runtime/Editor asmdef 作成
            CreateRuntimeAsmdef(packagePath, displayName);
            CreateEditorAsmdef(packagePath, displayName);

            // ProjectSettings/ProjectVersion.txt 作成
            CreateProjectVersionFile(projectPath);

            // .gitignore 作成
            CreateGitignore(projectPath);
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
                return "";
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

        private static void CreateProjectVersionFile(string projectPath)
        {
            string projectSettingsPath = Path.Combine(projectPath, "ProjectSettings");
            string projectVersionPath = Path.Combine(projectSettingsPath, "ProjectVersion.txt");

            string content = $"m_EditorVersion: {Application.unityVersion}";
            File.WriteAllText(projectVersionPath, content);
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
