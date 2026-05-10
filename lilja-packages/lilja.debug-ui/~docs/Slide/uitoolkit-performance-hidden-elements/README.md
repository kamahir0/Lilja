# デバッグメニュー自作してみた Marp Slides

Marp に移行したスライドデッキ。本文は `slides.md` に集約する。

## 構成

- `slides.md`: スライド本文。`---` 区切りでページを分ける。
- `theme.css`: Marp カスタムテーマ。`assets/theme.png` を共通背景として使う。
- `assets/theme.png`: 共通背景画像。
- `assets/images`: 画像置き場。参照しているファイルを同名で差し替える。
- `export-marp.ps1`: HTML と PDF をまとめて出力する。
- `export-images.ps1`: `dist/png` を掃除してから PNG を出力する。
- `package.json`: Marp CLI 用の npm scripts。
- `dist`: PDF / PPTX / HTML の出力先。
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

## PDF / HTML 出力

PowerShell からまとめて出力する。

```powershell
.\export-marp.ps1
```

または npm scripts を使う。

```powershell
npm run html
npm run pdf
```

出力先:

- `dist/debug-menu-built-from-scratch.html`
- `dist/debug-menu-built-from-scratch.pdf`

PPTX が必要な場合:

```powershell
npm run pptx
```

PNG が必要な場合:

```powershell
npm run images
```

出力名は `dist/png/debug-menu-built-from-scratch.001.png` のようにページ番号付きになる。

## 編集方針

- スライド順は `slides.md` の並び順で決まる。
- 1ページを追加する場合は `---` で区切って追記する。
- 文章量が増えたらページを分ける。
- 見た目は `theme.css` の Marp テーマで調整する。
- 画像は `assets/images` に置き、`slides.md` の `<img src="...">` から参照する。

## 画像差し替え

現在の差し替え想定:

- `assets/images/runtime-menu.svg`
- `assets/images/editor-window.svg`
- `assets/images/dark-theme.svg`
- `assets/images/unity-debug-sheet-buttons.svg`

PNG など別形式に差し替える場合は、ファイルを置いた上で `slides.md` の拡張子だけ変更する。
