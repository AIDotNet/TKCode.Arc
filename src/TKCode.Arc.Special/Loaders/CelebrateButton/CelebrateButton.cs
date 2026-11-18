using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A button for moments worth celebrating (publishing, finishing setup, a first payment). A press runs
/// <see cref="Action"/>: while its task runs a spinner and <see cref="PendingLabel"/> show; when it succeeds confetti bursts
/// from the button's edges, the button kicks, the label morphs into a check with <see cref="SuccessLabel"/> and the width
/// springs to fit; if it throws, <see cref="ErrorLabel"/> shows without a burst. After <see cref="ResetAfter"/> the button
/// returns to rest. <see cref="Status"/> can also be set from outside (a form submit elsewhere): moving it to
/// <see cref="CelebrateButtonStatus.Success"/> plays the burst.
/// </summary>
/// <remarks>
/// The capsule and its press scale are the theme's. The label swap, the width spring, the kick and the confetti run in
/// code (see <c>CelebrateButtonLabels</c> and <c>CelebrateConfetti</c>): they depend on which statuses meet, keep their
/// speed when interrupted, and the confetti is a particle system drawn past the button's edges, none of which a
/// ControlTheme transition can express. Under reduced motion there is no confetti, kick or press scale and the label only
/// crossfades; with motion off everything jumps.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(PopPart, typeof(Panel))]
[TemplatePart(BodyPart, typeof(Border))]
[PseudoClasses(PendingPseudoClass, SuccessPseudoClass, ErrorPseudoClass)]
public class CelebrateButton : AvaloniaButton
{
    internal const string RootPart = "PART_Root";
    internal const string PopPart = "PART_Pop";
    internal const string BodyPart = "PART_Body";
    private const string PendingPseudoClass = ":pending";
    private const string SuccessPseudoClass = ":success";
    private const string ErrorPseudoClass = ":error";

    /// <summary>
    /// The work a press runs. While its task runs the button shows the spinner and <see cref="PendingLabel"/>; when it
    /// completes the confetti bursts and <see cref="SuccessLabel"/> shows; when it throws <see cref="ErrorLabel"/> shows.
    /// Null goes straight to success. <see cref="AvaloniaButton.Command"/> and <see cref="AvaloniaButton.Click"/> still run
    /// first on every accepted press.
    /// </summary>
    public static readonly StyledProperty<Func<Task>?> ActionProperty =
        AvaloniaProperty.Register<CelebrateButton, Func<Task>?>(nameof(Action));

