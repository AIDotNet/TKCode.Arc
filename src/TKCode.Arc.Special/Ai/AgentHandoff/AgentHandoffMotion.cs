using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The measured motion numbers of <see cref="AgentHandoff"/> (Arc's Motion section).</summary>
internal static class AgentHandoffMotion
{
    /// <summary>The thread's height follows its content on a bounce-free 0.24s spring.</summary>
    public static readonly SpringSpec ThreadSpring = SpringSpec.FromVisual(0.24, 0);

    /// <summary>Each streamed chunk only fades in, over 140ms.</summary>
    public const double ChunkFadeSeconds = 0.14;

    /// <summary>The simulation streams a chunk about every 34ms.</summary>
    public const double ChunkIntervalSeconds = 0.034;

    /// <summary>A chunk is 3 to 8 words.</summary>
    public const int ChunkMinWords = 3;

    public const int ChunkMaxWords = 8;

    /// <summary>One trip of the travel dot down the handoff card's timeline (interpretation; Arc gives no number).</summary>
    public const double TravelSeconds = 1.6;

    /// <summary>The typing dots breathe once every 1.2s, 160ms apart.</summary>
    public const double DotsPeriod = 1.2;

    public const double DotsStagger = 0.16;
}

/// <summary>
/// The text of one bubble: the shared <see cref="AiStreamText"/>, as wide as its longest line like a text block. When the
/// message streams, each appended chunk fades from transparent to the text color over 140ms (linear; the rest of the text
/// never changes); the glyphs themselves fade, so it reads the same on every bubble fill. Only at full motion; reduced
/// motion and off show each chunk at once. The fades pause and finish off screen, and a frame neither lays the text out
/// nor allocates.
/// </summary>
internal sealed class AgentHandoffStreamText : AiStreamText
{
    public AgentHandoffStreamText()
    {
        FadeDuration = TimeSpan.FromSeconds(AgentHandoffMotion.ChunkFadeSeconds);
        FadeEasing = null;
        SizesToText = true;
    }

    /// <summary>The whole text on screen (settled and fading).</summary>
    public string Shown => Text;

    /// <summary>Shows <paramref name="text"/>; when it extends the shown text and <paramref name="fade"/> is set, the new part fades in.</summary>
    public void Show(string? text, bool fade)
    {
        text ??= string.Empty;
        var shown = Text;
        if (text == shown)
        {
            return;
        }
        if (fade && shown.Length > 0 && text.StartsWith(shown, StringComparison.Ordinal) && ArcMotion.IsFull(this))
        {
            Append(text[shown.Length..], fade: true);
            return;
        }
        SetText(text);
    }

    // Read as text, as the text block it replaces was (the bubble also carries the message as its name).
    protected override AutomationPeer OnCreateAutomationPeer() => new TextPeer(this);

    private sealed class TextPeer(AgentHandoffStreamText owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner) ?? ((AgentHandoffStreamText)Owner).Text;

        protected override bool IsControlElementCore() => Owner.TemplatedParent is null && base.IsControlElementCore();
    }
}

/// <summary>
/// Three dots that breathe one after another while a person types: the shared <see cref="AiTypingDots"/> with the
/// handoff's numbers (1.2s, 160ms apart; opacity 0.35 to 1 and 2px up at 30%). They move only when on screen at full
/// motion; otherwise they hold still at 0.6.
/// </summary>
internal sealed class AgentHandoffTypingDots : AiTypingDots
{
    public AgentHandoffTypingDots()
    {
        Period = AgentHandoffMotion.DotsPeriod;
        Stagger = AgentHandoffMotion.DotsStagger;
        Lift = 2;
    }

    /// <summary>Opacity and lift of dot <paramref name="index"/> at <paramref name="time"/> seconds.</summary>
    internal static (double Opacity, double Lift) Sample(double time, int index) =>
        AiTypingDots.Sample(time, index, AgentHandoffMotion.DotsPeriod, AgentHandoffMotion.DotsStagger, 2);
}

/// <summary>
/// The pending stretch of the timeline beside the handoff card while it transfers: a faint hairline from the provider's
/// mark down to the bottom of the card, with a small accent dot travelling down it once every 1.6s (ease in-out, fading
/// in and out at the ends). Drawn in <see cref="Render"/> on a <see cref="LiveLoop"/>, which pauses off screen and does
/// not run with reduced motion or motion off: then there is no travel dot, only the hairline.
/// </summary>
internal sealed class AgentHandoffTravelDot : Control
{
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<AgentHandoffTravelDot, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<AgentHandoffTravelDot, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<AgentHandoffTravelDot, double>(nameof(DotSize), 6);

    public static readonly StyledProperty<double> TrackWidthProperty =
        AvaloniaProperty.Register<AgentHandoffTravelDot, double>(nameof(TrackWidth), 1);

    public static readonly StyledProperty<bool> IsTravellingProperty =
        AvaloniaProperty.Register<AgentHandoffTravelDot, bool>(nameof(IsTravelling));

    private readonly LiveLoop _loop;
    private double _time;
    private bool _running;
    private ImmutablePen? _pen;

    static AgentHandoffTravelDot()
    {
        AffectsRender<AgentHandoffTravelDot>(DotBrushProperty, TrackBrushProperty, DotSizeProperty, TrackWidthProperty);
    }

    public AgentHandoffTravelDot()
    {
        IsHitTestVisible = false;
        _loop = new LiveLoop(this, seconds =>
        {
            _time = (_time + seconds) % AgentHandoffMotion.TravelSeconds;
            InvalidateVisual();
            return true;
        });
        _loop.RunningChanged += running =>
        {
            _running = running;
            InvalidateVisual();
        };
    }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    public double TrackWidth { get => GetValue(TrackWidthProperty); set => SetValue(TrackWidthProperty, value); }

    /// <summary>Whether the dot should travel (the card is transferring).</summary>
    public bool IsTravelling { get => GetValue(IsTravellingProperty); set => SetValue(IsTravellingProperty, value); }

    /// <summary>True while the dot is moving (tests).</summary>
    public bool IsMoving => _running;

    /// <summary>Where the dot is (0 top, 1 bottom) and its opacity, <paramref name="time"/> seconds into a trip.</summary>
    internal static (double Position, double Opacity) Sample(double time)
    {
        var phase = time % AgentHandoffMotion.TravelSeconds / AgentHandoffMotion.TravelSeconds;
        var position = phase < 0.5 ? 4 * phase * phase * phase : 1 - Math.Pow(-2 * phase + 2, 3) / 2;
        var opacity = phase < 0.15 ? phase / 0.15 : phase > 0.85 ? (1 - phase) / 0.15 : 1;
        return (position, opacity);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsTravellingProperty)
        {
            if (IsTravelling)
            {
                _time = 0;
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
            InvalidateVisual();
        }
        else if (change.Property == TrackBrushProperty || change.Property == TrackWidthProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(DotSize, 0);

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var x = bounds.Width / 2;
        if (TrackBrush is { } track && bounds.Height > 0)
        {
            _pen ??= new ImmutablePen(track.ToImmutable(), TrackWidth);
            context.DrawLine(_pen, new Point(x, 0), new Point(x, bounds.Height));
        }
        if (!_running || !IsTravelling || DotBrush is not { } dot)
        {
            return;
        }
        var r = DotSize / 2;
        var (position, opacity) = Sample(_time);
        var y = r + position * Math.Max(0, bounds.Height - DotSize);
        using (context.PushOpacity(opacity))
        {
            context.DrawEllipse(dot, null, new Point(x, y), r, r);
        }
    }
}
