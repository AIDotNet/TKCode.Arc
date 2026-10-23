using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawn middle of a <see cref="SpeculativeDecoding"/> card: the two lanes (labels or legend, the draft chips, the
/// target cells with the sweep, the verdicts and the strike), the rule, the answer line (and the target-only line) with
/// every token underlined in its model's color, and the tokens flying from the lanes into the answer. Everything here
/// is a function of the story time it is given, so pausing freezes it, Speed scales it and a restart or a jump redraws it
/// exactly; only the label crossfade, the restart fade and the compare line's height run on wall time (set by the card).
/// The answer is laid out once in full, so the line never reflows. One control, so a flight crosses from the lanes into
/// the answer in one coordinate space. Measured geometry (CSS px) is kept here as constants.
/// </summary>
internal sealed partial class SpeculativeDecodingStage : Control
{
    // Lanes: a 120 px label column 16 from the track (wide); rows of 32 with 8 between; columns 6 apart, wrapping 8 apart.
    internal const double LabelColumn = 120;
    internal const double LabelGap = 16;
    internal const double Row = 32;
    internal const double RowGap = 8;
    internal const double LaneBlock = Row + RowGap + Row;
    internal const double ColumnGap = 6;
    internal const double WrapGap = 8;
    internal const double ColumnMin = 40;
    internal const double ChipInset = 10;
    internal const double ChipRadius = 10;
    internal const double SlotWidth = 44;

    // Narrow legend: items 16 apart (4 between rows), dot and words 6 apart, 12 above the track.
    internal const double LegendItemGap = 16;
    internal const double LegendRowGap = 4;
    internal const double LegendInner = 6;
    internal const double LegendGap = 12;
    internal const double LegendRow = 16.8;

    // Outputs: the rule 20 (16 narrow) under the lanes, 20 (16) above the lines; lines of 25.2, 16 between two lines.
    internal const double Section = 20;
    internal const double NarrowSection = 16;
    internal const double TextLine = 25.2;
    internal const double LineGap = 16;
    internal const double NameLine = 18.2;
    internal const double MetaLine = 15.6;
    internal const double NarrowLabelGap = 4;

    // Text: 14 px body, 12 px details; underline 1.5 thick, 5 under the baseline.
    internal const double Body = 14;
    internal const double Small = 12;
    internal const double UnderlineThickness = 1.5;
    internal const double UnderlineOffset = 5;

    // Motion (seconds): column in 140 ms Enter from 4 px left, out 120 ms; verdict 160 ms; strike 180 ms; token in 140 ms;
    // underline 240 ms after 300 ms; summary 240 ms after 120 ms; flight on a critically damped spring of ω 16.3.
    internal const double ColumnIn = 0.14;
    internal const double ColumnOut = 0.12;
    internal const double VerdictIn = 0.16;
    internal const double StrikeIn = 0.18;
    internal const double TokenIn = 0.14;
    internal const double UnderlineDelay = 0.3;
    internal const double UnderlineIn = 0.24;
    internal const double SummaryDelay = 0.12;
    internal const double SummaryIn = 0.24;
    internal const double FlightOmega = 16.3;
    internal const double TextSettle = 0.3;

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<SpeculativeDecodingStage>();

    public static readonly StyledProperty<IBrush?> DraftInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(DraftInk));
    public static readonly StyledProperty<IBrush?> TargetInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(TargetInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(SecondaryInk));
    public static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(MutedInk));
    public static readonly StyledProperty<IBrush?> GhostInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(GhostInk));
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> SurfaceMutedInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(SurfaceMutedInk));
    public static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(BorderInk));
    public static readonly StyledProperty<IBrush?> BorderStrongInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(BorderStrongInk));
    public static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(SuccessInk));
    public static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<SpeculativeDecodingStage, IBrush?>(nameof(DangerInk));

    private readonly SpeculativeDecodingTextCache _text = new();
    private DecodeTimeline? _timeline;
    private SpeculativeDecodingLabels _labels = new();
    private double _time;
    private double _wall;
    private bool _showsSlots = true;
    private int _slotCount = 4;
    private bool _isNarrow;
    private bool _isTravel = true;
    private double _compareOpen;
    private bool _compareLabel;
    private double _labelSwapWall = double.NegativeInfinity;
    private double _fadeFrom = -1;
    private double _fadeWall;
    private string _draftName = "Draft model";
    private string _draftDetail = "Small, fast";
    private string _targetName = "Target model";
    private string _targetDetail = "Large";
    private double _measuredHeight = -1;

    static SpeculativeDecodingStage()
    {
        AffectsRender<SpeculativeDecodingStage>(DraftInkProperty, TargetInkProperty, ForegroundInkProperty, SecondaryInkProperty,
            MutedInkProperty, GhostInkProperty, SurfaceInkProperty, SurfaceMutedInkProperty, BorderInkProperty, BorderStrongInkProperty,
            SuccessInkProperty, DangerInkProperty);
        AffectsMeasure<SpeculativeDecodingStage>(FontFamilyProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SpeculativeDecodingStage>(false);
        FocusableProperty.OverrideDefaultValue<SpeculativeDecodingStage>(false);
    }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public IBrush? DraftInk { get => GetValue(DraftInkProperty); set => SetValue(DraftInkProperty, value); }
    public IBrush? TargetInk { get => GetValue(TargetInkProperty); set => SetValue(TargetInkProperty, value); }
    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }
    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    public IBrush? GhostInk { get => GetValue(GhostInkProperty); set => SetValue(GhostInkProperty, value); }
    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }
    public IBrush? SurfaceMutedInk { get => GetValue(SurfaceMutedInkProperty); set => SetValue(SurfaceMutedInkProperty, value); }
    public IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    public IBrush? BorderStrongInk { get => GetValue(BorderStrongInkProperty); set => SetValue(BorderStrongInkProperty, value); }
    public IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    public IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }

    /// <summary>The run to draw (null draws the empty skeleton).</summary>
    public DecodeTimeline? Timeline
    {
        get => _timeline;
        set
        {
            if (!ReferenceEquals(_timeline, value))
            {
                _timeline = value;
                _fadeFrom = -1;
                _layoutDirty = true;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Story time in seconds.</summary>
    public double Time
    {
        get => _time;
        set
        {
            _time = value;
            Refresh();
        }
    }

    /// <summary>Wall time in seconds (the card's frame clock), for the label crossfade and the restart fade.</summary>
    public double Wall
    {
        get => _wall;
        set
        {
            _wall = value;
            InvalidateVisual();
        }
    }

    /// <summary>Shows the ready skeleton (empty slots) instead of the run.</summary>
    public bool ShowsSlots
    {
        get => _showsSlots;
        set
        {
            if (_showsSlots != value)
            {
                _showsSlots = value;
                Refresh();
            }
        }
    }

    /// <summary>The number of empty slots in the ready skeleton (the draft length).</summary>
    public int SlotCount
    {
        get => _slotCount;
        set
        {
            if (_slotCount != value)
            {
                _slotCount = Math.Max(1, value);
                _layoutDirty = true;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>The narrow layout (a legend above the track, line labels above their text).</summary>
    public bool IsNarrow
    {
        get => _isNarrow;
        set
        {
            if (_isNarrow != value)
            {
                _isNarrow = value;
                _layoutDirty = true;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }
    }

    /// <summary>Full motion: flights, slides, the shimmer and the strike draw; otherwise changes appear in place.</summary>
    public bool IsTravel
    {
        get => _isTravel;
        set
        {
            if (_isTravel != value)
            {
                _isTravel = value;
                Refresh();
            }
        }
    }

    /// <summary>How far the target-only line is open, 0 to 1 (a wall-time spring of the card).</summary>
    public double CompareOpen
    {
        get => _compareOpen;
        set
        {
            _compareOpen = Math.Clamp(value, 0, 1);
            Refresh();
        }
    }

    /// <summary>The answer line is named "Speculative" (while comparing) rather than "Output"; changes crossfade.</summary>
    public bool IsCompareLabel
    {
        get => _compareLabel;
        set => SetCompareLabel(value, animate: true);
    }

    /// <summary>Names the answer line, crossfading from the old name when <paramref name="animate"/>.</summary>
    public void SetCompareLabel(bool value, bool animate)
    {
        if (_compareLabel == value)
        {
            return;
        }
        _compareLabel = value;
        _labelSwapWall = animate ? _wall : double.NegativeInfinity;
        InvalidateVisual();
    }

    /// <summary>The texts.</summary>
    public SpeculativeDecodingLabels Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            _summaryKey = -1;
            _layoutDirty = true;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Sets the lane names and details.</summary>
    public void SetLanes(string? draftName, string? draftDetail, string? targetName, string? targetDetail)
    {
        _draftName = draftName ?? string.Empty;
        _draftDetail = draftDetail ?? string.Empty;
        _targetName = targetName ?? string.Empty;
        _targetDetail = targetDetail ?? string.Empty;
        _layoutDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Fades out the tokens a run had written by <paramref name="storyTime"/> (a restart), from wall time
    /// <paramref name="wall"/>, while the new run starts over.
    /// </summary>
    public void BeginRestartFade(double storyTime, double wall)
    {
        _fadeFrom = storyTime;
        _fadeWall = wall;
        InvalidateVisual();
    }

    /// <summary>The text cache (tests check that a steady scene stops building layouts).</summary>
    internal SpeculativeDecodingTextCache TextCache => _text;

    /// <summary>Lines the answer takes (tests).</summary>
    internal int AnswerLines => _lines;

    /// <summary>The answer line of token <paramref name="q"/> (tests).</summary>
    internal int TokenLine(int q) => _tokLine[q];

    /// <summary>The track's height at the current time (72 for one row of columns).</summary>
    internal double TrackHeight => TrackHeightAt(_time);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty)
        {
            _layoutDirty = true;
        }
        else if (change.Property.Name.EndsWith("Ink", StringComparison.Ordinal))
        {
            _paletteDirty = true;
        }
    }

    // A new time or state: redraw, and measure again only while the height moves (the lanes' spring, the compare line).
    private void Refresh()
    {
        if (Math.Abs(ContentHeight() - _measuredHeight) > 0.01)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }
}
