---
name: unity-development-read-project
description: >-
  Use when inspecting the Unity project through UniCli without modifying any
  assets: finding GameObjects (GameObject.Find, GameObject.GetHierarchy),
  reading console logs (Console.GetLog), listing or connecting to players
  (Connection.List, Connection.Status, Connection.Connect), or invoking remote
  debug commands on a connected Development Build (Remote.List, Remote.Invoke).
  This skill is read-only — no AssetDatabase.Import or Compile is needed.
metadata:
  version: "1.0.0"
---

# UniCli — Unity Editor CLI (Read Project)

## RULES

- **Console logs**: Use `--logType "Warning"` or `--logType "Error"` to filter noise. Use `--stackTraceLines 3` when diagnosing errors.
- **Read-only**: Do not run `AssetDatabase.Import`, `Compile`, or any file-modifying commands. Use `unity-development-edit-scene`, `unity-development-manage-assets`, or `unity-development-sync-properties` for write operations.

## Key Workflows

**Find GameObjects and inspect hierarchy:**

```bash
unicli commands --json | grep -iE "find|hierarchy"
unicli exec GameObject.Find --namePattern "Player" --json
unicli exec GameObject.GetHierarchy --json
```

**Check console logs:**

```bash
unicli exec Console.GetLog --logType "Warning" --json
unicli exec Console.GetLog --logType "Error" --stackTraceLines 3 --json
```

**Player connection:**

```bash
unicli exec Connection.List --json
unicli exec Connection.Connect '{"id":-1}' --json               # by player ID
unicli exec Connection.Connect '{"ip":"192.168.1.100"}' --json  # by IP
unicli exec Connection.Connect '{"deviceId":"SERIAL"}' --json   # by device serial
unicli exec Connection.Status --json
```

**Remote debug commands (requires `UNICLI_REMOTE` define + Development Build with Autoconnect Profiler):**

```bash
unicli exec Remote.List --json
unicli exec Remote.Invoke '{"command":"Debug.Stats"}' --json
unicli exec Remote.Invoke '{"command":"Debug.GetPlayerPref","data":"{\"key\":\"HighScore\",\"type\":\"int\"}"}' --json
```

Built-in debug commands: `Debug.SystemInfo`, `Debug.Stats`, `Debug.GetLogs`, `Debug.GetHierarchy`, `Debug.FindGameObjects`, `Debug.GetScenes`, `Debug.GetPlayerPref`
