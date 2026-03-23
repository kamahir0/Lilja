# SKILL Patch — Reliable `AssetDatabase.Import` & `Compile` Triggering

Apply the following two changes to `unity-development-common` SKILL.md.

---

## Change 1 — Replace the "Rules — Always Follow" section

**Remove** the current rule block:

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

**Replace with:**

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

---

## Change 2 — Add an idempotency note to the Custom CommandHandlers section

In the **Custom CommandHandlers** section, after the existing `unicli exec AssemblyDefinition.Create` block, add the following paragraph:

> **Idempotency note**: `AssemblyDefinition.Create` fails if the definition already exists.
> Before running it, check for an existing `.asmdef` file at the target path and skip
> creation if found. `AssetDatabase.Import` and `Compile` are always safe to re-run.

---

## Rationale

The original `After creating…` / `After any C# edit…` phrasing required the agent to
match its own past actions against prose descriptions mid-task — a pattern that fails
silently when the agent does not re-read the rules between steps.

The replacement uses:

1. **A trigger table** — the agent compares *operation type* against a discrete row,
   which is more reliable than parsing conditional prose.
2. **A pre-completion checklist** — evaluated once at task boundary, not inline during
   execution, so it fires regardless of how many steps the task involved.
3. **Explicit order enforcement** — `Import → Compile` order is stated in the table
   rather than implied by separate bullet points.