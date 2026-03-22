---
name: unity-development-sync-properties
description: >-
  Use when updating serialized field values on Unity components through UniCli
  (Component.SetProperty), or when applying prefab overrides and saving
  modifications back to disk (Prefab.Apply). For ScriptableObject fields, use
  Eval with SerializedObject. Always run AssetDatabase.Import after modifying
  prefab or asset files.
metadata:
  version: "1.0.0"
---

# UniCli — Unity Editor CLI (Sync Properties)

## RULES

- **After modifying ANY prefab or asset file**: Run `unicli exec AssetDatabase.Import --path "<path>" --json`.
- **After modifying C# code** (only when scripts are also changed): Run `unicli exec Compile --json`. Not required for property-only changes.
- **`Component.SetProperty` takes `componentInstanceId`** — not the GameObject's instance ID. Use `GameObject.GetComponents` first to retrieve the component's instance ID.

## Key Workflows

**Discover property and prefab commands:**

```bash
unicli commands --json | grep -iE "serial|prefab|property|component"
```

**Set a serialized field value on a component:**

```bash
# Step 1: Get the component's instance ID
GO_ID=$(unicli exec GameObject.Find --namePattern "Player" --json | jq -r '.results[0].instanceId')
COMP_ID=$(unicli exec GameObject.GetComponents --instanceId "$GO_ID" --json \
  | jq -r '.components[] | select(.typeName | endswith("PlayerController")) | .instanceId')

# Step 2: Set the property
unicli exec Component.SetProperty \
  --componentInstanceId "$COMP_ID" \
  --propertyPath "moveSpeed" \
  --value "5.0" --json
```

**Update a ScriptableObject field (use Eval — Component.SetProperty does not support assets):**

```bash
unicli exec Eval \
  --declarations "using UnityEditor;" \
  --code 'var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/GameConfig.asset"); var sp = new SerializedObject(so); sp.FindProperty("maxHealth").intValue = 100; sp.ApplyModifiedProperties(); AssetDatabase.SaveAssets();' \
  --json
unicli exec AssetDatabase.Import --path "Assets/Data/GameConfig.asset" --json
```

**Apply prefab overrides:**

```bash
GO_ID=$(unicli exec GameObject.Find --namePattern "Enemy" --json | jq -r '.results[0].instanceId')
unicli exec Prefab.Apply --instanceId "$GO_ID" --json
unicli exec AssetDatabase.Import --path "Assets/Prefabs/Enemy.prefab" --json
```

**Typical property sync sequence:**

```bash
GO_ID=$(unicli exec GameObject.Find --namePattern "Player" --json | jq -r '.results[0].instanceId')
COMP_ID=$(unicli exec GameObject.GetComponents --instanceId "$GO_ID" --json \
  | jq -r '.components[] | select(.typeName | endswith("PlayerController")) | .instanceId')
unicli exec Component.SetProperty \
  --componentInstanceId "$COMP_ID" \
  --propertyPath "jumpHeight" \
  --value "3.5" --json
unicli exec Prefab.Apply --instanceId "$GO_ID" --json
unicli exec AssetDatabase.Import --path "Assets/Prefabs/Player.prefab" --json
```
