using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>What an <see cref="AiRunStatusLine"/> reports.</summary>
internal enum AiRunStatusKind
{
    /// <summary>Nothing has run: muted text, no icon.</summary>
    Idle,

    /// <summary>A run is going: the text shimmers, the count follows it.</summary>
    Running,

    /// <summary>Values need a check: a Warning triangle, the text and the action pill.</summary>
    NeedsCheck,

    /// <summary>The run finished with nothing to check: a Success check.</summary>
    Done,

    /// <summary>Everything was accepted: a Success check.</summary>
    Accepted,

    /// <summary>The person stopped the run: a filled muted square and a second line.</summary>
    Stopped,

    /// <summary>The run failed: a Danger alert circle and a second line; announced as an alert.</summary>
    Failed
}

/// <summary>
/// The status line at the top of an extraction's form (Arc document extraction): sticky, on the surface, with 10px above,
/// 8px below, 14px left and 12px right, and a 1px bottom edge that turns <c>BorderSubtle</c> once the form scrolled
/// (<see cref="ShowsEdge"/>, 160ms). A row at least 30 high holds a 16px icon, 8px, the text (14 / 19.6) and, while running,
/// the count (muted, tabular) 8px after it:
/// <list type="bullet">
/// <item>Idle: muted text, no icon; Running: the text shimmers (<see cref="TextShimmer"/>, while <see cref="IsShimmering"/>);</item>
/// <item>NeedsCheck: a Warning <c>triangle-alert</c>, the text in Foreground and, at the right, the action pill
/// (<see cref="ActionText"/>, raising <see cref="ActionRequested"/>);</item>
/// <item>Done and Accepted: a Success <c>circle-check</c> and secondary text;</item>
/// <item>Stopped: a filled muted square and secondary text; Failed: a Danger <c>circle-alert</c>, Foreground text and an
/// alert for screen readers. Both show <see cref="DetailText"/> under the row (12 / 16.8, muted, 24px in, 4px below).</item>
/// </list>
/// A new kind, text or detail is a new face (set together, they count once: the face is made before the next layout) that
/// crossfades in (the old one fades out in 100ms out of layout, the new one in over 160ms) while the height follows on
/// <see cref="Motion.ArcSpring.Smooth"/> (30 → 68 when a two-line detail appears); the count and the shimmer change in place. The pill sits outside the swap, so it keeps focus while the text changes,
/// and fades in and out on its own. Reduced motion crossfades in 120ms and jumps the height; Off jumps. Every word comes
/// from the owner; the line reads as its text, count and detail.
/// </summary>
[TemplatePart(FacePart, typeof(ArcContentSwap))]
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(ActionPart, typeof(AiRunStatusAction))]
[TemplatePart(ActionPresencePart, typeof(PresenceHost))]
[PseudoClasses(EdgePseudoClass, ActionPseudoClass)]
internal sealed class AiRunStatusLine : TemplatedControl
{
    public const string FacePart = "PART_Face";
    public const string SizePart = "PART_Size";
    public const string ActionPart = "PART_Action";
    public const string ActionPresencePart = "PART_ActionPresence";

    /// <summary>Set while the bottom edge shows.</summary>
    public const string EdgePseudoClass = ":edge";

    /// <summary>Set while the action pill shows.</summary>
    public const string ActionPseudoClass = ":action";

    /// <summary>What the line reports.</summary>
    public static readonly StyledProperty<AiRunStatusKind> KindProperty =
        AvaloniaProperty.Register<AiRunStatusLine, AiRunStatusKind>(nameof(Kind));

    /// <summary>The main text ("Extracting fields", "2 fields need a check").</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiRunStatusLine, string?>(nameof(Text));

    /// <summary>The count after the text while running ("3 of 10"); null or empty shows none.</summary>
    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<AiRunStatusLine, string?>(nameof(CountText));

    /// <summary>The second line of a stopped or failed run; null or empty shows none.</summary>
    public static readonly StyledProperty<string?> DetailTextProperty =
        AvaloniaProperty.Register<AiRunStatusLine, string?>(nameof(DetailText));

    /// <summary>The action pill's word ("Review"); it shows while <see cref="Kind"/> is NeedsCheck and this is not empty.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<AiRunStatusLine, string?>(nameof(ActionText));

    /// <summary>Whether the running text sweeps (the shimmer rests below full motion on its own).</summary>
    public static readonly StyledProperty<bool> IsShimmeringProperty =
        AvaloniaProperty.Register<AiRunStatusLine, bool>(nameof(IsShimmering), true);

    /// <summary>Whether the bottom edge shows (the form under the line has scrolled).</summary>
    public static readonly StyledProperty<bool> ShowsEdgeProperty =
        AvaloniaProperty.Register<AiRunStatusLine, bool>(nameof(ShowsEdge));

    /// <summary>Raised when the action pill is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActionRequestedEvent =
        RoutedEvent.Register<AiRunStatusLine, RoutedEventArgs>(nameof(ActionRequested), RoutingStrategies.Bubble);

    /// <summary>Whether the action pill shows (template use).</summary>
    internal static readonly DirectProperty<AiRunStatusLine, bool> ShowsActionProperty =
        AvaloniaProperty.RegisterDirect<AiRunStatusLine, bool>(nameof(ShowsAction), o => o.ShowsAction);

    private ArcContentSwap? _swap;
    private AiRunStatusAction? _action;
    private AiRunStatusFace? _face;
    private bool _showsAction;
    private bool _refreshPending;

    static AiRunStatusLine()
    {
        FocusableProperty.OverrideDefaultValue<AiRunStatusLine>(false);
        IsTabStopProperty.OverrideDefaultValue<AiRunStatusLine>(false);
    }

    public AiRunStatusLine()
    {
        UpdateState();
    }

