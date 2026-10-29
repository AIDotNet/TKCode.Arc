using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a <see cref="ShowpieceNumber"/> formats its value (see <see cref="ShowpieceFormat"/>).</summary>
internal enum ShowpieceNumberFormat
{
    /// <summary>Grouped number with <see cref="ShowpieceNumber.Decimals"/> decimals: 1,687.</summary>
    Number,

    /// <summary>Grouped currency: $2,512,340.</summary>
    Currency,

    /// <summary>Compact currency: $6.05M, $870K, $990.</summary>
    CompactCurrency,

    /// <summary>The value is a fraction shown as a percentage: 0.69 → 69% (signed with <see cref="ShowpieceNumber.IsSignShown"/>).</summary>
    Percent
}

/// <summary>
/// A rolling panel number for the Showpieces: formats its value (compact currency, currency, grouped number,
/// percentage, or a custom <see cref="Formatter"/>), rolls every digit on its own Core <see cref="RollingDigits"/>
/// wheel and keeps every other character (symbols, separators, units, <see cref="Prefix"/> / <see cref="Suffix"/>)
/// still, swapping them when they change ($870K → $1.20M). When the value changes, each wheel starts
/// <see cref="Stagger"/> × its character index later (Arc: 26ms per character from the left, separators and units
/// included). Columns are keyed by place value, so 999 → 1,000 keeps the ones column, and each wheel is a 0–9 strip:
/// 9 → 0 rolls back through 8…1 like Arc's CSS strips. New and leaving characters grow and fade. Screen readers read
/// <see cref="DisplayText"/>, never the rolling digits.
/// </summary>
/// <remarks>
/// Code-driven motion: the stagger delays are scheduled on a Core <see cref="LiveLoop"/> (full motion only; otherwise,
/// or while the number is off screen, changes apply at once) because a per-character start offset on separate wheels
/// cannot be declared in a ControlTheme; the wheels themselves are <see cref="RollingDigits"/>.
/// </remarks>
internal sealed class ShowpieceNumber : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ShowpieceNumber, double>(nameof(Value));

    public static readonly StyledProperty<ShowpieceNumberFormat> FormatProperty =
        AvaloniaProperty.Register<ShowpieceNumber, ShowpieceNumberFormat>(nameof(Format));

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<ShowpieceNumber, int>(nameof(Decimals));

    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<ShowpieceNumber, string?>(nameof(Prefix));

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<ShowpieceNumber, string?>(nameof(Suffix));

    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ShowpieceNumber, string>(nameof(CurrencySymbol), "$");

    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<ShowpieceNumber, string>(nameof(CultureName), "en-US");

    public static readonly StyledProperty<bool> IsSignShownProperty =
        AvaloniaProperty.Register<ShowpieceNumber, bool>(nameof(IsSignShown));

    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<ShowpieceNumber, Func<double, string>?>(nameof(Formatter));

    public static readonly StyledProperty<TimeSpan> StaggerProperty =
        AvaloniaProperty.Register<ShowpieceNumber, TimeSpan>(nameof(Stagger), TimeSpan.FromMilliseconds(26));

    public static readonly StyledProperty<RollingDigitsFalloff> FalloffProperty =
        AvaloniaProperty.Register<ShowpieceNumber, RollingDigitsFalloff>(nameof(Falloff), RollingDigitsFalloff.None);

    public static readonly StyledProperty<double> GlyphPitchProperty =
        AvaloniaProperty.Register<ShowpieceNumber, double>(nameof(GlyphPitch), 1.2);

    public static readonly StyledProperty<ArcSpring> WheelSpringProperty =
        AvaloniaProperty.Register<ShowpieceNumber, ArcSpring>(nameof(WheelSpring), ArcSpring.Smooth);

    public static readonly StyledProperty<bool> IsInstantProperty =
        AvaloniaProperty.Register<ShowpieceNumber, bool>(nameof(IsInstant));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ShowpieceNumber>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ShowpieceNumber>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ShowpieceNumber>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ShowpieceNumber>();

    /// <summary>The value (for <see cref="ShowpieceNumberFormat.Percent"/> a fraction).</summary>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The format (default <see cref="ShowpieceNumberFormat.Number"/>); ignored when <see cref="Formatter"/> is set.</summary>
    public ShowpieceNumberFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>Decimals for Number, Currency and Percent (compact currency picks its own).</summary>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    /// <summary>Text before the formatted value.</summary>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>Text after the formatted value.</summary>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>Currency symbol (default "$").</summary>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <summary>Formatting culture (default en-US, as Arc).</summary>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <summary>Percentages carry their sign (+8.6%).</summary>
    public bool IsSignShown { get => GetValue(IsSignShownProperty); set => SetValue(IsSignShownProperty, value); }

    /// <summary>Custom text for a value (digits roll, everything else stays still); replaces <see cref="Format"/>.</summary>
    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    /// <summary>Start delay per character from the left when the value changes (default 26ms; zero rolls all at once).</summary>
    public TimeSpan Stagger { get => GetValue(StaggerProperty); set => SetValue(StaggerProperty, value); }

    /// <summary>Wheel fade (default <see cref="RollingDigitsFalloff.None"/>: Arc's strips only clip).</summary>
    public RollingDigitsFalloff Falloff { get => GetValue(FalloffProperty); set => SetValue(FalloffProperty, value); }

    /// <summary>Distance between glyphs on a wheel in em (default 1.2 ≈ the line height, like a CSS strip).</summary>
    public double GlyphPitch { get => GetValue(GlyphPitchProperty); set => SetValue(GlyphPitchProperty, value); }

    /// <summary>Wheel spring (default smooth, close to Arc's 580ms CSS spring).</summary>
    public ArcSpring WheelSpring { get => GetValue(WheelSpringProperty); set => SetValue(WheelSpringProperty, value); }

    /// <summary>Changes jump without rolling.</summary>
    public bool IsInstant { get => GetValue(IsInstantProperty); set => SetValue(IsInstantProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The final text (what screen readers read; set at once, before any wheel turns).</summary>
    public string DisplayText { get; private set; } = string.Empty;

    private sealed class Column
    {
        public required string Key { get; init; }
        public bool IsDigit { get; init; }
        public string Text { get; set; } = string.Empty;
        public int Digit { get; set; }
        public RollingDigits? Wheel { get; set; }
        public MotionValue<double>? Presence { get; set; }
        public bool Leaving { get; set; }
        public double X { get; set; }
        public double Width { get; set; }
    }

    private readonly struct PendingTurn
    {
        public PendingTurn(Column column, int digit, double delay)
        {
            Column = column;
            Digit = digit;
            Delay = delay;
        }

        public Column Column { get; }
        public int Digit { get; }
        public double Delay { get; }
    }

    private readonly List<Column> _columns = new();
    private readonly List<PendingTurn> _pending = new();
    private readonly Dictionary<string, ShowpieceText> _textGlyphs = new(StringComparer.Ordinal);
    private readonly LiveLoop _loop;
    private double _clock;
    private double _lineHeight;

    public ShowpieceNumber()
    {
        _loop = new LiveLoop(this, OnFrame);
        _loop.RunningChanged += running =>
        {
            if (!running)
            {
                FlushPending();
            }
        };
    }

    static ShowpieceNumber()
    {
        AffectsMeasure<ShowpieceNumber>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<ShowpieceNumber>(ForegroundProperty);
    }

    /// <summary>The digit wheels in reading order (leaving ones excluded; tests).</summary>
    internal IReadOnlyList<RollingDigits> Wheels =>
        _columns.Where(c => c.IsDigit && !c.Leaving && c.Wheel is not null).Select(c => c.Wheel!).ToList();

    /// <summary>Digit changes still waiting for their stagger delay (tests).</summary>
    internal int PendingCount => _pending.Count;

    /// <summary>The text for <paramref name="value"/> with the current options.</summary>
    public string FormatValue(double value)
    {
        var culture = CultureInfo.GetCultureInfo(CultureName);
        var core = Formatter is { } formatter
            ? formatter(value)
            : Format switch
            {
                ShowpieceNumberFormat.Currency => ShowpieceFormat.Currency(value, Decimals, CurrencySymbol, culture),
                ShowpieceNumberFormat.CompactCurrency => ShowpieceFormat.CompactCurrency(value, CurrencySymbol, culture),
                ShowpieceNumberFormat.Percent => ShowpieceFormat.Percent(value, Decimals, IsSignShown, culture),
                _ => ShowpieceFormat.Grouped(value, Decimals, culture)
            };
        return Prefix + core + Suffix;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ValueProperty || property == FormatProperty || property == DecimalsProperty || property == PrefixProperty ||
            property == SuffixProperty || property == CurrencySymbolProperty || property == CultureNameProperty ||
            property == IsSignShownProperty || property == FormatterProperty)
        {
            Rebuild();
        }
        else if (property == FalloffProperty || property == GlyphPitchProperty || property == WheelSpringProperty || property == IsInstantProperty)
        {
            foreach (var column in _columns)
            {
                if (column.Wheel is { } wheel)
                {
                    ConfigureWheel(wheel);
                }
            }
        }
        else if (property == FontFamilyProperty || property == FontSizeProperty || property == FontWeightProperty)
        {
            // Glyphs are drawn in the current Foreground, so only font changes lay them out again.
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

    private bool Animates => IsLoaded && !IsInstant && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    private void Rebuild()
    {
        var text = FormatValue(Value);
        DisplayText = text;
        AutomationProperties.SetName(this, text);
        var culture = CultureInfo.GetCultureInfo(CultureName);
        var parts = ArcFormatting.ToParts(text, culture);

        // Place values: digits before the first decimal separator count down to the ones column.
        var integerDigits = 0;
        foreach (var part in parts)
        {
            if (part.Kind == FormattedPartKind.Decimal)
            {
                break;
            }
            if (part.Kind == FormattedPartKind.Digit)
            {
                integerDigits++;
            }
        }
        var animate = Animates;
        var stagger = animate && _loop.CanRun ? Math.Max(0, Stagger.TotalSeconds) : 0;
        var existing = new Dictionary<string, Column>(StringComparer.Ordinal);
        foreach (var column in _columns)
        {
            if (!column.Leaving)
            {
                existing[column.Key] = column;
            }
        }
        var next = new List<Column>(parts.Count + 2);
        var place = integerDigits;
        var fraction = 0;
        var literals = 0;
        var seenDecimal = false;
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            string key;
            var isDigit = part.Kind == FormattedPartKind.Digit;
            switch (part.Kind)
            {
                case FormattedPartKind.Digit when !seenDecimal:
                    key = "i" + (--place).ToString(CultureInfo.InvariantCulture);
                    break;
                case FormattedPartKind.Digit:
                    key = "f" + (fraction++).ToString(CultureInfo.InvariantCulture);
                    break;
                case FormattedPartKind.Decimal when !seenDecimal:
                    seenDecimal = true;
                    key = "d";
                    break;
                case FormattedPartKind.Group:
                    key = "g" + place.ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    key = "l" + (literals++).ToString(CultureInfo.InvariantCulture) + part.Text;
                    break;
            }
            if (existing.Remove(key, out var column) && (column.IsDigit == isDigit) && (isDigit || column.Text == part.Text))
            {
                if (isDigit)
                {
                    var digit = part.Text[0] - '0';
                    if (column.Digit != digit)
                    {
                        column.Digit = digit;
                        Turn(column, digit, stagger * index);
                    }
                }
                next.Add(column);
                continue;
            }
            if (column is not null)
            {
                // Same key, different kind or text: the old one leaves.
                existing[key + "\u0001"] = column;
            }
            next.Add(CreateColumn(key, part, animate));
        }
        foreach (var gone in existing.Values)
        {
            Leave(gone, animate);
            var at = _columns.IndexOf(gone);
            if (gone.Leaving && at >= 0)
            {
                next.Insert(Math.Min(at, next.Count), gone);
            }
        }
        _columns.Clear();
        _columns.AddRange(next);
        if (_pending.Count > 0)
        {
            _loop.Start();
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Column CreateColumn(string key, FormattedPart part, bool animate)
    {
        var isDigit = part.Kind == FormattedPartKind.Digit;
        var column = new Column { Key = key, IsDigit = isDigit, Text = part.Text, Digit = isDigit ? part.Text[0] - '0' : 0 };
        if (isDigit)
        {
            var wheel = new RollingDigits { Value = column.Digit, UseGrouping = false, Decimals = 0, CultureName = "en-US" };
            ConfigureWheel(wheel);
            AutomationProperties.SetAccessibilityView(wheel, AccessibilityView.Raw);
            column.Wheel = wheel;
            LogicalChildren.Add(wheel);
            VisualChildren.Add(wheel);
        }
        column.Presence = new MotionValue<double>(this, animate ? 0 : 1, MotionVectors.Double);
        column.Presence.Changed += _ => InvalidateMeasure();
        if (animate)
        {
            _ = column.Presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Morph));
        }
        return column;
    }

    private void ConfigureWheel(RollingDigits wheel)
    {
        wheel.Falloff = Falloff;
        wheel.GlyphPitch = GlyphPitch;
        wheel.WheelSpring = WheelSpring;
        wheel.IsInstant = IsInstant;
        wheel.IsShortestTurn = false;
    }

    private void Leave(Column column, bool animate)
    {
        _pending.RemoveAll(p => ReferenceEquals(p.Column, column));
        if (!animate || column.Presence is null)
        {
            Remove(column);
            return;
        }
        column.Leaving = true;
        var leaving = column;
        column.Presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Morph)).ContinueWith(_ =>
            Dispatcher.Post(() =>
            {
                if (leaving.Leaving)
                {
                    Remove(leaving);
                    _columns.Remove(leaving);
                    InvalidateMeasure();
                }
            }), TaskScheduler.Default);
    }

    private void Remove(Column column)
    {
        column.Leaving = false;
        column.Presence?.Dispose();
        if (column.Wheel is { } wheel)
        {
            VisualChildren.Remove(wheel);
            LogicalChildren.Remove(wheel);
            column.Wheel = null;
        }
    }

    private void Turn(Column column, int digit, double delay)
    {
        _pending.RemoveAll(p => ReferenceEquals(p.Column, column));
        if (delay <= 0)
        {
            Apply(column, digit);
            return;
        }
        _pending.Add(new PendingTurn(column, digit, _clock + delay));
    }

    private static void Apply(Column column, int digit)
    {
        if (column.Wheel is { } wheel)
        {
            wheel.Value = digit;
        }
    }

    private bool OnFrame(double dt)
    {
        _clock += dt;
        for (var i = 0; i < _pending.Count; i++)
        {
            var turn = _pending[i];
            if (turn.Delay <= _clock + 1e-9)
            {
                Apply(turn.Column, turn.Digit);
                _pending.RemoveAt(i);
                i--;
            }
        }
        if (_pending.Count == 0)
        {
            _clock = 0;
            return false;
        }
        return true;
    }

    private void FlushPending()
    {
        if (_pending.Count == 0)
        {
            return;
        }
        foreach (var turn in _pending)
        {
            Apply(turn.Column, turn.Digit);
        }
        _pending.Clear();
        _clock = 0;
    }

    private ShowpieceText TextGlyph(string text)
    {
        if (!_textGlyphs.TryGetValue(text, out var glyph))
        {
            glyph = ShowpieceText.Lay(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize);
            _textGlyphs[text] = glyph;
        }
        return glyph;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lineHeight = TextGlyph("0").Height;
        var width = 0.0;
        var height = _lineHeight;
        foreach (var column in _columns)
        {
            double natural;
            if (column.Wheel is { } wheel)
            {
                wheel.Measure(Size.Infinity);
                natural = wheel.DesiredSize.Width;
                height = Math.Max(height, wheel.DesiredSize.Height);
            }
            else
            {
                natural = TextGlyph(column.Text).WidthIncludingTrailingWhitespace;
            }
            column.Width = natural * Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
            width += column.Width;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var column in _columns)
        {
            column.X = x;
            if (column.Wheel is { } wheel)
            {
                wheel.Opacity = Math.Clamp(column.Presence?.Value ?? 1, 0, 1);
                wheel.Arrange(new Rect(x, 0, wheel.DesiredSize.Width, finalSize.Height));
            }
            x += column.Width;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground ?? Brushes.Black;
        foreach (var column in _columns)
        {
            if (column.IsDigit || column.Width <= 0.01)
            {
                continue;
            }
            using (context.PushOpacity(Math.Clamp(column.Presence?.Value ?? 1, 0, 1)))
            {
                TextGlyph(column.Text).Draw(context, brush, new Point(column.X, 0));
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ShowpieceNumberAutomationPeer(this);
}

/// <summary>Reads a <see cref="ShowpieceNumber"/> as one text with its final value; the rolling wheels are hidden.</summary>
internal sealed class ShowpieceNumberAutomationPeer : ControlAutomationPeer
{
    public ShowpieceNumberAutomationPeer(ShowpieceNumber owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string? GetNameCore() => ((ShowpieceNumber)Owner).DisplayText;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
}
