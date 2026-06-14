# Lilja.FancyScrollView

Included in the Lilja package series.

This package is a customized version of [setchi/FancyScrollView](https://github.com/setchi/FancyScrollView) licensed under the MIT License.

The runtime API is intentionally kept close to the original FancyScrollView design: inherit a scroll view class, provide a cell prefab, call `UpdateContents`, and let each cell convert a normalized `0.0` to `1.0` position into its own visual state.

## Differences from the original

- Namespace is `Lilja.FancyScrollView`.
- `CellPrefab` is typed as `FancyCell<TItemData, TContext>` instead of `GameObject`.
- `CellPrefab` can reference either a prefab asset or a cell component placed directly in the scene hierarchy.
- Edit Mode Preview is available in the inspector when a scroll view opts in.
- The previous Lilja-only public APIs `SetItems`, `RefreshItems`, and `RefreshLayout` are not provided. Use the original-style protected APIs `UpdateContents`, `Refresh`, and `Relayout`.
- `FancyScrollRect.JumpTo` and `FancyScrollRect.ScrollTo` follow the original design and are protected extension points.

## Cell Template

`CellPrefab` accepts a prefab asset or a hierarchy object. A hierarchy object can be placed under the same Content transform used as the cell container, which is useful when the cell is only used by that scroll view.

When a hierarchy object is used as the template, the template itself is excluded from pooling and hidden automatically at runtime and during Edit Mode Preview. Preview restores the template's original active state when it ends, and only generated preview clones are cleaned up.

## Edit Mode Preview

Preview is opt-in. If a scroll view does not override the preview data hook, the Preview UI is hidden.

```csharp
using Lilja.FancyScrollView;

class MyScrollView : FancyScrollView<ItemData>
{
    [UnityEngine.SerializeField] Cell cellPrefab = default;

    protected override FancyCell<ItemData, NullContext> CellPrefab => cellPrefab;

    protected override bool TryCreatePreviewItem(
        FancyScrollPreviewItemContext context,
        out ItemData item)
    {
        item = new ItemData($"Cell {context.Index}");
        return true;
    }
}
```

`FancyGridView<TItemData>` exposes the same preview hook for flat item data. The grid view groups preview items internally using `startAxisCellCount`.

## License

This package is licensed under the MIT License. See [LICENSE](LICENSE) for details.
