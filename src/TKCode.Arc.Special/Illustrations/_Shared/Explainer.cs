using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The names the playback buttons of an <see cref="Explainer"/> carry (and their tooltips).</summary>
public sealed class ExplainerPlaybackLabels
{
    /// <summary>The play button while the story is paused or ready.</summary>
    public string Play { get; set; } = "Play";

    /// <summary>The play button while the story plays.</summary>
    public string Pause { get; set; } = "Pause";

    /// <summary>The play button once the story has finished.</summary>
    public string Replay { get; set; } = "Play again";

    /// <summary>The step button: plays to the next beat and stops there.</summary>
    public string Step { get; set; } = "Next step";

    /// <summary>The restart button.</summary>
    public string Restart { get; set; } = "Restart";
}

/// <summary>The model an explainer names in its header: a name and an optional mark.</summary>
public sealed class ExplainerModel
{
    /// <summary>The model's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>A 24×24 stroked glyph drawn before the name; null draws the default mark.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color; null uses the accent.</summary>
    public IBrush? MarkBrush { get; set; }
}

/// <summary>
/// The base of the animated AI explainers (<c>AgentLoop</c>, <c>PromptCache</c>, <c>ReasoningTree</c>,
/// <c>SpeculativeDecoding</c>, <c>MixtureOfExperts</c>): a scripted story that runs on its own clock. The clock runs
/// only while the story plays (or steps) and the card is on screen, multiplied by <see cref="Speed"/>; pausing freezes
/// it. With <see cref="AutoPlay"/> the story starts the first time the card is mostly in view; under reduced motion it
/// opens on its finished state instead, and playing or stepping then applies each change in place. A polite live region
/// speaks only when the reader drives the story (step, pause, restart, a control change), never while it plays on its own.
/// </summary>
[PseudoClasses(PlayingPseudoClass, SteppingPseudoClass, FinishedPseudoClass, ReadyPseudoClass)]
public abstract class Explainer : TemplatedControl
{
    /// <summary>The play / pause button part (optional).</summary>
    public const string PlayButtonPart = "PART_PlayButton";

    /// <summary>The next-step button part (optional).</summary>
    public const string StepButtonPart = "PART_StepButton";

    /// <summary>The restart button part (optional).</summary>
    public const string RestartButtonPart = "PART_RestartButton";

    /// <summary>The live region part (optional; without it nothing is announced).</summary>
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the story plays on its own.</summary>
    public const string PlayingPseudoClass = ":playing";

    /// <summary>Set while a step plays to the next beat.</summary>
    public const string SteppingPseudoClass = ":stepping";

    /// <summary>Set once the story has reached its end.</summary>
    public const string FinishedPseudoClass = ":finished";

    /// <summary>Set before the story has started (time 0, nothing has happened).</summary>
    public const string ReadyPseudoClass = ":ready";

    /// <summary>Defines the <see cref="IsPlaying"/> property.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<Explainer, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="AutoPlay"/> property.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<Explainer, bool>(nameof(AutoPlay), true);

    /// <summary>Defines the <see cref="IsLooping"/> property.</summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<Explainer, bool>(nameof(IsLooping));

    /// <summary>Defines the <see cref="Speed"/> property.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<Explainer, double>(nameof(Speed), 1.0, coerce: CoerceSpeed, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="AreControlsVisible"/> property.</summary>
    public static readonly StyledProperty<bool> AreControlsVisibleProperty =
        AvaloniaProperty.Register<Explainer, bool>(nameof(AreControlsVisible), true);

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Explainer, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Model"/> property.</summary>
    public static readonly StyledProperty<ExplainerModel?> ModelProperty =
        AvaloniaProperty.Register<Explainer, ExplainerModel?>(nameof(Model));

    /// <summary>Defines the <see cref="PlaybackLabels"/> property.</summary>
    public static readonly StyledProperty<ExplainerPlaybackLabels> PlaybackLabelsProperty =
        AvaloniaProperty.Register<Explainer, ExplainerPlaybackLabels>(nameof(PlaybackLabels), new ExplainerPlaybackLabels());

    /// <summary>Defines the <see cref="IsFinished"/> property.</summary>
    public static readonly DirectProperty<Explainer, bool> IsFinishedProperty =
        AvaloniaProperty.RegisterDirect<Explainer, bool>(nameof(IsFinished), o => o.IsFinished);