    /// <inheritdoc cref="KindProperty"/>
    public AiRunStatusKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="CountTextProperty"/>
    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <inheritdoc cref="DetailTextProperty"/>
    public string? DetailText { get => GetValue(DetailTextProperty); set => SetValue(DetailTextProperty, value); }

    /// <inheritdoc cref="ActionTextProperty"/>
    public string? ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }

    /// <inheritdoc cref="IsShimmeringProperty"/>
    public bool IsShimmering { get => GetValue(IsShimmeringProperty); set => SetValue(IsShimmeringProperty, value); }

    /// <inheritdoc cref="ShowsEdgeProperty"/>
    public bool ShowsEdge { get => GetValue(ShowsEdgeProperty); set => SetValue(ShowsEdgeProperty, value); }

    /// <summary>Raised when the action pill is pressed.</summary>
    public event EventHandler<RoutedEventArgs>? ActionRequested
    {
        add => AddHandler(ActionRequestedEvent, value);
        remove => RemoveHandler(ActionRequestedEvent, value);
    }

    internal bool ShowsAction { get => _showsAction; private set => SetAndRaise(ShowsActionProperty, ref _showsAction, value); }

    /// <summary>The face on screen now.</summary>
    internal AiRunStatusFace? Face => _face;

    /// <summary>The action pill (template part).</summary>
    internal AiRunStatusAction? Action => _action;

    /// <summary>What the line reads as: the text, the count and the detail.</summary>
    internal string SpokenText { get; private set; } = string.Empty;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        _swap = e.NameScope.Find<ArcContentSwap>(FacePart);
        _action = e.NameScope.Find<AiRunStatusAction>(ActionPart);
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        _face = null;
        Refresh(instant: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == TextProperty || change.Property == DetailTextProperty)
        {
            // The owner sets kind, text and detail one after the other: they make one new face, before the next layout.
            UpdateState();
            ScheduleRefresh();
        }
        else if (change.Property == CountTextProperty || change.Property == IsShimmeringProperty)
        {
            // In place: a new count does not swap the face (the shimmer would start over every value).
            if (_face is { } face)
            {
                face.CountText = CountText;
                face.IsShimmering = IsShimmering;
            }
            UpdateState();
        }
        else if (change.Property == ActionTextProperty || change.Property == ShowsEdgeProperty)
        {
            UpdateState();
        }
    }

    private void ScheduleRefresh()
    {
        if (_refreshPending)
        {
            return;
        }
        _refreshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPending = false;
            // Before the first layout there is nothing on screen to fade from.
            Refresh(instant: !IsLoaded);
        }, DispatcherPriority.Normal);
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ActionRequestedEvent, this));
    }

    /// <summary>Brings the face in line with the kind, text and detail: a different one is a new face (at once when <paramref name="instant"/>).</summary>
    private void Refresh(bool instant)
    {
        UpdateState();
        if (_swap is null)
        {
            return;
        }
        var kind = Kind;
        var text = Text ?? string.Empty;
        var detail = DetailText ?? string.Empty;
        if (_face is { } current && current.Kind == kind && current.Text == text && (current.DetailText ?? string.Empty) == detail)
        {
            current.CountText = CountText;
            current.IsShimmering = IsShimmering;
            return;
        }
        _face = new AiRunStatusFace
        {
            Kind = kind,
            Text = text,
            DetailText = string.IsNullOrEmpty(detail) ? null : detail,
            CountText = CountText,
            IsShimmering = IsShimmering
        };
        if (instant)
        {
            // SetCurrentValue keeps the template's mode the value a theme can still change.
            var mode = _swap.Mode;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
            _swap.Content = _face;
            _swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
        }
        else
        {
            _swap.Content = _face;
        }
    }

    private void UpdateState()
    {
        var kind = Kind;
        var showsAction = kind == AiRunStatusKind.NeedsCheck && !string.IsNullOrEmpty(ActionText);
        if (showsAction != ShowsAction)
        {
            ShowsAction = showsAction;
            if (ControlAutomationPeer.FromElement(this) is LinePeer peer)
            {
                peer.ActionChanged();
            }
        }
        PseudoClasses.Set(ActionPseudoClass, ShowsAction);
        PseudoClasses.Set(EdgePseudoClass, ShowsEdge);
        var spoken = Join(Text, kind == AiRunStatusKind.Running ? CountText : null, " ");
        spoken = Join(spoken, kind is AiRunStatusKind.Stopped or AiRunStatusKind.Failed ? DetailText : null, ". ");
        SpokenText = spoken;
        AutomationProperties.SetName(this, spoken);
        // Arc gives the failure role=alert: an assertive live region; the owner's polite regions announce the rest.
        AutomationProperties.SetLiveSetting(this, kind == AiRunStatusKind.Failed ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Off);
        AutomationProperties.SetItemStatus(this, kind == AiRunStatusKind.Running ? "Busy" : null);
    }

    private static string Join(string? first, string? second, string separator)
    {
        if (string.IsNullOrEmpty(first))
        {
            return second ?? string.Empty;
        }
        return string.IsNullOrEmpty(second) ? first : first + separator + second;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LinePeer(this);

    /// <summary>One piece of text named by what it says; the faces inside are not read again, the action pill is.</summary>
    private sealed class LinePeer(AiRunStatusLine owner) : ControlAutomationPeer(owner)
    {
        private AiRunStatusLine Line => (AiRunStatusLine)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => Line.SpokenText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            if (Line.ShowsAction && Line.Action is { } action)
            {
                return [CreatePeerForElement(action)];
            }
            return null;
        }

        /// <summary>The pill came or went: the children are asked again.</summary>
        public void ActionChanged() => InvalidateChildren();
    }
}
