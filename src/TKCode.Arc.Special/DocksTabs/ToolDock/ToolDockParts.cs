using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The root of a <see cref="ToolDock"/>'s template: the bar (its surface and the slots) fills the control, the tray is
/// placed above it at the rectangle the dock gives it (outside the control's bounds, on purpose), and the floating label
/// is positioned by its own transform. Nothing here is clipped.
/// </summary>
internal sealed class ToolDockStage : Panel
{
    /// <summary>The space between the bar's edge and its slots.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<ToolDockStage>();

    static ToolDockStage()
    {
        AffectsMeasure<ToolDockStage>(PaddingProperty);
    }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var bar = default(Size);
        foreach (var child in Children)
        {
            if (child is ItemsPresenter presenter)
            {
                presenter.Measure(availableSize.Deflate(padding));
                bar = presenter.DesiredSize.Inflate(padding);
            }
        }
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            if (child is not ItemsPresenter)
            {
                child.Measure(child is ToolDockSurface ? bar : infinite);
            }
        }
        return bar;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var bar = new Rect(finalSize);
        foreach (var child in Children)
        {
            switch (child)
            {
                case ItemsPresenter presenter:
                    presenter.Arrange(bar.Deflate(Padding));
                    break;
                case ToolDockTray tray:
                    tray.Arrange(tray.Placement);
                    break;
                case ToolDockLabel label:
                    label.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, label.DesiredSize.Width), label.DesiredSize.Height));
                    break;
                default:
                    child.Arrange(bar);
                    break;
            }
        }
        return finalSize;
    }
}

/// <summary>
/// The default items panel of a <see cref="ToolDock"/>: slots in a row, <see cref="Spacing"/> apart. After every arrange it
/// lets the dock start the reflow of slots that a reorder moved (their old place is known, the new one only now).
/// </summary>
internal sealed class ToolDockPanel : Panel
{
    /// <summary>The space between two slots.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ToolDockPanel, double>(nameof(Spacing), 4);

    static ToolDockPanel()
    {
        AffectsMeasure<ToolDockPanel>(SpacingProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        double width = 0, height = 0;
        var count = 0;
        foreach (var child in Children)
        {
            child.Measure(infinite);
            if (!child.IsVisible)
            {
                continue;
            }
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
            count++;
        }
        return new Size(width + Math.Max(0, count - 1) * Spacing, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            child.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
            x += size.Width + Spacing;
        }
        this.FindAncestorOfType<ToolDock>()?.OnPanelArranged();
        return finalSize;
    }
}

/// <summary>
/// The material of a <see cref="ToolDock"/>: the bar and, while a group is open, the tray growing out of its top edge,
/// drawn as one filled outline with one hairline and one shadow. Where the tray meets the bar the outline curves inward
/// (the fillet); while the tray grows or sinks its corner and fillet radii shrink with its height.
/// </summary>
internal sealed class ToolDockSurface : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ToolDockSurface, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ToolDockSurface, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ToolDockSurface, double>(nameof(StrokeThickness), 0.5);

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<ToolDockSurface, BoxShadows>(nameof(BoxShadow));

    public static readonly StyledProperty<CornerRadius> BarCornerRadiusProperty =
        AvaloniaProperty.Register<ToolDockSurface, CornerRadius>(nameof(BarCornerRadius));

    public static readonly StyledProperty<CornerRadius> TrayCornerRadiusProperty =
        AvaloniaProperty.Register<ToolDockSurface, CornerRadius>(nameof(TrayCornerRadius));

    public static readonly StyledProperty<double> FilletRadiusProperty =
        AvaloniaProperty.Register<ToolDockSurface, double>(nameof(FilletRadius), 8);

    /// <summary>Corner and fillet radii follow a short tray: at most this share of its height.</summary>
    private const double CornerShare = 0.45;
    private const double FilletShare = 0.3;

    private Rect _tab;
    private StreamGeometry? _outline;
    private Rect _outlineTab;
    private Size _outlineSize;
    private IPen? _pen;

