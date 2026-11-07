using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Everything the player shows over the picture besides the controls and captions (spec section 5, <c>PART_State</c>): the
/// poster, the big play button, the loading spinner, the error with Retry, the Replay button at the end, the backend
/// hint, the centre play / pause feedback and the double-tap skip ripples. The player decides what shows; this part only
/// draws it. Clicks on its buttons bubble to the player as <see cref="Avalonia.Controls.Button.ClickEvent"/> (by part name).
/// </summary>
[TemplatePart(FeedbackPart, typeof(Border))]
[TemplatePart(FeedbackGlyphPart, typeof(VideoGlyph))]
[TemplatePart(SkipBackPart, typeof(Panel))]
[TemplatePart(SkipForwardPart, typeof(Panel))]
internal sealed class VideoStateLayer : TemplatedControl
{
    public const string BigPlayPart = "PART_BigPlay";
    public const string ReplayPart = "PART_Replay";
    public const string RetryPart = "PART_Retry";
    public const string FeedbackPart = "PART_Feedback";
    public const string FeedbackGlyphPart = "PART_FeedbackGlyph";
    public const string SkipBackPart = "PART_SkipBack";
    public const string SkipForwardPart = "PART_SkipForward";

    public static readonly StyledProperty<IImage?> PosterProperty =
        AvaloniaProperty.Register<VideoStateLayer, IImage?>(nameof(Poster));

    public static readonly StyledProperty<Stretch> PosterStretchProperty =
        AvaloniaProperty.Register<VideoStateLayer, Stretch>(nameof(PosterStretch), Stretch.Uniform);

    public static readonly StyledProperty<bool> ShowPosterProperty =
        AvaloniaProperty.Register<VideoStateLayer, bool>(nameof(ShowPoster));

    public static readonly StyledProperty<bool> ShowBigPlayProperty =
        AvaloniaProperty.Register<VideoStateLayer, bool>(nameof(ShowBigPlay));

    public static readonly StyledProperty<bool> ShowSpinnerProperty =
        AvaloniaProperty.Register<VideoStateLayer, bool>(nameof(ShowSpinner));

    public static readonly StyledProperty<bool> ShowReplayProperty =
        AvaloniaProperty.Register<VideoStateLayer, bool>(nameof(ShowReplay));

