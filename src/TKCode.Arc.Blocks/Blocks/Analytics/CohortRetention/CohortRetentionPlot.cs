using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the plot needs from its block besides the numbers: texts, formatters, the ramp and the flags.</summary>
internal sealed class CohortRetentionPlotOptions
{
    /// <summary>The default thresholds: 20 / 35 / 50 / 65 / 80 %.</summary>
    public static readonly double[] DefaultThresholds = [0.20, 0.35, 0.50, 0.65, 0.80];

    public IReadOnlyList<double> Thresholds { get; set; } = DefaultThresholds;

    public IReadOnlyList<double> RampMix { get; set; } = AnalyticsColors.CohortSteps;

    public bool ShowSizeColumn { get; set; } = true;

    public bool ShowAverageRow { get; set; } = true;

    public string AverageLabel { get; set; } = "Average";

    public string AxisTitle { get; set; } = "Months since start";

    public string AxisTitleShort { get; set; } = "Month";

    public string EmptyText { get; set; } = "No cohorts to show yet.";

    public Func<long, string> Count { get; set; } = v => AnalyticsFormat.Number(v);

    public Func<double, string> Percent { get; set; } = r => AnalyticsFormat.Percent(r * 100);

    public Func<CohortCell, CohortRetentionCardContent?>? Card { get; set; }

    /// <summary>The ramp weights for the threshold count: <see cref="RampMix"/> when it has one weight per step, else spread evenly.</summary>
    public double[] Weights()
    {
        var steps = Thresholds.Count + 1;
        if (RampMix.Count == steps)
        {
            return RampMix.ToArray();
        }
        return AnalyticsColors.EvenWeights(steps, AnalyticsColors.CohortSteps[0], AnalyticsColors.CohortSteps[^1]);
    }
}

/// <summary>
/// The block's plot: the heatmap triangle and the retention curves drawn by one control, and the morph between them
/// (cells shrink into points that travel to their rates, rows unfold into lines). It owns the per-cell motion (value
/// springs staggered along the diagonal, the stepped colour "rewash", dimming), the cursor visuals (box or ring,
/// crosshair, y tag), the hover card, hit testing and the keyboard; the <see cref="CohortRetention"/> that hosts it owns
/// the data, the selection state and the announcements.
/// </summary>
/// <remarks>
/// Code-driven motion: the morph is one master progress spring (k 72, c 15.3) that every cell reads a staggered slice of;
/// each cell's value has its own spring that starts after a diagonal delay and repaints its colour in 160ms steps as it
/// crosses thresholds; lines are rebuilt from the moving points. A ControlTheme transition can animate properties, not
/// 90 cells inside one drawing with per-cell delays and colour timelines, so one <see cref="LiveLoop"/> advances them all.
/// </remarks>
internal sealed partial class CohortRetentionPlot : Control
{
    private const double ColorSeconds = 0.16;
    private const double TextFlipSeconds = 0.08;
    private const double Dimmed = 0.28;
    private const double LineRest = 0.5;
    private const double LineDim = 0.16;

