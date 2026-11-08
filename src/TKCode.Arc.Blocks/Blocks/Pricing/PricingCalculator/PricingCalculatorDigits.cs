using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The pricing calculator's odometer (Arc pricing-calculator, design reference, notes 3.2 and 6.1). Every digit is a wheel
/// keyed by place value (the ones column stays the ones column). When the number changes, every wheel turns
/// <c>(new − old) mod 10</c> steps forward if the whole number grew, or <c>(old − new) mod 10</c> steps back if it shrank,
/// rolling through the digits in between, on the Smooth spring (no stagger, so all wheels share one curve and settle
/// together); a new change re-targets each wheel from where it is, keeping its speed. A glyph <c>d</c> steps from the
/// reading line is drawn 1em·d away, at opacity 1 − |d| and blurred by 2px·|d|, inside a column that fades out over its top
/// and bottom 0.24em. New columns (and their group separator) grow from zero width and fade in on the same spring, showing
/// their final digit; removed columns shrink and fade out keeping their old digit. Reduced motion and Off jump.
/// </summary>
/// <remarks>
/// Core's <c>RollingDigits</c> turns its wheels by the same rule, but it draws every glyph in one <c>Render</c>, so it cannot
/// blur glyphs by their distance, and its new columns grow on the Morph spring (Arc's preview grows them on Smooth). This
/// private copy gives each wheel two glyph visuals with a quantized <see cref="ArcBlur"/> effect. Shared-helper candidate:
/// a <c>BlurPerStep</c> / <c>PresenceSpring</c> option on <c>RollingDigits</c>.
/// </remarks>
internal sealed class PricingCalculatorDigits : Control
{
    /// <summary>Defines the <see cref="Value"/> property.</summary>
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<PricingCalculatorDigits, decimal>(nameof(Value));

    /// <summary>Defines the <see cref="Decimals"/> property.</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<PricingCalculatorDigits, int>(nameof(Decimals));

    /// <summary>Defines the <see cref="Prefix"/> property.</summary>
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<PricingCalculatorDigits, string?>(nameof(Prefix));

    /// <summary>Defines the <see cref="Suffix"/> property.</summary>
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<PricingCalculatorDigits, string?>(nameof(Suffix));

    /// <summary>Defines the <see cref="Culture"/> property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<PricingCalculatorDigits, CultureInfo?>(nameof(Culture));

    /// <summary>Defines the <see cref="FontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<PricingCalculatorDigits>();

    /// <summary>Defines the <see cref="FontSize"/> property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<PricingCalculatorDigits>();

    /// <summary>Defines the <see cref="FontWeight"/> property.</summary>
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<PricingCalculatorDigits>();

    /// <summary>Defines the <see cref="Foreground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<PricingCalculatorDigits>();

    /// <summary>Defines the <see cref="LetterSpacing"/> property.</summary>
    public static readonly StyledProperty<double> LetterSpacingProperty = TextElement.LetterSpacingProperty.AddOwner<PricingCalculatorDigits>();

    /// <summary>The blur per wheel step of distance (2px, notes 3.2).</summary>
    public const double BlurPerStep = 2;

    /// <summary>The room above and below the line box the wheel may draw in, in em (CSS padding .16em).</summary>
    public const double FeatherEm = 0.16;

    /// <summary>The fade at the column's top and bottom edge, in em (mask .24em).</summary>
    public const double MaskEm = 0.24;

    private readonly List<Column> _columns = new();
    private readonly TextLayout?[] _digits = new TextLayout?[10];
    private readonly Dictionary<string, TextLayout> _texts = new(StringComparer.Ordinal);
    private double _cellWidth;
    private double _lineHeight;
    private decimal? _previous;
    private int _direction = 1;
    private IBrush? _mask;
    private string _formatted = string.Empty;

    static PricingCalculatorDigits()
    {
        AffectsMeasure<PricingCalculatorDigits>(PrefixProperty, SuffixProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty, LetterSpacingProperty);
        AffectsRender<PricingCalculatorDigits>(ForegroundProperty);
    }

