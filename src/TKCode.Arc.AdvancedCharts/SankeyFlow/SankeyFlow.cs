using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A flow diagram: stages in columns, ribbons between them with a thickness proportional to their value, and particles
/// drifting along the ribbons. Hovering or focusing a node or ribbon traces its share through the whole journey in the
/// accent color while everything else dims. Below 420 px wide the columns become rows, read from top to bottom.
/// </summary>
public partial class SankeyFlow : Control, IChartsBAccessible, Avalonia.Rendering.ICustomHitTest
{
    /// <summary>The whole box takes the pointer, not only the painted marks.</summary>
    bool Avalonia.Rendering.ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <summary>The stages' nodes: add them inline in XAML, or assign any list. Link ends with no node here get a node named after their id.</summary>
    public static readonly StyledProperty<IList<SankeyFlowNode>?> NodesProperty =
        AvaloniaProperty.Register<SankeyFlow, IList<SankeyFlowNode>?>(nameof(Nodes));

    /// <summary>The flows between nodes: add them inline in XAML, or assign any list. Repeated pairs add up; self loops and values that are not positive are ignored.</summary>
    public static readonly StyledProperty<IList<SankeyFlowLink>?> LinksProperty =
        AvaloniaProperty.Register<SankeyFlow, IList<SankeyFlowLink>?>(nameof(Links));

    /// <summary>What flows, such as "Trial journey, March"; names the chart for assistive technology.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SankeyFlow, string?>(nameof(Label));

    /// <summary>Stage names drawn above each column, from the first column on.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ColumnsProperty =
        AvaloniaProperty.Register<SankeyFlow, IReadOnlyList<string>?>(nameof(Columns));

    /// <summary>Text after each value, such as "k" or " users" (default empty).</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<SankeyFlow, string>(nameof(Unit), string.Empty);

    /// <summary>Names the whole in share readouts: "42% of the total" (default "the total").</summary>
    public static readonly StyledProperty<string> TotalLabelProperty =
        AvaloniaProperty.Register<SankeyFlow, string>(nameof(TotalLabel), "the total");

    /// <summary>Formats values (default: grouped, one decimal at most, then <see cref="Unit"/>).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<SankeyFlow, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Height of the plot in pixels (default 340). The upright layout grows past it when its rows need more room.</summary>
    public static readonly StyledProperty<double> PlotHeightProperty =
        AvaloniaProperty.Register<SankeyFlow, double>(nameof(PlotHeight), 340);

