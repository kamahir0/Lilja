// ---------------------------------------------------------------
// handler.rs — MCPリクエストのディスパッチ
//
// JSON-RPCリクエストのメソッド名に応じて、
// 適切なMCPレスポンスを生成して返す。
// ---------------------------------------------------------------

use serde_json::{json, Value};

use crate::protocol::{
    error_response, success_response, ContentItem, InitializeResult, JsonRpcRequest,
    JsonRpcResponse, ServerCapabilities, ServerInfo, ToolCallResult, ToolsCapability,
};
use crate::tools;
use crate::unity_client;

/// MCPリクエスト1件を処理し、レスポンスを返す
///
/// - 通知（idなし）の場合は `None` を返す（レスポンス送出不要）
/// - リクエスト（idあり）の場合は `Some(response)` を返す
pub async fn handle_request(request: &JsonRpcRequest) -> Option<JsonRpcResponse> {
    match request.method.as_str() {
        // ==========================================================
        //  initialize — クライアントとの初期化ハンドシェイク
        // ==========================================================
        "initialize" => {
            let result = InitializeResult {
                protocol_version: "2025-03-26".to_string(),
                server_info: ServerInfo {
                    name: "lilja-mcp-server".to_string(),
                    version: "0.1.0".to_string(),
                },
                capabilities: ServerCapabilities {
                    tools: ToolsCapability {},
                },
            };

            let value = serde_json::to_value(result).unwrap();
            Some(success_response(request.id.clone(), value))
        }

        // ==========================================================
        //  notifications/initialized — 初期化完了通知（レスポンス不要）
        // ==========================================================
        "notifications/initialized" => {
            eprintln!("[MCP] クライアントの初期化が完了しました");
            None
        }

        // ==========================================================
        //  tools/list — 利用可能なツール一覧を返す
        // ==========================================================
        "tools/list" => {
            let definitions = tools::get_tool_definitions();
            let value = json!({ "tools": definitions });
            Some(success_response(request.id.clone(), value))
        }

        // ==========================================================
        //  tools/call — 指定されたツールを実行する
        // ==========================================================
        "tools/call" => {
            let result = handle_tool_call(&request.params).await;
            Some(success_response(request.id.clone(), result))
        }

        // ==========================================================
        //  ping — ヘルスチェック
        // ==========================================================
        "ping" => Some(success_response(request.id.clone(), json!({}))),

        // ==========================================================
        //  未知のメソッド — エラーを返す
        // ==========================================================
        unknown => {
            eprintln!("[MCP] 未知のメソッド: {}", unknown);
            Some(error_response(
                request.id.clone(),
                -32601,
                format!("Method not found: {}", unknown),
            ))
        }
    }
}

/// tools/call の実装
///
/// params から tool名と引数を取り出し、Unityに転送して結果を返す
async fn handle_tool_call(params: &Option<Value>) -> Value {
    // paramsからtool名と引数を取得
    let (tool_name, arguments) = match params {
        Some(p) => {
            let name = p.get("name").and_then(|v| v.as_str()).unwrap_or("");
            let args = p.get("arguments").cloned();
            (name.to_string(), args)
        }
        None => {
            return to_tool_result("エラー: paramsが指定されていません", true);
        }
    };

    if tool_name.is_empty() {
        return to_tool_result("エラー: ツール名が指定されていません", true);
    }

    // Unityにコマンドを送信
    match unity_client::send_command(&tool_name, arguments).await {
        Ok(value) => {
            // Unityからのレスポンスをテキストとして返す
            let text = serde_json::to_string_pretty(&value).unwrap_or_default();
            to_tool_result(&text, false)
        }
        Err(e) => {
            let message = format!("Unityへの接続に失敗しました: {}", e);
            eprintln!("[MCP] {}", message);
            to_tool_result(&message, true)
        }
    }
}

/// ToolCallResult のJSONを生成するヘルパー
fn to_tool_result(text: &str, is_error: bool) -> Value {
    let result = ToolCallResult {
        content: vec![ContentItem {
            content_type: "text".to_string(),
            text: text.to_string(),
        }],
        is_error,
    };
    serde_json::to_value(result).unwrap()
}
