mod json_rpc;
mod mcp;

use anyhow::Result;
use json_rpc::Message;
use serde_json::Value;
use std::io::{self, BufRead};

#[tokio::main]
async fn main() -> Result<()> {
    let stdin = io::stdin().lock();
    let mut _stdout = io::stdout().lock();

    for line in stdin.lines() {
        let line = line?;

        // 空行は無視
        if line.trim().is_empty() {
            continue;
        }

        match serde_json::from_str::<json_rpc::Message>(&line) {
            Ok(msg) => match msg {
                Message::Request(req) => match req.method.as_str() {
                    mcp::method::INITIALIZE => {
                        // TODO: 実装
                        let err_msg = json_rpc::method_not_found();
                        println!("{}", serde_json::to_string(&err_msg).unwrap());
                    }
                    mcp::method::TOOLS_LIST => {
                        // TODO: ツール一覧の実装
                        let err_msg = json_rpc::method_not_found();
                        println!("{}", serde_json::to_string(&err_msg).unwrap());
                    }
                    mcp::method::TOOLS_CALL => {
                        // TODO: ツール実行の実装
                        let err_msg = json_rpc::method_not_found();
                        println!("{}", serde_json::to_string(&err_msg).unwrap());
                    }
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
