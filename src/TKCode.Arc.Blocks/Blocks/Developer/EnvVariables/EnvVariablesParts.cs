using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The tints Env variables mixes from ArcDesign tokens at run time (Arc writes them as <c>color-mix()</c>). Read only while
/// the owner is attached; recompute on theme and resource changes.
/// </summary>
internal readonly record struct EnvPalette(
    IBrush ScopeHover,
    IBrush ScopePressed,
    IBrush ScopeFocus,
    IBrush EditingRow,
    IBrush PanelFill,
    IBrush AccentFill,
    IBrush AccentEdge,
    IBrush SuccessFill,
    IBrush SuccessEdge,
    IBrush DangerFill,
    IBrush DangerEdge)
{
    /// <summary>Mixes the palette for <paramref name="owner"/>'s theme.</summary>
    public static EnvPalette For(Control owner)
    {
        var surface = ArcTokens.GetColor(owner, ArcDesignTokenKind.Surface);
        var muted = ArcTokens.GetColor(owner, ArcDesignTokenKind.SurfaceMuted);
        var foreground = ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground);
        var border = ArcTokens.GetColor(owner, ArcDesignTokenKind.Border);
        var accent = ArcTokens.GetColor(owner, ArcDesignTokenKind.Accent);
        var success = ArcTokens.GetColor(owner, ArcDesignTokenKind.Success);
        var danger = ArcTokens.GetColor(owner, ArcDesignTokenKind.Danger);
        static IBrush B(Color c) => new ImmutableSolidColorBrush(c);
        return new EnvPalette(
            // Environment switches: Foreground 4% into Surface on hover (.966 / .245), 8% pressed, 7% keyboard focus.
            B(Oklch.MixOklab(surface, foreground, 0.04)),
            B(Oklch.MixOklab(surface, foreground, 0.08)),
            B(Oklch.MixOklab(surface, foreground, 0.07)),
            // A row being edited: SurfaceMuted 45% into Surface (.990 / .235); the add panel 55% (.988 / .240).
            B(Oklch.MixOklab(surface, muted, 0.45)),
            B(Oklch.MixOklab(surface, muted, 0.55)),
            // The redeploy banner: Accent 7% into Surface, edge Accent 24% into Border; Success and Danger alike.
            B(Oklch.MixOklab(surface, accent, 0.07)),
            B(Oklch.MixOklab(border, accent, 0.24)),
            B(Oklch.MixOklab(surface, success, 0.07)),
            B(Oklch.MixOklab(border, success, 0.24)),
            B(Oklch.MixOklab(surface, danger, 0.07)),
            B(Oklch.MixOklab(border, danger, 0.24)));
    }
}

/// <summary>
/// The 18px check box drawn inside an environment switch (radius 6): a 1.5px BorderStrong ring when off; when on a
/// ControlOn fill that fades in over 120ms while it grows from .6 to 1 on a quick spring, and a check mark in the
/// Background color drawn along its length with the fill. Code-driven (<see cref="MotionValue{T}"/>) because the mark is
/// drawn partially along its path, which no transition can express. Reduced motion and Off jump (Arc drops it entirely).
/// </summary>
internal sealed class EnvCheckGlyph : Control
{
    /// <summary>Whether the box is ticked.</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<EnvCheckGlyph, bool>(nameof(IsChecked));

    private const double Size = 18;
    private const double Radius = 6;
    private static readonly Point P0 = new(5, 9.4);
    private static readonly Point P1 = new(7.6, 12);
    private static readonly Point P2 = new(13, 6.6);
    private static readonly double L1 = Distance(P0, P1);
    private static readonly double L2 = Distance(P1, P2);

    private MotionValue<double>? _fill;
    private MotionValue<double>? _grow;
    private IBrush? _onBrush;
    private ImmutablePen? _ringPen;
    private ImmutablePen? _checkPen;

    static EnvCheckGlyph()
    {
        AffectsRender<EnvCheckGlyph>(IsCheckedProperty);
    }

