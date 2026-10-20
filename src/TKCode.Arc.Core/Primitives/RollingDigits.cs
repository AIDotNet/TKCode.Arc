using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P14 odometer: every digit is a wheel of ten glyphs keyed by place value (999 → 1,000 keeps the ones column),
/// turning in the direction the whole number moved. New columns grow in on the morph spring; changes ride the
/// smooth spring; the first reveal (when <see cref="IsArmed"/> turns false) staggers per digit. Drawn in
/// <see cref="Render"/> with cached glyphs: no per-digit controls, no layout during the roll.
/// </summary>
public class RollingDigits : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RollingDigits, double>(nameof(Value));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<RollingDigits, int>(nameof(Decimals));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<RollingDigits, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<RollingDigits, string?>(nameof(Suffix));

    /// <summary>Formatting culture name (Arc formats with en-US by default).</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<RollingDigits, string>(nameof(CultureName), "en-US");

    /// <summary>When true the wheels rest at zero; setting it to false plays the first reveal.</summary>
    public static readonly StyledProperty<bool> IsArmedProperty =
        AvaloniaProperty.Register<RollingDigits, bool>(nameof(IsArmed));

    /// <summary>When false, digits are formatted without group separators.</summary>
    public static readonly StyledProperty<bool> UseGroupingProperty =
        AvaloniaProperty.Register<RollingDigits, bool>(nameof(UseGrouping), true);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<RollingDigits>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<RollingDigits>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<RollingDigits>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<RollingDigits>();

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }
    public bool IsArmed { get => GetValue(IsArmedProperty); set => SetValue(IsArmedProperty, value); }
    public bool UseGrouping { get => GetValue(UseGroupingProperty); set => SetValue(UseGroupingProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The plain text currently shown (for accessibility).</summary>
    public string DisplayText { get; private set; } = string.Empty;

    private string _formatted = string.Empty;

    private sealed class Column
    {
        public required string Key { get; init; }
        public bool IsDigit { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Digit { get; set; }
        public double Target { get; set; }
        public bool Revealed { get; set; }
        public int Order { get; set; }
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public bool Leaving { get; set; }
    }

    private readonly List<Column> _columns = new();
    private readonly FormattedText?[] _digitGlyphs = new FormattedText?[10];
    private readonly Dictionary<string, FormattedText> _textGlyphs = new(StringComparer.Ordinal);
    private double _cellWidth;
    private double _lineHeight;
    private double _previousValue = double.NaN;
    private int _direction = 1;

    static RollingDigits()
    {
        AffectsMeasure<RollingDigits>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, PrefixProperty, SuffixProperty);
        AffectsRender<RollingDigits>(ForegroundProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == DecimalsProperty ||
            change.Property == CultureNameProperty || change.Property == UseGroupingProperty)
        {
            Rebuild();
        }
        else if (change.Property == IsArmedProperty && !IsArmed)
        {
            Reveal();
        }
        else if (change.Property == PrefixProperty || change.Property == SuffixProperty)
        {
            DisplayText = $"{Prefix}{_formatted}{Suffix}";
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            Array.Clear(_digitGlyphs);
            _textGlyphs.Clear();
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

    private void Rebuild()
    {
        var value = Value;
        if (!double.IsNaN(_previousValue) && value != _previousValue)
        {
            _direction = value > _previousValue ? 1 : -1;
        }
        _previousValue = value;

        var culture = CultureInfo.GetCultureInfo(CultureName);
        var format = (UseGrouping ? "N" : "F") + Math.Max(0, Decimals);
        var formatted = value.ToString(format, culture);
        _formatted = formatted;
        DisplayText = $"{Prefix}{formatted}{Suffix}";
        var parts = ArcFormatting.ToParts(formatted, culture);

        var integerDigits = 0;
        var seenDecimal = false;
        foreach (var part in parts)
        {
            if (part.Kind == FormattedPartKind.Decimal)
            {
                seenDecimal = true;
            }
            else if (part.Kind == FormattedPartKind.Digit && !seenDecimal)
            {
                integerDigits++;
            }
        }

        var place = integerDigits;
        var fraction = 0;
        var order = 0;
        seenDecimal = false;
        var wanted = new List<Column>(parts.Count);
        foreach (var part in parts)
        {
            switch (part.Kind)
            {
                case FormattedPartKind.Digit when !seenDecimal:
                    wanted.Add(new Column { Key = $"i{--place}", IsDigit = true, Digit = part.Text[0] - '0', Order = order++ });
                    break;
                case FormattedPartKind.Digit:
                    wanted.Add(new Column { Key = $"f{fraction++}", IsDigit = true, Digit = part.Text[0] - '0', Order = order++ });
                    break;
                case FormattedPartKind.Decimal:
                    seenDecimal = true;
                    wanted.Add(new Column { Key = "d", Text = part.Text });
                    break;
                case FormattedPartKind.Group:
                    wanted.Add(new Column { Key = $"g{place}", Text = part.Text });
                    break;
                default:
                    wanted.Add(new Column { Key = $"l{wanted.Count}{part.Text}", Text = part.Text });
                    break;
            }
        }

        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var reduced = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
        var existing = _columns.Where(c => !c.Leaving).ToDictionary(c => c.Key);
        var next = new List<Column>(wanted.Count);
        foreach (var w in wanted)
        {
            if (existing.Remove(w.Key, out var column))
            {
                column.Order = w.Order;
                column.Text = w.Text;
                if (column.IsDigit && column.Digit != w.Digit)
                {
                    TurnTo(column, w.Digit, reduced);
                }
                next.Add(column);
            }
            else
            {
                w.Revealed = !IsArmed;
                w.Target = IsArmed ? 0 : w.Digit;
                w.Position = CreateValue(w.Target, invalidateMeasure: false);
                w.Presence = CreateValue(animate ? 0 : 1, invalidateMeasure: true);
                if (animate)
                {
                    _ = w.Presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                }
                next.Add(w);
            }
        }
        // Columns that disappeared shrink away, then are removed.
        foreach (var gone in existing.Values)
        {
            gone.Leaving = true;
            var index = _columns.IndexOf(gone);
            next.Insert(Math.Min(index, next.Count), gone);
            if (animate && gone.Presence is not null)
            {
                var leaving = gone;
                gone.Presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph)).ContinueWith(_ =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _columns.Remove(leaving);
                        InvalidateMeasure();
                    }), TaskScheduler.Default);
            }
            else
            {
                next.Remove(gone);
            }
        }
        _columns.Clear();
        _columns.AddRange(next);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void TurnTo(Column column, int digit, bool reduced)
    {
        var delta = _direction < 0 && column.Revealed
            ? -((column.Digit - digit + 10) % 10)
            : (digit - column.Digit + 10) % 10;
        column.Target += delta;
        column.Digit = digit;
        if (IsArmed)
        {
            return;
        }
        if (reduced || !IsLoaded)
        {
            column.Position?.Jump(column.Target);
        }
        else
        {
            var spec = column.Revealed
                ? MotionSpec.Spring(ArcSpring.Smooth)
                : MotionSpec.Spring(SpringSpec.FromVisual(0.48, 0), TimeSpan.FromSeconds(Math.Min(column.Order * 0.035, 0.25)));
            _ = column.Position?.AnimateTo(column.Target, spec);
        }
        column.Revealed = true;
    }

    private void Reveal()
    {
        var reduced = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
        foreach (var column in _columns.Where(c => c.IsDigit && !c.Revealed))
        {
            column.Target = column.Digit;
            if (reduced || !IsLoaded)
            {
                column.Position?.Jump(column.Target);
            }
            else
            {
                var delay = TimeSpan.FromSeconds(Math.Min(column.Order * 0.035, 0.25));
                _ = column.Position?.AnimateTo(column.Target, MotionSpec.Spring(SpringSpec.FromVisual(0.48, 0), delay));
            }
            column.Revealed = true;
        }
    }

    private MotionValue<double> CreateValue(double initial, bool invalidateMeasure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        if (invalidateMeasure)
        {
            value.Changed += _ => InvalidateMeasure();
        }
        else
        {
            value.Changed += _ => InvalidateVisual();
        }
        return value;
    }

    private void EnsureGlyphs()
    {
        if (_digitGlyphs[0] is not null)
        {
            return;
        }
        var brush = Foreground ?? Brushes.Black;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        for (var d = 0; d < 10; d++)
        {
            _digitGlyphs[d] = new FormattedText(d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, FontSize, brush);
        }
        // Tabular cells: every digit occupies the width of "0".
        _cellWidth = _digitGlyphs.Max(g => g!.WidthIncludingTrailingWhitespace);
        _lineHeight = _digitGlyphs[0]!.Height;
    }

    private FormattedText TextGlyph(string text)
    {
        if (!_textGlyphs.TryGetValue(text, out var glyph))
        {
            glyph = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black);
            _textGlyphs[text] = glyph;
        }
        return glyph;
    }

    private double ColumnWidth(Column column)
    {
        var width = column.IsDigit ? _cellWidth : TextGlyph(column.Text).WidthIncludingTrailingWhitespace;
        return width * (column.Presence?.Value ?? 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGlyphs();
        var width = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            width += TextGlyph(Prefix).WidthIncludingTrailingWhitespace;
        }
        foreach (var column in _columns)
        {
            width += ColumnWidth(column);
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            width += TextGlyph(Suffix).WidthIncludingTrailingWhitespace;
        }
        return new Size(width, _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        EnsureGlyphs();
        var x = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            var prefix = TextGlyph(Prefix);
            context.DrawText(prefix, new Point(x, 0));
            x += prefix.WidthIncludingTrailingWhitespace;
        }
        var feather = FontSize * 0.16;
        foreach (var column in _columns)
        {
            var width = ColumnWidth(column);
            var presence = column.Presence?.Value ?? 1;
            if (width <= 0.01)
            {
                continue;
            }
            using (context.PushClip(new Rect(x, -feather, width, _lineHeight + 2 * feather)))
            using (context.PushOpacity(Math.Clamp(presence, 0, 1)))
            {
                if (column.IsDigit)
                {
                    var position = column.Position?.Value ?? column.Digit;
                    var cellX = x + (width - _cellWidth) / 2;
                    for (var d = 0; d < 10; d++)
                    {
                        var offset = ((((d - position) % 10) + 15) % 10) - 5;
                        var abs = Math.Abs(offset);
                        if (abs >= 1)
                        {
                            continue;
                        }
                        var glyph = _digitGlyphs[d]!;
                        var gx = cellX + (_cellWidth - glyph.WidthIncludingTrailingWhitespace) / 2;
                        using (context.PushOpacity(1 - abs))
                        {
                            context.DrawText(glyph, new Point(gx, offset * FontSize));
                        }
                    }
                }
                else
                {
                    context.DrawText(TextGlyph(column.Text), new Point(x, 0));
                }
            }
            x += width;
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            context.DrawText(TextGlyph(Suffix), new Point(x, 0));
        }
    }
}
