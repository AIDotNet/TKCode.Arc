using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Citations of <see cref="AiAnswerDocument"/>: which is active (pointer or focus), what is lit, the source preview card
/// and invoking a source.
/// </summary>
/// <remarks>
/// The lighting protocol: the active citation (under the pointer, else focused) lights the sentence right before it and
/// itself, and <see cref="AiAnswerDocument.CitationActivated"/> tells the owner its number so the source card with that
/// number lights too. The other way round the owner sets <see cref="AiAnswerDocument.LitSourceNumber"/> while a source
/// card is active: every sentence citing that source lights with its pills of that number (the sentence's other pills
/// stay as they are), and no preview opens.
/// </remarks>
internal partial class AiAnswerDocument
{
    /// <summary>Pointer time on a citation before its preview opens (Arc ≈ 220 ms); keyboard focus opens it at once.</summary>
    internal static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(220);

    /// <summary>Moving to another citation while a preview is open switches almost at once.</summary>
    internal static readonly TimeSpan SwitchDelay = TimeSpan.FromMilliseconds(80);

    /// <summary>Grace after the pointer leaves, so it can travel into the card (Arc 200 ms).</summary>
    internal static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Width of the preview card (Arc 296).</summary>
    internal const double CardWidth = 296;

    private AiAnswerCitation? _hoverPill;
    private AiAnswerCitation? _focusPill;
    private AiAnswerCitation? _cardPill;
    private AiAnswerCitation? _pendingPill;
    private AiAnswerCitation? _dismissedPill;
    private int _activeNumber;
    private bool _releasePending;
    private bool _pointerInCard;
    private ArcPopup? _popup;
    private AiCitationCard? _card;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;

    /// <summary>The source number of the active citation, or 0.</summary>
    internal int ActiveNumber => _activeNumber;

    /// <summary>The citation under the pointer.</summary>
    internal AiAnswerCitation? HoveredCitation => _hoverPill;

    /// <summary>The focused citation.</summary>
    internal AiAnswerCitation? FocusedCitation => _focusPill;

    /// <summary>The citation whose preview is open, or null.</summary>
    internal AiAnswerCitation? CardCitation => IsCardOpen ? _cardPill : null;

    /// <summary>Whether a preview is open.</summary>
    internal bool IsCardOpen => _cardPill is not null && _popup?.IsOpen == true;

    /// <summary>The preview card (created on first open).</summary>
    internal AiCitationCard? Card => _card;

    /// <summary>The preview popup (created on first open).</summary>
    internal ArcPopup? CardPopup => _popup;

    #region Pills

    /// <summary>A pill for citation <paramref name="number"/>: a link when the answer has its source, else a plain number.</summary>
    internal AiAnswerCitation CreateCitation(int number)
    {
        var pill = new AiAnswerCitation(this, number);
        UpdateCitation(pill);
        return pill;
    }

    /// <summary>A pill left the text: it stops being active, and its preview closes.</summary>
    internal void ForgetCitation(AiAnswerCitation pill)
    {
        var changed = false;
        if (ReferenceEquals(_hoverPill, pill))
        {
            _hoverPill = null;
            changed = true;
        }
        if (ReferenceEquals(_focusPill, pill))
        {
            _focusPill = null;
            changed = true;
        }
        if (ReferenceEquals(_cardPill, pill) || ReferenceEquals(_pendingPill, pill))
        {
            CloseCard();
        }
        if (ReferenceEquals(_dismissedPill, pill))
        {
            _dismissedPill = null;
        }
        if (changed)
        {
            UpdateActive();
        }
    }

    private void UpdateCitation(AiAnswerCitation pill)
    {
        var source = FindSource(pill.Number);
        pill.HasSource = source is not null;
        AutomationProperties.SetName(pill, source is null ? pill.Label : CitationLabel(pill.Number, source));
    }

