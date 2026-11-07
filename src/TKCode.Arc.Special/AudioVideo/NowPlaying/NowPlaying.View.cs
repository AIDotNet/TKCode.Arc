using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public sealed partial class NowPlaying
{
    /// <summary>Who asked to open or fold the player: decides where focus goes and whether its ring shows.</summary>
    private enum FocusOrigin
    {
        Program,
        Pointer,
        Keyboard
    }

    private Border? _slot;
    private NowPlayingRoot _root = null!;
    private NowPlayingSurface _surface = null!;
    private Border _clip = null!;
    private NowPlayingStage _stage = null!;
    private readonly TranslateTransform _pullTransform = new();
    private ExplainerAnnouncer _announcer = null!;

    private NowPlayingArtwork _art = null!;
    private NowPlayingButton _main = null!;
    private NowPlayingTrackText _miniText = null!;
    private NowPlayingLevels _levels = null!;
    private NowPlayingProgressLine _progress = null!;
    private NowPlayingButton _play = null!;
    private NowPlayingPlayGlyph _playGlyph = null!;
    private NowPlayingButton _next = null!;
    private NowPlayingGlyph _nextGlyph = null!;
    private NowPlayingButton _collapse = null!;
    private NowPlayingTrackText _info = null!;
    private Grid _waveArea = null!;
    private NowPlayingWaveform _wave = null!;
    private TextBlock _elapsed = null!;
    private TextBlock _remaining = null!;
    private NowPlayingButton _previous = null!;

    private NowPlayingPresence _mainPresence = null!;
    private NowPlayingPresence _levelsPresence = null!;
    private NowPlayingPresence _progressPresence = null!;
    private NowPlayingPresence _collapsePresence = null!;
    private NowPlayingPresence _infoPresence = null!;
    private NowPlayingPresence _wavePresence = null!;
    private NowPlayingPresence _previousPresence = null!;
    private NowPlayingPresence _nextPresence = null!;

    private NowPlayingLayout _layout = NowPlayingLayout.Compute(NowPlayingLayout.MaxShellWidth, false);
    private MotionValue<double> _height = null!;
    private double _heightValue = NowPlayingLayout.MiniShellHeight;
    private bool _target;
    private bool _fly;
    private bool _compact;
    private bool _measuring;
    private MotionValue<double> _pull = null!;
    private MotionValue<double>? _restart;
    private double _restartOffset;
    private LiveLoop? _loop;
    private DragTracker _pullTracker = null!;
    private IDisposable? _viewportWatch;
    private TopLevel? _escapeHost;
    private int _shownSecond = -1;
    private double _shownDuration = -1;

    // --- parts the tests read ----------------------------------------------------------------------------------------

    internal NowPlayingArtwork Artwork => _art;

    internal NowPlayingButton MainButton => _main;

    internal NowPlayingTrackText MiniText => _miniText;

    internal NowPlayingLevels Levels => _levels;

    internal NowPlayingProgressLine ProgressLine => _progress;

    internal NowPlayingButton PlayButton => _play;

    internal NowPlayingPlayGlyph PlayGlyph => _playGlyph;

    internal NowPlayingButton NextButton => _next;

    internal NowPlayingButton CollapseButton => _collapse;

    internal NowPlayingTrackText InfoText => _info;

    internal NowPlayingWaveform Waveform => _wave;

    internal TextBlock ElapsedText => _elapsed;

    internal TextBlock RemainingText => _remaining;

    internal NowPlayingButton PreviousButton => _previous;

    internal ExplainerAnnouncer Announcer => _announcer;

    internal NowPlayingSurface Surface => _surface;

    internal Border ClipHost => _clip;

    internal NowPlayingLayout CurrentLayout => _layout;

    internal double ShellHeight => _heightValue;

    internal double ShellRadius => Lerp(MiniShellRadius, FullShellRadius, Math.Clamp(_layout.Progress(_heightValue), 0, 1.2));

    internal double PullOffset => _pull.Value;

    internal bool IsMorphing => _height.IsAnimating;

    internal bool IsCompact => _compact;

    internal NowPlayingClock Clock => _clock;

    internal double DisplayedSeconds => Math.Clamp(_clock.Position + _restartOffset, 0, _clock.Duration);

    internal NowPlayingPresence PresenceOf(Control part) =>
        part == _main ? _mainPresence
        : part == _levels ? _levelsPresence
        : part == _progress ? _progressPresence
        : part == _collapse ? _collapsePresence
        : part == _info ? _infoPresence
        : part == _waveArea || part == _wave ? _wavePresence
        : part == _previous ? _previousPresence
        : _nextPresence;

    /// <summary>Where the shared part <paramref name="part"/> is drawn now, in the shell's inner coordinates.</summary>
    internal Rect SharedRect(Control part)
    {
        var p = _fly ? _layout.Progress(_heightValue) : (_target ? 1 : 0);
        if (part == _art)
        {
            return NowPlayingLayout.Lerp(_layout.MiniArt, _layout.FullArt, p);
        }
        if (part == _play)
        {
            return NowPlayingLayout.Lerp(_layout.MiniPlay, _layout.FullPlay, p);
        }
        return _layout.IsCompact ? _layout.FullNext : NowPlayingLayout.Lerp(_layout.MiniNext, _layout.FullNext, p);
    }

    // --- building ----------------------------------------------------------------------------------------------------

    private static TextBlock Text(ArcDesignTokenKind size, ArcDesignTokenKind brush, FontWeight weight = FontWeight.Normal,
        double lineHeight = double.NaN, double letterSpacing = 0, bool display = false)
    {
        var text = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            FontWeight = weight,
            LineHeight = lineHeight,
            LetterSpacing = letterSpacing
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, brush);
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, size);
        ArcTokens.Bind(text, TextBlock.FontFamilyProperty, display ? ArcDesignTokenKind.DisplayFontFamily : ArcDesignTokenKind.BodyFontFamily);
        return text;
    }

    private static NowPlayingButton Button(string cls, Control content)
    {
        var button = new NowPlayingButton { Content = content };
        button.Classes.Add(cls);
        return button;
    }

    private void BuildParts()
    {
        _art = new NowPlayingArtwork();
        _art.Cursor = new Cursor(StandardCursorType.Hand);
        _art.ZIndex = -1;

        _miniText = new NowPlayingTrackText(
            () => Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, 19.6, -0.16),
            () => Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextSecondary, lineHeight: 16.8));
        _main = Button("main", _miniText);
        _main.ExpandState = false;
        _main.Click += (_, _) => SetExpanded(true, _main.PressedByKeyboard ? FocusOrigin.Keyboard : FocusOrigin.Pointer);

        _levels = new NowPlayingLevels();

        _progress = new NowPlayingProgressLine();
        ArcTokens.Bind(_progress, NowPlayingProgressLine.ForegroundProperty, ArcDesignTokenKind.Foreground);

        _playGlyph = new NowPlayingPlayGlyph();
        _play = Button("play", _playGlyph);
        _play.Click += (_, _) => TogglePlay();

        _nextGlyph = new NowPlayingGlyph { Data = ExplainerIcons.SkipForward };
        _next = Button("skip", _nextGlyph);
        _next.Click += (_, _) => Next();

        _collapse = Button("collapse", new NowPlayingGlyph { Data = ArcIcons.ChevronDown, IsFilled = false, Size = 18 });
        _collapse.ExpandState = true;
        _collapse.Click += (_, _) => SetExpanded(false, _collapse.PressedByKeyboard ? FocusOrigin.Keyboard : FocusOrigin.Pointer);

        _info = new NowPlayingTrackText(
            () => Text(ArcDesignTokenKind.TypeLG, ArcDesignTokenKind.Foreground, FontWeight.Medium, 23.1, -0.18, display: true),
            () => Text(ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextSecondary, lineHeight: 22.9));

        _wave = new NowPlayingWaveform();
        _wave.Scrubbed += OnScrubbed;
        _wave.KeySeek += seconds => CommitSeek(seconds, NowPlayingSeekReason.Keyboard);
        _elapsed = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: NowPlayingLayout.TimesHeight);
        _remaining = Text(ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: NowPlayingLayout.TimesHeight);
        _elapsed.FontFeatures = [FontFeature.Parse("tnum")];
        _remaining.FontFeatures = [FontFeature.Parse("tnum")];
        _remaining.HorizontalAlignment = HorizontalAlignment.Right;
        AutomationProperties.SetAccessibilityView(_elapsed, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_remaining, AccessibilityView.Raw);
        Grid.SetRow(_elapsed, 2);
        Grid.SetRow(_remaining, 2);
        _waveArea = new Grid
        {
            RowDefinitions = new RowDefinitions($"{NowPlayingLayout.WaveHeight},{NowPlayingLayout.TimesGap},{NowPlayingLayout.TimesHeight}"),
            Children = { _wave, _elapsed, _remaining }
        };

        _previous = Button("skip", new NowPlayingGlyph { Data = NowPlayingIcons.SkipBack, Size = 20 });
        _previous.Click += (_, _) => Previous();

        // Tab order: mini = main, play, next; full = collapse, position, previous, play, next.
        KeyboardNavigation.SetTabIndex(_main, 1);
        KeyboardNavigation.SetTabIndex(_collapse, 2);
        KeyboardNavigation.SetTabIndex(_waveArea, 3);
        KeyboardNavigation.SetTabIndex(_wave, 3);
        KeyboardNavigation.SetTabIndex(_previous, 4);
        KeyboardNavigation.SetTabIndex(_play, 5);
        KeyboardNavigation.SetTabIndex(_next, 6);

        _stage = new NowPlayingStage(this)
        {
            Children = { _main, _collapse, _waveArea, _previous, _play, _next, _info, _levels, _progress, _art }
        };
        _clip = new Border { ClipToBounds = true, Child = _stage };
        _surface = new NowPlayingSurface();
        _announcer = new ExplainerAnnouncer();
        _root = new NowPlayingRoot(this) { Children = { _surface, _clip, _announcer }, RenderTransform = _pullTransform };

        // Mini parts leave at once and come back staggered (main 100, levels 140, progress 160ms; main rises 4px);
        // full parts come in after 80 (info, 12px), 120 (waveform, 14px), 140 (previous, 10px) and 160ms (collapse).
        _mainPresence = new NowPlayingPresence(_main, 4, TimeSpan.FromMilliseconds(100));
        _levelsPresence = new NowPlayingPresence(_levels, 0, TimeSpan.FromMilliseconds(140));
        _progressPresence = new NowPlayingPresence(_progress, 0, TimeSpan.FromMilliseconds(160));
        _infoPresence = new NowPlayingPresence(_info, 12, TimeSpan.FromMilliseconds(80));
        _wavePresence = new NowPlayingPresence(_waveArea, 14, TimeSpan.FromMilliseconds(120));
        _previousPresence = new NowPlayingPresence(_previous, 10, TimeSpan.FromMilliseconds(140));
        _collapsePresence = new NowPlayingPresence(_collapse, 0, TimeSpan.FromMilliseconds(160));
        _nextPresence = new NowPlayingPresence(_next, 0, TimeSpan.FromMilliseconds(140));
        _collapsePresence.Hide(false);
        _infoPresence.Hide(false);
        _wavePresence.Hide(false);
        _previousPresence.Hide(false);

        _height = new MotionValue<double>(this, NowPlayingLayout.MiniShellHeight, MotionVectors.Double);
        _height.Changed += OnHeightChanged;
        _pull = new MotionValue<double>(this, 0, MotionVectors.Double);
        _pull.Changed += v => _pullTransform.Y = v;

        _pullTracker = new DragTracker(_art, new DragOptions { Threshold = 0.01 })
        {
            CanBegin = _ => _target
        };
        _pullTracker.Moved += (_, e) => _pull.Jump(NowPlayingPull.Offset(e.Offset.Y));
        _pullTracker.Completed += (_, e) => OnPullReleased(e.Velocity.Y);
        _pullTracker.Cancelled += (_, _) => OnPullReleased(0);

        // The last input inside the player decides whether a programmatic focus move shows the keyboard ring.
        AddHandler(PointerPressedEvent, (_, _) => _lastKeyboard = false, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, (_, _) => _lastKeyboard = true, RoutingStrategies.Tunnel, true);

        ApplyRadius();
        UpdateNames();
        UpdateSteps();
        ApplyTargetStyles();
    }

    // --- layout ------------------------------------------------------------------------------------------------------

    /// <summary>Called while measuring: the shell is min(available, 336) wide; a new width or breakpoint rebuilds both layouts.</summary>
    private void EnsureLayout(double shellWidth)
    {
        if (shellWidth == _layout.ShellWidth && _compact == _layout.IsCompact)
        {
            return;
        }
        var wasFull = _layout.FullHeight;
        _layout = NowPlayingLayout.Compute(shellWidth, _compact);
        var target = _target ? _layout.FullHeight : _layout.MiniHeight;
        _measuring = true;
        try
        {
            if (_height.IsAnimating && Math.Abs(wasFull - _layout.FullHeight) > 0.01)
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(_target ? NowPlayingMotion.Expand : NowPlayingMotion.Collapse));
            }
            else if (!_height.IsAnimating)
            {
                _height.Jump(target);
            }
        }
        finally
        {
            _measuring = false;
        }
        SyncCompactNext(false);
    }

    private void OnHeightChanged(double h)
    {
        _heightValue = h;
        ApplyRadius();
        if (!_measuring)
        {
            _root.InvalidateMeasure();
        }
        _stage.InvalidateArrange();
    }

    private void ApplyRadius()
    {
        // Shell 22 -> 30, its clip 21 -> 29 and the artwork 12 -> 20 by default; each pair comes from the token so the
        // corner style scales or flattens them.
        var p = _layout.Progress(_heightValue);
        _surface.CornerRadius = new CornerRadius(Lerp(MiniShellRadius, FullShellRadius, p));
        _clip.CornerRadius = new CornerRadius(Lerp(MiniClipRadius, FullClipRadius, p));
        _art.CornerRadius = new CornerRadius(Lerp(MiniArtRadius, FullArtRadius, p));
    }

    private void OnRadiiChanged()
    {
        if (_surface is not null)
        {
            ApplyRadius();
        }
    }

    private static double Lerp(CornerRadius from, CornerRadius to, double p) =>
        Math.Max(0, from.TopLeft + (to.TopLeft - from.TopLeft) * p);

    internal void MeasureStage(Size available)
    {
        Measure(_art, SharedRect(_art).Size);
        Measure(_play, TargetRect(_play).Size);
        Measure(_next, TargetRect(_next).Size);
        Measure(_main, _layout.MiniMain.Size);
        Measure(_levels, _layout.MiniLevels.Size);
        Measure(_progress, _layout.MiniProgress.Size);
        Measure(_collapse, _layout.Collapse.Size);
        Measure(_info, _layout.Info.Size);
        Measure(_waveArea, new Size(_layout.Wave.Width, _layout.Times.Bottom - _layout.Wave.Y));
        Measure(_previous, _layout.Previous.Size);
        _ = available;

        static void Measure(Control control, Size size) => control.Measure(size);
    }

    internal void ArrangeStage()
    {
        _art.Arrange(SharedRect(_art));
        _main.Arrange(_layout.MiniMain);
        _levels.Arrange(_layout.MiniLevels);
        _progress.Arrange(_layout.MiniProgress);
        _collapse.Arrange(_layout.Collapse);
        _info.Arrange(_layout.Info);
        _waveArea.Arrange(new Rect(_layout.Wave.X, _layout.Wave.Y, _layout.Wave.Width, _layout.Times.Bottom - _layout.Wave.Y));
        _previous.Arrange(_layout.Previous);
        Project(_play);
        Project(_next);
    }

    /// <summary>Where a shared button is laid out: its spot in the layout being moved to (it is scaled from there).</summary>
    private Rect TargetRect(Control part)
    {
        if (part == _play)
        {
            return _target ? _layout.FullPlay : _layout.MiniPlay;
        }
        return _target || _layout.IsCompact ? _layout.FullNext : _layout.MiniNext;
    }

    /// <summary>Lays a shared button out at its target and projects it onto where it is now (translate + scale).</summary>
    private void Project(NowPlayingButton button)
    {
        var target = TargetRect(button);
        button.Arrange(target);
        var now = SharedRect(button);
        if (button.RenderTransform is not MatrixTransform transform)
        {
            transform = new MatrixTransform();
            button.RenderTransform = transform;
            button.RenderTransformOrigin = RelativePoint.TopLeft;
        }
        var sx = target.Width > 0 ? now.Width / target.Width : 1;
        var sy = target.Height > 0 ? now.Height / target.Height : 1;
        transform.Matrix = Matrix.CreateScale(sx, sy) * Matrix.CreateTranslation(now.X - target.X, now.Y - target.Y);
    }

    internal Size MeasureRoot(Size available)
    {
        EnsureLayout(NowPlayingLayout.ShellWidthFor(available.Width));
        var w = _layout.ShellWidth;
        var h = Math.Max(0, _heightValue);
        _surface.Measure(new Size(w, h));
        _clip.Measure(new Size(Math.Max(0, w - 2), Math.Max(0, h - 2)));
        _announcer.Measure(default);
        return new Size(w, h);
    }

    internal void ArrangeRoot(Size finalSize)
    {
        var w = _layout.ShellWidth;
        var h = Math.Max(0, _heightValue);
        var x = Math.Max(0, (finalSize.Width - w) / 2);
        _surface.Arrange(new Rect(x, 0, w, h));
        _clip.Arrange(new Rect(x + NowPlayingLayout.Border, NowPlayingLayout.Border, Math.Max(0, w - 2), Math.Max(0, h - 2)));
        _announcer.Arrange(default);
    }

    // --- expand and collapse -----------------------------------------------------------------------------------------

    private FocusOrigin _expandOrigin = FocusOrigin.Program;

    private void SetExpanded(bool expanded, FocusOrigin origin)
    {
        _expandOrigin = origin;
        SetCurrentValue(IsExpandedProperty, expanded);
        _expandOrigin = FocusOrigin.Program;
    }

    private void OnExpandedChanged()
    {
        var expanded = IsExpanded;
        if (expanded == _target)
        {
            return;
        }
        var origin = _expandOrigin;
        var focusWithin = IsKeyboardFocusWithin;
        var animate = IsAttached;
        var level = ArcMotion.GetEffectiveLevel(this);
        _target = expanded;
        _fly = animate && level == ArcMotionLevel.Full;
        ApplyTargetStyles();

        if (expanded)
        {
            _mainPresence.Hide(animate);
            _levelsPresence.Hide(animate);
            _progressPresence.Hide(animate);
            _infoPresence.Show(animate);
            _wavePresence.Show(animate);
            _previousPresence.Show(animate);
            _collapsePresence.Show(animate);
        }
        else
        {
            _collapsePresence.Hide(animate);
            _infoPresence.Hide(animate);
            _wavePresence.Hide(animate);
            _previousPresence.Hide(animate);
            _mainPresence.Show(animate);
            _levelsPresence.Show(animate);
            _progressPresence.Show(animate);
            // A pull that closes the player springs its offset home while the shell folds.
            if (_pull.Value != 0 && !_pull.IsAnimating)
            {
                _ = _pull.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            }
        }
        SyncCompactNext(animate);

        var target = expanded ? _layout.FullHeight : _layout.MiniHeight;
        if (!animate || level == ArcMotionLevel.Off)
        {
            _height.Jump(target);
        }
        else if (level == ArcMotionLevel.Full)
        {
            // One spring for the shell and every flying part; a reversal mid-way keeps its velocity.
            _ = _height.AnimateTo(target, MotionSpec.Spring(expanded ? NowPlayingMotion.Expand : NowPlayingMotion.Collapse));
        }
        else
        {
            _ = _height.AnimateTo(target, MotionSpec.Tween(NowPlayingMotion.ReducedShell, NowPlayingMotion.EaseOut));
        }
        _stage.InvalidateMeasure();
        _stage.InvalidateArrange();
        _root.InvalidateMeasure();
        MoveFocus(expanded, origin, focusWithin);
    }

    private void SyncCompactNext(bool animate)
    {
        if (_layout.IsCompact && !_target)
        {
            if (_nextPresence.IsPresent)
            {
                _nextPresence.Hide(animate);
            }
        }
        else if (!_nextPresence.IsPresent)
        {
            _nextPresence.Show(animate && _layout.IsCompact);
        }
    }

    /// <summary>The mini and full styles of the shared buttons (fill and glyph size; the color changes over 240ms).</summary>
    private void ApplyTargetStyles()
    {
        _play.IsFull = _target;
        _next.IsFull = _target;
        _playGlyph.GlyphSize = _target ? 24 : 18;
        _nextGlyph.Size = _target ? 20 : 18;
        _main.ExpandState = _target;
        _art.Cursor = _target ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        _stage.InvalidateMeasure();
    }

    private void MoveFocus(bool expanded, FocusOrigin origin, bool focusWithin)
    {
        if (origin == FocusOrigin.Program && !focusWithin)
        {
            return;
        }
        var method = origin switch
        {
            FocusOrigin.Keyboard => NavigationMethod.Tab,
            FocusOrigin.Pointer => NavigationMethod.Pointer,
            _ => _lastKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer
        };
        if (expanded)
        {
            _collapse.Focus(method);
        }
        else
        {
            _main.Focus(method);
        }
    }

    private bool _lastKeyboard;

    // --- pull --------------------------------------------------------------------------------------------------------

    private void OnPullReleased(double velocityY)
    {
        var offset = _pull.Value;
        if (_target && NowPlayingPull.ShouldClose(offset, velocityY))
        {
            _ = _pull.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            SetExpanded(false, FocusOrigin.Pointer);
            return;
        }
        _ = _pull.AnimateTo(0, MotionSpec.Spring(NowPlayingPull.ReturnSpring));
    }

    // --- Escape --------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _escapeHost = TopLevel.GetTopLevel(this);
        _escapeHost?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
        _viewportWatch = ArcViewport.Observe(this, size => SetCompact(size.Width <= CompactViewport));
        if (ArcViewport.GetSize(this) is { } size)
        {
            SetCompact(size.Width <= CompactViewport);
        }
        UpdateClockLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _escapeHost?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _escapeHost = null;
        _viewportWatch?.Dispose();
        _viewportWatch = null;
    }

    /// <summary>The viewport width at or below which the mini bar drops its next button (a viewport breakpoint, as measured).</summary>
    internal const double CompactViewport = 360;

    private void SetCompact(bool compact)
    {
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        _root.InvalidateMeasure();
    }

    /// <summary>
    /// Escape folds the full player when focus is inside it or nowhere in particular (Arc listens on the whole document; a
    /// gallery page has several players and other widgets, so a player leaves Escape alone while focus is elsewhere).
    /// </summary>
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || !_target)
        {
            return;
        }
        var focused = _escapeHost?.FocusManager?.GetFocusedElement();
        if (focused is null || focused == _escapeHost || IsKeyboardFocusWithin)
        {
            e.Handled = true;
            SetExpanded(false, FocusOrigin.Keyboard);
        }
    }

    // --- playback ------------------------------------------------------------------------------------------------------

    private void UpdateClockLoop()
    {
        _loop ??= new LiveLoop(this, StepClock) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
        if (IsPlaying && CurrentTrack is not null && _clock.Duration > 0)
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private bool StepClock(double dt)
    {
        if (_clock.Advance(dt))
        {
            UpdateDisplay(false);
            Skip(NowPlayingSkipDirection.Forward, NowPlayingIndexChangeReason.Ended);
            return true;
        }
        UpdateDisplay(false);
        return true;
    }

    private void OnPlayingChanged()
    {
        _playGlyph.Show(IsPlaying, IsAttached);
        _levels.IsPlaying = IsPlaying;
        UpdateNames();
        UpdateClockLoop();
    }

    private void OnCurrentTimeChanged(double seconds)
    {
        if (_clock.Correct(seconds))
        {
            _restart?.Jump(0);
            UpdateDisplay(false);
        }
    }

    private void OnScrubbed(double seconds, bool commit)
    {
        _restart?.Jump(0);
        if (commit)
        {
            _clock.IsFrozen = false;
            CommitSeek(seconds, NowPlayingSeekReason.Drag);
            return;
        }
        // A press jumps straight to the pointer and the drag follows 1:1; the clock holds still until the release.
        _clock.IsFrozen = true;
        _clock.Seek(seconds);
        UpdateDisplay(true);
    }

    private void CommitSeek(double seconds, NowPlayingSeekReason reason)
    {
        if (CurrentTrack is null)
        {
            return;
        }
        _restart?.Jump(0);
        var max = reason == NowPlayingSeekReason.Keyboard ? Math.Max(0, _clock.Duration) : _clock.Duration;
        var target = double.IsFinite(seconds) ? Math.Clamp(seconds, 0, max) : 0;
        var frozen = _clock.IsFrozen;
        _clock.IsFrozen = false;
        _clock.Seek(target);
        _clock.IsFrozen = frozen;
        UpdateDisplay(true);
        RaiseEvent(new NowPlayingSeekEventArgs(SeekRequestedEvent, TimeSpan.FromSeconds(target), reason));
    }

    /// <summary>Previous after the threshold: back to zero at once, while the shown position glides home on the smooth spring.</summary>
    private void Restart()
    {
        var from = DisplayedSeconds;
        _clock.Seek(0);
        if (_restart is null)
        {
            _restart = new MotionValue<double>(this, 0, MotionVectors.Double);
            _restart.Changed += v =>
            {
                _restartOffset = v;
                UpdateDisplay(false);
            };
        }
        _restart.Jump(from);
        _ = _restart.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        UpdateDisplay(true);
        RaiseEvent(new NowPlayingSeekEventArgs(SeekRequestedEvent, TimeSpan.Zero, NowPlayingSeekReason.Restart));
    }

    private void LoadTrack(bool animate, NowPlayingSkipDirection direction)
    {
        var track = CurrentTrack;
        _shownTrack = track;
        _restart?.Jump(0);
        _clock.IsFrozen = false;
        _clock.Load(track?.Duration.TotalSeconds ?? 0);
        _art.Show(track?.Artwork, direction, animate);
        _art.Alt = track?.ArtworkAlt;
        _miniText.Show(track?.Title ?? string.Empty, track?.Artist ?? string.Empty, animate);
        _info.Show(track?.Title ?? string.Empty, track?.Artist ?? string.Empty, animate);
        UpdatePeaks();
        UpdateNames();
        UpdateDisplay(true);
        UpdateClockLoop();
    }

    private void UpdatePeaks()
    {
        var track = CurrentTrack;
        var count = PeakCount;
        var peaks = track is null ? []
            : track.Peaks is { Count: > 0 } given ? NowPlayingPeaks.Resample(given, count)
            : NowPlayingPeaks.Generate(track.Id, count);
        _wave.SetPeaks(peaks);
    }

    private void UpdateSteps()
    {
        _wave.SmallStep = SeekStep.TotalSeconds;
        _wave.LargeStep = SeekPageStep.TotalSeconds;
    }

    /// <summary>Shows the position: the waveform cut every frame, the texts and the slider value on whole seconds.</summary>
    private void UpdateDisplay(bool force)
    {
        var duration = _clock.Duration;
        var shown = DisplayedSeconds;
        var second = NowPlayingFormat.Floor(shown);
        string? valueText = null;
        if (force || second != _shownSecond || duration != _shownDuration)
        {
            _shownSecond = second;
            _shownDuration = duration;
            var labels = Labels;
            _elapsed.Text = NowPlayingFormat.Elapsed(shown);
            _remaining.Text = NowPlayingFormat.Remaining(shown, duration, labels.RemainingSign);
            valueText = NowPlayingFormat.ValueText(shown, duration, labels.PositionValue);
            Position = TimeSpan.FromSeconds(_clock.Position);
        }
        _wave.SetPosition(shown, duration, valueText);
        _progress.Fraction = duration > 0 ? shown / duration : 0;
    }

    private void UpdateNames()
    {
        var labels = Labels;
        var track = CurrentTrack;
        var title = track?.Title ?? string.Empty;
        var artist = track?.Artist ?? string.Empty;
        AutomationProperties.SetName(_main, NowPlayingLabels.Format(labels.ExpandButton, title, artist));
        AutomationProperties.SetName(_play, NowPlayingLabels.Format(IsPlaying ? labels.PauseButton : labels.PlayButton, title));
        AutomationProperties.SetName(_next, labels.NextButton);
        AutomationProperties.SetName(_previous, labels.PreviousButton);
        AutomationProperties.SetName(_collapse, labels.CollapseButton);
        AutomationProperties.SetName(_wave, labels.Position);
    }

    /// <summary>The live region's sentence now ("Low Orbit by Mara Quell, paused").</summary>
    internal string AnnouncementText
    {
        get
        {
            var labels = Labels;
            var track = CurrentTrack;
            return track is null ? string.Empty
                : NowPlayingLabels.Format(labels.Announcement, track.Title, track.Artist, IsPlaying ? labels.Playing : labels.Paused);
        }
    }

    private void Announce()
    {
        if (IsAttached)
        {
            _announcer.Announce(AnnouncementText);
        }
    }
}