    static ToolDockSurface()
    {
        AffectsRender<ToolDockSurface>(FillProperty, StrokeProperty, StrokeThicknessProperty, BoxShadowProperty,
            BarCornerRadiusProperty, TrayCornerRadiusProperty, FilletRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolDockSurface>(true);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }
    public CornerRadius BarCornerRadius { get => GetValue(BarCornerRadiusProperty); set => SetValue(BarCornerRadiusProperty, value); }
    public CornerRadius TrayCornerRadius { get => GetValue(TrayCornerRadiusProperty); set => SetValue(TrayCornerRadiusProperty, value); }
    public double FilletRadius { get => GetValue(FilletRadiusProperty); set => SetValue(FilletRadiusProperty, value); }

    /// <summary>The tray's part of the outline, in the bar's coordinates (its bottom on the bar's top edge, y = 0).</summary>
    internal Rect Tab
    {
        get => _tab;
        set
        {
            if (_tab == value)
            {
                return;
            }
            _tab = value;
            InvalidateVisual();
        }
    }

    /// <summary>Whether the outline currently includes a tray.</summary>
    internal bool HasTab => _tab.Height > 0.25 && _tab.Width > 0.5;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrokeProperty || change.Property == StrokeThicknessProperty)
        {
            _pen = null;
        }
        else if (change.Property == BarCornerRadiusProperty || change.Property == TrayCornerRadiusProperty
                 || change.Property == FilletRadiusProperty)
        {
            _outline = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var bar = new Rect(size);
        var radius = Math.Min(BarCornerRadius.TopLeft, size.Height / 2);
        var pen = _pen ??= Stroke is { } stroke && StrokeThickness > 0 ? new ImmutablePen(stroke.ToImmutable(), StrokeThickness) : null;
        context.DrawRectangle(Fill, null, new RoundedRect(bar, radius), BoxShadow);
        if (!HasTab || !TryPlaceTab(size, radius, out var tab, out var corner, out var fillet))
        {
            if (pen is not null)
            {
                context.DrawRectangle(null, pen, new RoundedRect(bar, radius));
            }
            return;
        }
        context.DrawRectangle(Fill, null, new RoundedRect(tab, corner, corner, 0, 0), BoxShadow);
        if (_outline is null || _outlineTab != _tab || _outlineSize != size)
        {
            _outline = BuildOutline(size, radius, tab, corner, fillet);
            _outlineTab = _tab;
            _outlineSize = size;
        }
        context.DrawGeometry(Fill, pen, _outline);
    }

    /// <summary>Keeps the tray clear of the bar's rounded ends and shrinks its radii while it is short.</summary>
    private bool TryPlaceTab(Size size, double radius, out Rect tab, out double corner, out double fillet)
    {
        var h = _tab.Height;
        fillet = Math.Max(0, Math.Min(FilletRadius, h * FilletShare));
        var left = Math.Max(_tab.X, radius + fillet);
        var right = Math.Min(_tab.Right, size.Width - radius - fillet);
        tab = new Rect(left, -h, Math.Max(0, right - left), h);
        corner = Math.Max(0, Math.Min(Math.Min(TrayCornerRadius.TopLeft, h * CornerShare), tab.Width / 2));
        return tab.Width > 0.5;
    }

    private static StreamGeometry BuildOutline(Size size, double r, Rect tab, double corner, double fillet)
    {
        var w = size.Width;
        var h = size.Height;
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(new Point(0, r), true);
        Arc(g, new Point(r, 0), r, SweepDirection.Clockwise);
        g.LineTo(new Point(tab.X - fillet, 0));
        Arc(g, new Point(tab.X, -fillet), fillet, SweepDirection.CounterClockwise);
        g.LineTo(new Point(tab.X, tab.Y + corner));
        Arc(g, new Point(tab.X + corner, tab.Y), corner, SweepDirection.Clockwise);
        g.LineTo(new Point(tab.Right - corner, tab.Y));
        Arc(g, new Point(tab.Right, tab.Y + corner), corner, SweepDirection.Clockwise);
        g.LineTo(new Point(tab.Right, -fillet));
        Arc(g, new Point(tab.Right + fillet, 0), fillet, SweepDirection.CounterClockwise);
        g.LineTo(new Point(w - r, 0));
        Arc(g, new Point(w, r), r, SweepDirection.Clockwise);
        g.LineTo(new Point(w, h - r));
        Arc(g, new Point(w - r, h), r, SweepDirection.Clockwise);
        g.LineTo(new Point(r, h));
        Arc(g, new Point(0, h - r), r, SweepDirection.Clockwise);
        g.EndFigure(true);
        return geometry;
    }

    private static void Arc(StreamGeometryContext g, Point to, double radius, SweepDirection direction)
    {
        if (radius <= 0.01)
        {
            g.LineTo(to);
            return;
        }
        g.ArcTo(to, new Size(radius, radius), 0, false, direction);
    }
}

/// <summary>
/// A group's tray: its members in a row above the bar. The dock places it, fades it with the outline's height and hides
/// it once it has sunk back; it is created the first time a group opens and is then reused, holding whichever group
/// opened most recently.
/// </summary>
internal sealed class ToolDockTray : Panel
{
    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<ToolDockTray>();

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ToolDockTray, double>(nameof(Spacing), 4);

    /// <summary>How close the tray may come to either end of the bar.</summary>
    public static readonly StyledProperty<double> TrayInsetProperty =
        AvaloniaProperty.Register<ToolDockTray, double>(nameof(TrayInset), 30);

    static ToolDockTray()
    {
        AffectsMeasure<ToolDockTray>(PaddingProperty, SpacingProperty);
        BackgroundProperty.OverrideDefaultValue<ToolDockTray>(Brushes.Transparent);
    }

    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public double TrayInset { get => GetValue(TrayInsetProperty); set => SetValue(TrayInsetProperty, value); }

    /// <summary>The group whose members the tray shows.</summary>
    internal ToolDockItem? Group { get; set; }

    /// <summary>Where the stage places the tray, in the bar's coordinates.</summary>
    internal Rect Placement { get; set; }

    internal IEnumerable<ToolDockTrayButton> Buttons => Children.OfType<ToolDockTrayButton>();

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(infinite);
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(width + Math.Max(0, Children.Count - 1) * Spacing, height).Inflate(Padding);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = Padding.Left;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect(x, Padding.Top, size.Width, size.Height));
            x += size.Width + Spacing;
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TrayPeer(this);

    /// <summary>A group named after the dock group whose members it holds.</summary>
    private sealed class TrayPeer(ToolDockTray owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((ToolDockTray)Owner).Group?.DisplayName;
    }
}

