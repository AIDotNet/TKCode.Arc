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
/// The rolling figures of <see cref="CreditTopUp"/> (Arc Credit top-up, design reference): every digit is a 0–9 strip cut
/// at the line box and keyed by place value, all strips start together on one timed curve, and each turns one way only.
/// With <see cref="Laps"/> (the balance, 900ms) the highest place that changes takes the short count and every place
/// below it one extra lap, so a purchase reads like a counter; without (the checkout figures, 240ms) every place takes
/// the plain count. New leading places do not roll: they show their digit and open their width on the Quick spring
/// while fading in over 100ms; places that go away close the same way. A change that lands while a roll is running
/// re-plans from where each strip is drawn, and a place that comes back while closing reopens: nothing can be left
/// half open.
/// </summary>
/// <remarks>
/// Core's <c>RollingDigits</c> cannot do this yet (no lap rule, new places roll in, presence rides the Morph spring and
/// drives width and opacity together). Candidate for a shared helper: a lap mode and a "new places open, not roll" option
/// on <c>RollingDigits</c>. Motion is code-driven because each strip's travel is measured from the digits (+6, +10, +15)
/// and a ControlTheme transition cannot express per-place targets.
/// </remarks>
internal sealed class CreditTopUpOdometer : Control
{
    public static readonly StyledProperty<long> ValueProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, long>(nameof(Value));

    /// <summary>Minor-unit digits: <see cref="Value"/> is in hundredths when 2 (a price of 48.00 is 4800).</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, int>(nameof(Decimals));

    public static readonly StyledProperty<CultureInfo> CultureProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, CultureInfo>(nameof(Culture), CultureInfo.GetCultureInfo("en-US"));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, string?>(nameof(Suffix));

    /// <summary>A trailing word in its own size and color, on the figure's baseline ("credits" after the balance).</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, string?>(nameof(Unit));

    public static readonly StyledProperty<double> UnitFontSizeProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, double>(nameof(UnitFontSize), 14);

    public static readonly StyledProperty<IBrush?> UnitForegroundProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, IBrush?>(nameof(UnitForeground));

    public static readonly StyledProperty<double> UnitGapProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, double>(nameof(UnitGap), 8);

    /// <summary>Whether places below the highest change take an extra lap (the balance).</summary>
    public static readonly StyledProperty<bool> LapsProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, bool>(nameof(Laps));

    /// <summary>How long a roll takes (900ms for the balance, 240ms for the checkout figures), on Arc's standard curve.</summary>
    public static readonly StyledProperty<TimeSpan> RollDurationProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, TimeSpan>(nameof(RollDuration), TimeSpan.FromMilliseconds(240));

    /// <summary>The line box (and strip pitch); NaN uses the font's own line height.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, double>(nameof(LineHeight), double.NaN);

    /// <summary>Extra space after every glyph (CSS letter-spacing).</summary>
    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<CreditTopUpOdometer, double>(nameof(LetterSpacing));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<CreditTopUpOdometer>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<CreditTopUpOdometer>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<CreditTopUpOdometer>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<CreditTopUpOdometer>();

    /// <summary>The width spring of places that open or close (Arc: ω ≈ 22, ζ 1).</summary>
    internal static readonly SpringSpec PresenceSpring = ArcMotionPresets.Quick;

    /// <summary>The fade of places that open or close.</summary>
    internal static readonly TimeSpan PresenceFade = TimeSpan.FromMilliseconds(100);

    private static readonly Avalonia.Animation.Easings.Easing s_fadeEase = new Avalonia.Animation.Easings.CubicEaseOut();

