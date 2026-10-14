# Lilja

> [!IMPORTANT]
> **Liljaの各パッケージは独立した個別リポジトリへ移行しました。**
> 今後の開発・更新は各新リポジトリにて行われます。パッケージの導入も新しいGit URLをご利用ください。

## 📦 独立リポジトリ一覧（移行先）

| パッケージ | 新リポジトリ | UPM Package ID | インストールURL (Git URL) |
|---|---|---|---|
| **AssetManagement** | [kamahir0-lilja/AssetManagement](https://github.com/kamahir0-lilja/AssetManagement) | `com.kamahir0.asset-management` | `https://github.com/kamahir0-lilja/AssetManagement.git?path=/Packages/com.kamahir0.asset-management` |
| **DebugUI** | [kamahir0-lilja/DebugUI](https://github.com/kamahir0-lilja/DebugUI) | `com.kamahir0.debug-ui` | `https://github.com/kamahir0-lilja/DebugUI.git?path=/Packages/com.kamahir0.debug-ui` |
| **DevKit** | [kamahir0-lilja/DevKit](https://github.com/kamahir0-lilja/DevKit) | `com.kamahir0.dev-kit` | `https://github.com/kamahir0-lilja/DevKit.git?path=/Packages/com.kamahir0.dev-kit` |
| **EditorEx** | [kamahir0-lilja/EditorEx](https://github.com/kamahir0-lilja/EditorEx) | `com.kamahir0.editor-ex` | `https://github.com/kamahir0-lilja/EditorEx.git?path=/Packages/com.kamahir0.editor-ex` |
| **FancyScrollView** | [kamahir0-lilja/FancyScrollView](https://github.com/kamahir0-lilja/FancyScrollView) | `com.kamahir0.fancy-scroll-view` | `https://github.com/kamahir0-lilja/FancyScrollView.git?path=/Packages/com.kamahir0.fancy-scroll-view` |
| **Repository** | [kamahir0-lilja/Repository](https://github.com/kamahir0-lilja/Repository) | `com.kamahir0.repository` | `https://github.com/kamahir0-lilja/Repository.git?path=/Packages/com.kamahir0.repository` |
| **ScreenManagement** | [kamahir0-lilja/ScreenManagement](https://github.com/kamahir0-lilja/ScreenManagement) | `com.kamahir0.screen-management` | `https://github.com/kamahir0-lilja/ScreenManagement.git?path=/Packages/com.kamahir0.screen-management` |

---

## （旧構成）ディレクトリ構成

個人開発用のUnityパッケージ群をまとめたリポジトリです。

## ディレクトリ構成

### lilja-packages

- Liljaパッケージを格納するディレクトリです。
- 各パッケージは `lilja.package-name` という命名規則で配置されます

## Moved Packages

### MasterData

MasterData は独立リポジトリへ移動しました。

```text
https://github.com/kamahir0/MasterData
```

## Lilja.DevKit

Lilja開発用のツールパッケージです。

- **位置づけ**: Liljaパッケージを使用するゲームプロジェクトはインポート不要。Lilja開発用プロジェクトで使用します。

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
