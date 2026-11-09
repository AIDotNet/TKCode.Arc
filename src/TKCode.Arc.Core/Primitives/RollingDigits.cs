using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc.Primitives;

/// <summary>How a digit fades as it rolls away from the reading line.</summary>
public enum RollingDigitsFalloff
{
    /// <summary>Opacity falls linearly with the distance (1 − d).</summary>
    Linear,

    /// <summary>Arc's number-field wheel: stays solid longer, then fades (1 − 1.1·d^1.5).</summary>
    Eased,

    /// <summary>
    /// No fade: glyphs stay solid and are only clipped to the line box, like a CSS digit strip with
    /// <c>overflow: hidden</c> (Arc's Showpiece panel counters).
    /// </summary>
    None
}

/// <summary>
/// P14 odometer: every digit is a wheel of ten glyphs keyed by place value (999 → 1,000 keeps the ones column),
/// turning in the direction the whole number moved. New columns grow in on the morph spring; changes ride
/// <see cref="WheelSpring"/> (smooth) or, when set, <see cref="WheelMotion"/> (any spring or timed ease); the first
/// reveal (when <see cref="IsArmed"/> turns false) staggers per digit. Drawn in
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

    /// <summary>Spring for changes after the first reveal (Arc's counters use smooth; its number field uses snappy).</summary>
    public static readonly StyledProperty<ArcSpring> WheelSpringProperty =
        AvaloniaProperty.Register<RollingDigits, ArcSpring>(nameof(WheelSpring), ArcSpring.Smooth);

    /// <summary>
    /// Motion for changes after the first reveal when the presets are not enough: any spring
    /// (<c>MotionSpec.Spring(SpringSpec.FromVisual(0.28, 0))</c>) or a timed ease
    /// (<c>MotionSpec.Tween(TimeSpan.FromMilliseconds(240), easing)</c>). Null (the default) uses <see cref="WheelSpring"/>.
    /// Reduced and Off motion still jump.
    /// </summary>
    public static readonly StyledProperty<MotionSpec?> WheelMotionProperty =
        AvaloniaProperty.Register<RollingDigits, MotionSpec?>(nameof(WheelMotion));

    /// <summary>When true each wheel turns the short way round (9 → 0 goes up by one) instead of following the value.</summary>
    public static readonly StyledProperty<bool> IsShortestTurnProperty =
        AvaloniaProperty.Register<RollingDigits, bool>(nameof(IsShortestTurn));

    /// <summary>Distance between neighbouring glyphs on a wheel, in em (Arc's number field spaces them 1.05em apart).</summary>
    public static readonly StyledProperty<double> GlyphPitchProperty =
        AvaloniaProperty.Register<RollingDigits, double>(nameof(GlyphPitch), 1.0);

    /// <summary>How rolling glyphs fade with their distance from the reading line.</summary>
    public static readonly StyledProperty<RollingDigitsFalloff> FalloffProperty =
        AvaloniaProperty.Register<RollingDigits, RollingDigitsFalloff>(nameof(Falloff));

    /// <summary>When true value changes jump without rolling (for example while the number is being typed).</summary>
    public static readonly StyledProperty<bool> IsInstantProperty =
        AvaloniaProperty.Register<RollingDigits, bool>(nameof(IsInstant));

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
    public ArcSpring WheelSpring { get => GetValue(WheelSpringProperty); set => SetValue(WheelSpringProperty, value); }

    /// <inheritdoc cref="WheelMotionProperty"/>
    public MotionSpec? WheelMotion { get => GetValue(WheelMotionProperty); set => SetValue(WheelMotionProperty, value); }

    public bool IsShortestTurn { get => GetValue(IsShortestTurnProperty); set => SetValue(IsShortestTurnProperty, value); }
    public double GlyphPitch { get => GetValue(GlyphPitchProperty); set => SetValue(GlyphPitchProperty, value); }
    public RollingDigitsFalloff Falloff { get => GetValue(FalloffProperty); set => SetValue(FalloffProperty, value); }
    public bool IsInstant { get => GetValue(IsInstantProperty); set => SetValue(IsInstantProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The plain text currently shown (for accessibility).</summary>
    public string DisplayText => _displayText ??= string.Concat(Prefix, _formatted, Suffix);

    // Joined when read: a value driven every frame rebuilds often and is read rarely.
    private string? _displayText = string.Empty;

    /// <summary>Wheel targets of the digit columns, most significant first (tests: which way the wheels turned).</summary>
    internal IReadOnlyList<double> WheelTargets => _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Target).ToList();

    /// <summary>Where the digit wheels are drawn now, most significant first (tests: how the wheels move).</summary>
    internal IReadOnlyList<double> WheelPositions =>
        _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Position?.Value ?? c.Target).ToList();

    private string _formatted = string.Empty;
    private CultureInfo? _culture;
    private string? _cultureName;
    private string? _format;
    private int _formatDecimals;
    private bool _formatGrouping;

    /// <summary>
    /// What a column stands for across rebuilds: an integer digit by place value, a fraction digit by position, the
    /// decimal mark, a group separator by the place it follows, or any other character by position and text.
    /// </summary>
    private readonly record struct ColumnKey(char Kind, int Index, char Text);

    private sealed class Column
    {
        public required ColumnKey Key { get; init; }
        public bool IsDigit { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Digit { get; set; }
        public double Target { get; set; }
        public bool Revealed { get; set; }
        public int Order { get; set; }
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public bool Leaving { get; set; }
        /// <summary>Matched by the rebuild in progress.</summary>
        public bool Claimed { get; set; }
    }

    private List<Column> _columns = new();
    private List<Column> _nextColumns = new();
    // Glyphs are laid-out TextLayouts whose glyph runs are drawn in Foreground: Avalonia 12's FormattedText.Draw breaks
    // and shapes its text again on every draw (1–2 KB of garbage per glyph, up to three glyphs per wheel per frame while
    // a number rolls), a TextLayout keeps its lines.
    private readonly TextLayout?[] _digitGlyphs = new TextLayout?[10];
    private readonly Dictionary<string, TextLayout> _textGlyphs = new(StringComparer.Ordinal);
    private double _cellWidth;
    private double _lineHeight;
    private double _previousValue = double.NaN;
    private int _direction = 1;

    static RollingDigits()
    {
        AffectsMeasure<RollingDigits>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, PrefixProperty, SuffixProperty);
        AffectsRender<RollingDigits>(ForegroundProperty, GlyphPitchProperty, FalloffProperty);
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
            _displayText = null;
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty)
        {
            // Glyphs are drawn in the current Foreground, so only font changes lay them out again.
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

        // A rebuild runs on every value change (every frame for a driven counter), so it allocates only the formatted
        // string and the columns that are new: the culture, the format string and the column list are kept, and the
        // characters are classified in place instead of split into parts.
        var cultureName = CultureName;
        if (_culture is null || !string.Equals(_cultureName, cultureName, StringComparison.Ordinal))
        {
            _culture = CultureInfo.GetCultureInfo(cultureName);
            _cultureName = cultureName;
        }
        var culture = _culture;
        var decimals = Math.Max(0, Decimals);
        var grouping = UseGrouping;
        if (_format is null || _formatDecimals != decimals || _formatGrouping != grouping)
        {
            _format = (grouping ? "N" : "F") + decimals.ToString(CultureInfo.InvariantCulture);
            _formatDecimals = decimals;
            _formatGrouping = grouping;
        }
        var formatted = value.ToString(_format, culture);
        _formatted = formatted;
        _displayText = null;
        var numberFormat = culture.NumberFormat;

        var integerDigits = 0;
        var seenDecimal = false;
        for (var i = 0; i < formatted.Length; i++)
        {
            var kind = ArcFormatting.Classify(formatted[i], numberFormat);
            if (kind == FormattedPartKind.Decimal)
            {
                seenDecimal = true;
            }
            else if (kind == FormattedPartKind.Digit && !seenDecimal)
            {
                integerDigits++;
            }
        }

        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && !IsInstant && level == ArcMotionLevel.Full;
        var reduced = level != ArcMotionLevel.Full;
        var structureChanged = false;
        var columns = _columns;
        foreach (var column in columns)
        {
            column.Claimed = false;
        }
        var next = _nextColumns;
        next.Clear();

        var place = integerDigits;
        var fraction = 0;
        var order = 0;
        seenDecimal = false;
        for (var i = 0; i < formatted.Length; i++)
        {
            var c = formatted[i];
            var kind = ArcFormatting.Classify(c, numberFormat);
            ColumnKey key;
            var isDigit = false;
            var digit = 0;
            var columnOrder = 0;
            switch (kind)
            {
                case FormattedPartKind.Digit when !seenDecimal:
                    key = new ColumnKey('i', --place, '\0');
                    isDigit = true;
                    digit = c - '0';
                    columnOrder = order++;
                    break;
                case FormattedPartKind.Digit:
                    key = new ColumnKey('f', fraction++, '\0');
                    isDigit = true;
                    digit = c - '0';
                    columnOrder = order++;
                    break;
                case FormattedPartKind.Decimal:
                    seenDecimal = true;
                    key = new ColumnKey('d', 0, '\0');
                    break;
                case FormattedPartKind.Group:
                    key = new ColumnKey('g', place, '\0');
                    break;
                default:
                    key = new ColumnKey('l', next.Count, c);
                    break;
            }
            var existing = FindUnclaimed(columns, key);
            if (existing is not null)
            {
                existing.Claimed = true;
                existing.Order = columnOrder;
                if (existing.IsDigit)
                {
                    if (existing.Digit != digit)
                    {
                        TurnTo(existing, digit, reduced);
                    }
                }
                else if (existing.Text.Length != 1 || existing.Text[0] != c)
                {
                    // A separator keeps its column when the culture changes its character.
                    existing.Text = c.ToString();
                }
                next.Add(existing);
            }
            else
            {
                structureChanged = true;
                var column = new Column
                {
                    Key = key,
                    IsDigit = isDigit,
                    Text = isDigit ? string.Empty : c.ToString(),
                    Digit = digit,
                    Order = columnOrder,
                    Claimed = true,
                    Revealed = !IsArmed
                };
                column.Target = IsArmed ? 0 : digit;
                column.Position = CreateValue(column.Target, invalidateMeasure: false);
                column.Presence = CreateValue(animate ? 0 : 1, invalidateMeasure: true);
                if (animate)
                {
                    _ = column.Presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
                }
                next.Add(column);
            }
        }
        // Columns that disappeared shrink away, then are removed.
        for (var index = 0; index < columns.Count; index++)
        {
            var gone = columns[index];
            if (gone.Leaving || gone.Claimed)
            {
                continue;
            }
            structureChanged = true;
            gone.Leaving = true;
            if (animate && gone.Presence is not null)
            {
                next.Insert(Math.Min(index, next.Count), gone);
                _ = RemoveWhenGone(gone, gone.Presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph)));
            }
        }
        _nextColumns = columns;
        _columns = next;
        // Digits are tabular: turning wheels never change the width, so only a change of columns needs a layout pass.
        if (structureChanged)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private static Column? FindUnclaimed(List<Column> columns, ColumnKey key)
    {
        // A number has a dozen columns at most: a scan beats building a dictionary per rebuild.
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (!column.Leaving && !column.Claimed && column.Key == key)
            {
                return column;
            }
        }
        return null;
    }

    /// <summary>Drops a leaving column once its shrink ran (the value completes its task on the UI thread).</summary>
    private async Task RemoveWhenGone(Column column, Task<bool> shrink)
    {
        await shrink;
        _columns.Remove(column);
        InvalidateMeasure();
    }

    private void TurnTo(Column column, int digit, bool reduced)
    {
        var delta = _direction < 0 && column.Revealed
            ? -((column.Digit - digit + 10) % 10)
            : (digit - column.Digit + 10) % 10;
        if (IsShortestTurn && Math.Abs(delta) > 5)
        {
            delta -= Math.Sign(delta) * 10;
        }
        column.Target += delta;
        column.Digit = digit;
        if (IsArmed)
        {
            return;
        }
        if (reduced || !IsLoaded || IsInstant)
        {
            column.Position?.Jump(column.Target);
        }
        else
        {
            var spec = column.Revealed
                ? WheelMotion ?? MotionSpec.Spring(WheelSpring)
                : MotionSpec.Spring(SpringSpec.FromVisual(0.48, 0), TimeSpan.FromSeconds(Math.Min(column.Order * 0.035, 0.25)));
            _ = column.Position?.AnimateTo(column.Target, spec);
        }
        column.Revealed = true;
    }

    private void Reveal()
    {
        var reduced = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
        foreach (var column in _columns)
        {
            if (!column.IsDigit || column.Revealed)
            {
                continue;
            }
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

    /// <summary>Opacity of a glyph <paramref name="distance"/> wheel steps (0–1) from the reading line.</summary>
    internal static double GlyphOpacity(RollingDigitsFalloff falloff, double distance) => falloff switch
    {
        RollingDigitsFalloff.Eased => Math.Max(0, 1 - Math.Pow(distance, 1.5) * 1.1),
        RollingDigitsFalloff.None => 1,
        _ => 1 - distance
    };

    /// <summary>
    /// How many wheel steps from the reading line a glyph can still be seen: one step when glyphs fade out; without
    /// fade, as far as the line box shows (more than one step when the pitch is tighter than the line).
    /// </summary>
    internal static double GlyphReach(RollingDigitsFalloff falloff, double lineHeight, double pitch)
        => falloff == RollingDigitsFalloff.None && pitch > 0 ? Math.Max(1, lineHeight / pitch) : 1;

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
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        for (var d = 0; d < 10; d++)
        {
            _digitGlyphs[d] = new TextLayout(d.ToString(CultureInfo.InvariantCulture), typeface, FontSize, Brushes.Black);
        }
        // Tabular cells: every digit occupies the width of "0".
        _cellWidth = _digitGlyphs.Max(g => g!.WidthIncludingTrailingWhitespace);
        _lineHeight = _digitGlyphs[0]!.Height;
    }

    private TextLayout TextGlyph(string text)
    {
        if (!_textGlyphs.TryGetValue(text, out var glyph))
        {
            glyph = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Brushes.Black);
            _textGlyphs[text] = glyph;
        }
        return glyph;
    }

    /// <summary>
    /// Draws a glyph's runs in <paramref name="brush"/> with its top left at <paramref name="origin"/>: the calls
    /// <see cref="TextLayout.Draw"/> makes, with the brush given here instead of the one the glyph was laid out with.
    /// </summary>
    private static void DrawGlyph(DrawingContext context, TextLayout glyph, IBrush brush, Point origin)
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
        var brush = Foreground ?? Brushes.Black;
        var x = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            var prefix = TextGlyph(Prefix);
            DrawGlyph(context, prefix, brush, new Point(x, 0));
            x += prefix.WidthIncludingTrailingWhitespace;
        }
        var feather = FontSize * 0.16;
        var pitch = FontSize * GlyphPitch;
        var falloff = Falloff;
        var reach = GlyphReach(falloff, _lineHeight, pitch);
        foreach (var column in _columns)
        {
            var width = ColumnWidth(column);
            var presence = column.Presence?.Value ?? 1;
            if (width <= 0.01)
            {
                continue;
            }
            // Fading wheels feather past the line box; a wheel without fade is cut at the line box (overflow: hidden).
            var clip = falloff == RollingDigitsFalloff.None
                ? new Rect(x, 0, width, _lineHeight)
                : new Rect(x, -feather, width, _lineHeight + 2 * feather);
            using (context.PushClip(clip))
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
                        if (abs >= reach)
                        {
                            continue;
                        }
                        var glyph = _digitGlyphs[d]!;
                        var gx = cellX + (_cellWidth - glyph.WidthIncludingTrailingWhitespace) / 2;
                        var opacity = GlyphOpacity(falloff, abs);
                        using (context.PushOpacity(opacity))
                        {
                            DrawGlyph(context, glyph, brush, new Point(gx, offset * pitch));
                        }
                    }
                }
                else
                {
                    DrawGlyph(context, TextGlyph(column.Text), brush, new Point(x, 0));
                }
            }
            x += width;
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            DrawGlyph(context, TextGlyph(Suffix), brush, new Point(x, 0));
        }
    }
}
