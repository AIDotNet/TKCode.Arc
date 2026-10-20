using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How <see cref="TextMorph"/> pairs letters between the old and the new text.</summary>
public enum TextMorphMatching
{
    /// <summary>By character and occurrence: the second "i" pairs with the second "i" (Arc TextMorph).</summary>
    Occurrence,

    /// <summary>Shared leading and trailing runs keep their identity; the middle is replaced (Arc CopyButton).</summary>
    PrefixSuffix
}

/// <summary>
/// Morphs one short label into the next in place (P4): letters both strings share glide to their new positions,
/// new letters sharpen in with a short stagger, removed letters blur away, and the width follows on a spring.
/// Single line; assistive technology reads the plain text. Rendered directly: no control per letter.
/// </summary>
public class TextMorph : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TextMorph, string?>(nameof(Text));

    public static readonly StyledProperty<TextMorphMatching> MatchingProperty =
        AvaloniaProperty.Register<TextMorph, TextMorphMatching>(nameof(Matching));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<TextMorph>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<TextMorph>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<TextMorph>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<TextMorph>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public TextMorphMatching Matching { get => GetValue(MatchingProperty); set => SetValue(MatchingProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private sealed class Glyph
    {
        public required string Key { get; init; }
        public required string Char { get; init; }
        public FormattedText? Text { get; set; }
        public double Width { get; set; }
        public MotionValue<double>? X { get; set; }
        public double TargetX { get; set; }
        public MotionValue<double>? Enter { get; set; }
        public MotionValue<double>? Exit { get; set; }
        public bool Leaving { get; set; }
    }

    private readonly List<Glyph> _glyphs = new();
    private MotionValue<double>? _width;
    private double _lineHeight;
    private int _sequence;
    private bool _fontDirty = true;

    static TextMorph()
    {
        AffectsMeasure<TextMorph>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<TextMorph>(ForegroundProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text ?? string.Empty);
            Morph(change.GetOldValue<string?>() ?? string.Empty, Text ?? string.Empty);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty)
        {
            _fontDirty = true;
        }
        else if (change.Property == ForegroundProperty)
        {
            // Color only: re-brush the shaped glyphs, metrics stay.
            var brush = Foreground ?? Brushes.Black;
            foreach (var glyph in _glyphs)
            {
                glyph.Text?.SetForegroundBrush(brush);
            }
        }
    }

    private FormattedText Shape(string ch) => new(ch == " " ? " " : ch, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize, Foreground ?? Brushes.Black);

    private void EnsureFont()
    {
        if (!_fontDirty)
        {
            return;
        }
        _fontDirty = false;
        foreach (var glyph in _glyphs)
        {
            glyph.Text = Shape(glyph.Char);
            glyph.Width = glyph.Text.WidthIncludingTrailingWhitespace;
        }
        _lineHeight = Shape("Hg").Height;
        Relayout(animate: false, invalidate: false);
    }

    private List<(string Key, string Char)> Keys(string text, string? previous, List<Glyph>? previousGlyphs)
    {
        var chars = text.Select(c => c.ToString()).ToList();
        var result = new List<(string, string)>(chars.Count);
        if (Matching == TextMorphMatching.Occurrence || previous is null || previousGlyphs is null)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var ch in chars)
            {
                var count = seen.GetValueOrDefault(ch);
                seen[ch] = count + 1;
                result.Add(($"{ch}-{count}", ch));
            }
            return result;
        }
        // Prefix / suffix matching: shared runs of at least two characters keep their glyph identity.
        var prev = previous.Select(c => c.ToString()).ToList();
        int start = 0, end = 0;
        while (start < prev.Count && start < chars.Count && prev[start] == chars[start])
        {
            start++;
        }
        while (end < prev.Count - start && end < chars.Count - start && prev[^(end + 1)] == chars[^(end + 1)])
        {
            end++;
        }
        if (start < 2)
        {
            start = 0;
        }
        if (end < 2)
        {
            end = 0;
        }
        var live = previousGlyphs.Where(g => !g.Leaving).ToList();
        _sequence++;
        for (var i = 0; i < chars.Count; i++)
        {
            if (i < start && i < live.Count)
            {
                result.Add((live[i].Key, chars[i]));
            }
            else if (i >= chars.Count - end && live.Count - (chars.Count - i) >= 0)
            {
                result.Add((live[live.Count - (chars.Count - i)].Key, chars[i]));
            }
            else
            {
                result.Add(($"{_sequence}:{i}", chars[i]));
            }
        }
        return result;
    }

    private void Morph(string previous, string next)
    {
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var keys = Keys(next, previous, _glyphs);
        var existing = _glyphs.Where(g => !g.Leaving).ToDictionary(g => g.Key);
        var ordered = new List<Glyph>(keys.Count);
        var entering = 0;
        foreach (var (key, ch) in keys)
        {
            if (existing.Remove(key, out var glyph))
            {
                ordered.Add(glyph);
                continue;
            }
            glyph = new Glyph { Key = key, Char = ch, Text = Shape(ch) };
            glyph.Width = glyph.Text.WidthIncludingTrailingWhitespace;
            glyph.Enter = new MotionValue<double>(this, animate ? 0 : 1, MotionVectors.Double);
            glyph.Enter.Changed += _ => InvalidateVisual();
            if (animate)
            {
                var delay = 0.05 + Math.Min(entering, 8) * 0.016 * 2;
                entering++;
                _ = glyph.Enter.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromSeconds(0.28), ArcEasings.Enter, TimeSpan.FromSeconds(delay)));
            }
            ordered.Add(glyph);
        }
        // Removed letters stay where they were, out of flow, and fade away.
        foreach (var gone in existing.Values)
        {
            gone.Leaving = true;
            if (animate)
            {
                gone.Exit = new MotionValue<double>(this, 0, MotionVectors.Double);
                gone.Exit.Changed += _ => InvalidateVisual();
                var leaving = gone;
                gone.Exit.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard))
                    .ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _glyphs.Remove(leaving);
                        InvalidateVisual();
                    }), TaskScheduler.Default);
            }
        }
        var leavingGlyphs = _glyphs.Where(g => g.Leaving && existing.ContainsKey(g.Key) && animate).ToList();
        _glyphs.Clear();
        _glyphs.AddRange(ordered);
        _glyphs.AddRange(leavingGlyphs);
        Relayout(animate);
    }

    private void Relayout(bool animate, bool invalidate = true)
    {
        var x = 0.0;
        foreach (var glyph in _glyphs.Where(g => !g.Leaving))
        {
            glyph.TargetX = x;
            if (glyph.X is null)
            {
                glyph.X = new MotionValue<double>(this, x, MotionVectors.Double);
                glyph.X.Changed += _ =>
                {
                    if (!_suppressInvalidate)
                    {
                        InvalidateVisual();
                    }
                };
            }
            else if (animate)
            {
                _ = glyph.X.AnimateTo(x, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                _suppressInvalidate |= !invalidate;
                glyph.X.Jump(x);
                _suppressInvalidate = false;
            }
            x += glyph.Width;
        }
        if (_width is null)
        {
            _width = new MotionValue<double>(this, x, MotionVectors.Double);
            _width.Changed += _ =>
            {
                if (!_suppressInvalidate)
                {
                    InvalidateMeasure();
                }
            };
        }
        else if (animate && Math.Abs(_width.Target - x) > 0.5)
        {
            _ = _width.AnimateTo(x, MotionSpec.Spring(ArcSpring.Morph));
        }
        else if (invalidate)
        {
            _width.Jump(x);
        }
        else
        {
            _suppressInvalidate = true;
            try
            {
                _width.Jump(x);
            }
            finally
            {
                _suppressInvalidate = false;
            }
        }
        if (invalidate)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private bool _suppressInvalidate;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_glyphs.Count == 0 && !string.IsNullOrEmpty(Text))
        {
            Morph(string.Empty, Text);
        }
        EnsureFont();
        if (_lineHeight <= 0)
        {
            _lineHeight = Shape("Hg").Height;
        }
        return new Size(_width?.Value ?? 0, _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var em = FontSize;
        foreach (var glyph in _glyphs)
        {
            if (glyph.Text is null)
            {
                continue;
            }
            var x = glyph.X?.Value ?? glyph.TargetX;
            double opacity = 1, scale = 1, dy = 0;
            if (glyph.Leaving)
            {
                var q = glyph.Exit?.Value ?? 1;
                opacity = 1 - q;
                scale = 1 - 0.14 * q;
                dy = -0.1 * em * q;
            }
            else if (glyph.Enter is { } enter)
            {
                var p = enter.Value;
                opacity = p;
                scale = 0.8 + 0.2 * p;
                dy = 0.14 * em * (1 - p);
            }
            if (opacity <= 0.001)
            {
                continue;
            }
            var originX = x + glyph.Width / 2;
            var originY = _lineHeight * 0.6;
            var matrix = Matrix.CreateTranslation(-originX, -originY) * Matrix.CreateScale(scale, scale) *
                         Matrix.CreateTranslation(originX, originY + dy);
            using (context.PushTransform(matrix))
            using (context.PushOpacity(opacity))
            {
                context.DrawText(glyph.Text, new Point(x, 0));
            }
        }
    }
}
