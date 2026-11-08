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
/// The rolling numbers of <see cref="SeatManager"/> (Arc Seat manager, design reference; notes 6). Each digit is a 0–9
/// strip that slides 240 ms on (.22, 1, .36, 1) from its old digit to its new one: every column rolls by the size of its
/// own digit change (5 → 1 passes 4, 3, 2; 9 → 0 runs the whole strip), never the short way round and never in the
/// direction of the whole number; all columns start together, without stagger or blur. Columns are keyed by their place
/// from the right; a place that is new, or whose character changes between digit and symbol (the "$" that shifts left
/// when a digit is added), fades in over 160 ms already showing its final glyph; places that go away vanish at once. The
/// width is never animated: digits sit in tabular cells and the text is drawn from the right. Reduced and Off jump.
/// </summary>
/// <remarks>
/// Core <c>RollingDigits</c> turns its wheels in the direction of the whole value and grows new columns on a spring, so
/// this block keeps its own small strip renderer instead of changing Core. A <see cref="MotionValue{T}"/> per column
/// carries the strip's position (a tween that is redirected from where it is when the value changes mid-roll).
/// </remarks>
internal sealed class SeatManagerRoll : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SeatManagerRoll, string?>(nameof(Text));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SeatManagerRoll>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SeatManagerRoll>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SeatManagerRoll>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SeatManagerRoll>();

    /// <summary>Line height of the strip (20 for Arc's 14/20 and 13/20 copy).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<SeatManagerRoll, double>(nameof(LineHeight), 20);

    internal static readonly TimeSpan RollDuration = TimeSpan.FromMilliseconds(240);
    internal static readonly TimeSpan FreshDuration = TimeSpan.FromMilliseconds(160);

    private sealed class Column
    {
        public char Char;
        public bool IsDigit;
        public int Digit;
        public MotionValue<double>? Position;
        public MotionValue<double>? Fade;
    }

    // Left to right.
    private readonly List<Column> _columns = new();
    private readonly TextLayout?[] _digits = new TextLayout?[10];
    private readonly Dictionary<char, TextLayout> _symbols = new();
    private double _cell;
    private double _glyphHeight;

    static SeatManagerRoll()
    {
        AffectsMeasure<SeatManagerRoll>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        AffectsRender<SeatManagerRoll>(ForegroundProperty);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>Strip positions of the digit columns, left to right (tests: where each strip is now).</summary>
    internal IReadOnlyList<double> Positions => _columns.Where(c => c.IsDigit).Select(c => c.Position?.Value ?? c.Digit).ToList();

    /// <summary>Opacity of every column, left to right (tests: which columns fade in).</summary>
    internal IReadOnlyList<double> Opacities => _columns.Select(c => c.Fade?.Value ?? 1).ToList();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Rebuild(Text ?? string.Empty);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            Array.Clear(_digits);
            _symbols.Clear();
        }
    }

    private void Rebuild(string text)
    {
        AutomationProperties.SetName(this, text);
        var animate = IsLoaded && ArcMotion.IsFull(this);
        var old = _columns.ToArray();
        _columns.Clear();
        var structure = old.Length != text.Length;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            var place = text.Length - 1 - i;
            var oldIndex = old.Length - 1 - place;
            var previous = oldIndex >= 0 ? old[oldIndex] : null;
            var isDigit = ch is >= '0' and <= '9';
            if (previous is not null && isDigit && previous.IsDigit)
            {
                var digit = ch - '0';
                if (digit != previous.Digit)
                {
                    var from = previous.Digit;
                    previous.Digit = digit;
                    previous.Char = ch;
                    if (animate)
                    {
                        previous.Position ??= CreateValue(from);
                        _ = previous.Position.AnimateTo(digit, MotionSpec.Tween(RollDuration, ArcEasings.Standard));
                    }
                    else
                    {
                        previous.Position?.Jump(digit);
                    }
                }
                _columns.Add(previous);
                continue;
            }
            if (previous is not null && !isDigit && !previous.IsDigit && previous.Char == ch)
            {
                _columns.Add(previous);
                continue;
            }
            structure = true;
            var column = new Column { Char = ch, IsDigit = isDigit, Digit = isDigit ? ch - '0' : 0 };
            // Only places that replace an existing number fade in; the first text simply appears.
            if (animate && old.Length > 0)
            {
                column.Fade = CreateValue(0);
                _ = column.Fade.AnimateTo(1, MotionSpec.Tween(FreshDuration, ArcEasings.Standard));
            }
            _columns.Add(column);
        }
        if (structure)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private Typeface Face => new(FontFamily, FontStyle.Normal, FontWeight);

    private void EnsureGlyphs()
    {
        if (_digits[0] is not null)
        {
            return;
        }
        var brush = Foreground ?? Brushes.Black;
        for (var d = 0; d < 10; d++)
        {
            _digits[d] = new TextLayout(d.ToString(CultureInfo.InvariantCulture), Face, FontSize, brush);
        }
        _cell = _digits.Max(g => g!.WidthIncludingTrailingWhitespace);
        _glyphHeight = _digits[0]!.Height;
    }

    private TextLayout Symbol(char ch)
    {
        if (!_symbols.TryGetValue(ch, out var layout))
        {
            layout = new TextLayout(ch.ToString(), Face, FontSize, Foreground ?? Brushes.Black);
            _symbols[ch] = layout;
        }
        return layout;
    }

    private double ColumnWidth(Column column) => column.IsDigit ? _cell : Symbol(column.Char).WidthIncludingTrailingWhitespace;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGlyphs();
        var width = 0.0;
        foreach (var column in _columns)
        {
            width += ColumnWidth(column);
        }
        return new Size(width, Math.Max(LineHeight, _glyphHeight));
    }

    public override void Render(DrawingContext context)
    {
        EnsureGlyphs();
        var height = Bounds.Height;
        var line = Math.Max(LineHeight, _glyphHeight);
        var top = (height - _glyphHeight) / 2;
        var width = 0.0;
        foreach (var column in _columns)
        {
            width += ColumnWidth(column);
        }
        // Right aligned: the number grows to the left when a place is added.
        var x = Bounds.Width - width;
        foreach (var column in _columns)
        {
            var w = ColumnWidth(column);
            var opacity = Math.Clamp(column.Fade?.Value ?? 1, 0, 1);
            if (opacity > 0.001)
            {
                using (context.PushOpacity(opacity))
                {
                    if (column.IsDigit)
                    {
                        var position = column.Position?.Value ?? column.Digit;
                        using (context.PushClip(new Rect(x, 0, w, height)))
                        {
                            var first = Math.Max(0, (int)Math.Floor(position) - 1);
                            var last = Math.Min(9, (int)Math.Ceiling(position) + 1);
                            for (var d = first; d <= last; d++)
                            {
                                var y = top + (d - position) * line;
                                if (y > height || y + _glyphHeight < 0)
                                {
                                    continue;
                                }
                                var glyph = _digits[d]!;
                                glyph.Draw(context, new Point(x + (w - glyph.WidthIncludingTrailingWhitespace) / 2, y));
                            }
                        }
                    }
                    else
                    {
                        Symbol(column.Char).Draw(context, new Point(x, top));
                    }
                }
            }
            x += w;
        }
    }
}
