using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A headline that reveals itself once as it appears (Arc TextReveal): each word rises out of its own feathered
/// clip while it sharpens from a soft blur and fades in, word after word (P20, P29). A title that mounts on screen
/// starts with the first frame like Arc; one that mounts below the fold waits until it enters the viewport
/// (S6, once). Whenever the animation cannot run (motion off, detached), the text shows at rest.
/// Use <c>\n</c> for a deliberate line break; lines are at most <see cref="MaxWidthInChars"/> wide and balanced.
/// Drawn as one control: no per-word controls.
/// </summary>
public class TextReveal : Control
{
    /// <summary>The headline; use <c>\n</c> for a deliberate line break.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TextReveal, string?>(nameof(Text));

    /// <summary>Heading level (Arc <c>as</c>, default h2); a paragraph uses body type and a softer blur.</summary>
    public static readonly StyledProperty<TextLevel> LevelProperty =
        AvaloniaProperty.Register<TextReveal, TextLevel>(nameof(Level), TextLevel.H2);

    /// <summary>Wait before the first word rises.</summary>
    public static readonly StyledProperty<TimeSpan> DelayProperty =
        AvaloniaProperty.Register<TextReveal, TimeSpan>(nameof(Delay));

    /// <summary>Line length limit in "0" glyph widths (Arc: 24ch). 0 disables the limit.</summary>
    public static readonly StyledProperty<double> MaxWidthInCharsProperty =
        AvaloniaProperty.Register<TextReveal, double>(nameof(MaxWidthInChars), 24);

    /// <summary>Letter spacing in em (display tracking for headings).</summary>
    public static readonly StyledProperty<double> TrackingProperty =
        AvaloniaProperty.Register<TextReveal, double>(nameof(Tracking));

    /// <summary>Line height as a multiple of the font size.</summary>
    public static readonly StyledProperty<double> LeadingProperty =
        AvaloniaProperty.Register<TextReveal, double>(nameof(Leading), 1.1);

