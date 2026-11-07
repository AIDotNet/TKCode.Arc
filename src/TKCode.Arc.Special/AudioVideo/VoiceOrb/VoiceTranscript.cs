using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Live captions for a voice conversation (Arc Voice orb, design reference): one centred line, two at most in view,
/// where each new word rises out of a blur. Older lines slide up and fade under the top edge; a new turn fades the old
/// line out upward and brings the new one in.
/// </summary>
/// <remarks>
/// Append words to <see cref="Text"/> as they are recognized or spoken; only the new words animate, and a text that does
/// not extend the previous one is replaced without entrances. Change <see cref="Turn"/> to start a new turn (set it
/// together with the new speaker and text). Screen readers hear only whole sentences: set <see cref="IsComplete"/> when
/// the turn's text is final. Under reduced motion every change is a short fade.
/// </remarks>
public class VoiceTranscript : Control
{
    /// <summary>The words of the current turn, separated by spaces. Appending words animates only the new ones.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<VoiceTranscript, string?>(nameof(Text), "");

    /// <summary>The turn number: a new value starts a new turn (the old line leaves, the new one enters).</summary>
    public static readonly StyledProperty<int> TurnProperty =
        AvaloniaProperty.Register<VoiceTranscript, int>(nameof(Turn));

    /// <summary>Who speaks this turn: the assistant (foreground), the user (secondary) or the system (smaller, muted).</summary>
    public static readonly StyledProperty<VoiceTranscriptSpeaker> SpeakerProperty =
        AvaloniaProperty.Register<VoiceTranscript, VoiceTranscriptSpeaker>(nameof(Speaker));

    /// <summary>Whether the turn's text is final: screen readers then hear the whole sentence once.</summary>
    public static readonly StyledProperty<bool> IsCompleteProperty =
        AvaloniaProperty.Register<VoiceTranscript, bool>(nameof(IsComplete));

    /// <summary>How many lines stay in view (2 by default); older lines slide out under the top fade.</summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<VoiceTranscript, int>(nameof(MaxLines), 2, validate: v => v >= 1);

    internal static readonly StyledProperty<IBrush?> AssistantInkProperty =
        AvaloniaProperty.Register<VoiceTranscript, IBrush?>(nameof(AssistantInk));

    internal static readonly StyledProperty<IBrush?> UserInkProperty =
        AvaloniaProperty.Register<VoiceTranscript, IBrush?>(nameof(UserInk));

    internal static readonly StyledProperty<IBrush?> SystemInkProperty =
        AvaloniaProperty.Register<VoiceTranscript, IBrush?>(nameof(SystemInk));

    internal static readonly StyledProperty<FontFamily?> CaptionFontFamilyProperty =
        AvaloniaProperty.Register<VoiceTranscript, FontFamily?>(nameof(CaptionFontFamily));

    internal static readonly StyledProperty<double> CaptionFontSizeProperty =
        AvaloniaProperty.Register<VoiceTranscript, double>(nameof(CaptionFontSize), 16);

    internal static readonly StyledProperty<double> SystemFontSizeProperty =
        AvaloniaProperty.Register<VoiceTranscript, double>(nameof(SystemFontSize), 14);

    internal static readonly StyledProperty<double> CaptionLineHeightProperty =
        AvaloniaProperty.Register<VoiceTranscript, double>(nameof(CaptionLineHeight), 22.4);

    internal static readonly StyledProperty<double> SystemLineHeightProperty =
        AvaloniaProperty.Register<VoiceTranscript, double>(nameof(SystemLineHeight), 19.6);

    internal static readonly StyledProperty<double> CaptionLetterSpacingProperty =
        AvaloniaProperty.Register<VoiceTranscript, double>(nameof(CaptionLetterSpacing), -0.16);

    internal static readonly StyledProperty<double> FadeStopProperty =
        AvaloniaProperty.Register<VoiceTranscript, double>(nameof(FadeStop), 0.4);

    private readonly ExplainerAnnouncer _announcer = new();
    private readonly LiveLoop _loop;
    private readonly List<VoiceTranscriptBlock> _exiting = new();
    private VoiceTranscriptBlock? _current;
    private double _now;
    private bool _dirty = true;
    private string? _announced;
    private int _announcedTurn = int.MinValue;

