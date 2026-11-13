using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Where a chaptered explainer's story stands, as its status line shows it.</summary>
internal enum ExplainerChapterPhase
{
    Ready,
    Playing,
    Paused,
    Done
}

/// <summary>
/// The card chrome shared by the chaptered explainers (<c>DnsResolution</c>, <c>ModelTraining</c>):
/// the status line, the caption that crossfades from chapter to chapter, and the 0.5× / 1× / 2× speed control in the
/// footer, kept in step with <see cref="Explainer.Speed"/>. Each explainer owns one and hands it its template.
/// </summary>
internal sealed class ExplainerChapterKit
{
    /// <summary>The status line in the header.</summary>
    public const string StatusPart = "PART_Status";

    /// <summary>The caption under the title.</summary>
    public const string CaptionPart = "PART_Caption";

    /// <summary>The speed control.</summary>
    public const string SpeedPart = "PART_Speed";

    /// <summary>The "Speed" words before the speed control.</summary>
    public const string SpeedLabelPart = "PART_SpeedLabel";

    private readonly Explainer _owner;
    private readonly SegmentedOption[] _speeds =
    [
        new SegmentedOption(0.5, "0.5×"),
        new SegmentedOption(1.0, "1×"),
        new SegmentedOption(2.0, "2×")
    ];

    private ExplainerStatus? _status;
    private ArcContentSwap? _caption;
    private SegmentedControl? _speed;
    private TextBlock? _speedLabel;
    private string? _captionText;
    private ExplainerChapterPhase? _phase;
    private string? _statusText;
    private bool _syncingSpeed;

    public ExplainerChapterKit(Explainer owner)
    {
        _owner = owner;
    }

    /// <summary>The caption shown now (tests).</summary>
    public string? CaptionText => _captionText;

    /// <summary>The status words shown now (tests).</summary>
    public string? StatusText => _statusText;

    /// <summary>The phase shown now.</summary>
    public ExplainerChapterPhase Phase => _phase ?? ExplainerChapterPhase.Ready;

    /// <summary>The speed control (tests).</summary>
    public SegmentedControl? SpeedControl => _speed;

    public void Apply(INameScope scope)
    {
        if (_speed is not null)
        {
            _speed.SelectionChanged -= OnSpeedSelectionChanged;
        }
        _status = scope.Find<ExplainerStatus>(StatusPart);
        _caption = scope.Find<ArcContentSwap>(CaptionPart);
        _speed = scope.Find<SegmentedControl>(SpeedPart);
        _speedLabel = scope.Find<TextBlock>(SpeedLabelPart);
        _captionText = null;
        _phase = null;
        if (_speed is not null)
        {
            _speed.ItemsSource = _speeds;
            _speed.SelectionChanged += OnSpeedSelectionChanged;
        }
        UpdateSpeed();
    }

    /// <summary>Names the speed control and its words.</summary>
    public void SetSpeedLabel(string text)
    {
        if (_speedLabel is not null)
        {
            _speedLabel.Text = text;
        }
        if (_speed is not null)
        {
            AutomationProperties.SetName(_speed, text);
        }
    }

    /// <summary>Hides the "Speed" words on narrow cards (the control keeps its name).</summary>
    public void SetSpeedLabelVisible(bool visible)
    {
        if (_speedLabel is not null)
        {
            _speedLabel.IsVisible = visible;
        }
    }

    /// <summary>Selects the option matching <see cref="Explainer.Speed"/> (none for other speeds).</summary>
    public void UpdateSpeed()
    {
        if (_speed is null)
        {
            return;
        }
        var index = -1;
        for (var i = 0; i < _speeds.Length; i++)
        {
            if (_speeds[i].Value is double value && Math.Abs(value - _owner.Speed) < 0.001)
            {
                index = i;
            }
        }
        _syncingSpeed = true;
        try
        {
            _speed.SelectedIndex = index;
        }
        finally
        {
            _syncingSpeed = false;
        }
    }

    private void OnSpeedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSpeed || _speed?.SelectedItem is not SegmentedOption { Value: double value })
        {
            return;
        }
        _owner.SetCurrentValue(Explainer.SpeedProperty, value);
    }

    /// <summary>The phase from the owner's playback state and whether its story has started.</summary>
    public ExplainerChapterPhase PhaseOf(bool started)
    {
        if (_owner.IsFinished)
        {
            return ExplainerChapterPhase.Done;
        }
        if (_owner.IsPlaying || _owner.IsStepping)
        {
            return ExplainerChapterPhase.Playing;
        }
        return started ? ExplainerChapterPhase.Paused : ExplainerChapterPhase.Ready;
    }

    /// <summary>Shows <paramref name="phase"/> with its words (unchanged words do nothing unless forced).</summary>
    public void SetStatus(ExplainerChapterPhase phase, string text, bool force = false)
    {
        if (phase == _phase && text == _statusText && !force)
        {
            return;
        }
        _phase = phase;
        _statusText = text;
        if (_status is null)
        {
            return;
        }
        var (icon, tone, busy) = phase switch
        {
            ExplainerChapterPhase.Playing => ((Geometry?)null, ExplainerTone.Accent, true),
            ExplainerChapterPhase.Paused => (ArcIcons.Pause, ExplainerTone.Muted, false),
            ExplainerChapterPhase.Done => (ArcIcons.CircleCheck, ExplainerTone.Success, false),
            _ => (ExplainerIcons.CircleDashed, ExplainerTone.Muted, false)
        };
        _status.Text = text;
        _status.Icon = icon;
        _status.Tone = tone;
        _status.IsBusy = busy;
    }

    /// <summary>Crossfades the caption to <paramref name="text"/>; in place when <paramref name="animate"/> is false.</summary>
    public void SetCaption(string text, bool animate)
    {
        if (_caption is null || text == _captionText)
        {
            _captionText = _caption is null ? null : text;
            return;
        }
        _captionText = text;
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            LineHeight = 19.6
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        if (animate)
        {
            _caption.Content = block;
            return;
        }
        var mode = _caption.Mode;
        _caption.Mode = ContentSwapMode.None;
        _caption.Content = block;
        _caption.Mode = mode;
    }

    /// <summary>The card padding for a width: 24, 20 under 560 and 16 under 420.</summary>
    public static double CardPadding(double width) => width < 420 ? 16 : width < 560 ? 20 : 24;
}
