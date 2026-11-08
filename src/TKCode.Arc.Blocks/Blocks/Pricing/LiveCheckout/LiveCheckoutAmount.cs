using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An amount whose digits roll like the reference checkout's: every digit is a 0–9 strip that slides straight from the
/// old digit to the new one (7 → 0 runs down through 6…1: no wrap, no short way round), all columns at once on the ω 20
/// spring, no stagger, fade or blur. Columns are keyed by place value from the decimal point, so a change in the number
/// of digits keeps the columns on the right rolling while a new high column appears at once and the width jumps.
/// Reduced motion and Off jump. The full text is the accessible name, so readers never hear single digits.
/// </summary>
/// <remarks>
/// Not Core's <c>RollingDigits</c>: that one turns wheels in the direction of the whole number (with wrap-around) and
/// grows new columns on a spring, while the reference slides each strip straight and makes new columns appear at once.
/// Code-driven because each column follows its own spring and keeps its velocity when the amount changes again.
/// </remarks>
internal sealed class LiveCheckoutAmount : Control
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<LiveCheckoutAmount, decimal>(nameof(Value));

    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<LiveCheckoutAmount, string>(nameof(Currency), "USD");

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<LiveCheckoutAmount, CultureInfo?>(nameof(Culture));

    /// <summary>Text put before the amount (the discount's minus sign).</summary>
    public static readonly StyledProperty<string?> SignProperty =
        AvaloniaProperty.Register<LiveCheckoutAmount, string?>(nameof(Sign));

    /// <summary>Line box height as a multiple of the font size (1.4 by default).</summary>
    public static readonly StyledProperty<double> LineHeightFactorProperty =
        AvaloniaProperty.Register<LiveCheckoutAmount, double>(nameof(LineHeightFactor), 1.4);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<LiveCheckoutAmount>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<LiveCheckoutAmount>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<LiveCheckoutAmount>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LiveCheckoutAmount>();
    public static readonly StyledProperty<double> LetterSpacingProperty = TextElement.LetterSpacingProperty.AddOwner<LiveCheckoutAmount>();

    private sealed class Column
    {
        public required string Key { get; init; }
        public bool IsDigit { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Digit { get; set; }
        public MotionValue<double>? Position { get; set; }
    }

    private readonly List<Column> _columns = new();
    private readonly TextLayout?[] _digits = new TextLayout?[10];
    private readonly Dictionary<string, TextLayout> _glyphs = new(StringComparer.Ordinal);
    private double _cell;
    private bool _built;

    static LiveCheckoutAmount()
    {
        AffectsMeasure<LiveCheckoutAmount>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightFactorProperty,
            LetterSpacingProperty);
        AffectsRender<LiveCheckoutAmount>(ForegroundProperty);
    }

    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    public string? Sign { get => GetValue(SignProperty); set => SetValue(SignProperty, value); }
    public double LineHeightFactor { get => GetValue(LineHeightFactorProperty); set => SetValue(LineHeightFactorProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }

    /// <summary>The formatted amount as shown (also the accessible name).</summary>
    public string Text { get; private set; } = string.Empty;

    /// <summary>Where each digit strip stands now, most significant first (tests: straight travel, no wrap).</summary>
    internal IReadOnlyList<double> Positions => _columns.Where(c => c.IsDigit).Select(c => c.Position?.Value ?? c.Digit).ToArray();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == CurrencyProperty || change.Property == CultureProperty ||
            change.Property == SignProperty)
        {
            Rebuild();
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty ||
                 change.Property == LetterSpacingProperty)
        {
            ClearGlyphs();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_built)
        {
            Rebuild();
        }
    }

    private void ClearGlyphs()
    {
        for (var i = 0; i < _digits.Length; i++)
        {
            _digits[i]?.Dispose();
            _digits[i] = null;
        }
        foreach (var layout in _glyphs.Values)
        {
            layout.Dispose();
        }
        _glyphs.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void Rebuild()
    {
        _built = true;
        var culture = Culture ?? SettingsFormat.DefaultCulture;
        var text = (Sign ?? string.Empty) + SettingsFormat.Money(Value, Currency, culture);
        Text = text;
        AutomationProperties.SetName(this, text);

        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var lastDigit = -1;
        var firstDigit = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                firstDigit = firstDigit < 0 ? i : firstDigit;
                lastDigit = i;
            }
        }
        var decimalAt = -1;
        if (firstDigit >= 0)
        {
            var at = text.LastIndexOf(separator, lastDigit, StringComparison.Ordinal);
            decimalAt = at > firstDigit ? at : -1;
        }

        // Integer places counted leftwards from the decimal point (or the last digit), fractions rightwards.
        var integerEnd = decimalAt >= 0 ? decimalAt : lastDigit + 1;
        var wanted = new List<(string Key, bool IsDigit, string Text, int Digit)>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c))
            {
                if (i < integerEnd)
                {
                    var place = 0;
                    for (var j = i + 1; j < integerEnd; j++)
                    {
                        place += char.IsAsciiDigit(text[j]) ? 1 : 0;
                    }
                    wanted.Add(($"i{place}", true, string.Empty, c - '0'));
                }
                else
                {
                    var index = 0;
                    for (var j = integerEnd; j < i; j++)
                    {
                        index += char.IsAsciiDigit(text[j]) ? 1 : 0;
                    }
                    wanted.Add(($"f{index}", true, string.Empty, c - '0'));
                }
            }
            else if (i == decimalAt)
            {
                wanted.Add(("d", false, separator, 0));
                i += separator.Length - 1;
            }
            else if (firstDigit >= 0 && i > firstDigit && i < lastDigit)
            {
                wanted.Add(($"g{i - text.Length}", false, c.ToString(), 0));
            }
            else if (firstDigit < 0 || i < firstDigit)
            {
                wanted.Add(($"p{i}", false, c.ToString(), 0));
            }
            else
            {
                wanted.Add(($"s{text.Length - i}", false, c.ToString(), 0));
            }
        }

        var animate = LiveCheckoutMotion.CanAnimate(this);
        var existing = new Dictionary<string, Column>(StringComparer.Ordinal);
        foreach (var column in _columns)
        {
            existing[column.Key] = column;
        }
        var structure = wanted.Count != _columns.Count;
        _columns.Clear();
        foreach (var w in wanted)
        {
            if (existing.Remove(w.Key, out var column) && column.IsDigit == w.IsDigit)
            {
                column.Text = w.Text;
                if (column.IsDigit && column.Digit != w.Digit)
                {
                    column.Digit = w.Digit;
                    column.Position ??= CreatePosition(w.Digit);
                    if (animate)
                    {
                        _ = column.Position.AnimateTo(w.Digit, MotionSpec.Spring(LiveCheckoutMotion.Roll));
                    }
                    else
                    {
                        column.Position.Jump(w.Digit);
                    }
                }
                else if (!column.IsDigit && column.Text != w.Text)
                {
                    structure = true;
                }
                _columns.Add(column);
            }
            else
            {
                // A new column shows its digit at once (no roll, no growth), as measured.
                structure = true;
                var fresh = new Column { Key = w.Key, IsDigit = w.IsDigit, Text = w.Text, Digit = w.Digit };
                if (w.IsDigit)
                {
                    fresh.Position = CreatePosition(w.Digit);
                }
                _columns.Add(fresh);
            }
        }
        foreach (var gone in existing.Values)
        {
            gone.Position?.Dispose();
        }
        if (structure || existing.Count > 0)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private MotionValue<double> CreatePosition(int digit)
    {
        var value = new MotionValue<double>(this, digit, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private double LineHeight => Math.Max(1, FontSize * LineHeightFactor);

    private TextLayout Layout(string text)
        => new(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black,
            letterSpacing: LetterSpacing);

    private TextLayout Digit(int digit)
    {
        var layout = _digits[digit];
        if (layout is null)
        {
            layout = Layout(((char)('0' + digit)).ToString());
            _digits[digit] = layout;
        }
        return layout;
    }

    private TextLayout Glyph(string text)
    {
        if (!_glyphs.TryGetValue(text, out var layout))
        {
            layout = Layout(text);
            _glyphs[text] = layout;
        }
        return layout;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_built)
        {
            Rebuild();
        }
        _cell = 0;
        for (var d = 0; d < 10; d++)
        {
            _cell = Math.Max(_cell, Digit(d).WidthIncludingTrailingWhitespace);
        }
        var width = 0.0;
        foreach (var column in _columns)
        {
            width += column.IsDigit ? _cell : Glyph(column.Text).WidthIncludingTrailingWhitespace;
        }
        return new Size(Math.Ceiling(width), LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var height = Bounds.Height;
        var pitch = FontSize * 1.25;
        using var clip = context.PushClip(new Rect(Bounds.Size));
        var x = 0.0;
        foreach (var column in _columns)
        {
            if (!column.IsDigit)
            {
                var glyph = Glyph(column.Text);
                glyph.Draw(context, new Point(x, (height - glyph.Height) / 2));
                x += glyph.WidthIncludingTrailingWhitespace;
                continue;
            }
            var position = column.Position?.Value ?? column.Digit;
            var low = (int)Math.Floor(position);
            for (var d = low; d <= low + 1; d++)
            {
                if (d is < 0 or > 9)
                {
                    continue;
                }
                var offset = (d - position) * pitch;
                if (Math.Abs(offset) >= height)
                {
                    continue;
                }
                var layout = Digit(d);
                layout.Draw(context, new Point(x + (_cell - layout.WidthIncludingTrailingWhitespace) / 2,
                    (height - layout.Height) / 2 + offset));
            }
            x += _cell;
        }
    }
}
