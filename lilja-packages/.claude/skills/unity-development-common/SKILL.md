---
name: unity-development-common
description: >-
  Use for UniCli basics that apply to all Unity Editor automation: verifying
  or installing the UniCli server (unicli check, unicli install), setting the
  project path (UNICLI_PROJECT), discovering and executing commands
  (unicli commands, unicli exec), and implementing custom CommandHandlers
  when built-in commands are insufficient.
metadata:
  version: "1.2.0"
---

# UniCli — Unity Editor CLI (Common)

UniCli interacts with Unity Editor via named pipes. The Editor must be open with `com.yucchiy.unicli-server` installed.

## Setup

```bash
unicli check                  # Verify CLI and server connection
unicli install                # Install server package if missing
unicli install --update       # Update if version mismatch
```

If `UNICLI_PROJECT` is not the current directory, set it:

```bash
export UNICLI_PROJECT=path/to/unity/project
```

## Rules — Always Follow

- **Always use `--json`** when parsing output programmatically.
- **On connection failure**: Retry 2–3 times, then ask the user to confirm the Editor is open.
- **Discover commands dynamically** — never rely on memorized lists:
  ```bash
  unicli commands --json | grep -i "<keyword>"
  unicli exec <command> --help
  ```

## Post-Operation Requirements

These are **non-optional**. Run the required commands before considering any task complete.

| Operation performed | Required commands | Order |
|---|---|---|
| `.cs` file created | `AssetDatabase.Import` → `Compile` | Must be in this order |
| `.cs` file deleted | `AssetDatabase.Import` → `Compile` | Must be in this order |
| `.cs` file edited | `Compile` only | Import not required |
| Directory created / moved / renamed (contains `.cs`) | `AssetDatabase.Import` → `Compile` | Must be in this order |
| `AssemblyDefinition` created or modified | `AssetDatabase.Import` → `Compile` | Must be in this order |
| Any of the above combined in one task | `AssetDatabase.Import` → `Compile` | Once at the end is sufficient |

```bash
# Import — refresh Unity's asset database and regenerate .csproj
unicli exec AssetDatabase.Import --path "<affected path>" --json

# Compile — verify zero errors before finishing
unicli exec Compile --json
```

`Compile` output must show zero errors. If errors are present, fix them before marking the task done.

## Pre-Completion Checklist

Before declaring a task complete, run through this checklist:

- [ ] Did this task create, delete, or move any `.cs` file or directory? → **Run `AssetDatabase.Import`**
- [ ] Did this task create, edit, or move any `.cs` file? → **Run `Compile` and confirm zero errors**
- [ ] Did this task create or modify an `AssemblyDefinition`? → **Run both**

## Command Execution

```bash
unicli exec <command> [--key value ...] --json
```

Repeat flags for arrays: `--options Development --options ConnectWithProfiler`

If no single command covers the goal, chain up to ~4 sequential commands:

```bash
unicli exec GameObject.Find --namePattern "MyObject" --json > /tmp/go.json
GO_ID=$(jq -r '.results[0].instanceId' /tmp/go.json)
unicli exec GameObject.GetComponents --instanceId "$GO_ID" --json
```

## Custom CommandHandlers

Use when built-in commands are insufficient. Provides type safety and discoverability.

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
unicli commands --json   # Verify new command appears
```

> **Idempotency note**: `AssemblyDefinition.Create` fails if the definition already exists.
> Before running it, check for an existing `.asmdef` file at the target path and skip
> creation if found. `AssetDatabase.Import` and `Compile` are always safe to re-run.

```csharp
using System.Threading;
using System.Threading.Tasks;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;

namespace MyProject.UniCli.Editor.Handlers
{
    [System.Serializable]
    public class MyRequest { public string targetName = ""; }

    [System.Serializable]
    public class MyResponse { public string result; }

    public sealed class MyCustomHandler : CommandHandler<MyRequest, MyResponse>
    {
        public override string CommandName => "MyCategory.MyAction";
        public override string Description => "Description shown in unicli commands";

        protected override ValueTask<MyResponse> ExecuteAsync(MyRequest request, CancellationToken cancellationToken)
        {
            return new ValueTask<MyResponse>(new MyResponse { result = $"Processed {request.targetName}" });
        }
    }
}
```

- Request/Response types must be `[Serializable]` with **public fields** (not properties) — required by `JsonUtility`
- Throw `CommandFailedException` on failure
- Constructor parameters are resolved from `ServiceRegistry`