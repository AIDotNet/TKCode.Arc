using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A round toolbar button of <see cref="CursorTrailDraw"/>: an icon, an optional pressed (toggled) state with the
/// accent highlight, hover tint and a press squeeze. Tools, colors and snapping report <c>aria-pressed</c> through the
/// toggle pattern; Undo and Clear are plain buttons.
/// </summary>
internal sealed class CursorTrailDrawToolButton : Avalonia.Controls.Button
{
    public const string CheckedPseudoClass = ":checked";

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<CursorTrailDrawToolButton, bool>(nameof(IsChecked));

    public static readonly StyledProperty<bool> IsToggleProperty =
        AvaloniaProperty.Register<CursorTrailDrawToolButton, bool>(nameof(IsToggle));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<CursorTrailDrawToolButton, IBrush?>(nameof(HighlightBrush));

    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>Whether the button has a pressed state (tools, colors, snapping).</summary>
    public bool IsToggle { get => GetValue(IsToggleProperty); set => SetValue(IsToggleProperty, value); }

    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(CursorTrailDrawToolButton);

    protected override AutomationPeer OnCreateAutomationPeer() => new ToolButtonAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            PseudoClasses.Set(CheckedPseudoClass, IsChecked);
            if (IsToggle)
            {
                AutomationProperties.SetItemStatus(this, IsChecked ? "Pressed" : "Not pressed");
            }
        }
    }

    private sealed class ToolButtonAutomationPeer(CursorTrailDrawToolButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        private CursorTrailDrawToolButton Button => (CursorTrailDrawToolButton)Owner;

        public ToggleState ToggleState => Button.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(ClickEvent));

        protected override bool IsContentElementCore() => true;
    }
}

/// <summary>A color button: a 16 px dot that grows a little and gains a ring when its ink is picked.</summary>
internal sealed class CursorTrailDrawSwatch : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<CursorTrailDrawSwatch, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> EdgeProperty =
        AvaloniaProperty.Register<CursorTrailDrawSwatch, IBrush?>(nameof(Edge));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<CursorTrailDrawSwatch, bool>(nameof(IsSelected));

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<CursorTrailDrawSwatch, double>(nameof(DotSize), 16);

    public static readonly StyledProperty<double> RingSizeProperty =
        AvaloniaProperty.Register<CursorTrailDrawSwatch, double>(nameof(RingSize), 23);

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<CursorTrailDrawSwatch, double>(nameof(RingThickness), 1.5);

    static CursorTrailDrawSwatch()
    {
        AffectsRender<CursorTrailDrawSwatch>(FillProperty, EdgeProperty, IsSelectedProperty, DotSizeProperty, RingSizeProperty, RingThicknessProperty);
        AffectsMeasure<CursorTrailDrawSwatch>(RingSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<CursorTrailDrawSwatch>(false);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Edge { get => GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }
    public double RingSize { get => GetValue(RingSizeProperty); set => SetValue(RingSizeProperty, value); }
    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(RingSize, RingSize);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(":selected", IsSelected);
        }
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var r = DotSize / 2;
        if (IsSelected && Fill is { } ring)
        {
            var ringRadius = RingSize / 2 - RingThickness / 2;
            context.DrawEllipse(null, new Pen(ring, RingThickness), center, ringRadius, ringRadius);
        }
        context.DrawEllipse(Fill, IsSelected || Edge is null ? null : new Pen(Edge, 1), center, r - (IsSelected ? 0 : 0.5), r - (IsSelected ? 0 : 0.5));
    }
}

/// <summary>The chip's "keep as drawn" button: 32 px tall, pill shaped, muted surface.</summary>
internal sealed class CursorTrailDrawChipButton : Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(CursorTrailDrawChipButton);
}

/// <summary>
/// The toolbar row: one Tab stop per button, and the arrow keys, Home and End also move between its enabled buttons.
/// </summary>
internal sealed class CursorTrailDrawToolbarRow : StackPanel
{
    public CursorTrailDrawToolbarRow()
    {
        Orientation = Orientation.Horizontal;
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToolbarAutomationPeer(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var buttons = this.GetVisualDescendantsOfType<CursorTrailDrawToolButton>()
            .Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        if (buttons.Count == 0 || e.Source is not Visual source)
        {
            return;
        }
        var current = buttons.FindIndex(b => ReferenceEquals(b, source) || b.IsVisualAncestorOf(source));
        var next = e.Key switch
        {
            Key.Right => current < 0 ? 0 : Math.Min(buttons.Count - 1, current + 1),
            Key.Left => current < 0 ? 0 : Math.Max(0, current - 1),
            Key.Home => 0,
            Key.End => buttons.Count - 1,
            _ => -1
        };
        if (next >= 0)
        {
            e.Handled = true;
            buttons[next].Focus(NavigationMethod.Directional);
        }
    }

    private sealed class ToolbarAutomationPeer(CursorTrailDrawToolbarRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

internal static class CursorTrailDrawVisualExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOfType<T>(this Visual root) where T : Visual
    {
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
        {
            if (child is T match)
            {
                yield return match;
            }
        }
    }

    public static bool IsVisualAncestorOf(this Visual ancestor, Visual descendant) =>
        Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(ancestor, descendant);
}

/// <summary>Lucide glyphs the toolbar needs beyond Core's set (ISC license, Lucide 1.59.0).</summary>
internal static class CursorTrailDrawIcons
{
    // Lucide "wand" (ISC).
    public static Geometry Wand => ArcIcons.Parse("M15 4V2 M15 16v-2 M8 9h2 M20 9h2 M17.8 11.8 19 13 M15 9h.01 M17.8 6.2 19 5 M3 21l9-9 M12.2 6.2 11 5");

    // Lucide "shapes" (ISC).
    public static Geometry Shapes => ArcIcons.Parse(
        "M8.3 10a.7.7 0 0 1-.626-1.079L11.4 3a.7.7 0 0 1 1.198-.043L16.3 8.9a.7.7 0 0 1-.572 1.1Z M4 14h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M14 17.5a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0 -7 0z");
}