/// <summary>The player's root: the shell surface, the clipped content and the live region, as tall as the shell is now.</summary>
internal sealed class NowPlayingRoot(NowPlaying owner) : Panel
{
    protected override Size MeasureOverride(Size availableSize) => owner.MeasureRoot(availableSize);

    protected override Size ArrangeOverride(Size finalSize)
    {
        owner.ArrangeRoot(finalSize);
        return finalSize;
    }
}

/// <summary>
/// The shell's content: every part placed by the player's two layouts — single-layout parts at their spots, the artwork
/// at its interpolated box, play and next laid out at their target and projected onto their interpolated box.
/// </summary>
internal sealed class NowPlayingStage(NowPlaying owner) : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        owner.MeasureStage(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        owner.ArrangeStage();
        return finalSize;
    }
}

/// <summary>Lucide glyphs the player needs that Core <see cref="ArcIcons"/> does not ship (lucide-static 1.59.0, ISC).</summary>
internal static class NowPlayingIcons
{
    // Lucide skip-back, ISC.
    private const string SkipBackData =
        "M17.971 4.285A2 2 0 0 1 21 6v12a2 2 0 0 1-3.029 1.715l-9.997-5.998a2 2 0 0 1-.003-3.432z M3 20V4";

    /// <summary>A bar then a triangle pointing left.</summary>
    public static Geometry SkipBack => ArcIcons.Parse(SkipBackData);
}