    public EnvCheckGlyph()
    {
        Width = Size;
        Height = Size;
        IsHitTestVisible = false;
    }

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>Drawn fill opacity (tests).</summary>
    internal double FillProgress => _fill?.Value ?? (IsChecked ? 1 : 0);

    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        ClearBrushes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => OnThemeChanged(sender, EventArgs.Empty);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ClearBrushes();
        InvalidateVisual();
    }

    private void ClearBrushes()
    {
        _onBrush = null;
        _ringPen = null;
        _checkPen = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            Animate(IsChecked);
        }
    }

    private void Animate(bool on)
    {
        _fill ??= Create(on ? 0 : 1);
        _grow ??= Create(on ? 0 : 1);
        var target = on ? 1.0 : 0.0;
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            _fill.Jump(target);
            _grow.Jump(target);
            InvalidateVisual();
            return;
        }
        _ = _fill.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, new SineEaseOut()));
        _ = _grow.AnimateTo(target, MotionSpec.Spring(DeveloperSprings.Quick));
    }

    private MotionValue<double> Create(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        if (VisualRoot is null)
        {
            return;
        }
        if (_ringPen is null || _checkPen is null || _onBrush is null)
        {
            try
            {
                _onBrush = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.ControlOn));
                _ringPen = new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong)), 1.5);
                _checkPen = new ImmutablePen(new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Background)), 1.75,
                    lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            }
            catch (Exception)
            {
                return;
            }
        }
        var fill = Math.Clamp(_fill?.Value ?? (IsChecked ? 1 : 0), 0, 1);
        var grow = _grow?.Value ?? (IsChecked ? 1 : 0);
        if (fill < 1)
        {
            using (context.PushOpacity(1 - fill))
            {
                context.DrawRectangle(null, _ringPen, new Rect(0.75, 0.75, Size - 1.5, Size - 1.5), Radius - 0.75, Radius - 0.75);
            }
        }
        if (fill <= 0.001)
        {
            return;
        }
        var scale = 0.6 + 0.4 * grow;
        using (context.PushOpacity(fill))
        using (context.PushTransform(Matrix.CreateTranslation(-Size / 2, -Size / 2) * Matrix.CreateScale(scale, scale)
                                     * Matrix.CreateTranslation(Size / 2, Size / 2)))
        {
            context.DrawRectangle(_onBrush, null, new Rect(0, 0, Size, Size), Radius, Radius);
            var length = Math.Clamp(grow, 0, 1) * (L1 + L2);
            if (length <= 0.01)
            {
                return;
            }
            var first = Math.Min(length, L1) / L1;
            context.DrawLine(_checkPen, P0, Lerp(P0, P1, first));
            if (length > L1)
            {
                context.DrawLine(_checkPen, P1, Lerp(P1, P2, Math.Min(1, (length - L1) / L2)));
            }
        }
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}

/// <summary>
/// An environment switch of Env variables (<c>aria-pressed</c> button). Matrix face: 92 × 44, radius 12, the box alone
/// (its name is for screen readers). Pill face (stacked rows and the add panel): 36 high, a pill with a 1px edge
/// (BorderStrong when on, Border when off), the box and the environment's name. Hover, pressed and keyboard focus fills
/// are mixed in code (<see cref="EnvPalette"/>).
/// </summary>
internal sealed class EnvScopeToggle : ToggleButton
{
    /// <summary>The pill face with a visible label.</summary>
    public static readonly StyledProperty<bool> IsPillProperty =
        AvaloniaProperty.Register<EnvScopeToggle, bool>(nameof(IsPill));

    /// <summary>The visible label of the pill face.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<EnvScopeToggle, string?>(nameof(Label));

    /// <summary>Whether the box shows ticked (follows <see cref="ToggleButton.IsChecked"/>; read by the template).</summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<EnvScopeToggle, bool>(nameof(IsOn));

    private EnvPalette? _palette;