    static VoiceTranscript()
    {
        FocusableProperty.OverrideDefaultValue<VoiceTranscript>(false);
        ClipToBoundsProperty.OverrideDefaultValue<VoiceTranscript>(true);
        AffectsMeasure<VoiceTranscript>(MaxLinesProperty, CaptionLineHeightProperty);
    }

    /// <summary>Creates an empty transcript.</summary>
    public VoiceTranscript()
    {
        IsHitTestVisible = false;
        // Why code and not the ControlTheme: every word has its own entrance clock, the block's layout spring keeps its
        // velocity when lines arrive in quick succession, and turns cross over; none of it is a single property.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        LogicalChildren.Add(_announcer);
        VisualChildren.Add(_announcer);
        UpdateMask();
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="TurnProperty"/>
    public int Turn
    {
        get => GetValue(TurnProperty);
        set => SetValue(TurnProperty, value);
    }

    /// <inheritdoc cref="SpeakerProperty"/>
    public VoiceTranscriptSpeaker Speaker
    {
        get => GetValue(SpeakerProperty);
        set => SetValue(SpeakerProperty, value);
    }

    /// <inheritdoc cref="IsCompleteProperty"/>
    public bool IsComplete
    {
        get => GetValue(IsCompleteProperty);
        set => SetValue(IsCompleteProperty, value);
    }

    /// <inheritdoc cref="MaxLinesProperty"/>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    internal IBrush? AssistantInk { get => GetValue(AssistantInkProperty); set => SetValue(AssistantInkProperty, value); }

    internal IBrush? UserInk { get => GetValue(UserInkProperty); set => SetValue(UserInkProperty, value); }

    internal IBrush? SystemInk { get => GetValue(SystemInkProperty); set => SetValue(SystemInkProperty, value); }

    internal FontFamily? CaptionFontFamily { get => GetValue(CaptionFontFamilyProperty); set => SetValue(CaptionFontFamilyProperty, value); }

    internal double CaptionFontSize { get => GetValue(CaptionFontSizeProperty); set => SetValue(CaptionFontSizeProperty, value); }

    internal double SystemFontSize { get => GetValue(SystemFontSizeProperty); set => SetValue(SystemFontSizeProperty, value); }

    internal double CaptionLineHeight { get => GetValue(CaptionLineHeightProperty); set => SetValue(CaptionLineHeightProperty, value); }

    internal double SystemLineHeight { get => GetValue(SystemLineHeightProperty); set => SetValue(SystemLineHeightProperty, value); }

    internal double CaptionLetterSpacing { get => GetValue(CaptionLetterSpacingProperty); set => SetValue(CaptionLetterSpacingProperty, value); }

    internal double FadeStop { get => GetValue(FadeStopProperty); set => SetValue(FadeStopProperty, value); }

    /// <summary>The current turn's block (tests).</summary>
    internal VoiceTranscriptBlock? Current
    {
        get
        {
            Reconcile();
            return _current;
        }
    }

    /// <summary>Blocks still leaving (tests).</summary>
    internal int ExitingCount => _exiting.Count;

    /// <summary>The live region (tests).</summary>
    internal ExplainerAnnouncer Announcer => _announcer;

    /// <summary>Advances the animations by <paramref name="dt"/> seconds (the frame loop calls it; tests may too).</summary>
    internal bool Step(double dt)
    {
        _now += dt;
        var full = ArcMotion.IsFull(this);
        var moving = false;
        if (_current is not null)
        {
            moving |= _current.Apply(_now, dt, full);
        }
        for (var i = _exiting.Count - 1; i >= 0; i--)
        {
            var block = _exiting[i];
            moving |= block.Apply(_now, dt, full);
            if (block.IsGone)
            {
                _exiting.RemoveAt(i);
                VisualChildren.Remove(block);
            }
        }
        return moving;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TextProperty || property == TurnProperty || property == SpeakerProperty)
        {
            _dirty = true;
            InvalidateMeasure();
            if (property != SpeakerProperty)
            {
                Announce();
            }
        }
        else if (property == IsCompleteProperty)
        {
            Announce();
        }
        else if (property == AssistantInkProperty || property == UserInkProperty || property == SystemInkProperty ||
                 property == CaptionFontFamilyProperty || property == CaptionFontSizeProperty ||
                 property == SystemFontSizeProperty || property == CaptionLetterSpacingProperty ||
                 property == SystemLineHeightProperty || property == CaptionLineHeightProperty)
        {
            _current?.Restyle();
            foreach (var block in _exiting)
            {
                block.Restyle();
            }
        }
        else if (property == FadeStopProperty || property == MaxLinesProperty)
        {
            UpdateMask();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Reconcile();
        var width = availableSize.Width;
        _announcer.Measure(availableSize);
        foreach (var block in _exiting)
        {
            block.Measure(new Size(double.IsInfinity(width) ? (block.DesiredSize.Width > 0 ? block.DesiredSize.Width : 0) : width, double.PositiveInfinity));
        }
        if (_current is not null)
        {
            _current.Measure(new Size(width, double.PositiveInfinity));
            if (double.IsInfinity(width))
            {
                width = _current.DesiredSize.Width;
            }
        }
        if (double.IsInfinity(width))
        {
            width = 0;
        }
        return new Size(width, MaxLines * CaptionLineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _announcer.Arrange(new Rect(0, 0, 0, 0));
        foreach (var block in _exiting)
        {
            Place(block, finalSize);
        }
        if (_current is not null)
        {
            Place(_current, finalSize);
        }
        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_current is not null && NeedsFrames())
        {
            _loop.Start();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new VoiceTranscriptAutomationPeer(this);

    internal void OnBlockGrew(VoiceTranscriptBlock block, int lines, double lineHeight)
    {
        if (!ReferenceEquals(block, _current) || !CanAnimate())
        {
            return;
        }
        // Bottom-aligned: a new line pushes the block up, so it starts one line lower and springs into place.
        block.Kick(lines * lineHeight);
        _loop.Start();
    }

    private static void Place(VoiceTranscriptBlock block, Size size)
    {
        // Lines sit at the bottom; when there are more than fit, the oldest pass out of the top.
        var height = block.BlockHeight;
        block.Arrange(new Rect(0, size.Height - height, size.Width, height));
    }

    private void Reconcile()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        var words = VoiceTranscriptLayout.Words(Text);
        var animate = CanAnimate();
        if (_current is null || _current.Turn != Turn)
        {
            if (_current is not null)
            {
                if (animate && _current.WordCount > 0)
                {
                    _current.ExitBorn = _now;
                    _exiting.Add(_current);
                }
                else
                {
                    VisualChildren.Remove(_current);
                }
            }
            var first = _current is null;
            _current = new VoiceTranscriptBlock(this, Turn, Speaker);
            VisualChildren.Insert(0, _current);
            // The first turn just appears; later ones fade in. Words already in the text come with the block.
            _current.Sync(words, _now, false);
            if (!first && animate)
            {
                _current.EnterBorn = _now;
            }
        }
        else
        {
            _current.SetSpeaker(Speaker);
            _current.Sync(words, _now, animate);
        }
        if (animate)
        {
            _loop.Start();
        }
        else
        {
            _current.Settle();
            foreach (var block in _exiting)
            {
                VisualChildren.Remove(block);
            }
            _exiting.Clear();
            _current.Apply(_now, 0, false);
        }
    }

    private bool CanAnimate() => VisualRoot is not null && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;

    private bool NeedsFrames() => _exiting.Count > 0 || _current is not null;

    private void Announce()
    {
        if (!IsComplete)
        {
            return;
        }
        var text = Text;
        if (string.IsNullOrWhiteSpace(text) || (text == _announced && Turn == _announcedTurn))
        {
            return;
        }
        _announced = text;
        _announcedTurn = Turn;
        _announcer.Announce(text.Trim());
    }

    private void UpdateMask()
    {
        // The top of the box fades out (transparent to opaque over the first 40 %), so lines leave softly.
        var stop = Math.Clamp(FadeStop, 0, 1);
        OpacityMask = new ImmutableLinearGradientBrush(
            gradientStops: [new ImmutableGradientStop(0, Colors.Transparent), new ImmutableGradientStop(stop, Colors.Black)],
            startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
            endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
    }

    private sealed class VoiceTranscriptAutomationPeer(VoiceTranscript owner) : ControlAutomationPeer(owner)
    {
        // The animated words are hidden from assistive technology; the live region speaks whole sentences.
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}
