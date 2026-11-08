using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A ranking that changes over time: bars slide past each other as their values climb, and a timeline plays, pauses or
/// scrubs the periods. Every bar shows its name and value, so hue is never the only cue. Space or Enter plays or
/// pauses, ← / → step a period, ↑ / ↓ read the bars by rank, Home / End go to the first and last period, Escape clears
/// the reading. Under reduced motion nothing plays by itself and periods change without interpolation.
/// </summary>
[TemplatePart(StagePart, typeof(RaceBarStage))]
[TemplatePart(TimelinePart, typeof(RaceBarTimeline))]
[TemplatePart(PlayPart, typeof(Button))]
[TemplatePart(PlayGlyphPart, typeof(ArcGlyph))]
[TemplatePart(PeriodPart, typeof(ArcContentSwap))]
[PseudoClasses(NarrowPseudoClass, PlayingPseudoClass)]
public class RaceBarChart : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string TimelinePart = "PART_Timeline";
    internal const string PlayPart = "PART_Play";
    internal const string PlayGlyphPart = "PART_PlayGlyph";
    internal const string PeriodPart = "PART_Period";
    internal const string FirstPeriodPart = "PART_FirstPeriod";
    internal const string LastPeriodPart = "PART_LastPeriod";
    private const string NarrowPseudoClass = ":narrow";
    private const string PlayingPseudoClass = ":playing";

    /// <summary>The competitors, one per bar. Assign a list or add items inline in XAML; changes to an observable list are followed.</summary>
    public static readonly StyledProperty<IList<RaceBarItem>?> ItemsProperty =
        AvaloniaProperty.Register<RaceBarChart, IList<RaceBarItem>?>(nameof(Items));

    /// <summary>One frame per period, in order: the period's name and the values by item id. A missing id counts as zero.</summary>
    public static readonly StyledProperty<IList<RaceBarFrame>?> FramesProperty =
        AvaloniaProperty.Register<RaceBarChart, IList<RaceBarFrame>?>(nameof(Frames));

    /// <summary>Optional categories: the first four take series hues and a legend appears. Without groups every bar uses the first hue.</summary>
    public static readonly StyledProperty<IList<RaceBarGroup>?> GroupsProperty =
        AvaloniaProperty.Register<RaceBarChart, IList<RaceBarGroup>?>(nameof(Groups));

    /// <summary>What is being ranked, such as "Monthly riders by city"; names the chart for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<RaceBarChart, string?>(nameof(Label));

    /// <summary>Formats bar values and tooltips (default: compact numbers through <c>ArcFormatting.Compact</c>, such as 1.2K).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<RaceBarChart, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Formats the axis ticks (default: <see cref="ValueFormatter"/>, or compact numbers).</summary>
    public static readonly StyledProperty<Func<double, string>?> TickFormatterProperty =
        AvaloniaProperty.Register<RaceBarChart, Func<double, string>?>(nameof(TickFormatter));

    /// <summary>How many bars are shown (default 8, at least 1); items ranked below slide out.</summary>
    public static readonly StyledProperty<int> BarCountProperty =
        AvaloniaProperty.Register<RaceBarChart, int>(nameof(BarCount), 8, coerce: static (_, value) => Math.Max(1, value));

    /// <summary>The period shown (two-way). Changing it glides through the values between periods.</summary>
    public static readonly StyledProperty<int> FrameIndexProperty =
        AvaloniaProperty.Register<RaceBarChart, int>(nameof(FrameIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the timeline is playing (two-way). Starting it on the last period replays from the first.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<RaceBarChart, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Starts playing the first time the chart scrolls into view (default true). Ignored under reduced motion.</summary>
    public static readonly StyledProperty<bool> IsAutoPlayProperty =
        AvaloniaProperty.Register<RaceBarChart, bool>(nameof(IsAutoPlay), true);

    /// <summary>How long one period takes while playing (default 1400 ms).</summary>
    public static readonly StyledProperty<TimeSpan> FrameDurationProperty =
        AvaloniaProperty.Register<RaceBarChart, TimeSpan>(nameof(FrameDuration), TimeSpan.FromMilliseconds(1400));

    /// <summary>Starts over from the first period after the last one (default false).</summary>
    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<RaceBarChart, bool>(nameof(IsLooping));

    /// <summary>Id of the item whose name and value are emphasised.</summary>
    public static readonly StyledProperty<string?> HighlightIdProperty =
        AvaloniaProperty.Register<RaceBarChart, string?>(nameof(HighlightId));

    /// <summary>Message shown without items or frames.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<RaceBarChart, string>(nameof(EmptyLabel), "No data yet");

    internal static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(BarHeight), 28);

    internal static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(BarGap), 6);

    internal static readonly StyledProperty<double> BarRadiusProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(BarRadius), 6);

    internal static readonly StyledProperty<double> NameColumnWidthProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(NameColumnWidth), 132);

    internal static readonly StyledProperty<double> AxisHeightProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(AxisHeight), 26);

    internal static readonly StyledProperty<double> TickSpacingProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(TickSpacing), 84);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<RaceBarChart, double>(nameof(NarrowWidth), 400);

    private readonly RaceBarEngine _engine = new();
    private readonly ChartsBItemsWatcher _itemsWatcher;
    private readonly ChartsBItemsWatcher _framesWatcher;
    private readonly ChartsBItemsWatcher _groupsWatcher;
    private readonly LiveLoop _loop;
    private ViewportVisibility? _visibility;
    private bool _modelDirty = true;
    private bool _attached;
    private bool _autoPlayPending = true;
    private bool _seen;
    private bool _advancing;
    private bool _keepPosition;
    private double _clock;
    private double _hold;
    private int _readingItem = -1;
    private RaceBarStage? _stage;
    private RaceBarTimeline? _timeline;
    private Button? _play;
    private ArcGlyph? _playGlyph;
    private ArcContentSwap? _period;
    private TextBlock? _firstPeriod;
    private TextBlock? _lastPeriod;

    /// <summary>Creates a race bar chart.</summary>
    public RaceBarChart()
    {
        _itemsWatcher = new ChartsBItemsWatcher(MarkDirty);
        _framesWatcher = new ChartsBItemsWatcher(MarkDirty);
        _groupsWatcher = new ChartsBItemsWatcher(MarkDirty);
        SetCurrentValue(ItemsProperty, new AvaloniaList<RaceBarItem>());
        SetCurrentValue(FramesProperty, new AvaloniaList<RaceBarFrame>());
        SetCurrentValue(GroupsProperty, new AvaloniaList<RaceBarGroup>());
        // Off is the lowest level on purpose: playback is a function of the control, not decoration. Without full motion
        // the loop only keeps time and the periods change in place.
        _loop = new LiveLoop(this, dt => Tick(Math.Min(dt, 0.1))) { MinimumLevel = ArcMotionLevel.Off };
        Palette = new ChartsBPalette(this, OnPaletteChanged);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    public IList<RaceBarItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <inheritdoc cref="FramesProperty"/>
    public IList<RaceBarFrame>? Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
    }

    /// <inheritdoc cref="GroupsProperty"/>
    public IList<RaceBarGroup>? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <inheritdoc cref="TickFormatterProperty"/>
    public Func<double, string>? TickFormatter
    {
        get => GetValue(TickFormatterProperty);
        set => SetValue(TickFormatterProperty, value);
    }

    /// <inheritdoc cref="BarCountProperty"/>
    public int BarCount
    {
        get => GetValue(BarCountProperty);
        set => SetValue(BarCountProperty, value);
    }

    /// <inheritdoc cref="FrameIndexProperty"/>
    public int FrameIndex
    {
        get => GetValue(FrameIndexProperty);
        set => SetValue(FrameIndexProperty, value);
    }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <inheritdoc cref="IsAutoPlayProperty"/>
    public bool IsAutoPlay
    {
        get => GetValue(IsAutoPlayProperty);
        set => SetValue(IsAutoPlayProperty, value);
    }

    /// <inheritdoc cref="FrameDurationProperty"/>
    public TimeSpan FrameDuration
    {
        get => GetValue(FrameDurationProperty);
        set => SetValue(FrameDurationProperty, value);
    }

    /// <inheritdoc cref="IsLoopingProperty"/>
    public bool IsLooping
    {
        get => GetValue(IsLoopingProperty);
        set => SetValue(IsLoopingProperty, value);
    }

    /// <inheritdoc cref="HighlightIdProperty"/>
    public string? HighlightId
    {
        get => GetValue(HighlightIdProperty);
        set => SetValue(HighlightIdProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when the period shown changes, whether by playback, a step, a scrub or <see cref="FrameIndex"/>.</summary>
    public event EventHandler<RaceBarFrameChangedEventArgs>? FrameChanged;

    /// <summary>Raised when playback starts or stops.</summary>
    public event EventHandler<RaceBarPlayingChangedEventArgs>? PlayingChanged;

    internal double BarHeight
    {
        get => GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    internal double BarGap
    {
        get => GetValue(BarGapProperty);
        set => SetValue(BarGapProperty, value);
    }

    internal double BarRadius
    {
        get => GetValue(BarRadiusProperty);
        set => SetValue(BarRadiusProperty, value);
    }

    internal double NameColumnWidth
    {
        get => GetValue(NameColumnWidthProperty);
        set => SetValue(NameColumnWidthProperty, value);
    }

    internal double AxisHeight
    {
        get => GetValue(AxisHeightProperty);
        set => SetValue(AxisHeightProperty, value);
    }

    internal double TickSpacing
    {
        get => GetValue(TickSpacingProperty);
        set => SetValue(TickSpacingProperty, value);
    }

    internal double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    /// <summary>Plays from the current period (from the first when on the last).</summary>
    public void Play() => SetCurrentValue(IsPlayingProperty, true);

    /// <summary>Pauses on the next period boundary.</summary>
    public void Pause() => SetCurrentValue(IsPlayingProperty, false);

    /// <summary>Goes back to the first period and plays.</summary>
    public void Replay()
    {
        SeekTo(0);
        Play();
    }

    internal RaceBarEngine Engine
    {
        get
        {
            EnsureModel();
            return _engine;
        }
    }

    internal ChartsBPalette Palette { get; }

    internal RaceBarStage? Stage => _stage;

    internal RaceBarTimeline? Timeline => _timeline;

    internal Button? PlayButton => _play;

    internal ArcContentSwap? PeriodSwap => _period;

    /// <summary>The item being read, or -1.</summary>
    internal int ReadingItem => _readingItem;

    /// <summary>Id of the item being read.</summary>
    internal string? ReadingId => _readingItem >= 0 && _readingItem < _engine.ItemCount ? _engine.Items[_readingItem].Id : null;

    /// <summary>Index into <see cref="RaceBarEngine.Items"/> of <see cref="HighlightId"/>, or -1.</summary>
    internal int HighlightItem
    {
        get
        {
            var id = HighlightId;
            if (id is null)
            {
                return -1;
            }
            var items = _engine.Items;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    private bool Animates => ArcMotion.IsFull(this) && IsLoaded;

    private int CurrentIndex => _engine.FrameCount == 0 ? 0 : Math.Clamp(FrameIndex, 0, _engine.FrameCount - 1);

    private bool IsReady => _engine.FrameCount > 0 && _engine.ItemCount > 0;

    /// <summary>The name of the period shown.</summary>
    internal string CurrentPeriod => _engine.FrameCount == 0 ? string.Empty : _engine.Frames[CurrentIndex].Period;

    /// <summary>How many bars are on screen.</summary>
    internal int ShownBars => Math.Min(BarCount, _engine.ItemCount);

    internal string FormatValue(double value) => ValueFormatter is { } formatter ? formatter(value) : ArcFormatting.Compact(value);

    internal string FormatTick(double value)
    {
        if (TickFormatter is { } tick)
        {
            return tick(value);
        }
        return FormatValue(value);
    }

    // ---- data ---------------------------------------------------------------------------------------------------

    private void MarkDirty()
    {
        _modelDirty = true;
        if (_attached)
        {
            EnsureModel();
        }
    }

    private void EnsureModel()
    {
        // Inline additions before attaching are not reported, so a detached chart always reads the lists again.
        if (!_modelDirty && _attached)
        {
            return;
        }
        var changed = _modelDirty;
        _modelDirty = false;
        _engine.Load(Items ?? [], Frames ?? [], Groups ?? []);
        if (_readingItem >= _engine.ItemCount)
        {
            _readingItem = -1;
        }
        if (_engine.FrameCount > 0 && FrameIndex != CurrentIndex)
        {
            _advancing = true;
            SetCurrentValue(FrameIndexProperty, CurrentIndex);
            _advancing = false;
        }
        if (changed)
        {
            AfterDataChanged();
        }
    }

    private void AfterDataChanged()
    {
        // Settle on the frame in view: rows that survive a data change slide from where they are.
        if (Animates && _engine.HasSurvivingRows)
        {
            _engine.Refresh(BarCount);
            _engine.GlideTarget = CurrentIndex;
            _engine.IsGliding = !IsPlaying;
            _engine.Unsettle();
            _loop.Start();
        }
        else
        {
            _engine.Position = CurrentIndex;
            PlaceNow();
        }
        if (IsPlaying && _engine.FrameCount == 1)
        {
            SetCurrentValue(IsPlayingProperty, false);
        }
        TryAutoPlay();
        if (IsPlaying && _engine.FrameCount > 1)
        {
            _loop.Start();
        }
        _stage?.OnDataChanged();
        SyncChrome();
    }

    private int TickCountFor(double plotWidth) => Math.Clamp((int)Math.Floor(plotWidth / Math.Max(24, TickSpacing)), 2, 8);

    private int TickCount => TickCountFor(_stage?.PlotWidth ?? 400);

    private void PlaceNow()
    {
        _engine.Place(BarCount, _readingItem, TickCount);
        Repaint();
    }

    /// <summary>The plot changed width: the axis picks a tick count for it.</summary>
    internal void OnPlotResized()
    {
        if (Animates)
        {
            _engine.Unsettle();
            _loop.Start();
        }
        else
        {
            PlaceNow();
        }
    }

    private void Repaint()
    {
        _stage?.InvalidateVisual();
        _timeline?.InvalidateVisual();
    }

    private void OnPaletteChanged()
    {
        _stage?.OnPaletteChanged();
        _timeline?.InvalidateVisual();
    }

    // ---- life cycle --------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_play is not null)
        {
            _play.Click -= OnPlayClick;
        }
        _stage = e.NameScope.Find<RaceBarStage>(StagePart);
        _timeline = e.NameScope.Find<RaceBarTimeline>(TimelinePart);
        _play = e.NameScope.Find<Button>(PlayPart);
        _playGlyph = e.NameScope.Find<ArcGlyph>(PlayGlyphPart);
        _period = e.NameScope.Find<ArcContentSwap>(PeriodPart);
        _firstPeriod = e.NameScope.Find<TextBlock>(FirstPeriodPart);
        _lastPeriod = e.NameScope.Find<TextBlock>(LastPeriodPart);
        if (_play is not null)
        {
            _play.Click += OnPlayClick;
        }
        _stage?.Attach(this);
        _timeline?.Attach(this);
        _modelDirty = true;
        EnsureModel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _itemsWatcher.SetSource(Items);
        _framesWatcher.SetSource(Frames);
        _groupsWatcher.SetSource(Groups);
        _itemsWatcher.Attach();
        _framesWatcher.Attach();
        _groupsWatcher.Attach();
        _modelDirty = true;
        EnsureModel();
        if (_autoPlayPending && _visibility is null)
        {
            _visibility = new ViewportVisibility(this, 0.3, once: true);
            _visibility.VisibilityChanged += OnVisibilityChanged;
        }
        if (IsPlaying)
        {
            _loop.Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _itemsWatcher.Detach();
        _framesWatcher.Detach();
        _groupsWatcher.Detach();
        ReleaseVisibility();
    }

    private void ReleaseVisibility()
    {
        if (_visibility is not null)
        {
            _visibility.VisibilityChanged -= OnVisibilityChanged;
            _visibility.Dispose();
            _visibility = null;
        }
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (!visible)
        {
            return;
        }
        _seen = true;
        ReleaseVisibility();
        TryAutoPlay();
    }

    private void TryAutoPlay()
    {
        if (!_seen || !_autoPlayPending || _engine.FrameCount < 2 || _engine.ItemCount == 0)
        {
            return;
        }
        _autoPlayPending = false;
        if (IsAutoPlay && ArcMotion.IsFull(this))
        {
            SetCurrentValue(IsPlayingProperty, true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            _itemsWatcher.SetSource(Items);
            MarkDirty();
        }
        else if (change.Property == FramesProperty)
        {
            _framesWatcher.SetSource(Frames);
            MarkDirty();
        }
        else if (change.Property == GroupsProperty)
        {
            _groupsWatcher.SetSource(Groups);
            MarkDirty();
        }
        else if (change.Property == FrameIndexProperty)
        {
            OnFrameIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (change.Property == IsPlayingProperty)
        {
            OnIsPlayingChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == BarCountProperty)
        {
            EnsureModel();
            if (_readingItem >= 0 && _engine.Rank[_readingItem] >= BarCount)
            {
                _readingItem = -1;
                _stage?.SyncTooltip(true);
            }
            if (Animates)
            {
                _engine.Unsettle();
                _loop.Start();
            }
            else
            {
                PlaceNow();
            }
            _stage?.InvalidateMeasure();
        }
        else if (change.Property == HighlightIdProperty || change.Property == ValueFormatterProperty ||
                 change.Property == TickFormatterProperty || change.Property == EmptyLabelProperty)
        {
            _stage?.OnTextChanged();
        }
        else if (change.Property == LabelProperty)
        {
            SyncChrome();
        }
        else if (change.Property == BarHeightProperty || change.Property == BarGapProperty || change.Property == AxisHeightProperty ||
                 change.Property == NameColumnWidthProperty || change.Property == NarrowWidthProperty)
        {
            _stage?.InvalidateMeasure();
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, Bounds.Width > 0 && Bounds.Width < NarrowWidth);
        }
    }

    // ---- timeline ----------------------------------------------------------------------------------------------------

    private void OnFrameIndexChanged(int old, int value)
    {
        EnsureModel();
        if (_engine.FrameCount > 0)
        {
            var clamped = Math.Clamp(value, 0, _engine.FrameCount - 1);
            if (clamped != value)
            {
                SetCurrentValue(FrameIndexProperty, clamped);
                return;
            }
        }
        else
        {
            return;
        }
        if (!_advancing)
        {
            _clock = 0;
            _hold = 0;
            if (Animates)
            {
                // A step, a seek or an outside change glides; during play, the race carries on from there.
                _engine.GlideTarget = value;
                _engine.IsGliding = true;
                _engine.Unsettle();
                _loop.Start();
            }
            else
            {
                _engine.Position = value;
                PlaceNow();
            }
        }
        var leader = _engine.LeaderOf(value);
        SyncPeriod(old, value);
        FrameChanged?.Invoke(this, new RaceBarFrameChangedEventArgs(old, value, _engine.Frames[value].Period, leader >= 0 ? _engine.Items[leader] : null));
        SyncChrome();
    }

    private void OnIsPlayingChanged(bool playing)
    {
        EnsureModel();
        PseudoClasses.Set(PlayingPseudoClass, playing);
        if (playing)
        {
            _autoPlayPending = false;
            if (_engine.FrameCount == 1)
            {
                SetCurrentValue(IsPlayingProperty, false);
                return;
            }
            _clock = 0;
            _hold = 0;
            if (_engine.FrameCount > 1)
            {
                if (CurrentIndex >= _engine.FrameCount - 1)
                {
                    // Replay: pressing play on the last period starts over.
                    SetCurrentValue(FrameIndexProperty, 0);
                }
                else
                {
                    _engine.IsGliding = false;
                    _engine.PositionVelocity = 0;
                }
                _loop.Start();
            }
        }
        else
        {
            _hold = 0;
            if (!_keepPosition && Animates && !_engine.IsGliding)
            {
                // Mid-period: finish the move in progress rather than stopping between two periods.
                var next = (int)Math.Ceiling(_engine.Position - 1e-6);
                if (next != CurrentIndex)
                {
                    SetCurrentValue(FrameIndexProperty, next);
                }
                else if (Math.Abs(_engine.Position - next) > 1e-6)
                {
                    _engine.GlideTarget = next;
                    _engine.IsGliding = true;
                }
            }
            if (Animates)
            {
                _engine.Unsettle();
                _loop.Start();
            }
        }
        PlayingChanged?.Invoke(this, new RaceBarPlayingChangedEventArgs(playing, CurrentIndex));
        SyncChrome();
    }

    /// <summary>
    /// One frame of the single loop. It keeps playback's time, advances every spring and asks for a repaint; it stops
    /// (returns false) once the chart is paused and settled, and <see cref="LiveLoop"/> pauses it off screen.
    /// </summary>
    internal bool Tick(double dt)
    {
        EnsureModel();
        if (!IsReady)
        {
            return false;
        }
        var full = Animates;
        if (IsPlaying && _engine.FrameCount > 1)
        {
            if (full)
            {
                AdvancePlayhead(dt);
            }
            else
            {
                AdvanceInSteps(dt);
            }
        }
        if (full)
        {
            _engine.Update(dt, BarCount, _readingItem, TickCount);
            if (_readingItem >= 0 && _engine.Rank[_readingItem] >= BarCount)
            {
                _readingItem = -1;
                SyncChrome();
            }
            _stage?.SyncTooltip(false);
            Repaint();
        }
        return (IsPlaying && _engine.FrameCount > 1) || (full && !_engine.IsSettled);
    }

    private double FrameSeconds => Math.Max(0.05, FrameDuration.TotalSeconds);

    private void AdvancePlayhead(double dt)
    {
        if (_hold > 0)
        {
            _hold -= dt;
            if (_hold <= 0)
            {
                // The pause on the last period is over: glide back, and the race restarts when the glide lands.
                _hold = 0;
                SetCurrentValue(FrameIndexProperty, 0);
            }
            return;
        }
        if (_engine.IsGliding)
        {
            return;
        }
        var last = _engine.FrameCount - 1;
        var position = _engine.Position + dt / FrameSeconds;
        if (position >= last)
        {
            _engine.Position = last;
            SetFrameFromPlayback(last);
            if (IsLooping)
            {
                _hold = Math.Min(0.8, FrameSeconds * 0.6);
            }
            else
            {
                _keepPosition = true;
                SetCurrentValue(IsPlayingProperty, false);
                _keepPosition = false;
            }
            return;
        }
        _engine.Position = position;
        SetFrameFromPlayback((int)Math.Floor(position));
    }

    // Reduced or Off: the loop only keeps time, and each period is placed without interpolation.
    private void AdvanceInSteps(double dt)
    {
        _clock += dt / FrameSeconds;
        if (_clock < 1)
        {
            return;
        }
        _clock -= Math.Floor(_clock);
        var clock = _clock;
        var last = _engine.FrameCount - 1;
        var index = CurrentIndex;
        if (index >= last)
        {
            SetCurrentValue(FrameIndexProperty, 0);
        }
        else
        {
            SetCurrentValue(FrameIndexProperty, index + 1);
            if (index + 1 >= last && !IsLooping)
            {
                SetCurrentValue(IsPlayingProperty, false);
            }
        }
        _clock = clock;
    }

    private void SetFrameFromPlayback(int index)
    {
        if (index == CurrentIndex)
        {
            return;
        }
        _advancing = true;
        SetCurrentValue(FrameIndexProperty, index);
        _advancing = false;
    }

    /// <summary>Stops playback and moves to a period; the change glides under full motion.</summary>
    internal void SeekTo(int index)
    {
        EnsureModel();
        if (_engine.FrameCount == 0)
        {
            return;
        }
        StopForSeek();
        var target = Math.Clamp(index, 0, _engine.FrameCount - 1);
        if (target != FrameIndex)
        {
            SetCurrentValue(FrameIndexProperty, target);
        }
        else if (Animates)
        {
            _engine.GlideTarget = target;
            _engine.IsGliding = true;
            _engine.Unsettle();
            _loop.Start();
        }
    }

    private void StopForSeek()
    {
        if (IsPlaying)
        {
            _keepPosition = true;
            SetCurrentValue(IsPlayingProperty, false);
            _keepPosition = false;
        }
    }

    /// <summary>A finger or the mouse on the timeline: the race follows continuously, the period snaps to the nearest one.</summary>
    internal void ScrubTo(double fraction)
    {
        EnsureModel();
        if (_engine.FrameCount == 0)
        {
            return;
        }
        StopForSeek();
        var position = Math.Clamp(fraction, 0, 1) * (_engine.FrameCount - 1);
        var nearest = (int)Math.Round(position);
        if (Animates)
        {
            SetFrameFromPlayback(nearest);
            _engine.GlideTarget = position;
            _engine.IsGliding = true;
            _engine.Unsettle();
            _loop.Start();
        }
        else if (nearest != FrameIndex)
        {
            SetCurrentValue(FrameIndexProperty, nearest);
        }
    }

    /// <summary>The scrub ended: land on the period the label shows.</summary>
    internal void EndScrub()
    {
        if (Animates)
        {
            _engine.GlideTarget = CurrentIndex;
            _engine.IsGliding = true;
            _engine.Unsettle();
            _loop.Start();
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        Toggle();
        e.Handled = true;
    }

    private void Toggle()
    {
        if (_engine.FrameCount > 1)
        {
            SetCurrentValue(IsPlayingProperty, !IsPlaying);
        }
    }

    // ---- keyboard ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Keys from any part of the chart arrive here: Space / Enter play or pause, ← / → step a period, ↑ / ↓ read the bars
    /// by rank, Home / End first and last period, Escape clears the reading.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        EnsureModel();
        if (e.Handled || !IsReady || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                Toggle();
                e.Handled = true;
                break;
            case Key.Left:
                SeekTo(CurrentIndex - 1);
                e.Handled = true;
                break;
            case Key.Right:
                SeekTo(CurrentIndex + 1);
                e.Handled = true;
                break;
            case Key.Home:
                SeekTo(0);
                e.Handled = true;
                break;
            case Key.End:
                SeekTo(_engine.FrameCount - 1);
                e.Handled = true;
                break;
            case Key.Down:
                MoveReading(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveReading(-1);
                e.Handled = true;
                break;
            case Key.Escape when _readingItem >= 0:
                SetReading(-1);
                e.Handled = true;
                break;
        }
    }

    private void MoveReading(int direction)
    {
        var shown = ShownBars;
        if (shown == 0)
        {
            return;
        }
        var rank = _readingItem < 0 ? 0 : Math.Clamp(_engine.Rank[_readingItem] + direction, 0, shown - 1);
        SetReading(_engine.Order[rank]);
    }

    /// <summary>Starts reading a bar (or stops, with -1): the others step back and the tooltip shows it.</summary>
    internal void SetReading(int item)
    {
        if (item == _readingItem)
        {
            return;
        }
        _readingItem = item;
        if (Animates)
        {
            _engine.Unsettle();
            _loop.Start();
        }
        else
        {
            PlaceNow();
        }
        _stage?.SyncTooltip(true);
        SyncChrome();
    }

    // ---- chrome and accessibility -------------------------------------------------------------------------------------

    private void SyncPeriod(int old, int value)
    {
        if (_period is not null)
        {
            _period.Direction = value >= old ? 1 : -1;
            _period.Content = _engine.Frames[value].Period;
        }
    }

    private void SyncChrome()
    {
        var ready = IsReady;
        var last = _engine.FrameCount - 1;
        if (_period is not null)
        {
            var period = _engine.FrameCount == 0 ? null : CurrentPeriod;
            if (!Equals(_period.Content, period))
            {
                _period.Content = period;
            }
        }
        if (_firstPeriod is not null)
        {
            _firstPeriod.Text = _engine.FrameCount == 0 ? null : _engine.Frames[0].Period;
        }
        if (_lastPeriod is not null)
        {
            _lastPeriod.Text = _engine.FrameCount == 0 ? null : _engine.Frames[last].Period;
        }
        if (_play is not null)
        {
            _play.IsEnabled = ready && _engine.FrameCount > 1;
            var atEnd = _engine.FrameCount > 1 && CurrentIndex >= last;
            var name = IsPlaying ? "Pause" : atEnd ? "Replay from the start" : "Play";
            AutomationProperties.SetName(_play, name);
            if (_playGlyph is not null)
            {
                _playGlyph.Data = IsPlaying ? ArcIcons.Pause : atEnd ? ArcIcons.RotateCcw : ArcIcons.Play;
            }
        }
        if (_timeline is not null)
        {
            _timeline.IsEnabled = ready && _engine.FrameCount > 1;
            _timeline.Report();
        }
        if (_stage is not null)
        {
            _stage.Focusable = ready;
            // While playing, the periods would flood a screen reader; the state is announced again on pause.
            if (!IsPlaying)
            {
                _stage.Announce();
            }
        }
    }

    /// <summary>What the slider says for a period, such as "2019, period 3 of 12".</summary>
    internal string TimelineValueText => _engine.FrameCount == 0 ? string.Empty : $"{CurrentPeriod}, period {CurrentIndex + 1} of {_engine.FrameCount}";

    internal string TimelineName => string.IsNullOrEmpty(Label) ? "Timeline" : $"{Label} timeline";

    /// <summary>The reading of a bar: name, rank and value.</summary>
    internal string? ReadingText
    {
        get
        {
            if (_readingItem < 0 || _readingItem >= _engine.ItemCount)
            {
                return null;
            }
            return $"{_engine.Items[_readingItem].Label}, rank {_engine.Rank[_readingItem] + 1} of {_engine.ItemCount}, {FormatValue(_engine.Shown[_readingItem])}";
        }
    }

    /// <summary>The period and its leader: what stepping announces.</summary>
    internal string PeriodText
    {
        get
        {
            if (!IsReady)
            {
                return EmptyLabel;
            }
            var frame = CurrentIndex;
            var leader = _engine.LeaderOf(frame);
            return $"{_engine.Frames[frame].Period}. Leader: {_engine.Items[leader].Label}, {FormatValue(_engine.ValueAt(leader, frame))}";
        }
    }

    /// <summary>Accessible help of the bars: the live reading, else the period and its leader.</summary>
    internal string LiveText => ReadingText ?? PeriodText;

    /// <summary>Accessible name with the key instructions.</summary>
    internal string AccessibleName =>
        $"{(string.IsNullOrEmpty(Label) ? "Race bar chart" : Label)}. Space plays or pauses, left and right arrows step a period, up and down arrows read the bars.";
}
