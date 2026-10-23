using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="AiMessageActions.FeedbackRequestedEvent"/>: the rating the person chose and the one before.</summary>
internal sealed class AiMessageActionsFeedbackEventArgs : RoutedEventArgs
{
    public AiMessageActionsFeedbackEventArgs(RoutedEvent routedEvent, object source, AiChatFeedback value, AiChatFeedback previous)
        : base(routedEvent, source)
    {
        Value = value;
        Previous = previous;
    }

    /// <summary>The new rating (<see cref="AiChatFeedback.None"/> when the person took it back).</summary>
    public AiChatFeedback Value { get; }

    /// <summary>The rating before.</summary>
    public AiChatFeedback Previous { get; }
}

/// <summary>
/// The action bar of an AI reply (Arc chat): an optional <see cref="VersionPager"/> first (4px after it), then Copy, Retry,
/// Good response and Bad response, 30 × 30 each, no gap, the bar 4px apart and set 6px left and 2px up so the glyphs line
/// up with the text. Copy turns into a check for <see cref="CopiedDuration"/> (1.6s) after a press; the ratings are
/// mutually exclusive, a pressed rating fills its glyph, and pressing it again takes it back (<see cref="Feedback"/>, two-way).
/// Visibility: the actions show on the last reply (<see cref="IsLast"/>), when the owner reveals them for a hover or focus
/// inside the turn (<see cref="IsRevealed"/>) or always on touch (<see cref="AlwaysVisible"/>); hidden they keep their
/// height, are transparent and out of the tab order, and the pager stays. While the reply streams
/// (<see cref="IsStreaming"/>) the whole bar keeps its height but shows nothing. Opacity changes take 160ms (standard).
/// Every word comes from the owner; the bar raises <see cref="CopyRequested"/>, <see cref="Copied"/>,
/// <see cref="RetryRequested"/> and <see cref="FeedbackRequested"/>.
/// </summary>
[TemplatePart(CopyPart, typeof(AiActionButton))]
[TemplatePart(RetryPart, typeof(AiActionButton))]
[TemplatePart(GoodPart, typeof(AiRatingButton))]
[TemplatePart(BadPart, typeof(AiRatingButton))]
[PseudoClasses(ShownPseudoClass, StreamingPseudoClass, CopiedPseudoClass)]
internal class AiMessageActions : TemplatedControl
{
    public const string CopyPart = "PART_Copy";
    public const string RetryPart = "PART_Retry";
    public const string GoodPart = "PART_Good";
    public const string BadPart = "PART_Bad";

    /// <summary>Set while the four actions are shown.</summary>
    public const string ShownPseudoClass = ":shown";

    /// <summary>Set while the reply streams: the bar keeps its place but shows nothing.</summary>
    public const string StreamingPseudoClass = ":streaming";

    /// <summary>Set while Copy shows its check.</summary>
    public const string CopiedPseudoClass = ":copied";

    #region Properties

    /// <summary>The version pager shown before the actions (an <see cref="AiVersionPager"/>); it stays visible when the actions hide.</summary>
    public static readonly StyledProperty<Control?> VersionPagerProperty =
        AvaloniaProperty.Register<AiMessageActions, Control?>(nameof(VersionPager));