    /// <summary>The number shown.</summary>
    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Digits after the decimal separator.</summary>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    /// <summary>Static text before the number (a currency symbol).</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>Static text after the number.</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>Culture of the separators (en-US when null).</summary>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <summary>Font family of the glyphs.</summary>
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>Font size of the glyphs; 1em is also a wheel step.</summary>
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Font weight of the glyphs.</summary>
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>Color of the glyphs.</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Letter spacing added after every glyph.</summary>
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    /// <summary>The full text shown (prefix, number, suffix).</summary>
    public string DisplayText => $"{Prefix}{_formatted}{Suffix}";

    /// <summary>Wheel targets of the digit columns that stay, from the left (tests read them).</summary>
    internal IReadOnlyList<double> WheelTargets => _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Target).ToList();

    /// <summary>Current wheel positions of the digit columns that stay, from the left (tests read them).</summary>
    internal IReadOnlyList<double> WheelPositions =>
        _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Position?.Value ?? c.Target).ToList();

    /// <summary>The blur radius of the first glyph of each digit column (tests read it).</summary>
    internal IReadOnlyList<double> GlyphBlurs => _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.View.BlurA).ToList();

    /// <summary>Columns, leaving ones included (tests read the count).</summary>
    internal int ColumnCount => _columns.Count;

    /// <summary>Presence (0–1) of each column, leaving ones included (tests read it).</summary>
    internal IReadOnlyList<double> Presences => _columns.Select(c => c.Presence?.Value ?? 1).ToList();

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
        public required PricingCalculatorDigitColumn View { get; init; }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == DecimalsProperty || change.Property == CultureProperty)
        {
            Rebuild();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            Array.Clear(_digits);
            _texts.Clear();
            _mask = null;
            foreach (var column in _columns)
            {
                column.View.InvalidateGlyphs();
            }
            InvalidateMeasure();
        }
        else if (change.Property == PrefixProperty || change.Property == SuffixProperty)
        {
            UpdateName();
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

    private void UpdateName() => AutomationProperties.SetName(this, DisplayText);

    private void Rebuild()
    {
        var value = Value;
        if (_previous is { } previous && value != previous)
        {
            _direction = value > previous ? 1 : -1;
        }
        _previous = value;

        var culture = Culture ?? CultureInfo.GetCultureInfo("en-US");
        var formatted = Math.Abs(value).ToString("N" + Math.Max(0, Decimals).ToString(CultureInfo.InvariantCulture), culture);
        if (value < 0)
        {
            formatted = culture.NumberFormat.NegativeSign + formatted;
        }
        _formatted = formatted;
        UpdateName();

        var group = culture.NumberFormat.NumberGroupSeparator;
        var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
        var integerDigits = 0;
        var at = formatted.IndexOf(decimalSeparator, StringComparison.Ordinal);
        var integerPart = at < 0 ? formatted : formatted[..at];
        foreach (var ch in integerPart)
        {
            if (char.IsDigit(ch))
            {
                integerDigits++;
            }
        }

        var wanted = new List<(string Key, bool IsDigit, string Text, int Digit)>();
        var place = integerDigits;
        var fraction = 0;
        var seenDecimal = false;
        for (var i = 0; i < formatted.Length; i++)
        {
            var ch = formatted[i];
            if (char.IsDigit(ch))
            {
                wanted.Add(seenDecimal
                    ? ("f" + (fraction++).ToString(CultureInfo.InvariantCulture), true, string.Empty, ch - '0')
                    : ("i" + (--place).ToString(CultureInfo.InvariantCulture), true, string.Empty, ch - '0'));
            }
            else if (!seenDecimal && decimalSeparator.Length > 0 && string.CompareOrdinal(formatted, i, decimalSeparator, 0, decimalSeparator.Length) == 0)
            {
                seenDecimal = true;
                wanted.Add(("d", false, decimalSeparator, 0));
                i += decimalSeparator.Length - 1;
            }
            else if (group.Length > 0 && string.CompareOrdinal(formatted, i, group, 0, group.Length) == 0)
            {
                wanted.Add(("g" + place.ToString(CultureInfo.InvariantCulture), false, group, 0));
                i += group.Length - 1;
            }
            else
            {
                wanted.Add(("l" + i.ToString(CultureInfo.InvariantCulture) + ch, false, ch.ToString(), 0));
            }
        }

        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && level == ArcMotionLevel.Full;
        var existing = new Dictionary<string, Column>(StringComparer.Ordinal);
        foreach (var column in _columns)
        {
            if (!column.Leaving)
            {
                existing[column.Key] = column;
            }
        }
        var next = new List<Column>(wanted.Count + 2);
        var structureChanged = false;
        foreach (var w in wanted)
        {
            // A separator whose text changed (another culture) is replaced, not reused.
            if (existing.TryGetValue(w.Key, out var stale) && !stale.IsDigit && stale.Text != w.Text)
            {
                existing.Remove(w.Key);
                _columns.Remove(stale);
                VisualChildren.Remove(stale.View);
                stale.Position?.Dispose();
                stale.Presence?.Dispose();
                structureChanged = true;
            }
            if (existing.Remove(w.Key, out var column))
            {
                if (column.IsDigit && column.Digit != w.Digit)
                {
                    TurnTo(column, w.Digit, animate);
                }
                next.Add(column);
            }
            else
            {
                structureChanged = true;
                var view = new PricingCalculatorDigitColumn(this, w.IsDigit, w.Text);
                var created = new Column { Key = w.Key, IsDigit = w.IsDigit, Text = w.Text, Digit = w.Digit, Target = w.Digit, View = view };
                created.Position = CreateValue(w.Digit, measure: false, created);
                created.Presence = CreateValue(animate ? 0 : 1, measure: true, created);
                if (animate)
                {
                    _ = created.Presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
                }
                VisualChildren.Add(view);
                view.Update(created.Position.Value, created.Presence.Value);
                next.Add(created);
            }
        }
        foreach (var gone in existing.Values)
        {
            structureChanged = true;
            gone.Leaving = true;
            var index = _columns.IndexOf(gone);
            next.Insert(Math.Min(index, next.Count), gone);
            if (animate && gone.Presence is not null)
            {
                var leaving = gone;
                gone.Presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => RemoveColumn(leaving)), TaskScheduler.Default);
            }
            else
            {
                next.Remove(gone);
                VisualChildren.Remove(gone.View);
                gone.Position?.Dispose();
                gone.Presence?.Dispose();
            }
        }
        // Columns already leaving keep their place until they are gone.
        foreach (var column in _columns)
        {
            if (column.Leaving && !next.Contains(column))
            {
                next.Insert(Math.Min(_columns.IndexOf(column), next.Count), column);
            }
        }
        _columns.Clear();
        _columns.AddRange(next);
        if (structureChanged)
        {
            InvalidateMeasure();
        }
    }

    private void RemoveColumn(Column column)
    {
        if (!column.Leaving || !_columns.Remove(column))
        {
            return;
        }
        VisualChildren.Remove(column.View);
        column.Position?.Dispose();
        column.Presence?.Dispose();
        InvalidateMeasure();
    }

    private void TurnTo(Column column, int digit, bool animate)
    {
        var delta = _direction < 0 ? -((column.Digit - digit + 10) % 10) : (digit - column.Digit + 10) % 10;
        column.Target += delta;
        column.Digit = digit;
        if (column.Position is null)
        {
            return;
        }
        if (animate)
        {
            // Re-targeting a running MotionValue keeps its velocity: a second change mid-roll carries on from there.
            _ = column.Position.AnimateTo(column.Target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            column.Position.Jump(column.Target);
        }
    }

    private MotionValue<double> CreateValue(double initial, bool measure, Column column)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        if (measure)
        {
            value.Changed += _ => InvalidateMeasure();
        }
        else
        {
            value.Changed += v => column.View.Update(v, column.Presence?.Value ?? 1);
        }
        return value;
    }

    internal TextLayout DigitLayout(int digit)
    {
        EnsureGlyphs();
        return _digits[digit]!;
    }

    internal TextLayout TextLayout(string text)
    {
        if (!_texts.TryGetValue(text, out var layout))
        {
            layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black,
                letterSpacing: LetterSpacing);
            _texts[text] = layout;
        }
        return layout;
    }

    internal double CellWidth
    {
        get
        {
            EnsureGlyphs();
            return _cellWidth;
        }
    }

    internal double LineHeight
    {
        get
        {
            EnsureGlyphs();
            return _lineHeight;
        }
    }

    internal double Feather => FontSize * FeatherEm;

    /// <summary>The column mask: transparent at the edges, opaque from 0.24em in (one immutable brush per font size).</summary>
    internal IBrush Mask
    {
        get
        {
            if (_mask is null)
            {
                var height = LineHeight + 2 * Feather;
                var edge = height <= 0 ? 0 : Math.Clamp(FontSize * MaskEm / height, 0, 0.5);
                _mask = new ImmutableLinearGradientBrush(
                    new[]
                    {
                        new ImmutableGradientStop(0, Colors.Transparent),
                        new ImmutableGradientStop(edge, Colors.Black),
                        new ImmutableGradientStop(1 - edge, Colors.Black),
                        new ImmutableGradientStop(1, Colors.Transparent)
                    },
                    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                    endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
            }
            return _mask;
        }
    }

    private void EnsureGlyphs()
    {
        if (_digits[0] is not null)
        {
            return;
        }
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var brush = Foreground ?? Brushes.Black;
        var widest = 0.0;
        for (var d = 0; d < 10; d++)
        {
            var layout = new TextLayout(d.ToString(CultureInfo.InvariantCulture), typeface, FontSize, brush, letterSpacing: LetterSpacing);
            _digits[d] = layout;
            widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
        }
        _cellWidth = widest;
        _lineHeight = _digits[0]!.Height;
    }

    private double ColumnWidth(Column column)
    {
        var width = column.IsDigit ? CellWidth : TextLayout(column.Text).WidthIncludingTrailingWhitespace;
        return width * Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGlyphs();
        var width = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            width += TextLayout(Prefix).WidthIncludingTrailingWhitespace;
        }
        foreach (var column in _columns)
        {
            width += ColumnWidth(column);
            column.View.Measure(Size.Infinity);
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            width += TextLayout(Suffix).WidthIncludingTrailingWhitespace;
        }
        return new Size(width, _lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = string.IsNullOrEmpty(Prefix) ? 0 : TextLayout(Prefix).WidthIncludingTrailingWhitespace;
        var feather = Feather;
        foreach (var column in _columns)
        {
            var width = ColumnWidth(column);
            column.View.Arrange(new Rect(x, -feather, width, _lineHeight + 2 * feather));
            column.View.Update(column.Position?.Value ?? column.Digit, column.Presence?.Value ?? 1);
            x += width;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var x = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            var prefix = TextLayout(Prefix);
            prefix.Draw(context, new Point(0, 0));
            x = prefix.WidthIncludingTrailingWhitespace;
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            foreach (var column in _columns)
            {
                x += ColumnWidth(column);
            }
            TextLayout(Suffix).Draw(context, new Point(x, 0));
        }
    }
}

