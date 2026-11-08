using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A hierarchy drawn as concentric rings. Clicking a segment makes it the new centre: every arc moves to its new angles
/// on one shared spring while rings grow or shrink as levels appear and leave. A breadcrumb, the centre and the legend
/// lead back and forth, the centre rolls the current total digit by digit, and labels show only where they fit their arc.
/// </summary>
public class Sunburst : Control, IChartsBAccessible
{
    /// <summary>The whole hierarchy; its label names the first breadcrumb. Wins over <see cref="Items"/>.</summary>
    public static readonly StyledProperty<SunburstNode?> RootProperty =
        AvaloniaProperty.Register<Sunburst, SunburstNode?>(nameof(Root));

    /// <summary>What the whole is, such as "Annual costs": the accessible name of the chart, and the first breadcrumb when the nodes come from <see cref="Items"/>.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Sunburst, string?>(nameof(Label));

    /// <summary>Formats values in the centre, legend, tooltip and accessible reading (default: grouped, at most one decimal).</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<Sunburst, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>How many rings are visible at once (at least 1, default 3).</summary>
    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<Sunburst, int>(nameof(Depth), 3);

    /// <summary>Largest diameter of the ring in pixels (default 340); it scales down to fit its container.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(Size), 340);

    /// <summary>Id of the node at the centre (two-way; null is the root). A leaf's id shows its parent at the centre, and an unknown id shows the root.</summary>
    public static readonly StyledProperty<string?> FocusedIdProperty =
        AvaloniaProperty.Register<Sunburst, string?>(nameof(FocusedId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the legend of the centre's children shows, with values and shares (default true).</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<Sunburst, bool>(nameof(IsLegendVisible), true);

    /// <summary>Message shown when the total is zero.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<Sunburst, string>(nameof(EmptyLabel), "No data yet");

    internal static readonly StyledProperty<double> FigureGapProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(FigureGap), 12);

    internal static readonly StyledProperty<double> HoleRatioProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(HoleRatio), 0.3);

