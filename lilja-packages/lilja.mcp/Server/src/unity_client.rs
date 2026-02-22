// ---------------------------------------------------------------
// unity_client.rs — Unity HTTPブリッジとの通信
//
// Unity側で起動している localhost:8080 のHTTPサーバーへ
// コマンドをPOSTし、結果を受け取る。
// ---------------------------------------------------------------

use anyhow::Result;
use serde_json::{json, Value};

/// UnityブリッジサーバーのURL
const UNITY_URL: &str = "http://localhost:8080/";

/// Unityにコマンドを送信し、レスポンスボディを返す
///
/// # 引数
/// - `command` — 実行するコマンド名（例: "get_hierarchy"）
/// - `args` — コマンドに渡す引数（JSON値、なければ None）
///
/// # 戻り値
/// - Unity側の応答をそのまま `serde_json::Value` として返す
pub async fn send_command(command: &str, args: Option<Value>) -> Result<Value> {
    // Unityが期待するリクエストボディの形式
    let payload = json!({
        "command": command,
        "args": args
    });

    let client = reqwest::Client::new();
    let response = client.post(UNITY_URL).json(&payload).send().await?;

    // HTTPステータスの確認
    let status = response.status();
    let body = response.text().await?;

    if !status.is_success() {
        anyhow::bail!("Unity returned HTTP {}: {}", status.as_u16(), body);
    }

    // JSONとしてパースを試み、失敗した場合は文字列として返す
    let value: Value = serde_json::from_str(&body).unwrap_or(Value::String(body));
    Ok(value)
}