/// <summary>
/// One member in a tray: a 36 px button with its glyph. It enters with the tray (its fade, scale and rise staggered by the
/// dock) and fades out when the tray sinks. Choosing it selects the member and closes the tray.
/// </summary>
[PseudoClasses(SelectedPseudoClass, ToolDockItem.HoverPseudoClass, PressedPseudoClass)]
internal sealed class ToolDockTrayButton : TemplatedControl
{
    internal const string SelectedPseudoClass = ":selected";
    internal const string PressedPseudoClass = ":pressed";
    internal const string RootPart = "PART_Root";

    /// <summary>Member entry: fade, scale 0.9 → 1 and an 8 px rise on one spring (WAAPI linear() fit ω0 20.14, ζ 1).</summary>
    internal static readonly SpringSpec EnterSpring = SpringSpec.FromVisual(0.26, 0);

    private static readonly TimeSpan s_exit = ArcMotionPresets.Fast;

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ToolDockTrayButton, Geometry?>(nameof(Glyph));

    private readonly MatrixTransform _transform = new();
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _scale;
    private MotionValue<double>? _rise;
    private LazyFocusRing? _focusRing;
    private IPointer? _pressPointer;

    static ToolDockTrayButton()
    {
        FocusableProperty.OverrideDefaultValue<ToolDockTrayButton>(true);
        ClipToBoundsProperty.OverrideDefaultValue<ToolDockTrayButton>(false);
    }

    public ToolDockTrayButton(ToolDock dock, ToolDockItem member)
    {
        Dock = dock;
        Member = member;
        RenderTransform = _transform;
    }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    internal ToolDock Dock { get; }

    internal ToolDockItem Member { get; }

    internal bool IsSelected => PseudoClasses.Contains(SelectedPseudoClass);

    internal void SetSelected(bool selected) => PseudoClasses.Set(SelectedPseudoClass, selected);

    /// <summary>The entry opacity (separate from <see cref="Visual.Opacity"/> only in name: it writes it).</summary>
    internal double EntryOpacity => _opacity?.Value ?? Opacity;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, ToolDockTokens.Key(ToolDockTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep16));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    /// <summary>Plays the member's entry after <paramref name="delay"/>, or shows it at once.</summary>
    internal void Enter(TimeSpan delay, bool animate)
    {
        EnsureMotion();
        if (!animate)
        {
            _opacity!.Jump(1);
            _scale!.Jump(1);
            _rise!.Jump(0);
            return;
        }
        _opacity!.Jump(0);
        _scale!.Jump(0.9);
        _rise!.Jump(8);
        _opacity.AnimateTo(1, MotionSpec.Spring(EnterSpring, delay));
        _scale.AnimateTo(1, MotionSpec.Spring(EnterSpring, delay));
        _rise.AnimateTo(0, MotionSpec.Spring(EnterSpring, delay));
    }

    /// <summary>Fades the member out with the sinking tray (all members at once).</summary>
    internal void Exit(bool animate)
    {
        EnsureMotion();
        EndPress();
        if (!animate)
        {
            _opacity!.Jump(0);
            _scale!.Jump(0.92);
            _rise!.Jump(0);
            return;
        }
        _opacity!.AnimateTo(0, MotionSpec.Tween(s_exit, ArcEasings.Standard));
        _scale!.AnimateTo(0.92, MotionSpec.Tween(s_exit, ArcEasings.Standard));
    }

    private void EnsureMotion()
    {
        if (_opacity is not null)
        {
            return;
        }
        _opacity = new MotionValue<double>(this, Opacity, MotionVectors.Double);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _rise = new MotionValue<double>(this, 0, MotionVectors.Double);
        _opacity.Changed += value => Opacity = value;
        _scale.Changed += _ => ApplyTransform();
        _rise.Changed += _ => ApplyTransform();
    }

    private void ApplyTransform()
    {
        var scale = _scale!.Value;
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, _rise!.Value);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        PseudoClasses.Set(ToolDockItem.HoverPseudoClass, true);
        Dock.OnHover(this, true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(ToolDockItem.HoverPseudoClass, false);
        Dock.OnHover(this, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEffectivelyEnabled)
        {
            return;
        }
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressPointer = e.Pointer;
        PseudoClasses.Set(PressedPseudoClass, true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!ReferenceEquals(e.Pointer, _pressPointer))
        {
            return;
        }
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        EndPress();
        if (inside)
        {
            e.Handled = true;
            Dock.ChooseMember(Member, keyboard: false);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Pointer, _pressPointer))
        {
            EndPress();
        }
    }

    private void EndPress()
    {
        _pressPointer = null;
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
        Dock.OnMemberFocused(this, e.NavigationMethod);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
        Dock.OnFocusLeaving();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MemberPeer(this);

    /// <summary>A button that reports whether its member holds the dock's value.</summary>
    private sealed class MemberPeer(ToolDockTrayButton owner) : ControlAutomationPeer(owner), IToggleProvider, IInvokeProvider
    {
        private ToolDockTrayButton Button => (ToolDockTrayButton)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore()
        {
            var member = Button.Member;
            return member.Shortcut is { Length: > 0 } shortcut ? $"{member.DisplayName}, {shortcut}" : member.DisplayName;
        }

        public ToggleState ToggleState => Button.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Invoke();

        public void Invoke() => Button.Dock.ChooseMember(Button.Member, keyboard: false);
    }
}