    #region Geometry properties (from the token)

    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CompactWidth), 600);
    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(LabelWidth), 136);
    public static readonly StyledProperty<double> CompactLabelWidthProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CompactLabelWidth), 56);
    public static readonly StyledProperty<double> SizeInsetProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(SizeInset), 14);
    public static readonly StyledProperty<double> RowPitchProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(RowPitch), 30);
    public static readonly StyledProperty<double> CompactRowPitchProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CompactRowPitch), 24);
    public static readonly StyledProperty<double> CellHeightProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CellHeight), 28);
    public static readonly StyledProperty<double> CompactCellHeightProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CompactCellHeight), 22);
    public static readonly StyledProperty<double> CellRadiusProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CellRadius), 5);
    public static readonly StyledProperty<double> CompactCellRadiusProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CompactCellRadius), 3);
    public static readonly StyledProperty<double> AverageGapProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(AverageGap), 10);
    public static readonly StyledProperty<double> CompactAverageGapProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CompactAverageGap), 8);
    public static readonly StyledProperty<double> NumberMinWidthProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(NumberMinWidth), 32);
    public static readonly StyledProperty<double> PointDiameterProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(PointDiameter), 5);
    public static readonly StyledProperty<double> AveragePointDiameterProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(AveragePointDiameter), 7);
    public static readonly StyledProperty<double> RingDiameterProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(RingDiameter), 10.5);
    public static readonly StyledProperty<double> AverageRingDiameterProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(AverageRingDiameter), 12.5);
    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CardWidth), 200);
    public static readonly StyledProperty<double> CardGapProperty =
        AvaloniaProperty.Register<CohortRetentionPlot, double>(nameof(CardGap), 9);

    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }
    public double CompactLabelWidth { get => GetValue(CompactLabelWidthProperty); set => SetValue(CompactLabelWidthProperty, value); }
    public double SizeInset { get => GetValue(SizeInsetProperty); set => SetValue(SizeInsetProperty, value); }
    public double RowPitch { get => GetValue(RowPitchProperty); set => SetValue(RowPitchProperty, value); }
    public double CompactRowPitch { get => GetValue(CompactRowPitchProperty); set => SetValue(CompactRowPitchProperty, value); }
    public double CellHeight { get => GetValue(CellHeightProperty); set => SetValue(CellHeightProperty, value); }
    public double CompactCellHeight { get => GetValue(CompactCellHeightProperty); set => SetValue(CompactCellHeightProperty, value); }
    public double CellRadius { get => GetValue(CellRadiusProperty); set => SetValue(CellRadiusProperty, value); }
    public double CompactCellRadius { get => GetValue(CompactCellRadiusProperty); set => SetValue(CompactCellRadiusProperty, value); }
    public double AverageGap { get => GetValue(AverageGapProperty); set => SetValue(AverageGapProperty, value); }
    public double CompactAverageGap { get => GetValue(CompactAverageGapProperty); set => SetValue(CompactAverageGapProperty, value); }
    public double NumberMinWidth { get => GetValue(NumberMinWidthProperty); set => SetValue(NumberMinWidthProperty, value); }
    public double PointDiameter { get => GetValue(PointDiameterProperty); set => SetValue(PointDiameterProperty, value); }
    public double AveragePointDiameter { get => GetValue(AveragePointDiameterProperty); set => SetValue(AveragePointDiameterProperty, value); }
    public double RingDiameter { get => GetValue(RingDiameterProperty); set => SetValue(RingDiameterProperty, value); }
    public double AverageRingDiameter { get => GetValue(AverageRingDiameterProperty); set => SetValue(AverageRingDiameterProperty, value); }
    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }
    public double CardGap { get => GetValue(CardGapProperty); set => SetValue(CardGapProperty, value); }

    #endregion

    private static readonly string[] s_percents = Enumerable.Range(0, 101).Select(i => i.ToString(AnalyticsFormat.DefaultCulture)).ToArray();
    private static readonly string[] s_gridLabels = ["100%", "75%", "50%", "25%", "0%"];

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly CohortRetentionCard _card = new();
    private readonly LiveLoop _loop;

    private CohortRetentionModel _model = CohortRetentionModel.Empty;
    private CohortRetentionPlotOptions _options = new();
    private CohortRetentionView _view = CohortRetentionView.Heatmap;
    private CohortCell? _cursor;
    private bool _compact;

    // Cells, row-major, the average row last.
    private CohortCell[] _cells = [];
    private int[] _keys = [];
    private int[][] _index = [];
    private int[] _averageIndex = [];
    private int _count;
    private bool _hasAverage;
    private AnalyticsSpringChannel[] _value = [];
    private double[] _pending = [];
    private double[] _delay = [];
    private bool[] _waiting = [];
    private int[] _step = [];
    private Color[] _fromColor = [];
    private double[] _colorT = [];
    private bool[] _textFrom = [];
    private bool[] _textTo = [];
    private AnalyticsTween[] _dim = [];
    private ImmutableSolidColorBrush?[] _fillBrush = [];
    private AnalyticsTween[] _lineAlpha = [];
    private StreamGeometry?[] _lines = [];
    private StreamGeometry? _averageLine;
    private bool _linesDirty = true;

    private AnalyticsSpringChannel _morph = new(0, 0.0005, 0.005);
    private AnalyticsTween _pointMix = new(0);
    private AnalyticsTween _pointAlpha = new(0);

    private Color[] _ramp = [];
    private double[] _weights = [];
    private int _paletteVersion = -1;
    private Color _accentForeground;
    private string[] _monthTexts = [];
    private string[] _sizeTexts = [];
    private string _totalText = string.Empty;
    private string? _tagText;
    private double _tagValue = double.NaN;
    private bool _pointerCursor;

    static CohortRetentionPlot()
    {
        FocusableProperty.OverrideDefaultValue<CohortRetentionPlot>(true);
        AffectsMeasure<CohortRetentionPlot>(CompactWidthProperty, RowPitchProperty, CompactRowPitchProperty, AverageGapProperty,
            CompactAverageGapProperty);
        AffectsRender<CohortRetentionPlot>(LabelWidthProperty, CompactLabelWidthProperty, SizeInsetProperty, CellHeightProperty,
            CompactCellHeightProperty, CellRadiusProperty, CompactCellRadiusProperty, NumberMinWidthProperty, PointDiameterProperty,
            AveragePointDiameterProperty, RingDiameterProperty, AverageRingDiameterProperty, CardWidthProperty, CardGapProperty);
    }

    public CohortRetentionPlot()
    {
        // Function as well as decoration: the loop also finishes fades under reduced motion.
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
        Cursor = new Avalonia.Input.Cursor(StandardCursorType.Cross);
    }

    /// <summary>Raised when the person moves the cursor (pointer or keys); the block decides and calls <see cref="SetCursor"/>.</summary>
    public event Action<CohortCell?, bool>? CursorRequested;

    /// <summary>Raised on a click or Enter / Space on a cell.</summary>
    public event Action<CohortCell>? CellInvoked;

    /// <summary>Raised when the plot switches between its wide and compact geometry.</summary>
    public event Action<bool>? CompactChanged;

    /// <summary>The plot uses its compact geometry (narrower than <see cref="CompactWidth"/>).</summary>
    public bool IsCompact => _compact;

    /// <summary>The numbers drawn.</summary>
    public CohortRetentionModel Model => _model;

    /// <summary>The view the plot shows or is heading to.</summary>
    public CohortRetentionView View => _view;

    /// <summary>The cursor drawn.</summary>
    public CohortCell? CurrentCursor => _cursor;

    /// <summary>The morph's master progress (0 heatmap, 1 curves; tests).</summary>
    internal double MorphProgress => _morph.Value;

    /// <summary>True while anything animates (tests).</summary>
    internal bool IsMoving => _loop.IsActive;

    /// <summary>The hover card (tests).</summary>
    internal CohortRetentionCard Card => _card;

    /// <summary>The current animated rate of a cell (tests); NaN for no cell.</summary>
    internal double DisplayedRate(CohortCell cell) => IndexOf(cell) is { } i ? _value[i].Value : double.NaN;

    /// <summary>The current heat step of a cell (tests).</summary>
    internal int DisplayedStep(CohortCell cell) => IndexOf(cell) is { } i ? _step[i] : -1;

    /// <summary>The current dimming of a cell (tests).</summary>
    internal double DisplayedOpacity(CohortCell cell) => IndexOf(cell) is { } i ? _dim[i].Value : double.NaN;

    /// <summary>The geometry at the current width.</summary>
    internal CohortRetentionLayout CurrentLayout => LayoutFor(Bounds.Width);

    /// <summary>Where a cell's point is drawn now (centre).</summary>
    internal Point PointOf(CohortCell cell)
    {
        var layout = CurrentLayout;
        return new Point(layout.Cx(cell.MonthIndex), PointY(cell, layout));
    }

    #region Data, view, cursor

    /// <summary>Sets the options (texts, thresholds, flags); geometry and colours rebuild.</summary>
    public void SetOptions(CohortRetentionPlotOptions options)
    {
        var averageChanged = options.ShowAverageRow != _options.ShowAverageRow;
        _options = options;
        _weights = options.Weights();
        _paletteVersion = -1;
        if (averageChanged)
        {
            Rebuild();
        }
        else
        {
            for (var i = 0; i < _count; i++)
            {
                _step[i] = CohortRetentionModel.Step(_value[i].Value, _options.Thresholds);
                _colorT[i] = ColorSeconds;
                _textFrom[i] = _textTo[i] = IsLightText(_step[i]);
            }
        }
        UpdateTexts();
        RefreshCard();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Shows new numbers. With the same cells as before and <paramref name="animate"/>, every value springs to its new
    /// rate after a diagonal delay (9ms per key) and repaints its colour as it crosses thresholds (Full motion only);
    /// otherwise everything lands at once.
    /// </summary>
    public void SetData(CohortRetentionModel model, bool animate)
    {
        var old = _model;
        _model = model;
        var hasAverage = _options.ShowAverageRow && model.AverageLength > 0;
        var full = ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        if (!animate || !full || _count == 0 || !old.HasSameShape(model) || hasAverage != _hasAverage)
        {
            Rebuild();
        }
        else
        {
            for (var i = 0; i < _count; i++)
            {
                _pending[i] = Rate(_cells[i]);
                _delay[i] = _keys[i] * CohortRetentionMorph.ValueStaggerSeconds;
                _waiting[i] = true;
            }
            _loop.Start();
        }
        UpdateTexts();
        _linesDirty = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Switches between heatmap and curves: the morph at Full motion, at once otherwise.</summary>
    public void SetView(CohortRetentionView view, bool animate)
    {
        _view = view;
        var target = view == CohortRetentionView.Curves ? 1.0 : 0.0;
        if (animate && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree())
        {
            _morph.SpringTo(target, AnalyticsSprings.CohortMorph);
            _pointMix.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
            _pointAlpha.Start(target, ArcMotionPresets.Standard, AnalyticsEasings.Standard);
            _loop.Start();
        }
        else
        {
            _morph.Jump(target);
            _pointMix.Jump(target);
            _pointAlpha.Jump(target);
        }
        _linesDirty = true;
        UpdateDims(animate);
        RefreshCard();
        InvalidateVisual();
    }

    /// <summary>Draws the cursor (null clears it): box or ring, dimming, card.</summary>
    public void SetCursor(CohortCell? cell)
    {
        if (cell is { } c && !_model.Contains(c))
        {
            cell = null;
        }
        _cursor = cell;
        if (cell is null)
        {
            _pointerCursor = false;
        }
        UpdateDims(animate: true);
        RefreshCard();
        InvalidateVisual();
    }

    /// <summary>Recomputes the card's content and position (after the data or the texts changed).</summary>
    public void RefreshCard()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_cursor is not { } cell || _options.Card?.Invoke(cell) is not { } content || Bounds.Width <= 0)
        {
            _card.Hide(level);
            StartLoopIfNeeded();
            return;
        }
        _card.Width = CardWidth;
        _card.Content = content;
        _card.Show(CardPositionFor(cell, CurrentLayout), this.IsAttachedToVisualTree() ? level : ArcMotionLevel.Off);
        StartLoopIfNeeded();
    }

    private void StartLoopIfNeeded()
    {
        if (_card.IsMoving)
        {
            _loop.Start();
        }
    }

    private Point CardPositionFor(CohortCell cell, CohortRetentionLayout layout)
    {
        var size = _card.Size;
        if (_view == CohortRetentionView.Curves)
        {
            var point = new Point(layout.Cx(cell.MonthIndex), PointY(cell, layout));
            var r = (cell.IsAverage ? AverageRingDiameter : RingDiameter) / 2;
            var box = new Rect(point.X - r, point.Y - r, r * 2, r * 2);
            return CohortRetentionLayout.CardPosition(box, size, Bounds.Width, layout.Height, Math.Max(0, CardGap - 1.75));
        }
        var x0 = layout.X0(cell.MonthIndex);
        var x1 = layout.X1(cell.MonthIndex);
        var top = layout.CellTop(cell.Row, cell.IsAverage) - 1;
        var cellBox = new Rect(x0 - 1, top, x1 - x0 + 2, layout.CellHeight + 2);
        return CohortRetentionLayout.CardPosition(cellBox, size, Bounds.Width, layout.Height, CardGap);
    }

    private void Rebuild()
    {
        var model = _model;
        _hasAverage = _options.ShowAverageRow && model.AverageLength > 0;
        var count = 0;
        for (var r = 0; r < model.Rows; r++)
        {
            count += model.Length(r);
        }
        if (_hasAverage)
        {
            count += model.AverageLength;
        }
        _count = count;
        _cells = new CohortCell[count];
        _keys = new int[count];
        _index = new int[model.Rows][];
        _averageIndex = new int[_hasAverage ? model.AverageLength : 0];
        _value = new AnalyticsSpringChannel[count];
        _pending = new double[count];
        _delay = new double[count];
        _waiting = new bool[count];
        _step = new int[count];
        _fromColor = new Color[count];
        _colorT = new double[count];
        _textFrom = new bool[count];
        _textTo = new bool[count];
        _dim = new AnalyticsTween[count];
        _fillBrush = new ImmutableSolidColorBrush?[count];
        _lineAlpha = new AnalyticsTween[model.Rows];
        _lines = new StreamGeometry?[model.Rows];
        var i = 0;
        for (var r = 0; r < model.Rows; r++)
        {
            _lineAlpha[r] = new AnalyticsTween(LineRest);
            var length = model.Length(r);
            _index[r] = new int[length];
            for (var m = 0; m < length; m++)
            {
                _index[r][m] = i;
                Init(i++, new CohortCell(r, m));
            }
        }
        for (var m = 0; m < _averageIndex.Length; m++)
        {
            _averageIndex[m] = i;
            Init(i++, CohortCell.Average(m));
        }
        _linesDirty = true;
        UpdateDims(animate: false);
    }

    private void Init(int i, CohortCell cell)
    {
        var rate = Rate(cell);
        _cells[i] = cell;
        _keys[i] = CohortRetentionMorph.Key(cell, _model.Rows);
        _value[i] = new AnalyticsSpringChannel(rate, 0.0005, 0.005);
        _pending[i] = rate;
        _step[i] = CohortRetentionModel.Step(rate, _options.Thresholds);
        _colorT[i] = ColorSeconds;
        _textFrom[i] = _textTo[i] = IsLightText(_step[i]);
        _dim[i] = new AnalyticsTween(1);
    }

    private double Rate(CohortCell cell)
    {
        var rate = _model.Rate(cell);
        return double.IsNaN(rate) ? 0 : rate;
    }

    private int? IndexOf(CohortCell cell)
    {
        if (cell.IsAverage)
        {
            return cell.MonthIndex >= 0 && cell.MonthIndex < _averageIndex.Length ? _averageIndex[cell.MonthIndex] : null;
        }
        if (cell.Row < 0 || cell.Row >= _index.Length || cell.MonthIndex < 0 || cell.MonthIndex >= _index[cell.Row].Length)
        {
            return null;
        }
        return _index[cell.Row][cell.MonthIndex];
    }

    private bool IsLightText(int step) => step >= 0 && step < _weights.Length && _weights[step] >= 0.5;

    private void UpdateTexts()
    {
        if (_monthTexts.Length != _model.Months)
        {
            _monthTexts = Enumerable.Range(1, _model.Months).Select(m => m.ToString(AnalyticsFormat.DefaultCulture)).ToArray();
        }
        _sizeTexts = _model.Sizes.Select(s => _options.Count(s)).ToArray();
        _totalText = _options.Count(_model.TotalSize);
        _tagValue = double.NaN;
    }

    /// <summary>Sets every cell's and line's dimming target for the current cursor and view.</summary>
    private void UpdateDims(bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var tween = animate && level == ArcMotionLevel.Full && this.IsAttachedToVisualTree();
        var curves = _view == CohortRetentionView.Curves;
        var cursor = _cursor;
        for (var i = 0; i < _count; i++)
        {
            var cell = _cells[i];
            var target = 1.0;
            if (cursor is { } c)
            {
                if (curves)
                {
                    if (!c.IsAverage && !cell.IsAverage && cell.Row != c.Row)
                    {
                        target = Dimmed;
                    }
                }
                else
                {
                    var sameRow = c.IsAverage ? cell.IsAverage : !cell.IsAverage && cell.Row == c.Row;
                    if (!sameRow && cell.MonthIndex != c.MonthIndex)
                    {
                        target = Dimmed;
                    }
                }
            }
            Retarget(ref _dim[i], target, tween);
        }
        for (var r = 0; r < _lineAlpha.Length; r++)
        {
            var target = LineRest;
            if (curves && cursor is { IsAverage: false } c)
            {
                target = c.Row == r ? 1 : LineDim;
            }
            Retarget(ref _lineAlpha[r], target, tween);
        }
        if (tween)
        {
            _loop.Start();
        }
    }

    private static void Retarget(ref AnalyticsTween tween, double target, bool animate)
    {
        if (Math.Abs(tween.Target - target) < 1e-9 && (tween.IsRunning || Math.Abs(tween.Value - target) < 1e-9))
        {
            return;
        }
        if (animate)
        {
            tween.Start(target, ArcMotionPresets.Fast, AnalyticsEasings.Standard);
        }
        else
        {
            tween.Jump(target);
        }
    }

    #endregion

    #region Frame loop

    private bool Advance(double dt)
    {
        var full = ArcMotion.IsFull(this);
        var moving = false;
        var geometry = false;
        if (_morph.IsMoving)
        {
            if (full)
            {
                _morph.Step(dt);
            }
            else
            {
                _morph.Jump(_morph.Target);
            }
            moving |= _morph.IsMoving;
            geometry = true;
        }
        moving |= _pointMix.Advance(dt);
        moving |= _pointAlpha.Advance(dt);
        EnsurePalette();
        for (var i = 0; i < _count; i++)
        {
            if (_waiting[i])
            {
                _delay[i] -= dt;
                if (_delay[i] <= 0 || !full)
                {
                    _waiting[i] = false;
                    if (full)
                    {
                        _value[i].SpringTo(_pending[i], AnalyticsSprings.Smooth);
                    }
                    else
                    {
                        _value[i].Jump(_pending[i]);
                    }
                    geometry = true;
                }
                else
                {
                    moving = true;
                }
            }
            if (_value[i].IsMoving)
            {
                if (full)
                {
                    _value[i].Step(dt);
                }
                else
                {
                    _value[i].Jump(_value[i].Target);
                }
                geometry = true;
                moving |= _value[i].IsMoving;
            }
            var step = CohortRetentionModel.Step(_value[i].Value, _options.Thresholds);
            if (step != _step[i])
            {
                if (full)
                {
                    // The stepped "rewash": each crossed threshold starts a new 160ms colour transition from the colour
                    // on screen, and the number's colour flips half way through it.
                    _fromColor[i] = HeatColor(i);
                    _textFrom[i] = TextIsLight(i);
                    _colorT[i] = 0;
                }
                else
                {
                    _colorT[i] = ColorSeconds;
                    _textFrom[i] = IsLightText(step);
                }
                _step[i] = step;
                _textTo[i] = IsLightText(step);
            }
            if (_colorT[i] < ColorSeconds)
            {
                _colorT[i] += full ? dt : ColorSeconds;
                moving |= _colorT[i] < ColorSeconds;
            }
            moving |= _dim[i].Advance(dt);
        }
        for (var r = 0; r < _lineAlpha.Length; r++)
        {
            moving |= _lineAlpha[r].Advance(dt);
        }
        if (geometry)
        {
            _linesDirty = true;
            if (_cursor is { } cell && _card.IsShown && _view == CohortRetentionView.Curves && Bounds.Width > 0)
            {
                _card.Show(CardPositionFor(cell, CurrentLayout), full ? ArcMotionLevel.Full : ArcMotionLevel.Reduced);
            }
        }
        moving |= _card.Advance(dt);
        InvalidateVisual();
        return moving;
    }

    #endregion

    #region Colours

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        InvalidateVisual();
    }

    private void EnsurePalette()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        if (_paletteVersion >= 0 && _palette.IsReady)
        {
            return;
        }
        _palette.Refresh(this);
        _accentForeground = ArcTokens.GetColor(this, ArcDesignTokenKind.AccentForeground);
        if (_weights.Length == 0)
        {
            _weights = _options.Weights();
        }
        _ramp = AnalyticsColors.Ramp(_palette.Accent, _palette.Surface, _weights);
        _paletteVersion = _palette.Version;
        Array.Clear(_fillBrush);
        for (var i = 0; i < _count; i++)
        {
            _colorT[i] = ColorSeconds;
        }
    }

    private Color HeatColor(int i)
    {
        if (_ramp.Length == 0)
        {
            return default;
        }
        var target = _ramp[Math.Clamp(_step[i], 0, _ramp.Length - 1)];
        var t = _colorT[i] / ColorSeconds;
        return t >= 1 ? target : AnalyticsColors.Lerp(_fromColor[i], target, AnalyticsEasings.Standard.Ease(Math.Clamp(t, 0, 1)));
    }

    private bool TextIsLight(int i) => _colorT[i] >= TextFlipSeconds ? _textTo[i] : _textFrom[i];

    private IBrush FillBrush(int i, Color color)
    {
        var brush = _fillBrush[i];
        if (brush is null || brush.Color != color)
        {
            // Only while a colour moves: a new immutable brush per changed cell (no shared cache to grow).
            brush = new ImmutableSolidColorBrush(color);
            _fillBrush[i] = brush;
        }
        return brush;
    }

    #endregion

    #region Layout and input

    private CohortRetentionLayout LayoutFor(double width)
    {
        var compact = width < CompactWidth;
        var hasAverage = _options.ShowAverageRow && _model.AverageLength > 0;
        return new CohortRetentionLayout(width, _model.Rows, _model.Months, hasAverage, compact,
            compact ? CompactLabelWidth : LabelWidth,
            compact ? CompactRowPitch : RowPitch,
            compact ? CompactCellHeight : CellHeight,
            compact ? CompactCellRadius : CellRadius,
            compact ? CompactAverageGap : AverageGap);
    }

    private double PointY(CohortCell cell, CohortRetentionLayout layout)
    {
        if (IndexOf(cell) is not { } i)
        {
            return 0;
        }
        var yp = CohortRetentionMorph.Y(_morph.Value, _keys[i]);
        var rowY = layout.RowCenter(cell.Row, cell.IsAverage);
        return rowY + (layout.YOf(_value[i].Value) - rowY) * yp;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 760 : availableSize.Width;
        if (_model.IsEmpty)
        {
            return new Size(0, 160);
        }
        return new Size(0, LayoutFor(width).Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var compact = finalSize.Width < CompactWidth;
        if (compact != _compact)
        {
            _compact = compact;
            CompactChanged?.Invoke(compact);
        }
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _linesDirty = true;
        if (_cursor is not null)
        {
            RefreshCard();
        }
    }

    private CohortCell? HitTest(Point point)
    {
        if (_model.IsEmpty || Bounds.Width <= 0)
        {
            return null;
        }
        var layout = CurrentLayout;
        return _view == CohortRetentionView.Curves
            ? layout.HitCurves(point, _model, c => PointY(c, layout))
            : layout.HitHeatmap(point, _model);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var cell = HitTest(e.GetPosition(this));
        if (cell != _cursor)
        {
            _pointerCursor = cell is not null;
            CursorRequested?.Invoke(cell, false);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pointerCursor && _cursor is not null)
        {
            _pointerCursor = false;
            CursorRequested?.Invoke(null, false);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var cell = HitTest(e.GetPosition(this));
        if (cell is { } c)
        {
            if (cell != _cursor)
            {
                _pointerCursor = true;
                CursorRequested?.Invoke(cell, false);
            }
            CellInvoked?.Invoke(c);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Measured: a keyboard (Tab) focus puts the cursor on the first cell at once; focus from a click or code does not.
        if (e.NavigationMethod == NavigationMethod.Tab && _cursor is null && _model.First(_hasAverage) is { } first)
        {
            CursorRequested?.Invoke(first, true);
        }
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (_cursor is not null)
            {
                CursorRequested?.Invoke(null, true);
                e.Handled = true;
            }
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            if (_cursor is { } invoked)
            {
                CellInvoked?.Invoke(invoked);
                e.Handled = true;
            }
            return;
        }
        if (_model.Move(_cursor, e.Key, _hasAverage) is { } next)
        {
            _pointerCursor = false;
            if (next != _cursor)
            {
                CursorRequested?.Invoke(next, true);
            }
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CohortRetentionPlotPeer(this);

    #endregion
}

/// <summary>The plot's peer: a focusable group named by the block's summary of the chart.</summary>
internal sealed class CohortRetentionPlotPeer(CohortRetentionPlot owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "chart";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
