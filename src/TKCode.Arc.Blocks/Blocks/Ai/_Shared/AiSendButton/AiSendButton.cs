using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How <see cref="AiSendButton"/> looks while a reply is generated.</summary>
internal enum AiSendButtonAppearance
{
    /// <summary>The AI composer's: a muted stop square inside a progress ring, with tooltips.</summary>
    Ring,

    /// <summary>
    /// The AI chat's (and the other single-row inputs'): the stop square on the accent, no ring (<see cref="AiSendButton.Progress"/>
    /// is ignored), an 18 px arrow that crossfades with the square, and no tooltip (the accessible name stays).
    /// </summary>
    Solid
}

/// <summary>
/// The round send button of an AI composer (36 px). <see cref="State"/> picks one of four looks: <c>Empty</c> and
/// <c>Blocked</c> look disabled (SurfaceMuted with a muted arrow), <c>Ready</c> is the accent with an up arrow, and
/// <c>Generating</c> shows a solid stop square inside a thin progress ring that sits 1.75 px outside the button.
/// <code>
/// &lt;arc:AiSendButton State="{Binding SendState}" Progress="{Binding ReplyProgress}"
///                   SendName="Send message" StopName="Stop generating" EmptyName="Type a message to send" /&gt;
/// </code>
/// The button stays enabled and focusable in every state: in <c>Empty</c> and <c>Blocked</c> a press raises no
/// <c>Click</c> and runs no <c>Command</c>, the accessible name and the tooltip say why (<see cref="EmptyName"/>,
/// <see cref="BlockedName"/>), and the automation peer reports it as not enabled (ARIA's <c>aria-disabled</c>).
/// <para>
/// The ring is decoration of status: while generating, the arc chases <see cref="Progress"/> (15% of the gap every
/// 1/60 s, Arc's measured rate), never moves back and never passes the target; it starts at the progress reported when
/// generating begins (Arc shows 10% at once). Leaving <c>Generating</c> fades the ring out (track 240 ms, arc 240 ms after
/// 120 ms); a reply that finished (<see cref="Progress"/> reached 1, before or during that fade) first fills the arc in
/// about 130 ms, a stopped one fades where it is. When the fade ends the ring resets to 0. Reduced motion shows the
/// progress at once and fades in 120 ms; Off shows and hides it at once.
/// </para>
/// <para>
/// <see cref="Appearance"/> <see cref="AiSendButtonAppearance.Solid"/> is the AI chat's button: generating shows an
/// AccentForeground square on the accent, there is no ring at all, and the arrow and the square crossfade.
/// </para>
/// </summary>
[TemplatePart(GlyphPart, typeof(AiSendGlyph))]
[TemplatePart(TrackPart, typeof(ArcRing))]
[TemplatePart(RingPart, typeof(ArcRing))]
[PseudoClasses(EmptyPseudoClass, ReadyPseudoClass, BlockedPseudoClass, GeneratingPseudoClass, HoverPseudoClass, SolidPseudoClass)]
internal class AiSendButton : AvaloniaButton
{
    public const string GlyphPart = "PART_Glyph";
    public const string TrackPart = "PART_Track";
    public const string RingPart = "PART_Ring";

    /// <summary>Nothing to send.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Something to send.</summary>
    public const string ReadyPseudoClass = ":ready";

    /// <summary>Something to send, but waiting for uploads or over the limit.</summary>
    public const string BlockedPseudoClass = ":blocked";

    /// <summary>A reply is generated; pressing stops it.</summary>
    public const string GeneratingPseudoClass = ":generating";

    /// <summary>Mouse or pen over the button (touch leaves no hover look behind).</summary>
    public const string HoverPseudoClass = ":hover";

    /// <summary>Set while <see cref="Appearance"/> is <see cref="AiSendButtonAppearance.Solid"/>.</summary>
    public const string SolidPseudoClass = ":solid";

    /// <summary>Arc's reply ring closes 15% of the gap to its target every 1/60 s.</summary>
    public const double ChaseRate = 0.15;

    /// <summary>The rate that fills a finished reply's ring in about 130 ms (99% of the gap in 8 frames).</summary>
    internal const double FinishChaseRate = 0.45;

    /// <summary>CSS <c>ease-out</c>, Arc's curve for the ring's fades.</summary>
    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    private static readonly TimeSpan s_ringFade = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan s_arcFadeDelay = TimeSpan.FromMilliseconds(120);

    #region Properties