/// <summary>
/// A slot's glyph: two stacked glyphs so that, when a group's member changes, the old one shrinks to 80% and blurs away
/// while the new one fades in and grows back from 80% out of a 2 px blur (Arc Dock, design reference).
/// </summary>
internal sealed class ToolDockGlyph : Control
{
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ToolDockGlyph, double>(nameof(Size), 20);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ToolDockGlyph, IBrush?>(nameof(Foreground));

    /// <summary>The blur at the far end of a swap, in px.</summary>
    private const double SwapBlur = 2;

    /// <summary>The scale at the far end of a swap.</summary>
    private const double SwapScale = 0.8;

    private static readonly TimeSpan s_out = ArcMotionPresets.Instant;
    private static readonly TimeSpan s_in = ArcMotionPresets.Fast;

    private readonly Layer _a;
    private readonly Layer _b;
    private Layer _front;

    static ToolDockGlyph()
    {
        AffectsMeasure<ToolDockGlyph>(SizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolDockGlyph>(false);
    }

    public ToolDockGlyph()
    {
        _a = new Layer(this);
        _b = new Layer(this);
        _b.Glyph.Opacity = 0;
        _front = _a;
    }

    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The glyph shown (or being shown).</summary>
    internal Geometry? Data => _front.Glyph.Data;

    /// <summary>The front glyph's opacity, and the one leaving.</summary>
    internal double FrontOpacity => _front.Glyph.Opacity;

    internal double BackOpacity => (ReferenceEquals(_front, _a) ? _b : _a).Glyph.Opacity;

    /// <summary>The front glyph's current blur radius.</summary>
    internal double FrontBlur => _front.Morph?.Value * SwapBlur ?? 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _a.Glyph.Foreground = Foreground;
            _b.Glyph.Foreground = Foreground;
        }
        else if (change.Property == SizeProperty)
        {
            _a.Glyph.Size = Size;
            _b.Glyph.Size = Size;
        }
    }

    /// <summary>Shows <paramref name="data"/>, swapping with the animation when asked and motion allows.</summary>
    internal void Show(Geometry? data, bool animate)
    {
        if (ReferenceEquals(_front.Glyph.Data, data))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || _front.Glyph.Data is null || TopLevel.GetTopLevel(this) is null)
        {
            _front.Glyph.Data = data;
            _front.Rest(1);
            (ReferenceEquals(_front, _a) ? _b : _a).Rest(0);
            return;
        }
        var outgoing = _front;
        var incoming = ReferenceEquals(_front, _a) ? _b : _a;
        _front = incoming;
        incoming.Glyph.Data = data;
        var full = level == ArcMotionLevel.Full;
        outgoing.Fade(0, MotionSpec.Tween(s_out, ToolDockItem.EaseOut));
        if (full)
        {
            outgoing.MorphTo(1, MotionSpec.Tween(s_out, ToolDockItem.EaseOut));
            incoming.JumpMorph(1);
            incoming.MorphTo(0, MotionSpec.Spring(ToolDockTrayButton.EnterSpring));
        }
        else
        {
            outgoing.JumpMorph(0);
            incoming.JumpMorph(0);
        }
        incoming.Fade(1, MotionSpec.Tween(full ? s_in : s_out, ToolDockItem.EaseOut));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _a.Glyph.Measure(availableSize);
        _b.Glyph.Measure(availableSize);
        return new Size(Size, Size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _a.Glyph.Arrange(rect);
        _b.Glyph.Arrange(rect);
        return finalSize;
    }

    private sealed class Layer
    {
        private readonly ScaleTransform _scale = new();
        private MotionValue<double>? _fade;

        public Layer(ToolDockGlyph owner)
        {
            Owner = owner;
            Glyph = new ArcGlyph { Size = owner.Size, RenderTransform = _scale };
            owner.VisualChildren.Add(Glyph);
            owner.LogicalChildren.Add(Glyph);
        }

        private ToolDockGlyph Owner { get; }

        public ArcGlyph Glyph { get; }

        public MotionValue<double>? Morph { get; private set; }

        public void Rest(double opacity)
        {
            _fade?.Jump(opacity);
            Glyph.Opacity = opacity;
            JumpMorph(0);
        }

        public void Fade(double target, MotionSpec spec)
        {
            if (_fade is null)
            {
                _fade = new MotionValue<double>(Owner, Glyph.Opacity, MotionVectors.Double);
                _fade.Changed += value => Glyph.Opacity = value;
            }
            _fade.AnimateTo(target, spec);
        }

        public void JumpMorph(double value)
        {
            if (Morph is null && value == 0)
            {
                return;
            }
            EnsureMorph().Jump(value);
        }

        public void MorphTo(double target, MotionSpec spec) => EnsureMorph().AnimateTo(target, spec);

        private MotionValue<double> EnsureMorph()
        {
            if (Morph is null)
            {
                Morph = new MotionValue<double>(Owner, 0, MotionVectors.Double);
                Morph.Changed += Apply;
            }
            return Morph;
        }

        private void Apply(double morph)
        {
            var scale = 1 - (1 - SwapScale) * morph;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
            Glyph.Effect = ArcBlur.Get(SwapBlur * morph);
        }
    }
}

