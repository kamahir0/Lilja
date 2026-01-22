# Lilja

個人開発用のUnityパッケージ群をまとめたリポジトリです。

ライブラリ名の意味は "Library Layer of Joint-Architecture" -> "Lilja" です。

## ディレクトリ構成

### lilja-packages

Liljaパッケージを格納するディレクトリです。

- 各パッケージは `Lilja.Hoge` という命名規則で配置されます
- パッケージごとのディレクトリがUnityプロジェクトになっています
- パッケージ本体のコードはローカルパッケージとして `Packages` 配下に収録されます
- テスト用のコードやシーン等は `Assets` 配下に配置します

### sandbox

自由にUnityプロジェクトを配置できるディレクトリです。

- 複数のLiljaパッケージを組み合わせて動作確認したい場合などに使用します
- 実験的なプロジェクトや一時的な検証に活用できます

## Lilja.DevKit

Lilja開発用のツールパッケージです。

- **位置づけ**: Liljaパッケージを使用するプロジェクトはインポート不要。sandbox配下のプロジェクトなど、Lilja開発用プロジェクトで使用します。
- **機能**:
  - **Package Creator** (`Window > Lilja > Package Creator`): 新しいLiljaパッケージを `lilja-packages` 直下に作成

### 命名規則

Package Creatorで生成されるパッケージは以下の命名規則に従います：

| 項目             | 形式                                      | 例（入力: FooBar）            |
| ---------------- | ----------------------------------------- | ----------------------------- |
| DisplayName      | Lilja.{PascalCase}                        | `Lilja.FooBar`                |
| パッケージ名     | com.{OrganizationName}.lilja.{kebab-case} | `com.kamahir0.lilja.foo-bar`  |
| 出力ディレクトリ | lilja-packages/Lilja.{PascalCase}         | `lilja-packages/Lilja.FooBar` |