    /// <inheritdoc cref="IsPillProperty"/>
    public bool IsPill { get => GetValue(IsPillProperty); set => SetValue(IsPillProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IsOnProperty"/>
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    /// <summary>The environment's key.</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// When set, a click asks this callback instead of flipping the state (the block refuses a switch that would cover an
    /// environment twice and sets <see cref="ToggleButton.IsChecked"/> itself).
    /// </summary>
    public Action<EnvScopeToggle>? ToggleRequested { get; set; }

    protected override void Toggle()
    {
        if (ToggleRequested is { } request)
        {
            request(this);
            return;
        }
        base.Toggle();
    }

    protected override Type StyleKeyOverride => typeof(EnvScopeToggle);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        _palette = null;
        UpdateFill();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => OnThemeChanged(sender, EventArgs.Empty);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _palette = null;
        UpdateFill();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            SetCurrentValue(IsOnProperty, IsChecked == true);
        }
        if (change.Property == IsPointerOverProperty || change.Property == IsPressedProperty || change.Property == IsPillProperty)
        {
            UpdateFill();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateFill();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFill();
    }

    private void UpdateFill()
    {
        if (VisualRoot is null)
        {
            return;
        }
        try
        {
            _palette ??= EnvPalette.For(this);
        }
        catch (Exception)
        {
            return;
        }
        var p = _palette.Value;
        IBrush rest = IsPill ? (IBrush?)TryGet(ArcDesignTokenKind.Surface) ?? Brushes.Transparent : Brushes.Transparent;
        Background = IsPressed ? p.ScopePressed
            : Classes.Contains(":focus-visible") ? p.ScopeFocus
            : IsPointerOver ? p.ScopeHover
            : rest;
    }

    private IBrush? TryGet(ArcDesignTokenKind kind)
    {
        try
        {
            return new ImmutableSolidColorBrush(ArcTokens.GetColor(this, kind));
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>How an <see cref="EnvRowPanel"/> lays out a row.</summary>
internal enum EnvRowLayout
{
    /// <summary>One line: key, value, one column per environment, edit (Arc ≥ 760).</summary>
    Matrix,

    /// <summary>Three lines: key and edit, value, environment pills (Arc &lt; 760).</summary>
    Stacked
}

/// <summary>
/// Lays out one variable row (Arc uses a CSS grid with the switch group as <c>display:contents</c>, which Avalonia has
/// not). Children in order: the key cell, the value cell, one switch per environment, the edit button. Matrix: columns
/// key 7fr, value 9fr, switches <see cref="ScopeWidth"/> each, edit <see cref="EditWidth"/>, 12 apart, everything
/// centered vertically on a line at least <see cref="MinLineHeight"/> high. Stacked: key with edit on the right (36),
/// the value across, then the pills in as many columns of at least 120 as fit (6 apart), lines 8 apart.
/// </summary>
internal sealed class EnvRowPanel : Panel
{
    /// <summary>Matrix or stacked.</summary>
    public static readonly StyledProperty<EnvRowLayout> LayoutProperty =
        AvaloniaProperty.Register<EnvRowPanel, EnvRowLayout>(nameof(Layout));

    /// <summary>Width of an environment column (92).</summary>
    public static readonly StyledProperty<double> ScopeWidthProperty =
        AvaloniaProperty.Register<EnvRowPanel, double>(nameof(ScopeWidth), 92);

    /// <summary>Width of the edit column (52).</summary>
    public static readonly StyledProperty<double> EditWidthProperty =
        AvaloniaProperty.Register<EnvRowPanel, double>(nameof(EditWidth), 52);

    /// <summary>Matrix line height at least (44: the switches' hit area).</summary>
    public static readonly StyledProperty<double> MinLineHeightProperty =
        AvaloniaProperty.Register<EnvRowPanel, double>(nameof(MinLineHeight), 44);

    public const double ColumnGap = 12;
    public const double LineGap = 8;
    public const double PillGap = 6;
    public const double PillMin = 120;
    public const double KeyShare = 7;
    public const double ValueShare = 9;

    static EnvRowPanel()
    {
        AffectsMeasure<EnvRowPanel>(LayoutProperty, ScopeWidthProperty, EditWidthProperty, MinLineHeightProperty);
    }

    /// <inheritdoc cref="LayoutProperty"/>
    public EnvRowLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="ScopeWidthProperty"/>
    public double ScopeWidth { get => GetValue(ScopeWidthProperty); set => SetValue(ScopeWidthProperty, value); }

    /// <inheritdoc cref="EditWidthProperty"/>
    public double EditWidth { get => GetValue(EditWidthProperty); set => SetValue(EditWidthProperty, value); }

    /// <inheritdoc cref="MinLineHeightProperty"/>
    public double MinLineHeight { get => GetValue(MinLineHeightProperty); set => SetValue(MinLineHeightProperty, value); }

    private int ScopeCount => Math.Max(0, Children.Count - 3);

    /// <summary>The matrix column widths for <paramref name="width"/> (key, value), shared with the column header.</summary>
    public static (double Key, double Value) FlexColumns(double width, int scopes, double scopeWidth, double editWidth)
    {
        var fixedWidth = scopes * scopeWidth + editWidth + (scopes + 2) * ColumnGap;
        var flex = Math.Max(0, width - fixedWidth);
        return (flex * KeyShare / (KeyShare + ValueShare), flex * ValueShare / (KeyShare + ValueShare));
    }

    /// <summary>Pill columns for <paramref name="width"/> and <paramref name="count"/> pills (CSS auto-fit, minmax(120, 1fr)).</summary>
    public static int PillColumns(double width, int count)
    {
        if (count <= 0)
        {
            return 1;
        }
        var fit = (int)Math.Floor((width + PillGap) / (PillMin + PillGap));
        return Math.Clamp(fit, 1, count);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 3)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        var n = ScopeCount;
        var key = Children[0];
        var value = Children[1];
        var edit = Children[^1];
        if (Layout == EnvRowLayout.Matrix)
        {
            var (kw, vw) = FlexColumns(width, n, ScopeWidth, EditWidth);
            key.Measure(new Size(kw, double.PositiveInfinity));
            value.Measure(new Size(vw, double.PositiveInfinity));
            var height = Math.Max(MinLineHeight, Math.Max(key.DesiredSize.Height, value.DesiredSize.Height));
            for (var i = 0; i < n; i++)
            {
                Children[2 + i].Measure(new Size(ScopeWidth, double.PositiveInfinity));
                height = Math.Max(height, Children[2 + i].DesiredSize.Height);
            }
            edit.Measure(new Size(EditWidth, double.PositiveInfinity));
            height = Math.Max(height, edit.DesiredSize.Height);
            return new Size(width, height);
        }
        const double editSlot = 36;
        key.Measure(new Size(Math.Max(0, width - editSlot - ColumnGap), double.PositiveInfinity));
        edit.Measure(new Size(editSlot, double.PositiveInfinity));
        value.Measure(new Size(width, double.PositiveInfinity));
        var first = Math.Max(key.DesiredSize.Height, edit.DesiredSize.Height);
        var total = first + LineGap + value.DesiredSize.Height;
        if (n > 0)
        {
            var cols = PillColumns(width, n);
            var pillWidth = (width - (cols - 1) * PillGap) / cols;
            var rows = (int)Math.Ceiling(n / (double)cols);
            double pillHeight = 0;
            for (var i = 0; i < n; i++)
            {
                Children[2 + i].Measure(new Size(pillWidth, double.PositiveInfinity));
                pillHeight = Math.Max(pillHeight, Children[2 + i].DesiredSize.Height);
            }
            total += LineGap + rows * pillHeight + (rows - 1) * PillGap;
        }
        return new Size(width, total);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 3)
        {
            return finalSize;
        }
        var width = finalSize.Width;
        var n = ScopeCount;
        var key = Children[0];
        var value = Children[1];
        var edit = Children[^1];
        if (Layout == EnvRowLayout.Matrix)
        {
            var (kw, vw) = FlexColumns(width, n, ScopeWidth, EditWidth);
            var h = finalSize.Height;
            double x = 0;
            ArrangeCentered(key, x, kw, h);
            x += kw + ColumnGap;
            ArrangeCentered(value, x, vw, h);
            x += vw + ColumnGap;
            for (var i = 0; i < n; i++)
            {
                ArrangeCentered(Children[2 + i], x, ScopeWidth, h);
                x += ScopeWidth + ColumnGap;
            }
            ArrangeCentered(edit, x, EditWidth, h);
            return finalSize;
        }
        const double editSlot = 36;
        var first = Math.Max(key.DesiredSize.Height, edit.DesiredSize.Height);
        key.Arrange(new Rect(0, (first - key.DesiredSize.Height) / 2, Math.Max(0, width - editSlot - ColumnGap), key.DesiredSize.Height));
        edit.Arrange(new Rect(width - editSlot, (first - edit.DesiredSize.Height) / 2, editSlot, edit.DesiredSize.Height));
        var y = first + LineGap;
        value.Arrange(new Rect(0, y, width, value.DesiredSize.Height));
        y += value.DesiredSize.Height + LineGap;
        if (n > 0)
        {
            var cols = PillColumns(width, n);
            var pillWidth = (width - (cols - 1) * PillGap) / cols;
            double pillHeight = 0;
            for (var i = 0; i < n; i++)
            {
                pillHeight = Math.Max(pillHeight, Children[2 + i].DesiredSize.Height);
            }
            for (var i = 0; i < n; i++)
            {
                var col = i % cols;
                var row = i / cols;
                Children[2 + i].Arrange(new Rect(col * (pillWidth + PillGap), y + row * (pillHeight + PillGap), pillWidth, pillHeight));
            }
        }
        return finalSize;
    }

    // The child aligns itself in its slot (set VerticalAlignment Center on cells, Bottom on the column header).
    private static void ArrangeCentered(Control child, double x, double width, double height) =>
        child.Arrange(new Rect(x, 0, width, height));
}

/// <summary>
/// Opens and closes one part below another by height and opacity together (Arc's banner, add panel, inline editor and
/// conflict note): opening grows on the Smooth spring with the opacity on the same progress, closing shrinks and fades
/// over 180ms EaseExit; the content is created when it opens and dropped when the close ends, so a closed part costs
/// nothing. Code-driven (<see cref="MotionValue{T}"/>) because the open and close motions differ (a spring one way, a
/// tween the other) and the content must leave the tree only after the close, which a ControlTheme transition cannot
/// sequence. Reduced motion and Off jump (as Arc). Clips only while moving (focus rings stay whole at rest).
/// </summary>
internal sealed class EnvDisclosure : Control
{
    private Control? _child;
    private MotionValue<double>? _progress;
    private readonly RectangleGeometry _clip = new();
    private bool _open;
    private int _generation;

    public EnvDisclosure()
    {
        ClipToBounds = false;
    }

    /// <summary>The content (null while closed).</summary>
    public Control? Child => _child;

    /// <summary>True from <see cref="Open"/> until <see cref="Close"/>.</summary>
    public bool IsOpen => _open;

    /// <summary>The drawn progress (0 closed, 1 open).</summary>
    public double Progress => _progress?.Value ?? (_open ? 1 : 0);

    /// <summary>Raised when a close finished and the content left the tree.</summary>
    public event EventHandler? Closed;

    /// <summary>Opens with <paramref name="content"/> (replacing what is shown); <paramref name="animate"/> false jumps.</summary>
    public void Open(Control content, bool animate = true)
    {
        _generation++;
        if (!ReferenceEquals(_child, content))
        {
            RemoveChild();
            _child = content;
            LogicalChildren.Add(content);
            VisualChildren.Add(content);
        }
        var wasOpen = _open;
        _open = true;
        _progress ??= CreateProgress();
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _progress.Jump(1);
            UpdateVisual();
            return;
        }
        if (!wasOpen && _progress.Value <= 0)
        {
            _progress.Jump(0);
        }
        _ = _progress.AnimateTo(1, MotionSpec.Spring(DeveloperSprings.Smooth));
    }

    /// <summary>Closes and drops the content once the close ends; <paramref name="animate"/> false drops it at once.</summary>
    public void Close(bool animate = true)
    {
        if (_child is null)
        {
            _open = false;
            return;
        }
        _open = false;
        var generation = ++_generation;
        _progress ??= CreateProgress();
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _progress.Jump(0);
            Finish();
            return;
        }
        _ = CloseAsync(generation);
    }

