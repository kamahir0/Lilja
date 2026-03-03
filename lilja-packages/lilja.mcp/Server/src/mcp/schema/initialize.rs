use serde::{Deserialize, Serialize};
use serde_json::Value;

pub const METHOD: &str = "initialize";

// 1. Initialize Params (Cursorから届く)
#[derive(Deserialize, Debug)]
#[allow(dead_code)]
#[serde(rename_all = "camelCase")]
pub struct Params {
    pub protocol_version: ProtocolVersion,
    pub capabilities: Value, // クライアントの能力
    pub client_info: Implementation,
}

// 2. Initialize Result (Cursorへ返す)
#[derive(Serialize, Debug)]
#[serde(rename_all = "camelCase")]
pub struct Result {
    pub protocol_version: ProtocolVersion,
    pub capabilities: ServerCapabilities,
    pub server_info: Implementation,
}

#[derive(Serialize, Deserialize, Debug)]
pub struct ProtocolVersion(pub String);

#[derive(Serialize, Deserialize, Debug)]
#[serde(rename_all = "camelCase")]
pub struct ServerCapabilities {
    pub tools: Option<Value>,
}

#[derive(Serialize, Deserialize, Debug)]
#[serde(rename_all = "camelCase")]
pub struct Implementation {
    pub name: String,
    pub version: String,
}
