// ---------------------------------------------------------------
// unity_client.rs — Unity との通信 (ファイルベース)
//
// HTTPの代わりに、特定のディレクトリにファイルを書き出し、
// Unity側が書き出すレスポンスファイルを監視して結果を受け取る。
// ---------------------------------------------------------------

use anyhow::{Context, Result};
use serde_json::{json, Value};
use std::path::PathBuf;
use std::time::Duration;
use tokio::fs;

/// 通信に使用するベースディレクトリ (プロジェクトルートからの相対パス)
const COMM_DIR: &str = "mcp_comm";

/// Unityにコマンドを送信し、レスポンスをファイル経由で受け取る
///
/// # 引数
/// - `command` — 実行するコマンド名
/// - `args` — コマンドに渡す引数
///
/// # 戻り値
/// - Unity側の応答を `serde_json::Value` として返す
pub async fn send_command(command: &str, args: Option<Value>) -> Result<Value> {
    // 1. ディレクトリの準備
    let base_path = PathBuf::from(COMM_DIR);
    let req_dir = base_path.join("requests");
    let res_dir = base_path.join("responses");

    fs::create_dir_all(&req_dir)
        .await
        .context("リクエストディレクトリの作成に失敗しました")?;
    fs::create_dir_all(&res_dir)
        .await
        .context("レスポンスディレクトリの作成に失敗しました")?;

    // 2. リクエストIDの生成 (簡易的にタイムスタンプを使用)
    let request_id = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)?
        .as_nanos();

    let req_file = req_dir.join(format!("req_{}.json", request_id));
    let res_file = res_dir.join(format!("res_{}.json", request_id));

    // 3. リクエストの書き込み
    let payload = json!({
        "id": request_id.to_string(), // 数値が大きすぎる可能性を考慮して文字列化
        "command": command,
        "args": args
    });
    fs::write(&req_file, serde_json::to_string(&payload)?).await?;

    // 4. レスポンスの待機 (ポーリング)
    // 最大 10 秒間、0.1 秒間隔でファイルの出現をチェック
    let mut attempts = 0;
    let max_attempts = 100;
    let delay = Duration::from_millis(100);

    while attempts < max_attempts {
        if fs::metadata(&res_file).await.is_ok() {
            // ファイルが見つかったら読み取り
            let content = fs::read_to_string(&res_file).await?;
            let value: Value =
                serde_json::from_str(&content).unwrap_or_else(|_| Value::String(content.clone()));

            // 後処理: リクエストとレスポンスファイルを削除
            let _ = fs::remove_file(&req_file).await;
            let _ = fs::remove_file(&res_file).await;

            return Ok(value);
        }
        tokio::time::sleep(delay).await;
        attempts += 1;
    }

    // タイムアウトした場合はリクエストファイルを削除
    let _ = fs::remove_file(&req_file).await;
    anyhow::bail!(
        "Unityからのレスポンス待ちでタイムアウトしました: {}",
        command
    );
}
