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
## Packages

UPM経由でパッケージをインストールする際は、Package Manager の "Add package from git URL..." から以下のURLを入力してください。

### [Lilja.AssetManagement](./lilja-packages/lilja.asset-management)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.asset-management/src/Lilja.AssetManagement
```

### [Lilja.DebugUI](./lilja-packages/lilja.debug-ui)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.debug-ui/src/Lilja.DebugUI
```

### [Lilja.DevKit](./lilja-packages/lilja.dev-kit)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.dev-kit/src/Lilja.DevKit
```

### [Lilja.EditorEx](./lilja-packages/lilja.editor-ex)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.editor-ex/src/Lilja.EditorEx
```

### [Lilja.FancyScrollView](./lilja-packages/lilja.fancy-scroll-view)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.fancy-scroll-view/src/Lilja.FancyScrollView
```

### [Lilja.Repository](./lilja-packages/lilja.repository)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.repository/src/Lilja.Repository
```

### [Lilja.ScreenManagement](./lilja-packages/lilja.screen-management)
```text
https://github.com/kamahir0/Lilja.git?path=lilja-packages/lilja.screen-management/src/Lilja.ScreenManagement
```