/// <summary>
/// A slot's badge: a count on a small pill with a ring of the bar's fill around it. The digits roll when the count changes;
/// the pill pops in from half size when the count leaves 0 and shrinks away when it returns to 0.
/// </summary>
internal sealed class ToolDockBadge : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ToolDockBadge, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<ToolDockBadge, IBrush?>(nameof(RingBrush));

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<ToolDockBadge, double>(nameof(RingThickness), 2);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ToolDockBadge, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<ToolDockBadge>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<ToolDockBadge>();

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<ToolDockBadge, double>(nameof(FontSize), 11);

    public static readonly StyledProperty<double> PillHeightProperty =
        AvaloniaProperty.Register<ToolDockBadge, double>(nameof(PillHeight), 16);

    /// <summary>Digit roll: fitted ω0 18.9 (Arc Dock, design reference).</summary>
    private static readonly MotionSpec s_roll = MotionSpec.Spring(SpringSpec.FromVisual(0.28, 0));

    private readonly RollingDigits _digits;
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _pop;
    private MotionValue<double>? _fade;
    private int _count;

    static ToolDockBadge()
    {
        AffectsMeasure<ToolDockBadge>(PaddingProperty, PillHeightProperty, FontSizeProperty);
        AffectsRender<ToolDockBadge>(FillProperty, RingBrushProperty, RingThicknessProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolDockBadge>(false);
    }

    public ToolDockBadge()
    {
        _digits = new RollingDigits
        {
            UseGrouping = false,
            Falloff = RollingDigitsFalloff.None,
            WheelMotion = s_roll,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        VisualChildren.Add(_digits);
        LogicalChildren.Add(_digits);
        RenderTransform = _scale;
        IsVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? RingBrush { get => GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double PillHeight { get => GetValue(PillHeightProperty); set => SetValue(PillHeightProperty, value); }

    /// <summary>The count shown (0 while hidden or leaving).</summary>
    internal int Count => _count;

    /// <summary>The digits' value (keeps the last count while the pill leaves).</summary>
    internal double DigitsValue => _digits.Value;

    internal double PopScale => _scale.ScaleX;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _digits.Foreground = Foreground;
        }
        else if (change.Property == FontSizeProperty)
        {
            _digits.FontSize = FontSize;
            // One digit per line box: the wheel's glyphs sit a line apart, as in a CSS digit strip.
            _digits.GlyphPitch = PillHeight / Math.Max(1, FontSize);
        }
        else if (change.Property == PillHeightProperty)
        {
            _digits.GlyphPitch = PillHeight / Math.Max(1, FontSize);
        }
    }

    /// <summary>Shows <paramref name="count"/>: rolls the digits, or pops the pill in or out at 0.</summary>
    internal async void SetCount(int count, bool animate)
    {
        var was = _count;
        _count = count;
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && level != ArcMotionLevel.Off;
        if (count > 0)
        {
            _digits.IsInstant = !animate || was == 0;
            _digits.Value = count;
            if (was == 0)
            {
                IsVisible = true;
                EnsureMotion();
                if (!animate)
                {
                    _pop!.Jump(1);
                    _fade!.Jump(1);
                    return;
                }
                _pop!.Jump(level == ArcMotionLevel.Full ? 0.5 : 1);
                _fade!.Jump(0);
                _ = _pop.AnimateTo(1, MotionSpec.Spring(ToolDockTrayButton.EnterSpring));
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ToolDockItem.EaseOut));
            }
            return;
        }
        if (was == 0)
        {
            return;
        }
        EnsureMotion();
        if (!animate)
        {
            _fade!.Jump(0);
            IsVisible = false;
            return;
        }
        if (level == ArcMotionLevel.Full)
        {
            _ = _pop!.AnimateTo(0.5, MotionSpec.Tween(ArcMotionPresets.Fast, ToolDockItem.EaseOut));
        }
        var done = await _fade!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ToolDockItem.EaseOut));
        if (done && _count == 0)
        {
            IsVisible = false;
        }
    }

    private void EnsureMotion()
    {
        if (_pop is not null)
        {
            return;
        }
        _pop = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _pop.Changed += value =>
        {
            _scale.ScaleX = value;
            _scale.ScaleY = value;
        };
        _fade.Changed += value => Opacity = value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _digits.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var height = PillHeight;
        var width = Math.Max(height, Math.Ceiling(_digits.DesiredSize.Width) + Padding.Left + Padding.Right);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = _digits.DesiredSize;
        _digits.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        var radius = Math.Min(CornerRadius.TopLeft, rect.Height / 2);
        var ring = RingThickness;
        if (ring > 0 && RingBrush is { } ringBrush)
        {
            context.DrawRectangle(ringBrush, null, new RoundedRect(rect.Inflate(ring), radius + ring));
        }
        context.DrawRectangle(Fill, null, new RoundedRect(rect, radius));
    }
}

