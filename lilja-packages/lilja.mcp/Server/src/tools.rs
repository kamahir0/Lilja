// ---------------------------------------------------------------
// tools.rs — MCPツール定義
//
// 各ツールの名前・説明・入力スキーマをここに集約する。
// ツールを追加・変更する場合はこのファイルのみ編集すればよい。
// ---------------------------------------------------------------

use serde_json::json;

use crate::protocol::{ToolDefinition, ToolInputSchema};

/// 全ツール定義の一覧を返す
pub fn get_tool_definitions() -> Vec<ToolDefinition> {
    vec![
        // --------------------------------------------------
        //  compile_project — プロジェクトのコンパイルを実行
        // --------------------------------------------------
        ToolDefinition {
            name: "compile_project".to_string(),
            description: "Unityプロジェクトのスクリプトコンパイルをトリガーします。".to_string(),
            input_schema: ToolInputSchema {
                schema_type: "object".to_string(),
                properties: Some(json!({})),
                required: None,
            },
        },
        // --------------------------------------------------
        //  create_scene — 新しいシーンを作成
        // --------------------------------------------------
        ToolDefinition {
            name: "create_scene".to_string(),
            description: "新しいUnityシーンを作成します。".to_string(),
            input_schema: ToolInputSchema {
                schema_type: "object".to_string(),
                properties: Some(json!({
                    "name": {
                        "type": "string",
                        "description": "作成するシーンの名前"
                    }
                })),
                required: None,
            },
        },
        // --------------------------------------------------
        //  get_hierarchy — ヒエラルキー情報を取得
        // --------------------------------------------------
        ToolDefinition {
            name: "get_hierarchy".to_string(),
            description: "現在のシーンのヒエラルキー情報をJSON形式で取得します。".to_string(),
            input_schema: ToolInputSchema {
                schema_type: "object".to_string(),
                properties: Some(json!({})),
                required: None,
            },
        },
        // --------------------------------------------------
        //  instantiate_prefab — プレハブをシーンに配置
        // --------------------------------------------------
        ToolDefinition {
            name: "instantiate_prefab".to_string(),
            description: "指定パスのプレハブをシーンにインスタンス化して配置します。".to_string(),
            input_schema: ToolInputSchema {
                schema_type: "object".to_string(),
                properties: Some(json!({
                    "path": {
                        "type": "string",
                        "description": "プレハブのアセットパス（例: Assets/Prefabs/Player.prefab）"
                    }
                })),
                required: Some(vec!["path".to_string()]),
            },
        },
        // --------------------------------------------------
        //  get_console_logs — コンソールログを取得
        // --------------------------------------------------
        ToolDefinition {
            name: "get_console_logs".to_string(),
            description: "Unityコンソールの直近ログを取得します。".to_string(),
            input_schema: ToolInputSchema {
                schema_type: "object".to_string(),
                properties: Some(json!({
                    "count": {
                        "type": "integer",
                        "description": "取得するログの最大件数（デフォルト: 50）"
                    }
                })),
                required: None,
            },
        },
    ]
}
