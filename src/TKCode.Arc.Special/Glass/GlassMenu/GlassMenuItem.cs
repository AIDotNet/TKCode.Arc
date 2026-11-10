using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One command of a <see cref="GlassMenu"/>: an optional icon, the label and an optional shortcut hint. It is a
/// <see cref="Button"/>, so it takes a <c>Command</c> and raises <c>Click</c>; the menu also reports it through
/// <see cref="GlassMenu.ItemInvoked"/> and closes when it was opened from an anchor.
/// </summary>
[PseudoClasses(HighlightedPseudoClass, DestructivePseudoClass)]
public class GlassMenuItem : Button
{
    public const string HighlightedPseudoClass = ":highlighted";
    public const string DestructivePseudoClass = ":destructive";

    /// <summary>An optional 24×24 stroke icon before the label (for example <c>ArcIcons.Copy</c>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<GlassMenuItem, Geometry?>(nameof(Icon));

    /// <summary>A shortcut hint shown at the end of the row, such as "⌘C" or "Ctrl+C". It is only a label.</summary>
    public static readonly StyledProperty<string?> ShortcutProperty =
        AvaloniaProperty.Register<GlassMenuItem, string?>(nameof(Shortcut));

    /// <summary>Marks a destructive command (Delete, Leave): the label and icon take the danger color.</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<GlassMenuItem, bool>(nameof(IsDestructive));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="ShortcutProperty"/>
    public string? Shortcut
    {
        get => GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    /// <inheritdoc cref="IsDestructiveProperty"/>
    public bool IsDestructive
    {
        get => GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(GlassMenuItem);

    /// <summary>True while the menu's highlight is on this item (tests read it).</summary>
    internal bool IsHighlighted => PseudoClasses.Contains(HighlightedPseudoClass);

    internal void SetHighlighted(bool value) => PseudoClasses.Set(HighlightedPseudoClass, value);

    /// <summary>The text typeahead matches against: the label when it is text.</summary>
    internal string? Label => Content as string ?? AutomationProperties.GetName(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDestructiveProperty)
        {
            PseudoClasses.Set(DestructivePseudoClass, IsDestructive);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassMenuItemAutomationPeer(this);

    private sealed class GlassMenuItemAutomationPeer : ButtonAutomationPeer
    {
        public GlassMenuItemAutomationPeer(GlassMenuItem owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string? GetAcceleratorKeyCore() => ((GlassMenuItem)Owner).Shortcut ?? base.GetAcceleratorKeyCore();
    }
}

/// <summary>A thin line between groups of items in a <see cref="GlassMenu"/>.</summary>
public class GlassMenuSeparator : Control
{
    /// <summary>Color of the line (set by the theme).</summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<GlassMenuSeparator, IBrush?>(nameof(LineBrush));

    /// <summary>Thickness of the line (set by the theme).</summary>
    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<GlassMenuSeparator, double>(nameof(LineThickness), 1);

    static GlassMenuSeparator()
    {
        AffectsRender<GlassMenuSeparator>(LineBrushProperty, LineThicknessProperty);
        AffectsMeasure<GlassMenuSeparator>(LineThicknessProperty);
        FocusableProperty.OverrideDefaultValue<GlassMenuSeparator>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassMenuSeparator>(false);
    }

    /// <inheritdoc cref="LineBrushProperty"/>
    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <inheritdoc cref="LineThicknessProperty"/>
    public double LineThickness
    {
        get => GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Math.Max(0, LineThickness));

    public override void Render(DrawingContext context)
    {
        if (LineBrush is { } brush && Bounds.Width > 0)
        {
            context.FillRectangle(brush, new Rect(0, 0, Bounds.Width, Math.Max(0, LineThickness)));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassMenuSeparatorAutomationPeer(this);

    private sealed class GlassMenuSeparatorAutomationPeer : ControlAutomationPeer
    {
        public GlassMenuSeparatorAutomationPeer(GlassMenuSeparator owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Separator;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>Arguments of <see cref="GlassMenu.ItemInvoked"/>.</summary>
public sealed class GlassMenuItemInvokedEventArgs : RoutedEventArgs
{
    public GlassMenuItemInvokedEventArgs(RoutedEvent routedEvent, GlassMenuItem container, object? item) : base(routedEvent)
    {
        Container = container;
        Item = item;
    }

    /// <summary>The row that was invoked.</summary>
    public GlassMenuItem Container { get; }

    /// <summary>The item behind the row: the <see cref="GlassMenuItem"/> itself, or the data item from <c>ItemsSource</c>.</summary>
    public object? Item { get; }
}
