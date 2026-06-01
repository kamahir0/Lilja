using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lilja.Mcp.Editor.Server
{
    /// <summary>
    /// Rustサーバーから受信したコマンドを処理するハンドラのI/F
    /// </summary>
    public static class ToolHandlers
    {
        // =============================================================
        //  ログバッファ（get_console_logs 用）
        // =============================================================

        private const int MaxLogBufferSize = 200;
        private static readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private static bool _isLogCallbackRegistered;

        /// <summary>
        /// ログエントリの型
        /// </summary>
        [Serializable]
        private class LogEntry
        {
            public string message;
            public string type;
            public string timestamp;
        }

        /// <summary>
        /// コマンドリクエストの型
        /// </summary>
        [Serializable]
        private class CommandRequest
        {
            public string command;
            public Dictionary<string, object> args;
        }

        // =============================================================
        //  ログコールバックの登録
        // =============================================================

        /// <summary>
        /// ログコールバックを登録する
        /// </summary>
        /// <remarks>McpBridgeServer の初期化時に呼び出される</remarks>
        internal static void EnsureLogCallbackRegistered()
        {
            if (_isLogCallbackRegistered)
            {
                return;
            }

            Application.logMessageReceived += OnLogMessageReceived;
            _isLogCallbackRegistered = true;
        }

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            // MCP自身のログは記録しない（無限ループ防止）
            if (message.StartsWith("[Lilja.MCP]"))
            {
                return;
            }

            var entry = new LogEntry
            {
                message = message,
                type = type.ToString(),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            _logBuffer.Add(entry);

            // バッファサイズ制限
            while (_logBuffer.Count > MaxLogBufferSize)
            {
                _logBuffer.RemoveAt(0);
            }
        }

        // =============================================================
        //  コマンドディスパッチ
        // =============================================================

        /// <summary>
        /// JSONリクエストボディをパースし、対応するコマンドを実行する
        /// </summary>
        /// <param name="jsonBody">Rustサーバーから受信したJSONリクエスト</param>
        /// <returns>実行結果のJSON文字列</returns>
        public static string HandleCommand(string jsonBody)
        {
            var request = JsonConvert.DeserializeObject<CommandRequest>(jsonBody);

            if (request == null || string.IsNullOrEmpty(request.command))
            {
                throw new ArgumentException("Invalid command format");
            }

            switch (request.command)
            {
                case "compile_project":
                    return CompileProject();

                case "create_scene":
                    return CreateScene(request.args);

                case "get_hierarchy":
                    return GetHierarchy();

                case "instantiate_prefab":
                    return InstantiatePrefab(request.args);

                case "get_console_logs":
                    return GetConsoleLogs(request.args);

                default:
                    throw new ArgumentException($"Unknown command: {request.command}");
            }
        }

        // =============================================================
        //  各コマンドの実装
        // =============================================================

        /// <summary>
        /// スクリプトのコンパイルをトリガーする
        /// </summary>
        private static string CompileProject()
        {
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            return JsonConvert.SerializeObject(new { status = "ok", message = "Compilation triggered" });
        }

        /// <summary>
        /// 新しいシーンを作成する
        /// </summary>
        private static string CreateScene(Dictionary<string, object> args)
        {
            string sceneName = args?.ContainsKey("name") == true
                ? args["name"].ToString()
                : "New Scene";

            var newScene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );
            newScene.name = sceneName;

            return JsonConvert.SerializeObject(new { status = "ok", message = $"Created scene: {sceneName}" });
        }

        /// <summary>
        /// 現在のシーンのヒエラルキー情報を取得する
        /// </summary>
        private static string GetHierarchy()
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var hierarchy = rootObjects.Select(go => DumpGameObject(go)).ToList();
            return JsonConvert.SerializeObject(hierarchy);
        }

        /// <summary>
        /// GameObjectの情報を再帰的に取得する
        /// </summary>
        private static object DumpGameObject(GameObject go)
        {
            return new
            {
                name = go.name,
                instanceId = go.GetInstanceID(),
                children = Enumerable.Range(0, go.transform.childCount)
                    .Select(i => DumpGameObject(go.transform.GetChild(i).gameObject))
                    .ToList(),
                components = go.GetComponents<Component>()
                    .Select(c => c.GetType().Name)
                    .ToList()
            };
        }

        /// <summary>
        /// プレハブをシーンにインスタンス化する
        /// </summary>
        private static string InstantiatePrefab(Dictionary<string, object> args)
        {
            if (args == null || !args.TryGetValue("path", out var pathObj))
            {
                throw new ArgumentException("Missing 'path' argument");
            }

            string path = pathObj.ToString();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                return JsonConvert.SerializeObject(new { status = "error", message = $"Prefab not found at {path}" });
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab via MCP");
            Selection.activeGameObject = instance;

            return JsonConvert.SerializeObject(new
            {
                status = "ok",
                name = instance.name,
                instanceId = instance.GetInstanceID()
            });
        }

        /// <summary>
        /// コンソールログを取得する
        /// </summary>
        private static string GetConsoleLogs(Dictionary<string, object> args)
        {
            int count = 50;

            if (args?.ContainsKey("count") == true)
            {
                if (int.TryParse(args["count"].ToString(), out var parsed))
                {
                    count = parsed;
                }
            }

            // 直近 count 件を取得
            var logs = _logBuffer
                .Skip(Math.Max(0, _logBuffer.Count - count))
                .ToList();

            return JsonConvert.SerializeObject(new { status = "ok", count = logs.Count, logs });
        }
    }
}
