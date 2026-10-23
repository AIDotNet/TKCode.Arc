using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a label is drawn: Live shimmers while working, Plain is the text color, Quiet the secondary color.</summary>
internal enum AgentStatusLabelTone
{
    Live,
    Plain,
    Quiet
}

/// <summary>The glyphs that share the orb's center.</summary>
internal enum AgentStatusGlyph
{
    Mark,
    Check,
    Cross,
    Stop
}

/// <summary>
/// The pill's head drawn in one control (Arc Agent status): the 24px orb (a track and a short arc that turns while
/// working, a ring that closes around a check or a cross, the agent's mark, a dot with one ping while input is needed),
/// the activity label with its icon (a new one rolls in from below while the old one rolls out above, the working label
/// shimmers with its count inside the shimmer, the right edge fades while the label is wider than its room) and the timer in
/// tabular figures. Everything animates on one clock (a Core <see cref="LiveLoop"/>) with tweens and springs evaluated at
/// the clock's time: ControlTheme transitions cannot express a rolling pair of labels, a phase-frozen spinner, a stroke
/// drawn along a path or a shimmer, and a single drawn layer keeps a header with several pills cheap. Reduced motion
/// keeps every state but drops the roll, the spin, the ping, the draw and the shimmer; Off jumps.
/// </summary>
internal sealed class AgentStatusFace : AgentStatusInked
{
    public const double OrbSize = 24;
    public const double OrbLeft = 7;
    public const double OrbTop = 7;
    public const double LabelLeft = OrbLeft + OrbSize + 8;
    public const double RightPadding = 14;
    public const double TimerGap = 8;
    public const double IconSize = 16;
    public const double IconGap = 6;
    public const double FaceHeight = 38;
    public const double MaxFade = 24;

    private const double RingRadius = 10.75;
    private const double RingThickness = 1.5;
    private const double ArcSweep = 86.4;
    private const double TurnSeconds = 0.9;
    private const double ShimmerPeriod = 2.4;
    private const double ShimmerSweep = 0.72;
    private const double LetterSpacing = -0.14;

    private static readonly SpringSpec s_glyphSpring = SpringSpec.FromPhysical(529, 46);
    private static readonly SpringSpec s_dotSpring = SpringSpec.FromPhysical(473, 41.3);
    private static readonly SpringSpec s_rollInSpring = SpringSpec.FromPhysical(1406, 66);
    private static readonly SpringSpec s_rollOutSpring = SpringSpec.FromPhysical(1806, 72.25);