    private string CitationLabel(int number, AiSource source)
    {
        if (CitationNameFormat is { } format)
        {
            return format(number, source.Title, source.Domain);
        }
        var n = number.ToString(CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(CitationName) ? n + ": " + source.Title : CitationName + " " + n + ": " + source.Title;
    }

    #endregion

    #region Lighting

    /// <summary>Lights what the active citations and <see cref="LitSourceNumber"/> call for, in every text.</summary>
    internal void UpdateLighting()
    {
        var number = LitSourceNumber > 0 && FindSource(LitSourceNumber) is not null ? LitSourceNumber : 0;
        foreach (var text in AllTexts())
        {
            text.ApplyLighting(_hoverPill, _focusPill, number);
        }
    }

    /// <summary>
    /// Raises <see cref="CitationActivated"/> when the number of the active citation (pointer first, then focus) changes.
    /// "None" waits for the dispatcher: moving from one citation to the next (Tab, or the pointer from pill to pill)
    /// leaves the first before it enters the second, and the owner's card would dim and light again in between.
    /// </summary>
    private void UpdateActive()
    {
        var active = _hoverPill ?? _focusPill;
        var number = active?.Number ?? 0;
        if (number == 0)
        {
            if (_activeNumber != 0 && !_releasePending)
            {
                _releasePending = true;
                Dispatcher.UIThread.Post(ReleaseActive, DispatcherPriority.Input);
            }
            return;
        }
        _releasePending = false;
        if (number != _activeNumber)
        {
            _activeNumber = number;
            RaiseEvent(new AiAnswerLightEventArgs(CitationActivatedEvent, this, number));
        }
    }

    private void ReleaseActive()
    {
        if (!_releasePending)
        {
            return;
        }
        _releasePending = false;
        if (_hoverPill is null && _focusPill is null && _activeNumber != 0)
        {
            _activeNumber = 0;
            RaiseEvent(new AiAnswerLightEventArgs(CitationActivatedEvent, this, 0));
        }
    }

    internal void OnCitationPointer(AiAnswerCitation pill, bool entered)
    {
        if (!pill.HasSource)
        {
            return;
        }
        if (entered)
        {
            _hoverPill = pill;
        }
        else if (ReferenceEquals(_hoverPill, pill))
        {
            _hoverPill = null;
            if (ReferenceEquals(_dismissedPill, pill) && !ReferenceEquals(_focusPill, pill))
            {
                _dismissedPill = null;
            }
        }
        UpdateLighting();
        UpdateActive();
        if (entered)
        {
            ScheduleOpen(pill, IsCardOpen ? SwitchDelay : OpenDelay);
        }
        else
        {
            ScheduleClose();
        }
    }

    internal void OnCitationFocus(AiAnswerCitation pill, bool focused, bool keyboard)
    {
        if (focused)
        {
            _focusPill = pill;
            pill.BringIntoView();
        }
        else if (ReferenceEquals(_focusPill, pill))
        {
            _focusPill = null;
            if (ReferenceEquals(_dismissedPill, pill))
            {
                _dismissedPill = null;
            }
        }
        UpdateLighting();
        UpdateActive();
        if (focused && pill.HasSource)
        {
            // Arc: focusing a citation opens its preview at once.
            ScheduleOpen(pill, TimeSpan.Zero);
        }
        else if (!focused)
        {
            ScheduleClose();
        }
    }

    /// <summary>Raises <see cref="CitationInvoked"/> for the pill's source, if the answer has it.</summary>
    internal void InvokeCitation(AiAnswerCitation pill)
    {
        if (FindSource(pill.Number) is { } source)
        {
            RaiseEvent(new AiAnswerSourceEventArgs(CitationInvokedEvent, this, source, fromCitation: true));
        }
    }

    #endregion

    #region Card

    private void ScheduleOpen(AiAnswerCitation pill, TimeSpan delay)
    {
        _closeTimer?.Stop();
        if (ReferenceEquals(pill, _dismissedPill) || (ReferenceEquals(pill, _cardPill) && IsCardOpen))
        {
            return;
        }
        _pendingPill = pill;
        if (delay <= TimeSpan.Zero)
        {
            _openTimer?.Stop();
            OnOpenTimer();
            return;
        }
        _openTimer ??= CreateTimer(OnOpenTimer);
        _openTimer.Stop();
        _openTimer.Interval = delay;
        _openTimer.Start();
    }

    private void ScheduleClose()
    {
        _openTimer?.Stop();
        _pendingPill = null;
        if (!IsCardOpen || _pointerInCard || ReferenceEquals(_cardPill, _hoverPill) || ReferenceEquals(_cardPill, _focusPill))
        {
            return;
        }
        _closeTimer ??= CreateTimer(OnCloseTimer);
        _closeTimer.Stop();
        _closeTimer.Interval = CloseDelay;
        _closeTimer.Start();
    }

    private void OnOpenTimer()
    {
        var pill = _pendingPill;
        _pendingPill = null;
        if (pill is not null && (ReferenceEquals(pill, _hoverPill) || ReferenceEquals(pill, _focusPill)))
        {
            OpenCard(pill);
        }
    }

    private void OnCloseTimer()
    {
        if (IsCardOpen && !_pointerInCard && !ReferenceEquals(_cardPill, _hoverPill) && !ReferenceEquals(_cardPill, _focusPill))
        {
            CloseCard();
        }
    }

    private static DispatcherTimer CreateTimer(Action tick)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tick();
        };
        return timer;
    }

    /// <summary>Runs pending open and close timers now (tests: headless dispatcher timers do not advance).</summary>
    internal void FlushCardTimers()
    {
        if (_openTimer?.IsEnabled == true)
        {
            _openTimer.Stop();
            OnOpenTimer();
        }
        if (_closeTimer?.IsEnabled == true)
        {
            _closeTimer.Stop();
            OnCloseTimer();
        }
    }

    /// <summary>Opens (or moves) the preview under the pill, centered, 8px away; the popup shifts it into the window.</summary>
    internal void OpenCard(AiAnswerCitation pill)
    {
        if (FindSource(pill.Number) is not { } source || !pill.IsAttachedToVisualTree())
        {
            return;
        }
        EnsurePopup();
        _card!.Show(source, pill.Number);
        var previous = _cardPill;
        _cardPill = pill;
        if (_popup!.IsOpen && !ReferenceEquals(_popup.PlacementTarget, pill))
        {
            _popup.IsOpen = false;
        }
        _popup.PlacementTarget = pill;
        _popup.IsOpen = true;
        if (previous is not null && !ReferenceEquals(previous, pill))
        {
            AutomationProperties.SetHelpText(previous, null);
        }
        // Avalonia has no aria-describedby: the preview rides on the citation's help text while it is open.
        AutomationProperties.SetHelpText(pill, string.IsNullOrWhiteSpace(source.Snippet) ? source.Title : source.Title + ". " + source.Snippet);
    }

    /// <summary>Closes the preview.</summary>
    internal void CloseCard()
    {
        _openTimer?.Stop();
        _closeTimer?.Stop();
        _pendingPill = null;
        _pointerInCard = false;
        var pill = _cardPill;
        _cardPill = null;
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        if (pill is not null)
        {
            AutomationProperties.SetHelpText(pill, null);
        }
    }

    /// <summary>Escape on a pill: closes its preview and keeps it closed while the pill stays focused or hovered. False when nothing was open.</summary>
    internal bool CloseCardFrom(AiAnswerCitation pill)
    {
        if (!IsCardOpen && !ReferenceEquals(_pendingPill, pill))
        {
            return false;
        }
        CloseCard();
        _dismissedPill = pill;
        return true;
    }

    private void RefreshCard()
    {
        if (_cardPill is null)
        {
            return;
        }
        if (FindSource(_cardPill.Number) is { } source)
        {
            _card?.Show(source, _cardPill.Number);
        }
        else
        {
            CloseCard();
        }
    }

    private void EnsurePopup()
    {
        if (_popup is not null)
        {
            return;
        }
        // The answer's preview ends with "domain · date" (spec 3.2, fourth row).
        _card = new AiCitationCard { Width = CardWidth, Padding = new Thickness(16), ShowsMeta = true };
        // Arc clamps the excerpt to three lines.
        _card.Styles.Add(new Style(x => x.OfType<AiCitationCard>().Template().OfType<TextBlock>().Name("PART_Snippet"))
        {
            Setters =
            {
                new Setter(TextBlock.MaxLinesProperty, 3),
                new Setter(TextBlock.TextTrimmingProperty, Avalonia.Media.TextTrimming.CharacterEllipsis)
            }
        });
        _card.PointerEntered += OnCardEntered;
        _card.PointerExited += OnCardExited;
        _popup = new ArcPopup
        {
            Child = _card,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = false,
            TakesFocusFromNativeControl = false,
            EnterScale = 0.96,
            EnterOffset = 4
        };
        LogicalChildren.Add(_popup);
    }

    private void OnCardEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _pointerInCard = true;
        _closeTimer?.Stop();
    }

    private void OnCardExited(object? sender, PointerEventArgs e)
    {
        _pointerInCard = false;
        ScheduleClose();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        HookSources(null);
        CloseCard();
        _hoverPill = null;
    }

    #endregion
}