    public static readonly StyledProperty<bool> ShowErrorProperty =
        AvaloniaProperty.Register<VideoStateLayer, bool>(nameof(ShowError));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(ErrorText));

    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(RetryText));

    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(HintText));

    public static readonly StyledProperty<string?> BigPlayTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(BigPlayText));

    public static readonly StyledProperty<string?> ReplayTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(ReplayText));

    public static readonly StyledProperty<string?> SkipBackTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(SkipBackText));

    public static readonly StyledProperty<string?> SkipForwardTextProperty =
        AvaloniaProperty.Register<VideoStateLayer, string?>(nameof(SkipForwardText));

    private Border? _feedback;
    private VideoGlyph? _feedbackGlyph;
    private Panel? _skipBack;
    private Panel? _skipForward;
    private MotionValue<double>? _flash;
    private MotionValue<double>? _skipFade;
    private DispatcherTimer? _skipTimer;
    private Panel? _skipShown;

    public IImage? Poster { get => GetValue(PosterProperty); set => SetValue(PosterProperty, value); }

    public Stretch PosterStretch { get => GetValue(PosterStretchProperty); set => SetValue(PosterStretchProperty, value); }

    public bool ShowPoster { get => GetValue(ShowPosterProperty); set => SetValue(ShowPosterProperty, value); }

    public bool ShowBigPlay { get => GetValue(ShowBigPlayProperty); set => SetValue(ShowBigPlayProperty, value); }

    public bool ShowSpinner { get => GetValue(ShowSpinnerProperty); set => SetValue(ShowSpinnerProperty, value); }

    public bool ShowReplay { get => GetValue(ShowReplayProperty); set => SetValue(ShowReplayProperty, value); }

    public bool ShowError { get => GetValue(ShowErrorProperty); set => SetValue(ShowErrorProperty, value); }

    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    /// <summary>The backend hint at the top left (spec section 5.4); hidden when null.</summary>
    public string? HintText { get => GetValue(HintTextProperty); set => SetValue(HintTextProperty, value); }

    public string? BigPlayText { get => GetValue(BigPlayTextProperty); set => SetValue(BigPlayTextProperty, value); }

    public string? ReplayText { get => GetValue(ReplayTextProperty); set => SetValue(ReplayTextProperty, value); }

    /// <summary>The text of the left skip ripple ("−10s"); for tests and screen readers.</summary>
    public string? SkipBackText { get => GetValue(SkipBackTextProperty); set => SetValue(SkipBackTextProperty, value); }

    /// <summary>The text of the right skip ripple ("+20s").</summary>
    public string? SkipForwardText { get => GetValue(SkipForwardTextProperty); set => SetValue(SkipForwardTextProperty, value); }

    /// <summary>Whether the centre feedback is showing (for tests).</summary>
    public bool IsFlashing => _feedback is { Opacity: > 0 };

    /// <summary>The skip ripple that is showing, if any (for tests).</summary>
    public Panel? ShownSkip => _skipShown is { Opacity: > 0 } ? _skipShown : null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _feedback = e.NameScope.Find<Border>(FeedbackPart);
        _feedbackGlyph = e.NameScope.Find<VideoGlyph>(FeedbackGlyphPart);
        _skipBack = e.NameScope.Find<Panel>(SkipBackPart);
        _skipForward = e.NameScope.Find<Panel>(SkipForwardPart);
        _flash = null;
        _skipFade = null;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Each skip ripple covers a third of the picture (the double-tap zones).
        var third = Math.Floor(finalSize.Width / 3);
        if (_skipBack is not null && _skipBack.Width != third)
        {
            _skipBack.Width = third;
        }
        if (_skipForward is not null && _skipForward.Width != third)
        {
            _skipForward.Width = third;
        }
        return base.ArrangeOverride(finalSize);
    }

    /// <summary>
    /// The centre feedback after a click on the picture: the play or pause glyph appears and grows while it fades, 400 ms
    /// (spec section 8). Not shown under reduced or no motion. Code-driven: it is a one-shot reaction to a click, not a state
    /// a ControlTheme selector could follow.
    /// </summary>
    public void Flash(bool playing)
    {
        if (_feedback is null || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_feedbackGlyph is not null)
        {
            _feedbackGlyph.Data = playing ? VideoGlyphs.Play : VideoGlyphs.Pause;
        }
        _flash ??= CreateFlash(_feedback);
        _flash.Jump(0);
        ApplyFlash(_feedback, 0);
        _ = _flash.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(400), ArcEasings.Standard));
    }

    /// <summary>
    /// The double-tap skip ripple on one side: "−10s" / "+20s" with a half-disc ripple; repeated taps update the text. Under
    /// reduced or no motion only the text shows (spec section 8). It fades 600 ms after the last tap.
    /// </summary>
    public void ShowSkip(int direction, int seconds)
    {
        var side = direction < 0 ? _skipBack : _skipForward;
        var other = direction < 0 ? _skipForward : _skipBack;
        var text = (direction < 0 ? "−" : "+") + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
        if (direction < 0)
        {
            SkipBackText = text;
        }
        else
        {
            SkipForwardText = text;
        }
        if (side is null)
        {
            return;
        }
        if (other is not null)
        {
            other.Opacity = 0;
        }
        var full = ArcMotion.IsFull(this);
        side.Classes.Set("ripple", full);
        _skipShown = side;
        _skipFade?.Stop();
        side.Opacity = 1;
        _skipTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _skipTimer.Stop();
        _skipTimer.Tick -= OnSkipTimer;
        _skipTimer.Tick += OnSkipTimer;
        _skipTimer.Start();
    }

    private void OnSkipTimer(object? sender, EventArgs e)
    {
        _skipTimer?.Stop();
        HideSkip();
    }

    /// <summary>Fades the skip ripple out now (the player calls it when the tap streak ends; the timer does otherwise).</summary>
    public void HideSkip()
    {
        if (_skipShown is not { } side)
        {
            return;
        }
        if (ArcMotion.IsFull(this))
        {
            _skipFade ??= CreateSkipFade();
            _skipFade.Jump(side.Opacity);
            _ = _skipFade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
        }
        else
        {
            side.Opacity = 0;
        }
    }

    private MotionValue<double> CreateSkipFade()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_skipShown is { } side)
            {
                side.Opacity = v;
            }
        };
        return value;
    }

    private MotionValue<double> CreateFlash(Border feedback)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += p => ApplyFlash(feedback, p);
        return value;
    }

    private static void ApplyFlash(Border feedback, double p)
    {
        feedback.Opacity = p >= 1 ? 0 : 1 - p;
        // The transform is reused across frames: only its values change.
        if (feedback.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform();
            feedback.RenderTransform = scale;
            feedback.RenderTransformOrigin = RelativePoint.Center;
        }
        scale.ScaleX = scale.ScaleY = 1 + 0.5 * p;
    }
}
