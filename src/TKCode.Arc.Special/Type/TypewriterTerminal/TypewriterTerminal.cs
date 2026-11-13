using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A terminal window that plays a script: each command is typed after the prompt at a person's uneven pace, its output
/// follows line by line, and a block caret blinks whenever the terminal waits.
/// </summary>
/// <remarks>
/// The script is <see cref="Lines"/>, written inline or set from code. The window reserves the height of the whole
/// script, so the page does not move while it plays; when it is given less height its text scrolls to follow the
/// newest line. The script waits while the terminal is scrolled out of view, so people see it from the start. Under
/// reduced motion commands appear whole and the caret does not blink; with motion off the whole script shows at once.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(ScreenPart, typeof(TypewriterTerminalScreen))]
[TemplatePart(ReplayPart, typeof(AvaloniaButton))]
[PseudoClasses(FinishedPseudoClass, PlayingPseudoClass)]
public class TypewriterTerminal : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string ScreenPart = "PART_Screen";
    public const string ReplayPart = "PART_Replay";
    public const string FinishedPseudoClass = ":finished";
    public const string PlayingPseudoClass = ":playing";

    /// <summary>How long the caret keeps blinking at the final prompt before it rests, solid, to save frames.</summary>
    private const double IdleBlinkSeconds = 12;

    private const double BlinkPeriod = 1.06;

    /// <summary>The title in the window's bar, also its accessible name.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TypewriterTerminal, string?>(nameof(Title), "Terminal");

    /// <summary>The prompt drawn before each command (a space follows it).</summary>
    public static readonly StyledProperty<string> PromptProperty =
        AvaloniaProperty.Register<TypewriterTerminal, string>(nameof(Prompt), "$");

    /// <summary>The typing pace in characters per second; each keystroke varies around it.</summary>
    public static readonly StyledProperty<double> TypingSpeedProperty =
        AvaloniaProperty.Register<TypewriterTerminal, double>(nameof(TypingSpeed), 28);

    /// <summary>The pause between a command's last keystroke and its output, as if Enter was pressed and the program started.</summary>
    public static readonly StyledProperty<TimeSpan> CommandDelayProperty =
        AvaloniaProperty.Register<TypewriterTerminal, TimeSpan>(nameof(CommandDelay), TimeSpan.FromMilliseconds(450));

    /// <summary>The pause between two lines of output.</summary>
    public static readonly StyledProperty<TimeSpan> OutputDelayProperty =
        AvaloniaProperty.Register<TypewriterTerminal, TimeSpan>(nameof(OutputDelay), TimeSpan.FromMilliseconds(90));

    /// <summary>Whether the script starts over after <see cref="LoopDelay"/> at the final prompt.</summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<TypewriterTerminal, bool>(nameof(IsLooping));

    /// <summary>How long a looping script rests at the final prompt before it starts over.</summary>
    public static readonly StyledProperty<TimeSpan> LoopDelayProperty =
        AvaloniaProperty.Register<TypewriterTerminal, TimeSpan>(nameof(LoopDelay), TimeSpan.FromSeconds(3));

    /// <summary>Whether the script plays on its own once the terminal is in view (true); otherwise call <see cref="Play"/>.</summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<TypewriterTerminal, bool>(nameof(AutoPlay), true);

    /// <summary>Whether a Replay button shows in the title bar once a script that does not loop has finished.</summary>
    public static readonly StyledProperty<bool> ShowsReplayProperty =
        AvaloniaProperty.Register<TypewriterTerminal, bool>(nameof(ShowsReplay), true);

    /// <summary>The label of the Replay button.</summary>
    public static readonly StyledProperty<string> ReplayTextProperty =
        AvaloniaProperty.Register<TypewriterTerminal, string>(nameof(ReplayText), "Replay");

    /// <summary>Whether the script is playing (it may still wait while the terminal is out of view).</summary>
    public static readonly DirectProperty<TypewriterTerminal, bool> IsPlayingProperty =
        AvaloniaProperty.RegisterDirect<TypewriterTerminal, bool>(nameof(IsPlaying), o => o.IsPlaying);

    /// <summary>Whether every line is out and the terminal waits at its final prompt.</summary>
    public static readonly DirectProperty<TypewriterTerminal, bool> IsFinishedProperty =
        AvaloniaProperty.RegisterDirect<TypewriterTerminal, bool>(nameof(IsFinished), o => o.IsFinished);

    /// <summary>Raised when a line is complete: a command after its Enter, an output line as it appears.</summary>
    public static readonly RoutedEvent<TypewriterTerminalLineEventArgs> LineCompletedEvent =
        RoutedEvent.Register<TypewriterTerminal, TypewriterTerminalLineEventArgs>(nameof(LineCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when the last line is out.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CompletedEvent =
        RoutedEvent.Register<TypewriterTerminal, RoutedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    private readonly TypewriterTerminalPlayer _player = new();
    private readonly LiveLoop _loop;
    private LazyFocusRing? _focusRing;
    private TypewriterTerminalScreen? _screen;
    private AvaloniaButton? _replay;
    private bool _isPlaying;
    private bool _isFinished;
    private bool _scriptDirty = true;
    private bool _caretShown = true;
    private double _blink;
    private double _idle;
    private string? _transcript;

    static TypewriterTerminal()
    {
        FocusableProperty.OverrideDefaultValue<TypewriterTerminal>(true);
        ClipToBoundsProperty.OverrideDefaultValue<TypewriterTerminal>(false);
    }

    public TypewriterTerminal()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        Lines.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (TypewriterTerminalLine line in e.OldItems)
                {
                    line.Changed -= OnLineChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (TypewriterTerminalLine line in e.NewItems)
                {
                    line.Changed += OnLineChanged;
                }
            }
            ScriptChanged();
        };
    }

    /// <summary>The script, in order: commands to type and lines of output.</summary>
    [Content]
    public AvaloniaList<TypewriterTerminalLine> Lines { get; } = [];

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="PromptProperty"/>
    public string Prompt
    {
        get => GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    /// <inheritdoc cref="TypingSpeedProperty"/>
    public double TypingSpeed
    {
        get => GetValue(TypingSpeedProperty);
        set => SetValue(TypingSpeedProperty, value);
    }

    /// <inheritdoc cref="CommandDelayProperty"/>
    public TimeSpan CommandDelay
    {
        get => GetValue(CommandDelayProperty);
        set => SetValue(CommandDelayProperty, value);
    }

    /// <inheritdoc cref="OutputDelayProperty"/>
    public TimeSpan OutputDelay
    {
        get => GetValue(OutputDelayProperty);
        set => SetValue(OutputDelayProperty, value);
    }

    /// <inheritdoc cref="IsLoopingProperty"/>
    public bool IsLooping
    {
        get => GetValue(IsLoopingProperty);
        set => SetValue(IsLoopingProperty, value);
    }

    /// <inheritdoc cref="LoopDelayProperty"/>
    public TimeSpan LoopDelay
    {
        get => GetValue(LoopDelayProperty);
        set => SetValue(LoopDelayProperty, value);
    }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    /// <inheritdoc cref="ShowsReplayProperty"/>
    public bool ShowsReplay
    {
        get => GetValue(ShowsReplayProperty);
        set => SetValue(ShowsReplayProperty, value);
    }

    /// <inheritdoc cref="ReplayTextProperty"/>
    public string ReplayText
    {
        get => GetValue(ReplayTextProperty);
        set => SetValue(ReplayTextProperty, value);
    }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            SetAndRaise(IsPlayingProperty, ref _isPlaying, value);
            PseudoClasses.Set(PlayingPseudoClass, value);
        }
    }

    /// <inheritdoc cref="IsFinishedProperty"/>
    public bool IsFinished
    {
        get => _isFinished;
        private set => SetAndRaise(IsFinishedProperty, ref _isFinished, value);
    }

    /// <summary>The number of lines that are complete.</summary>
    public int CompletedLines => Math.Min(_player.Line, Lines.Count);

    /// <inheritdoc cref="LineCompletedEvent"/>
    public event EventHandler<TypewriterTerminalLineEventArgs>? LineCompleted
    {
        add => AddHandler(LineCompletedEvent, value);
        remove => RemoveHandler(LineCompletedEvent, value);
    }

    /// <inheritdoc cref="CompletedEvent"/>
    public event EventHandler<RoutedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    internal TypewriterTerminalPlayer Player
    {
        get
        {
            EnsureScript();
            return _player;
        }
    }

    internal bool IsCaretShown => _caretShown;

    /// <summary>The whole script as plain text, with prompts: what assistive technology reads.</summary>
    internal string Transcript => _transcript ??= BuildTranscript();

    /// <summary>Plays the script from where it is.</summary>
    public void Play()
    {
        EnsureScript();
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            Complete();
            return;
        }
        if (_player.IsFinished && !IsLooping)
        {
            _player.Restart();
            _screen?.InvalidateVisual();
            UpdateFinished();
        }
        ApplyTiming();
        IsPlaying = true;
        _idle = 0;
        _loop.Start();
    }

    /// <summary>Holds the script where it is; <see cref="Play"/> carries on.</summary>
    public void Pause()
    {
        IsPlaying = false;
        _caretShown = true;
        _loop.Stop();
        _screen?.InvalidateVisual();
    }

    /// <summary>Starts the script again from its first line.</summary>
    public void Restart()
    {
        EnsureScript();
        _player.Restart();
        _screen?.InvalidateVisual();
        UpdateFinished();
        ScrollToCurrent();
        Play();
    }

    /// <summary>Shows every line at once.</summary>
    public void Complete()
    {
        EnsureScript();
        var wasFinished = _player.IsFinished;
        _player.Complete();
        _screen?.InvalidateVisual();
        UpdateFinished();
        ScrollToCurrent();
        if (!wasFinished)
        {
            RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
        }
        if (!IsLooping)
        {
            IsPlaying = false;
            _caretShown = true;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_screen is not null)
        {
            _screen.Owner = null;
        }
        if (_replay is not null)
        {
            _replay.Click -= OnReplayClick;
        }
        _screen = e.NameScope.Find<TypewriterTerminalScreen>(ScreenPart);
        _replay = e.NameScope.Find<AvaloniaButton>(ReplayPart);
        if (_screen is not null)
        {
            _screen.Owner = this;
            _screen.Reset();
        }
        if (_replay is not null)
        {
            _replay.Click += OnReplayClick;
        }
        _focusRing ??= new LazyFocusRing(this, TypewriterTerminalTokens.Key(TypewriterTerminalTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusSurface));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
        UpdateFinished();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateName();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureScript();
        if (AutoPlay && !_player.IsFinished)
        {
            Play();
        }
        else if (IsPlaying)
        {
            _loop.Start();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TitleProperty)
        {
            UpdateName();
        }
        else if (property == PromptProperty)
        {
            _transcript = null;
            _screen?.Reset();
        }
        else if (property == TypingSpeedProperty || property == CommandDelayProperty || property == OutputDelayProperty
                 || property == LoopDelayProperty || property == IsLoopingProperty)
        {
            ApplyTiming();
            UpdateFinished();
            if (property == IsLoopingProperty && IsLooping && IsPlaying)
            {
                _loop.Start();
            }
        }
        else if (property == ShowsReplayProperty)
        {
            UpdateFinished();
        }
        else if (property == ArcMotion.LevelProperty)
        {
            ApplyTiming();
            if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off && IsPlaying)
            {
                Complete();
            }
            _caretShown = true;
            _screen?.InvalidateVisual();
        }
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is AvaloniaButton)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Space:
                e.Handled = true;
                if (IsPlaying && !(_player.IsFinished && !IsLooping))
                {
                    Pause();
                }
                else
                {
                    Play();
                }
                break;
            case Key.End:
            case Key.Enter:
                e.Handled = true;
                Complete();
                break;
            case Key.Home:
                e.Handled = true;
                Restart();
                break;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TypewriterTerminalAutomationPeer(this);

    private bool Step(double dt)
    {
        EnsureScript();
        var lineBefore = _player.Line;
        var change = _player.Step(dt);
        if (change > 0)
        {
            _screen?.InvalidateVisual();
        }
        if (change == 2)
        {
            OnLinesAdvanced(lineBefore);
        }
        // The caret is solid while keys are struck and blinks while the terminal waits.
        var shown = !ArcMotion.IsFull(this) || _player.SinceKey < 0.5;
        if (!shown)
        {
            _blink += dt;
            shown = _blink % BlinkPeriod < BlinkPeriod / 2;
        }
        else
        {
            _blink = 0;
        }
        if (shown != _caretShown)
        {
            _caretShown = shown;
            _screen?.InvalidateVisual();
        }
        if (_player.IsFinished && !IsLooping)
        {
            _idle += dt;
            if (_idle > IdleBlinkSeconds || !ArcMotion.IsFull(this))
            {
                _caretShown = true;
                _screen?.InvalidateVisual();
                return false;
            }
        }
        return true;
    }

    private void OnLinesAdvanced(int lineBefore)
    {
        var line = _player.Line;
        if (line < lineBefore)
        {
            // The loop started over.
            UpdateFinished();
            ScrollToCurrent();
            return;
        }
        for (var i = lineBefore; i < line && i < Lines.Count; i++)
        {
            RaiseEvent(new TypewriterTerminalLineEventArgs(LineCompletedEvent, i, Lines[i]));
        }
        ScrollToCurrent();
        if (_player.IsFinished)
        {
            _idle = 0;
            if (!IsLooping)
            {
                // The caret may keep blinking for a while, but the script itself is over.
                IsPlaying = false;
            }
            UpdateFinished();
            RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
        }
    }

    private void ScrollToCurrent()
    {
        if (_screen is { } screen && screen.IsArrangeValid)
        {
            screen.BringIntoView(screen.LineBounds(_player.Line));
        }
    }

    private void UpdateFinished()
    {
        var finished = _scriptLoaded && _player.IsFinished;
        IsFinished = finished;
        PseudoClasses.Set(FinishedPseudoClass, finished);
        PseudoClasses.Set(PlayingPseudoClass, IsPlaying);
        if (_replay is not null)
        {
            _replay.IsVisible = finished && ShowsReplay && !IsLooping;
        }
    }

    private bool _scriptLoaded;

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        // The button hides as the script restarts; keep keyboard focus in the terminal.
        var hadFocus = _replay?.IsFocused == true;
        Restart();
        if (hadFocus)
        {
            Focus(NavigationMethod.Tab);
        }
    }

    private void OnLineChanged(object? sender, EventArgs e) => ScriptChanged();

    private void ScriptChanged()
    {
        _scriptDirty = true;
        _transcript = null;
        _screen?.Reset();
        if (IsPlaying)
        {
            _loop.Start();
        }
    }

    private void EnsureScript()
    {
        if (!_scriptDirty)
        {
            return;
        }
        _scriptDirty = false;
        _scriptLoaded = true;
        var count = Lines.Count;
        var lengths = new int[count];
        var commands = new bool[count];
        var pauses = new double[count];
        for (var i = 0; i < count; i++)
        {
            var line = Lines[i];
            lengths[i] = line.Text?.Length ?? 0;
            commands[i] = line.Kind == TypewriterTerminalLineKind.Command;
            pauses[i] = Math.Max(0, line.Pause.TotalSeconds);
        }
        // A script edited while playing keeps its place as far as it still exists.
        var keep = _player.Count > 0 ? Math.Min(_player.Line, count) : 0;
        ApplyTiming();
        _player.Load(lengths, commands, pauses);
        if (keep > 0)
        {
            for (var guard = 0; _player.Line < keep && guard < 1_000_000; guard++)
            {
                _player.Step(1);
            }
        }
        UpdateFinished();
    }

    private void ApplyTiming()
    {
        _player.Speed = Math.Max(1, TypingSpeed);
        _player.AfterCommand = Math.Max(0, CommandDelay.TotalSeconds);
        _player.BetweenOutput = Math.Max(0, OutputDelay.TotalSeconds);
        _player.LoopDelay = Math.Max(0, LoopDelay.TotalSeconds);
        _player.IsLooping = IsLooping;
        _player.TypesInstantly = !ArcMotion.IsFull(this);
    }

    private string BuildTranscript()
    {
        var builder = new StringBuilder();
        foreach (var line in Lines)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }
            if (line.Kind == TypewriterTerminalLineKind.Command)
            {
                builder.Append(Prompt).Append(' ');
            }
            builder.Append(line.Text);
        }
        return builder.ToString();
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Title ?? "Terminal");
        }
    }

    private bool _ownsName;

    private sealed class TypewriterTerminalAutomationPeer(TypewriterTerminal owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "terminal";

        protected override string? GetHelpTextCore() =>
            base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.Transcript;
    }
}

/// <summary>A line of a <see cref="TypewriterTerminal"/> script that has just completed.</summary>
public sealed class TypewriterTerminalLineEventArgs : RoutedEventArgs
{
    public TypewriterTerminalLineEventArgs(RoutedEvent routedEvent, int index, TypewriterTerminalLine line) : base(routedEvent)
    {
        Index = index;
        Line = line;
    }

    /// <summary>The line's position in <see cref="TypewriterTerminal.Lines"/>.</summary>
    public int Index { get; }

    /// <summary>The line itself.</summary>
    public TypewriterTerminalLine Line { get; }
}
