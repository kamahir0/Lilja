// ---------------------------------------------------------------
// protocol.rs — JSON-RPC 2.0 / MCPプロトコルの型定義
// ---------------------------------------------------------------

use serde::{Deserialize, Serialize};
use serde_json::Value;

// =============================================================
//  JSON-RPC 2.0 の基本型
// =============================================================

/// JSON-RPCリクエスト
#[derive(Debug, Deserialize)]
pub struct JsonRpcRequest {
    /// JSON-RPCバージョン（常に "2.0"）
    #[allow(dead_code)]
    pub jsonrpc: String,

    /// 呼び出すメソッド名（例: "initialize", "tools/list", "tools/call"）
    pub method: String,

    /// メソッドのパラメータ（省略可能）
    pub params: Option<Value>,

    /// リクエストID（通知の場合は None）
    pub id: Option<Value>,
}

/// JSON-RPCレスポンス
#[derive(Debug, Serialize)]
pub struct JsonRpcResponse {
    /// JSON-RPCバージョン（常に "2.0"）
    pub jsonrpc: String,

    /// 成功時の結果
    #[serde(skip_serializing_if = "Option::is_none")]
    pub result: Option<Value>,

    /// エラー時の情報
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<JsonRpcError>,

    /// リクエストに対応するID
    pub id: Option<Value>,
}

/// JSON-RPCエラーオブジェクト
#[derive(Debug, Serialize)]
pub struct JsonRpcError {
    /// エラーコード
    pub code: i32,

    /// エラーメッセージ
    pub message: String,

    /// 追加データ（省略可能）
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<Value>,
}

// =============================================================
//  レスポンスのヘルパー関数
// =============================================================

/// 成功レスポンスを生成する
pub fn success_response(id: Option<Value>, result: Value) -> JsonRpcResponse {
    JsonRpcResponse {
        jsonrpc: "2.0".to_string(),
        result: Some(result),
        error: None,
        id,
    }
}

/// エラーレスポンスを生成する
pub fn error_response(id: Option<Value>, code: i32, message: String) -> JsonRpcResponse {
    JsonRpcResponse {
        jsonrpc: "2.0".to_string(),
        result: None,
        error: Some(JsonRpcError {
            code,
            message,
            data: None,
        }),
        id,
    }
}

// =============================================================
//  MCP initialize レスポンス用の型
// =============================================================

/// MCPサーバーの情報
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerInfo {
    pub name: String,
    pub version: String,
}

/// MCPサーバーのケイパビリティ
#[derive(Debug, Serialize)]
pub struct ServerCapabilities {
    /// ツール機能を提供することを示す
    pub tools: ToolsCapability,
}

/// ツールケイパビリティ — 空のオブジェクト `{}` でOK
#[derive(Debug, Serialize)]
pub struct ToolsCapability {}

/// `initialize` メソッドのレスポンス本体
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InitializeResult {
    /// 対応するプロトコルバージョン
    pub protocol_version: String,

    /// サーバー情報
    pub server_info: ServerInfo,

    /// ケイパビリティ
    pub capabilities: ServerCapabilities,
}

// =============================================================
//  MCP tools/list レスポンス用の型
// =============================================================

/// ツールの入力スキーマ（JSON Schema形式）
#[derive(Debug, Clone, Serialize)]
pub struct ToolInputSchema {
    /// スキーマのタイプ（常に "object"）
    #[serde(rename = "type")]
    pub schema_type: String,

    /// プロパティ定義
    #[serde(skip_serializing_if = "Option::is_none")]
    pub properties: Option<Value>,

    /// 必須パラメータ
    #[serde(skip_serializing_if = "Option::is_none")]
    pub required: Option<Vec<String>>,
}

/// ツール定義（tools/list で返す個々のツール情報）
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ToolDefinition {
    /// ツールの一意な名前
    pub name: String,

    /// ツールの説明
    pub description: String,

    /// 入力パラメータのJSONスキーマ
    pub input_schema: ToolInputSchema,
}

// =============================================================
//  MCP tools/call レスポンス用の型
// =============================================================

/// ツール実行結果のコンテンツアイテム
#[derive(Debug, Serialize)]
pub struct ContentItem {
    /// コンテンツタイプ（"text" のみ対応）
    #[serde(rename = "type")]
    pub content_type: String,

    /// テキスト内容
    pub text: String,
}

/// ツール実行結果
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ToolCallResult {
    /// コンテンツ配列
    pub content: Vec<ContentItem>,

    /// エラーが発生したかどうか
    pub is_error: bool,
}
