using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A testimonial stage: one quote at a time, set large, with the person and their company under it. The stage moves on
/// by itself after <see cref="Interval"/> (a thin bar per quote shows the time), and pauses while a pointer is over it,
/// while the keyboard is inside it, or after the pause button. The old quote blurs out as the new one rises in; the
/// person changes a beat later.
/// <code>
/// &lt;arc:StudioPerspectives Title="What studios say"&gt;
///     &lt;arc:StudioPerspectivesQuote Text="..." Name="Amara Lindqvist" Role="Creative Director" Company="Fieldnote Studio" /&gt;
/// &lt;/arc:StudioPerspectives&gt;
/// </code>
/// </summary>
[TemplatePart(QuotePart, typeof(ArcContentSwap))]
[TemplatePart(PersonPart, typeof(ArcContentSwap))]
[TemplatePart(ProgressPart, typeof(StudioPerspectivesProgress))]
[TemplatePart(PreviousPart, typeof(Button))]
[TemplatePart(NextPart, typeof(Button))]
[TemplatePart(PausePart, typeof(Button))]
[PseudoClasses(CompactPseudoClass, PausedPseudoClass, SinglePseudoClass)]
public class StudioPerspectives : TemplatedControl
{
    public const string QuotePart = "PART_Quote";
    public const string PersonPart = "PART_Person";
    public const string ProgressPart = "PART_Progress";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string PausePart = "PART_Pause";
    public const string CompactPseudoClass = ":compact";
    public const string PausedPseudoClass = ":paused";
    public const string SinglePseudoClass = ":single";

    /// <summary>The small heading above the stage; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StudioPerspectives, string?>(nameof(Title), "Studio perspectives");

