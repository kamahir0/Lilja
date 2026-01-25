using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lilja.DevKit.PackageManagement
{
    /// <summary>
    /// .code-workspaceファイルを作成する機能を提供します。
    /// </summary>
    public static class WorkspaceCreator
    {
        [MenuItem("Lilja/DevKit/Create Workspace")]
        public static void CreateWorkspace()
        {
            // Unityプロジェクトのルートディレクトリ (Assetsの親)
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogError("[WorkspaceCreator] Failed to get project root.");
                return;
            }

            // lilja-packages ディレクトリを探す
            // 現在の構成では、Sandboxプロジェクトは sandbox/ProjectName にあると仮定し、
            // lilja-packages は sandbox の兄弟ディレクトリにある
            // つまり ../../lilja-packages

            // 安全のため、親ディレクトリを遡って lilja-packages を探すロジックにするか、
            // あるいはシンプルに既定の相対パスを計算するか。
            // ユーザーのリクエストでは「今後Lilja開発用の新たなsandboxプロジェクトを追加したときに」とあるため、
            // ディレクトリ構造は lilja/sandbox/NewProject を想定するのが自然。
            // その場合、 lilja/lilja-packages は ../../lilja-packages となる。

            // プロジェクト名の取得 (ディレクトリ名)
            string projectName = Path.GetFileName(projectRoot);
            string workspaceFileName = $"{projectName}.code-workspace";
            string workspacePath = Path.Combine(projectRoot, workspaceFileName);

            // lilja-packages への相対パスを計算
            // まず絶対パスで探してみる
            string repoRoot = GetRepoRoot(projectRoot);
            string relativePathToPackages = string.Empty;

            if (!string.IsNullOrEmpty(repoRoot))
            {
                var liljaPackagesPath = Path.Combine(repoRoot, "lilja-packages");
                if (Directory.Exists(liljaPackagesPath))
                {
                    relativePathToPackages = Path.GetRelativePath(projectRoot, liljaPackagesPath).Replace("\\", "/");
                }
            }

            // 見つからなければデフォルトの仮定 (../../lilja-packages) を使用
            if (string.IsNullOrEmpty(relativePathToPackages))
            {
                relativePathToPackages = "../../lilja-packages";
                Debug.LogWarning(
                    $"[WorkspaceCreator] 'lilja-packages' not found automatically. Using default relative path: {relativePathToPackages}");
            }

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

                // ファイルを作成したのでAssetDatabaseを更新してUnityに見えるようにする（必須ではないが親切）
                // ただし .code-workspace はUnityが直接管理するファイルではないため Import は不要かもしれないが、
                // Projectウィンドウで見えたほうがいい場合もある。
                // 通常 .code-workspace はAssets外(ProjectRoot)に置くのでUnityのエクスプローラには出ない。
            }
            catch (IOException e)
            {
                Debug.LogError($"[WorkspaceCreator] Failed to write workspace file: {e.Message}");
            }
        }

        private static string GetRepoRoot(string path)
        {
            string directory = path;
            while (directory != null)
            {
                // .git がある場所、あるいは sandbox と lilja-packages がある場所をルートとみなす
                if (Directory.Exists(Path.Combine(directory, ".git")))
                {
                    return directory;
                }

                // あるいは lilja-packages ディレクトリがある場所
                if (Directory.Exists(Path.Combine(directory, "lilja-packages")))
                {
                    return directory;
                }

                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }
    }
}