    internal static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(SegmentGap), 2);

    internal static readonly StyledProperty<double> SegmentCornerProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(SegmentCorner), 3);

    internal static readonly StyledProperty<double> LegendGapProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(LegendGap), 24);

    internal static readonly StyledProperty<double> LegendMinWidthProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(LegendMinWidth), 200);

    internal static readonly StyledProperty<double> LegendMaxWidthProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(LegendMaxWidth), 300);

    internal static readonly StyledProperty<double> LegendRowHeightProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(LegendRowHeight), 36);

    internal static readonly StyledProperty<double> LegendRowRadiusProperty =
        AvaloniaProperty.Register<Sunburst, double>(nameof(LegendRowRadius), 8);

    private const double MinRing = 120;

    private readonly ChartsBPalette _palette;
    private readonly AvaloniaList<SunburstNode> _items = new();
    private readonly AvaloniaList<string> _crumbLabels = new();
    private readonly List<SunburstEntry> _trail = new();
    private readonly Breadcrumb _breadcrumb;
    private readonly SunburstStage _stage;
    private readonly ScrollViewer _legendScroll;
    private readonly StackPanel _legendRows;
    private readonly ChartsBOverlay _overlay;
    private readonly MotionValue<double> _legendFade;
    private SunburstTree _tree = SunburstTree.Build(new SunburstNode { Id = "__root" });
    private SunburstEntry _center;
    private SunburstFocusChangeSource _source;
    private bool _wide;
    private double _diameter;
    private bool _shown;

    static Sunburst()
    {
        AffectsMeasure<Sunburst>(SizeProperty, IsLegendVisibleProperty, FigureGapProperty, LegendGapProperty, LegendMinWidthProperty,
            LegendMaxWidthProperty, LegendRowHeightProperty);
    }

    /// <summary>Creates a sunburst.</summary>
    public Sunburst()
    {
        _center = _tree.Root;
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _breadcrumb = new Breadcrumb { ItemsSource = _crumbLabels, Label = "Path" };
        _breadcrumb.ItemClick += OnCrumbClick;
        _overlay = new ChartsBOverlay(PaintOverlay);
        _stage = new SunburstStage(this);
        _legendRows = new StackPanel();
        _legendScroll = new ScrollViewer
        {
            Content = _legendRows,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalAlignment = VerticalAlignment.Top
        };
        _legendFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _legendFade.Changed += value => _legendScroll.Opacity = value;
        foreach (var child in new Control[] { _breadcrumb, _stage, _legendScroll, _overlay })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _items.CollectionChanged += (_, _) =>
        {
            if (Root is null)
            {
                Rebuild();
            }
        };
        Rebuild();
    }

    /// <inheritdoc cref="RootProperty"/>
    public SunburstNode? Root
    {
        get => GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    /// <summary>Inline top-level nodes, used when <see cref="Root"/> is not set.</summary>
    [Content]
    public AvaloniaList<SunburstNode> Items => _items;

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

    /// <inheritdoc cref="DepthProperty"/>
    public int Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="FocusedIdProperty"/>
    public string? FocusedId
    {
        get => GetValue(FocusedIdProperty);
        set => SetValue(FocusedIdProperty, value);
    }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible
    {
        get => GetValue(IsLegendVisibleProperty);
        set => SetValue(IsLegendVisibleProperty, value);
    }

    /// <inheritdoc cref="EmptyLabelProperty"/>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when another node moves to the centre, from a segment, the breadcrumb, the legend, the centre or code.</summary>
    public event EventHandler<SunburstFocusedIdChangedEventArgs>? FocusedIdChanged;

    internal double FigureGap
    {
        get => GetValue(FigureGapProperty);
        set => SetValue(FigureGapProperty, value);
    }

    internal double HoleRatio
    {
        get => GetValue(HoleRatioProperty);
        set => SetValue(HoleRatioProperty, value);
    }

    internal double SegmentGap
    {
        get => GetValue(SegmentGapProperty);
        set => SetValue(SegmentGapProperty, value);
    }

    internal double SegmentCorner
    {
        get => GetValue(SegmentCornerProperty);
        set => SetValue(SegmentCornerProperty, value);
    }

    internal double LegendGap
    {
        get => GetValue(LegendGapProperty);
        set => SetValue(LegendGapProperty, value);
    }

    internal double LegendMinWidth
    {
        get => GetValue(LegendMinWidthProperty);
        set => SetValue(LegendMinWidthProperty, value);
    }

    internal double LegendMaxWidth
    {
        get => GetValue(LegendMaxWidthProperty);
        set => SetValue(LegendMaxWidthProperty, value);
    }

    internal double LegendRowHeight
    {
        get => GetValue(LegendRowHeightProperty);
        set => SetValue(LegendRowHeightProperty, value);
    }

    internal double LegendRowRadius
    {
        get => GetValue(LegendRowRadiusProperty);
        set => SetValue(LegendRowRadiusProperty, value);
    }

    internal ChartsBPalette Palette => _palette;
    internal SunburstTree Tree => _tree;
    internal SunburstEntry Center => _center;
    internal SunburstStage Stage => _stage;
    internal Breadcrumb Breadcrumb => _breadcrumb;
    internal ChartsBOverlay Overlay => _overlay;
    internal IReadOnlyList<SunburstEntry> Trail => _trail;
    internal IEnumerable<SunburstLegendRow> LegendRows => _legendRows.Children.OfType<SunburstLegendRow>().Where(r => r.IsVisible);
    internal bool IsEmpty => _tree.Root.Value <= 0;
    internal bool IsLegendBeside => _wide;
    internal double Diameter => _diameter;

    internal string Format(double value) => ValueFormatter?.Invoke(value) ?? ChartsBMath.Grouped(value);

    /// <summary>A share in the legend: always one decimal, so the column's figures line up and read alike.</summary>
    internal static string LegendShare(double share) => (share * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    internal static double ShareOf(SunburstEntry node, SunburstEntry of) => of.Value > 0 ? node.Value / of.Value : 0;

    /// <summary>Path, value and share of the parent: what the tooltip shows and a screen reader says.</summary>
    internal string Describe(SunburstEntry node)
    {
        var parent = node.Parent ?? node;
        var path = node.Path.Length > 0 ? string.Join(", ", node.Path) : node.Label;
        return $"{path}, {Format(node.Value)}, {ChartsBMath.Percent(ShareOf(node, parent), 1)} of {parent.Label}";
    }

    /// <summary>Moves a node to the centre, keeping any two-way binding on <see cref="FocusedId"/>.</summary>
    internal void ZoomTo(string id, SunburstFocusChangeSource source)
    {
        var target = id == _tree.Root.Id ? null : id;
        if (FocusedId == target)
        {
            return;
        }
        _source = source;
        SetCurrentValue(FocusedIdProperty, target);
        _source = SunburstFocusChangeSource.Code;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RootProperty)
        {
            Rebuild();
        }
        else if (change.Property == LabelProperty)
        {
            if (Root is null)
            {
                Rebuild();
            }
            AutomationProperties.SetName(this, Label);
            _breadcrumb.Label = string.IsNullOrEmpty(Label) ? "Path" : $"{Label} path";
        }
        else if (change.Property == FocusedIdProperty)
        {
            OnFocusedIdChanged();
        }
        else if (change.Property == ValueFormatterProperty)
        {
            UpdateLegend();
            _stage.OnTextChanged();
        }
        else if (change.Property == EmptyLabelProperty)
        {
            _stage.OnTextChanged();
        }
        else if (change.Property == DepthProperty)
        {
            _stage.Relayout(animate: true);
        }
        else if (change.Property == HoleRatioProperty)
        {
            _stage.Relayout(animate: false);
        }
        else if (change.Property == SegmentGapProperty || change.Property == SegmentCornerProperty)
        {
            _stage.OnShapeChanged();
        }
        else if (change.Property == IsLegendVisibleProperty)
        {
            UpdateLegend();
        }
        else if (change.Property == LegendRowHeightProperty || change.Property == LegendRowRadiusProperty)
        {
            foreach (var row in LegendRows)
            {
                row.InvalidateMeasure();
                row.InvalidateVisual();
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Nodes written in XAML can fill in after they are added, and a tree built in code can change before it is shown.
        if (!_shown)
        {
            _shown = true;
            Rebuild();
        }
    }

    private void OnPaletteChanged()
    {
        _stage?.OnPaletteChanged();
        if (_legendRows is not null)
        {
            foreach (var row in LegendRows)
            {
                row.InvalidateVisual();
            }
        }
    }

    private void Rebuild()
    {
        var old = _tree;
        SunburstNode root;
        if (Root is { } explicitRoot)
        {
            root = explicitRoot;
        }
        else
        {
            root = new SunburstNode { Id = "__root", Label = Label ?? "All" };
            root.Children.AddRange(_items);
        }
        _tree = SunburstTree.Build(root);
        _center = ResolveCenter();
        UpdateTrail();
        UpdateLegend();
        _stage.OnDataChanged(old);
        InvalidateMeasure();
    }

    private SunburstEntry ResolveCenter()
    {
        if (FocusedId is not { } id || !_tree.ById.TryGetValue(id, out var node))
        {
            return _tree.Root;
        }
        while (!node.IsBranch && node.Parent is { } parent)
        {
            node = parent;
        }
        return node;
    }

    private void OnFocusedIdChanged()
    {
        var previous = _center;
        var next = ResolveCenter();
        if (ReferenceEquals(previous, next))
        {
            return;
        }
        _center = next;
        UpdateTrail();
        UpdateLegend();
        FadeInLegend();
        _stage.OnCenterChanged();
        FocusedIdChanged?.Invoke(this, new SunburstFocusedIdChangedEventArgs(next.Id, previous.Id, next.Node, _source));
    }

    private void FadeInLegend()
    {
        _legendFade.Jump(0);
        _ = _legendFade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private void UpdateTrail()
    {
        _trail.Clear();
        for (var at = _center; at is not null; at = at.Parent)
        {
            _trail.Insert(0, at);
        }
        // Shared crumbs stay put so only the changed end of the path animates.
        var keep = 0;
        while (keep < _crumbLabels.Count && keep < _trail.Count && _crumbLabels[keep] == _trail[keep].Label)
        {
            keep++;
        }
        while (_crumbLabels.Count > keep)
        {
            _crumbLabels.RemoveAt(_crumbLabels.Count - 1);
        }
        for (var i = keep; i < _trail.Count; i++)
        {
            _crumbLabels.Add(_trail[i].Label);
        }
        _breadcrumb.IsVisible = !IsEmpty;
    }

    private void UpdateLegend()
    {
        var center = _center;
        var count = 0;
        var showLegend = IsLegendVisible && !IsEmpty;
        _legendScroll.IsVisible = showLegend;
        if (showLegend)
        {
            foreach (var child in center.Children)
            {
                if (child.Value <= 0)
                {
                    continue;
                }
                SunburstLegendRow row;
                if (count < _legendRows.Children.Count)
                {
                    row = (SunburstLegendRow)_legendRows.Children[count];
                }
                else
                {
                    row = new SunburstLegendRow(this);
                    _legendRows.Children.Add(row);
                }
                row.SetEntry(child, center);
                row.IsVisible = true;
                count++;
            }
        }
        var lostFocus = false;
        for (var i = count; i < _legendRows.Children.Count; i++)
        {
            var row = _legendRows.Children[i];
            lostFocus |= row.IsFocused;
            row.IsVisible = false;
        }
        if (lostFocus)
        {
            _stage.Focus(NavigationMethod.Tab);
        }
        InvalidateMeasure();
    }

    private void OnCrumbClick(object? sender, BreadcrumbItemClickEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _trail.Count - 1)
        {
            return;
        }
        // The chosen crumb becomes the current one, which cannot hold focus: a keyboard user continues in the ring.
        var keyboard = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control focused &&
                       focused.Classes.Contains(":focus-visible") && _breadcrumb.IsVisualAncestorOf(focused);
        _stage.ClearActive();
        ZoomTo(_trail[e.Index].Id, SunburstFocusChangeSource.Breadcrumb);
        if (keyboard)
        {
            _stage.Focus(NavigationMethod.Tab);
        }
    }

    private void PaintOverlay(Avalonia.Media.DrawingContext context) => _stage.DrawTooltip(context);

    string IChartsBAccessible.ChartRoleDescription => "sunburst chart";

    string IChartsBAccessible.ChartName => _stage.Reading ??
        $"{Label}, {_center.Label}. Left and Right move between siblings, Down opens a child, Up goes to the parent, Enter zooms in, Escape zooms out.";

    string IChartsBAccessible.ChartHelp => _stage.Reading ?? $"{_center.Label}, {Format(_center.Value)}";

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Math.Max(MinRing, double.IsFinite(Size) ? Size : MinRing);
        var legend = _legendScroll.IsVisible;
        var legendRows = LegendRows.Count() * LegendRowHeight;
        var gapAcross = LegendGap + LegendMinWidth;
        var width = double.IsInfinity(availableSize.Width) ? size + (legend ? LegendGap + LegendMaxWidth : 0) : availableSize.Width;
        _breadcrumb.Measure(new Size(width, double.PositiveInfinity));
        var bar = _breadcrumb.IsVisible ? _breadcrumb.DesiredSize.Height + FigureGap : 0;
        var room = double.IsInfinity(availableSize.Height) ? double.PositiveInfinity : Math.Max(0, availableSize.Height - bar);
        // Beside when the ring keeps a useful size next to the legend's minimum width; this reads the chart's own width, never the window's.
        _wide = legend && width >= Math.Min(size, MinRing * 2) + gapAcross;
        double height;
        if (_wide)
        {
            _diameter = Math.Max(0, Math.Min(Math.Min(size, width - gapAcross), room));
            var legendWidth = Math.Min(LegendMaxWidth, width - _diameter - LegendGap);
            _legendScroll.Measure(new Size(legendWidth, Math.Max(_diameter, LegendRowHeight)));
            height = bar + Math.Max(_diameter, _legendScroll.DesiredSize.Height);
        }
        else
        {
            var below = legend ? FigureGap + legendRows : 0;
            var limit = double.IsInfinity(room) ? double.PositiveInfinity : Math.Max(Math.Min(MinRing, room), room - below);
            _diameter = Math.Max(0, Math.Min(Math.Min(size, width), limit));
            if (legend)
            {
                _legendScroll.Measure(new Size(width, double.IsInfinity(room) ? double.PositiveInfinity : Math.Max(0, room - _diameter - FigureGap)));
            }
            height = bar + _diameter + (legend ? FigureGap + _legendScroll.DesiredSize.Height : 0);
        }
        _stage.Measure(new Size(_diameter, _diameter));
        _overlay.Measure(new Size(width, height));
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        _breadcrumb.Arrange(new Rect(0, 0, width, _breadcrumb.IsVisible ? _breadcrumb.DesiredSize.Height : 0));
        var top = _breadcrumb.IsVisible ? _breadcrumb.DesiredSize.Height + FigureGap : 0;
        var stageX = (width - _diameter) / 2;
        if (_wide)
        {
            var legendWidth = _legendScroll.DesiredSize.Width;
            var group = _diameter + LegendGap + legendWidth;
            stageX = Math.Max(0, (width - group) / 2);
            _legendScroll.Arrange(new Rect(stageX + _diameter + LegendGap, top, legendWidth, _legendScroll.DesiredSize.Height));
        }
        else if (_legendScroll.IsVisible)
        {
            _legendScroll.Arrange(new Rect(0, top + _diameter + FigureGap, width, _legendScroll.DesiredSize.Height));
        }
        _stage.Arrange(new Rect(stageX, top, _diameter, _diameter));
        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }
}