    /// <summary>Which of the four states the button shows (default <see cref="AiSendState.Empty"/>).</summary>
    public static readonly StyledProperty<AiSendState> StateProperty =
        AvaloniaProperty.Register<AiSendButton, AiSendState>(nameof(State), AiSendState.Empty);

    /// <summary>The reply's progress from 0 to 1, the target of the ring while <see cref="AiSendState.Generating"/>.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AiSendButton, double>(nameof(Progress),
            coerce: (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <summary>Accessible name and tooltip when <see cref="AiSendState.Ready"/> ("Send message"); also the fallback of the other send states.</summary>
    public static readonly StyledProperty<string?> SendNameProperty =
        AvaloniaProperty.Register<AiSendButton, string?>(nameof(SendName));

    /// <summary>Accessible name and tooltip while <see cref="AiSendState.Generating"/> ("Stop generating").</summary>
    public static readonly StyledProperty<string?> StopNameProperty =
        AvaloniaProperty.Register<AiSendButton, string?>(nameof(StopName));

    /// <summary>Accessible name and tooltip when <see cref="AiSendState.Empty"/> ("Type a message to send"); null uses <see cref="SendName"/>.</summary>
    public static readonly StyledProperty<string?> EmptyNameProperty =
        AvaloniaProperty.Register<AiSendButton, string?>(nameof(EmptyName));

    /// <summary>Accessible name and tooltip when <see cref="AiSendState.Blocked"/>: the reason ("Send after the uploads finish"); null uses <see cref="SendName"/>.</summary>
    public static readonly StyledProperty<string?> BlockedNameProperty =
        AvaloniaProperty.Register<AiSendButton, string?>(nameof(BlockedName));

    /// <summary>The composer's look with a progress ring (default) or the chat's solid stop button without one.</summary>
    public static readonly StyledProperty<AiSendButtonAppearance> AppearanceProperty =
        AvaloniaProperty.Register<AiSendButton, AiSendButtonAppearance>(nameof(Appearance));

    /// <inheritdoc cref="AppearanceProperty"/>
    public AiSendButtonAppearance Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public AiSendState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc cref="SendNameProperty"/>
    public string? SendName { get => GetValue(SendNameProperty); set => SetValue(SendNameProperty, value); }

    /// <inheritdoc cref="StopNameProperty"/>
    public string? StopName { get => GetValue(StopNameProperty); set => SetValue(StopNameProperty, value); }

    /// <inheritdoc cref="EmptyNameProperty"/>
    public string? EmptyName { get => GetValue(EmptyNameProperty); set => SetValue(EmptyNameProperty, value); }

    /// <inheritdoc cref="BlockedNameProperty"/>
    public string? BlockedName { get => GetValue(BlockedNameProperty); set => SetValue(BlockedNameProperty, value); }

    /// <summary>
    /// The progress the ring shows right now: it trails <see cref="Progress"/> while generating, holds (or fills to 1)
    /// while the ring fades out, and is 0 once the ring is hidden.
    /// </summary>
    public double DisplayedProgress => _phase == RingPhase.Hidden ? 0 : _ring?.DisplayedValue ?? _ringTarget;

    /// <summary>The accessible name and tooltip of the current state.</summary>
    public string? CurrentName => State switch
    {
        AiSendState.Empty => EmptyName ?? SendName,
        AiSendState.Blocked => BlockedName ?? SendName,
        AiSendState.Generating => StopName,
        _ => SendName
    };

    /// <summary>True when a press acts: sends when <see cref="AiSendState.Ready"/>, stops when <see cref="AiSendState.Generating"/>.</summary>
    public bool CanPress => State is AiSendState.Ready or AiSendState.Generating;

    #endregion

    private enum RingPhase
    {
        Hidden,
        Shown,
        Leaving
    }

    private AiSendGlyph? _glyph;
    private ArcRing? _track;
    private ArcRing? _ring;
    private MotionValue<double>? _trackOpacity;
    private MotionValue<double>? _arcOpacity;
    private RingPhase _phase;
    private double _ringTarget;
    private int _leaveVersion;

    static AiSendButton()
    {
        FocusableProperty.OverrideDefaultValue<AiSendButton>(true);
    }

    public AiSendButton()
    {
        UpdateStateClasses();
        UpdateName();
    }

    protected override Type StyleKeyOverride => typeof(AiSendButton);

    /// <summary>The morphing arrow / stop mark, for tests.</summary>
    internal AiSendGlyph? Glyph => _glyph;

    /// <summary>The ring's track (full circle), for tests.</summary>
    internal ArcRing? Track => _track;

    /// <summary>The ring's progress arc, for tests.</summary>
    internal ArcRing? Ring => _ring;

    #region State

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            var (old, now) = change.GetOldAndNewValue<AiSendState>();
            UpdateStateClasses();
            UpdateName();
            if (_glyph is not null)
            {
                _glyph.IsStop = now == AiSendState.Generating;
            }
            if (now == AiSendState.Generating)
            {
                ShowRing();
            }
            else if (old == AiSendState.Generating)
            {
                HideRing();
            }
        }
        else if (change.Property == ProgressProperty)
        {
            OnProgress(change.GetNewValue<double>());
        }
        else if (change.Property == SendNameProperty || change.Property == StopNameProperty
                 || change.Property == EmptyNameProperty || change.Property == BlockedNameProperty)
        {
            UpdateName();
        }
        else if (change.Property == AppearanceProperty)
        {
            var solid = change.GetNewValue<AiSendButtonAppearance>() == AiSendButtonAppearance.Solid;
            PseudoClasses.Set(SolidPseudoClass, solid);
            UpdateName();
            if (solid)
            {
                // No ring in this look: whatever showed goes at once.
                _phase = RingPhase.Hidden;
                _leaveVersion++;
                _ringTarget = 0;
                TrackOpacity.Jump(0);
                ArcOpacity.Jump(0);
            }
            else if (State == AiSendState.Generating)
            {
                ShowRing();
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A button disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    private void UpdateStateClasses()
    {
        var state = State;
        PseudoClasses.Set(EmptyPseudoClass, state == AiSendState.Empty);
        PseudoClasses.Set(ReadyPseudoClass, state == AiSendState.Ready);
        PseudoClasses.Set(BlockedPseudoClass, state == AiSendState.Blocked);
        PseudoClasses.Set(GeneratingPseudoClass, state == AiSendState.Generating);
    }

    /// <summary>One text names the button and fills its tooltip; empty text leaves the button unnamed and without a tooltip.</summary>
    private void UpdateName()
    {
        var name = CurrentName;
        if (string.IsNullOrEmpty(name))
        {
            ClearValue(AutomationProperties.NameProperty);
            Tooltip.SetTip(this, null);
            return;
        }
        AutomationProperties.SetName(this, name);
        // The chat's send button has no tooltip (its name stays).
        Tooltip.SetTip(this, Appearance == AiSendButtonAppearance.Solid ? null : name);
    }

    #endregion

    #region Ring

    /// <summary>Generating begins: the arc shows the progress so far at once, the track fades in.</summary>
    private void ShowRing()
    {
        if (Appearance == AiSendButtonAppearance.Solid)
        {
            return;
        }
        _phase = RingPhase.Shown;
        _leaveVersion++;
        _ringTarget = Progress;
        if (_ring is not null)
        {
            // Jump (no chase from the last reply's value), then chase from here on.
            _ring.ChaseRate = 0;
            _ring.Value = _ringTarget;
            _ring.ChaseRate = ChaseRate;
        }
        var level = Level();
        Fade(TrackOpacity, 1, level == ArcMotionLevel.Full ? Tween(s_ringFade, s_easeOut) : FadeSpec(level));
        // The arc is there from the first frame (Arc: opacity 1 at once), so the start is visible right away.
        Fade(ArcOpacity, 1, level == ArcMotionLevel.Reduced ? FadeSpec(level) : MotionSpec.Instant);
    }

    /// <summary>Generating ends: fill a finished reply's arc, fade the ring out, then reset it.</summary>
    private void HideRing()
    {
        if (_phase == RingPhase.Hidden)
        {
            return;
        }
        _phase = RingPhase.Leaving;
        var version = ++_leaveVersion;
        if (_ringTarget >= 1 || Progress >= 1)
        {
            Finish();
        }
        else if (_ring is not null)
        {
            // Stopped: the arc stays where it is drawn.
            _ring.Value = _ring.DisplayedValue;
            _ringTarget = _ring.Value;
        }
        var level = Level();
        Fade(TrackOpacity, 0, level == ArcMotionLevel.Full ? Tween(s_ringFade, s_easeOut) : FadeSpec(level));
        var arc = level == ArcMotionLevel.Full ? Tween(s_ringFade, s_easeOut, s_arcFadeDelay) : FadeSpec(level);
        var faded = ArcOpacity.AnimateTo(0, arc);
        if (faded.IsCompleted)
        {
            ResetRing(version);
        }
        else
        {
            _ = ResetWhenFaded(faded, version);
        }
    }

    private async Task ResetWhenFaded(Task<bool> faded, int version)
    {
        await faded;
        ResetRing(version);
    }

    /// <summary>The fade finished (and was not taken over by a new reply): back to an empty, hidden ring.</summary>
    private void ResetRing(int version)
    {
        if (version != _leaveVersion || _phase != RingPhase.Leaving)
        {
            return;
        }
        _phase = RingPhase.Hidden;
        _ringTarget = 0;
        if (_ring is not null)
        {
            _ring.ChaseRate = 0;
            _ring.Value = 0;
        }
    }

    private void OnProgress(double progress)
    {
        switch (_phase)
        {
            case RingPhase.Shown:
                // Monotonic: a lower estimate never pulls the arc back.
                if (progress > _ringTarget)
                {
                    _ringTarget = progress;
                    if (_ring is not null)
                    {
                        _ring.Value = progress;
                    }
                }
                break;
            case RingPhase.Leaving:
                // The finish can arrive just after the state left Generating; it still fills the arc.
                if (progress >= 1 && _ringTarget < 1)
                {
                    Finish();
                }
                break;
        }
    }

    private void Finish()
    {
        _ringTarget = 1;
        if (_ring is not null)
        {
            _ring.ChaseRate = FinishChaseRate;
            _ring.Value = 1;
        }
    }

    // Code-driven: the ring's two layers fade asymmetrically (the arc appears at once but leaves late, after the fill),
    // and the end of the arc's fade resets the progress, which a ControlTheme transition cannot report.
    private MotionValue<double> TrackOpacity => _trackOpacity ??= CreateOpacity(v =>
    {
        if (_track is not null)
        {
            _track.Opacity = v;
        }
    });

    private MotionValue<double> ArcOpacity => _arcOpacity ??= CreateOpacity(v =>
    {
        if (_ring is not null)
        {
            _ring.Opacity = v;
        }
    });

    private MotionValue<double> CreateOpacity(Action<double> apply)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }

    private void Fade(MotionValue<double> value, double to, MotionSpec spec) => _ = value.AnimateTo(to, spec);

    /// <summary>Reduced motion: a 120 ms linear fade; Off (or before the button is shown): at once.</summary>
    private MotionSpec FadeSpec(ArcMotionLevel level)
        => level == ArcMotionLevel.Reduced ? MotionSpec.Tween(ArcMotionPresets.Instant, new LinearEasing()) : MotionSpec.Instant;

    private static MotionSpec Tween(TimeSpan duration, Easing easing, TimeSpan delay = default) => MotionSpec.Tween(duration, easing, delay);

    /// <summary>The effective motion level; a button not yet loaded shows its first state without motion.</summary>
    private ArcMotionLevel Level() => IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    #endregion

    #region Template and input

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<AiSendGlyph>(GlyphPart);
        _track = e.NameScope.Find<ArcRing>(TrackPart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        if (_glyph is not null)
        {
            _glyph.IsStop = State == AiSendState.Generating;
        }
        // A new template shows the current state at once.
        var shown = _phase != RingPhase.Hidden;
        if (_ring is not null)
        {
            _ring.ChaseRate = 0;
            _ring.Value = shown ? _ringTarget : 0;
            _ring.ChaseRate = _phase == RingPhase.Shown ? ChaseRate : 0;
        }
        TrackOpacity.Jump(_phase == RingPhase.Shown ? 1 : 0);
        ArcOpacity.Jump(_phase == RingPhase.Shown ? 1 : 0);
        if (_phase == RingPhase.Leaving)
        {
            ResetRing(_leaveVersion);
        }
    }

    /// <summary>Empty and Blocked look disabled: a press (pointer, Enter, Space or automation) does nothing there.</summary>
    protected override void OnClick()
    {
        if (!CanPress)
        {
            return;
        }
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new AiSendButtonAutomationPeer(this);

    /// <summary>
    /// A button that reports itself not enabled in Empty and Blocked while it stays focusable (ARIA's
    /// <c>aria-disabled</c>); its name is the state's text, and the ring and mark inside are not read. Avalonia has no
    /// automation event for a change of the enabled state; the name changes with every state, and that change is raised.
    /// </summary>
    private sealed class AiSendButtonAutomationPeer(AiSendButton owner) : ButtonAutomationPeer(owner)
    {
        protected override bool IsEnabledCore() => base.IsEnabledCore() && owner.CanPress;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
