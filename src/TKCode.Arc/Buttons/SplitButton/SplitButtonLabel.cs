using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The split button's main label (P4). Leading and trailing runs that the old and the new text share (two letters or
/// more) keep their glyphs and glide to their new places on the morph spring; the changed letters rise 5px out of a soft
/// 4px blur (240ms on the enter curve, 16ms apart, capped at 100ms) while the removed ones lift 4px into a 2px blur and
/// fade (160ms), out of the flow. Reduced motion crossfades in 120ms; Off replaces at once. Drawn in <see cref="Render"/>:
/// no control per letter. Code-driven because every letter has its own delay and identity, which ControlTheme
/// transitions cannot express.
/// </summary>
internal sealed class SplitButtonLabel : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SplitButtonLabel, string?>(nameof(Text));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SplitButtonLabel>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SplitButtonLabel>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SplitButtonLabel>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SplitButtonLabel>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private const double EnterRise = 5;
    private const double EnterBlur = 4;
    private const double ExitLift = 4;
    private const double ExitBlur = 2;
    private static readonly TimeSpan StaggerCap = TimeSpan.FromMilliseconds(100);

    /// <summary>Blur radii quantized to 0.25px, so a running morph allocates no effects.</summary>
    private static readonly ImmutableBlurEffect[] Blurs = Enumerable.Range(1, 16).Select(i => new ImmutableBlurEffect(i * 0.25)).ToArray();

    private sealed class Glyph
    {
        public required string Char { get; init; }
        public int Order { get; init; }
        public FormattedText? Text { get; set; }
        public double Width { get; set; }
        public double TargetX { get; set; }
        public MotionValue<double>? X { get; set; }
        public MotionValue<double>? Enter { get; set; }
        public MotionValue<double>? Exit { get; set; }
        public bool Leaving { get; set; }
        public bool Spatial { get; set; }
    }

    private readonly List<Glyph> _glyphs = new();
    private List<string> _chars = new();
    private double _lineHeight;
    private bool _shapeDirty = true;

    static SplitButtonLabel()
    {
        AffectsMeasure<SplitButtonLabel>(FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<SplitButtonLabel>(ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SplitButtonLabel>(false);
    }

    public SplitButtonLabel()
    {
        // The button carries the name; the letters are decoration.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The glyphs on screen in reading order (tests): the present letters, then the ones still leaving.</summary>
    internal IReadOnlyList<string> PresentGlyphs => _glyphs.Where(g => !g.Leaving).Select(g => g.Char).ToList();

    /// <summary>Number of letters still fading out (tests).</summary>
    internal int LeavingCount => _glyphs.Count(g => g.Leaving);

    /// <summary>Identity of the present glyph at <paramref name="index"/> (tests: shared runs keep their objects).</summary>
    internal object GlyphIdentity(int index) => _glyphs.Where(g => !g.Leaving).ElementAt(index);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Morph(change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            _shapeDirty = true;
            InvalidateMeasure();
        }
    }

    private static List<string> Split(string text)
    {
        var list = new List<string>(text.Length);
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            list.Add(elements.GetTextElement());
        }
        return list;
    }

    private void Morph(string text)
    {
        var next = Split(text);
        var previous = _chars;
        _chars = next;
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && level != ArcMotionLevel.Off;
        var full = animate && level == ArcMotionLevel.Full;
        EnsureShaped();

        // Shared leading and trailing runs keep their identity, so "Copy page" to "Copied" only replaces the changed letters.
        int start = 0, end = 0;
        while (start < previous.Count && start < next.Count && previous[start] == next[start])
        {
            start++;
        }
        while (end < previous.Count - start && end < next.Count - start && previous[^(end + 1)] == next[^(end + 1)])
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

        var present = new List<Glyph>(previous.Count);
        var leaving = new List<Glyph>();
        foreach (var glyph in _glyphs)
        {
            (glyph.Leaving ? leaving : present).Add(glyph);
        }
        var ordered = new List<Glyph>(next.Count);
        for (var i = 0; i < start && i < present.Count; i++)
        {
            ordered.Add(present[i]);
        }
        for (var i = start; i < next.Count - end; i++)
        {
            var glyph = new Glyph { Char = next[i], Order = i - start };
            Shape(glyph);
            if (animate)
            {
                var enter = new MotionValue<double>(this, 0, MotionVectors.Double);
                enter.Changed += _ => InvalidateVisual();
                glyph.Enter = enter;
                glyph.Spatial = full;
                var delay = TimeSpan.FromTicks(Math.Min(ArcMotionPresets.StaggerChar.Ticks * glyph.Order, StaggerCap.Ticks));
                _ = enter.AnimateTo(1, full
                    ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay)
                    : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            ordered.Add(glyph);
        }
        for (var i = Math.Max(present.Count - end, start); i < present.Count; i++)
        {
            ordered.Add(present[i]);
        }

        // Removed letters stay where they were, out of the flow, and fade away.
        for (var i = start; i < present.Count - end; i++)
        {
            var gone = present[i];
            if (!animate)
            {
                continue;
            }
            gone.Leaving = true;
            gone.Spatial = full;
            gone.X?.Stop();
            var exit = new MotionValue<double>(this, 0, MotionVectors.Double);
            exit.Changed += _ => InvalidateVisual();
            gone.Exit = exit;
            leaving.Add(gone);
            _ = RemoveWhenGone(gone, exit.AnimateTo(1, full
                ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)));
        }

        _glyphs.Clear();
        _glyphs.AddRange(ordered);
        _glyphs.AddRange(leaving);
        Relayout(full);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private async Task RemoveWhenGone(Glyph glyph, Task<bool> exit)
    {
        await exit;
        if (_glyphs.Remove(glyph))
        {
            InvalidateVisual();
        }
    }

    private void Relayout(bool animate)
    {
        var x = 0.0;
        foreach (var glyph in _glyphs)
        {
            if (glyph.Leaving)
            {
                continue;
            }
            glyph.TargetX = x;
            if (glyph.X is null)
            {
                var motion = new MotionValue<double>(this, x, MotionVectors.Double);
                motion.Changed += _ => InvalidateVisual();
                glyph.X = motion;
            }
            else if (animate && Math.Abs(glyph.X.Value - x) > 0.01)
            {
                _ = glyph.X.AnimateTo(x, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                glyph.X.Jump(x);
            }
            x += glyph.Width;
        }
    }

    private Typeface CurrentTypeface => new(FontFamily, FontStyle.Normal, FontWeight);

    private void Shape(Glyph glyph)
    {
        glyph.Text = new FormattedText(glyph.Char, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, CurrentTypeface,
            FontSize > 0 ? FontSize : 14, Foreground);
        glyph.Width = glyph.Text.WidthIncludingTrailingWhitespace;
    }

    private void EnsureShaped()
    {
        if (!_shapeDirty)
        {
            return;
        }
        _shapeDirty = false;
        foreach (var glyph in _glyphs)
        {
            Shape(glyph);
        }
        _lineHeight = new FormattedText("Hg", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, CurrentTypeface,
            FontSize > 0 ? FontSize : 14, null).Height;
        Relayout(animate: false);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureShaped();
        double width = 0;
        foreach (var glyph in _glyphs)
        {
            if (!glyph.Leaving)
            {
                width += glyph.Width;
            }
        }
        return new Size(Math.Ceiling(width), _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        foreach (var glyph in _glyphs)
        {
            if (glyph.Text is not { } text)
            {
                continue;
            }
            var x = glyph.X?.Value ?? glyph.TargetX;
            double opacity = 1, dy = 0, blur = 0;
            if (glyph.Leaving)
            {
                var q = glyph.Exit?.Value ?? 1;
                opacity = 1 - q;
                if (glyph.Spatial)
                {
                    dy = -ExitLift * q;
                    blur = ExitBlur * q;
                }
            }
            else if (glyph.Enter is { } enter)
            {
                var p = enter.Value;
                opacity = p;
                if (glyph.Spatial)
                {
                    dy = EnterRise * (1 - p);
                    blur = EnterBlur * (1 - p);
                }
            }
            if (opacity <= 0.001)
            {
                continue;
            }
            var origin = new Point(x, dy);
            using (opacity < 0.999 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?))
            {
                var index = (int)Math.Round(blur * 4) - 1;
                if (index >= 0)
                {
                    using (context.PushEffect(Blurs[Math.Min(index, Blurs.Length - 1)], new Rect(origin, new Size(glyph.Width, _lineHeight))))
                    {
                        context.DrawText(text, origin);
                    }
                }
                else
                {
                    context.DrawText(text, origin);
                }
            }
        }
    }
}
