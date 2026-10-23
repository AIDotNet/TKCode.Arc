using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A release gate: a few checks that must all be done before the one action unlocks. A header names the release and its
/// owners, a bar fills as checks are ticked (and turns green when every required one is done), a switch chooses whether
/// to notify a channel, and the button runs <see cref="ReleaseAction"/>. While it runs the button shows a spinner and the
/// checks lock; on success the button reads <see cref="ReleasedText"/>, a status line opens under the footer and a reset
/// button appears; on failure the error shows in the same line and the release can be tried again.
/// <code>
/// &lt;arc:ReleaseReadiness Title="Version 5.3" Subtitle="Lumen mobile · goes live Tuesday" ChannelName="#launches"&gt;
///     &lt;arc:ReleaseCheck Title="Design signed off" Description="Final screens approved by product" IsDone="True" /&gt;
///     &lt;arc:ReleaseCheck Title="Smoke tests green" Description="Build 5.3.0-beta.2 passed on preview" /&gt;
/// &lt;/arc:ReleaseReadiness&gt;
/// </code>
/// </summary>
public partial class ReleaseReadiness : TemplatedControl
{
    #region Properties

    /// <summary>The heading (the release's name, a version); null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string?>(nameof(Title));

    /// <summary>The line under the heading (where and when it ships); null leaves it out.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string?>(nameof(Subtitle));

    /// <summary>The owners shown as overlapping avatars, when you give them as a list to follow; it wins over <see cref="Owners"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> OwnersSourceProperty =
        AvaloniaProperty.Register<ReleaseReadiness, IEnumerable?>(nameof(OwnersSource));

    /// <summary>The screen reader name of the owners' avatars; null reads <see cref="OwnersLabelPrefix"/> and the names ("Owners: A and B").</summary>
    public static readonly StyledProperty<string?> OwnersLabelProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string?>(nameof(OwnersLabel));

