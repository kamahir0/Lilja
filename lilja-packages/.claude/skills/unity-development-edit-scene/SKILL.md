---
name: unity-development-edit-scene
description: >-
  Use when creating or modifying GameObjects and scenes in Unity through
  UniCli: creating new GameObjects (GameObject.Create), adding or removing
  components (GameObject.AddComponent, GameObject.RemoveComponent), reparenting
  objects (GameObject.SetParent), or opening and saving scenes (Scene.Open,
  Scene.Save). Always run AssetDatabase.Import after file changes and Compile
  after C# edits.
metadata:
  version: "1.0.0"
---

# UniCli — Unity Editor CLI (Edit Scene)

## RULES

- **After creating/modifying ANY file under `Assets/` or `Packages/`**: Run `unicli exec AssetDatabase.Import --path "<path>" --json`. Never create `.meta` files manually — skipping this causes missing references and broken imports.
- **After modifying C# code**: Run `unicli exec Compile --json`.

## Key Workflows

**Discover scene and GameObject commands:**

```bash
unicli commands --json | grep -iE "scene|gameobject|component|parent"
```

**Create a GameObject and configure it:**

```bash
unicli exec GameObject.Create --name "SpawnPoint" --json
GO_ID=$(unicli exec GameObject.Find --namePattern "SpawnPoint" --json | jq -r '.results[0].instanceId')
unicli exec GameObject.AddComponent --instanceId "$GO_ID" --typeName "UnityEngine.BoxCollider" --json
unicli exec GameObject.SetParent --instanceId "$GO_ID" --parentPath "Environment" --json
```

**Open and save a scene:**

```bash
unicli exec Scene.Open --path "Assets/Scenes/Level1.unity" --json
unicli exec Scene.Save --json
```

**Typical editing sequence:**

```bash
unicli exec GameObject.Create --name "Enemy" --json
GO_ID=$(unicli exec GameObject.Find --namePattern "Enemy" --json | jq -r '.results[0].instanceId')
unicli exec GameObject.AddComponent --instanceId "$GO_ID" --typeName "UnityEngine.CapsuleCollider" --json
unicli exec GameObject.SetParent --instanceId "$GO_ID" --parentPath "Enemies" --json
unicli exec Scene.Save --json
```