    private async Task CloseAsync(int generation)
    {
        var done = await _progress!.AnimateTo(0, DeveloperSprings.ExitTween());
        if (generation == _generation && !_open)
        {
            if (done || _progress.Value <= 0.001)
            {
                Finish();
            }
        }
    }

    private void Finish()
    {
        RemoveChild();
        UpdateVisual();
        InvalidateMeasure();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveChild()
    {
        if (_child is null)
        {
            return;
        }
        VisualChildren.Remove(_child);
        LogicalChildren.Remove(_child);
        _child = null;
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => UpdateVisual();
        return value;
    }

    private void UpdateVisual()
    {
        var p = Math.Clamp(Progress, 0, 1);
        if (_child is not null)
        {
            _child.Opacity = p;
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_child is null)
        {
            return default;
        }
        _child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var p = Math.Clamp(Progress, 0, 1);
        return new Size(_child.DesiredSize.Width, _child.DesiredSize.Height * p);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_child is null)
        {
            Clip = null;
            return finalSize;
        }
        var full = _child.DesiredSize.Height;
        _child.Arrange(new Rect(0, 0, finalSize.Width, full));
        if (finalSize.Height < full - 0.5)
        {
            _clip.Rect = new Rect(-8, 0, finalSize.Width + 16, finalSize.Height);
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }
}

/// <summary>
/// Two parts on one line when they fit (the first on the left, the second on the right), else the second under the
/// first (CSS flex-wrap with space-between). <see cref="IsStacked"/> forces the stack with both stretched across
/// (Arc &lt; 560). <see cref="FillsFirst"/> lets the first part take the room left by the second, up to its MaxWidth
/// (the search box: 360, shrinking to fit).
/// </summary>
internal sealed class EnvSplitPanel : Panel
{
    /// <summary>Stacks both parts across the width.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<EnvSplitPanel, bool>(nameof(IsStacked));

