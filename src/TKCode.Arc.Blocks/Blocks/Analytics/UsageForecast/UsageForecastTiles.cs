using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One tile of the row under the headline (a <c>dt</c> / <c>dd</c> pair): label, the value rolling digit by digit with its
/// unit set small beside it on the same baseline, and a small caption. The value turns red (240ms) when it is an overage.
/// Stacked (block under 440): label and caption on the left, the value right-aligned beside them.
/// </summary>
internal sealed class UsageKpiItem : Control
{
    private readonly TextBlock _label = new() { TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 19.6 };
    private readonly AnalyticsRollingNumber _number = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _unit = new();
    private readonly TextBlock _small = new() { TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 16.8 };
    private readonly AnalyticsPalette _palette = new();
    private readonly LiveLoop _loop;
    private AnalyticsTween _tone = new(0);
    private double _valueSize = 22;
    private double _unitSize = 16;
    private bool _stacked;
    private double _unitY;

    public UsageKpiItem()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        foreach (var child in new Control[] { _label, _number, _unit, _small })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        ArcTokens.Bind(_label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_number, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_unit, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_unit, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_small, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(_small, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_number, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ApplySizes();
        ActualThemeVariantChanged += (_, _) => ApplyTone();
    }

    /// <summary>The label.</summary>
    public string? Label { get => _label.Text; set => _label.Text = value; }

    /// <summary>The caption under the value.</summary>
    public string? Small { get => _small.Text; set => _small.Text = value; }

    /// <summary>The unit written after the value.</summary>
    public string? Unit { get => _unit.Text; set { _unit.Text = value; InvalidateMeasure(); } }

    /// <summary>The rolling number (tests).</summary>
    internal AnalyticsRollingNumber Number => _number;

    /// <summary>1 while the value is shown as an overage (tests).</summary>
    internal double Tone => _tone.Value;

    /// <summary>Label and caption left, value right (the block is under 440).</summary>
    public bool IsStacked
    {
        get => _stacked;
        set
        {
            if (_stacked != value)
            {
                _stacked = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The value's font size (22; 18 in a compact block).</summary>
    public double ValueSize
    {
        get => _valueSize;
        set
        {
            if (Math.Abs(_valueSize - value) > 0.01)
            {
                _valueSize = value;
                ApplySizes();
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The unit's font size (16).</summary>
    public double UnitSize
    {
        get => _unitSize;
        set
        {
            _unitSize = value;
            ApplySizes();
            InvalidateMeasure();
        }
    }

    /// <summary>Shows a value: <paramref name="format"/> writes it; the unit sits beside it.</summary>
    public void Show(double value, AnalyticsNumberFormat format, string? unit)
    {
        _number.Format = format;
        _number.Amount = value;
        _unit.Text = unit;
        _unit.IsVisible = !string.IsNullOrEmpty(unit);
        AutomationProperties.SetName(this, SpokenText);
        InvalidateMeasure();
    }

    /// <summary>What a screen reader hears.</summary>
    public string SpokenText => $"{_label.Text}, {_number.FullText}{_unit.Text}. {_small.Text}";

    /// <summary>Colours the value as an overage (red) or normally; the colour changes in 240ms.</summary>
    public void SetOver(bool over, bool animate)
    {
        var target = over ? 1 : 0;
        if (animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off && this.IsAttachedToVisualTree())
        {
            _tone.Start(target, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _tone.Jump(target);
            ApplyTone();
        }
    }

    private bool Advance(double dt)
    {
        var moving = _tone.Advance(dt);
        ApplyTone();
        return moving;
    }

    private void ApplyTone()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        _palette.Refresh(this);
        var color = AnalyticsColors.Lerp(_palette.Foreground, _palette.Danger, _tone.Value);
        _number.Foreground = new SolidColorBrush(color);
    }

    private void ApplySizes()
    {
        _number.FontSize = _valueSize;
        _unit.FontSize = _unitSize;
        _unit.LineHeight = Math.Round(_unitSize * 1.3, 1);
    }

    private bool _rule;
    private bool _ruleAcross;
    private double _ruleOffset;

    /// <summary>Draws the 1px line before the tile (a vertical one at its left, or a horizontal one above it), <paramref name="offset"/> px outside it.</summary>
    internal void SetRule(bool on, bool across, double offset)
    {
        if (_rule != on || _ruleAcross != across || Math.Abs(_ruleOffset - offset) > 0.01)
        {
            _rule = on;
            _ruleAcross = across;
            _ruleOffset = offset;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (!_rule || !this.IsAttachedToVisualTree())
        {
            return;
        }
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var pen = _palette.Pen(_palette.Border, 1);
        if (_ruleAcross)
        {
            context.DrawLine(pen, new Point(0, -_ruleOffset + 0.5), new Point(Bounds.Width, -_ruleOffset + 0.5));
        }
        else
        {
            context.DrawLine(pen, new Point(-_ruleOffset + 0.5, 0), new Point(-_ruleOffset + 0.5, Bounds.Height));
        }
    }

    private double ValueLine => Math.Round(_valueSize * 1.3, 1);

    private static double Baseline(double top, double height, double size) => top + (height - 1.21 * size) / 2 + 0.969 * size;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _number.Measure(inf);
        _unit.Measure(inf);
        var valueWidth = _number.DesiredSize.Width + (_unit.IsVisible ? _unit.DesiredSize.Width : 0);
        var rowHeight = Math.Max(_number.DesiredSize.Height, ValueLine);
        _unitY = Baseline(0, rowHeight, _valueSize) - Baseline(0, _unit.DesiredSize.Height, _unitSize);
        if (_stacked)
        {
            var left = Math.Max(0, width - 12 - valueWidth);
            _label.Measure(new Size(left, double.PositiveInfinity));
            _small.Measure(new Size(left, double.PositiveInfinity));
            return new Size(width, Math.Max(19.6 + 2 + _small.DesiredSize.Height, rowHeight));
        }
        _label.Measure(new Size(width, double.PositiveInfinity));
        _small.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, 19.6 + 2 + rowHeight + 2 + _small.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var valueWidth = _number.DesiredSize.Width + (_unit.IsVisible ? _unit.DesiredSize.Width : 0);
        var rowHeight = Math.Max(_number.DesiredSize.Height, ValueLine);
        if (_stacked)
        {
            var x = Math.Max(0, finalSize.Width - valueWidth);
            var y = (finalSize.Height - rowHeight) / 2;
            _number.Arrange(new Rect(x, y, _number.DesiredSize.Width, rowHeight));
            _unit.Arrange(new Rect(x + _number.DesiredSize.Width, y + _unitY, _unit.DesiredSize.Width, _unit.DesiredSize.Height));
            var left = Math.Max(0, x - 12);
            _label.Arrange(new Rect(0, 0, left, 19.6));
            _small.Arrange(new Rect(0, 21.6, left, _small.DesiredSize.Height));
            return finalSize;
        }
        _label.Arrange(new Rect(0, 0, finalSize.Width, 19.6));
        var top = 19.6 + 2;
        _number.Arrange(new Rect(0, top, _number.DesiredSize.Width, rowHeight));
        _unit.Arrange(new Rect(_number.DesiredSize.Width, top + _unitY, _unit.DesiredSize.Width, _unit.DesiredSize.Height));
        _small.Arrange(new Rect(0, top + rowHeight + 2, finalSize.Width, _small.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The tiles in a row: three equal columns split by 1px lines (paddings 20 inside, 10 when compact), or one column of
/// stacked tiles split by horizontal lines. Hidden tiles take no column.
/// </summary>
internal sealed class UsageKpiRow : Panel
{
    public static readonly StyledProperty<double> InlinePaddingProperty =
        AvaloniaProperty.Register<UsageKpiRow, double>(nameof(InlinePadding), 20);
    public static readonly StyledProperty<double> StackedPaddingProperty =
        AvaloniaProperty.Register<UsageKpiRow, double>(nameof(StackedPadding), 10);
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<UsageKpiRow, bool>(nameof(IsStacked));

    static UsageKpiRow()
    {
        AffectsMeasure<UsageKpiRow>(InlinePaddingProperty, StackedPaddingProperty, IsStackedProperty);
        AffectsRender<UsageKpiRow>(IsStackedProperty);
    }

    public double InlinePadding { get => GetValue(InlinePaddingProperty); set => SetValue(InlinePaddingProperty, value); }
    public double StackedPadding { get => GetValue(StackedPaddingProperty); set => SetValue(StackedPaddingProperty, value); }
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private List<UsageKpiItem> Items() => Children.OfType<UsageKpiItem>().Where(c => c.IsVisible).ToList();

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items();
        if (items.Count == 0)
        {
            return default;
        }
        var width = availableSize.Width;
        if (IsStacked)
        {
            var total = 0.0;
            for (var i = 0; i < items.Count; i++)
            {
                items[i].IsStacked = true;
                items[i].Measure(new Size(width, double.PositiveInfinity));
                total += items[i].DesiredSize.Height + (i > 0 ? StackedPadding : 0) + (i < items.Count - 1 ? StackedPadding : 0) + (i > 0 ? 1 : 0);
            }
            return new Size(width, total);
        }
        var column = width / items.Count;
        var height = 0.0;
        for (var i = 0; i < items.Count; i++)
        {
            items[i].IsStacked = false;
            items[i].Measure(new Size(Math.Max(0, ItemWidth(column, i, items.Count)), double.PositiveInfinity));
            height = Math.Max(height, items[i].DesiredSize.Height);
        }
        return new Size(width, height);
    }

    private double ItemWidth(double column, int index, int count)
    {
        var pad = InlinePadding;
        return index == 0 ? column - pad : index == count - 1 ? column - 2 * pad - 1 : column - 2 * pad - 1;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var items = Items();
        if (items.Count == 0)
        {
            return finalSize;
        }
        if (IsStacked)
        {
            var y = 0.0;
            for (var i = 0; i < items.Count; i++)
            {
                y += i > 0 ? StackedPadding + 1 : 0;
                items[i].SetRule(i > 0, true, StackedPadding + 1);
                items[i].Arrange(new Rect(0, y, finalSize.Width, items[i].DesiredSize.Height));
                y += items[i].DesiredSize.Height + (i < items.Count - 1 ? StackedPadding : 0);
            }
            return finalSize;
        }
        var column = finalSize.Width / items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            var x = i * column + (i == 0 ? 0 : InlinePadding + 1);
            items[i].SetRule(i > 0, false, InlinePadding + 1);
            items[i].Arrange(new Rect(x, 0, Math.Max(0, ItemWidth(column, i, items.Count)), finalSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// An alert chip (a toggle button, pressed state = on): a pill with the percent, the status text and, while on, a check that
/// opens its slot (20px, the quick spring). Colours change in 160ms; the status text morphs: the old text fades in 180ms,
/// the new letters arrive in 280ms, each 32ms after the one before (from 50ms), while the chip's width follows on a spring.
/// Stretched (block under 440), the percent sits at the left edge and the status at the right.
/// </summary>
internal sealed class UsageAlertChip : ToggleButton
{
    public static readonly StyledProperty<string> PercentTextProperty =
        AvaloniaProperty.Register<UsageAlertChip, string>(nameof(PercentText), string.Empty);
    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<UsageAlertChip, string>(nameof(StatusText), string.Empty);
    public static readonly StyledProperty<bool> IsStretchedProperty =
        AvaloniaProperty.Register<UsageAlertChip, bool>(nameof(IsStretched));
    public static readonly StyledProperty<double> ChipHeightProperty =
        AvaloniaProperty.Register<UsageAlertChip, double>(nameof(ChipHeight), 32);

    private const double SlotWidth = 20;
    private const double CharSeconds = 0.28;
    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(128);
    private readonly LiveLoop _loop;
    private AnalyticsTween _on = new(0);
    private AnalyticsTween _hover = new(0);
    private AnalyticsTween _oldAlpha = new(0);
    private AnalyticsSpringChannel _slot = new(0, 0.05, 0.5);
    private AnalyticsSpringChannel _check = new(0, 0.002, 0.02);
    private AnalyticsSpringChannel _statusWidth = new(0, 0.05, 0.5);
    private string _shownStatus = string.Empty;
    private string _oldStatus = string.Empty;
    private double _morph = 10;
    private bool _primed;

    static UsageAlertChip()
    {
        FocusableProperty.OverrideDefaultValue<UsageAlertChip>(true);
        AffectsMeasure<UsageAlertChip>(IsStretchedProperty, ChipHeightProperty, PercentTextProperty);
        AffectsRender<UsageAlertChip>(PercentTextProperty);
    }

    public UsageAlertChip()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand);
        HorizontalAlignment = HorizontalAlignment.Left;
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette.Refresh(this);
            _text.Clear();
            InvalidateVisual();
        };
    }

    protected override Type StyleKeyOverride => typeof(UsageAlertChip);

    public string PercentText { get => GetValue(PercentTextProperty); set => SetValue(PercentTextProperty, value); }
    public string StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public bool IsStretched { get => GetValue(IsStretchedProperty); set => SetValue(IsStretchedProperty, value); }
    public double ChipHeight { get => GetValue(ChipHeightProperty); set => SetValue(ChipHeightProperty, value); }

    /// <summary>The width of the check's slot now (tests).</summary>
    internal double SlotNow => _slot.Value;

    /// <summary>The status text the letters fade in for (tests).</summary>
    internal string ShownStatus => _shownStatus;

    private bool Animates => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full && this.IsAttachedToVisualTree();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            var on = IsChecked == true;
            if (!_primed || !Animates)
            {
                _on.Jump(on ? 1 : 0);
                _slot.Jump(on ? SlotWidth : 0);
                _check.Jump(on ? 1 : 0);
            }
            else
            {
                _on.Start(on ? 1 : 0, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
                _slot.SpringTo(on ? SlotWidth : 0, AnalyticsSprings.Quick);
                _check.SpringTo(on ? 1 : 0, AnalyticsSprings.Quick);
                _loop.Start();
            }
            InvalidateMeasure();
        }
        else if (change.Property == StatusTextProperty)
        {
            var text = StatusText ?? string.Empty;
            var width = MeasureStatus(text);
            if (!_primed || !Animates || text == _shownStatus)
            {
                _shownStatus = text;
                _oldStatus = string.Empty;
                _oldAlpha.Jump(0);
                _morph = 10;
                _statusWidth.Jump(width);
            }
            else
            {
                _oldStatus = _shownStatus;
                _shownStatus = text;
                _oldAlpha.Jump(1);
                _oldAlpha.Start(0, ArcMotionPresets.Exit, AnalyticsEasings.Standard);
                _morph = 0;
                _statusWidth.SpringTo(width, AnalyticsSprings.Smooth);
                _loop.Start();
            }
            InvalidateMeasure();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _palette.Refresh(this);
        _statusWidth.Jump(MeasureStatus(_shownStatus));
        _primed = true;
    }

    private double MeasureStatus(string text)
    {
        if (string.IsNullOrEmpty(text) || !_palette.IsReady && !this.IsAttachedToVisualTree())
        {
            return string.IsNullOrEmpty(text) ? 0 : text.Length * 7.0;
        }
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        return _text.Get(text, new Typeface(_palette.BodyFont), 14, tabular: true).Width;
    }

    private bool Advance(double dt)
    {
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var moving = _on.Advance(dt) | _hover.Advance(dt) | _oldAlpha.Advance(dt);
        moving |= Step(ref _slot, dt, full) | Step(ref _check, dt, full) | Step(ref _statusWidth, dt, full);
        if (_morph < 10)
        {
            _morph += dt;
            moving |= _morph < 0.05 + 0.032 * _shownStatus.Length + CharSeconds;
        }
        InvalidateMeasure();
        InvalidateVisual();
        return moving;
    }

    private static bool Step(ref AnalyticsSpringChannel c, double dt, bool full)
    {
        if (!c.IsMoving)
        {
            return false;
        }
        if (full)
        {
            c.Step(dt);
        }
        else
        {
            c.Jump(c.Target);
        }
        return c.IsMoving;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        SetHover(1);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(0);
    }

    private void SetHover(double target)
    {
        if (Animates)
        {
            _hover.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _hover.Jump(target);
            InvalidateVisual();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    private double PercentWidth()
    {
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
        return _text.Get(PercentText, new Typeface(_palette.BodyFont, FontStyle.Normal, _palette.Medium), 14, tabular: true).Width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var natural = 10 + _slot.Value + PercentWidth() + 8 + _statusWidth.Value + 12;
        return new Size(IsStretched && !double.IsInfinity(availableSize.Width) ? availableSize.Width : natural, ChipHeight);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (!_palette.IsReady)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return;
            }
            _palette.Refresh(this);
        }
        var p = _palette;
        var on = Math.Clamp(_on.Value, 0, 1);
        var hover = Math.Clamp(_hover.Value, 0, 1);
        var rect = new Rect(size);
        var radius = size.Height / 2;
        var border = AnalyticsColors.Lerp(AnalyticsColors.Lerp(p.Border, p.BorderStrong, hover), p.BorderStrong, on);
        var fill = AnalyticsColors.Fade(p.SurfaceMuted, on);
        var textColor = AnalyticsColors.Lerp(AnalyticsColors.Lerp(p.TextSecondary, p.Foreground, hover), p.Foreground, on);
        context.DrawRectangle(p.Brush(fill), p.Pen(border, 1), new RoundedRect(rect.Deflate(0.5), radius));
        var body = new Typeface(p.BodyFont);
        var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        var percent = _text.Get(PercentText, medium, 14, tabular: true);
        var x = 10.0;
        var cy = size.Height / 2;
        if (_check.Value > 0.01)
        {
            var scale = 14.0 / 24;
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, cy - 7)))
            {
                context.DrawGeometry(null, p.Pen(textColor, 2 / scale, Math.Clamp(_check.Value, 0, 1)), ArcIcons.Check);
            }
        }
        x += _slot.Value;
        percent.Draw(context, p.Brush(textColor), new Point(x, cy - percent.Height / 2));
        var statusRight = IsStretched;
        var statusX = statusRight ? size.Width - 12 - _statusWidth.Value : x + percent.Width + 8;
        DrawStatus(context, body, p, statusX, cy);
        if (AnalyticsFocus.IsVisible(this))
        {
            AnalyticsFocus.Draw(context, p, rect, radius);
        }
    }

    private void DrawStatus(DrawingContext context, Typeface body, AnalyticsPalette p, double x, double cy)
    {
        if (_oldAlpha.Value > 0.001 && _oldStatus.Length > 0)
        {
            var old = _text.Get(_oldStatus, body, 14, tabular: true);
            old.Draw(context, p.Brush(p.TextMuted, _oldAlpha.Value), new Point(x, cy - old.Height / 2));
        }
        if (_shownStatus.Length == 0)
        {
            return;
        }
        if (_morph >= 10 || _morph >= 0.05 + 0.032 * _shownStatus.Length + CharSeconds)
        {
            var whole = _text.Get(_shownStatus, body, 14, tabular: true);
            whole.Draw(context, p.Brush(p.TextMuted), new Point(x, cy - whole.Height / 2));
            return;
        }
        // New letters: 280ms each, the i-th starting 50 + 32 i ms in.
        for (var i = 0; i < _shownStatus.Length; i++)
        {
            var alpha = Math.Clamp((_morph - 0.05 - 0.032 * i) / CharSeconds, 0, 1);
            if (alpha <= 0)
            {
                continue;
            }
            var offset = _text.Get(_shownStatus[..i], body, 14, tabular: true).WidthIncludingTrailingWhitespace;
            if (i == 0)
            {
                offset = 0;
            }
            var glyph = _text.Get(_shownStatus[i].ToString(), body, 14, tabular: true);
            glyph.Draw(context, p.Brush(p.TextMuted, AnalyticsEasings.Enter.Ease(alpha)), new Point(x + offset, cy - glyph.Height / 2));
        }
    }

    protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        new Avalonia.Automation.Peers.ToggleButtonAutomationPeer(this);
}

/// <summary>
/// The alert row: the label, then the chips, wrapping with 8px gaps; under 440 (stretched) the label has a line and every
/// chip fills a line of its own.
/// </summary>
internal sealed class UsageAlertRow : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<UsageAlertRow, double>(nameof(Gap), 8);
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<UsageAlertRow, bool>(nameof(IsStacked));

    static UsageAlertRow()
    {
        AffectsMeasure<UsageAlertRow>(GapProperty, IsStackedProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    private List<Control> Visible() => Children.Where(c => c.IsVisible).ToList();

    private List<(Control Child, Rect Rect)> Flow(double width, bool measure)
    {
        var result = new List<(Control, Rect)>();
        double x = 0, y = 0, lineHeight = 0;
        foreach (var child in Visible())
        {
            if (child is UsageAlertChip chip)
            {
                chip.IsStretched = IsStacked;
            }
            if (measure)
            {
                child.Measure(new Size(IsStacked ? width : double.PositiveInfinity, double.PositiveInfinity));
            }
            var size = child.DesiredSize;
            var w = IsStacked && child is UsageAlertChip ? width : size.Width;
            if (x > 0 && (IsStacked || x + w > width + 0.01))
            {
                y += lineHeight + Gap;
                x = 0;
                lineHeight = 0;
            }
            result.Add((child, new Rect(x, y, w, size.Height)));
            x += w + Gap;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return result;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 10000 : availableSize.Width;
        var rects = Flow(width, measure: true);
        var h = rects.Count == 0 ? 0 : rects.Max(r => r.Rect.Bottom);
        var w = rects.Count == 0 ? 0 : rects.Max(r => r.Rect.Right);
        return new Size(IsStacked ? width : Math.Min(width, w), h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Items of one line share a centre line (the label is shorter than the chips).
        var rects = Flow(finalSize.Width, measure: false);
        foreach (var line in rects.GroupBy(r => r.Rect.Y))
        {
            var height = line.Max(r => r.Rect.Height);
            foreach (var (child, rect) in line)
            {
                child.Arrange(new Rect(rect.X, rect.Y + (height - rect.Height) / 2, rect.Width, rect.Height));
            }
        }
        return finalSize;
    }
}
