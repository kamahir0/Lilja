# デバッグメニュー自作してみた Slidev Slides

Slidev に移行したスライドデッキ。本文は `slides.md` に集約する。

## 構成

- `slides.md`: スライド本文。`---` 区切りでページを分ける。
- `style.css`: Slidev のグローバルCSS。`assets/theme.png` を共通背景として使う。
- `slide-bottom.vue`: ページ番号表示用のSlidevレイヤー。
- `assets/theme.png`: 共通背景画像。
- `assets/images`: 画像置き場。参照しているファイルを同名で差し替える。
- `export-slidev.ps1`: 静的HTML、PDF、PPTX をまとめて出力する。
- `export-slidev-images.ps1`: `dist/png` を掃除してから PNG を出力する。
- `package.json`: Slidev CLI 用の npm scripts。
- `dist`: PDF / PPTX の出力先。
- `dist/html`: 静的HTMLの出力先。
- `dist/png`: PNG 連番画像の出力先。

## 確認

初回だけ依存を入れる。

```powershell
npm install
```

プレビュー用サーバーを起動する。

```powershell
npm run deck
```

## 出力

PowerShell から静的HTML、PDF、PPTX をまとめて出力する。

```powershell
.\export-slidev.ps1
```

同じ処理は npm script からも呼べる。

```powershell
npm run export
```

個別に出力する場合:

```powershell
npm run build
npm run pdf
npm run pptx
```

出力先:

- `dist/debug-menu-built-from-scratch.pdf`
- `dist/debug-menu-built-from-scratch.pptx`
- `dist/html`

静的HTMLだけ出力する場合:

```powershell
npm run build
```

出力先:

- `dist/html`

PNG が必要な場合:

```powershell
npm run images
```

出力名は `dist/png/01.png` のようにページ番号付きになる。

## 編集方針

- スライド順は `slides.md` の並び順で決まる。
- 1ページを追加する場合は `---` で区切って追記する。
- 文章量が増えたらページを分ける。
- 見た目は `style.css` の Slidev 用スタイルで調整する。
- 画像は `assets/images` に置き、`slides.md` の `<img src="...">` から参照する。

## 画像差し替え

現在の差し替え想定:

- `assets/images/runtime-menu.svg`
- `assets/images/editor-window.svg`
- `assets/images/dark-theme.svg`
- `assets/images/unity-debug-sheet-buttons.svg`

PNG など別形式に差し替える場合は、ファイルを置いた上で `slides.md` の拡張子だけ変更する。