    /// <summary>
    /// Where the button is in its cycle. Set it to drive the button from outside (moving to
    /// <see cref="CelebrateButtonStatus.Success"/> plays the burst); the button updates it as its own action runs and when it
    /// resets.
    /// </summary>
    public static readonly StyledProperty<CelebrateButtonStatus> StatusProperty =
        AvaloniaProperty.Register<CelebrateButton, CelebrateButtonStatus>(nameof(Status), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shown beside the spinner while the action runs. Null (the default) repeats the button's text content.</summary>
    public static readonly StyledProperty<string?> PendingLabelProperty =
        AvaloniaProperty.Register<CelebrateButton, string?>(nameof(PendingLabel));

    /// <summary>Shown beside the check after a success, and announced.</summary>
    public static readonly StyledProperty<string> SuccessLabelProperty =
        AvaloniaProperty.Register<CelebrateButton, string>(nameof(SuccessLabel), "Done");

    /// <summary>Shown after a failure, and announced. A press then runs the action again.</summary>
    public static readonly StyledProperty<string> ErrorLabelProperty =
        AvaloniaProperty.Register<CelebrateButton, string>(nameof(ErrorLabel), "Try again");

    /// <summary>How long the success or error state stays before the button returns to rest (2.4 s). Zero or less stays.</summary>
    public static readonly StyledProperty<TimeSpan> ResetAfterProperty =
        AvaloniaProperty.Register<CelebrateButton, TimeSpan>(nameof(ResetAfter), TimeSpan.FromMilliseconds(2400));

    /// <summary>The shortest time between two bursts (1.2 s); a success sooner after the last burst shows no confetti.</summary>
    public static readonly StyledProperty<TimeSpan> CooldownProperty =
        AvaloniaProperty.Register<CelebrateButton, TimeSpan>(nameof(Cooldown), TimeSpan.FromMilliseconds(1200));

    /// <summary>Pieces per burst (44). At most 180 pieces fly at once; a burst over that cap launches only what fits.</summary>
    public static readonly StyledProperty<int> ParticleCountProperty =
        AvaloniaProperty.Register<CelebrateButton, int>(nameof(ParticleCount), CelebrateConfettiMath.DefaultCount,
            coerce: static (_, v) => Math.Clamp(v, 0, CelebrateConfettiMath.MaxLive));

    /// <summary>
    /// The confetti colors, picked evenly. Null (the default) uses the accent in three tones, a sunny yellow and a little of
    /// the foreground ink, read from the theme at the moment of the burst, so the confetti follows the accent and appearance.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<Color>?> ConfettiColorsProperty =
        AvaloniaProperty.Register<CelebrateButton, IReadOnlyList<Color>?>(nameof(ConfettiColors));

    /// <summary>Raised on every change of <see cref="Status"/>, including the automatic return to rest.</summary>
    public static readonly RoutedEvent<CelebrateButtonStatusChangedEventArgs> StatusChangedEvent =
        RoutedEvent.Register<CelebrateButton, CelebrateButtonStatusChangedEventArgs>(nameof(StatusChanged), RoutingStrategies.Bubble);

    private readonly CelebrateButtonLabels _labels;
    private readonly CelebrateConfetti _confetti = new();
    private readonly ExplainerAnnouncer _announcer = new();
    private readonly ScaleTransform _kick = new(1, 1);
    private readonly Color[] _palette = new Color[5];
    private LazyFocusRing? _focusRing;
    private Panel? _root;
    private Panel? _pop;
    private Border? _body;
    private IDisposable? _resetTimer;
    private int _version;
    private Exception? _lastError;
    private long _lastBurst;
    private bool _loaded;
    private bool _finishing;

    static CelebrateButton()
    {
        ClipToBoundsProperty.OverrideDefaultValue<CelebrateButton>(false);
        FocusableProperty.OverrideDefaultValue<CelebrateButton>(true);
    }

    public CelebrateButton()
    {
        _labels = new CelebrateButtonLabels(this);
        _confetti.KickChanged = scale =>
        {
            _kick.ScaleX = scale;
            _kick.ScaleY = scale;
        };
        UpdateTexts();
    }

    /// <inheritdoc cref="ActionProperty"/>
    public Func<Task>? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public CelebrateButtonStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <inheritdoc cref="PendingLabelProperty"/>
    public string? PendingLabel
    {
        get => GetValue(PendingLabelProperty);
        set => SetValue(PendingLabelProperty, value);
    }

    /// <inheritdoc cref="SuccessLabelProperty"/>
    public string SuccessLabel
    {
        get => GetValue(SuccessLabelProperty);
        set => SetValue(SuccessLabelProperty, value);
    }

    /// <inheritdoc cref="ErrorLabelProperty"/>
    public string ErrorLabel
    {
        get => GetValue(ErrorLabelProperty);
        set => SetValue(ErrorLabelProperty, value);
    }

    /// <inheritdoc cref="ResetAfterProperty"/>
    public TimeSpan ResetAfter
    {
        get => GetValue(ResetAfterProperty);
        set => SetValue(ResetAfterProperty, value);
    }

    /// <inheritdoc cref="CooldownProperty"/>
    public TimeSpan Cooldown
    {
        get => GetValue(CooldownProperty);
        set => SetValue(CooldownProperty, value);
    }

    /// <inheritdoc cref="ParticleCountProperty"/>
    public int ParticleCount
    {
        get => GetValue(ParticleCountProperty);
        set => SetValue(ParticleCountProperty, value);
    }

    /// <inheritdoc cref="ConfettiColorsProperty"/>
    public IReadOnlyList<Color>? ConfettiColors
    {
        get => GetValue(ConfettiColorsProperty);
        set => SetValue(ConfettiColorsProperty, value);
    }

    /// <inheritdoc cref="StatusChangedEvent"/>
    public event EventHandler<CelebrateButtonStatusChangedEventArgs>? StatusChanged
    {
        add => AddHandler(StatusChangedEvent, value);
        remove => RemoveHandler(StatusChangedEvent, value);
    }

    internal CelebrateButtonLabels Labels => _labels;

    internal CelebrateConfetti Confetti => _confetti;

    internal ExplainerAnnouncer Announcer => _announcer;

    /// <summary>The kick scale now (1 at rest).</summary>
    internal double KickScale => _kick.ScaleX;

    /// <summary>True while a press would be ignored (the action runs or the success shows).</summary>
    private bool IsBusy => Status is CelebrateButtonStatus.Pending or CelebrateButtonStatus.Success;

    /// <summary>The text the label shows now (the accessible name unless one is set).</summary>
    internal string? CurrentLabelText => Status switch
    {
        CelebrateButtonStatus.Pending => _labels.PendingText,
        CelebrateButtonStatus.Success => SuccessLabel,
        CelebrateButtonStatus.Error => ErrorLabel,
        _ => Content as string
    };

    /// <summary>
    /// Plays a confetti burst and the kick now, without changing <see cref="Status"/>. Does nothing within
    /// <see cref="Cooldown"/> of the last burst, under reduced motion or with motion off, or before the button is shown.
    /// </summary>
    public void Celebrate()
    {
        if (!_loaded || _body is null || _root is null || ParticleCount <= 0 || !ArcMotion.IsFull(this))
        {
            return;
        }
        var now = Stopwatch.GetTimestamp();
        if (_lastBurst != 0 && Stopwatch.GetElapsedTime(_lastBurst, now) < Cooldown)
        {
            return;
        }
        _lastBurst = now;
        var origin = _body.TranslatePoint(default, _confetti) ?? default;
        var button = new Rect(origin, _body.Bounds.Size);
        _confetti.ReachSide = Token(CelebrateButtonTokenKind.ConfettiReachSide, 150);
        _confetti.ReachTop = Token(CelebrateButtonTokenKind.ConfettiReachTop, 190);
        _confetti.ReachBottom = Token(CelebrateButtonTokenKind.ConfettiReachBottom, 110);
        if (ConfettiColors is { Count: > 0 } custom)
        {
            _confetti.Burst(button, custom, false, ParticleCount);
        }
        else
        {
            _confetti.Burst(button, ResolvePalette(), true, ParticleCount);
        }
    }

    /// <summary>Returns a succeeded or failed button to rest now (as <see cref="ResetAfter"/> does). Ignored while pending.</summary>
    public void Reset()
    {
        if (Status is CelebrateButtonStatus.Success or CelebrateButtonStatus.Error)
        {
            SetCurrentValue(StatusProperty, CelebrateButtonStatus.Idle);
        }
    }

    /// <summary>Runs the reset timer's work now (tests: the headless dispatcher does not fire timers).</summary>
    internal void FireResetTimer()
    {
        if (_resetTimer is not null)
        {
            _resetTimer.Dispose();
            _resetTimer = null;
            Reset();
        }
    }

    internal bool IsResetPending => _resetTimer is not null;

    protected override void OnClick()
    {
        if (IsBusy)
        {
            return;
        }
        base.OnClick();
        Run();
    }

    private void Run()
    {
        var version = ++_version;
        _lastError = null;
        var action = Action;
        if (action is null)
        {
            SetCurrentValue(StatusProperty, CelebrateButtonStatus.Success);
            return;
        }
        SetCurrentValue(StatusProperty, CelebrateButtonStatus.Pending);
        Task task;
        try
        {
            task = action() ?? Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Finish(version, ex);
            return;
        }
        Await(task, version);
    }

    private async void Await(Task task, int version)
    {
        Exception? failure = null;
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        Finish(version, failure);
    }

    private void Finish(int version, Exception? failure)
    {
        if (version != _version || Status != CelebrateButtonStatus.Pending)
        {
            return;
        }
        _lastError = failure;
        _finishing = true;
        try
        {
            SetCurrentValue(StatusProperty, failure is null ? CelebrateButtonStatus.Success : CelebrateButtonStatus.Error);
        }
        finally
        {
            _finishing = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            OnStatusChanged(change.GetOldValue<CelebrateButtonStatus>(), change.GetNewValue<CelebrateButtonStatus>());
        }
        else if (change.Property == ContentProperty || change.Property == PendingLabelProperty
                 || change.Property == SuccessLabelProperty || change.Property == ErrorLabelProperty)
        {
            UpdateTexts();
        }
    }

    private void OnStatusChanged(CelebrateButtonStatus old, CelebrateButtonStatus status)
    {
        if (old == CelebrateButtonStatus.Pending && !_finishing)
        {
            // Status moved on from outside: the running action no longer decides how this cycle ends.
            _version++;
        }
        var error = status == CelebrateButtonStatus.Error ? _lastError : null;
        if (status != CelebrateButtonStatus.Error)
        {
            _lastError = null;
        }
        PseudoClasses.Set(PendingPseudoClass, status == CelebrateButtonStatus.Pending);
        PseudoClasses.Set(SuccessPseudoClass, status == CelebrateButtonStatus.Success);
        PseudoClasses.Set(ErrorPseudoClass, status == CelebrateButtonStatus.Error);
        _labels.Show(status, _loaded);

        _resetTimer?.Dispose();
        _resetTimer = null;
        switch (status)
        {
            case CelebrateButtonStatus.Success:
                _announcer.Announce(SuccessLabel);
                Celebrate();
                StartResetTimer();
                break;
            case CelebrateButtonStatus.Error:
                _announcer.Announce(ErrorLabel);
                StartResetTimer();
                break;
            case CelebrateButtonStatus.Idle:
                _announcer.Clear();
                break;
        }
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, peer.GetName());
        }
        RaiseEvent(new CelebrateButtonStatusChangedEventArgs(StatusChangedEvent, old, status, error));
    }

    private void StartResetTimer()
    {
        var after = ResetAfter;
        if (after <= TimeSpan.Zero)
        {
            return;
        }
        var status = Status;
        _resetTimer = DispatcherTimer.RunOnce(() =>
        {
            _resetTimer = null;
            if (Status == status)
            {
                SetCurrentValue(StatusProperty, CelebrateButtonStatus.Idle);
            }
        }, after);
    }

    private void UpdateTexts()
    {
        var pending = PendingLabel ?? Content as string ?? string.Empty;
        _labels.SetTexts(pending, SuccessLabel, ErrorLabel);
    }

    private IReadOnlyList<Color> ResolvePalette()
    {
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        _palette[0] = accent;
        _palette[1] = ArcColor.LerpOklab(accent, Colors.White, 0.55);
        _palette[2] = ArcTokens.GetColor(this, ArcDesignTokenKind.AccentStrong);
        _palette[3] = this.TryFindResource(CelebrateButtonTokens.Key(CelebrateButtonTokenKind.ConfettiYellow), out var y) && y is Color yellow
            ? yellow
            : Color.FromRgb(0xF8, 0xCC, 0x38);
        _palette[4] = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        return _palette;
    }

    private double Token(CelebrateButtonTokenKind kind, double fallback)
        => this.TryFindResource(CelebrateButtonTokens.Key(kind), out var value) && value is double d ? d : fallback;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _root = e.NameScope.Find<Panel>(RootPart);
        _pop = e.NameScope.Find<Panel>(PopPart);
        _body = e.NameScope.Find<Border>(BodyPart);
        if (_body is not null)
        {
            _body.Child = _labels;
        }
        if (_pop is not null)
        {
            _pop.RenderTransform = _kick;
        }
        if (_root is not null)
        {
            _root.Children.Add(_confetti);
            _root.Children.Add(_announcer);
        }
        _focusRing ??= new LazyFocusRing(this, CelebrateButtonTokens.Key(CelebrateButtonTokenKind.FocusRingMargin),
            CelebrateButtonTokens.Key(CelebrateButtonTokenKind.FocusRingRadius));
        _focusRing.Attach(_pop);
    }

    private void DetachParts()
    {
        if (_body is not null && ReferenceEquals(_body.Child, _labels))
        {
            _body.Child = null;
        }
        if (_pop is not null && ReferenceEquals(_pop.RenderTransform, _kick))
        {
            _pop.RenderTransform = null;
        }
        _root?.Children.Remove(_confetti);
        _root?.Children.Remove(_announcer);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _loaded = true;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _loaded = false;
        _confetti.Clear();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CelebrateButtonAutomationPeer(this);

    /// <summary>A button peer named by the label it shows now and disabled while the action runs.</summary>
    private sealed class CelebrateButtonAutomationPeer : ButtonAutomationPeer
    {
        public CelebrateButtonAutomationPeer(CelebrateButton owner)
            : base(owner)
        {
        }

        private CelebrateButton Button => (CelebrateButton)Owner;

        protected override string? GetNameCore()
        {
            var name = AutomationProperties.GetName(Owner);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return Button.CurrentLabelText is { Length: > 0 } text ? text : base.GetNameCore();
        }

        protected override bool IsEnabledCore() => base.IsEnabledCore() && Button.Status != CelebrateButtonStatus.Pending;
    }
}