    /// <summary>Whether particles drift along the ribbons (two-way, default true). Reduced motion never draws them.</summary>
    public static readonly StyledProperty<bool> IsParticlesEnabledProperty =
        AvaloniaProperty.Register<SankeyFlow, bool>(nameof(IsParticlesEnabled), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the switch that turns particles on and off (default true). Reduced motion removes it.</summary>
    public static readonly StyledProperty<bool> IsParticleToggleVisibleProperty =
        AvaloniaProperty.Register<SankeyFlow, bool>(nameof(IsParticleToggleVisible), true);

    /// <summary>Text of the particle switch (default "Particles").</summary>
    public static readonly StyledProperty<string> ParticleToggleLabelProperty =
        AvaloniaProperty.Register<SankeyFlow, string>(nameof(ParticleToggleLabel), "Particles");

    /// <summary>Message shown while there are no flows (default "No flows yet").</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<SankeyFlow, string>(nameof(EmptyLabel), "No flows yet");

    internal static readonly StyledProperty<double> NodeThicknessProperty =
        AvaloniaProperty.Register<SankeyFlow, double>(nameof(NodeThickness), 12);

    internal static readonly StyledProperty<double> NodeGapProperty =
        AvaloniaProperty.Register<SankeyFlow, double>(nameof(NodeGap), 14);

    internal static readonly StyledProperty<double> NodeRadiusProperty =
        AvaloniaProperty.Register<SankeyFlow, double>(nameof(NodeRadius), 3);

    internal static readonly StyledProperty<double> ParticleRadiusProperty =
        AvaloniaProperty.Register<SankeyFlow, double>(nameof(ParticleRadius), 1.75);

    internal static readonly StyledProperty<double> FigureGapProperty =
        AvaloniaProperty.Register<SankeyFlow, double>(nameof(FigureGap), 10);

    /// <summary>Below this width the chart runs from top to bottom.</summary>
    internal const double UprightBelow = 420;

    /// <summary>The most particles one ribbon carries.</summary>
    internal const int MaxParticlesPerRibbon = 34;

    private const double UprightRowSpacing = 112;
    private const double UprightBeneath = 40;
    private const double Inset = 3;

    private readonly ChartsBPalette _palette;
    private readonly ChartsBItemsWatcher _nodesWatcher;
    private readonly ChartsBItemsWatcher _linksWatcher;
    private readonly ChartsBReveal _reveal;
    private readonly ChartsBTooltip _tooltip;
    private readonly ChartsBOverlay _overlay;
    private readonly MotionValue<double> _progress;
    private readonly MotionValue<double> _dim;
    private readonly MotionValue<double> _pour;
    private SankeyFlowModel _model = SankeyFlowModel.Build(null, null);
    private SankeyFlowPlacement? _placement;
    private readonly List<NodeItem> _nodeItems = new();
    private readonly List<LinkItem> _linkItems = new();
    private bool _modelDirty = true;
    private bool _placementDirty = true;
    private bool _dataChanged = true;
    private Size _placedSize;
    private bool _upright;
    private bool _played;
    private bool _morphing;
    private bool _entrance;
    private int _generation;
    private int _stamp;
    private double _maxStagger;
    private double _staggerStep;
    private Switch? _toggle;
    private ChartsBOverlay? _particleLayer;
    private LiveLoop? _particleLoop;
    private double _clock;
    private bool _drawingParticles;

    /// <summary>A node as drawn: where it animates from and to, and the pose of the current frame.</summary>
    private sealed class NodeItem
    {
        public required string Id;
        public int Column;
        public SankeyNodePose From;
        public SankeyNodePose To;
        public SankeyNodePose Drawn;
    }

    /// <summary>A ribbon as drawn, with its cached geometry and particle plan.</summary>
    private sealed class LinkItem
    {
        public required string Key;
        public int SourceColumn;
        public int TargetColumn;
        public SankeyLinkPose From;
        public SankeyLinkPose To;
        public SankeyLinkPose Drawn;
        public Avalonia.Media.StreamGeometry? Fill;
        public int FillStamp = -1;
        public Avalonia.Media.StreamGeometry? Band;
        public int BandStamp = -1;
        public int Particles;
        public double Speed;
        public double Seed;
    }

    /// <summary>Creates a flow chart.</summary>
    public SankeyFlow()
    {
        Focusable = false;
        ClipToBounds = true;
        _palette = new ChartsBPalette(this, () =>
        {
            _textDirty = true;
            Repaint();
            _particleLayer?.InvalidateVisual();
        });
        _reveal = new ChartsBReveal(this, 0.3, OnRevealed);
        _overlay = new ChartsBOverlay(PaintOverlay) { ZIndex = 2 };
        VisualChildren.Add(_overlay);
        _tooltip = new ChartsBTooltip(this, ArcMotionPresets.Snappy, _overlay.InvalidateVisual) { MinWidth = 170 };
        _progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Changed += OnProgress;
        _dim = Value(0);
        _pour = Value(1);
        _nodesWatcher = new ChartsBItemsWatcher(MarkDirty);
        _linksWatcher = new ChartsBItemsWatcher(MarkDirty);
        var inlineNodes = new AvaloniaList<SankeyFlowNode>();
        var inlineLinks = new AvaloniaList<SankeyFlowLink>();
        // While attached the watchers report inline changes themselves.
        inlineNodes.CollectionChanged += (_, _) =>
        {
            if (!_nodesWatcher.IsSubscribed)
            {
                MarkDirty();
            }
        };
        inlineLinks.CollectionChanged += (_, _) =>
        {
            if (!_linksWatcher.IsSubscribed)
            {
                MarkDirty();
            }
        };
        SetCurrentValue(NodesProperty, inlineNodes);
        SetCurrentValue(LinksProperty, inlineLinks);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        ResourcesChanged += (_, _) => SyncMotion();
    }

    static SankeyFlow()
    {
        AffectsMeasure<SankeyFlow>(PlotHeightProperty, NodeThicknessProperty, NodeGapProperty, FigureGapProperty, IsParticleToggleVisibleProperty);
        AffectsRender<SankeyFlow>(NodeRadiusProperty);
    }

    private MotionValue<double> Value(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ =>
        {
            Repaint();
            _particleLayer?.InvalidateVisual();
        };
        return value;
    }

    /// <inheritdoc cref="NodesProperty"/>
    public IList<SankeyFlowNode>? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    /// <inheritdoc cref="LinksProperty"/>
    public IList<SankeyFlowLink>? Links
    {
        get => GetValue(LinksProperty);
        set => SetValue(LinksProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public IReadOnlyList<string>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="UnitProperty"/>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <inheritdoc cref="TotalLabelProperty"/>
    public string TotalLabel
    {
        get => GetValue(TotalLabelProperty);
        set => SetValue(TotalLabelProperty, value);
    }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <inheritdoc cref="PlotHeightProperty"/>
    public double PlotHeight
    {
        get => GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    /// <inheritdoc cref="IsParticlesEnabledProperty"/>
    public bool IsParticlesEnabled
    {
        get => GetValue(IsParticlesEnabledProperty);
        set => SetValue(IsParticlesEnabledProperty, value);
    }

    /// <inheritdoc cref="IsParticleToggleVisibleProperty"/>
    public bool IsParticleToggleVisible
    {
        get => GetValue(IsParticleToggleVisibleProperty);
        set => SetValue(IsParticleToggleVisibleProperty, value);
    }

    /// <inheritdoc cref="ParticleToggleLabelProperty"/>
    public string ParticleToggleLabel
    {
        get => GetValue(ParticleToggleLabelProperty);
        set => SetValue(ParticleToggleLabelProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when <see cref="IsParticlesEnabled"/> changes.</summary>
    public event EventHandler<SankeyFlowParticlesChangedEventArgs>? ParticlesChanged;

    internal double NodeThickness
    {
        get => GetValue(NodeThicknessProperty);
        set => SetValue(NodeThicknessProperty, value);
    }

    internal double NodeGap
    {
        get => GetValue(NodeGapProperty);
        set => SetValue(NodeGapProperty, value);
    }

    internal double NodeRadius
    {
        get => GetValue(NodeRadiusProperty);
        set => SetValue(NodeRadiusProperty, value);
    }

    internal double ParticleRadius
    {
        get => GetValue(ParticleRadiusProperty);
        set => SetValue(ParticleRadiusProperty, value);
    }

    internal double FigureGap
    {
        get => GetValue(FigureGapProperty);
        set => SetValue(FigureGapProperty, value);
    }

    internal SankeyFlowModel Model
    {
        get
        {
            EnsureModel();
            return _model;
        }
    }

    internal SankeyFlowPlacement? Placement => _placement;

    internal bool IsUpright => _upright;

    internal bool IsMorphing => _morphing;

    internal Switch? ParticleToggle => _toggle;

    internal bool IsToggleShown => _toggle is { IsVisible: true };

    internal bool IsParticleLoopRunning => _particleLoop?.IsRunning == true;

    internal bool IsDrawingParticles => IsParticlesEnabled && _reveal.IsRevealed && !_model.IsEmpty && ArcMotion.IsFull(this);

    private bool IsEmpty => _model.IsEmpty;

    private bool ToggleWanted => IsParticleToggleVisible && !_model.IsEmpty && ArcMotion.IsFull(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NodesProperty)
        {
            _nodesWatcher.SetSource(change.GetNewValue<IList<SankeyFlowNode>?>() as IEnumerable);
        }
        else if (change.Property == LinksProperty)
        {
            _linksWatcher.SetSource(change.GetNewValue<IList<SankeyFlowLink>?>() as IEnumerable);
        }
        else if (change.Property == ColumnsProperty)
        {
            _textDirty = true;
            _placementDirty = true;
            InvalidateMeasure();
            Repaint();
        }
        else if (change.Property == ValueFormatterProperty || change.Property == UnitProperty || change.Property == EmptyLabelProperty || change.Property == TotalLabelProperty)
        {
            _textDirty = true;
            _tipSignature = null;
            SyncTooltip();
            UpdateAccessibility();
            Repaint();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateAccessibility();
        }
        else if (change.Property == IsParticlesEnabledProperty)
        {
            var enabled = change.GetNewValue<bool>();
            if (_toggle is not null)
            {
                _toggle.IsChecked = enabled;
            }
            SyncParticles();
            ParticlesChanged?.Invoke(this, new SankeyFlowParticlesChangedEventArgs(enabled));
        }
        else if (change.Property == ParticleToggleLabelProperty)
        {
            if (_toggle is not null)
            {
                _toggle.Content = ParticleToggleLabel;
                AutomationProperties.SetName(_toggle, ParticleToggleLabel);
            }
        }
        else if (change.Property == IsParticleToggleVisibleProperty || change.Property == ArcMotion.LevelProperty)
        {
            SyncMotion();
        }
        else if (change.Property == NodeThicknessProperty || change.Property == NodeGapProperty || change.Property == FigureGapProperty || change.Property == PlotHeightProperty)
        {
            _placementDirty = true;
            _textDirty = true;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _nodesWatcher.Attach();
        _linksWatcher.Attach();
        _reveal.Attach();
        SyncMotion();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _nodesWatcher.Detach();
        _linksWatcher.Detach();
        _reveal.Detach();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SyncMotion();
    }

    // ---- data -------------------------------------------------------------------------------------------------

    private void MarkDirty()
    {
        _modelDirty = true;
        _placementDirty = true;
        _dataChanged = true;
        InvalidateMeasure();
        Repaint();
    }

    private void EnsureModel()
    {
        if (!_modelDirty)
        {
            return;
        }
        _modelDirty = false;
        var keyId = _keyNode >= 0 && _keyNode < _model.Nodes.Count ? _model.Nodes[_keyNode].Id : null;
        _model = SankeyFlowModel.Build(Nodes, Links);
        _placement = null;
        Focusable = !_model.IsEmpty;
        _textDirty = true;
        _tipSignature = null;
        _keyNode = keyId is null ? -1 : FindNode(keyId);
        _pointerTarget = null;
        _traced = null;
        _trace = null;
        ApplyTrace();
        SyncMotion();
        UpdateAccessibility();
    }

    private int FindNode(string id)
    {
        foreach (var node in _model.Nodes)
        {
            if (node.Id == id)
            {
                return node.Index;
            }
        }
        return -1;
    }

    // ---- layout -----------------------------------------------------------------------------------------------

    private double HeaderHeight => Columns is { Count: > 0 } ? Math.Ceiling(_palette.TypeXS * 1.5) : 0;

    private double PlotExtent(bool upright)
        => upright ? Math.Max(PlotHeight, _model.ColumnCount * UprightRowSpacing) : PlotHeight;

    private double ToggleBand => ToggleWanted && _toggle is not null ? _toggle.DesiredSize.Height + FigureGap : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureModel();
        SyncMotion();
        var width = double.IsInfinity(availableSize.Width) ? 560 : availableSize.Width;
        _toggle?.Measure(new Size(width, double.PositiveInfinity));
        _particleLayer?.Measure(availableSize);
        _overlay.Measure(availableSize);
        if (_model.IsEmpty)
        {
            return new Size(width, PlotHeight);
        }
        var upright = width < UprightBelow;
        var header = HeaderHeight;
        var height = upright ? PlotExtent(true) : (header > 0 ? header + FigureGap : 0) + PlotHeight;
        return new Size(width, height + ToggleBand);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureModel();
        EnsurePlacement(finalSize);
        var full = new Rect(finalSize);
        _particleLayer?.Arrange(full);
        _overlay.Arrange(full);
        if (_toggle is not null)
        {
            var size = _toggle.DesiredSize;
            // Right under the plot, however much taller a stretching parent makes the chart.
            var plotBottom = _upright ? PlotExtent(true) : (HeaderHeight > 0 ? HeaderHeight + FigureGap : 0) + PlotHeight;
            _toggle.Arrange(new Rect(Math.Max(0, finalSize.Width - size.Width), plotBottom + FigureGap, size.Width, size.Height));
        }
        return finalSize;
    }

    private void EnsurePlacement(Size size)
    {
        if (!_placementDirty && size == _placedSize)
        {
            return;
        }
        _placementDirty = false;
        _placedSize = size;
        _upright = size.Width > 0 && size.Width < UprightBelow;
        _textDirty = true;
        SankeyFlowLayoutOptions options;
        if (_upright)
        {
            var rowHeader = HeaderHeight > 0 ? HeaderHeight + 2 : 0;
            options = new SankeyFlowLayoutOptions(rowHeader, PlotExtent(true) - UprightBeneath, Inset, size.Width - Inset, NodeThickness, Math.Min(NodeGap, 8));
        }
        else
        {
            var top = HeaderHeight > 0 ? HeaderHeight + FigureGap : 0;
            options = new SankeyFlowLayoutOptions(Inset, size.Width - Inset, top + Inset, top + PlotHeight - Inset, NodeThickness, NodeGap);
        }
        _placement = SankeyFlowLayout.Arrange(_model, options);
        var dataChanged = _dataChanged;
        _dataChanged = false;
        Retarget(_placement, dataChanged);
        SyncTooltip();
        Repaint();
    }

    private void Retarget(SankeyFlowPlacement placement, bool dataChanged)
    {
        var sum = 0.0;
        foreach (var link in _model.Links)
        {
            sum += link.Value;
        }
        if (dataChanged)
        {
            RebuildItems(placement);
        }
        else
        {
            for (var i = 0; i < placement.Nodes.Length; i++)
            {
                _nodeItems[i].To = placement.Nodes[i];
                if (!_morphing)
                {
                    _nodeItems[i].Drawn = placement.Nodes[i];
                }
            }
            for (var i = 0; i < placement.Links.Length; i++)
            {
                _linkItems[i].To = placement.Links[i];
                if (!_morphing)
                {
                    _linkItems[i].Drawn = placement.Links[i];
                }
            }
        }
        for (var i = 0; i < placement.Links.Length; i++)
        {
            PlanParticles(_linkItems[i], i, _model.Links[i].Value, sum);
        }
        _staggerStep = _model.ColumnCount > 1 ? Math.Min(0.08, 0.4 / _model.ColumnCount) : 0;
        _maxStagger = _staggerStep * Math.Max(0, _model.ColumnCount - 1);
        _stamp++;
        if (!dataChanged)
        {
            return;
        }
        if (_reveal.IsRevealed)
        {
            Play(!_played && !_model.IsEmpty);
        }
        else
        {
            Finish();
        }
    }

    private void RebuildItems(SankeyFlowPlacement placement)
    {
        var oldNodes = new Dictionary<string, NodeItem>(_nodeItems.Count);
        foreach (var item in _nodeItems)
        {
            oldNodes[item.Id] = item;
        }
        var oldLinks = new Dictionary<string, LinkItem>(_linkItems.Count);
        foreach (var item in _linkItems)
        {
            oldLinks[item.Key] = item;
        }
        _nodeItems.Clear();
        _linkItems.Clear();
        for (var i = 0; i < placement.Nodes.Length; i++)
        {
            var node = _model.Nodes[i];
            var to = placement.Nodes[i];
            var from = oldNodes.Remove(node.Id, out var previous) ? previous.Drawn : CollapsedNode(to);
            _nodeItems.Add(new NodeItem { Id = node.Id, Column = node.Column, From = from, To = to, Drawn = from });
        }
        for (var i = 0; i < placement.Links.Length; i++)
        {
            var link = _model.Links[i];
            var to = placement.Links[i];
            var from = oldLinks.Remove(link.Key, out var previous) ? previous.Drawn : CollapsedLink(to);
            _linkItems.Add(new LinkItem
            {
                Key = link.Key,
                SourceColumn = _model.Nodes[link.Source].Column,
                TargetColumn = _model.Nodes[link.Target].Column,
                From = from, To = to, Drawn = from,
                Seed = (i * 0.6180339887498949) % 1
            });
        }
        // What left the data shrinks away; it is dropped when the morph ends.
        foreach (var ghost in oldNodes.Values)
        {
            ghost.From = ghost.Drawn;
            ghost.To = ghost.Drawn;
            ghost.To.Cross += ghost.Drawn.Extent / 2;
            ghost.To.Extent = 0;
            _nodeItems.Add(ghost);
        }
        foreach (var ghost in oldLinks.Values)
        {
            ghost.From = ghost.Drawn;
            ghost.To = ghost.Drawn;
            ghost.To.Thick = 0;
            ghost.Particles = 0;
            ghost.FillStamp = -1;
            _linkItems.Add(ghost);
        }
    }

    private static SankeyNodePose CollapsedNode(in SankeyNodePose pose) => new()
    {
        Main = pose.Main, Cross = pose.CrossCenter, Extent = 0, Thickness = pose.Thickness
    };

    private static SankeyLinkPose CollapsedLink(in SankeyLinkPose pose) => new()
    {
        M0 = pose.M0, M1 = pose.M0, C0 = pose.C0, C1 = pose.C0, Thick = 0
    };

    private static void PlanParticles(LinkItem item, int index, double value, double sum)
    {
        var length = Math.Max(40, Math.Abs(item.To.M1 - item.To.M0) + Math.Abs(item.To.C1 - item.To.C0) * 0.5);
        item.Speed = (46 + 18 * ((index * 0.7548776662466927) % 1)) / length;
        if (item.To.Thick < 0.75 || sum <= 0)
        {
            item.Particles = 0;
            return;
        }
        // Heavy ribbons carry more, but a chart of hundreds of ribbons stays within a few hundred dots.
        var byValue = Math.Max(1, (int)Math.Round(value / sum * 420));
        var byArea = Math.Max(1, (int)Math.Ceiling(item.To.Thick * length / 450));
        item.Particles = Math.Min(MaxParticlesPerRibbon, Math.Min(byValue, byArea));
    }

    // ---- motion -----------------------------------------------------------------------------------------------

    private void OnRevealed(bool animate)
    {
        SyncParticles();
        if (_placement is not null && !_dataChanged && _nodeItems.Count > 0)
        {
            Play(animate);
        }
    }

    /// <summary>Runs the shared spring from the current poses to the targets; the entrance grows everything from nothing.</summary>
    private void Play(bool entrance)
    {
        if (entrance)
        {
            foreach (var item in _nodeItems)
            {
                item.From = CollapsedNode(item.To);
                item.Drawn = item.From;
            }
            foreach (var item in _linkItems)
            {
                item.From = CollapsedLink(item.To);
                item.Drawn = item.From;
            }
        }
        _played = !_model.IsEmpty;
        if (!ArcMotion.IsFull(this) || !IsLoaded)
        {
            Finish();
            return;
        }
        _morphing = true;
        _entrance = entrance;
        _progress.Jump(0);
        var spec = entrance ? ChartsBMath.Physical(1, 0) : ChartsBMath.Physical(0.7, 0.08);
        FinishWhenDone(_progress.AnimateTo(1, MotionSpec.Spring(spec)), ++_generation);
    }

    private async void FinishWhenDone(Task<bool> run, int generation)
    {
        if (await run && generation == _generation)
        {
            Finish();
        }
    }

    private void OnProgress(double t)
    {
        if (!_morphing)
        {
            return;
        }
        var span = 1 - _maxStagger;
        foreach (var item in _nodeItems)
        {
            var local = _entrance ? ChartsBMath.Clamp((t - item.Column * _staggerStep) / span, 0, 1) : t;
            item.Drawn = SankeyNodePose.Mix(item.From, item.To, local);
        }
        foreach (var item in _linkItems)
        {
            var local = _entrance ? ChartsBMath.Clamp((t - item.SourceColumn * _staggerStep) / span, 0, 1) : t;
            item.Drawn = SankeyLinkPose.Mix(item.From, item.To, local);
        }
        _stamp++;
        Repaint();
        _particleLayer?.InvalidateVisual();
    }

    private void Finish()
    {
        _morphing = false;
        _generation++;
        _nodeItems.RemoveRange(_model.Nodes.Count, Math.Max(0, _nodeItems.Count - _model.Nodes.Count));
        _linkItems.RemoveRange(_model.Links.Count, Math.Max(0, _linkItems.Count - _model.Links.Count));
        foreach (var item in _nodeItems)
        {
            item.Drawn = item.To;
        }
        foreach (var item in _linkItems)
        {
            item.Drawn = item.To;
        }
        _stamp++;
        Repaint();
        _particleLayer?.InvalidateVisual();
    }

    private void SyncMotion()
    {
        var wanted = ToggleWanted;
        if (wanted && _toggle is null)
        {
            _toggle = new Switch
            {
                Content = ParticleToggleLabel,
                IsChecked = IsParticlesEnabled,
                ZIndex = 3
            };
            AutomationProperties.SetName(_toggle, ParticleToggleLabel);
            _toggle.IsCheckedChanged += (_, _) => SetCurrentValue(IsParticlesEnabledProperty, _toggle.IsChecked == true);
            VisualChildren.Add(_toggle);
            LogicalChildren.Add(_toggle);
            InvalidateMeasure();
        }
        if (_toggle is not null && _toggle.IsVisible != wanted)
        {
            _toggle.IsVisible = wanted;
            InvalidateMeasure();
        }
        SyncParticles();
    }

    private void SyncParticles()
    {
        var wanted = IsDrawingParticles;
        _drawingParticles = wanted;
        if (wanted)
        {
            if (_particleLayer is null)
            {
                _particleLayer = new ChartsBOverlay(DrawParticles) { ZIndex = 1 };
                VisualChildren.Add(_particleLayer);
                InvalidateMeasure();
            }
            _particleLayer.IsVisible = true;
            _particleLoop ??= new LiveLoop(this, AdvanceParticles);
            _particleLoop.Start();
        }
        else
        {
            _particleLoop?.Stop();
            if (_particleLayer is not null)
            {
                _particleLayer.IsVisible = false;
            }
        }
    }

    /// <summary>Repaints the chart and the label layer above its particles.</summary>
    private void Repaint()
    {
        InvalidateVisual();
        // Null only while the palette reports its first values from the constructor.
        _overlay?.InvalidateVisual();
    }

    private bool AdvanceParticles(double dt)
    {
        _clock += dt;
        _particleLayer?.InvalidateVisual();
        return true;
    }
}