    /// <summary>The person's rating (two-way).</summary>
    public static readonly StyledProperty<AiChatFeedback> FeedbackProperty =
        AvaloniaProperty.Register<AiMessageActions, AiChatFeedback>(nameof(Feedback), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The reply is the last one: its actions always show.</summary>
    public static readonly StyledProperty<bool> IsLastProperty =
        AvaloniaProperty.Register<AiMessageActions, bool>(nameof(IsLast));

    /// <summary>The owner shows the actions (the pointer is over the turn or focus is inside it).</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<AiMessageActions, bool>(nameof(IsRevealed));

    /// <summary>The reply streams: the bar keeps its height but shows nothing.</summary>
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<AiMessageActions, bool>(nameof(IsStreaming));

    /// <summary>The actions always show (touch screens, where there is no hover).</summary>
    public static readonly StyledProperty<bool> AlwaysVisibleProperty =
        AvaloniaProperty.Register<AiMessageActions, bool>(nameof(AlwaysVisible));

    /// <summary>How long Copy shows its check (1.6s).</summary>
    public static readonly StyledProperty<TimeSpan> CopiedDurationProperty =
        AvaloniaProperty.Register<AiMessageActions, TimeSpan>(nameof(CopiedDuration), TimeSpan.FromSeconds(1.6));

    /// <summary>Copy's name and tooltip ("Copy").</summary>
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<AiMessageActions, string?>(nameof(CopyText));

    /// <summary>Copy's name and tooltip while it shows the check ("Copied"); null keeps <see cref="CopyText"/>.</summary>
    public static readonly StyledProperty<string?> CopiedTextProperty =
        AvaloniaProperty.Register<AiMessageActions, string?>(nameof(CopiedText));

    /// <summary>Retry's name and tooltip ("Retry").</summary>
    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<AiMessageActions, string?>(nameof(RetryText));

    /// <summary>The thumbs up's name and tooltip ("Good response").</summary>
    public static readonly StyledProperty<string?> GoodTextProperty =
        AvaloniaProperty.Register<AiMessageActions, string?>(nameof(GoodText));

    /// <summary>The thumbs down's name and tooltip ("Bad response").</summary>
    public static readonly StyledProperty<string?> BadTextProperty =
        AvaloniaProperty.Register<AiMessageActions, string?>(nameof(BadText));

    /// <summary>Raised (bubbling) when the person presses Copy; the owner copies the reply.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CopyRequestedEvent =
        RoutedEvent.Register<AiMessageActions, RoutedEventArgs>(nameof(CopyRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) right after <see cref="CopyRequestedEvent"/>, when Copy shows its check: the owner announces it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CopiedEvent =
        RoutedEvent.Register<AiMessageActions, RoutedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person presses Retry.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<AiMessageActions, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when the person rates the reply or takes the rating back; <see cref="Feedback"/> already holds it.</summary>
    public static readonly RoutedEvent<AiMessageActionsFeedbackEventArgs> FeedbackRequestedEvent =
        RoutedEvent.Register<AiMessageActions, AiMessageActionsFeedbackEventArgs>(nameof(FeedbackRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="VersionPagerProperty"/>
    public Control? VersionPager { get => GetValue(VersionPagerProperty); set => SetValue(VersionPagerProperty, value); }

    /// <inheritdoc cref="FeedbackProperty"/>
    public AiChatFeedback Feedback { get => GetValue(FeedbackProperty); set => SetValue(FeedbackProperty, value); }

    /// <inheritdoc cref="IsLastProperty"/>
    public bool IsLast { get => GetValue(IsLastProperty); set => SetValue(IsLastProperty, value); }

    /// <inheritdoc cref="IsRevealedProperty"/>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <inheritdoc cref="IsStreamingProperty"/>
    public bool IsStreaming { get => GetValue(IsStreamingProperty); set => SetValue(IsStreamingProperty, value); }

    /// <inheritdoc cref="AlwaysVisibleProperty"/>
    public bool AlwaysVisible { get => GetValue(AlwaysVisibleProperty); set => SetValue(AlwaysVisibleProperty, value); }

    /// <inheritdoc cref="CopiedDurationProperty"/>
    public TimeSpan CopiedDuration { get => GetValue(CopiedDurationProperty); set => SetValue(CopiedDurationProperty, value); }

    /// <inheritdoc cref="CopyTextProperty"/>
    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }

    /// <inheritdoc cref="CopiedTextProperty"/>
    public string? CopiedText { get => GetValue(CopiedTextProperty); set => SetValue(CopiedTextProperty, value); }

    /// <inheritdoc cref="RetryTextProperty"/>
    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    /// <inheritdoc cref="GoodTextProperty"/>
    public string? GoodText { get => GetValue(GoodTextProperty); set => SetValue(GoodTextProperty, value); }

    /// <inheritdoc cref="BadTextProperty"/>
    public string? BadText { get => GetValue(BadTextProperty); set => SetValue(BadTextProperty, value); }

    /// <inheritdoc cref="CopyRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CopyRequested { add => AddHandler(CopyRequestedEvent, value); remove => RemoveHandler(CopyRequestedEvent, value); }

    /// <inheritdoc cref="CopiedEvent"/>
    public event EventHandler<RoutedEventArgs>? Copied { add => AddHandler(CopiedEvent, value); remove => RemoveHandler(CopiedEvent, value); }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested { add => AddHandler(RetryRequestedEvent, value); remove => RemoveHandler(RetryRequestedEvent, value); }

    /// <inheritdoc cref="FeedbackRequestedEvent"/>
    public event EventHandler<AiMessageActionsFeedbackEventArgs>? FeedbackRequested
    {
        add => AddHandler(FeedbackRequestedEvent, value);
        remove => RemoveHandler(FeedbackRequestedEvent, value);
    }

    #endregion

    private AiActionButton? _copy;
    private AiActionButton? _retry;
    private AiRatingButton? _good;
    private AiRatingButton? _bad;
    private DateTimeOffset? _copiedUntil;
    private DispatcherTimer? _copiedTimer;

    public AiMessageActions()
    {
        UpdateVisibility();
    }

    /// <summary>Where the time of the copied check comes from (tests move it by hand and call <see cref="Tick"/>).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>Whether Copy shows its check.</summary>
    internal bool IsCopied => _copiedUntil is not null;

    /// <summary>Whether the four actions are shown.</summary>
    internal bool IsShown => !IsStreaming && (AlwaysVisible || IsLast || IsRevealed);

    internal AiActionButton? CopyButton => _copy;

    internal AiActionButton? RetryButton => _retry;

    internal AiRatingButton? GoodButton => _good;

    internal AiRatingButton? BadButton => _bad;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_copy is not null)
        {
            _copy.Click -= OnCopyClick;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_good is not null)
        {
            _good.Click -= OnGoodClick;
        }
        if (_bad is not null)
        {
            _bad.Click -= OnBadClick;
        }
        _copy = e.NameScope.Find<AiActionButton>(CopyPart);
        _retry = e.NameScope.Find<AiActionButton>(RetryPart);
        _good = e.NameScope.Find<AiRatingButton>(GoodPart);
        _bad = e.NameScope.Find<AiRatingButton>(BadPart);
        if (_copy is not null)
        {
            _copy.Click += OnCopyClick;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_good is not null)
        {
            _good.Click += OnGoodClick;
        }
        if (_bad is not null)
        {
            _bad.Click += OnBadClick;
        }
        UpdateCopy();
        UpdateTexts();
        UpdateRatings();
        UpdateVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLastProperty || change.Property == IsRevealedProperty || change.Property == IsStreamingProperty
            || change.Property == AlwaysVisibleProperty || change.Property == VersionPagerProperty)
        {
            UpdateVisibility();
        }
        else if (change.Property == FeedbackProperty)
        {
            UpdateRatings();
        }
        else if (change.Property == CopyTextProperty || change.Property == CopiedTextProperty || change.Property == RetryTextProperty
                 || change.Property == GoodTextProperty || change.Property == BadTextProperty)
        {
            UpdateTexts();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The check does not outlive the bar's stay in a window.
        _copiedTimer?.Stop();
        if (_copiedUntil is not null)
        {
            _copiedUntil = null;
            UpdateCopy();
        }
    }

    /// <summary>Hidden actions keep their place but take no pointer, keys or tab stops.</summary>
    private void UpdateVisibility()
    {
        var shown = IsShown;
        PseudoClasses.Set(ShownPseudoClass, shown);
        PseudoClasses.Set(StreamingPseudoClass, IsStreaming);
        foreach (var button in Buttons())
        {
            button.IsHitTestVisible = shown;
            KeyboardNavigation.SetIsTabStop(button, shown);
        }
        if (VersionPager is { } pager)
        {
            pager.IsHitTestVisible = !IsStreaming;
        }
    }

    private IEnumerable<Control> Buttons()
    {
        if (_copy is not null)
        {
            yield return _copy;
        }
        if (_retry is not null)
        {
            yield return _retry;
        }
        if (_good is not null)
        {
            yield return _good;
        }
        if (_bad is not null)
        {
            yield return _bad;
        }
    }

    private void UpdateTexts()
    {
        SetText(_retry, RetryText);
        SetText(_good, GoodText);
        SetText(_bad, BadText);
        UpdateCopy();
    }

    private static void SetText(Control? button, string? text)
    {
        if (button is null)
        {
            return;
        }
        AutomationProperties.SetName(button, text ?? string.Empty);
        Tooltip.SetTip(button, string.IsNullOrEmpty(text) ? null : text);
    }

    private void UpdateCopy()
    {
        var copied = _copiedUntil is not null;
        PseudoClasses.Set(CopiedPseudoClass, copied);
        if (_copy is null)
        {
            return;
        }
        _copy.Glyph = copied ? ArcIcons.Check : ArcIcons.Copy;
        SetText(_copy, copied ? CopiedText ?? CopyText : CopyText);
    }

    private void UpdateRatings()
    {
        if (_good is not null)
        {
            _good.IsPressedOn = Feedback == AiChatFeedback.Up;
        }
        if (_bad is not null)
        {
            _bad.IsPressedOn = Feedback == AiChatFeedback.Down;
        }
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(CopyRequestedEvent, this));
        ShowCopied();
    }

