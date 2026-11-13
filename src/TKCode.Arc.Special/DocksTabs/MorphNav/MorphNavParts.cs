using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A button of a <see cref="MorphNav"/> (bar items, brand, tools, links): its keyboard ring is built on first keyboard
/// focus, outside the face with the pill radius unless a subclass asks for another, and it carries the compact state.
/// </summary>
[TemplatePart("PART_Root", typeof(Panel))]
[PseudoClasses(CompactPseudoClass, HoverPseudoClass)]
internal abstract class MorphNavButton : AvaloniaButton
{
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Under a mouse or pen (not a finger): hover effects never flash on touch.</summary>
    internal const string HoverPseudoClass = ":hover";

    private LazyFocusRing? _focusRing;

    static MorphNavButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<MorphNavButton>(false);
    }

    /// <summary>Resource key of the ring's corner radius.</summary>
    protected virtual object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill);

    public void SetCompact(bool compact) => PseudoClasses.Set(CompactPseudoClass, compact);

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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, MorphNavTokens.Key(MorphNavTokenKind.FocusRingMargin), RingRadiusKey);
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

/// <summary>The bar: a row of the brand, the items and the tools, 56 px tall (46 compact).</summary>
[PseudoClasses(MorphNavButton.CompactPseudoClass)]
internal sealed class MorphNavBar : Border
{
    public void SetCompact(bool compact) => PseudoClasses.Set(MorphNavButton.CompactPseudoClass, compact);
}

/// <summary>
/// Shows its child at <see cref="Amount"/> of its width, clipping the rest: the brand name folds away in compact and
/// narrow bars.
/// </summary>
internal sealed class MorphNavReveal : Decorator
{
    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<MorphNavReveal, double>(nameof(Amount), 1);

    static MorphNavReveal()
    {
        AffectsMeasure<MorphNavReveal>(AmountProperty);
        ClipToBoundsProperty.OverrideDefaultValue<MorphNavReveal>(true);
    }

    public double Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
        {
            return default;
        }
        Child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        var size = Child.DesiredSize;
        return new Size(size.Width * Math.Clamp(Amount, 0, 1), size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, Child.DesiredSize.Width, finalSize.Height));
        return finalSize;
    }
}

/// <summary>The brand button: the mark and the name, which folds away in compact and narrow bars.</summary>
[PseudoClasses(BarePseudoClass)]
internal sealed class MorphNavBrandButton : MorphNavButton
{
    internal const string BarePseudoClass = ":bare";

    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<MorphNavBrandButton, object?>(nameof(Mark));

    public static readonly StyledProperty<IDataTemplate?> MarkTemplateProperty =
        AvaloniaProperty.Register<MorphNavBrandButton, IDataTemplate?>(nameof(MarkTemplate));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MorphNavBrandButton, string?>(nameof(Text));

    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    public IDataTemplate? MarkTemplate { get => GetValue(MarkTemplateProperty); set => SetValue(MarkTemplateProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Folds the name away (compact or narrow).</summary>
    public void SetBare(bool bare) => PseudoClasses.Set(BarePseudoClass, bare);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkPeer(this);

    private sealed class LinkPeer(AvaloniaButton owner) : ButtonAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    }
}

/// <summary>
/// A top-level item of the bar: its label and, for items with a panel, a chevron that flips while the panel is open. The
/// current item and the open one read in the foreground color.
/// </summary>
[PseudoClasses(OpenPseudoClass, CurrentPseudoClass, PanelPseudoClass)]
internal sealed class MorphNavTrigger : MorphNavButton
{
    internal const string OpenPseudoClass = ":open";
    internal const string CurrentPseudoClass = ":current";
    internal const string PanelPseudoClass = ":panel";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<MorphNavTrigger, string?>(nameof(Label));

    public static readonly StyledProperty<bool> HasPanelProperty =
        AvaloniaProperty.Register<MorphNavTrigger, bool>(nameof(HasPanel));

