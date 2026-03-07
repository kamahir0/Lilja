use super::ToolName;
use serde::{Deserialize, Serialize};
use serde_json::Value;

pub const METHOD: &str = "tools/call";

// 5. CallTool Params (どのツールを、何の引数で呼ぶか)
#[derive(Deserialize, Debug)]
pub struct Params {
    pub name: ToolName,
    pub arguments: Option<Value>,
}

// 6. CallTool Result (実行結果をどう見せるか)
#[derive(Serialize, Debug)]
pub struct Result {
    pub content: Vec<Content>,
    #[serde(rename = "isError")]
    pub is_error: bool,
}

#[derive(Serialize, Debug)]
#[serde(tag = "type")]
pub enum Content {
    #[serde(rename = "text")]
    Text { text: String },
}