    /// <summary>Shows the check for <see cref="CopiedDuration"/> (a new copy starts the time again) and raises <see cref="Copied"/>.</summary>
    internal void ShowCopied()
    {
        _copiedUntil = Time.GetUtcNow() + CopiedDuration;
        UpdateCopy();
        if (_copiedTimer is null)
        {
            _copiedTimer = new DispatcherTimer();
            _copiedTimer.Tick += (_, _) => Tick();
        }
        _copiedTimer.Stop();
        _copiedTimer.Interval = CopiedDuration > TimeSpan.Zero ? CopiedDuration : TimeSpan.FromMilliseconds(1);
        _copiedTimer.Start();
        RaiseEvent(new RoutedEventArgs(CopiedEvent, this));
    }

    /// <summary>Turns the check back into Copy once its time is up (the timer calls it; tests call it after moving the time).</summary>
    internal void Tick()
    {
        if (_copiedUntil is not { } until)
        {
            _copiedTimer?.Stop();
            return;
        }
        var now = Time.GetUtcNow();
        if (now < until)
        {
            // Early (a timer runs on its own clock): wait for the rest.
            if (_copiedTimer is not null)
            {
                _copiedTimer.Interval = until - now;
            }
            return;
        }
        _copiedTimer?.Stop();
        _copiedUntil = null;
        UpdateCopy();
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent, this));
    }

    private void OnGoodClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Rate(AiChatFeedback.Up);
    }

    private void OnBadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Rate(AiChatFeedback.Down);
    }

    /// <summary>Chooses <paramref name="rating"/>, or takes it back when it is already chosen (the other one clears).</summary>
    private void Rate(AiChatFeedback rating)
    {
        var previous = Feedback;
        var value = previous == rating ? AiChatFeedback.None : rating;
        SetCurrentValue(FeedbackProperty, value);
        RaiseEvent(new AiMessageActionsFeedbackEventArgs(FeedbackRequestedEvent, this, value, previous));
    }
}
