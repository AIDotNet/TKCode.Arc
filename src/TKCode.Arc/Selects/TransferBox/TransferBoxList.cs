using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaListBox = Avalonia.Controls.ListBox;
using AvaloniaListBoxItem = Avalonia.Controls.ListBoxItem;

namespace TKCode.Arc;

/// <summary>
/// One list of a <see cref="TransferBox"/>: an Avalonia <see cref="AvaloniaListBox"/> (click, Ctrl and Shift
/// selection, Ctrl+A, arrow keys, virtualization) with Arc rows.
/// </summary>
internal sealed class TransferBoxList : AvaloniaListBox
{
    /// <summary>Text of a row when the box has no item template (null: the item itself).</summary>
    public Func<object?, string>? TextOf { get; set; }

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<TransferBoxListItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new TransferBoxListItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (ItemTemplate is null && TextOf is { } text && container is ContentControl row)
        {
            row.Content = text(item);
        }
    }
}

/// <summary>A row of a <see cref="TransferBoxList"/>: hover and selected fills, a check when selected, a keyboard focus ring.</summary>
internal sealed class TransferBoxListItem : AvaloniaListBoxItem
{
    private LazyFocusRing? _focusRing;

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, TransferBoxTokens.Key(TransferBoxTokenKind.RowFocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep12));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}
