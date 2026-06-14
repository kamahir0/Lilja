# Lilja.FancyScrollView

Included in the Lilja package series.

This package is a customized version of [setchi/FancyScrollView](https://github.com/setchi/FancyScrollView) licensed under the MIT License.

The runtime API is intentionally kept close to the original FancyScrollView design: inherit a scroll view class, provide a cell prefab, call `UpdateContents`, and let each cell convert a normalized `0.0` to `1.0` position into its own visual state.

## Differences from the original

- Namespace is `Lilja.FancyScrollView`.
- `CellPrefab` is typed as `FancyCell<TItemData, TContext>` instead of `GameObject`.
- Edit Mode Preview is available in the inspector when a scroll view opts in.
- The previous Lilja-only public APIs `SetItems`, `RefreshItems`, and `RefreshLayout` are not provided. Use the original-style protected APIs `UpdateContents`, `Refresh`, and `Relayout`.
- `FancyScrollRect.JumpTo` and `FancyScrollRect.ScrollTo` follow the original design and are protected extension points.

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
