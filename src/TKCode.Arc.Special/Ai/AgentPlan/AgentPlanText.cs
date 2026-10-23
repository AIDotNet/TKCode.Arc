using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Wrapping text drawn by the plan itself: a running step's title and the done line. It lays its text out exactly as the
/// draft's title field does (same font, size, line height, letter spacing and width), so a step does not move a pixel when
/// the field gives way to it. Three effects run on code clocks, which a ControlTheme cannot express:
/// <list type="bullet">
/// <item>its color settles to a new one in 240ms (a finished step's title turning secondary);</item>
/// <item>the shimmer of the active step (Arc text shimmer): the text rests at 12% of its color mixed into muted text while a
/// bell-shaped band of the full color sweeps through it in 1.8s, line after line as if the lines were one, then rests for a
/// third of that; full motion only, on a LiveLoop that pauses off screen;</item>
/// <item>streaming: words after a given point appear a chunk of 3-5 at a time every 34ms, each chunk fading in (full motion)
/// or appearing at once (reduced), then <see cref="StreamCompleted"/> fires.</item>
/// </list>
/// The full text is laid out from the start, so streamed words appear in place and the height does not change as they do.
/// </summary>
internal sealed class AgentPlanText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentPlanText, string?>(nameof(Text));

    /// <summary>A bold lead-in drawn in <see cref="PrefixForeground"/> before the text ("Done in 4m 06s.").</summary>
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<AgentPlanText, string?>(nameof(Prefix));

    public static readonly StyledProperty<IBrush?> PrefixForegroundProperty =
        AvaloniaProperty.Register<AgentPlanText, IBrush?>(nameof(PrefixForeground));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AgentPlanText>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<AgentPlanText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<AgentPlanText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<AgentPlanText>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<AgentPlanText, double>(nameof(LineHeight), double.NaN);

    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<AgentPlanText, double>(nameof(LetterSpacing));

    /// <summary>The muted text color the shimmer's resting base is mixed from.</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<AgentPlanText, IBrush?>(nameof(MutedForeground));

    /// <summary>True while the shimmer runs (the active step of a running plan).</summary>
    public static readonly StyledProperty<bool> IsShimmeringProperty =
        AvaloniaProperty.Register<AgentPlanText, bool>(nameof(IsShimmering));

    private const double BandEm = 2.2;
    private const double BaseMix = 0.12;
    private static readonly TimeSpan SweepDuration = TimeSpan.FromSeconds(1.8);
    private static readonly LinearEasing s_linear = new();

    private readonly SolidColorBrush _brush = new(Colors.Black);
    private readonly SolidColorBrush _baseBrush = new(Colors.Gray);
    private readonly LiveLoop _sweepLoop;
    private readonly LiveLoop _streamLoop;
    private readonly MotionValue<Color> _color;
    private readonly MotionValue<double> _settle;
    private TextLayout? _layout;
    private TextLayout? _baseLayout;
    private double _layoutWidth = double.NaN;
    private bool _layoutDirty = true;
    private bool _colorStarted;
    private double _sweep;
    private double _rest;
    private IBrush? _band;
    private double _bandEm;

    // Streaming.
    private readonly List<Chunk> _chunks = new();
    private int _streamStart = -1;
    private int _revealed;
    private double _streamClock;
    private bool _streamFades;
    private bool _streaming;

    private sealed class Chunk
    {
        public int Start;
        public int Length;
        public double RevealedAt = -1;
        public Rect[]? Rects;
    }

    static AgentPlanText()
    {
        AffectsMeasure<AgentPlanText>(TextProperty, PrefixProperty, FontFamilyProperty, FontSizeProperty, FontWeightProperty,
            LineHeightProperty, LetterSpacingProperty);
    }

    public AgentPlanText()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
        _color = new MotionValue<Color>(this, Colors.Black, MotionVectors.Color);
        _color.Changed += c =>
        {
            _brush.Color = c;
            UpdateBaseColor();
            InvalidateVisual();
        };
        _settle = new MotionValue<double>(this, 1, MotionVectors.Double);
        _settle.Changed += _ => InvalidateVisual();
        _sweepLoop = new LiveLoop(this, StepSweep) { ViewportMargin = 64 };
        _sweepLoop.RunningChanged += _ => InvalidateVisual();
        _streamLoop = new LiveLoop(this, StepStream) { MinimumLevel = ArcMotionLevel.Off };
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }
    public IBrush? PrefixForeground { get => GetValue(PrefixForegroundProperty); set => SetValue(PrefixForegroundProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double LetterSpacing { get => GetValue(LetterSpacingProperty); set => SetValue(LetterSpacingProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public bool IsShimmering { get => GetValue(IsShimmeringProperty); set => SetValue(IsShimmeringProperty, value); }

    /// <summary>Raised once the streamed words are all shown (40ms after the last chunk).</summary>
    public event EventHandler? StreamCompleted;

    /// <summary>True while words are still streaming in.</summary>
    public bool IsStreaming => _streaming;

    /// <summary>How many characters of the full text (prefix included) are shown (tests).</summary>
    public int VisibleLength => !_streaming ? FullText.Length : _revealed == 0 ? _streamStart : _chunks[_revealed - 1].Start + _chunks[_revealed - 1].Length;

    /// <summary>The shimmer's position (0 left of the text, 1 past it).</summary>
    public double Sweep => _sweep;

    /// <summary>The color the text shows now.</summary>
    public Color CurrentColor => _brush.Color;

    /// <summary>The laid-out text (tests compare it with the title field).</summary>
    public TextLayout? Layout => _layout;

    private string FullText => string.IsNullOrEmpty(Prefix) ? Text ?? string.Empty : Prefix + " " + (Text ?? string.Empty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            var target = (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;
            var level = ArcMotion.GetEffectiveLevel(this);
            if (!_colorStarted || !IsLoaded || level == ArcMotionLevel.Off)
            {
                _colorStarted = true;
                _color.Jump(target);
            }
            else
            {
                _ = _color.AnimateTo(target, MotionSpec.Tween(level == ArcMotionLevel.Full ? AgentPlanMotion.TitleColor : ArcMotionPresets.Instant,
                    ArcEasings.Standard));
            }
        }
        else if (change.Property == MutedForegroundProperty)
        {
            UpdateBaseColor();
            InvalidateVisual();
        }
        else if (change.Property == IsShimmeringProperty)
        {
            OnShimmerChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == TextProperty || change.Property == PrefixProperty || change.Property == PrefixForegroundProperty ||
                 change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == LineHeightProperty || change.Property == LetterSpacingProperty)
        {
            _layoutDirty = true;
            if (change.Property == TextProperty && _streaming)
            {
                FinishStream(raise: false);
            }
            InvalidateVisual();
        }
        else if (change.Property == ArcMotion.LevelProperty || change.Property == IsVisibleProperty)
        {
            UpdateSweep();
        }
    }

    private void UpdateBaseColor()
    {
        var muted = (MutedForeground as ISolidColorBrush)?.Color ?? Colors.Gray;
        _baseBrush.Color = Oklch.MixOklab(muted, _brush.Color, BaseMix);
    }

    // Shimmer -----------------------------------------------------------------------------------------------------------

    private void OnShimmerChanged(bool on)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (on)
        {
            _sweep = 0;
            _rest = 0.05;
            _settle.Jump(0);
        }
        else if (IsLoaded && level != ArcMotionLevel.Off)
        {
            _ = _settle.AnimateTo(1, MotionSpec.Tween(level == ArcMotionLevel.Full ? AgentPlanMotion.TitleColor : ArcMotionPresets.Instant,
                ArcEasings.Standard));
        }
        else
        {
            _settle.Jump(1);
        }
        UpdateSweep();
        InvalidateVisual();
    }

    private void UpdateSweep()
    {
        if (IsShimmering)
        {
            _sweepLoop.Start();
        }
        else
        {
            _sweepLoop.Stop();
            _sweep = 0;
        }
    }

    private bool StepSweep(double dt)
    {
        if (_rest > 0)
        {
            _rest -= dt;
            if (_rest > 0)
            {
                return true;
            }
            dt = -_rest;
            _rest = 0;
            if (_sweep >= 1)
            {
                _sweep = 0;
            }
        }
        _sweep = Math.Min(1, _sweep + dt / SweepDuration.TotalSeconds);
        if (_sweep >= 1)
        {
            _rest = SweepDuration.TotalSeconds * 0.32;
        }
        InvalidateVisual();
        return true;
    }

    // Streaming ---------------------------------------------------------------------------------------------------------

    /// <summary>Streams the words of the full text from <paramref name="fromIndex"/> on (characters before it show at once).</summary>
    public void Stream(int fromIndex = 0)
    {
        var text = FullText;
        _chunks.Clear();
        _streamStart = Math.Clamp(fromIndex, 0, text.Length);
        // Chunks of 3, 4, 5, 3, 4, 5 … words; each keeps the space before its first word.
        var sizes = 0;
        var i = _streamStart;
        while (i < text.Length)
        {
            var words = 3 + sizes % 3;
            sizes++;
            var start = i;
            var seen = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                if (i >= text.Length)
                {
                    break;
                }
                if (seen == words)
                {
                    break;
                }
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }
                seen++;
            }
            _chunks.Add(new Chunk { Start = start, Length = i - start });
        }
        _revealed = 0;
        _streamClock = 0;
        _streaming = _chunks.Count > 0;
        var level = ArcMotion.GetEffectiveLevel(this);
        _streamFades = level == ArcMotionLevel.Full;
        if (!_streaming || level == ArcMotionLevel.Off || !IsLoaded)
        {
            FinishStream(raise: true);
            return;
        }
        // The first chunk shows right away (14ms after the row mounts on the reference: the next frame).
        RevealNext();
        _streamLoop.Start();
        InvalidateVisual();
    }

    /// <summary>Shows every streamed word at once.</summary>
    public void FinishStream(bool raise)
    {
        var was = _streaming;
        _streaming = false;
        _chunks.Clear();
        _streamLoop.Stop();
        InvalidateVisual();
        if (raise && (was || _streamStart >= 0))
        {
            _streamStart = -1;
            StreamCompleted?.Invoke(this, EventArgs.Empty);
        }
        _streamStart = -1;
    }

    private void RevealNext()
    {
        if (_revealed < _chunks.Count)
        {
            _chunks[_revealed].RevealedAt = _streamClock;
            _revealed++;
        }
    }

    private bool StepStream(double dt)
    {
        if (!_streaming)
        {
            return false;
        }
        _streamClock += dt;
        var interval = AgentPlanMotion.ChunkInterval.TotalSeconds;
        while (_revealed < _chunks.Count && _streamClock >= _revealed * interval)
        {
            RevealNext();
        }
        InvalidateVisual();
        if (_revealed >= _chunks.Count)
        {
            var last = _chunks[^1].RevealedAt;
            var settle = Math.Max(AgentPlanMotion.StreamSettle.TotalSeconds, _streamFades ? AgentPlanMotion.ChunkFade.TotalSeconds : 0);
            if (_streamClock - last >= settle)
            {
                FinishStream(raise: true);
                return false;
            }
        }
        return true;
    }

    // Layout --------------------------------------------------------------------------------------------------------------

    private void EnsureLayout(double width)
    {
        if (!_layoutDirty && _layout is not null && Math.Abs(_layoutWidth - width) < 0.01)
        {
            return;
        }
        _layoutDirty = false;
        _layoutWidth = width;
        _layout?.Dispose();
        _baseLayout?.Dispose();
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var text = FullText;
        IReadOnlyList<ValueSpan<TextRunProperties>>? overrides = null;
        IReadOnlyList<ValueSpan<TextRunProperties>>? baseOverrides = null;
        if (!string.IsNullOrEmpty(Prefix))
        {
            var bold = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium);
            var prefixBrush = PrefixForeground ?? _brush;
            overrides = [new ValueSpan<TextRunProperties>(0, Prefix.Length, new GenericTextRunProperties(bold, FontSize, foregroundBrush: prefixBrush))];
            baseOverrides = overrides;
        }
        _layout = Create(text, typeface, _brush, width, overrides);
        _baseLayout = Create(text, typeface, _baseBrush, width, baseOverrides);
        foreach (var chunk in _chunks)
        {
            chunk.Rects = null;
        }
    }

    private TextLayout Create(string text, Typeface typeface, IBrush brush, double width, IReadOnlyList<ValueSpan<TextRunProperties>>? overrides) =>
        new(text, typeface, FontSize, brush, TextAlignment.Left, TextWrapping.Wrap, TextTrimming.None, null, FlowDirection.LeftToRight,
            double.IsFinite(width) ? width : double.PositiveInfinity, double.PositiveInfinity, LineHeight, LetterSpacing, 0, null, overrides);

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayout(availableSize.Width);
        var layout = _layout!;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : layout.WidthIncludingTrailingWhitespace;
        return new Size(width, layout.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // As the title field's TextPresenter does: the arranged width, rounded up, is the wrapping width.
        EnsureLayout(Math.Ceiling(finalSize.Width));
        return finalSize;
    }

    // Render --------------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext context)
    {
        if (_layout is null || _baseLayout is null)
        {
            return;
        }
        if (_streaming)
        {
            RenderStream(context);
            return;
        }
        var settle = Math.Clamp(_settle.Value, 0, 1);
        if (settle >= 0.999)
        {
            _layout.Draw(context, default);
            return;
        }
        _baseLayout.Draw(context, default);
        if (settle > 0.001)
        {
            using (context.PushOpacity(settle))
            {
                _layout.Draw(context, default);
            }
        }
        if (IsShimmering && _sweepLoop.IsRunning && _sweep > 0 && _sweep < 1)
        {
            DrawBand(context, 1 - settle);
        }
    }

    private void DrawBand(DrawingContext context, double strength)
    {
        var em = FontSize;
        if (_band is null || Math.Abs(_bandEm - em) > 0.01)
        {
            _bandEm = em;
            // Bell falloff around the centre: 0% at ±2.2em, 50% at ±1.1em, 90% at ±0.44em, 100% at the centre.
            _band = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(0, 0, 0, 0)),
                new ImmutableGradientStop(0.25, Color.FromArgb(128, 0, 0, 0)),
                new ImmutableGradientStop(0.4, Color.FromArgb(230, 0, 0, 0)),
                new ImmutableGradientStop(0.5, Color.FromArgb(255, 0, 0, 0)),
                new ImmutableGradientStop(0.6, Color.FromArgb(230, 0, 0, 0)),
                new ImmutableGradientStop(0.75, Color.FromArgb(128, 0, 0, 0)),
                new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))
            ], startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));
        }
        // The lines read as one: the band crosses the first line, then the next (CSS slices an inline background so).
        var lines = _layout!.TextLines;
        var total = 0.0;
        foreach (var line in lines)
        {
            total += line.WidthIncludingTrailingWhitespace;
        }
        var center = _sweep * total + (2 * _sweep - 1) * BandEm * em;
        var before = 0.0;
        var top = 0.0;
        using var _ = context.PushOpacity(strength);
        foreach (var line in lines)
        {
            var width = line.WidthIncludingTrailingWhitespace;
            var local = center - before;
            if (local + BandEm * em > 0 && local - BandEm * em < width)
            {
                var band = new Rect(local - BandEm * em, top, 2 * BandEm * em, line.Height);
                var clip = band.Intersect(new Rect(0, top, width, line.Height));
                if (clip.Width > 0)
                {
                    using (context.PushClip(clip))
                    using (context.PushOpacityMask(_band, band))
                    {
                        _layout.Draw(context, default);
                    }
                }
            }
            before += width;
            top += line.Height;
        }
    }

    private void RenderStream(DrawingContext context)
    {
        var layout = _layout!;
        if (_streamStart > 0)
        {
            foreach (var rect in RectsOf(0, _streamStart))
            {
                using (context.PushClip(rect))
                {
                    layout.Draw(context, default);
                }
            }
        }
        var fade = AgentPlanMotion.ChunkFade.TotalSeconds;
        for (var i = 0; i < _revealed; i++)
        {
            var chunk = _chunks[i];
            chunk.Rects ??= RectsOf(chunk.Start, chunk.Length);
            var opacity = _streamFades ? ArcEasings.Standard.Ease(Math.Clamp((_streamClock - chunk.RevealedAt) / fade, 0, 1)) : 1;
            if (opacity <= 0.001)
            {
                continue;
            }
            using (context.PushOpacity(opacity))
            {
                foreach (var rect in chunk.Rects)
                {
                    using (context.PushClip(rect))
                    {
                        layout.Draw(context, default);
                    }
                }
            }
        }
    }

    private Rect[] RectsOf(int start, int length)
    {
        // Inflate up and down so ascenders and descenders are not cut; the sides stay at the characters' edges.
        return _layout!.HitTestTextRange(start, length).Select(r => new Rect(r.X, r.Y - 2, r.Width, r.Height + 4)).ToArray();
    }
}
