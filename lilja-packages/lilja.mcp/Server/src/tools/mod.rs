pub mod math;

use crate::mcp;
use schemars::JsonSchema;
use serde::de::DeserializeOwned;
use serde_json::Value;

pub trait Tool {
    type Args: DeserializeOwned + JsonSchema;

    fn name(&self) -> &'static str;
    fn description(&self) -> &'static str;

    // スキーマはArgsの型から自動生成
    fn input_schema(&self) -> Value {
        let schema = schemars::schema_for!(Self::Args);
        serde_json::to_value(schema).unwrap()
    }

    fn handle(&self, args: Self::Args) -> Result<Value, String>;
}

// ヘルパー関数: 個別のToolをリスト用定義に変換
pub fn tool_to_schema<T: Tool>(tool: T) -> mcp::schema::tools_list::Tool {
    mcp::schema::tools_list::Tool {
        name: tool.name().into(),
        description: tool.description().to_string(),
        input_schema: tool.input_schema(),
    }
}

// ヘルパー関数: Valueをパースして型付きhandleに渡す
pub fn execute_tool<T: Tool>(tool: T, args_value: Value) -> Result<Value, String> {
    let typed_args = serde_json::from_value::<T::Args>(args_value).map_err(|e| format!("Invalid arguments: {}", e))?;

    let result_value = tool.handle(typed_args)?;

    let text = match result_value {
        Value::String(s) => s,
        v => v.to_string(),
    };

    let result = mcp::schema::tools_call::Result {
        content: vec![mcp::schema::tools_call::Content::Text { text }],
        is_error: false,
    };
    Ok(serde_json::to_value(result).unwrap())
}
