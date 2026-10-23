using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The panel's thin step progress bar (Arc Agent status): one 3px segment per step, 3px apart, all the same width. A done
/// step's segment fills from the left in the accent color over 480ms (a waiting or running one only tints its base, a
/// failed one fills danger red, a skipped one fills the muted color, a pending one stays the border color). Self-drawn on
/// a Core <see cref="LiveLoop"/>: one control instead of a Border per step. It is a progress bar for assistive technology.
/// </summary>
internal sealed class AgentStatusSegments : AgentStatusInked
{
    public const double SegmentHeight = 3;
    public const double SegmentGap = 3;

    private enum Fill
    {
        None,
        Accent,
        Danger,
        Muted
    }

    private sealed class Segment
    {
        public AgentStatusStepState State;
        public Fill Kind;
        public ExplainerValue Amount = new(0);
        public bool Tinted;
    }

    private readonly LiveLoop _loop;
    private readonly List<Segment> _segments = new();
    private double _now;
    private ArcMotionLevel _level = ArcMotionLevel.Full;
    private int _finished;
    private string _valueText = string.Empty;

    public AgentStatusSegments()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
    }

    /// <summary>How many segments there are.</summary>
    public int Count => _segments.Count;

    /// <summary>Steps finished (done or skipped), the bar's value.</summary>
    public int Finished => _finished;

    /// <summary>The value text for assistive technology ("4 of 6 steps finished").</summary>
    public string ValueText => _valueText;

    /// <summary>The motion level the bar draws at.</summary>
    public ArcMotionLevel Level
    {
        get => _level;
        set
        {
            _level = value;
            if (value == ArcMotionLevel.Off)
            {
                foreach (var s in _segments)
                {
                    s.Amount.Jump(s.Amount.Target);
                }
            }
        }
    }

    /// <summary>The fill of segment <paramref name="index"/> now (0…1).</summary>
    public double AmountOf(int index) => _segments[index].Amount.Value(_now);

    /// <summary>The state of segment <paramref name="index"/>.</summary>
    public AgentStatusStepState StateOf(int index) => _segments[index].State;

    /// <summary>Sets the segments' states and the value text; filled segments animate, the rest change at once.</summary>
    public void SetStates(IReadOnlyList<AgentStatusStepState> states, int finished, string valueText)
    {
        _finished = finished;
        _valueText = valueText;
        var animate = _level != ArcMotionLevel.Off && (TopLevel.GetTopLevel(this) is not null);
        var full = _level == ArcMotionLevel.Full && animate;
        while (_segments.Count > states.Count)
        {
            _segments.RemoveAt(_segments.Count - 1);
        }
        while (_segments.Count < states.Count)
        {
            _segments.Add(new Segment());
        }
        for (var i = 0; i < states.Count; i++)
        {
            var segment = _segments[i];
            var state = states[i];
            if (segment.State == state && i < _segments.Count)
            {
                continue;
            }
            segment.State = state;
            segment.Tinted = state is AgentStatusStepState.Active or AgentStatusStepState.Waiting;
            var kind = state switch
            {
                AgentStatusStepState.Done => Fill.Accent,
                AgentStatusStepState.Failed => Fill.Danger,
                AgentStatusStepState.Skipped => Fill.Muted,
                _ => Fill.None
            };
            var target = kind == Fill.None ? 0 : 1;
            if (kind != Fill.None)
            {
                segment.Kind = kind;
            }
            if (full)
            {
                segment.Amount.Tween(target, _now, 0.48, ArcEasings.Standard);
            }
            else
            {
                segment.Amount.Jump(target);
            }
        }
        if (full)
        {
            _loop.Start();
        }
        InvalidateVisual();
    }

    internal bool Advance(double seconds) => Step(seconds);

    private bool Step(double dt)
    {
        _now += dt;
        InvalidateVisual();
        foreach (var s in _segments)
        {
            if (s.Amount.IsMoving(_now))
            {
                return true;
            }
        }
        return false;
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, SegmentHeight);

    public override void Render(DrawingContext context)
    {
        var n = _segments.Count;
        if (n == 0)
        {
            return;
        }
        var border = ColorOf(BorderInk, Colors.LightGray);
        var accent = ColorOf(AccentInk, Colors.Black);
        var tint = ActiveSegmentInk is ISolidColorBrush t ? t.Color : Oklch.MixOklab(border, accent, 0.24);
        var danger = ColorOf(DangerInk, Colors.Red);
        var muted = ColorOf(MutedInk, Colors.Gray);
        var width = Bounds.Width;
        var segmentWidth = (width - SegmentGap * (n - 1)) / n;
        if (segmentWidth <= 0)
        {
            return;
        }
        for (var i = 0; i < n; i++)
        {
            var segment = _segments[i];
            var rect = new Rect(i * (segmentWidth + SegmentGap), 0, segmentWidth, SegmentHeight);
            var round = new RoundedRect(rect, SegmentHeight / 2);
            context.DrawRectangle(new ImmutableSolidColorBrush(segment.Tinted ? tint : border), null, round);
            var amount = Math.Clamp(segment.Amount.Value(_now), 0, 1);
            if (amount > 0.001)
            {
                var color = segment.Kind switch { Fill.Danger => danger, Fill.Muted => muted, _ => accent };
                var fill = new Rect(rect.X, rect.Y, Math.Max(SegmentHeight, rect.Width * amount), SegmentHeight);
                using (context.PushClip(rect))
                {
                    context.DrawRectangle(new ImmutableSolidColorBrush(color), null, new RoundedRect(fill, SegmentHeight / 2));
                }
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SegmentsPeer(this);

    private sealed class SegmentsPeer : ControlAutomationPeer, IRangeValueProvider
    {
        private readonly AgentStatusSegments _owner;

        public SegmentsPeer(AgentStatusSegments owner) : base(owner) => _owner = owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override string? GetNameCore() => AutomationProperties.GetName(_owner) is { Length: > 0 } name ? name : _owner.RunProgressName;

        protected override string? GetHelpTextCore() => _owner._valueText;

        public bool IsReadOnly => true;

        public double Maximum => _owner.Count;

        public double Minimum => 0;

        public double Value => _owner._finished;

        public double SmallChange => 1;

        public double LargeChange => 1;

        public void SetValue(double value)
        {
        }
    }

    /// <summary>The accessible name ("Run progress").</summary>
    internal string RunProgressName { get; set; } = "Run progress";
}
