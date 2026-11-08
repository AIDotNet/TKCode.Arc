using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The amounts of <see cref="UsagePricing"/> (Arc's animated counter as the notes measured it, section 6.1-4): every
/// digit is a wheel keyed by place value that turns through the digits in between on the Smooth spring (up when the
/// amount grows, down when it shrinks), glyphs fade linearly (1 − d) and blur up to <see cref="MaxBlur"/> px a whole step
/// from the reading line; columns that appear or disappear (a thousands digit and its separator) grow and shrink on the
/// same spring. The text comes from <see cref="Format"/>, so currency symbols and separators follow the culture.
/// Core's <c>RollingDigits</c> draws every glyph in one Render pass and so cannot blur a glyph on its own; here the two
/// visible glyphs of a wheel are tiny child visuals whose <see cref="Visual.Effect"/> is a cached blur, set only while
/// they are off the line (no effect at rest).
/// </summary>
internal sealed class UsagePricingDigits : Control
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<UsagePricingDigits, decimal>(nameof(Value));

    public static readonly StyledProperty<Func<decimal, string>?> FormatProperty =
        AvaloniaProperty.Register<UsagePricingDigits, Func<decimal, string>?>(nameof(Format));

    /// <summary>Blur in px of a glyph one whole step from the reading line (0 = none).</summary>
    public static readonly StyledProperty<double> MaxBlurProperty =
        AvaloniaProperty.Register<UsagePricingDigits, double>(nameof(MaxBlur), 2);

    /// <summary>Distance between neighbouring glyphs of a wheel, in em.</summary>
    public static readonly StyledProperty<double> GlyphPitchProperty =
        AvaloniaProperty.Register<UsagePricingDigits, double>(nameof(GlyphPitch), 1.0);

    public static readonly StyledProperty<bool> IsInstantProperty =
        AvaloniaProperty.Register<UsagePricingDigits, bool>(nameof(IsInstant));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<UsagePricingDigits>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<UsagePricingDigits>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<UsagePricingDigits>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<UsagePricingDigits>();

    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Func<decimal, string>? Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public double MaxBlur { get => GetValue(MaxBlurProperty); set => SetValue(MaxBlurProperty, value); }
    public double GlyphPitch { get => GetValue(GlyphPitchProperty); set => SetValue(GlyphPitchProperty, value); }
    public bool IsInstant { get => GetValue(IsInstantProperty); set => SetValue(IsInstantProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The text the digits spell at rest.</summary>
    public string DisplayText { get; private set; } = string.Empty;

    /// <summary>Wheel targets of the digit columns that stay (tests).</summary>
    internal IReadOnlyList<double> WheelTargets => _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Target).ToList();

    /// <summary>Wheel positions of the digit columns that stay (tests).</summary>
    internal IReadOnlyList<double> WheelPositions =>
        _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Position?.Value ?? c.Target).ToList();

    /// <summary>The largest blur radius any glyph wears right now (tests).</summary>
    internal double CurrentMaxBlur => _columns.Where(c => c.IsDigit)
        .SelectMany(c => new[] { c.Lower, c.Upper })
        .Select(g => g?.Effect is IBlurEffect b && g.IsVisible ? b.Radius : 0)
        .DefaultIfEmpty(0).Max();

    private sealed class Column
    {
        public required string Key { get; init; }
        public bool IsDigit { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Digit { get; set; }
        public double Target { get; set; }
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public bool Leaving { get; set; }
        public UsagePricingDigitGlyph? Lower { get; set; }
        public UsagePricingDigitGlyph? Upper { get; set; }
        public double X { get; set; }
    }

    private readonly List<Column> _columns = new();
    private readonly TextLayout?[] _digits = new TextLayout?[10];
    private readonly Dictionary<string, TextLayout> _texts = new(StringComparer.Ordinal);
    private double _cellWidth;
    private double _lineHeight;
    private decimal? _previous;
    private int _direction = 1;

    static UsagePricingDigits()
    {
        AffectsMeasure<UsagePricingDigits>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<UsagePricingDigits>(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == FormatProperty)
        {
            Rebuild();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty)
        {
            ClearLayouts();
            InvalidateMeasure();
            InvalidateVisual();
            foreach (var column in _columns)
            {
                if (column.IsDigit)
                {
                    UpdateWheel(column);
                }
            }
        }
        else if (change.Property == ForegroundProperty)
        {
            // Glyphs are laid out once and drawn in the current Foreground: a color transition only repaints.
            InvalidateVisual();
            foreach (var child in VisualChildren)
            {
                child.InvalidateVisual();
            }
        }
        else if (change.Property == GlyphPitchProperty || change.Property == MaxBlurProperty)
        {
            foreach (var column in _columns)
            {
                if (column.IsDigit)
                {
                    UpdateWheel(column);
                }
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_columns.Count == 0)
        {
            Rebuild();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    private string FormatValue(decimal value) => Format?.Invoke(value) ?? value.ToString("N0", CultureInfo.InvariantCulture);

    private void Rebuild()
    {
        var value = Value;
        if (_previous is { } previous && previous != value)
        {
            _direction = value > previous ? 1 : -1;
        }
        _previous = value;
        var text = FormatValue(value);
        DisplayText = text;
        AutomationProperties.SetName(this, text);

        var totalDigits = 0;
        foreach (var ch in text)
        {
            if (char.IsAsciiDigit(ch))
            {
                totalDigits++;
            }
        }
        var wanted = new List<Column>(text.Length);
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsAsciiDigit(ch))
            {
                var place = totalDigits - 1 - seen++;
                wanted.Add(new Column { Key = "i" + place.ToString(CultureInfo.InvariantCulture), IsDigit = true, Digit = ch - '0' });
            }
            else
            {
                var key = seen == 0 ? $"p{i}{ch}" : seen < totalDigits ? $"g{totalDigits - seen}{ch}" : $"s{text.Length - i}{ch}";
                wanted.Add(new Column { Key = key, Text = ch.ToString() });
            }
        }

        var animate = IsLoaded && !IsInstant && ArcMotion.IsFull(this);
        var existing = new Dictionary<string, Column>(StringComparer.Ordinal);
        foreach (var column in _columns)
        {
            if (!column.Leaving)
            {
                existing.TryAdd(column.Key, column);
            }
        }
        var next = new List<Column>(wanted.Count + 2);
        var structureChanged = false;
        foreach (var w in wanted)
        {
            if (existing.Remove(w.Key, out var column))
            {
                if (column.IsDigit && column.Digit != w.Digit)
                {
                    TurnTo(column, w.Digit, animate);
                }
                next.Add(column);
                continue;
            }
            structureChanged = true;
            w.Target = w.Digit;
            w.Presence = CreateValue(animate ? 0 : 1, measure: true);
            if (w.IsDigit)
            {
                w.Position = CreateValue(w.Digit, measure: false, w);
                w.Lower = AddGlyph();
                w.Upper = AddGlyph();
            }
            if (animate)
            {
                _ = w.Presence.AnimateTo(1, MotionSpec.Spring(ArcMotionPresets.Smooth));
            }
            next.Add(w);
        }
        foreach (var gone in existing.Values)
        {
            structureChanged = true;
            gone.Leaving = true;
            if (animate && gone.Presence is not null)
            {
                var leaving = gone;
                gone.Presence.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Smooth)).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => RemoveColumn(leaving)), TaskScheduler.Default);
            }
        }
        // Leaving columns (of this change or earlier ones) keep their place until their exit ends; without animation
        // they go at once.
        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            if (!column.Leaving)
            {
                continue;
            }
            if (animate)
            {
                next.Insert(Math.Min(i, next.Count), column);
            }
            else
            {
                RemoveGlyphs(column);
            }
        }
        _columns.Clear();
        _columns.AddRange(next);
        foreach (var column in _columns)
        {
            if (column.IsDigit)
            {
                UpdateWheel(column);
            }
        }
        if (structureChanged)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private void TurnTo(Column column, int digit, bool animate)
    {
        var delta = _direction < 0 ? -((column.Digit - digit + 10) % 10) : (digit - column.Digit + 10) % 10;
        column.Target += delta;
        column.Digit = digit;
        if (!animate)
        {
            column.Position?.Jump(column.Target);
        }
        else
        {
            _ = column.Position?.AnimateTo(column.Target, MotionSpec.Spring(ArcMotionPresets.Smooth));
        }
    }

    private void RemoveColumn(Column column)
    {
        if (!column.Leaving || !_columns.Remove(column))
        {
            return;
        }
        RemoveGlyphs(column);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void RemoveGlyphs(Column column)
    {
        if (column.Lower is not null)
        {
            VisualChildren.Remove(column.Lower);
        }
        if (column.Upper is not null)
        {
            VisualChildren.Remove(column.Upper);
        }
        column.Position?.Stop();
        column.Presence?.Stop();
    }

    private UsagePricingDigitGlyph AddGlyph()
    {
        var glyph = new UsagePricingDigitGlyph(this);
        VisualChildren.Add(glyph);
        return glyph;
    }

    private MotionValue<double> CreateValue(double initial, bool measure, Column? wheel = null)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        if (measure)
        {
            value.Changed += _ =>
            {
                InvalidateMeasure();
                InvalidateVisual();
                foreach (var column in _columns)
                {
                    if (column.IsDigit)
                    {
                        UpdateWheel(column);
                    }
                }
            };
        }
        else if (wheel is not null)
        {
            value.Changed += _ => UpdateWheel(wheel);
        }
        return value;
    }

    /// <summary>Places the two glyphs of a wheel around the reading line: offset, fade and blur follow the distance.</summary>
    private void UpdateWheel(Column column)
    {
        if (column.Lower is null || column.Upper is null)
        {
            return;
        }
        var position = column.Position?.Value ?? column.Target;
        var floor = Math.Floor(position);
        var fraction = position - floor;
        var digit = (int)(((floor % 10) + 10) % 10);
        var presence = Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
        Place(column.Lower, digit, -fraction, presence);
        Place(column.Upper, (digit + 1) % 10, 1 - fraction, presence);
    }

    private void Place(UsagePricingDigitGlyph glyph, int digit, double offset, double presence)
    {
        var distance = Math.Abs(offset);
        if (distance >= 0.999 || presence <= 0.001)
        {
            glyph.IsVisible = false;
            glyph.Effect = null;
            return;
        }
        EnsureLayouts();
        glyph.IsVisible = true;
        glyph.Show(_digits[digit], (_cellWidth - (_digits[digit]?.WidthIncludingTrailingWhitespace ?? 0)) / 2);
        glyph.Shift.Y = offset * FontSize * GlyphPitch;
        glyph.Opacity = (1 - distance) * presence;
        glyph.Effect = MaxBlur > 0 ? ArcBlur.Get(MaxBlur * distance) : null;
    }

    private void ClearLayouts()
    {
        foreach (var layout in _digits)
        {
            layout?.Dispose();
        }
        Array.Clear(_digits);
        foreach (var layout in _texts.Values)
        {
            layout.Dispose();
        }
        _texts.Clear();
    }

    private void EnsureLayouts()
    {
        if (_digits[0] is not null)
        {
            return;
        }
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        for (var d = 0; d < 10; d++)
        {
            _digits[d] = new TextLayout(d.ToString(CultureInfo.InvariantCulture), typeface, FontSize, Brushes.Black);
        }
        _cellWidth = _digits.Max(g => g!.WidthIncludingTrailingWhitespace);
        _lineHeight = _digits[0]!.Height;
    }

    private TextLayout TextOf(string text)
    {
        if (!_texts.TryGetValue(text, out var layout))
        {
            layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Brushes.Black);
            _texts[text] = layout;
        }
        return layout;
    }

    private double WidthOf(Column column)
    {
        var width = column.IsDigit ? _cellWidth : TextOf(column.Text).WidthIncludingTrailingWhitespace;
        return width * Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayouts();
        var width = 0.0;
        foreach (var column in _columns)
        {
            width += WidthOf(column);
        }
        foreach (var child in VisualChildren)
        {
            if (child is Layoutable layoutable)
            {
                layoutable.Measure(new Size(_cellWidth, _lineHeight));
            }
        }
        return new Size(width, _lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var column in _columns)
        {
            var width = WidthOf(column);
            column.X = x;
            if (column.IsDigit)
            {
                var cell = new Rect(x + (width - _cellWidth) / 2, 0, _cellWidth, _lineHeight);
                column.Lower?.Arrange(cell);
                column.Upper?.Arrange(cell);
            }
            x += width;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        EnsureLayouts();
        foreach (var column in _columns)
        {
            if (column.IsDigit)
            {
                continue;
            }
            var presence = Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
            if (presence <= 0.001)
            {
                continue;
            }
            using (context.PushOpacity(presence))
            {
                UsagePricingGlyphs.Draw(context, TextOf(column.Text), Foreground ?? Brushes.Black, new Point(column.X, 0));
            }
        }
    }
}

