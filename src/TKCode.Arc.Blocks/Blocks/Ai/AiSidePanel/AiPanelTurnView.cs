using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One exchange of the side panel's thread (spec 3.5, F7–F10), its parts 10px apart: the question right-aligned (the
/// context line "⧉ Selected: Summary", 12 / 16.8 TextMuted, 4px above the bubble; the bubble 13 / 18.2 Foreground on
/// SurfaceMuted, radius 14, padding 7 12), then by <see cref="AiPanelTurn.State"/>: "Reading the page" shimmering (13 /
/// 18.2, TextMuted base), the answer (<see cref="AiPanelAnswerText"/>), the stopped line ("■ Stopped", 12 / 16.8
/// TextMuted with a 10px square), the error card, and a change card. A new question fades in over 160ms while it rises
/// 3px (reduced motion fades only); a change card fades in over 160ms without moving.
/// <para>
/// The view follows its <see cref="Turn"/>: a turn the host restores shows as it is. While the panel streams the turn
/// (<see cref="IsDriven"/>) the answer grows through <see cref="AppendAnswer"/> instead, one step at a time, without laying
/// out anything but its last paragraph.
/// </para>
/// </summary>
[TemplatePart(AskPart, typeof(Control))]
[TemplatePart(ContextPart, typeof(Control))]
[TemplatePart(ContextTextPart, typeof(TextBlock))]
[TemplatePart(PromptPart, typeof(TextBlock))]
[TemplatePart(ReadingPart, typeof(TextShimmer))]
[TemplatePart(AnswerPart, typeof(AiPanelAnswerText))]
[TemplatePart(StoppedPart, typeof(Control))]
[TemplatePart(StoppedTextPart, typeof(TextBlock))]
[TemplatePart(ErrorPart, typeof(AiPanelErrorCard))]
[TemplatePart(CardHostPart, typeof(Decorator))]
internal sealed class AiPanelTurnView : TemplatedControl
{
    public const string AskPart = "PART_Ask";
    public const string ContextPart = "PART_Context";
    public const string ContextTextPart = "PART_ContextText";
    public const string PromptPart = "PART_Prompt";
    public const string ReadingPart = "PART_Reading";
    public const string AnswerPart = "PART_Answer";
    public const string StoppedPart = "PART_Stopped";
    public const string StoppedTextPart = "PART_StoppedText";
    public const string ErrorPart = "PART_Error";
    public const string CardHostPart = "PART_CardHost";

    /// <summary>How far a new question rises while it fades in.</summary>
    public const double AskRise = 3;

    private readonly AiPanelTurn _turn;
    private AiSidePanelLabels _labels;
    private string _assistantName;
    private Control? _ask;
    private Control? _context;
    private TextBlock? _contextText;
    private TextBlock? _prompt;
    private TextShimmer? _reading;
    private AiPanelAnswerText? _answer;
    private Control? _stopped;
    private TextBlock? _stoppedText;
    private AiPanelErrorCard? _error;
    private Decorator? _cardHost;
    private AiPanelChangeCard? _card;
    private AiFadePresence? _askFade;
    private AiFadePresence? _cardFade;
    private bool _animateIn;
    private bool _watching;
    private bool _isDriven;