/// <summary>
/// The floating label of a <see cref="ToolDock"/>: a dark pill with the name and the key hint of the slot or member under
/// the mouse or the keyboard focus. It appears with a short rise and scale, glides between targets on springs, and swaps
/// its text with a quick blurred crossfade. It is decoration: assistive technology reads the slots' own names.
/// </summary>
internal sealed class ToolDockLabel : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ToolDockLabel, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ToolDockLabel, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> ShortcutFillProperty =
        AvaloniaProperty.Register<ToolDockLabel, IBrush?>(nameof(ShortcutFill));

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<ToolDockLabel, BoxShadows>(nameof(BoxShadow));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<ToolDockLabel>();

    public static readonly StyledProperty<CornerRadius> ShortcutCornerRadiusProperty =
        AvaloniaProperty.Register<ToolDockLabel, CornerRadius>(nameof(ShortcutCornerRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<ToolDockLabel>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<ToolDockLabel>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<ToolDockLabel>();

    public static readonly StyledProperty<double> ShortcutFontSizeProperty =
        AvaloniaProperty.Register<ToolDockLabel, double>(nameof(ShortcutFontSize), 10);

    public static readonly StyledProperty<double> ShortcutSizeProperty =
        AvaloniaProperty.Register<ToolDockLabel, double>(nameof(ShortcutSize), 16);

    public static readonly StyledProperty<double> ShortcutSpacingProperty =
        AvaloniaProperty.Register<ToolDockLabel, double>(nameof(ShortcutSpacing), 8);

    public static readonly StyledProperty<Thickness> PaddingProperty =
        Decorator.PaddingProperty.AddOwner<ToolDockLabel>();

    public static readonly StyledProperty<double> PillHeightProperty =
        AvaloniaProperty.Register<ToolDockLabel, double>(nameof(PillHeight), 28);

    /// <summary>The gap between the label and the bar or tray under it.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ToolDockLabel, double>(nameof(Gap), 10);

    /// <summary>Appearing: fade, scale 0.96 → 1 and a 4 px rise on one spring (fitted ω0 23.9, ζ 1).</summary>
    private static readonly SpringSpec s_show = SpringSpec.FromVisual(0.22, 0);

    /// <summary>Gliding: the position (fitted ω0 17.4, ζ 1) and the width (fitted ω0 12.6, the smooth spring).</summary>
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.3, 0);

    /// <summary>Hiding: 140 ms on the standard curve (the frames follow it closely).</summary>
    private static readonly TimeSpan s_hide = TimeSpan.FromMilliseconds(140);

    /// <summary>Reduced motion keeps short fades only.</summary>
    private static readonly TimeSpan s_reducedFade = TimeSpan.FromMilliseconds(100);

    /// <summary>Text swap: the old text leaves in 120 ms (ease-out), the new one arrives in 180 ms (enter curve).</summary>
    private static readonly TimeSpan s_textOut = ArcMotionPresets.Instant;
    private static readonly TimeSpan s_textIn = ArcMotionPresets.Exit;

    private const double HiddenScale = 0.96;
    private const double HiddenDrop = 4;

    private readonly ToolDockLabelText _a;
    private readonly ToolDockLabelText _b;
    private readonly MatrixTransform _transform = new();
    private ToolDockLabelText _front;
    private MotionValue<Point>? _anchor;
    private MotionValue<double>? _width;
    private MotionValue<double>? _presence;
    private bool _shown;

    static ToolDockLabel()
    {
        AffectsRender<ToolDockLabel>(BackgroundProperty, BoxShadowProperty, CornerRadiusProperty);
        AffectsMeasure<ToolDockLabel>(PillHeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolDockLabel>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ToolDockLabel>(false);
    }

    public ToolDockLabel()
    {
        _a = new ToolDockLabelText(this);
        _b = new ToolDockLabelText(this);
        _a.Opacity = 0;
        _b.Opacity = 0;
        _front = _a;
        VisualChildren.Add(_a);
        VisualChildren.Add(_b);
        LogicalChildren.Add(_a);
        LogicalChildren.Add(_b);
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        RenderTransform = _transform;
        Opacity = 0;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? ShortcutFill { get => GetValue(ShortcutFillProperty); set => SetValue(ShortcutFillProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public CornerRadius ShortcutCornerRadius { get => GetValue(ShortcutCornerRadiusProperty); set => SetValue(ShortcutCornerRadiusProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public double ShortcutFontSize { get => GetValue(ShortcutFontSizeProperty); set => SetValue(ShortcutFontSizeProperty, value); }
    public double ShortcutSize { get => GetValue(ShortcutSizeProperty); set => SetValue(ShortcutSizeProperty, value); }
    public double ShortcutSpacing { get => GetValue(ShortcutSpacingProperty); set => SetValue(ShortcutSpacingProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public double PillHeight { get => GetValue(PillHeightProperty); set => SetValue(PillHeightProperty, value); }
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty
            || change.Property == FontWeightProperty || change.Property == ShortcutFontSizeProperty
            || change.Property == ShortcutSizeProperty || change.Property == ShortcutSpacingProperty
            || change.Property == ShortcutFillProperty || change.Property == ShortcutCornerRadiusProperty)
        {
            // Inherited fonts can change while the base constructor runs, before the layers exist.
            _a?.Invalidate();
            _b?.Invalidate();
        }
    }

    /// <summary>Whether the label is meant to be visible (it may still be fading in).</summary>
    internal bool IsShown => _shown;

    /// <summary>The text the label shows (or is changing to).</summary>
    internal string Text => _front.Text;

    internal string? Shortcut => _front.Shortcut;

    /// <summary>The pill's current rectangle in the stage's coordinates, before the appear scale and rise.</summary>
    internal Rect PillRect
    {
        get
        {
            var anchor = _anchor?.Value ?? default;
            var width = _width?.Value ?? 0;
            return new Rect(anchor.X - width / 2, anchor.Y - PillHeight, width, PillHeight);
        }
    }

    /// <summary>The label's current opacity (0 hidden, 1 shown).</summary>
    internal double Presence => _presence?.Value ?? 0;

    /// <summary>
    /// Shows <paramref name="text"/> centred over <paramref name="centerX"/> with its bottom at <paramref name="bottom"/>.
    /// A label that is (still partly) visible glides there; a hidden one appears in place.
    /// </summary>
    internal void ShowAt(string text, string? shortcut, double centerX, double bottom)
    {
        EnsureMotion();
        var level = ArcMotion.GetEffectiveLevel(this);
        var visible = _presence!.Value > 0.01;
        SetText(text, shortcut, level);
        var width = Math.Ceiling(_front.DesiredSize.Width) + Padding.Left + Padding.Right;
        var anchor = new Point(centerX, bottom);
        if (visible && level == ArcMotionLevel.Full)
        {
            _anchor!.AnimateTo(anchor, MotionSpec.Spring(s_glide));
            _width!.AnimateTo(width, MotionSpec.Spring(ArcMotionPresets.Smooth));
        }
        else
        {
            _anchor!.Jump(anchor);
            _width!.Jump(width);
        }
        if (!_shown)
        {
            _shown = true;
            switch (level)
            {
                case ArcMotionLevel.Full:
                    _presence.AnimateTo(1, MotionSpec.Spring(s_show));
                    break;
                case ArcMotionLevel.Reduced:
                    _presence.AnimateTo(1, MotionSpec.Tween(s_reducedFade, ArcEasings.Standard));
                    break;
                default:
                    _presence.Jump(1);
                    break;
            }
        }
        Apply();
    }

    /// <summary>Hides the label.</summary>
    internal void Hide()
    {
        if (!_shown || _presence is null)
        {
            return;
        }
        _shown = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        switch (level)
        {
            case ArcMotionLevel.Full:
                _presence.AnimateTo(0, MotionSpec.Tween(s_hide, ArcEasings.Standard));
                break;
            case ArcMotionLevel.Reduced:
                _presence.AnimateTo(0, MotionSpec.Tween(s_reducedFade, ArcEasings.Standard));
                break;
            default:
                _presence.Jump(0);
                break;
        }
    }

    private void SetText(string text, string? shortcut, ArcMotionLevel level)
    {
        if (_front.Text == text && _front.Shortcut == shortcut)
        {
            if (_front.Opacity < 1 && _front.Fade is not { IsAnimating: true })
            {
                _front.FadeTo(1, MotionSpec.Instant, blur: false);
            }
            return;
        }
        var outgoing = _front;
        var incoming = ReferenceEquals(_front, _a) ? _b : _a;
        _front = incoming;
        incoming.SetText(text, shortcut);
        incoming.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (level == ArcMotionLevel.Off || _presence!.Value <= 0.01 && !_shown)
        {
            // Nothing is on screen yet: the text arrives with the label itself.
            outgoing.FadeTo(0, MotionSpec.Instant, blur: false);
            incoming.FadeTo(1, level == ArcMotionLevel.Full
                ? MotionSpec.Tween(s_textIn, ArcEasings.Enter)
                : MotionSpec.Instant, blur: level == ArcMotionLevel.Full);
            return;
        }
        var full = level == ArcMotionLevel.Full;
        outgoing.FadeTo(0, MotionSpec.Tween(full ? s_textOut : s_reducedFade, ToolDockItem.EaseOut), blur: full);
        incoming.FadeTo(1, MotionSpec.Tween(full ? s_textIn : s_reducedFade, full ? ArcEasings.Enter : ArcEasings.Standard), blur: full);
    }

    private void EnsureMotion()
    {
        if (_presence is not null)
        {
            return;
        }
        _anchor = new MotionValue<Point>(this, default, MotionVectors.Point);
        _width = new MotionValue<double>(this, 0, MotionVectors.Double);
        _presence = new MotionValue<double>(this, 0, MotionVectors.Double);
        _anchor.Changed += _ => Apply();
        _width.Changed += _ => Apply();
        _presence.Changed += _ => Apply();
    }

    private void Apply()
    {
        var pill = PillRect;
        var presence = _presence?.Value ?? 0;
        Opacity = presence;
        var full = ArcMotion.IsFull(this);
        var scale = full ? HiddenScale + (1 - HiddenScale) * presence : 1;
        var drop = full ? HiddenDrop * (1 - presence) : 0;
        var center = pill.Center;
        _transform.Matrix = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale)
                            * Matrix.CreateTranslation(center.X, center.Y + drop);
        PlaceText(_a, pill);
        PlaceText(_b, pill);
        InvalidateVisual();
    }

    private static void PlaceText(ToolDockLabelText layer, Rect pill)
    {
        var size = layer.DesiredSize;
        layer.Offset = new Point(pill.X + (pill.Width - size.Width) / 2, pill.Y + (pill.Height - size.Height) / 2);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _a.Measure(infinite);
        _b.Measure(infinite);
        return new Size(0, PillHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _a.Arrange(new Rect(_a.DesiredSize));
        _b.Arrange(new Rect(_b.DesiredSize));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_presence is null || Background is null)
        {
            return;
        }
        var pill = PillRect;
        if (pill.Width <= 0)
        {
            return;
        }
        context.DrawRectangle(Background, null, new RoundedRect(pill, Math.Min(CornerRadius.TopLeft, pill.Height / 2)), BoxShadow);
    }
}

/// <summary>
/// One text layer of the floating label: the name, then the key hint on its own small pill. Two layers let the old text
/// blur away while the new one comes in. Text is laid out once per change and drawn from the cached layouts.
/// </summary>
internal sealed class ToolDockLabelText : Control
{
    /// <summary>Arc's label tracking: −0.16 px at 12 px.</summary>
    private const double Tracking = -0.16 / 12;

    /// <summary>The key hint is drawn at 80% opacity.</summary>
    private const double ShortcutOpacity = 0.8;

    private const double ShortcutPadding = 4;

    /// <summary>The blur at the far end of a text swap, in px.</summary>
    private const double SwapBlur = 2;

    private readonly ToolDockLabel _owner;
    private readonly TranslateTransform _offset = new();
    private TextLayout? _text;
    private TextLayout? _shortcut;
    private bool _dirty = true;

    public ToolDockLabelText(ToolDockLabel owner)
    {
        _owner = owner;
        RenderTransform = _offset;
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);
        IsHitTestVisible = false;
    }

    public string Text { get; private set; } = string.Empty;

    public string? Shortcut { get; private set; }

    public MotionValue<double>? Fade { get; private set; }

    public Point Offset
    {
        get => new(_offset.X, _offset.Y);
        set
        {
            _offset.X = value.X;
            _offset.Y = value.Y;
        }
    }

    public void SetText(string text, string? shortcut)
    {
        Text = text;
        Shortcut = shortcut is { Length: > 0 } ? shortcut : null;
        _dirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    public void FadeTo(double target, MotionSpec spec, bool blur)
    {
        if (Fade is null)
        {
            Fade = new MotionValue<double>(this, Opacity, MotionVectors.Double);
            Fade.Changed += value =>
            {
                Opacity = value;
                Effect = UsesBlur ? ArcBlur.Get(SwapBlur * (1 - value)) : null;
            };
        }
        UsesBlur = blur;
        if (spec is MotionSpec.InstantSpec)
        {
            Fade.Jump(target);
            return;
        }
        Fade.AnimateTo(target, spec);
    }

    private bool UsesBlur { get; set; }

    internal void Invalidate()
    {
        _dirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void EnsureLayouts()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        _text?.Dispose();
        _shortcut?.Dispose();
        var typeface = new Typeface(_owner.FontFamily, FontStyle.Normal, _owner.FontWeight);
        var size = _owner.FontSize;
        _text = new TextLayout(Text, typeface, size, _owner.Foreground, lineHeight: size, letterSpacing: Tracking * size);
        _shortcut = Shortcut is null
            ? null
            : new TextLayout(Shortcut, typeface, _owner.ShortcutFontSize, _owner.Foreground, lineHeight: _owner.ShortcutFontSize);
    }

    private double ShortcutWidth => _shortcut is null ? 0 : Math.Max(_owner.ShortcutSize, Math.Ceiling(_shortcut.Width) + 2 * ShortcutPadding);

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayouts();
        var width = Math.Ceiling(_text!.Width);
        var height = Math.Max(_text.Height, _shortcut is null ? 0 : _owner.ShortcutSize);
        if (_shortcut is not null)
        {
            width += _owner.ShortcutSpacing + ShortcutWidth;
        }
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        EnsureLayouts();
        var height = Bounds.Height;
        _text!.Draw(context, new Point(0, (height - _text.Height) / 2));
        if (_shortcut is null)
        {
            return;
        }
        var key = new Rect(Math.Ceiling(_text.Width) + _owner.ShortcutSpacing, (height - _owner.ShortcutSize) / 2, ShortcutWidth, _owner.ShortcutSize);
        using (context.PushOpacity(ShortcutOpacity))
        {
            context.DrawRectangle(_owner.ShortcutFill, null, new RoundedRect(key, Math.Min(_owner.ShortcutCornerRadius.TopLeft, key.Height / 2)));
            _shortcut.Draw(context, new Point(key.X + (key.Width - _shortcut.Width) / 2, key.Y + (key.Height - _shortcut.Height) / 2));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _text?.Dispose();
        _shortcut?.Dispose();
        _text = null;
        _shortcut = null;
        _dirty = true;
    }
}
