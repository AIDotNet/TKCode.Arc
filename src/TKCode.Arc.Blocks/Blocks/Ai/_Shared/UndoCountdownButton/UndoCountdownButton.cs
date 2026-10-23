using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The bordered "Undo" pill of a forgotten memory: a 14px ring that drains clockwise from 12 o'clock, the word and the
/// seconds left. <see cref="Start"/> opens a window of <see cref="Duration"/>; the seconds count down once per whole
/// second and, at zero, the button raises <see cref="Elapsed"/> once and disables itself. Pressing it (<c>Click</c>)
/// means "undo": it stops the countdown first, so <see cref="Elapsed"/> never follows a press.
/// <code>
/// &lt;arc:UndoCountdownButton Text="Undo" Duration="0:0:5" Elapsed="OnForgotten" Click="OnUndo" /&gt;
/// </code>
/// All countdowns share one <see cref="UndoClock"/>. A countdown keeps its deadline: taken out of the tree it stops
/// ticking, and put back it catches up with the time that passed. Reduced motion and Off keep the ring full and still;
/// the seconds and <see cref="Elapsed"/> are status, not decoration, so they run at every motion level.
/// </summary>
[TemplatePart(RingPart, typeof(ArcRing))]
[PseudoClasses(HoverPseudoClass, RunningPseudoClass)]
internal class UndoCountdownButton : AvaloniaButton
{
    public const string RingPart = "PART_Ring";

    /// <summary>Mouse or pen over the button (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Set while the countdown runs.</summary>
    public const string RunningPseudoClass = ":running";

    #region Properties

    /// <summary>The length of the undo window (default 5 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<UndoCountdownButton, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(5));

    /// <summary>The word on the button ("Undo"); also the start of its accessible name.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<UndoCountdownButton, string?>(nameof(Text));

    /// <summary>The visible seconds ("5 s") for the whole seconds left; null shows the number alone.</summary>
    public static readonly StyledProperty<Func<int, string>?> SecondsTextProperty =
        AvaloniaProperty.Register<UndoCountdownButton, Func<int, string>?>(nameof(SecondsText));

    /// <summary>The spoken seconds ("5 seconds left") that follow <see cref="Text"/> in the accessible name; null names the button by its text alone.</summary>
    public static readonly StyledProperty<Func<int, string>?> SecondsNameProperty =
        AvaloniaProperty.Register<UndoCountdownButton, Func<int, string>?>(nameof(SecondsName));

    /// <summary>The share of the window left, from 1 at the start to 0 at the end.</summary>
    public static readonly DirectProperty<UndoCountdownButton, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<UndoCountdownButton, double>(nameof(Progress), o => o.Progress);

    /// <summary>The time left in the window.</summary>
    public static readonly DirectProperty<UndoCountdownButton, TimeSpan> RemainingProperty =
        AvaloniaProperty.RegisterDirect<UndoCountdownButton, TimeSpan>(nameof(Remaining), o => o.Remaining);

    /// <summary>The whole seconds shown (the time left rounded up, at least 1 so the last moment reads "1 s").</summary>
    public static readonly DirectProperty<UndoCountdownButton, int> SecondsProperty =
        AvaloniaProperty.RegisterDirect<UndoCountdownButton, int>(nameof(Seconds), o => o.Seconds);

    /// <summary>The visible seconds text (<see cref="SecondsText"/> of <see cref="Seconds"/>).</summary>
    public static readonly DirectProperty<UndoCountdownButton, string?> SecondsLabelProperty =
        AvaloniaProperty.RegisterDirect<UndoCountdownButton, string?>(nameof(SecondsLabel), o => o.SecondsLabel);

    /// <summary>True between <see cref="Start"/> and the end of the window, a press or <see cref="Cancel"/>.</summary>
    public static readonly DirectProperty<UndoCountdownButton, bool> IsRunningProperty =
        AvaloniaProperty.RegisterDirect<UndoCountdownButton, bool>(nameof(IsRunning), o => o.IsRunning);

