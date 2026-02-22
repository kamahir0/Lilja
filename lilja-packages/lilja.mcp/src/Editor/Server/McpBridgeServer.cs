using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Lilja.Mcp.Editor.Server
{
    /// <summary>
    /// UnityエディタにHTTPサーバーを組み込み、Rust MCPサーバーからのリクエストを受け付けるブリッジ
    /// </summary>
    [InitializeOnLoad]
    public static class McpBridgeServer
    {
        private static HttpListener _listener;
        private static Thread _serverThread;
        private static bool _isRunning;
        private const int Port = 8080;

        // =============================================================
        //  ライフサイクル管理
        // =============================================================

        static McpBridgeServer()
        {
            if (!Application.isBatchMode)
            {
                StartServer();
                AssemblyReloadEvents.beforeAssemblyReload += StopServer;
                EditorApplication.quitting += StopServer;
            }
        }

        /// <summary>
        /// HTTPサーバーを開始する
        /// </summary>
        private static void StartServer()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();
                _isRunning = true;

                // ログコールバックを登録（get_console_logs 用）
                ToolHandlers.EnsureLogCallbackRegistered();

                _serverThread = new Thread(Listen)
                {
                    IsBackground = true
                };
                _serverThread.Start();

                Debug.Log($"[Lilja.MCP] Server started at http://localhost:{Port}/");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lilja.MCP] Failed to start server: {e.Message}");
            }
        }

        /// <summary>
        /// HTTPサーバーを停止する
        /// </summary>
        private static void StopServer()
        {
            _isRunning = false;

            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }

            if (_serverThread != null && _serverThread.IsAlive)
            {
                _serverThread.Abort();
                _serverThread = null;
            }

            Debug.Log("[Lilja.MCP] Server stopped.");
        }

        // =============================================================
        //  リクエスト待ち受けループ
        // =============================================================

        /// <summary>
        /// バックグラウンドスレッドでリクエストを待ち受ける
        /// </summary>
        private static void Listen()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // リスナーが停止された場合の正常終了
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Lilja.MCP] Error: {e.Message}");
                }
            }
        }

        // =============================================================
        //  リクエスト処理
        // =============================================================

        /// <summary>
        /// 受信したHTTPリクエストを処理する
        /// </summary>
        /// <remarks>
        /// Unity APIはメインスレッドでのみ呼び出し可能なため、
        /// ManualResetEventSlim を使用してメインスレッドでの実行完了を待つ
        /// </remarks>
        private static void ProcessRequest(HttpListenerContext context)
        {
            string responseString = string.Empty;
            int statusCode = 200;

            try
            {
                var request = context.Request;

                if (request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string body = reader.ReadToEnd();

                        // メインスレッドでコマンドを実行し、完了を待つ
                        string result = string.Empty;
                        Exception capturedException = null;
                        var waitHandle = new ManualResetEventSlim(false);

                        EditorApplication.delayCall += () =>
                        {
                            try
                            {
                                result = ToolHandlers.HandleCommand(body);
                            }
                            catch (Exception e)
                            {
                                capturedException = e;
                            }
                            finally
                            {
                                waitHandle.Set();
                            }
                        };

                        // メインスレッドでの実行完了を待機（タイムアウト30秒）
                        if (!waitHandle.Wait(TimeSpan.FromSeconds(30)))
                        {
                            statusCode = 504;
                            responseString = "{\"error\": \"Timeout waiting for Unity main thread\"}";
                            return;
                        }

                        if (capturedException != null)
                        {
                            throw capturedException;
                        }

                        responseString = result;
                    }
                }
                else
                {
                    statusCode = 405;
                    responseString = "{\"error\": \"Method Not Allowed\"}";
                }
            }
            catch (Exception e)
            {
                statusCode = 500;
                responseString = $"{{\"error\": \"{e.Message}\"}}";
                Debug.LogError($"[Lilja.MCP] Request Handling Error: {e}");
            }
            finally
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
        }
    }
}
