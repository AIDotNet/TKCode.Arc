using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a legend item draws its swatch.</summary>
internal enum ChartSwatchKind
{
    /// <summary>A 14×10 slot holding a 2.5px line (dashed for dashed series); hidden shrinks it to 35% width.</summary>
    Line,

    /// <summary>A 10×10 rounded square (Streamgraph layers); hidden scales it to 60%.</summary>
    Square
}

/// <summary>
/// Series toggles (template part <c>PART_Legend</c>): a wrap of <see cref="ChartLegendItem"/> with 4px between
/// items and 2px between rows. A hidden series keeps its place; its swatch empties and its name steps back.
/// </summary>
internal sealed class ChartLegend : Panel
{
    internal ChartBase? Owner { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        double x = 0, y = 0, rowHeight = 0, width = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > availableSize.Width)
            {
                y += rowHeight + 2;
                x = 0;
                rowHeight = 0;
            }
            x += size.Width + 4;
            width = Math.Max(width, x - 4);
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return Children.Count == 0 ? default : new Size(width, y + rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                y += rowHeight + 2;
                x = 0;
                rowHeight = 0;
            }
            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width + 4;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return finalSize;
    }

    /// <summary>Syncs the items with the given series, reusing items by key.</summary>
    internal void Sync(IReadOnlyList<(string Key, string Name, Color? Color, int Order, bool Dashed)> series, ChartSwatchKind kind)
    {
        for (var i = Children.Count - 1; i >= series.Count; i--)
        {
            Children.RemoveAt(i);
        }
        for (var i = 0; i < series.Count; i++)
        {
            ChartLegendItem item;
            if (i < Children.Count)
            {
                item = (ChartLegendItem)Children[i];
            }
            else
            {
                item = new ChartLegendItem { Legend = this };
                Children.Add(item);
            }
            var s = series[i];
            item.Configure(s.Key, s.Name, s.Color, s.Order, s.Dashed, kind);
        }
    }

    internal void Refresh()
    {
        foreach (var child in Children)
        {
            if (child is ChartLegendItem item)
            {
                item.RefreshState();
            }
        }
    }
}

/// <summary>
/// One self-drawn series toggle: swatch plus name in a pill. Click, Enter or Space shows or hides the series;
/// hovering (mouse) or keyboard focus previews it. Exposed to assistive technology as a toggle button.
/// </summary>
internal sealed class ChartLegendItem : Control, Avalonia.Rendering.ICustomHitTest
{
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private const double SwatchWidth = 14;
    private const double ItemHeight = 32;
    private MotionValue<double>? _hover;
    private MotionValue<double>? _press;
    private MotionValue<double>? _off;
    private MotionValue<double>? _offFade;
    private MotionValue<double>? _emphasis;
    private bool _pressed;
    private bool _hidden;

    public ChartLegendItem()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal ChartLegend? Legend { get; set; }

    internal string SeriesKey { get; private set; } = string.Empty;

    internal string SeriesName { get; private set; } = string.Empty;

    internal bool IsSeriesHidden => _hidden;

    private ChartBase? Owner => Legend?.Owner;

    private Color? _color;
    private int _order;
    private bool _dashed;
    private ChartSwatchKind _kind;

    internal void Configure(string key, string name, Color? color, int order, bool dashed, ChartSwatchKind kind)
    {
        var changed = key != SeriesKey || name != SeriesName;
        SeriesKey = key;
        SeriesName = name;
        _color = color;
        _order = order;
        _dashed = dashed;
        _kind = kind;
        if (changed)
        {
            AutomationProperties.SetName(this, name);
            InvalidateMeasure();
        }
        RefreshState(jump: changed);
    }