internal static class UsagePricingGlyphs
{
    /// <summary>
    /// Draws a laid-out glyph's runs in <paramref name="brush"/> with its top left at <paramref name="origin"/> (what
    /// <see cref="TextLayout.Draw"/> does, with another brush), so color transitions need no new layout.
    /// </summary>
    public static void Draw(DrawingContext context, TextLayout glyph, IBrush brush, Point origin)
    {
        var y = origin.Y;
        var lines = glyph.TextLines;
        for (var l = 0; l < lines.Count; l++)
        {
            var line = lines[l];
            var x = origin.X + line.Start;
            var runs = line.TextRuns;
            for (var r = 0; r < runs.Count; r++)
            {
                if (runs[r] is not DrawableTextRun drawable)
                {
                    continue;
                }
                if (drawable is ShapedTextRun shaped && shaped.GlyphRun.GlyphInfos.Count > 0)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x, y + (line.Baseline - shaped.Baseline))))
                    {
                        context.DrawGlyphRun(brush, shaped.GlyphRun);
                    }
                }
                x += drawable.Size.Width;
            }
            y += line.Height;
        }
    }
}

/// <summary>One glyph of a <see cref="UsagePricingDigits"/> wheel: a visual of its own so it can wear its own blur.</summary>
internal sealed class UsagePricingDigitGlyph : Control
{
    private readonly UsagePricingDigits _owner;
    private TextLayout? _layout;
    private double _x;

    public UsagePricingDigitGlyph(UsagePricingDigits owner)
    {
        _owner = owner;
        Shift = new TranslateTransform();
        RenderTransform = Shift;
        IsHitTestVisible = false;
        IsVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The vertical offset from the reading line (changed in place every frame: no allocation).</summary>
    public TranslateTransform Shift { get; }

    public void Show(TextLayout? layout, double x)
    {
        if (!ReferenceEquals(layout, _layout) || _x != x)
        {
            _layout = layout;
            _x = x;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_layout is not null)
        {
            UsagePricingGlyphs.Draw(context, _layout, _owner.Foreground ?? Brushes.Black, new Point(_x, 0));
        }
    }
}
