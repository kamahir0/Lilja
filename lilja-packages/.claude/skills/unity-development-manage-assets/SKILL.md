---
name: unity-development-manage-assets
description: >-
  Use when managing Unity project assets through UniCli: importing assets
  (AssetDatabase.Import), creating materials or other asset files
  (Material.Create), deleting assets (AssetDatabase.Delete), creating or
  modifying Assembly Definition files (AssemblyDefinition.Create,
  AssemblyDefinition.AddReference), compiling C# code (Compile), or running
  EditMode/PlayMode tests (TestRunner.RunEditMode, TestRunner.RunPlayMode).
  Always run AssetDatabase.Import after file changes and Compile after C# edits.
metadata:
  version: "1.0.0"
---

# UniCli — Unity Editor CLI (Manage Assets)

## RULES

- **After creating/modifying ANY file under `Assets/` or `Packages/`**: Run `unicli exec AssetDatabase.Import --path "<path>" --json`. Never create `.meta` files manually. Applies to `.cs`, `.asmdef`, `.asset`, `.prefab`, directories, etc.
- **After modifying C# code**: Run `unicli exec Compile --json`.
- **For platform-specific verification**: Use `unicli exec BuildPlayer.Compile --target <platform> --json` to catch platform-specific errors.
- **When running tests**: Use `--resultFilter failures` (or `--resultFilter none` for summary-only). Use `--stackTraceLines 3` when diagnosing failures.

## Key Workflows

**Compile and run tests:**

```bash
unicli exec Compile --json
unicli exec TestRunner.RunEditMode --resultFilter failures --json
unicli exec TestRunner.RunPlayMode --resultFilter failures --stackTraceLines 3 --json
unicli exec TestRunner.RunEditMode --resultFilter none --json  # summary only (no per-test entries)
```

**Import and manage assets:**

```bash
unicli exec AssetDatabase.Import --path "Assets/Textures/MyTexture.png" --json
unicli exec AssetDatabase.Import --path "Assets/Textures" --json  # whole directory
unicli exec AssetDatabase.Delete --path "Assets/Old/Texture.png" --json
unicli exec Material.Create --assetPath "Assets/Materials/NewMaterial.mat" --shader "Standard" --json
```

> **Note**: There is no built-in `MoveAsset` command. To move or rename an asset, use `Eval` with `AssetDatabase.MoveAsset()`:
> ```bash
> unicli exec Eval --declarations "using UnityEditor;" \
>   --code 'AssetDatabase.MoveAsset("Assets/Old/Texture.png", "Assets/New/Texture.png");' --json
> ```

**Assembly Definition operations:**

```bash
unicli exec AssemblyDefinition.Create \
  --name "MyProject.UniCli.Editor" \
  --directory "Assets/Editor/UniCli" \
  --includePlatforms Editor --json
unicli exec AssemblyDefinition.AddReference \
  --name "MyProject.UniCli.Editor" \
  --reference "UniCli.Server.Editor" --json
unicli exec AssetDatabase.Import --path "Assets/Editor/UniCli" --json
unicli exec Compile --json
```
