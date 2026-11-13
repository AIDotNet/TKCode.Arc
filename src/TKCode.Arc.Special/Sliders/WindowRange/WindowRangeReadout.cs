using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One readout of a <see cref="WindowRange"/> (start, length or end). The text is split into slots: every digit is a slot
/// and every run of other characters ("Aug ", " days") is one slot. When the text changes with <c>roll</c>, each slot whose
/// text changed rolls: the old glyph slides one line away and fades out while the new one slides in from the other side,
/// in the direction of the change. Digit runs pair from the right like an odometer, so "9" and "10" roll their units;
/// a slot with no partner appears or disappears at once. While a drag or a coast is running the text is replaced in place.
/// </summary>
internal sealed class WindowRangeReadout : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<WindowRangeReadout, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<WindowRangeReadout, FontFamily>(nameof(FontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<WindowRangeReadout, double>(nameof(FontSize), 13);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<WindowRangeReadout, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<WindowRangeReadout, double>(nameof(LineHeight), 18.2);

    /// <summary>Letter spacing in em (Arc: −0.01em).</summary>
    public static readonly StyledProperty<double> LetterSpacingEmProperty =
        AvaloniaProperty.Register<WindowRangeReadout, double>(nameof(LetterSpacingEm), -0.01);

    /// <summary>How far an old glyph travels, as a share of the line height (Arc: 13.1px on an 18.2px line).</summary>
    internal const double RollDistance = 0.72;

    /// <summary>Arc window range (design reference): the roll settles on a critically damped spring in about 0.3s.</summary>
    internal static readonly SpringSpec RollSpring = SpringSpec.FromPhysical(708, 53.7);

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly Dictionary<string, TextLayout> _layouts = new(StringComparer.Ordinal);
    private Slot[] _slots = [];
    private MotionValue<double>? _roll;
    private int _direction = 1;
    private string _text = "";
    private double _width;

    private struct Slot
    {
        public string Text;
        public TextLayout Layout;
        public TextLayout? Old;
    }

    static WindowRangeReadout()
    {
        AffectsMeasure<WindowRangeReadout>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty,
            LetterSpacingEmProperty);
        AffectsRender<WindowRangeReadout>(ForegroundProperty);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double LetterSpacingEm { get => GetValue(LetterSpacingEmProperty); set => SetValue(LetterSpacingEmProperty, value); }

    /// <summary>The text shown (after the roll).</summary>
    public string Text => _text;

    /// <summary>Progress of the running roll (1 at rest; tests).</summary>
    internal double RollProgress => _roll?.Value ?? 1;

    /// <summary>
    /// Shows <paramref name="text"/>. With <paramref name="roll"/> the changed slots roll; <paramref name="direction"/> is
    /// +1 when the value went up (old glyphs leave upward) and −1 when it went down.
    /// </summary>
    public void SetText(string text, int direction, bool roll)
    {
        if (text == _text)
        {
            return;
        }
        var previous = _text;
        _text = text;
        _direction = direction < 0 ? -1 : 1;
        // An interrupted roll restarts from the text that was arriving.
        _slots = BuildSlots(text, roll && previous.Length > 0 ? previous : null);
        InvalidateMeasure();
        InvalidateVisual();
        var rolling = false;
        foreach (var slot in _slots)
        {
            rolling |= slot.Old is not null;
        }
        if (!rolling)
        {
            _roll?.Jump(1);
            return;
        }
        _roll ??= CreateRoll();
        _roll.Jump(0);
        _ = _roll.AnimateTo(1, MotionSpec.Spring(RollSpring));
    }

    private MotionValue<double> CreateRoll()
    {
        var roll = new MotionValue<double>(this, 1, MotionVectors.Double);
        roll.Changed += _ => InvalidateVisual();
        return roll;
    }

    private Slot[] BuildSlots(string text, string? previous)
    {
        var segments = Split(text);
        var oldSegments = previous is null ? null : Split(previous);
        var count = 0;
        foreach (var segment in segments)
        {
            count += segment.Digits ? segment.Text.Length : 1;
        }
        var slots = new Slot[count];
        var index = 0;
        for (var s = 0; s < segments.Count; s++)
        {
            var segment = segments[s];
            Run? partner = oldSegments is not null && s < oldSegments.Count && oldSegments[s].Digits == segment.Digits
                ? oldSegments[s]
                : null;
            if (!segment.Digits)
            {
                slots[index++] = MakeSlot(segment.Text, partner is { } p && p.Text != segment.Text ? p.Text : null);
                continue;
            }
            var length = segment.Text.Length;
            for (var d = 0; d < length; d++)
            {
                var glyph = segment.Text.Substring(d, 1);
                string? old = null;
                if (partner is { } p)
                {
                    // Odometer pairing: the n-th digit from the right meets the n-th digit from the right.
                    var fromRight = length - 1 - d;
                    var oldIndex = p.Text.Length - 1 - fromRight;
                    if (oldIndex >= 0 && p.Text[oldIndex] != glyph[0])
                    {
                        old = p.Text.Substring(oldIndex, 1);
                    }
                }
                slots[index++] = MakeSlot(glyph, old);
            }
        }
        return slots;
    }

    private Slot MakeSlot(string text, string? old) => new()
    {
        Text = text,
        Layout = LayoutOf(text),
        Old = old is null ? null : LayoutOf(old)
    };

    private readonly record struct Run(string Text, bool Digits);

    private static List<Run> Split(string text)
    {
        var segments = new List<Run>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || char.IsAsciiDigit(text[i]) != char.IsAsciiDigit(text[start]))
            {
                segments.Add(new Run(text[start..i], char.IsAsciiDigit(text[start])));
                start = i;
            }
        }
        return segments;
    }

    private TextLayout LayoutOf(string text)
    {
        if (_layouts.TryGetValue(text, out var layout))
        {
            return layout;
        }
        layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground,
            TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null, FlowDirection.LeftToRight,
            double.PositiveInfinity, double.PositiveInfinity, LineHeight, LetterSpacingEm * FontSize, 1, s_tabular);
        _layouts[text] = layout;
        return layout;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
            change.Property == FontWeightProperty || change.Property == LineHeightProperty || change.Property == LetterSpacingEmProperty)
        {
            foreach (var layout in _layouts.Values)
            {
                layout.Dispose();
            }
            _layouts.Clear();
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i] = MakeSlot(_slots[i].Text, null);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _width = 0;
        foreach (var slot in _slots)
        {
            _width += slot.Layout.WidthIncludingTrailingWhitespace;
        }
        return new Size(_width, LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var x = HorizontalAlignment switch
        {
            HorizontalAlignment.Right => width - _width,
            HorizontalAlignment.Center => (width - _width) / 2,
            _ => 0
        };
        var p = Math.Clamp(_roll?.Value ?? 1, 0, 1.2);
        var rolling = p < 1;
        var travel = LineHeight * RollDistance;
        foreach (var slot in _slots)
        {
            var w = slot.Layout.WidthIncludingTrailingWhitespace;
            if (rolling && slot.Old is { } old)
            {
                // Up: the old glyph leaves upward and the new one rises from below; down mirrors it.
                using (context.PushOpacity(Math.Clamp(1 - p, 0, 1)))
                {
                    old.Draw(context, new Point(x, -_direction * travel * p));
                }
                using (context.PushOpacity(Math.Clamp(p, 0, 1)))
                {
                    slot.Layout.Draw(context, new Point(x, _direction * travel * (1 - p)));
                }
            }
            else
            {
                slot.Layout.Draw(context, new Point(x, 0));
            }
            x += w;
        }
    }
}