    /// <summary>The index of the quote on stage (two-way); clamped to the quotes.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<StudioPerspectives, int>(nameof(SelectedIndex), defaultBindingMode: BindingMode.TwoWay,
            coerce: (o, v) => o is StudioPerspectives s ? Math.Clamp(v, 0, Math.Max(0, s.EffectiveQuotes.Count - 1)) : v);

    /// <summary>Whether the stage moves on by itself.</summary>
    public static readonly StyledProperty<bool> IsAutoAdvanceProperty =
        AvaloniaProperty.Register<StudioPerspectives, bool>(nameof(IsAutoAdvance), true);

    /// <summary>How long each quote stays before the next one (7 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<StudioPerspectives, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(7));

    /// <summary>Whether the invented sample quotes show while <see cref="Quotes"/> is empty.</summary>
    public static readonly StyledProperty<bool> ShowsSampleQuotesProperty =
        AvaloniaProperty.Register<StudioPerspectives, bool>(nameof(ShowsSampleQuotes), true);

    /// <summary>The screen reader name of the previous button.</summary>
    public static readonly StyledProperty<string> PreviousLabelProperty =
        AvaloniaProperty.Register<StudioPerspectives, string>(nameof(PreviousLabel), "Previous quote");

    /// <summary>The screen reader name of the next button.</summary>
    public static readonly StyledProperty<string> NextLabelProperty =
        AvaloniaProperty.Register<StudioPerspectives, string>(nameof(NextLabel), "Next quote");

    /// <summary>The screen reader name of the pause button while the stage moves on by itself.</summary>
    public static readonly StyledProperty<string> PauseLabelProperty =
        AvaloniaProperty.Register<StudioPerspectives, string>(nameof(PauseLabel), "Pause quotes");

    /// <summary>The screen reader name of the pause button while the stage is paused.</summary>
    public static readonly StyledProperty<string> PlayLabelProperty =
        AvaloniaProperty.Register<StudioPerspectives, string>(nameof(PlayLabel), "Play quotes");

    /// <summary>The position line ("02 / 04"); {0} is the position, {1} the count, both two digits.</summary>
    public static readonly StyledProperty<string> PositionFormatProperty =
        AvaloniaProperty.Register<StudioPerspectives, string>(nameof(PositionFormat), "{0} / {1}");

    /// <summary>What is announced when a person changes the quote; {0} position, {1} count, {2} name, {3} byline.</summary>
    public static readonly StyledProperty<string> AnnouncementFormatProperty =
        AvaloniaProperty.Register<StudioPerspectives, string>(nameof(AnnouncementFormat), "Quote {0} of {1}, {2}, {3}");

    /// <summary>Below this width of the block (640) the quote steps down a size.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<StudioPerspectives, double>(nameof(CompactWidth), 640);

    /// <summary>The position line for the template (read-only).</summary>
    public static readonly DirectProperty<StudioPerspectives, string?> PositionTextProperty =
        AvaloniaProperty.RegisterDirect<StudioPerspectives, string?>(nameof(PositionText), o => o.PositionText);

    /// <summary>Whether the stage is paused by its pause button (read-only; <see cref="Pause"/> and <see cref="Play"/> change it).</summary>
    public static readonly DirectProperty<StudioPerspectives, bool> IsPausedProperty =
        AvaloniaProperty.RegisterDirect<StudioPerspectives, bool>(nameof(IsPaused), o => o.IsPaused);

    /// <summary>Raised when another quote comes on stage.</summary>
    public static readonly RoutedEvent<StudioPerspectivesChangedEventArgs> QuoteChangedEvent =
        RoutedEvent.Register<StudioPerspectives, StudioPerspectivesChangedEventArgs>(nameof(QuoteChanged), RoutingStrategies.Bubble);

    private static readonly StudioPerspectivesQuote[] s_samples = StudioPerspectivesSamples.Create();
    private readonly AvaloniaList<StudioPerspectivesQuote> _quotes = new();
    private ArcContentSwap? _quote;
    private ArcContentSwap? _person;
    private StudioPerspectivesProgress? _progress;
    private Button? _previous;
    private Button? _next;
    private Button? _pause;
    private SiteAnnouncer? _announcer;
    private MotionTimeline? _clock;
    private string? _positionText;
    private bool _paused;
    private bool _hovered;
    private bool _automatic;
    private int _shown = -1;

    public StudioPerspectives()
    {
        _quotes.CollectionChanged += OnQuotesChanged;
    }

    /// <summary>The quotes, in order. While it is empty the stage shows invented samples (see <see cref="ShowsSampleQuotes"/>).</summary>
    [Content]
    public AvaloniaList<StudioPerspectivesQuote> Quotes => _quotes;

    /// <summary>The quotes on the stage: <see cref="Quotes"/>, or the samples while that is empty.</summary>
    public IReadOnlyList<StudioPerspectivesQuote> EffectiveQuotes => _quotes.Count > 0 || !ShowsSampleQuotes ? _quotes : s_samples;

    /// <summary>The quote on stage, or null when there is none.</summary>
    public StudioPerspectivesQuote? CurrentQuote => EffectiveQuotes.Count > 0 ? EffectiveQuotes[Math.Clamp(SelectedIndex, 0, EffectiveQuotes.Count - 1)] : null;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <inheritdoc cref="IsAutoAdvanceProperty"/>
    public bool IsAutoAdvance { get => GetValue(IsAutoAdvanceProperty); set => SetValue(IsAutoAdvanceProperty, value); }

    /// <inheritdoc cref="IntervalProperty"/>
    public TimeSpan Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

    /// <inheritdoc cref="ShowsSampleQuotesProperty"/>
    public bool ShowsSampleQuotes { get => GetValue(ShowsSampleQuotesProperty); set => SetValue(ShowsSampleQuotesProperty, value); }

    /// <inheritdoc cref="PreviousLabelProperty"/>
    public string PreviousLabel { get => GetValue(PreviousLabelProperty); set => SetValue(PreviousLabelProperty, value); }

    /// <inheritdoc cref="NextLabelProperty"/>
    public string NextLabel { get => GetValue(NextLabelProperty); set => SetValue(NextLabelProperty, value); }

    /// <inheritdoc cref="PauseLabelProperty"/>
    public string PauseLabel { get => GetValue(PauseLabelProperty); set => SetValue(PauseLabelProperty, value); }

    /// <inheritdoc cref="PlayLabelProperty"/>
    public string PlayLabel { get => GetValue(PlayLabelProperty); set => SetValue(PlayLabelProperty, value); }

    /// <inheritdoc cref="PositionFormatProperty"/>
    public string PositionFormat { get => GetValue(PositionFormatProperty); set => SetValue(PositionFormatProperty, value); }

    /// <inheritdoc cref="AnnouncementFormatProperty"/>
    public string AnnouncementFormat { get => GetValue(AnnouncementFormatProperty); set => SetValue(AnnouncementFormatProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="PositionTextProperty"/>
    public string? PositionText { get => _positionText; private set => SetAndRaise(PositionTextProperty, ref _positionText, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => _paused; private set => SetAndRaise(IsPausedProperty, ref _paused, value); }

    /// <inheritdoc cref="QuoteChangedEvent"/>
    public event EventHandler<StudioPerspectivesChangedEventArgs>? QuoteChanged
    {
        add => AddHandler(QuoteChangedEvent, value);
        remove => RemoveHandler(QuoteChangedEvent, value);
    }

    /// <summary>True while the timer runs toward the next quote (tests read it).</summary>
    internal bool IsClockRunning => _clock?.IsRunning == true;

    internal StudioPerspectivesProgress? ProgressBar => _progress;

    internal SiteAnnouncer? Announcer => _announcer;

    internal ArcContentSwap? QuoteSwap => _quote;

    /// <summary>Shows the next quote (after the last, the first).</summary>
    public void Next() => GoTo(SelectedIndex + 1, announce: true);

    /// <summary>Shows the previous quote (before the first, the last).</summary>
    public void Previous() => GoTo(SelectedIndex - 1, announce: true);

    /// <summary>Stops the stage from moving on by itself until <see cref="Play"/>.</summary>
    public void Pause()
    {
        IsPaused = true;
        UpdateClock();
    }

    /// <summary>Lets the stage move on by itself again.</summary>
    public void Play()
    {
        IsPaused = false;
        UpdateClock();
    }

    private void GoTo(int index, bool announce)
    {
        var count = EffectiveQuotes.Count;
        if (count == 0)
        {
            return;
        }
        var target = ((index % count) + count) % count;
        _direction = index >= SelectedIndex ? 1 : -1;
        SetCurrentValue(SelectedIndexProperty, target);
        if (announce && CurrentQuote is { } quote)
        {
            _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, AnnouncementFormat, target + 1, count, quote.Name, quote.Byline));
        }
        // A person's choice starts the next quote's full time.
        _clock?.Stop();
        UpdateClock();
    }

    private int _direction = 1;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_pause is not null)
        {
            _pause.Click -= OnPauseClick;
        }
        if (_progress is not null)
        {
            _progress.SegmentPressed -= OnSegmentPressed;
        }
        _quote = e.NameScope.Find<ArcContentSwap>(QuotePart);
        _person = e.NameScope.Find<ArcContentSwap>(PersonPart);
        _progress = e.NameScope.Find<StudioPerspectivesProgress>(ProgressPart);
        _previous = e.NameScope.Find<Button>(PreviousPart);
        _next = e.NameScope.Find<Button>(NextPart);
        _pause = e.NameScope.Find<Button>(PausePart);
        _announcer = e.NameScope.Find<SiteAnnouncer>("PART_Announcer");
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_pause is not null)
        {
            _pause.Click += OnPauseClick;
        }
        if (_progress is not null)
        {
            _progress.SegmentPressed += OnSegmentPressed;
        }
        _shown = -1;
        ShowCurrent();
        UpdateLabels();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_clock is null)
        {
            _clock = new MotionTimeline(this);
            _clock.Changed += value =>
            {
                if (_progress is not null)
                {
                    _progress.Progress = value;
                }
            };
            _clock.Completed += OnClockCompleted;
        }
        UpdateClock();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock?.Pause();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            ShowCurrent();
        }
        else if (change.Property == ShowsSampleQuotesProperty)
        {
            CoerceValue(SelectedIndexProperty);
            _shown = -1;
            ShowCurrent();
        }
        else if (change.Property == IsAutoAdvanceProperty || change.Property == IntervalProperty)
        {
            if (change.Property == IntervalProperty)
            {
                _clock?.Stop();
            }
            UpdateClock();
        }
        else if (change.Property == IsPausedProperty || change.Property == PauseLabelProperty || change.Property == PlayLabelProperty
                 || change.Property == PreviousLabelProperty || change.Property == NextLabelProperty)
        {
            UpdateLabels();
        }
        else if (change.Property == PositionFormatProperty)
        {
            UpdatePosition();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            var compact = Bounds.Width > 0 && Bounds.Width < CompactWidth;
            PseudoClasses.Set(CompactPseudoClass, compact);
            if (_quote?.Content is StudioPerspectivesQuoteView view)
            {
                view.IsCompact = compact;
            }
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            _hovered = true;
            UpdateClock();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        UpdateClock();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateClock();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateClock();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Left:
                Previous();
                break;
            case Key.Right:
                Next();
                break;
            case Key.Home:
                GoTo(0, announce: true);
                break;
            case Key.End:
                GoTo(EffectiveQuotes.Count - 1, announce: true);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnQuotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CoerceValue(SelectedIndexProperty);
        _shown = -1;
        ShowCurrent();
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Previous();

    private void OnNextClick(object? sender, RoutedEventArgs e) => Next();

    private void OnPauseClick(object? sender, RoutedEventArgs e)
    {
        if (IsPaused)
        {
            Play();
        }
        else
        {
            Pause();
        }
    }

    private void OnSegmentPressed(object? sender, int index) => GoTo(index, announce: true);

    private void OnClockCompleted()
    {
        _automatic = true;
        try
        {
            GoTo(SelectedIndex + 1, announce: false);
        }
        finally
        {
            _automatic = false;
        }
    }

    private void ShowCurrent()
    {
        var quotes = EffectiveQuotes;
        PseudoClasses.Set(SinglePseudoClass, quotes.Count < 2);
        UpdatePosition();
        if (_progress is not null)
        {
            _progress.Count = quotes.Count;
            _progress.Index = SelectedIndex;
            _progress.Progress = 0;
        }
        if (CurrentQuote is not { } quote || _shown == SelectedIndex && _quote?.Content is not null)
        {
            if (quotes.Count == 0 && _quote is not null && _person is not null)
            {
                _quote.Content = null;
                _person.Content = null;
            }
            return;
        }
        var first = _shown < 0;
        _shown = SelectedIndex;
        if (_quote is not null)
        {
            _quote.Direction = _direction;
            _quote.Content = new StudioPerspectivesQuoteView { Text = quote.Text, IsCompact = PseudoClasses.Contains(CompactPseudoClass) };
        }
        if (_person is not null)
        {
            _person.Direction = _direction;
            _person.Content = new StudioPerspectivesPerson
            {
                PersonName = quote.Name,
                Byline = quote.Byline,
                Seed = quote.Seed ?? TeamShowcasePortrait.SeedOf(quote.Name),
                Image = quote.Image
            };
        }
        if (!first)
        {
            RaiseEvent(new StudioPerspectivesChangedEventArgs(QuoteChangedEvent, SelectedIndex, quote, _automatic));
        }
    }

    private void UpdatePosition()
    {
        var count = EffectiveQuotes.Count;
        PositionText = count == 0 ? null : string.Format(CultureInfo.CurrentCulture, PositionFormat,
            (SelectedIndex + 1).ToString("00", CultureInfo.CurrentCulture), count.ToString("00", CultureInfo.CurrentCulture));
    }

    private void UpdateLabels()
    {
        PseudoClasses.Set(PausedPseudoClass, IsPaused);
        if (_previous is not null)
        {
            AutomationProperties.SetName(_previous, PreviousLabel);
        }
        if (_next is not null)
        {
            AutomationProperties.SetName(_next, NextLabel);
        }
        if (_pause is not null)
        {
            AutomationProperties.SetName(_pause, IsPaused ? PlayLabel : PauseLabel);
        }
    }

    private void UpdateClock()
    {
        if (_clock is null)
        {
            return;
        }
        var run = IsAutoAdvance && !IsPaused && !_hovered && !IsKeyboardFocusWithin && EffectiveQuotes.Count > 1
                  && TopLevel.GetTopLevel(this) is not null;
        if (!run)
        {
            if (_clock.IsRunning)
            {
                _clock.Pause();
            }
            return;
        }
        if (_clock.IsActive)
        {
            _clock.Resume();
        }
        else
        {
            _clock.Start(Interval);
        }
    }
}
