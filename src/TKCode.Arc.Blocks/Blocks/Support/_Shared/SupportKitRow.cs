using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A pressable row of the Support blocks (a conversation, an article, a request): any content on a plate that tints on
/// hover and stays tinted while <see cref="ToggleButton.IsChecked"/> (the block owns that state; a press never toggles
/// it). Enter and Space press it; the keyboard focus ring sits 3px outside.
/// </summary>
internal class SupportKitRow : ToggleButton
{
    private LazyFocusRing? _focusRing;

    static SupportKitRow()
    {
        FocusableProperty.OverrideDefaultValue<SupportKitRow>(true);
    }

    protected override Type StyleKeyOverride => typeof(SupportKitRow);

    /// <summary>The data the row stands for.</summary>
    public object? Item { get; set; }

    protected override void Toggle()
    {
        // The block owns the checked state.
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep14));
        var host = e.NameScope.Find<Panel>("PART_RingHost");
        if (host is null)
        {
            _focusRing.Attach(null, null);
        }
        else
        {
            _focusRing.Attach(ring =>
            {
                ring.Margin = new Thickness(-3);
                host.Children.Add(ring);
            }, ring => host.Children.Remove(ring));
        }
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

/// <summary>
/// A filter chip of the Support blocks: a pill that is filled with the foreground color while checked. Unlike
/// <see cref="SupportKitRow"/> it toggles itself unless <see cref="IsSelfToggling"/> is false (a chip in a single-choice group).
/// </summary>
internal sealed class SupportKitChip : SupportKitRow
{
    protected override Type StyleKeyOverride => typeof(SupportKitChip);

    /// <summary>Whether a press toggles the chip (false: the owner sets <see cref="ToggleButton.IsChecked"/>).</summary>
    public bool IsSelfToggling { get; set; }

    protected override void Toggle()
    {
        if (IsSelfToggling)
        {
            SetCurrentValue(IsCheckedProperty, IsChecked != true);
        }
    }
}