    public long Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public CultureInfo Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }
    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double UnitFontSize { get => GetValue(UnitFontSizeProperty); set => SetValue(UnitFontSizeProperty, value); }
    public IBrush? UnitForeground { get => GetValue(UnitForegroundProperty); set => SetValue(UnitForegroundProperty, value); }
    public double UnitGap { get => GetValue(UnitGapProperty); set => SetValue(UnitGapProperty, value); }
    public bool Laps { get => GetValue(LapsProperty); set => SetValue(LapsProperty, value); }
    public TimeSpan RollDuration { get => GetValue(RollDurationProperty); set => SetValue(RollDurationProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The figure as text, final value (what screen readers get).</summary>
    public string DisplayText { get; private set; } = string.Empty;

    private sealed class Column
    {
        public required string Key { get; init; }
        public bool IsDigit { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Digit { get; set; }
        public double Target { get; set; }
        public MotionValue<double>? Position { get; set; }
        public MotionValue<double>? Width { get; set; }
        public MotionValue<double>? Fade { get; set; }
        public bool Leaving { get; set; }
    }

    private readonly List<Column> _columns = new();
    private readonly TextLayout?[] _digits = new TextLayout?[10];
    private readonly Dictionary<string, TextLayout> _texts = new(StringComparer.Ordinal);
    private TextLayout? _unitLayout;
    private double _cell;
    private long _shown;
    private bool _built;

    static CreditTopUpOdometer()
    {
        AffectsMeasure<CreditTopUpOdometer>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, PrefixProperty, SuffixProperty,
            UnitProperty, UnitFontSizeProperty, UnitGapProperty, LineHeightProperty, LetterSpacingProperty);
        AffectsRender<CreditTopUpOdometer>(ForegroundProperty, UnitForegroundProperty);
    }

    /// <summary>Wheel targets of the digit places, most significant first (tests: how far each place turns).</summary>
    internal IReadOnlyList<double> Targets => _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Target).ToList();

    /// <summary>Where the digit strips are drawn now, most significant first.</summary>
    internal IReadOnlyList<double> Positions =>
        _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Position?.Value ?? c.Target).ToList();

    /// <summary>How open each place is (0–1), leaving places included, most significant first.</summary>
    internal IReadOnlyList<double> Openness => _columns.Select(c => c.Width?.Value ?? 1).ToList();

    /// <summary>Whether a place is still opening, closing or rolling.</summary>
    internal bool IsMoving => _columns.Any(c => c.Leaving || (c.Position?.IsAnimating ?? false) || (c.Width?.IsAnimating ?? false));

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
            ClearGlyphs();
        }
        else if (change.Property == UnitProperty || change.Property == UnitFontSizeProperty ||
                 change.Property == UnitForegroundProperty)
        {
            _unitLayout?.Dispose();
            _unitLayout = null;
            UpdateName();
        }
        else if (change.Property == PrefixProperty || change.Property == SuffixProperty)
        {
            UpdateName();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_built)
        {
            Rebuild();
        }
        else
        {
            Settle();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Motion stops with the visual: land every place where it was going so a later attach starts clean.
        Settle();
    }

    /// <summary>Jumps every strip and every opening or closing place to its end state.</summary>
    internal void Settle()
    {
        _columns.RemoveAll(c => c.Leaving);
        foreach (var column in _columns)
        {
            column.Position?.Jump(column.Target);
            column.Width?.Jump(1);
            column.Fade?.Jump(1);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ClearGlyphs()
    {
        for (var i = 0; i < _digits.Length; i++)
        {
            _digits[i]?.Dispose();
            _digits[i] = null;
        }
        foreach (var layout in _texts.Values)
        {
            layout.Dispose();
        }
        _texts.Clear();
        _unitLayout?.Dispose();
        _unitLayout = null;
    }

    /// <summary>
    /// The columns of a value: digits keyed by place ("i3", "f0"), group separators keyed by the place on their left
    /// ("g3"), the decimal separator "d".
    /// </summary>
    internal static List<(string Key, bool IsDigit, string Text, int Digit)> Layout(long value, int decimals, CultureInfo culture)
    {
        value = Math.Max(0, value);
        var scale = 1L;
        for (var i = 0; i < decimals; i++)
        {
            scale *= 10;
        }
        var whole = value / scale;
        var fraction = value % scale;
        var integer = whole.ToString("N0", culture);
        var result = new List<(string, bool, string, int)>(integer.Length + decimals + 1);
        var digitCount = integer.Count(char.IsDigit);
        var place = digitCount;
        var i0 = 0;
        while (i0 < integer.Length)
        {
            var ch = integer[i0];
            if (char.IsDigit(ch))
            {
                place--;
                result.Add(($"i{place}", true, string.Empty, ch - '0'));
                i0++;
            }
            else
            {
                // A separator may be longer than one char (and may be a no-break space).
                var start = i0;
                while (i0 < integer.Length && !char.IsDigit(integer[i0]))
                {
                    i0++;
                }
                result.Add(($"g{place}", false, integer[start..i0], 0));
            }
        }
        if (decimals > 0)
        {
            result.Add(("d", false, culture.NumberFormat.NumberDecimalSeparator, 0));
            var text = fraction.ToString(new string('0', decimals), CultureInfo.InvariantCulture);
            for (var f = 0; f < decimals; f++)
            {
                result.Add(($"f{f}", true, string.Empty, text[f] - '0'));
            }
        }
        return result;
    }

    /// <summary>Digits of a laid-out value, least significant place first (fractions are the lowest places).</summary>
    private static int[] PlaceDigits(List<(string Key, bool IsDigit, string Text, int Digit)> layout)
    {
        var digits = layout.Where(c => c.IsDigit).Select(c => c.Digit).ToArray();
        Array.Reverse(digits);
        return digits;
    }

    private void Rebuild()
    {
        var value = Math.Max(0, Value);
        var culture = Culture ?? CultureInfo.InvariantCulture;
        var wanted = Layout(value, Math.Max(0, Decimals), culture);
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = _built && IsLoaded && level == ArcMotionLevel.Full;
        var growing = value > _shown;

        // Plan the turns on the places both numbers share, from each strip's own current digit.
        var live = _columns.Where(c => !c.Leaving).ToDictionary(c => c.Key);
        var oldDigits = _columns.Where(c => c.IsDigit && !c.Leaving).Select(c => c.Digit).Reverse().ToArray();
        var newDigits = PlaceDigits(wanted);
        var turns = _built && value != _shown
            ? CreditTopUpMath.PlanTurns(oldDigits, newDigits, growing, Laps)
            : new int[Math.Min(oldDigits.Length, newDigits.Length)];
        var digitPlaces = wanted.Count(w => w.IsDigit);

        var next = new List<Column>(wanted.Count + 2);
        var structure = false;
        var digitIndex = 0;
        foreach (var w in wanted)
        {
            var place = w.IsDigit ? digitPlaces - 1 - digitIndex++ : -1;
            if (live.Remove(w.Key, out var column))
            {
                column.Text = w.Text;
                if (column.IsDigit && place >= 0 && place < turns.Length)
                {
                    var turn = turns[place];
                    column.Digit = w.Digit;
                    if (!animate)
                    {
                        // Nothing rolls: the strip lands on its digit (no laps to unwind later).
                        column.Target = Nearest(column.Target, w.Digit);
                        Move(column, false);
                    }
                    else if (turn != 0)
                    {
                        column.Target += turn;
                        Move(column, true);
                    }
                }
                else if (column.IsDigit && column.Digit != w.Digit)
                {
                    column.Digit = w.Digit;
                    column.Target = Nearest(column.Target, w.Digit);
                    Move(column, animate);
                }
                next.Add(column);
                continue;
            }
            structure = true;
            // A place that is closing and comes back reopens where it is instead of being doubled.
            var revived = _columns.FirstOrDefault(c => c.Leaving && c.Key == w.Key);
            if (revived is not null)
            {
                revived.Leaving = false;
                revived.Text = w.Text;
                if (revived.IsDigit)
                {
                    revived.Digit = w.Digit;
                    revived.Target = Nearest(revived.Target, w.Digit);
                    revived.Position?.Jump(revived.Target);
                }
                Open(revived, animate);
                next.Add(revived);
                continue;
            }
            var created = new Column { Key = w.Key, IsDigit = w.IsDigit, Text = w.Text, Digit = w.Digit, Target = w.Digit };
            created.Position = CreateValue(created.Target, measure: false);
            created.Width = CreateValue(animate ? 0 : 1, measure: true);
            created.Fade = CreateValue(animate ? 0 : 1, measure: false);
            Open(created, animate);
            next.Add(created);
        }

        // Places that went away close, then leave; they keep their spot at the front of the figure.
        foreach (var gone in live.Values)
        {
            structure = true;
            gone.Leaving = true;
            Close(gone, animate);
        }
        // Closing places (now or from an earlier change) stay drawn in front of the figure until they have closed.
        var merged = new List<Column>(next.Count + 4);
        foreach (var column in _columns)
        {
            if (column.Leaving)
            {
                merged.Add(column);
            }
        }
        merged.AddRange(next);
        _columns.Clear();
        _columns.AddRange(merged);
        _shown = value;
        _built = true;
        UpdateName();
        if (structure)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    /// <summary>The strip position closest to <paramref name="from"/> that shows <paramref name="digit"/>.</summary>
    private static double Nearest(double from, int digit)
    {
        var baseTurn = Math.Floor(from / 10) * 10;
        var candidate = baseTurn + digit;
        if (candidate - from > 5)
        {
            candidate -= 10;
        }
        else if (from - candidate > 5)
        {
            candidate += 10;
        }
        return candidate;
    }

    private void Move(Column column, bool animate)
    {
        if (column.Position is null)
        {
            return;
        }
        if (animate)
        {
            _ = column.Position.AnimateTo(column.Target, MotionSpec.Tween(RollDuration, ArcEasings.Standard));
        }
        else
        {
            column.Position.Jump(column.Target);
        }
    }

    private void Open(Column column, bool animate)
    {
        if (animate)
        {
            _ = column.Width!.AnimateTo(1, MotionSpec.Spring(PresenceSpring));
            _ = column.Fade!.AnimateTo(1, MotionSpec.Tween(PresenceFade, s_fadeEase));
        }
        else
        {
            column.Width!.Jump(1);
            column.Fade!.Jump(1);
        }
    }

    private void Close(Column column, bool animate)
    {
        if (!animate || column.Width is null)
        {
            _columns.Remove(column);
            return;
        }
        _ = column.Fade!.AnimateTo(0, MotionSpec.Tween(PresenceFade, s_fadeEase));
        var closing = column;
        column.Width.AnimateTo(0, MotionSpec.Spring(PresenceSpring)).ContinueWith(done =>
        {
            if (done.Result && closing.Leaving)
            {
                _columns.Remove(closing);
                InvalidateMeasure();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private MotionValue<double> CreateValue(double initial, bool measure)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        if (measure)
        {
            value.Changed += _ => InvalidateMeasure();
        }
        else
        {
            value.Changed += _ => InvalidateVisual();
        }
        return value;
    }

    private void UpdateName()
    {
        var culture = Culture ?? CultureInfo.InvariantCulture;
        var text = string.Concat(Layout(Math.Max(0, Value), Math.Max(0, Decimals), culture)
            .Select(c => c.IsDigit ? ((char)('0' + c.Digit)).ToString() : c.Text));
        DisplayText = $"{Prefix}{text}{Suffix}";
        var name = string.IsNullOrEmpty(Unit) ? DisplayText : $"{DisplayText} {Unit}";
        AutomationProperties.SetName(this, name);
    }

    private Typeface Face => new(FontFamily, FontStyle.Normal, FontWeight);

    private void EnsureGlyphs()
    {
        if (_digits[0] is not null)
        {
            return;
        }
        var brush = Foreground ?? Brushes.Black;
        var face = Face;
        for (var d = 0; d < 10; d++)
        {
            _digits[d] = new TextLayout(d.ToString(CultureInfo.InvariantCulture), face, FontSize, brush);
        }
        _cell = _digits.Max(g => g!.WidthIncludingTrailingWhitespace);
    }

    private TextLayout Text(string text)
    {
        if (!_texts.TryGetValue(text, out var layout))
        {
            layout = new TextLayout(text, Face, FontSize, Foreground ?? Brushes.Black);
            _texts[text] = layout;
        }
        return layout;
    }

    private TextLayout? UnitLayout()
    {
        if (string.IsNullOrEmpty(Unit))
        {
            return null;
        }
        return _unitLayout ??= new TextLayout(Unit, new Typeface(FontFamily), UnitFontSize, UnitForeground ?? Foreground ?? Brushes.Black);
    }

    private double Line => double.IsNaN(LineHeight) || LineHeight <= 0 ? _digits[0]!.Height : LineHeight;

    private double BaseWidth(Column column)
        => (column.IsDigit ? _cell : Text(column.Text).WidthIncludingTrailingWhitespace) + LetterSpacing;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGlyphs();
        var width = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            width += Text(Prefix).WidthIncludingTrailingWhitespace + LetterSpacing;
        }
        foreach (var column in _columns)
        {
            width += BaseWidth(column) * Math.Clamp(column.Width?.Value ?? 1, 0, 1);
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            width += Text(Suffix).WidthIncludingTrailingWhitespace;
        }
        if (UnitLayout() is { } unit)
        {
            width += UnitGap + unit.WidthIncludingTrailingWhitespace;
        }
        return new Size(Math.Max(0, width), Line);
    }

    public override void Render(DrawingContext context)
    {
        EnsureGlyphs();
        var line = Line;
        var glyphHeight = _digits[0]!.Height;
        var top = (line - glyphHeight) / 2;
        var x = 0.0;
        if (!string.IsNullOrEmpty(Prefix))
        {
            var prefix = Text(Prefix);
            prefix.Draw(context, new Point(x, top));
            x += prefix.WidthIncludingTrailingWhitespace + LetterSpacing;
        }
        foreach (var column in _columns)
        {
            var open = Math.Clamp(column.Width?.Value ?? 1, 0, 1);
            var full = BaseWidth(column);
            var width = full * open;
            if (width <= 0.01)
            {
                continue;
            }
            var fade = Math.Clamp(column.Fade?.Value ?? 1, 0, 1);
            using (context.PushClip(new Rect(x, 0, width, line)))
            using (context.PushOpacity(fade))
            {
                // Content is centered in a place that is opening or closing (it grows from its middle).
                var cx = x + (width - full) / 2;
                if (column.IsDigit)
                {
                    var position = column.Position?.Value ?? column.Target;
                    var floor = Math.Floor(position);
                    var frac = position - floor;
                    var d0 = (int)(((floor % 10) + 10) % 10);
                    var g0 = _digits[d0]!;
                    g0.Draw(context, new Point(cx + (_cell - g0.WidthIncludingTrailingWhitespace) / 2, top - frac * line));
                    if (frac > 0.0001)
                    {
                        var g1 = _digits[(d0 + 1) % 10]!;
                        g1.Draw(context, new Point(cx + (_cell - g1.WidthIncludingTrailingWhitespace) / 2, top + (1 - frac) * line));
                    }
                }
                else
                {
                    Text(column.Text).Draw(context, new Point(cx, top));
                }
            }
            x += width;
        }
        if (!string.IsNullOrEmpty(Suffix))
        {
            var suffix = Text(Suffix);
            suffix.Draw(context, new Point(x, top));
            x += suffix.WidthIncludingTrailingWhitespace;
        }
        if (UnitLayout() is { } unit)
        {
            var baseline = top + _digits[0]!.Baseline;
            unit.Draw(context, new Point(x + UnitGap, baseline - unit.Baseline));
        }
    }
}