    /// <summary>The first part takes the room left by the second, up to its MaxWidth.</summary>
    public static readonly StyledProperty<bool> FillsFirstProperty =
        AvaloniaProperty.Register<EnvSplitPanel, bool>(nameof(FillsFirst));

    /// <summary>Horizontal gap on one line (16).</summary>
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<EnvSplitPanel, double>(nameof(ColumnGap), 16);

    /// <summary>Vertical gap when stacked (12).</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<EnvSplitPanel, double>(nameof(RowGap), 12);

    /// <summary>Vertical alignment of the parts on one line (Top for the header, Center for the toolbar).</summary>
    public static readonly StyledProperty<Avalonia.Layout.VerticalAlignment> LineAlignmentProperty =
        AvaloniaProperty.Register<EnvSplitPanel, Avalonia.Layout.VerticalAlignment>(nameof(LineAlignment), Avalonia.Layout.VerticalAlignment.Center);

    private bool _oneLine;
    private double _firstWidth;

    static EnvSplitPanel()
    {
        AffectsMeasure<EnvSplitPanel>(IsStackedProperty, FillsFirstProperty, ColumnGapProperty, RowGapProperty);
        AffectsArrange<EnvSplitPanel>(LineAlignmentProperty);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <inheritdoc cref="FillsFirstProperty"/>
    public bool FillsFirst { get => GetValue(FillsFirstProperty); set => SetValue(FillsFirstProperty, value); }

    /// <inheritdoc cref="ColumnGapProperty"/>
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <inheritdoc cref="RowGapProperty"/>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <inheritdoc cref="LineAlignmentProperty"/>
    public Avalonia.Layout.VerticalAlignment LineAlignment { get => GetValue(LineAlignmentProperty); set => SetValue(LineAlignmentProperty, value); }

    /// <summary>True when the last layout put both parts on one line.</summary>
    public bool IsOneLine => _oneLine;

    protected override Size MeasureOverride(Size availableSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var width = double.IsInfinity(availableSize.Width) ? 1200 : availableSize.Width;
        if (visible.Count == 0)
        {
            return default;
        }
        if (visible.Count == 1)
        {
            visible[0].Measure(new Size(width, double.PositiveInfinity));
            _oneLine = true;
            _firstWidth = FillsFirst ? Math.Min(width, visible[0].MaxWidth) : visible[0].DesiredSize.Width;
            return new Size(width, visible[0].DesiredSize.Height);
        }
        var first = visible[0];
        var second = visible[1];
        if (IsStacked)
        {
            first.Measure(new Size(width, double.PositiveInfinity));
            second.Measure(new Size(width, double.PositiveInfinity));
            _oneLine = false;
            _firstWidth = width;
            return new Size(width, first.DesiredSize.Height + RowGap + second.DesiredSize.Height);
        }
        second.Measure(new Size(width, double.PositiveInfinity));
        var room = width - second.DesiredSize.Width - ColumnGap;
        first.Measure(new Size(Math.Max(0, room), double.PositiveInfinity));
        var firstNatural = first.DesiredSize.Width;
        var minFirst = FillsFirst ? Math.Max(first.MinWidth, 200) : firstNatural;
        if (room >= minFirst - 0.5 && firstNatural <= room + 0.5)
        {
            _oneLine = true;
            _firstWidth = FillsFirst ? Math.Min(room, first.MaxWidth) : firstNatural;
            return new Size(width, Math.Max(first.DesiredSize.Height, second.DesiredSize.Height));
        }
        first.Measure(new Size(width, double.PositiveInfinity));
        _oneLine = false;
        _firstWidth = FillsFirst ? Math.Min(width, first.MaxWidth) : first.DesiredSize.Width;
        return new Size(width, first.DesiredSize.Height + RowGap + second.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        if (visible.Count == 0)
        {
            return finalSize;
        }
        var width = finalSize.Width;
        if (visible.Count == 1)
        {
            visible[0].Arrange(new Rect(0, 0, Math.Min(width, _firstWidth), visible[0].DesiredSize.Height));
            return finalSize;
        }
        var first = visible[0];
        var second = visible[1];
        if (IsStacked)
        {
            first.Arrange(new Rect(0, 0, width, first.DesiredSize.Height));
            second.Arrange(new Rect(0, first.DesiredSize.Height + RowGap, width, second.DesiredSize.Height));
            return finalSize;
        }
        if (_oneLine)
        {
            var h = finalSize.Height;
            var y1 = Align(first.DesiredSize.Height, h);
            var y2 = Align(second.DesiredSize.Height, h);
            first.Arrange(new Rect(0, y1, _firstWidth, first.DesiredSize.Height));
            second.Arrange(new Rect(width - second.DesiredSize.Width, y2, second.DesiredSize.Width, second.DesiredSize.Height));
            return finalSize;
        }
        first.Arrange(new Rect(0, 0, Math.Min(width, _firstWidth), first.DesiredSize.Height));
        second.Arrange(new Rect(0, first.DesiredSize.Height + RowGap, Math.Min(width, second.DesiredSize.Width), second.DesiredSize.Height));
        return finalSize;
    }

    private double Align(double child, double line) => LineAlignment switch
    {
        Avalonia.Layout.VerticalAlignment.Top => 0,
        Avalonia.Layout.VerticalAlignment.Bottom => line - child,
        _ => (line - child) / 2,
    };
}

/// <summary>
/// Holds the two views (variables and compare) and switches between them like Arc's wait-mode presence: the old view
/// fades out over 120ms EaseExit, then the new one rises 6px and fades in over 240ms (standard curve), while the height
/// springs (Smooth) from the old view's to the new one's. Code-driven (<see cref="MotionValue{T}"/>) because the two
/// fades run one after the other and the height must start from the old view, which a ControlTheme transition cannot
/// sequence. Reduced motion and Off switch at once. Outside a switch the height follows the shown view directly (its
/// rows animate themselves).
/// </summary>
internal sealed class EnvViewHost : Control
{
    private readonly Control[] _views;
    private readonly TranslateTransform _rise = new();
    private int _shown;
    private int _target;
    private MotionValue<double>? _height;
    private MotionValue<double>? _fade;
    private bool _switching;
    private bool _heightActive;
    private int _generation;
    private readonly RectangleGeometry _clip = new();

    public EnvViewHost(Control first, Control second)
    {
        _views = [first, second];
        foreach (var view in _views)
        {
            LogicalChildren.Add(view);
            VisualChildren.Add(view);
        }
        second.IsVisible = false;
        first.RenderTransform = null;
    }

    /// <summary>The index of the view shown (or being switched to).</summary>
    public int Target => _target;

    /// <summary>True while a switch runs.</summary>
    public bool IsSwitching => _switching;

    /// <summary>Shows view <paramref name="index"/>.</summary>
    public void Show(int index, bool animate)
    {
        if (index == _target)
        {
            return;
        }
        _target = index;
        var generation = ++_generation;
        if (!animate || !IsLoaded || !ArcMotion.IsFull(this))
        {
            Commit(index);
            return;
        }
        _ = SwitchAsync(index, generation);
    }

    private void Commit(int index)
    {
        _switching = false;
        _heightActive = false;
        for (var i = 0; i < _views.Length; i++)
        {
            _views[i].IsVisible = i == index;
            _views[i].Opacity = 1;
            _views[i].RenderTransform = null;
        }
        _shown = index;
        _height?.Stop();
        InvalidateMeasure();
    }

    private async Task SwitchAsync(int index, int generation)
    {
        _switching = true;
        _fade ??= new MotionValue<double>(this, 1, MotionVectors.Double);
        _height ??= CreateHeight();
        var old = _views[_shown];
        var start = Bounds.Height;
        _heightActive = true;
        _height.Jump(start);
        _fade.Jump(old.Opacity);
        Action<double> outHandler = p => old.Opacity = Math.Clamp(p, 0, 1);
        _fade.Changed += outHandler;
        await _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Exit));
        _fade.Changed -= outHandler;
        if (generation != _generation)
        {
            return;
        }
        old.IsVisible = false;
        old.Opacity = 1;
        var view = _views[index];
        _shown = index;
        view.Opacity = 0;
        view.RenderTransform = _rise;
        _rise.Y = 6;
        view.IsVisible = true;
        view.Measure(new Size(Bounds.Width, double.PositiveInfinity));
        var target = view.DesiredSize.Height;
        _ = AnimateHeight(target, generation);
        _fade.Jump(0);
        Action<double> inHandler = p =>
        {
            view.Opacity = Math.Clamp(p, 0, 1);
            _rise.Y = 6 * (1 - Math.Clamp(p, 0, 1));
        };
        _fade.Changed += inHandler;
        await _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        _fade.Changed -= inHandler;
        if (generation != _generation)
        {
            return;
        }
        view.RenderTransform = null;
        _switching = false;
    }

    private async Task AnimateHeight(double target, int generation)
    {
        await _height!.AnimateTo(target, MotionSpec.Spring(DeveloperSprings.Smooth));
        if (generation == _generation)
        {
            _heightActive = false;
            InvalidateMeasure();
        }
    }

    private MotionValue<double> CreateHeight()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = new Size(availableSize.Width, double.PositiveInfinity);
        double width = 0;
        double height = 0;
        foreach (var view in _views)
        {
            if (view.IsVisible)
            {
                view.Measure(child);
                width = Math.Max(width, view.DesiredSize.Width);
                height = Math.Max(height, view.DesiredSize.Height);
            }
        }
        if (_heightActive && _height is not null)
        {
            height = Math.Max(0, _height.Value);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double natural = 0;
        foreach (var view in _views)
        {
            if (view.IsVisible)
            {
                view.Arrange(new Rect(0, 0, finalSize.Width, view.DesiredSize.Height));
                natural = Math.Max(natural, view.DesiredSize.Height);
            }
        }
        if (_heightActive && finalSize.Height < natural - 0.5)
        {
            _clip.Rect = new Rect(-8, 0, finalSize.Width + 16, finalSize.Height);
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
        }
        return finalSize;
    }
}
