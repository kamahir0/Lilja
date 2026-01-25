# Lilja

個人開発用のUnityパッケージ群をまとめたリポジトリです。

## ディレクトリ構成

### lilja-packages

- Liljaパッケージを格納するディレクトリです。
- 各パッケージは `lilja.package-name` という命名規則で配置されます

### sandbox

自由にUnityプロジェクトを配置できるディレクトリです。

- Liljaパッケージを動作確認しながら開発するために使用します
- 実験的なプロジェクトや一時的な検証に活用できます

## Lilja.DevKit

Lilja開発用のツールパッケージです。

- **位置づけ**: Liljaパッケージを使用するゲームプロジェクトはインポート不要。sandbox配下のプロジェクトなど、Lilja開発用プロジェクトで使用します。

## 命名規則

Package Creatorで生成されるパッケージは以下の命名規則に従います：

| 項目             | 形式                                        | 例（入力: FooBar）             |
| ---------------- | ------------------------------------------- | ------------------------------ |
| DisplayName      | Lilja.{PackageName}                         | `Lilja.FooBar`                 |
| パッケージ名     | com.{OrganizationName}.lilja.{package-name} | `com.kamahir0.lilja.foo-bar`   |
| 出力ディレクトリ | lilja-packages/lilja.{package-name}         | `lilja-packages/lilja.foo-bar` |