    /// <summary>Defines the <see cref="IsStepping"/> property.</summary>
    public static readonly DirectProperty<Explainer, bool> IsSteppingProperty =
        AvaloniaProperty.RegisterDirect<Explainer, bool>(nameof(IsStepping), o => o.IsStepping);

    /// <summary>Defines the <see cref="CanStep"/> property.</summary>
    public static readonly DirectProperty<Explainer, bool> CanStepProperty =
        AvaloniaProperty.RegisterDirect<Explainer, bool>(nameof(CanStep), o => o.CanStep);

    /// <summary>Defines the <see cref="Finished"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FinishedEvent =
        RoutedEvent.Register<Explainer, RoutedEventArgs>(nameof(Finished), RoutingStrategies.Bubble);

    private readonly LiveLoop _loop;
    private readonly ExplainerScript _script = new();
    private ViewportVisibility? _visibility;
    private ExplainerAnnouncer? _announcer;
    private AvaloniaButton? _playButton;
    private AvaloniaButton? _stepButton;
    private AvaloniaButton? _restartButton;
    private ArcContentSwap? _playGlyph;
    private string? _playGlyphKey;
    private double _now;
    private double _wall;
    private double _framesUntil;
    private double? _stepTarget;
    private double _loopWait = -1;
    private bool _built;
    private bool _started;
    private bool _userDriven;
    private bool _isFinished;
    private bool _isStepping;
    private bool _canStep = true;
    private bool _syncingPlaying;

    static Explainer()
    {
        FocusableProperty.OverrideDefaultValue<Explainer>(false);
    }

