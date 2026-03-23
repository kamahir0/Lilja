---
name: unity-development-common
description: >-
  Use for UniCli basics that apply to all Unity Editor automation: verifying
  or installing the UniCli server (unicli check, unicli install), setting the
  project path (UNICLI_PROJECT), discovering and executing commands
  (unicli commands, unicli exec), and implementing custom CommandHandlers
  when built-in commands are insufficient.
metadata:
  version: "1.1.0"
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
- **After creating or deleting any `.cs` file**: Run `AssetDatabase.Import` to refresh and regenerate `.csproj`:
  ```bash
  unicli exec AssetDatabase.Import --path "<path>" --json
  ```
- **After any C# edit**: Run `Compile` and confirm zero errors before finishing:
  ```bash
  unicli exec Compile --json
  ```

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