    private static readonly ImmutableLinearGradientBrush s_band = new(
        [
            new ImmutableGradientStop(0, Color.FromArgb(0, 0, 0, 0)),
            new ImmutableGradientStop(0.5, Color.FromArgb(255, 0, 0, 0)),
            new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))
        ], startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));

    // Path of the drawn check and cross in 24 units (the glyphs are 14px boxes of stroke 2: bounding boxes 13×9 and 9×9).
    private static readonly AiStroke s_check = new(new Point(5.5, 12.5), new Point(10, 17), new Point(18.5, 8));
    private static readonly AiStroke s_crossA = new(new Point(7.5, 7.5), new Point(16.5, 16.5));
    private static readonly AiStroke s_crossB = new(new Point(16.5, 7.5), new Point(7.5, 16.5));

    private sealed class Unit
    {
        public string Key = string.Empty;
        public string Text = string.Empty;
        public string? Count;
        public Geometry? Icon;
        public AgentStatusLabelTone Tone;
        public TextLayout? Base;
        public TextLayout? Highlight;
        public double Width;
        public ExplainerValue Opacity = new(1);
        public ExplainerValue Y = new(0);
        public double RemoveAt = double.MaxValue;
    }

    private readonly LiveLoop _loop;
    private readonly List<Unit> _leaving = new();
    private readonly EllipseGeometry _circle = new();
    private readonly PathGeometry _arcGeometry = new();
    private readonly PathGeometry _closedGeometry = new();
    private readonly PathFigure _closedFigure = new() { IsClosed = false, IsFilled = false };
    private readonly ArcSegment _closedSegment = new() { SweepDirection = SweepDirection.Clockwise };
    private readonly Pen?[] _arcPens = new Pen?[17];
    private Pen? _trackPen;
    private Pen? _closedPen;
    private Pen? _checkPen;
    private Pen? _iconPen;
    private Pen? _markPen;
    private IBrush? _markPenBrush;
    private bool _palette;
    private Color _foreground, _secondary, _muted, _border, _accent, _success, _danger, _surface, _shimmerBase;

    private Unit? _current;
    private TextLayout? _timerLayout;
    private string _timerText = "0:00";
    private bool _timerVisible;
    private ExplainerValue _timerOpacity = new(0);

    private AgentStatusRunState _state = AgentStatusRunState.Idle;
    private bool _stateSet;
    private ArcMotionLevel _level = ArcMotionLevel.Full;
    private double _now;
    private readonly AiSpin _spinner = new(TurnSeconds);
    private double _pingStart = double.NegativeInfinity;

    private readonly ExplainerValue[] _glyphOpacity = [new(1), new(0), new(0), new(0)];
    private readonly ExplainerValue[] _glyphScale = [new(1), new(0.4), new(0.4), new(0.4)];
    private ExplainerValue _checkDraw = new(0);
    private ExplainerValue _crossDraw = new(0);
    private ExplainerValue _trackOpacity = new(0);
    private ExplainerValue _arcOpacity = new(0);
    private ExplainerValue _arcMuted = new(0);
    private ExplainerValue _closedDraw = new(0);
    private ExplainerValue _closedOpacity = new(0);
    private ExplainerValue _dotScale = new(0);
    private ExplainerValue _dotOpacity = new(0);
    private bool _closedDanger;
    private AgentStatusGlyph _glyph = AgentStatusGlyph.Mark;
    private Geometry? _mark;
    private IBrush? _markBrush;
    private Size _geometrySize;

    public AgentStatusFace()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        FontSize = 14;
        ClipToBounds = false;
    }

    /// <summary>Raised when <see cref="NaturalWidth"/> changed (a new label, the timer showing or hiding, a font change).</summary>
    public event Action? NaturalWidthChanged;

    private double _lastNatural = double.NaN;

    private void NotifyNatural()
    {
        var natural = NaturalWidth;
        if (double.IsNaN(_lastNatural) || Math.Abs(natural - _lastNatural) > 0.01)
        {
            _lastNatural = natural;
            NaturalWidthChanged?.Invoke();
        }
    }

    /// <summary>The state the orb and the label are in.</summary>
    public AgentStatusRunState State => _state;

    /// <summary>Whole width the face wants: the orb, the label, the timer when it shows, and the padding.</summary>
    public double NaturalWidth => LabelLeft + LabelWidth + (_timerVisible ? TimerGap + TimerWidth : 0) + RightPadding;

    /// <summary>The current label's width (icon included), 0 before one is set.</summary>
    public double LabelWidth => _current?.Width ?? 0;

    /// <summary>The timer text's width.</summary>
    public double TimerWidth { get { EnsureTimer(); return _timerLayout?.WidthIncludingTrailingWhitespace ?? 0; } }

    /// <summary>The key of the label shown (equal keys do not roll).</summary>
    public string? LabelKey => _current?.Key;

    /// <summary>The text of the label shown, count included.</summary>
    public string? LabelText => _current is null ? null : _current.Text + (_current.Count is null ? string.Empty : " " + _current.Count);

    /// <summary>The tone of the label shown.</summary>
    public AgentStatusLabelTone LabelTone => _current?.Tone ?? AgentStatusLabelTone.Quiet;

    /// <summary>Labels still rolling out.</summary>
    public int LeavingCount => _leaving.Count;

    /// <summary>The timer text and whether it shows.</summary>
    public string TimerText => _timerText;

    public bool IsTimerVisible => _timerVisible;

    /// <summary>The glyph the orb shows (or is changing to).</summary>
    public AgentStatusGlyph Glyph => _glyph;

    /// <summary>The turning arc's angle in degrees.</summary>
    public double SpinAngle => _spinner.Angle;

    /// <summary>True while the loop runs (a spinner, a shimmer or a transition).</summary>
    public bool IsLoopRunning => _loop.IsRunning;

    /// <summary>Opacity of the dot while input is needed (0…1).</summary>
    public double DotOpacity => _dotOpacity.Value(_now);

    /// <summary>The ping's scale (1 before and after it) and opacity.</summary>
    public (double Scale, double Opacity) Ping => PingAt(_now);

    /// <summary>How much of the closed ring is drawn (0…1).</summary>
    public double ClosedProgress => _closedDraw.Value(_now);

    public double ClosedOpacity => _closedOpacity.Value(_now);

    public double GlyphOpacity(AgentStatusGlyph glyph) => _glyphOpacity[(int)glyph].Value(_now);

    public double GlyphScale(AgentStatusGlyph glyph) => _glyphScale[(int)glyph].Value(_now);

    public double GlyphDraw(AgentStatusGlyph glyph) => glyph == AgentStatusGlyph.Check ? _checkDraw.Value(_now) : glyph == AgentStatusGlyph.Cross ? _crossDraw.Value(_now) : 1;

    public double TrackOpacity => _trackOpacity.Value(_now);

    public double ArcOpacity => _arcOpacity.Value(_now);

    public double ArcMuted => _arcMuted.Value(_now);

    public double TimerOpacity => _timerOpacity.Value(_now);

    /// <summary>The label's right edge fade in px at the current width (0 when the label fits).</summary>
    public double CurrentFade => FadeFor(Bounds.Width);

    /// <summary>The motion level the face draws at (set by the owner from the control's effective level).</summary>
    public ArcMotionLevel Level
    {
        get => _level;
        set
        {
            if (_level == value)
            {
                return;
            }
            _level = value;
            if (value != ArcMotionLevel.Full)
            {
                _spinner.Reset();
                _pingStart = double.NegativeInfinity;
            }
            if (value == ArcMotionLevel.Off)
            {
                SettleAll();
            }
            EnsureLoop();
            InvalidateVisual();
        }
    }

    private bool Animates => _level != ArcMotionLevel.Off && (TopLevel.GetTopLevel(this) is not null);

    private bool IsFull => _level == ArcMotionLevel.Full && (TopLevel.GetTopLevel(this) is not null);

    /// <summary>Sets the agent's mark: a 24 × 24 stroked glyph and its color (null: the default mark in the text color).</summary>
    public void SetMark(Geometry? mark, IBrush? brush)
    {
        _mark = mark ?? AgentStatusIcons.Bot;
        _markBrush = brush;
        _markPen = null;
        InvalidateVisual();
    }

    /// <summary>Sets the orb's state: the glyph, the ring and the dot move to it (at once the first time and at Off).</summary>
    public void SetState(AgentStatusRunState state)
    {
        var first = !_stateSet;
        if (!first && state == _state)
        {
            return;
        }
        var previous = _state;
        _state = state;
        _stateSet = true;
        var animate = !first && Animates;
        var full = animate && IsFull;
        var now = _now;
        var wasClosed = previous is AgentStatusRunState.Done or AgentStatusRunState.Failed;
        var closed = state is AgentStatusRunState.Done or AgentStatusRunState.Failed;

        // The ring: a track while the run is live, an arc only while working or paused.
        var live = state is AgentStatusRunState.Working or AgentStatusRunState.NeedsInput or AgentStatusRunState.Paused;
        Move(ref _trackOpacity, live ? 1 : 0, animate, 0.24);
        Move(ref _arcOpacity, state is AgentStatusRunState.Working or AgentStatusRunState.Paused ? 1 : 0, animate, 0.24);
        Move(ref _arcMuted, state == AgentStatusRunState.Paused ? 1 : 0, animate, 0.24);

        // The closed ring draws itself around the check or the cross, and takes itself back when the run settles.
        if (closed)
        {
            _closedDanger = state == AgentStatusRunState.Failed;
            _closedOpacity.Jump(0);
            _closedDraw.Jump(0);
            if (full)
            {
                _closedOpacity.Tween(1, now, 0.15, ArcEasings.Standard);
                _closedDraw.Tween(1, now, 0.44, ArcEasings.Enter);
            }
            else
            {
                if (animate)
                {
                    _closedOpacity.Tween(1, now, 0.13, ArcEasings.Standard);
                }
                else
                {
                    _closedOpacity.Jump(1);
                }
                _closedDraw.Jump(1);
            }
        }
        else if (wasClosed)
        {
            if (full)
            {
                _closedOpacity.Tween(0, now, 0.4, ArcEasings.Standard);
                _closedDraw.Tween(0, now, 0.4, ArcEasings.Standard);
            }
            else if (animate)
            {
                _closedOpacity.Tween(0, now, 0.12, ArcEasings.Standard);
            }
            else
            {
                _closedOpacity.Jump(0);
                _closedDraw.Jump(0);
            }
        }

        // The needs-input dot springs in with one ping; it leaves in 100ms.
        if (state == AgentStatusRunState.NeedsInput)
        {
            if (full)
            {
                _dotScale.Jump(0);
                _dotScale.Spring(1, now, s_dotSpring);
                _dotOpacity.Tween(1, now, 0.12, ArcEasings.Standard);
                _pingStart = now;
            }
            else
            {
                _dotScale.Jump(1);
                Move(ref _dotOpacity, 1, animate, 0.12);
            }
        }
        else
        {
            _pingStart = double.NegativeInfinity;
            if (full)
            {
                _dotScale.Tween(0, now, 0.1, ArcEasings.Standard);
            }
            else if (!animate)
            {
                _dotScale.Jump(0);
            }
            Move(ref _dotOpacity, 0, animate, 0.1);
        }

        var glyph = state switch
        {
            AgentStatusRunState.Done => AgentStatusGlyph.Check,
            AgentStatusRunState.Failed => AgentStatusGlyph.Cross,
            AgentStatusRunState.Stopped => AgentStatusGlyph.Stop,
            _ => AgentStatusGlyph.Mark
        };
        MoveGlyph(glyph, animate, full);
        EnsureLoop();
        InvalidateVisual();
    }

    private void MoveGlyph(AgentStatusGlyph glyph, bool animate, bool full)
    {
        var now = _now;
        var changed = glyph != _glyph;
        _glyph = glyph;
        for (var i = 0; i < _glyphOpacity.Length; i++)
        {
            var shown = i == (int)glyph;
            var target = !shown ? 0 : (i == (int)AgentStatusGlyph.Mark && _state == AgentStatusRunState.Paused ? 0.5 : 1);
            if (!animate)
            {
                _glyphOpacity[i].Jump(target);
                _glyphScale[i].Jump(shown ? 1 : 0.4);
                continue;
            }
            if (shown && changed)
            {
                _glyphOpacity[i].Jump(0);
                _glyphOpacity[i].Tween(target, now, full ? 0.15 : 0.13, ArcEasings.Standard);
                if (full)
                {
                    _glyphScale[i].Jump(0.4);
                    _glyphScale[i].Spring(1, now, s_glyphSpring);
                }
                else
                {
                    _glyphScale[i].Jump(1);
                }
            }
            else if (shown)
            {
                _glyphOpacity[i].Tween(target, now, 0.24, ArcEasings.Standard);
            }
            else if (_glyphOpacity[i].Value(now) > 0.001)
            {
                _glyphOpacity[i].Tween(0, now, full ? 0.09 : 0.12, ArcEasings.Standard);
                if (full)
                {
                    _glyphScale[i].Tween(0.4, now, 0.14, ArcEasings.Exit);
                }
            }
        }
        if (changed && (glyph == AgentStatusGlyph.Check || glyph == AgentStatusGlyph.Cross))
        {
            ref var draw = ref (glyph == AgentStatusGlyph.Check ? ref _checkDraw : ref _crossDraw);
            draw.Jump(0);
            if (full)
            {
                draw.Tween(1, now, 0.28, ArcEasings.Enter, 0.12);
            }
            else
            {
                draw.Jump(1);
            }
        }
    }

    private void Move(ref ExplainerValue value, double target, bool animate, double seconds)
    {
        if (animate)
        {
            value.Tween(target, _now, seconds, ArcEasings.Standard);
        }
        else
        {
            value.Jump(target);
        }
    }

    /// <summary>
    /// Shows a label. A different <paramref name="key"/> rolls the new label in from below and the old one out above
    /// (Full), crossfades (Reduced) or replaces (Off); the same key only changes the text and count in place.
    /// </summary>
    public void SetLabel(string key, string text, string? count, Geometry? icon, AgentStatusLabelTone tone)
    {
        if (_current is { } current && current.Key == key && current.Tone == tone && ReferenceEquals(current.Icon, icon))
        {
            if (current.Text != text || current.Count != count)
            {
                current.Text = text;
                current.Count = count;
                Build(current);
                InvalidateMeasure();
                InvalidateVisual();
                NotifyNatural();
            }
            return;
        }
        var unit = new Unit { Key = key, Text = text, Count = count, Icon = icon, Tone = tone };
        Build(unit);
        var animate = Animates && _current is not null;
        if (_current is { } old)
        {
            if (animate)
            {
                var now = _now;
                if (IsFull)
                {
                    old.Opacity.Tween(0, now, 0.14, ArcEasings.Standard);
                    old.Y.Spring(-12, now, s_rollOutSpring);
                    old.RemoveAt = now + 0.16;
                    _leaving.Add(old);
                    unit.Opacity.Jump(0);
                    unit.Opacity.Tween(1, now, 0.18, ArcEasings.Standard);
                    unit.Y.Jump(12);
                    unit.Y.Spring(0, now, s_rollInSpring);
                }
                else
                {
                    unit.Opacity.Jump(0);
                    unit.Opacity.Tween(1, now, 0.1, ArcEasings.Standard);
                }
            }
        }
        _current = unit;
        EnsureLoop();
        InvalidateMeasure();
        InvalidateVisual();
        NotifyNatural();
    }

    /// <summary>Shows or hides the timer and sets its text (it fades in 160ms and out in 100ms).</summary>
    public void SetTimer(string text, bool visible)
    {
        if (_timerText != text)
        {
            _timerText = text;
            _timerLayout = null;
        }
        if (_timerVisible != visible)
        {
            _timerVisible = visible;
            if (Animates)
            {
                _timerOpacity.Tween(visible ? 1 : 0, _now, visible ? 0.16 : 0.1, ArcEasings.Standard);
            }
            else
            {
                _timerOpacity.Jump(visible ? 1 : 0);
            }
            EnsureLoop();
            InvalidateMeasure();
            NotifyNatural();
        }
        InvalidateVisual();
    }

    /// <summary>Advances the face's clock by <paramref name="seconds"/> (the loop does it every frame; tests call it).</summary>
    internal bool Advance(double seconds) => Step(seconds);

    private bool Step(double dt)
    {
        _now += dt;
        if (IsFull && _state == AgentStatusRunState.Working)
        {
            _spinner.Advance(dt);
        }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            if (_now >= _leaving[i].RemoveAt)
            {
                _leaving.RemoveAt(i);
            }
        }
        UpdateClosed();
        InvalidateVisual();
        return NeedsLoop();
    }

    private bool NeedsLoop()
    {
        if (_level == ArcMotionLevel.Off)
        {
            return false;
        }
        if (IsFull && _state == AgentStatusRunState.Working)
        {
            return true;
        }
        if (_leaving.Count > 0 || PingAt(_now).Opacity > 0 || _now - _pingStart < 1.01)
        {
            return true;
        }
        var now = _now;
        if (_current is { } c && (c.Opacity.IsMoving(now) || c.Y.IsMoving(now)))
        {
            return true;
        }
        for (var i = 0; i < _glyphOpacity.Length; i++)
        {
            if (_glyphOpacity[i].IsMoving(now) || _glyphScale[i].IsMoving(now))
            {
                return true;
            }
        }
        return _checkDraw.IsMoving(now) || _crossDraw.IsMoving(now) || _trackOpacity.IsMoving(now) || _arcOpacity.IsMoving(now)
               || _arcMuted.IsMoving(now) || _closedDraw.IsMoving(now) || _closedOpacity.IsMoving(now) || _dotScale.IsMoving(now)
               || _dotOpacity.IsMoving(now) || _timerOpacity.IsMoving(now);
    }

    private void EnsureLoop()
    {
        if (NeedsLoop())
        {
            _loop.Start();
        }
    }

    /// <summary>Puts every animated value at its target (motion Off).</summary>
    private void SettleAll()
    {
        _leaving.Clear();
        if (_current is { } c)
        {
            c.Opacity.Jump(1);
            c.Y.Jump(0);
        }
        _timerOpacity.Jump(_timerVisible ? 1 : 0);
        _trackOpacity.Jump(_trackOpacity.Target);
        _arcOpacity.Jump(_arcOpacity.Target);
        _arcMuted.Jump(_arcMuted.Target);
        _closedOpacity.Jump(_closedOpacity.Target);
        _closedDraw.Jump(_closedDraw.Target);
        _dotScale.Jump(_dotScale.Target);
        _dotOpacity.Jump(_dotOpacity.Target);
        _checkDraw.Jump(_checkDraw.Target);
        _crossDraw.Jump(_crossDraw.Target);
        for (var i = 0; i < _glyphOpacity.Length; i++)
        {
            _glyphOpacity[i].Jump(_glyphOpacity[i].Target);
            _glyphScale[i].Jump(_glyphScale[i].Target);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureLoop();
    }

    protected override void OnInksChanged()
    {
        _palette = false;
        if (_current is { } c)
        {
            Build(c);
        }
        foreach (var l in _leaving)
        {
            Build(l);
        }
        _timerLayout = null;
        InvalidateMeasure();
        InvalidateVisual();
        NotifyNatural();
    }

    private void EnsurePalette()
    {
        if (_palette)
        {
            return;
        }
        _foreground = ColorOf(ForegroundInk, Colors.Black);
        _secondary = ColorOf(SecondaryInk, Colors.Gray);
        _muted = ColorOf(MutedInk, Colors.Gray);
        _border = ColorOf(BorderInk, Colors.LightGray);
        _accent = ColorOf(AccentInk, Colors.Black);
        _success = ColorOf(SuccessInk, Colors.Green);
        _danger = ColorOf(DangerInk, Colors.Red);
        _surface = ColorOf(SurfaceInk, Colors.White);
        _shimmerBase = ColorOf(ShimmerBaseInk, _secondary);
        _trackPen = new Pen(new ImmutableSolidColorBrush(_border), RingThickness);
        _closedPen = null;
        _checkPen = null;
        _iconPen = new Pen(new ImmutableSolidColorBrush(_secondary), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        Array.Clear(_arcPens);
        for (var i = 0; i < _arcPens.Length; i++)
        {
            var color = Oklch.MixOklab(_foreground, _muted, i / 16.0);
            _arcPens[i] = new Pen(new ImmutableSolidColorBrush(color), RingThickness, lineCap: PenLineCap.Round);
        }
        _palette = true;
    }

    private void Build(Unit unit)
    {
        EnsurePalette();
        var text = unit.Count is null ? unit.Text : unit.Text + " " + unit.Count;
        var tone = unit.Tone switch
        {
            AgentStatusLabelTone.Live => _shimmerBase,
            AgentStatusLabelTone.Plain => _foreground,
            _ => _secondary
        };
        unit.Base?.Dispose();
        unit.Highlight?.Dispose();
        unit.Base = Layout(text, tone);
        unit.Highlight = unit.Tone == AgentStatusLabelTone.Live ? Layout(text, _foreground) : null;
        unit.Width = (unit.Icon is null ? 0 : IconSize + IconGap) + unit.Base.WidthIncludingTrailingWhitespace;
    }

    private TextLayout Layout(string text, Color color) =>
        new(text, Typeface, FontSize, new ImmutableSolidColorBrush(color), TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.None, null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, double.NaN,
            LetterSpacing, 1, Tabular);

    private void EnsureTimer()
    {
        if (_timerLayout is null)
        {
            EnsurePalette();
            _timerLayout = Layout(_timerText, _muted);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsurePalette();
        EnsureTimer();
        var width = double.IsInfinity(availableSize.Width) ? NaturalWidth : Math.Min(NaturalWidth, availableSize.Width);
        return new Size(width, FaceHeight);
    }

    private double FadeFor(double width)
    {
        if (_current is null)
        {
            return 0;
        }
        var slot = SlotWidth(width);
        return Math.Clamp(_current.Width - slot, 0, MaxFade);
    }

    private double SlotWidth(double width) =>
        Math.Max(0, width - RightPadding - LabelLeft - (_timerVisible ? TimerGap + TimerWidth : 0));

    private (double Scale, double Opacity) PingAt(double now)
    {
        var t = now - _pingStart - 0.1;
        if (t < 0 || t >= 0.9)
        {
            return (1, 0);
        }
        var e = ArcEasings.Standard.Ease(t / 0.9);
        return (1 + 1.6 * e, 0.4 * (1 - e));
    }

    private void UpdateClosed()
    {
        var size = new Size(OrbSize, OrbSize);
        var p = _closedDraw.Value(_now);
        var c = OrbSize / 2;
        if (_geometrySize != size)
        {
            _circle.Center = new Point(c, c);
            _circle.RadiusX = RingRadius;
            _circle.RadiusY = RingRadius;
            _closedGeometry.Figures ??= new PathFigures();
            if (_closedGeometry.Figures.Count == 0)
            {
                _closedGeometry.Figures.Add(_closedFigure);
                _closedFigure.Segments ??= new PathSegments();
                _closedFigure.Segments.Add(_closedSegment);
            }
            var arc = new PathFigure { IsClosed = false, IsFilled = false, StartPoint = new Point(c + RingRadius, c) };
            arc.Segments = new PathSegments
            {
                new ArcSegment
                {
                    Point = new Point(c + RingRadius * Math.Cos(ArcSweep * Math.PI / 180), c + RingRadius * Math.Sin(ArcSweep * Math.PI / 180)),
                    Size = new Size(RingRadius, RingRadius),
                    SweepDirection = SweepDirection.Clockwise
                }
            };
            _arcGeometry.Figures = new PathFigures { arc };
            _geometrySize = size;
        }
        if (p > 0.0001 && p < 0.9999)
        {
            var to = (-90 + p * 360) * Math.PI / 180;
            _closedFigure.StartPoint = new Point(c, c - RingRadius);
            _closedSegment.Point = new Point(c + RingRadius * Math.Cos(to), c + RingRadius * Math.Sin(to));
            _closedSegment.Size = new Size(RingRadius, RingRadius);
            _closedSegment.IsLargeArc = p > 0.5;
        }
    }

    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        if (_current is null)
        {
            return;
        }
        UpdateClosed();
        var width = Bounds.Width;
        using (context.PushTransform(Matrix.CreateTranslation(OrbLeft, OrbTop)))
        {
            DrawOrb(context);
        }
        var slot = SlotWidth(width);
        var clip = new Rect(LabelLeft, 9, slot, 20);
        var fade = FadeFor(width);
        using (context.PushClip(clip))
        {
            IDisposable? mask = null;
            if (fade > 0.01 && slot > 0)
            {
                var p = Math.Max(0, 1 - fade / slot);
                mask = context.PushOpacityMask(new ImmutableLinearGradientBrush(
                    [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(p, Colors.Black), new ImmutableGradientStop(1, Color.FromArgb(0, 0, 0, 0))],
                    startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative)), clip);
            }
            foreach (var unit in _leaving)
            {
                DrawUnit(context, unit);
            }
            DrawUnit(context, _current);
            mask?.Dispose();
        }
        var timerOpacity = _timerOpacity.Value(_now);
        if (timerOpacity > 0.003)
        {
            EnsureTimer();
            var layout = _timerLayout!;
            using (context.PushOpacity(timerOpacity))
            {
                layout.Draw(context, new Point(width - RightPadding - layout.WidthIncludingTrailingWhitespace, (FaceHeight - layout.Height) / 2));
            }
        }
    }

    private void DrawUnit(DrawingContext context, Unit unit)
    {
        var opacity = unit.Opacity.Value(_now);
        if (opacity <= 0.003 || unit.Base is null)
        {
            return;
        }
        var dy = unit.Y.Value(_now);
        var x = LabelLeft;
        using (context.PushOpacity(opacity))
        {
            if (unit.Icon is { } icon)
            {
                using (context.PushTransform(Matrix.CreateScale(IconSize / 24, IconSize / 24) * Matrix.CreateTranslation(x, (FaceHeight - IconSize) / 2 + dy)))
                {
                    context.DrawGeometry(null, _iconPen, icon);
                }
                x += IconSize + IconGap;
            }
            var origin = new Point(x, (FaceHeight - unit.Base.Height) / 2 + dy);
            var shimmer = unit.Tone == AgentStatusLabelTone.Live && unit.Highlight is not null;
            if (!shimmer)
            {
                unit.Base.Draw(context, origin);
                return;
            }
            if (!(IsFull && _state == AgentStatusRunState.Working))
            {
                // Without the sweep the working label is the plain text color (reduced motion, and while paused or settling).
                unit.Highlight!.Draw(context, origin);
                return;
            }
            unit.Base.Draw(context, origin);
            var w = Math.Max(1, unit.Base.WidthIncludingTrailingWhitespace);
            var phase = _now % ShimmerPeriod / ShimmerPeriod;
            double center;
            if (phase < ShimmerSweep)
            {
                center = -0.5 * w + 2 * w * ArcEasings.InOut.Ease(phase / ShimmerSweep);
            }
            else
            {
                center = 1.5 * w;
            }
            var half = 0.36 * w;
            var band = new Rect(origin.X + center - half, origin.Y, 2 * half, unit.Base.Height);
            using (context.PushClip(band))
            using (context.PushOpacityMask(s_band, band))
            {
                unit.Highlight!.Draw(context, origin);
            }
        }
    }

    private void DrawOrb(DrawingContext context)
    {
        var c = OrbSize / 2;
        var track = _trackOpacity.Value(_now);
        if (track > 0.003)
        {
            using (context.PushOpacity(track))
            {
                context.DrawGeometry(null, _trackPen, _circle);
            }
        }
        var arc = _arcOpacity.Value(_now);
        if (arc > 0.003)
        {
            var level = (int)Math.Round(Math.Clamp(_arcMuted.Value(_now), 0, 1) * 16);
            using (context.PushOpacity(arc))
            using (context.PushTransform(_spinner.Turn(new Point(c, c))))
            {
                context.DrawGeometry(null, _arcPens[level], _arcGeometry);
            }
        }
        var closed = _closedOpacity.Value(_now);
        var draw = _closedDraw.Value(_now);
        if (closed > 0.003 && draw > 0.0001)
        {
            var color = _closedDanger ? _danger : _success;
            _closedPen ??= new Pen(new ImmutableSolidColorBrush(color), RingThickness, lineCap: PenLineCap.Round);
            if (_closedPen.Brush is ISolidColorBrush b && b.Color != color)
            {
                _closedPen = new Pen(new ImmutableSolidColorBrush(color), RingThickness, lineCap: PenLineCap.Round);
            }
            using (context.PushOpacity(closed))
            {
                context.DrawGeometry(null, _closedPen, draw >= 0.9999 ? _circle : _closedGeometry);
            }
        }

        DrawGlyph(context, AgentStatusGlyph.Mark, c);
        DrawGlyph(context, AgentStatusGlyph.Check, c);
        DrawGlyph(context, AgentStatusGlyph.Cross, c);
        DrawGlyph(context, AgentStatusGlyph.Stop, c);

        var dotOpacity = _dotOpacity.Value(_now);
        var dotScale = Math.Max(0, _dotScale.Value(_now));
        if (dotOpacity > 0.003 && dotScale > 0.003)
        {
            var center = new Point(20, 4);
            var ping = PingAt(_now);
            if (ping.Opacity > 0.003)
            {
                context.DrawEllipse(new ImmutableSolidColorBrush(_accent, ping.Opacity), null, center, 4 * ping.Scale, 4 * ping.Scale);
            }
            using (context.PushOpacity(dotOpacity))
            {
                context.DrawEllipse(new ImmutableSolidColorBrush(_surface), null, center, 6 * dotScale, 6 * dotScale);
                context.DrawEllipse(new ImmutableSolidColorBrush(_accent), null, center, 4 * dotScale, 4 * dotScale);
            }
        }
    }

    private void DrawGlyph(DrawingContext context, AgentStatusGlyph glyph, double c)
    {
        var opacity = _glyphOpacity[(int)glyph].Value(_now);
        if (opacity <= 0.003)
        {
            return;
        }
        var scale = Math.Max(0, _glyphScale[(int)glyph].Value(_now));
        using (context.PushOpacity(opacity))
        {
            switch (glyph)
            {
                case AgentStatusGlyph.Mark:
                    if (_mark is { } mark)
                    {
                        if (_markPen is null || !ReferenceEquals(_markPenBrush, _markBrush))
                        {
                            _markPenBrush = _markBrush;
                            _markPen = new Pen(_markBrush ?? new ImmutableSolidColorBrush(_foreground), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                        }
                        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(0.5 * scale, 0.5 * scale) * Matrix.CreateTranslation(c, c)))
                        {
                            context.DrawGeometry(null, _markPen, mark);
                        }
                    }
                    break;
                case AgentStatusGlyph.Stop:
                    using (context.PushTransform(Matrix.CreateTranslation(-c, -c) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(c, c)))
                    {
                        context.DrawRectangle(new ImmutableSolidColorBrush(_secondary), null, new RoundedRect(new Rect(c - 4, c - 4, 8, 8), 2));
                    }
                    break;
                default:
                    DrawStroked(context, glyph, c, scale);
                    break;
            }
        }
    }

    /// <summary>The check or the cross drawn along its path: two or three points joined, cut at the draw progress.</summary>
    private void DrawStroked(DrawingContext context, AgentStatusGlyph glyph, double c, double scale)
    {
        var color = glyph == AgentStatusGlyph.Check ? _success : _danger;
        if (_checkPen is null || _checkPen.Brush is not ISolidColorBrush b || b.Color != color)
        {
            // Thickness 2 in 24-unit coordinates: 1.17px at the 14px glyph size.
            _checkPen = new Pen(new ImmutableSolidColorBrush(color), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }
        var p = Math.Clamp(glyph == AgentStatusGlyph.Check ? _checkDraw.Value(_now) : _crossDraw.Value(_now), 0, 1);
        using (context.PushTransform(Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(14.0 / 24 * scale, 14.0 / 24 * scale) * Matrix.CreateTranslation(c, c)))
        {
            var unitPen = _checkPen;
            if (glyph == AgentStatusGlyph.Check)
            {
                s_check.Draw(context, unitPen, p);
            }
            else
            {
                s_crossA.Draw(context, unitPen, Math.Clamp(p * 2, 0, 1));
                s_crossB.Draw(context, unitPen, Math.Clamp(p * 2 - 1, 0, 1));
            }
        }
    }
}
