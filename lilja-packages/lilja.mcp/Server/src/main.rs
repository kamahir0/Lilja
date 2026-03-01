// ---------------------------------------------------------------
// main.rs — Lilja MCP Server エントリーポイント
//
// 標準入力からJSON-RPCリクエストを1行ずつ読み取り、
// MCPプロトコルに従って処理し、標準出力にレスポンスを返す。
//
// MCPはstdioトランスポートを使用するため、
// ログは stderr に出力し、レスポンスは stdout に出力する。
// ---------------------------------------------------------------

mod handler;
mod protocol;
mod tools;
mod unity_client;

use anyhow::Result;
use std::io::{self, BufRead, Write};

use protocol::JsonRpcRequest;

#[tokio::main]
async fn main() -> Result<()> {
    let stdin = io::stdin();
    let mut stdout = io::stdout().lock();

    eprintln!("[MCP] Lilja MCP Server を起動しました");

    for line in stdin.lock().lines() {
        let line = line?;

        // 空行はスキップ
        if line.trim().is_empty() {
            continue;
        }

        // JSON-RPCリクエストのパース
        let request: JsonRpcRequest = match serde_json::from_str(&line) {
            Ok(req) => req,
            Err(e) => {
                eprintln!("[MCP] パースエラー: {}", e);

                // パースに失敗した場合はJSON-RPCのエラーレスポンスを返す
                let error = protocol::error_response(None, -32700, format!("Parse error: {}", e));
                let response_str = serde_json::to_string(&error)?;
                writeln!(stdout, "{}", response_str)?;
                stdout.flush()?;
                continue;
            }
        };

        // リクエストを処理（通知の場合は None が返る）
        if let Some(response) = handler::handle_request(&request).await {
            let response_str = serde_json::to_string(&response)?;
            writeln!(stdout, "{}", response_str)?;
            stdout.flush()?;
        }
    }

    eprintln!("[MCP] Lilja MCP Server を終了しました");
    Ok(())
}