/// <summary>One wheel (or one static symbol) of <see cref="PricingCalculatorDigits"/>: clipped, masked, two glyph visuals.</summary>
internal sealed class PricingCalculatorDigitColumn : Control
{
    private readonly PricingCalculatorDigits _owner;
    private readonly bool _isDigit;
    private readonly string _text;
    private readonly PricingCalculatorDigitGlyph _a;
    private readonly PricingCalculatorDigitGlyph? _b;

    public PricingCalculatorDigitColumn(PricingCalculatorDigits owner, bool isDigit, string text)
    {
        _owner = owner;
        _isDigit = isDigit;
        _text = text;
        ClipToBounds = true;
        IsHitTestVisible = false;
        _a = new PricingCalculatorDigitGlyph();
        VisualChildren.Add(_a);
        if (isDigit)
        {
            _b = new PricingCalculatorDigitGlyph();
            VisualChildren.Add(_b);
        }
    }

    /// <summary>The blur radius of the glyph nearest the reading line.</summary>
    public double BlurA { get; private set; }

    public void InvalidateGlyphs()
    {
        _a.Layout = null;
        if (_b is not null)
        {
            _b.Layout = null;
        }
        OpacityMask = null;
    }

    /// <summary>Places the glyphs for wheel position <paramref name="position"/> and presence <paramref name="presence"/>.</summary>
    public void Update(double position, double presence)
    {
        Opacity = Math.Clamp(presence, 0, 1);
        var em = _owner.FontSize;
        if (!_isDigit)
        {
            _a.Layout = _owner.TextLayout(_text);
            _a.SetOffset(0, 1, null);
            return;
        }
        OpacityMask ??= _owner.Mask;
        var floor = Math.Floor(position);
        var frac = position - floor;
        var first = (int)((floor % 10 + 10) % 10);
        var second = (first + 1) % 10;
        // Glyph d sits (d − v)·1em from the reading line: the lower digit is −frac steps away, the next one 1 − frac.
        Place(_a, first, -frac, em);
        Place(_b!, second, 1 - frac, em);
        BlurA = frac <= 0.5 ? Blur(frac) : Blur(1 - frac);
    }

