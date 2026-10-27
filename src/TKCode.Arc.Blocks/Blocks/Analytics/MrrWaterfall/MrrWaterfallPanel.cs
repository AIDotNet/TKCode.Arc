using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Which color a panel's swatch and share bars take.</summary>
internal enum MrrTone
{
    /// <summary>The start and end bars: Accent.</summary>
    Total,

    /// <summary>Growth: Success.</summary>
    Growth,

    /// <summary>Decrease: Danger.</summary>
    Decrease
}

/// <summary>What the panel lists for one bar.</summary>
internal sealed class MrrPanelContent
{
    /// <summary>The heading ("New business").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The heading's total, signed.</summary>
    public double Total { get; init; }

    /// <summary>How the total and the amounts are written.</summary>
    public AnalyticsNumberFormat Format { get; init; } = AnalyticsNumberFormat.Integer;

    /// <summary>The count line's number.</summary>
    public int Count { get; init; }

    /// <summary>The words after the count.</summary>
    public string CountNoun { get; init; } = string.Empty;

    /// <summary>Swatch and bar color.</summary>
    public MrrTone Tone { get; init; }

    /// <summary>The rows; their amounts are positive and carry <see cref="Sign"/>.</summary>
    public IReadOnlyList<MrrRowSpec> Rows { get; init; } = [];

    /// <summary>1 or -1 for the rows' amounts (totals use 1 and an unsigned format).</summary>
    public int Sign { get; init; } = 1;

    /// <summary>The amount divisor (100 / start in percent mode, 1 in currency), applied to every row.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>The hint text under the list.</summary>
    public string Hint { get; init; } = string.Empty;

    /// <summary>The panel's screen reader name.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>An 8×8 rounded swatch that eases its color (240ms) when the panel changes bar.</summary>
internal sealed class MrrSwatch : Control
{
    private readonly AnalyticsPalette _palette = new();
    private readonly LiveLoop _loop;
    private AnalyticsTween _mix = new(1);
    private MrrTone _from;
    private MrrTone _to;
    private int _version = -1;

    public MrrSwatch()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => _version = -1;
        ResourcesChanged += (_, _) => _version = -1;
        IsHitTestVisible = false;
    }

    /// <summary>The tone shown (the end of the fade).</summary>
    public MrrTone Tone => _to;

    public void SetTone(MrrTone tone, bool animate)
    {
        if (tone == _to)
        {
            return;
        }
        _from = _mix.Value >= 1 ? _to : _from;
        _to = tone;
        if (animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            _mix.Jump(0);
            _mix.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _mix.Jump(1);
        }
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = _mix.Advance(dt);
        InvalidateVisual();
        return moving;
    }

    protected override Size MeasureOverride(Size availableSize) => new(8, 8);

    private Color ToneColor(MrrTone tone) => tone switch
    {
        MrrTone.Growth => _palette.Success,
        MrrTone.Decrease => _palette.Danger,
        _ => _palette.Accent
    };

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady || _version != _palette.Version)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return;
            }
            _palette.Refresh(this);
            _version = _palette.Version;
        }
        var color = _mix.Value >= 1 ? ToneColor(_to) : AnalyticsColors.Lerp(ToneColor(_from), ToneColor(_to), _mix.Value);
        context.DrawRectangle(_palette.Brush(color), null, new RoundedRect(new Rect(0, 0, 8, 8), 2));
    }
}

/// <summary>
/// A wrapping line of text that swaps like <see cref="AnalyticsTextSwap"/> (the old text rises 5px and fades in 160ms while
/// the new one rises in from 6px below in 240ms). The panel's hint wraps to two lines, which the single-line swap cannot.
/// </summary>
internal sealed class MrrWrapSwap : Control
{
    private sealed class Layer
    {
        public required TextBlock Block;
        public AnalyticsTween Opacity = new(1);
        public AnalyticsTween Offset = new(0);
        public TranslateTransform Transform = new();
    }

    private readonly LiveLoop _loop;
    private readonly List<Layer> _leaving = new();
    private Layer? _current;
    private string? _text;

