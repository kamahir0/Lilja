mod json_rpc;
mod mcp;
mod tools;

use anyhow::Result;
use json_rpc::Message;
use serde_json::Value;
use std::io::{self, BufRead, Write};

#[tokio::main]
async fn main() -> Result<()> {
    let stdin = io::stdin().lock();
    let mut stdout = io::stdout().lock();

    for line in stdin.lines() {
        let line = line?;

        // 空行は無視
        if line.trim().is_empty() {
            continue;
        }

        match serde_json::from_str::<json_rpc::Message>(&line) {
            Ok(msg) => match msg {
                Message::Request(req) => match req.method.as_str() {
                    mcp::method::INITIALIZE => match handle_initialize(req.params) {
                        Ok(result) => {
                            let resp = json_rpc::response(req.id, result);
                            writeln!(stdout, "{}", serde_json::to_string(&resp).unwrap())?;
                        }
                        Err(e) => {
                            let err_resp = json_rpc::internal_error(e);
                            eprintln!("{}", serde_json::to_string(&err_resp).unwrap());
                        }
                    },
                    mcp::method::TOOLS_LIST => {
                        let result = handle_tools_list();
                        let resp = json_rpc::response(req.id, result);
                        writeln!(stdout, "{}", serde_json::to_string(&resp).unwrap())?;
                    }
                    mcp::method::TOOLS_CALL => match handle_tools_call(req.params) {
                        Ok(result) => {
                            let resp = json_rpc::response(req.id, result);
                            writeln!(stdout, "{}", serde_json::to_string(&resp).unwrap())?;
                        }
                        Err(e) => {
                            let err_resp = json_rpc::internal_error(e);
                            eprintln!("{}", serde_json::to_string(&err_resp).unwrap());
                        }
                    },
                    _ => {
                        let err_msg = json_rpc::method_not_found();
                        eprintln!("{}", serde_json::to_string(&err_msg).unwrap());
                    }
                },
                Message::Notification(notification) => {
                    eprintln!("Notification: {}", serde_json::to_string(&notification).unwrap());
                }
                _ => {
                    let err_msg = json_rpc::invalid_request();
                    eprintln!("{}", serde_json::to_string(&err_msg).unwrap());
                }
            },
            Err(_) => {
                let err_msg = json_rpc::parse_error();
                eprintln!("{}", serde_json::to_string(&err_msg).unwrap());
            }
        }
    }
    Ok(())
}

/// 初期化
fn handle_initialize(params: Option<Value>) -> std::result::Result<Value, String> {
    // パラメータのパース（必須ではないが、クライアントが求めるバージョンを確認できる）
    let req_params: Option<mcp::schema::initialize::Params> = params
        .map(|p| serde_json::from_value(p))
        .transpose()
        .map_err(|e| format!("Invalid parameters: {}", e))?;

    // クライアントが送ってきたバージョンを確認（必要であれば）
    let protocol_version = req_params
        .map(|p| p.protocol_version)
        .unwrap_or_else(|| mcp::schema::initialize::ProtocolVersion("2024-11-05".to_string()));

    let result = mcp::schema::initialize::Result {
        protocol_version,
        capabilities: mcp::schema::initialize::ServerCapabilities { tools: Some(serde_json::json!({})) },
        server_info: mcp::schema::initialize::Implementation {
            name: "lilja-mcp-server".to_string(),
            version: "0.1.0".to_string(),
        },
    };

    Ok(serde_json::to_value(result).unwrap())
}

/// ツール一覧
fn handle_tools_list() -> Value {
    let tools = vec![
        tools::tool_to_schema(tools::math::AddTool),
        tools::tool_to_schema(tools::math::SubtractTool),
        tools::tool_to_schema(tools::math::MultiplyTool),
        tools::tool_to_schema(tools::math::DivideTool),
    ];
    let result = mcp::schema::tools_list::Result { tools };
    serde_json::to_value(result).unwrap()
}

/// ツールを実行
fn handle_tools_call(params: Option<Value>) -> std::result::Result<Value, String> {
    let params: mcp::schema::tools_call::Params = serde_json::from_value(params.ok_or_else(|| "Missing parameters".to_string())?).map_err(|e| format!("Invalid parameters: {}", e))?;

    let args_value = params
        .arguments
        .unwrap_or_else(|| serde_json::json!({}));

    match params.name.as_str() {
        "add" => tools::execute_tool(tools::math::AddTool, args_value),
        "subtract" => tools::execute_tool(tools::math::SubtractTool, args_value),
        "multiply" => tools::execute_tool(tools::math::MultiplyTool, args_value),
        "divide" => tools::execute_tool(tools::math::DivideTool, args_value),
        _ => Err(format!("Unknown tool: {}", params.name.as_str())),
    }
}