    /// <summary>Put before the owners' names in their generated screen reader name.</summary>
    public static readonly StyledProperty<string> OwnersLabelPrefixProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(OwnersLabelPrefix), "Owners: ");

    /// <summary>The word joining the last two owners' names ("and").</summary>
    public static readonly StyledProperty<string> AndWordProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(AndWord), "and");

    /// <summary>The checks, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Checks"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ChecksSourceProperty =
        AvaloniaProperty.Register<ReleaseReadiness, IEnumerable?>(nameof(ChecksSource));

    /// <summary>The screen reader name of the list of checks.</summary>
    public static readonly StyledProperty<string> ChecksLabelProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ChecksLabel), "Release checks");

    /// <summary>The screen reader name of the progress bar.</summary>
    public static readonly StyledProperty<string> ProgressLabelProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ProgressLabel), "Readiness");

    /// <summary>Whether the notify switch is on (two-way). A reset leaves it as it is.</summary>
    public static readonly StyledProperty<bool> IsNotifyEnabledProperty =
        AvaloniaProperty.Register<ReleaseReadiness, bool>(nameof(IsNotifyEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the footer shows the notify switch.</summary>
    public static readonly StyledProperty<bool> IsNotifyVisibleProperty =
        AvaloniaProperty.Register<ReleaseReadiness, bool>(nameof(IsNotifyVisible), true);

    /// <summary>The notify switch's text; null formats <see cref="NotifyFormat"/> with <see cref="ChannelName"/>.</summary>
    public static readonly StyledProperty<string?> NotifyTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string?>(nameof(NotifyText));

    /// <summary>The notify switch's text when <see cref="NotifyText"/> is null; {0} is <see cref="ChannelName"/>.</summary>
    public static readonly StyledProperty<string> NotifyFormatProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(NotifyFormat), "Notify {0}");

    /// <summary>The channel told about the release ("#launches"), used by the switch and the success line.</summary>
    public static readonly StyledProperty<string?> ChannelNameProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string?>(nameof(ChannelName));

    /// <summary>The release's name in the success line; null uses <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> ReleaseNameProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string?>(nameof(ReleaseName));

    /// <summary>
    /// Starts the release: awaited when the button is pressed (or <see cref="ReleaseAsync"/> is called) with what the
    /// person chose and a token that <see cref="CancelRelease"/> (or the block leaving the window) cancels. An exception
    /// fails the attempt and shows its message; a cancellation returns the gate to ready without an error. Null releases at once.
    /// </summary>
    public static readonly StyledProperty<Func<ReleaseRequest, CancellationToken, Task>?> ReleaseActionProperty =
        AvaloniaProperty.Register<ReleaseReadiness, Func<ReleaseRequest, CancellationToken, Task>?>(nameof(ReleaseAction));

    /// <summary>Run when a release starts, with the <see cref="ReleaseRequest"/> as the parameter, before <see cref="ReleaseAction"/>; when it cannot execute the release does not start.</summary>
    public static readonly StyledProperty<ICommand?> ReleaseCommandProperty =
        AvaloniaProperty.Register<ReleaseReadiness, ICommand?>(nameof(ReleaseCommand));

    /// <summary>The release button's text before the release (an arrow follows it).</summary>
    public static readonly StyledProperty<string> ReleaseTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ReleaseText), "Start rollout");

    /// <summary>The release button's text once the release started (a check mark leads it).</summary>
    public static readonly StyledProperty<string> ReleasedTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ReleasedText), "Rollout started");

    /// <summary>The text of the button that appears after a release and puts the checks back.</summary>
    public static readonly StyledProperty<string> ResetTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ResetText), "Start over");

    /// <summary>Whether the reset button appears after a release.</summary>
    public static readonly StyledProperty<bool> IsResetVisibleProperty =
        AvaloniaProperty.Register<ReleaseReadiness, bool>(nameof(IsResetVisible), true);

    /// <summary>The progress text while more than one required check is open; {0} is how many.</summary>
    public static readonly StyledProperty<string> RemainingFormatProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(RemainingFormat), "{0} to go");

    /// <summary>The progress text while one required check is open.</summary>
    public static readonly StyledProperty<string> RemainingOneTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(RemainingOneText), "1 to go");

    /// <summary>The progress text when every required check is done.</summary>
    public static readonly StyledProperty<string> ReadyTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ReadyText), "All clear");

    /// <summary>The progress text from the moment the release starts until a reset.</summary>
    public static readonly StyledProperty<string> ReleasingTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(ReleasingText), "Rolling out");

    /// <summary>The count on the right of the progress text; {0} is the required checks done, {1} all required checks.</summary>
    public static readonly StyledProperty<string> CountFormatProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(CountFormat), "{0} of {1}");

    /// <summary>The success line when nobody was notified; {0} is the release's name.</summary>
    public static readonly StyledProperty<string> SuccessFormatProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(SuccessFormat), "{0} is on its way.");

    /// <summary>The success line when the channel was notified; {0} is the release's name, {1} the channel.</summary>
    public static readonly StyledProperty<string> SuccessNotifiedFormatProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(SuccessNotifiedFormat), "{0} is on its way. We posted in {1}.");

    /// <summary>The error line when the exception has no message and no <see cref="ErrorFormatter"/> is set.</summary>
    public static readonly StyledProperty<string> FailureTextProperty =
        AvaloniaProperty.Register<ReleaseReadiness, string>(nameof(FailureText), "The rollout did not start. Try again.");

    /// <summary>Turns a failed attempt's exception into the error line; null shows the exception's message.</summary>
    public static readonly StyledProperty<Func<Exception, string>?> ErrorFormatterProperty =
        AvaloniaProperty.Register<ReleaseReadiness, Func<Exception, string>?>(nameof(ErrorFormatter));

    /// <summary>Draws a 560-wide bordered card (true); false lays the block flat on the page at the container's width.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<ReleaseReadiness, bool>(nameof(IsFramed), true);

    /// <summary>Where the gate stands.</summary>
    public static readonly DirectProperty<ReleaseReadiness, ReleaseReadinessState> StateProperty =
        AvaloniaProperty.RegisterDirect<ReleaseReadiness, ReleaseReadinessState>(nameof(State), o => o.State);

    /// <summary>How many required checks are done.</summary>
    public static readonly DirectProperty<ReleaseReadiness, int> DoneCountProperty =
        AvaloniaProperty.RegisterDirect<ReleaseReadiness, int>(nameof(DoneCount), o => o.DoneCount);

    /// <summary>How many checks are required (optional ones are not counted).</summary>
    public static readonly DirectProperty<ReleaseReadiness, int> TotalCountProperty =
        AvaloniaProperty.RegisterDirect<ReleaseReadiness, int>(nameof(TotalCount), o => o.TotalCount);

    /// <summary>True when there is at least one check and every required one is done.</summary>
    public static readonly DirectProperty<ReleaseReadiness, bool> IsReadyProperty =
        AvaloniaProperty.RegisterDirect<ReleaseReadiness, bool>(nameof(IsReady), o => o.IsReady);

    /// <summary>The error line of the last failed attempt; null unless <see cref="State"/> is Failed.</summary>
    public static readonly DirectProperty<ReleaseReadiness, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<ReleaseReadiness, string?>(nameof(ErrorText), o => o.ErrorText);

    /// <summary>The line shown under the footer (the success or the error line); null while it is closed.</summary>
    public static readonly DirectProperty<ReleaseReadiness, string?> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<ReleaseReadiness, string?>(nameof(StatusText), o => o.StatusText);

    #endregion

    #region Events

    /// <summary>Raised when a check is ticked or cleared, by the person or by code.</summary>
    public static readonly RoutedEvent<ReleaseCheckChangedEventArgs> CheckChangedEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleaseCheckChangedEventArgs>(nameof(CheckChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="IsReady"/> changes.</summary>
    public static readonly RoutedEvent<ReleaseReadyChangedEventArgs> ReadyChangedEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleaseReadyChangedEventArgs>(nameof(ReadyChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    public static readonly RoutedEvent<ReleaseStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleaseStateChangedEventArgs>(nameof(StateChanged), RoutingStrategies.Bubble);

    /// <summary>Raised before a release runs; setting <see cref="ReleaseStartingEventArgs.Cancel"/> keeps it from starting.</summary>
    public static readonly RoutedEvent<ReleaseStartingEventArgs> ReleaseStartingEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleaseStartingEventArgs>(nameof(ReleaseStarting), RoutingStrategies.Bubble);

    /// <summary>Raised when the release action finished without an error.</summary>
    public static readonly RoutedEvent<ReleasedEventArgs> ReleasedEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleasedEventArgs>(nameof(Released), RoutingStrategies.Bubble);

    /// <summary>Raised when the release action or command threw.</summary>
    public static readonly RoutedEvent<ReleaseFailedEventArgs> ReleaseFailedEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleaseFailedEventArgs>(nameof(ReleaseFailed), RoutingStrategies.Bubble);

    /// <summary>Raised after <see cref="Reset"/> put the checks back.</summary>
    public static readonly RoutedEvent<ReleaseResetEventArgs> ResetPerformedEvent =
        RoutedEvent.Register<ReleaseReadiness, ReleaseResetEventArgs>(nameof(ResetPerformed), RoutingStrategies.Bubble);

    /// <summary>Raised when a check is ticked or cleared, by the person or by code.</summary>
    public event EventHandler<ReleaseCheckChangedEventArgs>? CheckChanged
    {
        add => AddHandler(CheckChangedEvent, value);
        remove => RemoveHandler(CheckChangedEvent, value);
    }

    /// <summary>Raised when <see cref="IsReady"/> changes.</summary>
    public event EventHandler<ReleaseReadyChangedEventArgs>? ReadyChanged
    {
        add => AddHandler(ReadyChangedEvent, value);
        remove => RemoveHandler(ReadyChangedEvent, value);
    }

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    public event EventHandler<ReleaseStateChangedEventArgs>? StateChanged
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <summary>Raised before a release runs; set <see cref="ReleaseStartingEventArgs.Cancel"/> to keep it from starting.</summary>
    public event EventHandler<ReleaseStartingEventArgs>? ReleaseStarting
    {
        add => AddHandler(ReleaseStartingEvent, value);
        remove => RemoveHandler(ReleaseStartingEvent, value);
    }

    /// <summary>Raised when the release action finished without an error.</summary>
    public event EventHandler<ReleasedEventArgs>? Released
    {
        add => AddHandler(ReleasedEvent, value);
        remove => RemoveHandler(ReleasedEvent, value);
    }

    /// <summary>Raised when the release action or command threw.</summary>
    public event EventHandler<ReleaseFailedEventArgs>? ReleaseFailed
    {
        add => AddHandler(ReleaseFailedEvent, value);
        remove => RemoveHandler(ReleaseFailedEvent, value);
    }

    /// <summary>Raised after <see cref="Reset"/> put the checks back.</summary>
    public event EventHandler<ReleaseResetEventArgs>? ResetPerformed
    {
        add => AddHandler(ResetPerformedEvent, value);
        remove => RemoveHandler(ResetPerformedEvent, value);
    }

    #endregion

    private ReleaseReadinessState _state;
    private int _doneCount;
    private int _totalCount;
    private bool _isReady;
    private string? _errorText;
    private string? _statusText;

    /// <summary>The checks, in order, when no <see cref="ChecksSource"/> is set. Ticking one writes its <see cref="ReleaseCheck.IsDone"/>.</summary>
    [Content]
    public AvaloniaList<ReleaseCheck> Checks { get; } = new();

    /// <summary>The owners shown as overlapping avatars at the right of the header, when no <see cref="OwnersSource"/> is set.</summary>
    public AvaloniaList<ProjectPerson> Owners { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="OwnersSourceProperty"/>
    public IEnumerable? OwnersSource { get => GetValue(OwnersSourceProperty); set => SetValue(OwnersSourceProperty, value); }

    /// <inheritdoc cref="OwnersLabelProperty"/>
    public string? OwnersLabel { get => GetValue(OwnersLabelProperty); set => SetValue(OwnersLabelProperty, value); }

    /// <inheritdoc cref="OwnersLabelPrefixProperty"/>
    public string OwnersLabelPrefix { get => GetValue(OwnersLabelPrefixProperty); set => SetValue(OwnersLabelPrefixProperty, value); }

    /// <inheritdoc cref="AndWordProperty"/>
    public string AndWord { get => GetValue(AndWordProperty); set => SetValue(AndWordProperty, value); }

    /// <inheritdoc cref="ChecksSourceProperty"/>
    public IEnumerable? ChecksSource { get => GetValue(ChecksSourceProperty); set => SetValue(ChecksSourceProperty, value); }

    /// <inheritdoc cref="ChecksLabelProperty"/>
    public string ChecksLabel { get => GetValue(ChecksLabelProperty); set => SetValue(ChecksLabelProperty, value); }

    /// <inheritdoc cref="ProgressLabelProperty"/>
    public string ProgressLabel { get => GetValue(ProgressLabelProperty); set => SetValue(ProgressLabelProperty, value); }

    /// <inheritdoc cref="IsNotifyEnabledProperty"/>
    public bool IsNotifyEnabled { get => GetValue(IsNotifyEnabledProperty); set => SetValue(IsNotifyEnabledProperty, value); }

    /// <inheritdoc cref="IsNotifyVisibleProperty"/>
    public bool IsNotifyVisible { get => GetValue(IsNotifyVisibleProperty); set => SetValue(IsNotifyVisibleProperty, value); }

    /// <inheritdoc cref="NotifyTextProperty"/>
    public string? NotifyText { get => GetValue(NotifyTextProperty); set => SetValue(NotifyTextProperty, value); }

    /// <inheritdoc cref="NotifyFormatProperty"/>
    public string NotifyFormat { get => GetValue(NotifyFormatProperty); set => SetValue(NotifyFormatProperty, value); }

    /// <inheritdoc cref="ChannelNameProperty"/>
    public string? ChannelName { get => GetValue(ChannelNameProperty); set => SetValue(ChannelNameProperty, value); }

    /// <inheritdoc cref="ReleaseNameProperty"/>
    public string? ReleaseName { get => GetValue(ReleaseNameProperty); set => SetValue(ReleaseNameProperty, value); }

    /// <inheritdoc cref="ReleaseActionProperty"/>
    public Func<ReleaseRequest, CancellationToken, Task>? ReleaseAction
    {
        get => GetValue(ReleaseActionProperty);
        set => SetValue(ReleaseActionProperty, value);
    }

    /// <inheritdoc cref="ReleaseCommandProperty"/>
    public ICommand? ReleaseCommand { get => GetValue(ReleaseCommandProperty); set => SetValue(ReleaseCommandProperty, value); }

    /// <inheritdoc cref="ReleaseTextProperty"/>
    public string ReleaseText { get => GetValue(ReleaseTextProperty); set => SetValue(ReleaseTextProperty, value); }

    /// <inheritdoc cref="ReleasedTextProperty"/>
    public string ReleasedText { get => GetValue(ReleasedTextProperty); set => SetValue(ReleasedTextProperty, value); }

    /// <inheritdoc cref="ResetTextProperty"/>
    public string ResetText { get => GetValue(ResetTextProperty); set => SetValue(ResetTextProperty, value); }

    /// <inheritdoc cref="IsResetVisibleProperty"/>
    public bool IsResetVisible { get => GetValue(IsResetVisibleProperty); set => SetValue(IsResetVisibleProperty, value); }

    /// <inheritdoc cref="RemainingFormatProperty"/>
    public string RemainingFormat { get => GetValue(RemainingFormatProperty); set => SetValue(RemainingFormatProperty, value); }

    /// <inheritdoc cref="RemainingOneTextProperty"/>
    public string RemainingOneText { get => GetValue(RemainingOneTextProperty); set => SetValue(RemainingOneTextProperty, value); }

    /// <inheritdoc cref="ReadyTextProperty"/>
    public string ReadyText { get => GetValue(ReadyTextProperty); set => SetValue(ReadyTextProperty, value); }

    /// <inheritdoc cref="ReleasingTextProperty"/>
    public string ReleasingText { get => GetValue(ReleasingTextProperty); set => SetValue(ReleasingTextProperty, value); }

    /// <inheritdoc cref="CountFormatProperty"/>
    public string CountFormat { get => GetValue(CountFormatProperty); set => SetValue(CountFormatProperty, value); }

    /// <inheritdoc cref="SuccessFormatProperty"/>
    public string SuccessFormat { get => GetValue(SuccessFormatProperty); set => SetValue(SuccessFormatProperty, value); }

    /// <inheritdoc cref="SuccessNotifiedFormatProperty"/>
    public string SuccessNotifiedFormat
    {
        get => GetValue(SuccessNotifiedFormatProperty);
        set => SetValue(SuccessNotifiedFormatProperty, value);
    }

    /// <inheritdoc cref="FailureTextProperty"/>
    public string FailureText { get => GetValue(FailureTextProperty); set => SetValue(FailureTextProperty, value); }

    /// <inheritdoc cref="ErrorFormatterProperty"/>
    public Func<Exception, string>? ErrorFormatter { get => GetValue(ErrorFormatterProperty); set => SetValue(ErrorFormatterProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public ReleaseReadinessState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="DoneCountProperty"/>
    public int DoneCount { get => _doneCount; private set => SetAndRaise(DoneCountProperty, ref _doneCount, value); }

    /// <inheritdoc cref="TotalCountProperty"/>
    public int TotalCount { get => _totalCount; private set => SetAndRaise(TotalCountProperty, ref _totalCount, value); }

    /// <inheritdoc cref="IsReadyProperty"/>
    public bool IsReady { get => _isReady; private set => SetAndRaise(IsReadyProperty, ref _isReady, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => _errorText; private set => SetAndRaise(ErrorTextProperty, ref _errorText, value); }

    /// <inheritdoc cref="StatusTextProperty"/>
    public string? StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }
}