    public MrrWrapSwap()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        IsHitTestVisible = false;
    }

    public string? Text
    {
        get => _text;
        set
        {
            if (value == _text)
            {
                return;
            }
            var animate = IsLoaded && _current is not null && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
            var full = animate && ArcMotion.IsFull(this);
            _text = value;
            if (_current is { } old)
            {
                if (animate)
                {
                    old.Opacity.Start(0, full ? ArcMotionPresets.Fast : ArcMotionPresets.Instant, AnalyticsEasings.Standard);
                    old.Offset.Start(full ? -5 : 0, full ? ArcMotionPresets.Fast : ArcMotionPresets.Instant, AnalyticsEasings.Standard);
                    _leaving.Add(old);
                }
                else
                {
                    Remove(old);
                }
            }
            _current = null;
            if (!string.IsNullOrEmpty(value))
            {
                var layer = Make(value);
                if (animate)
                {
                    layer.Opacity.Jump(0);
                    layer.Offset.Jump(full ? 6 : 0);
                    layer.Opacity.Start(1, full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant, AnalyticsEasings.Enter);
                    layer.Offset.Start(0, full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant, AnalyticsEasings.Enter);
                }
                _current = layer;
                Apply(layer);
            }
            if (animate)
            {
                _loop.Start();
            }
            else
            {
                foreach (var l in _leaving.ToArray())
                {
                    Remove(l);
                }
                _leaving.Clear();
            }
            InvalidateMeasure();
        }
    }

    /// <summary>Texts still leaving (tests).</summary>
    internal int LeavingCount => _leaving.Count;

    private Layer Make(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 16.8 };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var layer = new Layer { Block = block };
        block.RenderTransform = layer.Transform;
        VisualChildren.Add(block);
        LogicalChildren.Add(block);
        return layer;
    }

    private void Remove(Layer layer)
    {
        VisualChildren.Remove(layer.Block);
        LogicalChildren.Remove(layer.Block);
    }

    private static void Apply(Layer layer)
    {
        layer.Block.Opacity = layer.Opacity.Value;
        layer.Transform.Y = layer.Offset.Value;
    }

    private bool Advance(double dt)
    {
        var moving = false;
        if (_current is { } current)
        {
            moving |= current.Opacity.Advance(dt) | current.Offset.Advance(dt);
            Apply(current);
        }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var layer = _leaving[i];
            var running = layer.Opacity.Advance(dt) | layer.Offset.Advance(dt);
            Apply(layer);
            if (!running)
            {
                _leaving.RemoveAt(i);
                Remove(layer);
            }
            moving |= running;
        }
        return moving;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        if (_current is { } current)
        {
            current.Block.Measure(availableSize);
            height = current.Block.DesiredSize.Height;
        }
        foreach (var leaving in _leaving)
        {
            leaving.Block.Measure(availableSize);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _current?.Block.Arrange(new Rect(0, 0, finalSize.Width, _current.Block.DesiredSize.Height));
        foreach (var leaving in _leaving)
        {
            leaving.Block.Arrange(new Rect(0, 0, finalSize.Width, leaving.Block.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// One account row of the panel: name and amount on the first line, detail on the second, a 2px share bar under them
/// (it doubles as the row's divider). The panel moves, fades and removes rows by key.
/// </summary>
internal sealed class MrrAccountRow : Control
{
    /// <summary>The row's height: 6 above, 19.6 + 16.8 of text, 6 + 2 of bar, 5 below.</summary>
    public const double RowHeight = 55.4;

    private readonly TextBlock _name = new() { TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 19.6, MaxLines = 1 };
    private readonly AnalyticsRollingNumber _amount = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly AnalyticsTextSwap _detail = new() { LineHeight = 16.8 };
    private readonly AnalyticsShareBar _bar = new()
    {
        Height = 2, Radius = 1, Track = AnalyticsBarTrack.BorderSubtle, Motion = AnalyticsBarMotion.Spring
    };
    private readonly TranslateTransform _transform = new();

    public MrrAccountRow(string key)
    {
        Key = key;
        foreach (var child in new Control[] { _name, _amount, _detail, _bar })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _amount.FontSize = 14;
        _detail.FontSize = 12;
        _detail.Swap = AnalyticsSwapStyle.Rise;
        _detail.TextAlignment = TextAlignment.Left;
        ArcTokens.Bind(_name, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_amount, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_amount, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightRegular);
        ArcTokens.Bind(_detail, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_detail, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        RenderTransform = _transform;
        ClipToBounds = false;
        IsHitTestVisible = false;
    }

    /// <summary>The account key.</summary>
    public string Key { get; }

    /// <summary>Row opacity state (enter and exit).</summary>
    public AnalyticsTween Fade = new(1);

    /// <summary>The translation tween of a new row (6 → 0).</summary>
    public AnalyticsTween Rise = new(0);

    /// <summary>The slide spring of a kept row (offset from its slot, target 0).</summary>
    public AnalyticsSpringChannel Slide = new(0, 0.05, 0.5);

    /// <summary>Seconds left before the share bar starts to grow.</summary>
    public double BarDelay;

    /// <summary>The share the bar grows to.</summary>
    public double PendingShare;

    /// <summary>The slot the row sits in (absolute while leaving).</summary>
    public double Slot;

    /// <summary>The row is leaving: it is drawn where it was and no longer counts.</summary>
    public bool IsLeaving;

    /// <summary>The name shown.</summary>
    public string? NameText => _name.Text;

    /// <summary>The amount (tests).</summary>
    public AnalyticsRollingNumber Amount => _amount;

    /// <summary>The detail swap (tests).</summary>
    public AnalyticsTextSwap Detail => _detail;

    /// <summary>The share bar (tests).</summary>
    public AnalyticsShareBar Bar => _bar;

    public void Update(MrrRowSpec spec, double amount, AnalyticsNumberFormat format)
    {
        _name.Text = spec.Name;
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, spec.IsRest ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        _amount.Format = format;
        _amount.Amount = amount;
        _detail.Text = spec.Detail ?? string.Empty;
        AutomationProperties.SetName(this, $"{spec.Name}, {_amount.FullText}" + (string.IsNullOrEmpty(spec.Detail) ? string.Empty : $", {spec.Detail}"));
    }

    public void SetTone(MrrTone tone) =>
        _bar.FillColor = tone switch { MrrTone.Growth => AnalyticsBarColor.Success, MrrTone.Decrease => AnalyticsBarColor.Danger, _ => AnalyticsBarColor.Accent };

    public void Apply()
    {
        Opacity = Math.Clamp(Fade.Value, 0, 1);
        _transform.Y = Slide.Value + Rise.Value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 243 : availableSize.Width;
        _amount.Measure(new Size(width, double.PositiveInfinity));
        _name.Measure(new Size(Math.Max(0, width - _amount.DesiredSize.Width - 12), 19.6));
        _detail.Measure(new Size(width, 16.8));
        _bar.Measure(new Size(width, 2));
        return new Size(width, RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var amountWidth = _amount.DesiredSize.Width;
        _amount.Arrange(new Rect(finalSize.Width - amountWidth, 6 + (19.6 - _amount.DesiredSize.Height) / 2, amountWidth, _amount.DesiredSize.Height));
        _name.Arrange(new Rect(0, 6, Math.Max(0, finalSize.Width - amountWidth - 12), 19.6));
        _detail.Arrange(new Rect(0, 6 + 19.6, Math.Min(finalSize.Width, Math.Max(_detail.DesiredSize.Width, 0)), 16.8));
        _bar.Arrange(new Rect(0, 6 + 19.6 + 16.8 + 6, finalSize.Width, 2));
        return finalSize;
    }
}

/// <summary>
/// The side panel: heading with a color swatch and the step's total, the count line, the account rows and a hint.
/// Rows are keyed by account: a kept row slides to its new slot on the Smooth spring, a new row fades and rises in (240ms,
/// 35ms apart), a removed row fades out in 120ms where it stood. Everything lands at once under reduced motion.
/// </summary>
/// <remarks>
/// Code-driven motion: the list is a keyed presence-and-layout animation (FLIP) and a ControlTheme cannot express rows
/// that stay in place while they leave, or slots that spring from where a row was; one loop advances the springs and fades.
/// </remarks>
internal sealed class MrrWaterfallPanel : Control
{
    private const double HeaderHeight = 19.6;
    private const double CountTop = HeaderHeight + 2;
    private const double ListTop = CountTop + 16.8 + 8;

    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<MrrWaterfallPanel, bool>(nameof(IsStacked));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<MrrWaterfallPanel, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<MrrWaterfallPanel, double>(nameof(Inset), 20);

    public static readonly StyledProperty<double> StackedInsetProperty =
        AvaloniaProperty.Register<MrrWaterfallPanel, double>(nameof(StackedInset), 16);

    private readonly MrrSwatch _swatch = new();
    private readonly AnalyticsTextSwap _title = new() { LineHeight = HeaderHeight };
    private readonly AnalyticsRollingNumber _total = new();
    private readonly AnalyticsRollingNumber _count = new();
    private readonly AnalyticsTextSwap _noun = new() { LineHeight = 16.8 };
    private readonly MrrWrapSwap _hint = new();
    private readonly List<MrrAccountRow> _rows = new();
    private readonly List<MrrAccountRow> _leaving = new();
    private readonly LiveLoop _loop;
    private MrrPanelContent _content = new();
    private bool _hasContent;

    static MrrWaterfallPanel()
    {
        AffectsMeasure<MrrWaterfallPanel>(IsStackedProperty, InsetProperty, StackedInsetProperty);
        AffectsRender<MrrWaterfallPanel>(LineBrushProperty, IsStackedProperty);
    }

    public MrrWaterfallPanel()
    {
        foreach (var child in new Control[] { _swatch, _title, _total, _count, _noun, _hint })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        _title.FontSize = 14;
        _title.Swap = AnalyticsSwapStyle.Rise;
        _noun.FontSize = 12;
        _noun.Swap = AnalyticsSwapStyle.Rise;
        _total.FontSize = 14;
        _count.FontSize = 12;
        ArcTokens.Bind(_title, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_title, AnalyticsTextSwap.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_title, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_total, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_total, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_count, RollingDigits.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_noun, AnalyticsTextSwap.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_noun, AnalyticsTextSwap.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        IsHitTestVisible = false;
    }

    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    public double StackedInset { get => GetValue(StackedInsetProperty); set => SetValue(StackedInsetProperty, value); }

    /// <summary>The rows shown (tests).</summary>
    internal IReadOnlyList<MrrAccountRow> Rows => _rows;

    /// <summary>Rows fading out (tests).</summary>
    internal IReadOnlyList<MrrAccountRow> LeavingRows => _leaving;

    /// <summary>The total in the heading (tests).</summary>
    internal AnalyticsRollingNumber Total => _total;

    /// <summary>The count (tests).</summary>
    internal AnalyticsRollingNumber Count => _count;

    /// <summary>The heading text (tests).</summary>
    internal AnalyticsTextSwap Title => _title;

    /// <summary>The swatch (tests).</summary>
    internal MrrSwatch Swatch => _swatch;

    /// <summary>The hint (tests).</summary>
    internal MrrWrapSwap Hint => _hint;

    /// <summary>True while a slide or fade runs.</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>The slot of row <paramref name="index"/> from the panel's content top.</summary>
    internal static double SlotOf(int index) => ListTop + index * MrrAccountRow.RowHeight;

    /// <summary>Lists a bar's accounts. <paramref name="animate"/> false (or reduced motion) replaces everything at once.</summary>
    public void SetContent(MrrPanelContent content, bool animate)
    {
        var full = animate && _hasContent && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        _content = content;
        _hasContent = true;
        AutomationProperties.SetName(this, content.Name);
        _swatch.SetTone(content.Tone, animate);
        _title.Text = content.Title;
        _total.Format = content.Format;
        _total.Amount = content.Total;
        _count.Amount = content.Count;
        _noun.Text = content.CountNoun;
        _hint.Text = content.Hint;
        SyncRows(content, full);
        InvalidateMeasure();
        if (full)
        {
            _loop.Start();
        }
    }

    private void SyncRows(MrrPanelContent content, bool full)
    {
        var old = _rows.ToArray();
        _rows.Clear();
        var staggered = 0;
        for (var i = 0; i < content.Rows.Count; i++)
        {
            var spec = content.Rows[i];
            var amount = spec.Amount * content.Scale * content.Sign;
            var row = Take(old, spec.Key) ?? TakeLeaving(spec.Key);
            var isNew = row is null;
            if (row is null)
            {
                row = new MrrAccountRow(spec.Key);
                VisualChildren.Add(row);
                LogicalChildren.Add(row);
            }
            else
            {
                // Keep the visual place: the slide offset is where the row is now minus where it will sit.
                var visual = row.Slot + row.Slide.Value;
                row.Slot = SlotOf(i);
                row.Slide.Jump(visual - row.Slot);
                row.IsLeaving = false;
            }
            row.Slot = SlotOf(i);
            row.Update(spec, amount, content.Format);
            row.SetTone(content.Tone);
            _rows.Add(row);
            if (!full)
            {
                row.Fade.Jump(1);
                row.Rise.Jump(0);
                row.Slide.Jump(0);
                row.Bar.Value = spec.Share;
                row.PendingShare = spec.Share;
                row.BarDelay = 0;
            }
            else if (isNew)
            {
                var delay = TimeSpan.FromMilliseconds(35 * staggered);
                row.Fade.Jump(0);
                row.Fade.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter, delay);
                row.Rise.Jump(6);
                row.Rise.Start(0, ArcMotionPresets.Standard, AnalyticsEasings.Standard, delay);
                row.PendingShare = spec.Share;
                row.BarDelay = delay.TotalSeconds;
                row.Bar.Value = 0;
                staggered++;
            }
            else
            {
                row.Fade.Start(1, ArcMotionPresets.Fast, AnalyticsEasings.Enter);
                row.Slide.SpringTo(0, AnalyticsSprings.Smooth);
                row.PendingShare = spec.Share;
                row.BarDelay = 0;
                row.Bar.Value = spec.Share;
            }
            row.Apply();
        }
        // Rows that are not in the new list leave (or vanish).
        foreach (var gone in old)
        {
            if (gone is null)
            {
                continue;
            }
            if (full)
            {
                gone.IsLeaving = true;
                gone.Fade.Start(0, TimeSpan.FromMilliseconds(120), AnalyticsEasings.EaseOut);
                _leaving.Add(gone);
            }
            else
            {
                VisualChildren.Remove(gone);
                LogicalChildren.Remove(gone);
            }
        }
        if (!full)
        {
            foreach (var leaving in _leaving)
            {
                VisualChildren.Remove(leaving);
                LogicalChildren.Remove(leaving);
            }
            _leaving.Clear();
        }
    }

    private static MrrAccountRow? Take(MrrAccountRow[] rows, string key)
    {
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i] is { } row && row.Key == key)
            {
                rows[i] = null!;
                return row;
            }
        }
        return null;
    }

    private MrrAccountRow? TakeLeaving(string key)
    {
        for (var i = 0; i < _leaving.Count; i++)
        {
            if (_leaving[i].Key == key)
            {
                var row = _leaving[i];
                _leaving.RemoveAt(i);
                return row;
            }
        }
        return null;
    }

    private bool Advance(double dt)
    {
        var moving = false;
        foreach (var row in _rows)
        {
            moving |= row.Fade.Advance(dt) | row.Rise.Advance(dt) | row.Slide.Step(dt);
            if (row.BarDelay > 0)
            {
                row.BarDelay -= dt;
                moving = true;
                if (row.BarDelay <= 0)
                {
                    row.Bar.Value = row.PendingShare;
                }
            }
            row.Apply();
        }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var row = _leaving[i];
            var running = row.Fade.Advance(dt);
            row.Apply();
            if (!running)
            {
                _leaving.RemoveAt(i);
                VisualChildren.Remove(row);
                LogicalChildren.Remove(row);
            }
            moving |= running;
        }
        return moving;
    }

    /// <summary>Advances the list's motion by hand (tests).</summary>
    internal bool Step(double dt) => Advance(dt);

    private double ContentLeft => IsStacked ? 0 : Inset + 1;

    private double ContentTop => IsStacked ? StackedInset + 1 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 264 : availableSize.Width;
        var inner = Math.Max(0, width - ContentLeft);
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _swatch.Measure(inf);
        _total.Measure(inf);
        _title.Measure(new Size(Math.Max(0, inner - _total.DesiredSize.Width - 12 - 16), double.PositiveInfinity));
        _count.Measure(inf);
        _noun.Measure(inf);
        foreach (var row in _rows)
        {
            row.Measure(new Size(inner, MrrAccountRow.RowHeight));
        }
        foreach (var row in _leaving)
        {
            row.Measure(new Size(inner, MrrAccountRow.RowHeight));
        }
        _hint.Measure(new Size(inner, double.PositiveInfinity));
        var height = ContentTop + ListTop + _rows.Count * MrrAccountRow.RowHeight + 8 + _hint.DesiredSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var left = ContentLeft;
        var top = ContentTop;
        var inner = Math.Max(0, finalSize.Width - left);
        var totalWidth = _total.DesiredSize.Width;
        _total.Arrange(new Rect(left + inner - totalWidth, top, totalWidth, HeaderHeight));
        _swatch.Arrange(new Rect(left, top + (HeaderHeight - 8) / 2, 8, 8));
        _title.Arrange(new Rect(left + 16, top, Math.Min(_title.DesiredSize.Width, Math.Max(0, inner - totalWidth - 12 - 16)), HeaderHeight));
        var countWidth = _count.DesiredSize.Width;
        _count.Arrange(new Rect(left, top + CountTop, countWidth, 16.8));
        _noun.Arrange(new Rect(left + countWidth + 4, top + CountTop, _noun.DesiredSize.Width, 16.8));
        foreach (var row in _rows)
        {
            row.Arrange(new Rect(left, top + row.Slot, inner, MrrAccountRow.RowHeight));
        }
        foreach (var row in _leaving)
        {
            row.Arrange(new Rect(left, top + row.Slot, inner, MrrAccountRow.RowHeight));
        }
        _hint.Arrange(new Rect(left, top + ListTop + _rows.Count * MrrAccountRow.RowHeight + 8, inner, _hint.DesiredSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (LineBrush is not { } brush)
        {
            return;
        }
        context.FillRectangle(brush, IsStacked ? new Rect(0, 0, Bounds.Width, 1) : new Rect(0, 0, 1, Bounds.Height));
    }
}
