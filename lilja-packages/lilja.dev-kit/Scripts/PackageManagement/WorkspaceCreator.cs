using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// .code-workspaceファイルを作成する機能を提供します。
    /// </summary>
    public static class WorkspaceCreator
    {
        // EditorPrefsのキー (PackageCreatorWindowに合わせています)
        private const string KeyLiljaPackagesDirectory = "Lilja.DevKit.PackageCreator.LiljaPackagesDirectory";

        [MenuItem("Lilja/DevKit/Create Workspace", false, 1)]
        public static void CreateWorkspace()
        {
            // Unityプロジェクトのルートディレクトリ (Assetsの親)
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogError("[WorkspaceCreator] Failed to get project root.");
                return;
            }

            // プロジェクト名の取得 (ディレクトリ名)
            string projectName = Path.GetFileName(projectRoot);
            string workspaceFileName = $"{projectName}.code-workspace";
            string workspacePath = Path.Combine(projectRoot, workspaceFileName);

            // lilja-packages ディレクトリのパスを取得
            string relativePathToPackages = GetLiljaPackagesRelativePath(projectRoot);

            // JSONコンテンツの作成
            string jsonContent = $@"{{
	""folders"": [
		{{
			""path"": "".""
		}},
		{{
			""path"": ""{relativePathToPackages}""
		}}
	],
	""settings"": {{}}
}}";

            try
            {
                File.WriteAllText(workspacePath, jsonContent);
                Debug.Log($"✨ Created workspace file: {workspacePath}");
            }
            catch (IOException e)
            {
                Debug.LogError($"[WorkspaceCreator] Failed to write workspace file: {e.Message}");
            }
        }

        private static string GetLiljaPackagesRelativePath(string projectRoot)
        {
            // 候補1: DevKitパッケージ自身のインストールパスから逆算 (最優先)
            // アセンブリからパッケージ情報を取得
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WorkspaceCreator).Assembly);

            // ローカル（ファイルパス指定）でインストールされている場合のみ信頼する
            if (packageInfo != null && packageInfo.source == PackageSource.Local)
            {
                // 末尾のパス区切り文字（存在する場合）を削除してから親ディレクトリを取得する
                string devKitPath =
                    packageInfo.resolvedPath.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar); // 例: .../lilja/lilja-packages/lilja.dev-kit

                // もしresolvedPathがpackage.jsonを指していた場合の対策
                if (devKitPath.EndsWith("package.json"))
                {
                    devKitPath = Path.GetDirectoryName(devKitPath);
                }

                string liljaPackagesPath = Path.GetDirectoryName(devKitPath);

                // もし親ディレクトリではなく dev-kit 自身が返ってきてしまった場合のフェールセーフ
                if (liljaPackagesPath != null && liljaPackagesPath.EndsWith("lilja.dev-kit"))
                {
                    liljaPackagesPath = Path.GetDirectoryName(liljaPackagesPath);
                }

                if (Directory.Exists(liljaPackagesPath))
                {
                    Debug.Log($"[WorkspaceCreator] Found 'lilja-packages' from PackageInfo: {liljaPackagesPath}");
                    return Path.GetRelativePath(projectRoot, liljaPackagesPath).Replace("\\", "/");
                }
            }

            // 候補2: PackageCreatorWindow で保存されたパスからの取得 (フォールバック)
            // PackageCreatorWindowが動作するため、ユーザーがGUIで設定したパスがあればそれを使用
            string savedPath = EditorPrefs.GetString(KeyLiljaPackagesDirectory, string.Empty);
            if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
            {
                // ユーザーがGUIから誤って dev-kit ディレクトリ自体を選択して保存していた場合のフェールセーフ
                if (savedPath.EndsWith("lilja.dev-kit"))
                {
                    savedPath = Path.GetDirectoryName(savedPath);
                }

                Debug.Log($"[WorkspaceCreator] Found 'lilja-packages' from EditorPrefs: {savedPath}");
                return Path.GetRelativePath(projectRoot, savedPath).Replace("\\", "/");
            }

            // 候補3: 従来のリポジトリルート探索からの取得 (最後の手段)
            string repoRoot = GetRepoRoot(projectRoot);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                string liljaPackagesPathFallback = Path.Combine(repoRoot, "lilja-packages");
                if (Directory.Exists(liljaPackagesPathFallback))
                {
                    Debug.Log(
                        $"[WorkspaceCreator] Found 'lilja-packages' from Repo Root search: {liljaPackagesPathFallback}");
                    return Path.GetRelativePath(projectRoot, liljaPackagesPathFallback).Replace("\\", "/");
                }
            }

            // 全て失敗した場合のデフォルト
            string defaultRelativePath = "../../lilja-packages";
            Debug.LogWarning(
                $"[WorkspaceCreator] 'lilja-packages' not found automatically. Using default relative path: {defaultRelativePath}");
            return defaultRelativePath;
        }

        private static string GetRepoRoot(string path)
        {
            string directory = path;
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory, ".git"))) return directory;
                if (Directory.Exists(Path.Combine(directory, "lilja-packages"))) return directory;

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }
    }
}
