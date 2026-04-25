# Lilja.DebugMenu

Included in the Lilja package series.

## Installation

Add via `Plugins/Lilja/Package Management/Fix Lilja Package Paths to Relative` if imported as a local package.

## Builder API

Use `IDebugUIBuilder` extension methods for common controls. Each method adds the control and returns the created element, so dynamic debug pages can keep handles for refresh logic.
`VisualElement` also returns the added element when a custom control is needed.

```csharp
private DebugLabel _statusLabel;
private DebugIntegerField _idField;

public override void Configure(IDebugUIBuilder builder)
{
    builder.Label("Repository test");
    _statusLabel = builder.Label();

    builder.Foldout("Input", foldout =>
    {
        _idField = foldout.IntegerField("Id");

        foldout.HorizontalScope(row =>
        {
            row.PrimaryButton("Create", Create);
            row.SecondaryButton("Read", Read);
            row.DangerButton("Delete", Delete);
        });
    });
}
```

For temporary pages, use `TempNavigationButton`. Temporary pages open in the current host
(runtime menu or editor window) and are not added to the editor page list:

```csharp
builder.TempNavigationButton("Monster Repository", page =>
{
    page.Label("Monster tools");
});
```
