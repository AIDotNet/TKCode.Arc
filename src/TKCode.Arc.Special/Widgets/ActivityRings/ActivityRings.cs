using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="ActivityRings.GoalReached"/>.</summary>
public sealed class ActivityRingGoalReachedEventArgs : RoutedEventArgs
{
    internal ActivityRingGoalReachedEventArgs(RoutedEvent routedEvent, ActivityRing ring) : base(routedEvent)
    {
        Ring = ring;
    }

    /// <summary>The ring whose value has just reached its goal.</summary>
    public ActivityRing Ring { get; }
}

/// <summary>
/// Concentric progress rings for daily goals, outer ring first. Each ring fills clockwise from 12 o'clock on a spring,
/// is full at its goal and keeps going round past it, the new lap overlapping the first with a shade under its head. A
/// legend beside or under the rings names each goal and rolls its value.
/// </summary>
/// <remarks>
/// Put <see cref="ActivityRing"/> items in <see cref="Rings"/>. A ring without a <see cref="ActivityRing.Brush"/> takes the
/// theme's series colors in order. On first view the rings fill from empty one after another; later changes spring from
/// where the ring is. Reduced motion and Off show the values at once.
/// </remarks>
[TemplatePart(CanvasPart, typeof(ActivityRingsCanvas))]
[TemplatePart(LegendPart, typeof(StackPanel))]
[TemplatePart(LayoutPart, typeof(ActivityRingsLayout))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
public class ActivityRings : TemplatedControl
{
    internal const string CanvasPart = "PART_Canvas";
    internal const string LegendPart = "PART_Legend";
    internal const string LayoutPart = "PART_Layout";
    internal const string LivePart = "PART_Live";

    /// <summary>The largest progress a ring draws: four laps.</summary>
    public const double MaxProgress = 4;

    /// <summary>Accessible name of the rings as a whole.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ActivityRings, string>(nameof(Label), "Activity");

    /// <summary>Whether the legend with each goal's name and value is shown next to the rings.</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<ActivityRings, bool>(nameof(IsLegendVisible), true);

    /// <summary>Outer diameter of the rings in px. They shrink to fit when there is less room.</summary>
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<ActivityRings, double>(nameof(Diameter), 168);

    /// <summary>Stroke width of each ring in px.</summary>
    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<ActivityRings, double>(nameof(RingThickness), 15);

    /// <summary>Space between two rings in px.</summary>
    public static readonly StyledProperty<double> RingGapProperty =
        AvaloniaProperty.Register<ActivityRings, double>(nameof(RingGap), 3);

    /// <summary>Text read after a goal's name when its ring closes, such as "goal reached".</summary>
    public static readonly StyledProperty<string> GoalReachedTextProperty =
        AvaloniaProperty.Register<ActivityRings, string>(nameof(GoalReachedText), "goal reached");

    /// <summary>Raised when a ring's value reaches its goal after the rings were shown (not on first view).</summary>
    public static readonly RoutedEvent<ActivityRingGoalReachedEventArgs> GoalReachedEvent =
        RoutedEvent.Register<ActivityRings, ActivityRingGoalReachedEventArgs>(nameof(GoalReached), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> TrackOpacityProperty =
        AvaloniaProperty.Register<ActivityRings, double>(nameof(TrackOpacity), 0.16);

    internal static readonly StyledProperty<IBrush?> OverlapShadowProperty =
        AvaloniaProperty.Register<ActivityRings, IBrush?>(nameof(OverlapShadow));

    internal static readonly StyledProperty<TimeSpan> FillDurationProperty =
        AvaloniaProperty.Register<ActivityRings, TimeSpan>(nameof(FillDuration), TimeSpan.FromMilliseconds(900));

    internal static readonly StyledProperty<double> FillBounceProperty =
        AvaloniaProperty.Register<ActivityRings, double>(nameof(FillBounce), 0.18);

    internal static readonly StyledProperty<TimeSpan> RingStaggerProperty =
        AvaloniaProperty.Register<ActivityRings, TimeSpan>(nameof(RingStagger), TimeSpan.FromMilliseconds(90));

    internal static readonly StyledProperty<double> LegendRingSizeProperty =
        AvaloniaProperty.Register<ActivityRings, double>("LegendRingSize", 16);

    internal static readonly StyledProperty<double> LegendRingThicknessProperty =
        AvaloniaProperty.Register<ActivityRings, double>("LegendRingThickness", 3);

    internal static readonly StyledProperty<IBrush?> Series1Property =
        AvaloniaProperty.Register<ActivityRings, IBrush?>("Series1");

    internal static readonly StyledProperty<IBrush?> Series2Property =
        AvaloniaProperty.Register<ActivityRings, IBrush?>("Series2");

    internal static readonly StyledProperty<IBrush?> Series3Property =
        AvaloniaProperty.Register<ActivityRings, IBrush?>("Series3");

    internal static readonly StyledProperty<IBrush?> Series4Property =
        AvaloniaProperty.Register<ActivityRings, IBrush?>("Series4");

    private readonly List<ActivityRingsTrack> _tracks = [];
    private readonly List<ActivityRingsLegendRow> _rows = [];
    private ActivityRingsCanvas? _canvas;
    private StackPanel? _legend;
    private ExplainerAnnouncer? _live;
    private bool _revealed;

    // Theme-set values, read through these so the XAML setters can name them.
    internal double LegendRingSize => GetValue(LegendRingSizeProperty);
    internal double LegendRingThickness => GetValue(LegendRingThicknessProperty);
    internal IBrush? Series1 => GetValue(Series1Property);
    internal IBrush? Series2 => GetValue(Series2Property);
    internal IBrush? Series3 => GetValue(Series3Property);
    internal IBrush? Series4 => GetValue(Series4Property);

    static ActivityRings()
    {
        AffectsMeasure<ActivityRings>(DiameterProperty);
    }

    public ActivityRings()
    {
        Rings.CollectionChanged += OnRingsChanged;
    }

    /// <summary>The goals, outer ring first.</summary>
    [Content]
    public AvaloniaList<ActivityRing> Rings { get; } = [];

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible
    {
        get => GetValue(IsLegendVisibleProperty);
        set => SetValue(IsLegendVisibleProperty, value);
    }

    /// <inheritdoc cref="DiameterProperty"/>
    public double Diameter
    {
        get => GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    /// <inheritdoc cref="RingThicknessProperty"/>
    public double RingThickness
    {
        get => GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    /// <inheritdoc cref="RingGapProperty"/>
    public double RingGap
    {
        get => GetValue(RingGapProperty);
        set => SetValue(RingGapProperty, value);
    }

    /// <inheritdoc cref="GoalReachedTextProperty"/>
    public string GoalReachedText
    {
        get => GetValue(GoalReachedTextProperty);
        set => SetValue(GoalReachedTextProperty, value);
    }

    /// <summary>Raised when a ring's value reaches its goal after the rings were shown (not on first view).</summary>
    public event EventHandler<ActivityRingGoalReachedEventArgs>? GoalReached
    {
        add => AddHandler(GoalReachedEvent, value);
        remove => RemoveHandler(GoalReachedEvent, value);
    }

    internal double TrackOpacity => GetValue(TrackOpacityProperty);

    internal IBrush? OverlapShadow => GetValue(OverlapShadowProperty);

    internal TimeSpan FillDuration => GetValue(FillDurationProperty);

    internal double FillBounce => GetValue(FillBounceProperty);

    internal TimeSpan RingStagger => GetValue(RingStaggerProperty);

    internal IReadOnlyList<ActivityRingsTrack> Tracks => _tracks;

    internal ActivityRingsCanvas? Canvas => _canvas;

    internal IReadOnlyList<ActivityRingsLegendRow> LegendRows => _rows;

    internal string? Announcement => _live?.Announcement;

    /// <summary>The progress of a value against a goal: 0 for a goal of zero or less, at most <see cref="MaxProgress"/>.</summary>
    internal static double ProgressOf(double value, double goal)
    {
        if (!(goal > 0) || !(value > 0) || double.IsNaN(value))
        {
            return 0;
        }
        return Math.Min(MaxProgress, value / goal);
    }

    /// <summary>The progress each ring draws now, outer ring first (it trails the values while a spring runs).</summary>
    public IReadOnlyList<double> GetShownProgress()
    {
        var shown = new double[_tracks.Count];
        for (var i = 0; i < shown.Length; i++)
        {
            shown[i] = _tracks[i].Shown;
        }
        return shown;
    }

    /// <summary>Empties the rings and fills them again, one after another, as on first view.</summary>
    public void Replay()
    {
        var full = ArcMotion.IsFull(this) && IsLoaded;
        for (var i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            if (full)
            {
                Spring(track).Jump(0);
            }
            Fill(track, full ? Stagger(i) : TimeSpan.Zero);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_canvas is not null)
        {
            _canvas.Owner = null;
        }
        _legend?.Children.Clear();
        _canvas = e.NameScope.Find<ActivityRingsCanvas>(CanvasPart);
        _legend = e.NameScope.Find<StackPanel>(LegendPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_canvas is not null)
        {
            _canvas.Owner = this;
        }
        if (_legend is not null)
        {
            foreach (var row in _rows)
            {
                _legend.Children.Add(row);
            }
        }
        _canvas?.Relayout();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_revealed)
        {
            return;
        }
        _revealed = true;
        // First view: the rings fill from empty, outer ring first. Without full motion they simply show their values.
        var full = ArcMotion.IsFull(this);
        for (var i = 0; i < _tracks.Count; i++)
        {
            Fill(_tracks[i], full ? Stagger(i) : TimeSpan.Zero);
        }
        foreach (var row in _rows)
        {
            row.Reveal();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RingThicknessProperty || change.Property == RingGapProperty)
        {
            foreach (var track in _tracks)
            {
                track.ResetPens();
            }
            _canvas?.Relayout();
        }
        else if (change.Property == DiameterProperty)
        {
            _canvas?.InvalidateMeasure();
        }
        else if (change.Property == TrackOpacityProperty || change.Property == OverlapShadowProperty)
        {
            _canvas?.InvalidateVisual();
        }
        else if (change.Property == Series1Property || change.Property == Series2Property ||
                 change.Property == Series3Property || change.Property == Series4Property)
        {
            UpdateBrushes();
        }
        else if (change.Property == LegendRingSizeProperty || change.Property == LegendRingThicknessProperty)
        {
            foreach (var row in _rows)
            {
                row.SetRingSize(GetValue(LegendRingSizeProperty), GetValue(LegendRingThicknessProperty));
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ActivityRingsAutomationPeer(this);

    /// <summary>One sentence per ring for assistive technology: "Move: 420 of 600 kcal, 70 percent."</summary>
    internal string Describe()
    {
        var parts = new List<string>(_tracks.Count);
        foreach (var track in _tracks)
        {
            var ring = track.Ring;
            var percent = Math.Round(ring.Progress * 100);
            var unit = string.IsNullOrEmpty(ring.Unit) ? string.Empty : " " + ring.Unit;
            parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.##} of {2:0.##}{3}, {4:0} percent.",
                ring.Label, Math.Max(0, ring.Value), Math.Max(0, ring.Goal), unit, percent));
        }
        return string.Join(" ", parts);
    }

    private TimeSpan Stagger(int index) => TimeSpan.FromTicks(RingStagger.Ticks * index);

    private MotionValue<double> Spring(ActivityRingsTrack track)
    {
        if (track.Spring is { } existing)
        {
            return existing;
        }
        var spring = new MotionValue<double>(this, track.Shown, MotionVectors.Double);
        spring.Changed += value =>
        {
            track.Shown = value;
            _canvas?.BuildArc(track);
            _canvas?.InvalidateVisual();
        };
        track.Spring = spring;
        return spring;
    }

    /// <summary>Springs a ring to its target, or shows it at once before the first view and without full motion.</summary>
    private void Fill(ActivityRingsTrack track, TimeSpan delay)
    {
        var target = track.Ring.Progress;
        track.Target = target;
        if (!_revealed || !ArcMotion.IsFull(this))
        {
            if (track.Spring is { } running)
            {
                running.Jump(_revealed ? target : 0);
            }
            track.Shown = _revealed ? target : 0;
            _canvas?.BuildArc(track);
            _canvas?.InvalidateVisual();
            return;
        }
        var spec = SpringSpec.FromVisual(Math.Max(0.05, FillDuration.TotalSeconds), Math.Clamp(FillBounce, 0, 0.9));
        _ = Spring(track).AnimateTo(target, MotionSpec.Spring(spec, delay));
    }

    private void OnRingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Rebuild the tracks in order, keeping the ones whose ring is still there (and their springs).
        var kept = new Dictionary<ActivityRing, ActivityRingsTrack>(ReferenceEqualityComparer.Instance);
        foreach (var track in _tracks)
        {
            kept[track.Ring] = track;
        }
        _tracks.Clear();
        foreach (var ring in Rings)
        {
            if (kept.Remove(ring, out var track))
            {
                _tracks.Add(track);
                continue;
            }
            track = new ActivityRingsTrack(ring);
            ring.PropertyChanged += OnRingPropertyChanged;
            _tracks.Add(track);
        }
        foreach (var gone in kept.Values)
        {
            gone.Ring.PropertyChanged -= OnRingPropertyChanged;
            gone.Spring?.Dispose();
        }
        SyncRows();
        UpdateBrushes();
        foreach (var track in _tracks)
        {
            if (track.Spring is null || !track.Target.Equals(track.Ring.Progress))
            {
                Fill(track, TimeSpan.Zero);
            }
        }
        _canvas?.Relayout();
    }

    private void OnRingPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ActivityRing ring)
        {
            return;
        }
        var index = IndexOf(ring);
        if (index < 0)
        {
            return;
        }
        var track = _tracks[index];
        if (e.Property == ActivityRing.ValueProperty || e.Property == ActivityRing.GoalProperty)
        {
            var before = track.Target;
            Fill(track, TimeSpan.Zero);
            _rows[index].Update();
            if (_revealed && before < 1 && track.Target >= 1)
            {
                _live?.Announce($"{ring.Label} {GoalReachedText}");
                RaiseEvent(new ActivityRingGoalReachedEventArgs(GoalReachedEvent, ring));
            }
        }
        else if (e.Property == ActivityRing.BrushProperty)
        {
            UpdateBrushes();
        }
        else if (e.Property == ActivityRing.LabelProperty || e.Property == ActivityRing.UnitProperty)
        {
            _rows[index].Update();
        }
    }

    private int IndexOf(ActivityRing ring)
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            if (ReferenceEquals(_tracks[i].Ring, ring))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>One legend row per ring, reusing the rows already made.</summary>
    private void SyncRows()
    {
        while (_rows.Count > _tracks.Count)
        {
            var last = _rows[^1];
            _rows.RemoveAt(_rows.Count - 1);
            _legend?.Children.Remove(last);
        }
        while (_rows.Count < _tracks.Count)
        {
            var row = new ActivityRingsLegendRow();
            row.SetRingSize(GetValue(LegendRingSizeProperty), GetValue(LegendRingThicknessProperty));
            if (_revealed)
            {
                row.Reveal();
            }
            _rows.Add(row);
            _legend?.Children.Add(row);
        }
        for (var i = 0; i < _tracks.Count; i++)
        {
            _rows[i].Ring = _tracks[i].Ring;
        }
    }

    private void UpdateBrushes()
    {
        for (var i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            var brush = track.Ring.Brush ?? (i % 4) switch
            {
                0 => GetValue(Series1Property),
                1 => GetValue(Series2Property),
                2 => GetValue(Series3Property),
                _ => GetValue(Series4Property)
            };
            if (!ReferenceEquals(brush, track.Brush))
            {
                track.Brush = brush;
                track.ResetPens();
            }
            if (i < _rows.Count)
            {
                _rows[i].SetBrush(brush);
            }
        }
        _canvas?.InvalidateVisual();
    }
}

/// <summary>Names the rings and reads every goal's progress as help text.</summary>
internal sealed class ActivityRingsAutomationPeer : ControlAutomationPeer
{
    public ActivityRingsAutomationPeer(ActivityRings owner) : base(owner)
    {
    }

    private new ActivityRings Owner => (ActivityRings)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Owner.Label;

    protected override string? GetHelpTextCore() => Owner.Describe();

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