    /// <summary>Starting blur of each word (BlurText for headings, BlurSoft for paragraphs).</summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<TextReveal, double>(nameof(BlurRadius), 8);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<TextReveal>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<TextReveal>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<TextReveal>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<TextReveal>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public TextLevel Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public TimeSpan Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }
    public double MaxWidthInChars { get => GetValue(MaxWidthInCharsProperty); set => SetValue(MaxWidthInCharsProperty, value); }
    public double Tracking { get => GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    public double Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    internal const double RiseSeconds = 0.76;
    internal const double FadeSeconds = 0.44;
    internal const double FocusSeconds = 0.58;
    internal const double RiseEm = 0.62;

    private readonly TextEffectsWordLayout _layout = new();
    private MotionValue<double>? _clock;
    private ViewportVisibility? _visibility;
    private bool _waiting;
    private bool _playing;
    private bool _full;
    private double[] _delays = Array.Empty<double>();
    private double _total;
    private IBrush? _mask;
    private double _maskHeight;
    private static readonly Easing s_linear = new LinearEasing();

    static TextReveal()
    {
        AffectsMeasure<TextReveal>(TextProperty, MaxWidthInCharsProperty, TrackingProperty, LeadingProperty,
            FontFamilyProperty, FontSizeProperty, FontWeightProperty);
        AffectsRender<TextReveal>(ForegroundProperty, BlurRadiusProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<TextReveal>(AutomationControlType.Text);
    }

    /// <summary>True while the entrance is running.</summary>
    public bool IsRevealing => _playing;

    /// <summary>True while the text waits (hidden) to scroll into view.</summary>
    internal bool IsWaiting => _waiting;

    /// <summary>Plays the entrance again (Arc replays by remounting with a new key).</summary>
    public void Replay()
    {
        if (TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        Play();
    }

    /// <summary>Per-word stagger: 40ms, shortened so the total stagger stays under the considered duration (0.48s).</summary>
    internal static double StepFor(int wordCount) => Math.Min(ArcMotionPresets.StaggerWord.TotalSeconds,
        ArcMotionPresets.Considered.TotalSeconds / Math.Max(wordCount, 1));

    /// <summary>Delay of each word: delay + position × step + forced line index × step.</summary>
    internal static double[] ComputeDelays(string text, double delay)
    {
        var lines = TextEffectsWordLayout.Split(text);
        var count = lines.Sum(l => l.Length);
        var step = StepFor(count);
        var delays = new double[count];
        var position = 0;
        for (var line = 0; line < lines.Count; line++)
        {
            for (var w = 0; w < lines[line].Length; w++)
            {
                delays[position] = delay + position * step + line * step;
                position++;
            }
        }
        return delays;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == LevelProperty)
        {
            UpdateAutomation();
        }
        if (change.Property == TextProperty && TopLevel.GetTopLevel(this) is not null && !_waiting)
        {
            Play();
        }
        else if (change.Property == ForegroundProperty)
        {
            _layout.Invalidate();
            InvalidateMeasure();
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, (Text ?? string.Empty).Replace("\r\n", " ").Replace('\n', ' '));
        AutomationProperties.SetHeadingLevel(this, Level switch
        {
            TextLevel.H1 => 1,
            TextLevel.H2 => 2,
            TextLevel.H3 => 3,
            _ => 0
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _waiting = false;
            return;
        }
        // Hidden until the first viewport report (raised during layout, before the first frame is drawn).
        _waiting = true;
        _visibility?.Dispose();
        _visibility = new ViewportVisibility(this, 0.01, once: true);
        _visibility.VisibilityChanged += OnVisibilityChanged;
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (visible && _waiting)
        {
            _waiting = false;
            Play();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        _waiting = false;
        _clock?.Stop();
        _playing = false;
    }

    private void Play()
    {
        _waiting = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        _delays = ComputeDelays(Text ?? string.Empty, Delay.TotalSeconds);
        if (level == ArcMotionLevel.Off || _delays.Length == 0)
        {
            _playing = false;
            InvalidateVisual();
            return;
        }
        _full = level == ArcMotionLevel.Full;
        _total = _delays.Max() + (_full ? RiseSeconds : ArcMotionPresets.Fast.TotalSeconds);
        if (_clock is null)
        {
            _clock = new MotionValue<double>(this, 0, MotionVectors.Double);
            _clock.Changed += elapsed =>
            {
                // The timeline lands exactly on its total: from then on words are drawn plain.
                if (_playing && elapsed >= _total)
                {
                    _playing = false;
                }
                InvalidateVisual();
            };
        }
        _playing = false;
        _clock.Jump(0);
        _playing = true;
        _ = _clock.AnimateTo(_total, MotionSpec.Tween(TimeSpan.FromSeconds(_total), s_linear));
        InvalidateVisual();
    }

    private TextEffectsStyle Style() => new(new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize,
        Foreground ?? Brushes.Black, Tracking * FontSize, Leading * FontSize);

    protected override Size MeasureOverride(Size availableSize)
    {
        var style = Style();
        var limit = availableSize.Width;
        if (MaxWidthInChars > 0)
        {
            var ch = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                style.Typeface, FontSize, null).WidthIncludingTrailingWhitespace;
            limit = Math.Min(limit, MaxWidthInChars * ch);
        }
        if (_layout.BuildWords(Text ?? string.Empty, style, limit, balance: true, letters: false))
        {
            _mask = null;
        }
        return _layout.Size;
    }

    public override void Render(DrawingContext context)
    {
        var words = _layout.Words;
        if (words.Count == 0)
        {
            return;
        }
        var elapsed = _clock?.Value ?? double.MaxValue;
        var playing = _playing && _clock is not null;
        var em = FontSize;
        var lineHeight = _layout.LineHeight;
        var clipHeight = lineHeight + 0.34 * em;
        if (_mask is null || Math.Abs(_maskHeight - clipHeight) > 0.01)
        {
            _maskHeight = clipHeight;
            var solid = Math.Clamp((clipHeight - 0.18 * em) / clipHeight, 0, 1);
            _mask = new ImmutableLinearGradientBrush(
                [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(solid, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent)],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        }
        if (_waiting)
        {
            return;
        }
        foreach (var word in words)
        {
            var origin = new Point(word.X, word.Y);
            if (!playing || word.Index >= _delays.Length)
            {
                word.Layout.Draw(context, origin);
                continue;
            }
            var delay = _delays[word.Index];
            if (!_full)
            {
                // Reduced motion: words only fade in, still staggered.
                var fade = TextEffectsTiming.Track(elapsed, delay, ArcMotionPresets.Fast.TotalSeconds, ArcEasings.Standard);
                if (fade <= 0.001)
                {
                    continue;
                }
                using (context.PushOpacity(fade))
                {
                    word.Layout.Draw(context, origin);
                }
                continue;
            }
            var rise = TextEffectsTiming.Track(elapsed, delay, RiseSeconds, ArcEasings.Enter);
            var opacity = TextEffectsTiming.Track(elapsed, delay, FadeSeconds, ArcEasings.Standard);
            var focus = TextEffectsTiming.Track(elapsed, delay, FocusSeconds, ArcEasings.Enter);
            if (rise >= 1 && opacity >= 1 && focus >= 1)
            {
                word.Layout.Draw(context, origin);
                continue;
            }
            if (opacity <= 0.001)
            {
                continue;
            }
            var clip = new Rect(word.X - 0.16 * em, word.Y - 0.1 * em, word.Width + 0.32 * em, clipHeight);
            var dy = RiseEm * em * (1 - rise);
            using (context.PushClip(clip))
            using (context.PushOpacityMask(_mask, clip))
            using (context.PushOpacity(opacity))
            {
                var effect = TextEffectsBlur.Get(BlurRadius * (1 - focus));
                if (effect is null)
                {
                    word.Layout.Draw(context, new Point(word.X, word.Y + dy));
                }
                else
                {
                    using (context.PushEffect(effect, clip))
                    {
                        word.Layout.Draw(context, new Point(word.X, word.Y + dy));
                    }
                }
            }
        }
    }
}