    public MorphNavTrigger(MorphNavItem? item)
    {
        Item = item;
    }

    /// <summary>The item; null for the menu trigger of a folded bar.</summary>
    public MorphNavItem? Item { get; }

    /// <summary>What the trigger opens: its item, or <see cref="MorphNav.MenuKey"/>.</summary>
    public object Key => (object?)Item ?? MorphNav.MenuKey;

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool HasPanel { get => GetValue(HasPanelProperty); set => SetValue(HasPanelProperty, value); }

    public bool IsOpen { get; private set; }

    public bool IsCurrent { get; private set; }

    /// <summary>Whether the keyboard ring shows (the highlight follows keyboard focus only).</summary>
    public bool IsFocusVisible => IsFocused && Classes.Contains(":focus-visible");

    /// <summary>Raised after keyboard focus arrives or leaves, once the focus pseudo-classes are up to date.</summary>
    public event EventHandler? FocusChanged;

    public void SetOpen(bool open)
    {
        IsOpen = open;
        PseudoClasses.Set(OpenPseudoClass, open);
        if (HasPanel)
        {
            AutomationProperties.SetItemStatus(this, open ? "Expanded" : "Collapsed");
        }
    }

    public void SetCurrent(bool current)
    {
        IsCurrent = current;
        PseudoClasses.Set(CurrentPseudoClass, current);
        AutomationProperties.SetHelpText(this, current ? "Current page" : null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == HasPanelProperty)
        {
            PseudoClasses.Set(PanelPseudoClass, HasPanel);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerPeer(this);

    /// <summary>Items with a panel expand and collapse; plain items are links.</summary>
    private sealed class TriggerPeer(MorphNavTrigger owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private MorphNavTrigger Trigger => (MorphNavTrigger)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            Trigger.HasPanel ? AutomationControlType.Button : AutomationControlType.Hyperlink;

        public ExpandCollapseState ExpandCollapseState =>
            !Trigger.HasPanel ? ExpandCollapseState.LeafNode
            : Trigger.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand() => Trigger.FindAncestorOfType<MorphNav>()?.OpenFromAutomation(Trigger);

        public void Collapse()
        {
            if (Trigger.IsOpen)
            {
                Trigger.FindAncestorOfType<MorphNav>()?.ClosePanel();
            }
        }
    }
}

/// <summary>
/// The row of items: lays them out side by side and hosts the highlight that glides between them, under the items.
/// </summary>
internal sealed class MorphNavTrack : Panel
{
    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<MorphNavTrack, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<CornerRadius> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<MorphNavTrack, CornerRadius>(nameof(HighlightCornerRadius));

    public MorphNavTrack()
    {
        Highlight = new GlidingIndicator { IsHitTestVisible = false };
        Children.Add(Highlight);
    }

    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }

    public CornerRadius HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HighlightBrushProperty)
        {
            Highlight.Fill = HighlightBrush;
        }
        else if (change.Property == HighlightCornerRadiusProperty)
        {
            Highlight.CornerRadius = HighlightCornerRadius;
        }
    }

    public GlidingIndicator Highlight { get; }

    /// <summary>Raised after every arrange, so the highlight can follow items whose size changed.</summary>
    public event EventHandler? Arranged;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            if (ReferenceEquals(child, Highlight))
            {
                continue;
            }
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (!child.IsVisible)
            {
                continue;
            }
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        Highlight.Measure(Size.Infinity);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children)
        {
            if (ReferenceEquals(child, Highlight) || !child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            child.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
            x += size.Width;
        }
        Highlight.Arrange(new Rect(finalSize));
        Arranged?.Invoke(this, EventArgs.Empty);
        return finalSize;
    }
}

/// <summary>A round icon button of the bar (search) and of the search bar (close).</summary>
internal sealed class MorphNavToolButton : MorphNavButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<MorphNavToolButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>The call-to-action button at the far end of the bar, filled with the foreground color.</summary>
internal sealed class MorphNavActionButton : MorphNavButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<MorphNavActionButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}