    private static double Blur(double distance) => distance < 0.025 ? 0 : PricingCalculatorDigits.BlurPerStep * distance;

    private void Place(PricingCalculatorDigitGlyph glyph, int digit, double offset, double em)
    {
        var distance = Math.Abs(offset);
        glyph.Layout = _owner.DigitLayout(digit);
        if (distance >= 1)
        {
            glyph.SetOffset(0, 0, null);
            return;
        }
        var blur = Blur(distance);
        glyph.SetOffset(offset * em, 1 - distance, blur <= 0 ? null : ArcBlur.Get(blur));
    }

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var feather = _owner.Feather;
        var line = _owner.LineHeight;
        var cell = _isDigit ? _owner.CellWidth : _owner.TextLayout(_text).WidthIncludingTrailingWhitespace;
        // The glyph keeps its full cell while the column grows or shrinks, centred, and is clipped to the column.
        var x = (finalSize.Width - cell) / 2;
        var rect = new Rect(x, feather, cell, line);
        _a.Arrange(rect);
        _b?.Arrange(rect);
        return finalSize;
    }
}

/// <summary>One glyph of a wheel: draws a laid-out digit, centred in its cell, moved by a translate.</summary>
internal sealed class PricingCalculatorDigitGlyph : Control
{
    private readonly TranslateTransform _translate = new();
    private TextLayout? _layout;

    public PricingCalculatorDigitGlyph()
    {
        RenderTransform = _translate;
        IsHitTestVisible = false;
    }

    public TextLayout? Layout
    {
        get => _layout;
        set
        {
            if (!ReferenceEquals(_layout, value))
            {
                _layout = value;
                InvalidateVisual();
            }
        }
    }

    public void SetOffset(double y, double opacity, IEffect? effect)
    {
        _translate.Y = y;
        Opacity = opacity;
        IsVisible = opacity > 0.001;
        if (!ReferenceEquals(Effect, effect))
        {
            Effect = effect;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_layout is null)
        {
            return;
        }
        _layout.Draw(context, new Point((Bounds.Width - _layout.WidthIncludingTrailingWhitespace) / 2, 0));
    }
}