    /// <summary>Raised once when the window runs out, before the button disables itself.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ElapsedEvent =
        RoutedEvent.Register<UndoCountdownButton, RoutedEventArgs>(nameof(Elapsed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration { get => GetValue(DurationProperty); set => SetValue(DurationProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SecondsTextProperty"/>
    public Func<int, string>? SecondsText { get => GetValue(SecondsTextProperty); set => SetValue(SecondsTextProperty, value); }

    /// <inheritdoc cref="SecondsNameProperty"/>
    public Func<int, string>? SecondsName { get => GetValue(SecondsNameProperty); set => SetValue(SecondsNameProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => _progress; private set => SetAndRaise(ProgressProperty, ref _progress, value); }

    /// <inheritdoc cref="RemainingProperty"/>
    public TimeSpan Remaining { get => _remaining; private set => SetAndRaise(RemainingProperty, ref _remaining, value); }

    /// <inheritdoc cref="SecondsProperty"/>
    public int Seconds { get => _seconds; private set => SetAndRaise(SecondsProperty, ref _seconds, value); }

    /// <inheritdoc cref="SecondsLabelProperty"/>
    public string? SecondsLabel { get => _secondsLabel; private set => SetAndRaise(SecondsLabelProperty, ref _secondsLabel, value); }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => _isRunning; private set => SetAndRaise(IsRunningProperty, ref _isRunning, value); }

    /// <inheritdoc cref="ElapsedEvent"/>
    public event EventHandler<RoutedEventArgs>? Elapsed
    {
        add => AddHandler(ElapsedEvent, value);
        remove => RemoveHandler(ElapsedEvent, value);
    }

    #endregion

    private double _progress = 1;
    private TimeSpan _remaining;
    private int _seconds;
    private string? _secondsLabel;
    private bool _isRunning;
    private bool _expired;
    private DateTimeOffset _deadline;
    private UndoClock _clock = UndoClock.Shared;
    private ArcRing? _ring;

    public UndoCountdownButton()
    {
        // Not started yet: a full window.
        ShowRemaining(Duration);
    }

    protected override Type StyleKeyOverride => typeof(UndoCountdownButton);

    /// <summary>The clock that ticks this countdown (<see cref="UndoClock.Shared"/>); tests give their own.</summary>
    internal UndoClock Clock
    {
        get => _clock;
        set
        {
            if (ReferenceEquals(_clock, value))
            {
                return;
            }
            var registered = _clock.IsRegistered(this);
            _clock.Unregister(this);
            _clock = value;
            if (registered)
            {
                _clock.Register(this);
            }
        }
    }

    /// <summary>The ring part, for tests.</summary>
    internal ArcRing? Ring => _ring;

    /// <summary>The button stays disabled after its window ran out, until <see cref="Start"/> opens a new one.</summary>
    protected override bool IsEnabledCore => base.IsEnabledCore && !_expired;

    #region Countdown

    /// <summary>
    /// Opens a new window of <see cref="Duration"/> from now: the ring is full, the seconds start over, and a button that
    /// ran out is enabled again. Calling it while running restarts the window.
    /// </summary>
    public void Start()
    {
        _deadline = _clock.Now + Duration;
        if (_expired)
        {
            _expired = false;
            UpdateIsEffectivelyEnabled();
        }
        SetRunning(true);
        if (this.IsAttachedToVisualTree())
        {
            _clock.Register(this);
        }
        ShowRemaining(Duration);
    }

    /// <summary>Stops the countdown where it is, without raising <see cref="Elapsed"/>.</summary>
    public void Cancel()
    {
        if (!IsRunning)
        {
            return;
        }
        _clock.Unregister(this);
        SetRunning(false);
    }

    /// <summary>A tick of the shared clock: shows the time left, or ends the window.</summary>
    internal void OnClockTick(DateTimeOffset now)
    {
        if (!IsRunning)
        {
            _clock.Unregister(this);
            return;
        }
        var remaining = _deadline - now;
        if (remaining <= TimeSpan.Zero)
        {
            Expire();
            return;
        }
        ShowRemaining(remaining);
    }

    private void Expire()
    {
        _clock.Unregister(this);
        SetRunning(false);
        ShowRemaining(TimeSpan.Zero);
        _expired = true;
        // The event comes first, while the button can still hold the focus, so its owner can see where the focus is and
        // move it before the button turns disabled. A handler that starts a new window keeps the button enabled.
        RaiseEvent(new RoutedEventArgs(ElapsedEvent, this));
        if (_expired)
        {
            UpdateIsEffectivelyEnabled();
        }
    }

    private void SetRunning(bool running)
    {
        IsRunning = running;
        PseudoClasses.Set(RunningPseudoClass, running);
    }

    /// <summary>Shows <paramref name="remaining"/>: progress at every update, the seconds and the name only when the whole second changes.</summary>
    private void ShowRemaining(TimeSpan remaining)
    {
        var duration = Duration;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }
        else if (duration > TimeSpan.Zero && remaining > duration)
        {
            remaining = duration;
        }
        Remaining = remaining;
        Progress = duration > TimeSpan.Zero ? remaining.TotalMilliseconds / duration.TotalMilliseconds : 0;
        var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds - 1e-9));
        if (seconds != Seconds || SecondsLabel is null)
        {
            Seconds = seconds;
            UpdateTexts();
        }
        UpdateRing();
    }

    private void UpdateTexts()
    {
        var seconds = Seconds;
        SecondsLabel = SecondsText is { } format ? format(seconds) : seconds.ToString(CultureInfo.InvariantCulture);
        var text = Text;
        var spoken = SecondsName?.Invoke(seconds);
        AutomationProperties.SetName(this, string.IsNullOrEmpty(spoken) ? text ?? string.Empty
            : string.IsNullOrEmpty(text) ? spoken : text + ", " + spoken);
    }

    /// <summary>The ring drains with the window at full motion; reduced motion and Off keep it full and still.</summary>
    private void UpdateRing()
    {
        if (_ring is null)
        {
            return;
        }
        _ring.Value = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full ? Progress : 1;
    }

    #endregion

    #region Lifetime and input

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        UpdateRing();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == SecondsTextProperty || change.Property == SecondsNameProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == DurationProperty && !IsRunning && !_expired)
        {
            // Not started (or cancelled): show the full new window. A running window keeps its deadline.
            ShowRemaining(change.GetNewValue<TimeSpan>());
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            UpdateRing();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsRunning)
        {
            // Back in a window: the deadline did not move. Catch up now, but leave the end of a window that ran out while
            // away to the next tick, so Elapsed is never raised in the middle of an attach.
            _clock.Register(this);
            ShowRemaining(_deadline - _clock.Now);
        }
        UpdateRing();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Out of a window nothing ticks; the countdown keeps running against its deadline.
        _clock.Unregister(this);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    /// <summary>A press is "undo": the window closes first, so Elapsed cannot follow it.</summary>
    protected override void OnClick()
    {
        Cancel();
        base.OnClick();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new UndoCountdownAutomationPeer(this);

    /// <summary>A button whose name carries the seconds left ("Undo, 4 seconds left"); the ring and texts inside are not read separately.</summary>
    private sealed class UndoCountdownAutomationPeer(UndoCountdownButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