    /// <summary>Creates the story clock.</summary>
    protected Explainer()
    {
        // The story is function, not decoration: its clock runs at every motion level (reduced motion only drops travel).
        _loop = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Off };
        UpdatePseudoClasses();
    }

    /// <summary>
    /// True while the story plays on its own. Set it (or call <see cref="Play"/> / <see cref="Pause"/>) to drive it;
    /// it turns false when the story ends without <see cref="IsLooping"/>.
    /// </summary>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <summary>Starts the story the first time the card is mostly in view (default true).</summary>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <summary>Plays the story again a few seconds after it ends.</summary>
    public bool IsLooping { get => GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }

    /// <summary>Multiplies the pace of the story (0 to 4; 0 holds it still).</summary>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <summary>Shows the playback controls (default true).</summary>
    public bool AreControlsVisible { get => GetValue(AreControlsVisibleProperty); set => SetValue(AreControlsVisibleProperty, value); }

    /// <summary>The card's title, also its accessible name.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The model named in the header; null shows none.</summary>
    public ExplainerModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>The names of the playback buttons.</summary>
    public ExplainerPlaybackLabels PlaybackLabels { get => GetValue(PlaybackLabelsProperty); set => SetValue(PlaybackLabelsProperty, value); }

    /// <summary>True once the story has reached its end.</summary>
    public bool IsFinished { get => _isFinished; private set => SetAndRaise(IsFinishedProperty, ref _isFinished, value); }

    /// <summary>True while a step plays to the next beat.</summary>
    public bool IsStepping { get => _isStepping; private set => SetAndRaise(IsSteppingProperty, ref _isStepping, value); }

    /// <summary>True while there is a beat left to step to.</summary>
    public bool CanStep { get => _canStep; private set => SetAndRaise(CanStepProperty, ref _canStep, value); }

    /// <summary>Raised each time the story reaches its end.</summary>
    public event EventHandler<RoutedEventArgs>? Finished
    {
        add => AddHandler(FinishedEvent, value);
        remove => RemoveHandler(FinishedEvent, value);
    }

    /// <summary>Plays the story from where it is (from the start once it has finished).</summary>
    public void Play()
    {
        EnsureBuilt();
        _started = true;
        if (IsFinished)
        {
            RestartCore(play: true);
            return;
        }
        SetCurrentValue(IsPlayingProperty, true);
    }

    /// <summary>Freezes the story where it is.</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Plays the story to its next beat and stops there; at the end it does nothing.</summary>
    public void StepForward()
    {
        EnsureBuilt();
        _started = true;
        _userDriven = true;
        if (IsFinished)
        {
            return;
        }
        var target = _script.NextStopTime() ?? _script.Length;
        SetPlayingSilently(false);
        if (!ArcMotion.IsFull(this) || !StepsPlayThrough)
        {
            // Reduced motion (or a story whose steps jump): no travel, the beat applies at once.
            AdvanceTo(target);
            AfterStep();
            return;
        }
        _stepTarget = target;
        IsStepping = true;
        UpdateLoop();
    }

    /// <summary>
    /// Starts the story over from its first beat. It then plays, unless the component keeps the playing state on restart
    /// (a paused story stays paused at its start).
    /// </summary>
    public void Restart()
    {
        _userDriven = true;
        RestartCore(play: !RestartKeepsPlayState || IsPlaying || IsFinished);
    }

    /// <summary>The story time in seconds (frozen while paused).</summary>
    private protected double Now => _now;

    /// <summary>Seconds of frames the clock has run (also while paused or finished), for transitions that finish on their own.</summary>
    private protected double WallTime => _wall;

    /// <summary>True when the reader is driving the story (stepping or paused), so its beats should be announced.</summary>
    private protected bool IsReaderDriven => _userDriven && !IsPlaying;

    /// <summary>True when travel and sweeps are drawn (full motion); otherwise changes apply in place.</summary>
    private protected bool IsTravelEnabled => ArcMotion.IsFull(this);

    /// <summary>The script, for tests.</summary>
    internal ExplainerScript Script => _script;

    /// <summary>The live region (from the template; tests may set one).</summary>
    internal ExplainerAnnouncer? Announcer { get => _announcer; set => _announcer = value; }

    /// <summary>Seconds after the end before a looping story starts again (default 4).</summary>
    private protected virtual double LoopDelay => 4;

    /// <summary>
    /// True (default): a step plays the story at its speed up to the next stop beat. False: a step moves to the next stop
    /// at once and its changes animate from there (a story of fixed beats).
    /// </summary>
    private protected virtual bool StepsPlayThrough => true;

    /// <summary>The play button's glyph once the story has finished (default Play; RotateCcw reads as "again").</summary>
    private protected virtual Geometry ReplayIcon => ArcIcons.Play;

    /// <summary>The clock's pace while a step plays to its stop (default <see cref="Speed"/>).</summary>
    private protected virtual double StepSpeed => Speed;

    /// <summary>False (default): Restart always plays. True: Restart keeps a paused story paused at its start.</summary>
    private protected virtual bool RestartKeepsPlayState => false;

    /// <summary>Whether reduced motion opens the card on its finished story (default true).</summary>
    private protected virtual bool OpensFinishedWhenReduced => true;

    /// <summary>Builds the beats of the story from the current data into <paramref name="script"/>.</summary>
    private protected abstract void BuildStory(ExplainerScript script);

    /// <summary>Puts the scene back to its state before the first beat.</summary>
    private protected abstract void ResetScene();

    /// <summary>Called after the clock moved (story time or wall time): update drawn state, invalidate visuals.</summary>
    private protected virtual void OnTimeChanged()
    {
    }

    /// <summary>Called when the story reaches its end (before <see cref="Finished"/> is raised).</summary>
    private protected virtual void OnFinished()
    {
    }

    /// <summary>Called when the playing state changes (pause, resume, step, end).</summary>
    private protected virtual void OnPlaybackChanged()
    {
    }

    /// <summary>Speaks <paramref name="text"/> when the reader drives the story; quiet while it plays on its own.</summary>
    private protected void AnnounceBeat(string? text)
    {
        if (IsReaderDriven)
        {
            _announcer?.Announce(text);
        }
    }

    /// <summary>Speaks <paramref name="text"/> now (direct answers to the reader's actions).</summary>
    private protected void Announce(string? text) => _announcer?.Announce(text);

    /// <summary>Keeps the clock's frames coming for <paramref name="seconds"/> of wall time (a transition after a beat).</summary>
    private protected void RequestFrames(double seconds)
    {
        _framesUntil = Math.Max(_framesUntil, _wall + seconds);
        UpdateLoop();
    }

    /// <summary>Rebuilds the story after its data changed and starts it over; plays on if it was playing.</summary>
    private protected void Rebuild(bool? play = null)
    {
        if (!_built)
        {
            return;
        }
        if (!_started)
        {
            // Not shown yet: rebuild quietly so the first sight still autoplays (or opens finished under reduced motion).
            _script.Clear();
            BuildStory(_script);
            ResetScene();
            _now = 0;
            UpdateCanStep();
            UpdatePseudoClasses();
            OnTimeChanged();
            return;
        }
        RestartCore(play ?? IsPlaying);
    }

    /// <summary>Moves the story to its end at once (reduced motion's opening state; tests).</summary>
    internal void JumpToEnd()
    {
        EnsureBuilt();
        SetPlayingSilently(false);
        AdvanceTo(_script.Length);
        CheckFinished();
    }

    /// <summary>Moves the story on by <paramref name="seconds"/> of story time at once (tests).</summary>
    internal void Advance(double seconds)
    {
        EnsureBuilt();
        AdvanceTo(_now + seconds);
        CheckFinished();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Hook(ref _playButton, e.NameScope.Find<AvaloniaButton>(PlayButtonPart), OnPlayClick);
        Hook(ref _stepButton, e.NameScope.Find<AvaloniaButton>(StepButtonPart), OnStepClick);
        Hook(ref _restartButton, e.NameScope.Find<AvaloniaButton>(RestartButtonPart), OnRestartClick);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _playGlyph = null;
        _playGlyphKey = null;
        if (_playButton is not null)
        {
            _playGlyph = new ArcContentSwap { Mode = ContentSwapMode.Crossfade, HorizontalContentAlignment = HorizontalAlignment.Center };
            _playButton.Content = _playGlyph;
        }
        if (_restartButton is not null && _restartButton.Content is null)
        {
            _restartButton.Content = Glyph(ArcIcons.RotateCcw);
        }
        if (_stepButton is not null && _stepButton.Content is null)
        {
            _stepButton.Content = Glyph(ExplainerIcons.StepForward);
        }
        UpdateButtons();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureBuilt();
        _visibility?.Dispose();
        _visibility = null;
        if (!_started)
        {
            _visibility = new ViewportVisibility(this, 0.6, once: true);
            _visibility.VisibilityChanged += OnFirstSeen;
        }
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
        {
            if (IsPlaying)
            {
                EnsureBuilt();
                _started = true;
                _stepTarget = null;
                IsStepping = false;
                if (IsFinished)
                {
                    RestartCore(play: true);
                    return;
                }
            }
            if (!_syncingPlaying)
            {
                // A change from outside (binding, button) is the reader's.
                _userDriven = true;
            }
            UpdatePseudoClasses();
            UpdateButtons();
            UpdateLoop();
            OnPlaybackChanged();
        }
        else if (change.Property == SpeedProperty || change.Property == PlaybackLabelsProperty)
        {
            UpdateButtons();
            UpdateLoop();
        }
        else if (change.Property == ArcMotion.LevelProperty)
        {
            UpdateLoop();
        }
    }

    private static double CoerceSpeed(AvaloniaObject owner, double value)
        => double.IsFinite(value) ? Math.Clamp(value, 0, 4) : 1;

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }
        _built = true;
        _script.Clear();
        BuildStory(_script);
        ResetScene();
        _now = 0;
        UpdateCanStep();
    }

    private void OnFirstSeen(object? sender, bool visible)
    {
        if (!visible || _started)
        {
            return;
        }
        _started = true;
        if (!ArcMotion.IsFull(this))
        {
            if (OpensFinishedWhenReduced)
            {
                JumpToEnd();
            }
            return;
        }
        if (AutoPlay)
        {
            SetPlayingSilently(true);
        }
    }

    private void RestartCore(bool play)
    {
        _script.Clear();
        BuildStory(_script);
        ResetScene();
        _now = 0;
        _stepTarget = null;
        _loopWait = -1;
        IsStepping = false;
        IsFinished = false;
        UpdateCanStep();
        _started = true;
        SetPlayingSilently(play);
        UpdatePseudoClasses();
        UpdateButtons();
        UpdateLoop();
        OnTimeChanged();
    }

    private bool OnFrame(double dt)
    {
        _wall += dt;
        if (_loopWait >= 0)
        {
            _loopWait -= dt * Math.Max(Speed, 0.25);
            if (_loopWait < 0)
            {
                RestartCore(play: true);
                return true;
            }
        }
        else if (IsPlaying || _stepTarget is not null)
        {
            var target = _now + dt * (_stepTarget is not null ? StepSpeed : Speed);
            if (_stepTarget is { } step && target >= step)
            {
                target = step;
            }
            AdvanceTo(target);
            if (_stepTarget is { } reached && _now >= reached)
            {
                AfterStep();
            }
            CheckFinished();
        }
        OnTimeChanged();
        return WantsFrames();
    }

    private void AdvanceTo(double time)
    {
        if (time < _now)
        {
            return;
        }
        _now = time;
        _script.RunTo(time);
        UpdateCanStep();
        UpdatePseudoClasses();
        OnTimeChanged();
    }

    private void AfterStep()
    {
        // Read focus first: finishing disables the step button, which drops its focus.
        var stepHadFocus = _stepButton is { IsKeyboardFocusWithin: true };
        _stepTarget = null;
        IsStepping = false;
        CheckFinished();
        UpdateButtons();
        UpdateLoop();
        // At the end the step button turns off: keep focus in the card on Restart instead of dropping it to the page.
        if (!CanStep && stepHadFocus && _restartButton is not null)
        {
            _restartButton.Focus(NavigationMethod.Tab);
        }
    }

    private void CheckFinished()
    {
        if (IsFinished || !_script.IsFinished || _now < _script.Length)
        {
            return;
        }
        IsFinished = true;
        _stepTarget = null;
        IsStepping = false;
        var loop = IsLooping && IsPlaying;
        if (loop)
        {
            _loopWait = LoopDelay;
        }
        else
        {
            SetPlayingSilently(false);
        }
        UpdatePseudoClasses();
        UpdateButtons();
        OnFinished();
        RaiseEvent(new RoutedEventArgs(FinishedEvent, this));
        UpdateLoop();
    }

    private void SetPlayingSilently(bool playing)
    {
        _syncingPlaying = true;
        try
        {
            SetCurrentValue(IsPlayingProperty, playing);
        }
        finally
        {
            _syncingPlaying = false;
        }
        UpdatePseudoClasses();
        UpdateButtons();
        UpdateLoop();
    }

    private void UpdateCanStep() => CanStep = !_script.IsFinished || _now < _script.Length;

    private bool WantsFrames()
        => _loopWait >= 0 || _stepTarget is not null || _wall < _framesUntil || (IsPlaying && Speed > 0 && !IsFinished);

    private void UpdateLoop()
    {
        if (WantsFrames())
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(PlayingPseudoClass, IsPlaying);
        PseudoClasses.Set(SteppingPseudoClass, IsStepping);
        PseudoClasses.Set(FinishedPseudoClass, IsFinished);
        PseudoClasses.Set(ReadyPseudoClass, _now <= 0 && !IsPlaying && !IsStepping);
    }

    private void UpdateButtons()
    {
        var labels = PlaybackLabels;
        if (_playButton is not null && _playGlyph is not null)
        {
            var (key, icon, name) = IsPlaying && !IsFinished
                ? ("pause", ArcIcons.Pause, labels.Pause)
                : IsFinished ? ("replay", ReplayIcon, labels.Replay) : ("play", ArcIcons.Play, labels.Play);
            if (key != _playGlyphKey)
            {
                var first = _playGlyphKey is null;
                _playGlyphKey = key;
                var glyph = Glyph(icon);
                if (first)
                {
                    var mode = _playGlyph.Mode;
                    _playGlyph.Mode = ContentSwapMode.None;
                    _playGlyph.Content = glyph;
                    _playGlyph.Mode = mode;
                }
                else
                {
                    _playGlyph.Content = glyph;
                }
            }
            Avalonia.Automation.AutomationProperties.SetName(_playButton, name);
            Tooltip.SetTip(_playButton, name);
        }
        if (_stepButton is not null)
        {
            _stepButton.IsEnabled = CanStep;
            Avalonia.Automation.AutomationProperties.SetName(_stepButton, labels.Step);
            Tooltip.SetTip(_stepButton, labels.Step);
        }
        if (_restartButton is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(_restartButton, labels.Restart);
            Tooltip.SetTip(_restartButton, labels.Restart);
        }
    }

    private static ArcGlyph Glyph(Geometry data) => new() { Data = data, Size = 16 };

    private static void Hook(ref AvaloniaButton? field, AvaloniaButton? button, EventHandler<RoutedEventArgs> handler)
    {
        if (field is not null)
        {
            field.Click -= handler;
        }
        field = button;
        if (field is not null)
        {
            field.Click += handler;
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        _userDriven = true;
        if (IsPlaying && !IsFinished)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private void OnStepClick(object? sender, RoutedEventArgs e) => StepForward();

    private void OnRestartClick(object? sender, RoutedEventArgs e) => Restart();
}
