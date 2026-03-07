use super::ToolName;
use serde::{Deserialize, Serialize};
use serde_json::Value;

pub const METHOD: &str = "tools/list";

// 3. ListTools Params (空の場合が多いが構造体として定義)
#[derive(Deserialize, Debug)]
#[allow(dead_code)]
pub struct Params {
    pub cursor: Option<String>,
}

// 4. ListTools Result (サーバーが持つツールの一覧)
#[derive(Serialize, Debug)]
pub struct Result {
    pub tools: Vec<Tool>,
}

#[derive(Serialize, Debug)]
#[serde(rename_all = "camelCase")]
pub struct Tool {
    pub name: ToolName,
    pub description: String,
    pub input_schema: Value, // JSON Schema形式
}
