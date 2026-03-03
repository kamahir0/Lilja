use serde::{Deserialize, Serialize};
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
#[serde(untagged)]
pub enum Message {
    Request(Request),
    Response(Response),
    Notification(Notification),
}

/// JSON-RPC 2.0 リクエスト
#[derive(Debug, Serialize, Deserialize)]
pub struct Request {
    pub jsonrpc: String,
    pub id: Value,
    pub method: String,
    pub params: Option<Value>,
}

/// JSON-RPC 2.0 レスポンス
#[derive(Debug, Serialize, Deserialize)]
pub struct Response {
    pub jsonrpc: String,
    pub id: Value,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub result: Option<Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<Error>,
}

/// JSON-RPC 2.0 通知
#[derive(Debug, Serialize, Deserialize)]
pub struct Notification {
    pub jsonrpc: String,
    pub method: String,
    pub params: Option<Value>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct Error {
    pub code: i32,
    pub message: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<Value>,
}

/// レスポンスを生成する
pub fn response(id: Value, result: Value) -> Message {
    Message::Response(Response::new(id, result))
}

/// リクエストが不正な場合のエラーレスポンスを生成する
pub fn invalid_request() -> Message {
    Message::Response(Response::error(Value::Null, -32600, "Invalid Request".into()))
}

/// JSONパースエラーの場合のエラーレスポンスを生成する
pub fn parse_error() -> Message {
    Message::Response(Response::error(Value::Null, -32700, "Parse Error".into()))
}

/// メソッドが見つからない場合のエラーレスポンスを生成する
pub fn method_not_found() -> Message {
    Message::Response(Response::error(Value::Null, -32601, "Method Not Found".into()))
}

impl Response {
    const JSON_RPC: &str = "2.0";

    fn new(id: Value, result: Value) -> Self {
        Self {
            jsonrpc: Self::JSON_RPC.into(),
            id,
            result: Some(result),
            error: None,
        }
    }

    fn error(id: Value, code: i32, message: String) -> Self {
        Self {
            jsonrpc: Self::JSON_RPC.into(),
            id,
            result: None,
            error: Some(Error { code, message, data: None }),
        }
    }
}