    internal void RefreshState(bool jump = false)
    {
        var owner = Owner;
        var hidden = owner?.IsLegendSeriesHidden(SeriesKey) ?? false;
        var emphasis = owner?.LegendEmphasis(SeriesKey) ?? 0;
        if (hidden != _hidden || jump)
        {
            var wasHidden = _hidden;
            _hidden = hidden;
            _off ??= CreateValue(hidden ? 1 : 0);
            _offFade ??= CreateValue(hidden ? 1 : 0);
            if (jump || !IsLoaded)
            {
                _off.Jump(hidden ? 1 : 0);
                _offFade.Jump(hidden ? 1 : 0);
            }
            else if (wasHidden != hidden)
            {
                _ = _off.AnimateTo(hidden ? 1 : 0, ArcMotion.IsFull(this)
                    ? MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring)
                    : MotionSpec.Instant);
                _ = _offFade.AnimateTo(hidden ? 1 : 0, Fade(ArcMotionPresets.Fast));
            }
        }
        _emphasis ??= CreateValue(emphasis);
        if (Math.Abs(_emphasis.Target - emphasis) > 1e-6)
        {
            _ = _emphasis.AnimateTo(emphasis, Fade(ArcMotionPresets.Fast));
        }
        InvalidateVisual();
    }

    private MotionSpec Fade(TimeSpan duration) => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off
        ? MotionSpec.Instant
        : MotionSpec.Tween(duration, ArcEasings.Standard);

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private FormattedText? Text(Color color)
    {
        var owner = Owner;
        if (owner is null)
        {
            return null;
        }
        var palette = owner.Palette;
        return owner.Texts.Get(palette, SeriesName, palette.SizeSM, color);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        if (owner is null)
        {
            return new Size(0, ItemHeight);
        }
        owner.EnsurePalette();
        var text = Text(owner.Palette.TextSecondary);
        var swatch = _kind == ChartSwatchKind.Square ? 10 : SwatchWidth;
        return new Size(8 + swatch + 8 + (text?.WidthIncludingTrailingWhitespace ?? 0) + 10, ItemHeight);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _hover ??= CreateValue(0);
        _ = _hover.AnimateTo(1, Fade(ArcMotionPresets.Fast));
        Owner?.OnLegendPreview(SeriesKey, true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover ??= CreateValue(0);
        _ = _hover.AnimateTo(0, Fade(ArcMotionPresets.Fast));
        SetPressed(false);
        Owner?.OnLegendPreview(SeriesKey, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        e.Handled = true;
        SetPressed(true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        SetPressed(false);
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            e.Handled = true;
            Toggle();
        }
    }

    private void SetPressed(bool pressed)
    {
        if (_pressed == pressed)
        {
            return;
        }
        _pressed = pressed;
        _press ??= CreateValue(1);
        _ = _press.AnimateTo(pressed ? 0.97 : 1, ArcMotion.IsFull(this)
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Instant);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Toggle();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (Classes.Contains(":focus-visible") || e.NavigationMethod == NavigationMethod.Tab)
        {
            Owner?.OnLegendPreview(SeriesKey, true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnLegendPreview(SeriesKey, false);
        InvalidateVisual();
    }

    internal void Toggle() => Owner?.ToggleLegendSeries(SeriesKey);

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        if (owner is null)
        {
            return;
        }
        owner.EnsurePalette();
        var palette = owner.Palette;
        var size = Bounds.Size;
        var hover = _hover?.Value ?? 0;
        var off = _off?.Value ?? (_hidden ? 1 : 0);
        var offFade = _offFade?.Value ?? (_hidden ? 1 : 0);
        var emphasis = _emphasis?.Value ?? 0;
        var scale = _press?.Value ?? 1;
        var dim = emphasis < 0 ? -emphasis : 0;
        var active = emphasis > 0 ? emphasis : 0;
        var center = new Point(size.Width / 2, size.Height / 2);
        using var transform = context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale)
                                                    * Matrix.CreateTranslation(center.X, center.Y));
        using var dimming = context.PushOpacity(1 - 0.44 * dim);
        var pill = new RoundedRect(new Rect(size), size.Height / 2);
        var wash = owner.LegendHoverWash ? Math.Max(hover, active) : active;
        if (wash > 0.001)
        {
            context.DrawRectangle(palette.Brush(Oklch.WithAlpha(palette.Foreground, 0.05 * wash)), null, pill);
        }
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            context.DrawRectangle(null, palette.Pen(palette.FocusRing, 2), pill.Inflate(2, 2));
        }

        var seriesColor = _color ?? owner.LegendSeriesColor(_order);
        var swatchAlpha = 1 - (1 - (_kind == ChartSwatchKind.Square ? 0.35 : 0.45)) * offFade;
        double x = 8;
        if (_kind == ChartSwatchKind.Square)
        {
            var s = 10 * (1 - 0.4 * off);
            var rect = new Rect(x + (10 - s) / 2, (size.Height - s) / 2, s, s);
            context.DrawRectangle(palette.Brush(Oklch.WithAlpha(seriesColor, swatchAlpha)), null, new RoundedRect(rect, 3 * s / 10));
            x += 10 + 8;
        }
        else
        {
            var width = SwatchWidth * (1 - 0.65 * off);
            var y = size.Height / 2;
            var brush = palette.Brush(Oklch.WithAlpha(seriesColor, swatchAlpha));
            if (_dashed)
            {
                using (context.PushClip(new Rect(x, y - 2, width, 4)))
                {
                    for (double dx = 0; dx < SwatchWidth; dx += 7)
                    {
                        context.DrawRectangle(brush, null, new Rect(x + dx, y - 1.25, Math.Min(4, SwatchWidth - dx), 2.5));
                    }
                }
            }
            else
            {
                context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, y - 1.25, width, 2.5), 1.25));
            }
            x += SwatchWidth + 8;
        }

        // Label: secondary at rest, foreground on hover or emphasis, muted (and struck through) when hidden.
        var lift = Math.Max(owner.LegendHoverText ? hover : 0, active);
        var restColor = ChartPalette.Lerp(palette.TextSecondary, palette.TextMuted, ChartPalette.Step(offFade));
        var labelColor = ChartPalette.Lerp(restColor, palette.Foreground, ChartPalette.Step(lift * (1 - offFade)));
        var text = Text(labelColor);
        if (text is null)
        {
            return;
        }
        var origin = new Point(x, (size.Height - text.Height) / 2);
        using (context.PushOpacity(1 - 0.28 * offFade))
        {
            context.DrawText(text, origin);
            if (offFade > 0.01)
            {
                var strikeY = Math.Round(origin.Y + text.Height * 0.55) + 0.5;
                context.DrawLine(palette.Pen(Oklch.WithAlpha(labelColor, 0.55 * offFade), 1),
                    new Point(x, strikeY), new Point(x + text.WidthIncludingTrailingWhitespace, strikeY));
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ChartLegendItemAutomationPeer(this);
}

/// <summary>Toggle-button peer: "on" means the series is shown.</summary>
internal sealed class ChartLegendItemAutomationPeer : ControlAutomationPeer, IToggleProvider
{
    public ChartLegendItemAutomationPeer(ChartLegendItem owner) : base(owner)
    {
    }

    private ChartLegendItem Item => (ChartLegendItem)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore() => Item.SeriesName;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    ToggleState IToggleProvider.ToggleState => Item.IsSeriesHidden ? ToggleState.Off : ToggleState.On;

    void IToggleProvider.Toggle() => Item.Toggle();
}