    static AiPanelTurnView()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelTurnView>(false);
    }

    /// <param name="turn">The turn to show.</param>
    /// <param name="labels">The words.</param>
    /// <param name="assistantName">The assistant's name (the error card).</param>
    /// <param name="animateIn">Whether the question fades in (a new question; not a restored turn).</param>
    public AiPanelTurnView(AiPanelTurn turn, AiSidePanelLabels labels, string assistantName, bool animateIn)
    {
        _turn = turn;
        _labels = labels;
        _assistantName = assistantName;
        _animateIn = animateIn;
    }

    /// <summary>The turn shown.</summary>
    public AiPanelTurn Turn => _turn;

    /// <summary>
    /// Whether the panel streams this turn: the answer grows through <see cref="AppendAnswer"/> and the turn's
    /// <see cref="AiPanelTurn.Answer"/> is not mirrored.
    /// </summary>
    public bool IsDriven
    {
        get => _isDriven;
        set
        {
            _isDriven = value;
            if (!value)
            {
                Refresh();
            }
        }
    }

    /// <summary>The change card, when the turn has a change.</summary>
    internal AiPanelChangeCard? Card => _card;

    /// <summary>The answer text (once the template is applied).</summary>
    internal AiPanelAnswerText? Answer => _answer;

    /// <summary>The reading shimmer (once the template is applied).</summary>
    internal TextShimmer? Reading => _reading;

    /// <summary>The stopped line (once the template is applied).</summary>
    internal Control? StoppedLine => _stopped;

    /// <summary>The error card (once the template is applied).</summary>
    internal AiPanelErrorCard? ErrorCard => _error;

    /// <summary>The question (context line and bubble).</summary>
    internal Control? Ask => _ask;

    /// <summary>The context line above the bubble.</summary>
    internal Control? ContextLine => _context;

    /// <summary>The bubble's text.</summary>
    internal TextBlock? Prompt => _prompt;

    /// <summary>How often the turn was measured (tests: a streamed answer must not lay out the other turns).</summary>
    internal int MeasureCount { get; private set; }

    /// <summary>Takes new words.</summary>
    public void ApplyLabels(AiSidePanelLabels labels, string assistantName)
    {
        _labels = labels;
        _assistantName = assistantName;
        if (_card is not null)
        {
            _card.Labels = labels;
        }
        Refresh();
    }

    /// <summary>Clears the answer for a new stream (a first run or a retry).</summary>
    public void BeginStream()
    {
        _isDriven = true;
        _answer?.SetText(string.Empty);
        Refresh();
    }

    /// <summary>Adds a released step to the answer; with <paramref name="fade"/> it fades in.</summary>
    public void AppendAnswer(string step, bool fade)
    {
        if (_answer is null)
        {
            return;
        }
        _answer.Append(step, fade);
        _answer.IsVisible = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCount++;
        return base.MeasureOverride(availableSize);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _askFade?.Dispose();
        _askFade = null;
        _cardFade?.Dispose();
        _cardFade = null;
        _ask = e.NameScope.Find<Control>(AskPart);
        _context = e.NameScope.Find<Control>(ContextPart);
        _contextText = e.NameScope.Find<TextBlock>(ContextTextPart);
        _prompt = e.NameScope.Find<TextBlock>(PromptPart);
        _reading = e.NameScope.Find<TextShimmer>(ReadingPart);
        _answer = e.NameScope.Find<AiPanelAnswerText>(AnswerPart);
        _stopped = e.NameScope.Find<Control>(StoppedPart);
        _stoppedText = e.NameScope.Find<TextBlock>(StoppedTextPart);
        _error = e.NameScope.Find<AiPanelErrorCard>(ErrorPart);
        _cardHost = e.NameScope.Find<Decorator>(CardHostPart);
        if (_ask is not null && _animateIn)
        {
            // Hidden until the view is in a window: then it fades in (a presence change before that would jump).
            _askFade = new AiFadePresence(_ask, shown: false, AskRise);
        }
        _answer?.SetText(_isDriven ? string.Empty : _turn.Answer);
        Refresh();
        if (_askFade is not null && _animateIn && TopLevel.GetTopLevel(this) is not null)
        {
            // The template comes with the first layout, after the view joined the window: fade in now.
            _animateIn = false;
            _askFade.SetShown(true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_watching)
        {
            _watching = true;
            _turn.PropertyChanged += OnTurnPropertyChanged;
        }
        Refresh();
        if (_askFade is not null && _animateIn)
        {
            _animateIn = false;
            _askFade.SetShown(true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_watching)
        {
            _watching = false;
            _turn.PropertyChanged -= OnTurnPropertyChanged;
        }
    }

    private void OnTurnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiPanelTurn.AnswerProperty)
        {
            if (!_isDriven)
            {
                _answer?.SetText(_turn.Answer);
                Refresh();
            }
        }
        else
        {
            Refresh();
        }
    }

    /// <summary>Shows the parts the turn's state calls for.</summary>
    private void Refresh()
    {
        var state = _turn.State;
        if (_context is not null)
        {
            _context.IsVisible = !string.IsNullOrEmpty(_turn.ContextLabel);
        }
        if (_contextText is not null)
        {
            _contextText.Text = string.IsNullOrEmpty(_turn.ContextLabel) ? null : _labels.SelectionContext(_turn.ContextLabel);
        }
        if (_prompt is not null)
        {
            _prompt.Text = _turn.Prompt;
        }
        if (_reading is not null)
        {
            _reading.Text = _labels.ReadingThePage;
            _reading.IsVisible = state == AiPanelTurnState.Reading;
            _reading.IsActive = state == AiPanelTurnState.Reading;
            AutomationProperties.SetName(_reading, _labels.ReadingThePage);
        }
        if (_answer is not null)
        {
            _answer.IsVisible = state != AiPanelTurnState.Reading && _answer.HasText;
        }
        if (_stopped is not null)
        {
            _stopped.IsVisible = state == AiPanelTurnState.Stopped;
        }
        if (_stoppedText is not null)
        {
            _stoppedText.Text = _labels.Stopped;
        }
        if (_error is not null)
        {
            _error.IsVisible = state == AiPanelTurnState.Failed;
            _error.Title = _labels.DidNotRespond(_assistantName);
            _error.Detail = _turn.ErrorMessage;
            _error.RetryText = _labels.Retry;
        }
        RefreshCard(state);
    }

    private void RefreshCard(AiPanelTurnState state)
    {
        if (_cardHost is null)
        {
            return;
        }
        var change = state == AiPanelTurnState.Done ? _turn.Change : null;
        if (ReferenceEquals(change, _card?.Change))
        {
            return;
        }
        _cardFade?.Dispose();
        _cardFade = null;
        if (change is null)
        {
            _card = null;
            _cardHost.Child = null;
            _cardHost.IsVisible = false;
            return;
        }
        _card = new AiPanelChangeCard(change, _labels);
        _cardHost.Child = _card;
        _cardHost.IsVisible = true;
        if (TopLevel.GetTopLevel(this) is not null)
        {
            // A card that arrives with the answer fades in; one restored with the turn is just there.
            _cardFade = new AiFadePresence(_cardHost, shown: false);
            _cardFade.SetShown(true);
        }
    }
}
