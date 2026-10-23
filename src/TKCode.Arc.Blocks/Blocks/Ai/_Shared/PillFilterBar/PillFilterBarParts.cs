using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One pill of a <see cref="PillFilterBar"/>: a text button with no background of its own (the bar's gliding background
/// shows under the selected one). The selected pill has <c>:selected</c> and reads as a pressed toggle button
/// (<c>aria-pressed</c>); <c>:hover</c> is set for mouse and pen only (Arc's <c>(hover: hover)</c>).
/// </summary>
[PseudoClasses(SelectedPseudoClass, HoverPseudoClass)]
internal sealed class PillFilterButton : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";
    public const string HoverPseudoClass = ":hover";

    /// <summary>The visible and accessible label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PillFilterButton, string?>(nameof(Label));

    /// <summary>Whether this is the selected pill (<c>:selected</c>, toggle state on).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PillFilterButton, bool>(nameof(IsSelected));

    protected override Type StyleKeyOverride => typeof(PillFilterButton);

    /// <summary>The visible and accessible label.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Whether this is the selected pill.</summary>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>The option's key (null for "All").</summary>
    public string? Key { get; set; }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            var selected = change.GetNewValue<bool>();
            PseudoClasses.Set(SelectedPseudoClass, selected);
            if (ControlAutomationPeer.FromElement(this) is PillPeer peer)
            {
                peer.RaiseToggleChanged(selected);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PillPeer(this);

    /// <summary>A toggle button named by its label, pressed while selected.</summary>
    private sealed class PillPeer(PillFilterButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        private PillFilterButton Pill => (PillFilterButton)Owner;

        public ToggleState ToggleState => Pill.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void RaiseToggleChanged(bool on) =>
            RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                on ? ToggleState.Off : ToggleState.On, on ? ToggleState.On : ToggleState.Off);

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Pill.Label;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(IToggleProvider) ? this : base.GetProviderCore(providerType);
    }
}

/// <summary>
/// The row inside a <see cref="PillFilterBar"/>'s scroller: lays the pills out side by side with <see cref="Spacing"/>
/// between them, vertically centered, and stretches every other child (the gliding background) over the whole row, so
/// a pill's <see cref="Visual.Bounds"/> is also its rectangle in the background's coordinates. Raises
/// <see cref="Arranged"/> after each arrange so the bar can move the background after the pills.
/// </summary>
internal sealed class PillFilterTrack : Panel
{
    /// <summary>The gap between two pills.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PillFilterTrack, double>(nameof(Spacing));

    static PillFilterTrack()
    {
        AffectsMeasure<PillFilterTrack>(SpacingProperty);
    }

    /// <summary>The gap between two pills.</summary>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Raised at the end of every arrange pass of the row.</summary>
    public event Action? Arranged;

    protected override Size MeasureOverride(Size availableSize)
    {
        var pillSize = new Size(double.PositiveInfinity, availableSize.Height);
        var width = 0.0;
        var height = 0.0;
        var pills = 0;
        foreach (var child in Children)
        {
            if (child is PillFilterButton)
            {
                child.Measure(pillSize);
                if (child.IsVisible)
                {
                    width += child.DesiredSize.Width;
                    height = Math.Max(height, child.DesiredSize.Height);
                    pills++;
                }
            }
            else
            {
                child.Measure(default);
            }
        }
        if (pills > 1)
        {
            width += Spacing * (pills - 1);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children)
        {
            if (child is PillFilterButton)
            {
                if (!child.IsVisible)
                {
                    continue;
                }
                var size = child.DesiredSize;
                child.Arrange(new Rect(x, Math.Max(0, (finalSize.Height - size.Height) / 2), size.Width, size.Height));
                x += size.Width + Spacing;
            }
            else
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        Arranged?.Invoke();
        return finalSize;
    }
}
