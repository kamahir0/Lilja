# Unity MCP Server Implementation Plan

## 概要

`lilja.mcp` は、CursorなどのAIエディタからUnityプロジェクトを直接操作するためのMCP (Model Context Protocol) サーバー実装です。
Unityエディタ自体がMCPサーバーとして振る舞うのではなく、Rust製の外部プロセスがMCPサーバーとして機能し、Unityエディタ内のHTTPサーバーと通信することで間接的にUnityを操作します。

## アーキテクチャ

```mermaid
graph LR
    Cursor[MCP Client (Cursor)] -- Stdio --> RustServer[Rust MCP Server]
    RustServer -- HTTP (localhost:8080) --> Unity[Unity Editor (C#)]
    Unity -- C# API --> Project[Unity Project]
```

## Unity Package (`com.kamahir0.lilja.mcp`)

### 責務
- Unityエディタ起動時にバックグラウンドでHTTPサーバーを立ち上げる (`InitializeOnLoad`)
- Rustサーバーからのリクエストを受け取り、メインスレッドでUnity APIを実行する

### 実装機能
- **Compilation**: スクリプトのコンパイルをトリガー
- **Scene**: シーンの作成、保存、読み込み
- **Hierarchy**: 現在のシーンのヒエラルキー情報の取得
- **GameObject**: ゲームオブジェクトの作成、プレハブのインスタンス化、コンポーネント操作
- **Console**: コンソールログの取得

## Rust MCP Server (`Server`)

### 責務
- MCPプロトコルの実装 (Stdio transport)
- Unityエディタとの通信 (HTTP Client)
- Unityが起動していない場合のハンドリング (Headlessモードでの起動など、将来的な展望)

### ツール定義 (Tools)
- `compile_project`: コンパイルを実行
- `create_scene`: 新しいシーンを作成
- `get_hierarchy`: ヒエラルキー情報をJSONで返す
- `instantiate_prefab`: 指定パスのプレハブをシーンに配置
- `get_console_logs`: 最新のコンソールログを取得

## ディレクトリ構成

```
lilja-packages/lilja.mcp/
├── package.json
├── PLAN.md (本ファイル)
├── Server/ (Rust Project)
│   ├── Cargo.toml
│   └── src/
│       └── main.rs
└── src/ (Unity Package Source)
    ├── Editor/
    │   ├── Server/
    │   └── Tools/
    └── Runtime/
```

## 今後の展望 (Roadmap)
- AWS Lambda等での実行を見越したルーティング設計
- UnityのHeadlessモード起動のサポート

## ビルドと実行

### Unity側
1. Unityプロジェクトを開く。
2. `Lilja.Mcp` パッケージが読み込まれていることを確認。
3. `InitializeOnLoad` により自動的にサーバーが `http://localhost:8080/` で起動する。
4. コンソールに `[Lilja.MCP] Server started...` と表示されればOK。

### Rust側 (MCP Server)
1. `lilja-packages/lilja.mcp/Server` ディレクトリに移動。
2. `cargo build` を実行。
3. `cargo run` で実行し、標準入力にJSON-RPCリクエストを送信する。

例:
```json
{"jsonrpc": "2.0", "method": "get_hierarchy", "id": 1}
```
