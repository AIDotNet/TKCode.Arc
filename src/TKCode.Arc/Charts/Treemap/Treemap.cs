using System.Collections;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A squarified treemap (Arc "Treemap") for a hierarchy of parts. Top-level branches take Series1–4 and, with
/// <see cref="ColorLabel"/>, every tile deepens with a second measure. Clicking (or Enter on) a branch zooms in: the
/// tile grows to fill the view while its siblings fly out and its children open inside it, all on one affine 0.62s
/// spring; the breadcrumb, Escape or Backspace zoom back out along the same path. A data change morphs every tile in
/// place. Labels never scale: each tile lays its text out at its size and shows it only when it fits.
/// </summary>
public class Treemap : Control, IChartsBAccessible
{
    /// <summary>Defines the <see cref="Root"/> property.</summary>
    public static readonly StyledProperty<TreemapNode?> RootProperty =
        AvaloniaProperty.Register<Treemap, TreemapNode?>(nameof(Root));

    /// <summary>Defines the <see cref="ItemsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Treemap, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Defines the <see cref="KeySelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> KeySelectorProperty =
        AvaloniaProperty.Register<Treemap, Func<object, string>?>(nameof(KeySelector));

    /// <summary>Defines the <see cref="LabelSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, string>?> LabelSelectorProperty =
        AvaloniaProperty.Register<Treemap, Func<object, string>?>(nameof(LabelSelector));

    /// <summary>Defines the <see cref="ValueSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, double>?> ValueSelectorProperty =
        AvaloniaProperty.Register<Treemap, Func<object, double>?>(nameof(ValueSelector));

    /// <summary>Defines the <see cref="ColorValueSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, double?>?> ColorValueSelectorProperty =
        AvaloniaProperty.Register<Treemap, Func<object, double?>?>(nameof(ColorValueSelector));

    /// <summary>Defines the <see cref="ChildrenSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<Treemap, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Treemap, string?>(nameof(Label));

    /// <summary>Defines the <see cref="ValueFormatter"/> property.</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<Treemap, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Defines the <see cref="ColorLabel"/> property.</summary>
    public static readonly StyledProperty<string?> ColorLabelProperty =
        AvaloniaProperty.Register<Treemap, string?>(nameof(ColorLabel));

    /// <summary>Defines the <see cref="ColorFormatter"/> property.</summary>
    public static readonly StyledProperty<Func<double, string>?> ColorFormatterProperty =
        AvaloniaProperty.Register<Treemap, Func<double, string>?>(nameof(ColorFormatter));

    /// <summary>Defines the <see cref="ColorDomainMinimum"/> property.</summary>
    public static readonly StyledProperty<double> ColorDomainMinimumProperty =
        AvaloniaProperty.Register<Treemap, double>(nameof(ColorDomainMinimum), double.NaN);

    /// <summary>Defines the <see cref="ColorDomainMaximum"/> property.</summary>
    public static readonly StyledProperty<double> ColorDomainMaximumProperty =
        AvaloniaProperty.Register<Treemap, double>(nameof(ColorDomainMaximum), double.NaN);

    /// <summary>Defines the <see cref="FocusedNodeId"/> property.</summary>
    public static readonly StyledProperty<string?> FocusedNodeIdProperty =
        AvaloniaProperty.Register<Treemap, string?>(nameof(FocusedNodeId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="PlotHeight"/> property.</summary>
    public static readonly StyledProperty<double> PlotHeightProperty =
        AvaloniaProperty.Register<Treemap, double>(nameof(PlotHeight), 420);

    /// <summary>Defines the <see cref="EmptyLabel"/> property.</summary>
    public static readonly StyledProperty<string> EmptyLabelProperty =
        AvaloniaProperty.Register<Treemap, string>(nameof(EmptyLabel), "No data yet");

    internal static readonly StyledProperty<double> StageRadiusProperty =
        AvaloniaProperty.Register<Treemap, double>(nameof(StageRadius), 12);

    internal static readonly StyledProperty<double> FigureGapProperty =
        AvaloniaProperty.Register<Treemap, double>(nameof(FigureGap), 12);

    internal const int Steps = 5;

    private readonly ChartsBPalette _palette;
    private readonly ChartsBItemsWatcher _watcher;
    private readonly ChartsBMemberReader _reader = new();
    private readonly AvaloniaList<TreemapNode> _items = new();
    private readonly TreemapCrumbs _crumbs;
    private readonly ArcContentSwap _total;
    private readonly TreemapStage _stage;
    private BindingBase? _keyBinding;
    private BindingBase? _labelBinding;
    private BindingBase? _valueBinding;
    private BindingBase? _colorBinding;
    private BindingBase? _childrenBinding;
    private TreemapNode _root = new() { Id = "__root" };
    private Dictionary<string, TreemapFlat> _flat = new();
    private Dictionary<string, int> _branchOf = new();
    private double _colorLo;
    private double _colorHi = 1;
    private bool _colored;
    private FormattedText? _scaleLabel;
    private FormattedText? _scaleLo;
    private FormattedText? _scaleHi;
    private int _scaleVersion = -1;

    /// <summary>Creates a treemap.</summary>
    public Treemap()
    {
        _palette = new ChartsBPalette(this, () =>
        {
            _scaleVersion = -1;
            _stage?.OnPaletteChanged();
            _crumbs?.InvalidateText();
            InvalidateVisual();
        });
        _crumbs = new TreemapCrumbs(this);
        _total = new ArcContentSwap { Mode = ContentSwapMode.Roll, HorizontalContentAlignment = HorizontalAlignment.Right };
        ArcTokens.Bind(_total, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_total, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_total, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_total, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _stage = new TreemapStage(this);
        foreach (var child in new Control[] { _crumbs, _total, _stage })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _watcher = new ChartsBItemsWatcher(Rebuild);
        _items.CollectionChanged += (_, _) =>
        {
            // While attached the watcher reports inline changes itself.
            if (ItemsSource is null && !_watcher.IsSubscribed)
            {
                Rebuild();
            }
        };
        _watcher.SetSource(_items);
    }

    static Treemap()
    {
        AffectsMeasure<Treemap>(PlotHeightProperty, ColorLabelProperty, FigureGapProperty);
    }

    /// <summary>The whole hierarchy; its label names the first breadcrumb. Wins over <see cref="ItemsSource"/>.</summary>
    public TreemapNode? Root
    {
        get => GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    /// <summary>Top-level items: <see cref="TreemapNode"/> instances, or any objects read through the selectors (children via <see cref="ChildrenSelector"/>).</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Inline top-level nodes, used when neither <see cref="Root"/> nor <see cref="ItemsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<TreemapNode> Items => _items;

    /// <summary>Reads a node id (wins over <see cref="KeyMemberBinding"/>).</summary>
    public Func<object, string>? KeySelector
    {
        get => GetValue(KeySelectorProperty);
        set => SetValue(KeySelectorProperty, value);
    }

    /// <summary>Reads a node label.</summary>
    public Func<object, string>? LabelSelector
    {
        get => GetValue(LabelSelectorProperty);
        set => SetValue(LabelSelectorProperty, value);
    }

    /// <summary>Reads a leaf value.</summary>
    public Func<object, double>? ValueSelector
    {
        get => GetValue(ValueSelectorProperty);
        set => SetValue(ValueSelectorProperty, value);
    }

    /// <summary>Reads the optional shading measure.</summary>
    public Func<object, double?>? ColorValueSelector
    {
        get => GetValue(ColorValueSelectorProperty);
        set => SetValue(ColorValueSelectorProperty, value);
    }

    /// <summary>Reads the child items of a data item.</summary>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => GetValue(ChildrenSelectorProperty);
        set => SetValue(ChildrenSelectorProperty, value);
    }

    /// <summary>Binding (member path) that reads each node id.</summary>
    [AssignBinding]
    public BindingBase? KeyMemberBinding
    {
        get => _keyBinding;
        set
        {
            _keyBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each node label.</summary>
    [AssignBinding]
    public BindingBase? LabelMemberBinding
    {
        get => _labelBinding;
        set
        {
            _labelBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each leaf value.</summary>
    [AssignBinding]
    public BindingBase? ValueMemberBinding
    {
        get => _valueBinding;
        set
        {
            _valueBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each node's shading measure.</summary>
    [AssignBinding]
    public BindingBase? ColorValueMemberBinding
    {
        get => _colorBinding;
        set
        {
            _colorBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each item's children (an enumerable).</summary>
    [AssignBinding]
    public BindingBase? ChildrenMemberBinding
    {
        get => _childrenBinding;
        set
        {
            _childrenBinding = value;
            Rebuild();
        }
    }

    /// <summary>What the whole is, such as "Revenue"; names the chart and the first breadcrumb (when items come from <see cref="ItemsSource"/>).</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Formats sizes (default: grouped, at most one decimal).</summary>
    public Func<double, string>? ValueFormatter
    {
        get => GetValue(ValueFormatterProperty);
        set => SetValue(ValueFormatterProperty, value);
    }

    /// <summary>Names the shading measure, such as "Growth"; tiles then deepen with it and the scale shows. Unset: one depth.</summary>
    public string? ColorLabel
    {
        get => GetValue(ColorLabelProperty);
        set => SetValue(ColorLabelProperty, value);
    }

    /// <summary>Formats the shading measure.</summary>
    public Func<double, string>? ColorFormatter
    {
        get => GetValue(ColorFormatterProperty);
        set => SetValue(ColorFormatterProperty, value);
    }

    /// <summary>Lower end of the shading range (NaN: the leaves' minimum).</summary>
    public double ColorDomainMinimum
    {
        get => GetValue(ColorDomainMinimumProperty);
        set => SetValue(ColorDomainMinimumProperty, value);
    }

    /// <summary>Upper end of the shading range (NaN: the leaves' maximum).</summary>
    public double ColorDomainMaximum
    {
        get => GetValue(ColorDomainMaximumProperty);
        set => SetValue(ColorDomainMaximumProperty, value);
    }

    /// <summary>Id of the node that fills the view (two-way; null is the root).</summary>
    public string? FocusedNodeId
    {
        get => GetValue(FocusedNodeIdProperty);
        set => SetValue(FocusedNodeIdProperty, value);
    }

    /// <summary>Height of the tiles in pixels (default 420).</summary>
    public double PlotHeight
    {
        get => GetValue(PlotHeightProperty);
        set => SetValue(PlotHeightProperty, value);
    }

    /// <summary>Message shown when the tree has no value.</summary>
    public string EmptyLabel
    {
        get => GetValue(EmptyLabelProperty);
        set => SetValue(EmptyLabelProperty, value);
    }

    /// <summary>Raised when another node fills the view (zoom in or out).</summary>
    public event EventHandler<TreemapFocusedNodeChangedEventArgs>? FocusedNodeChanged;

    internal double StageRadius
    {
        get => GetValue(StageRadiusProperty);
        set => SetValue(StageRadiusProperty, value);
    }

    internal double FigureGap
    {
        get => GetValue(FigureGapProperty);
        set => SetValue(FigureGapProperty, value);
    }

    internal ChartsBPalette Palette => _palette;
    internal Dictionary<string, TreemapFlat> Flat => _flat;
    internal TreemapFlat RootFlat => _flat[_root.Id];
    internal TreemapStage Stage => _stage;
    internal TreemapCrumbs Crumbs => _crumbs;
    internal bool IsColored => _colored;
    internal bool IsEmpty => RootFlat.Value <= 0;

    /// <summary>The node that fills the view: the focused node when it is a branch, else the root.</summary>
    internal TreemapFlat Center =>
        FocusedNodeId is { } id && _flat.TryGetValue(id, out var node) && node.Children.Count > 0 ? node : RootFlat;

    internal string Format(double value) => ValueFormatter?.Invoke(value) ?? ChartsBMath.Grouped(value);

    internal string FormatColor(double value) => ColorFormatter?.Invoke(value) ?? ChartsBMath.Grouped(value);

    /// <summary>Top-level branches take the four series hues in order; a fifth and beyond stay neutral rather than reuse a hue.</summary>
    internal Color HueOf(string id) => _branchOf.TryGetValue(id, out var index) && index < 4 ? _palette.Series(index) : _branchOf.ContainsKey(id) ? _palette.Foreground : _palette.Series(0);

    /// <summary>Shade percentage: a few steps from light to deep so shade reads as "more" without adding a hue.</summary>
    internal double ShadeOf(double? value)
    {
        if (!_colored || value is null)
        {
            return 0.34;
        }
        var t = ChartsBMath.Clamp((value.Value - _colorLo) / (_colorHi - _colorLo == 0 ? 1 : _colorHi - _colorLo), 0, 1);
        return (14 + Math.Round(t * (Steps - 1)) / (Steps - 1) * 44) / 100;
    }

    internal TreemapFlat ParentOf(TreemapFlat node) => node.Parent is { } parent ? _flat[parent] : node;

    internal static double ShareOf(TreemapFlat node, TreemapFlat of) => of.Value > 0 ? node.Value / of.Value : 0;

    internal string Describe(TreemapFlat node)
    {
        var parent = ParentOf(node);
        var text = $"{string.Join(", ", node.Path)}, {Format(node.Value)}, {ChartsBMath.Percent(ShareOf(node, parent), 1)} of {parent.Label}";
        return _colored && node.Color is { } color ? $"{text}, {ColorLabel} {FormatColor(color)}" : text;
    }

    internal void ZoomTo(string id)
    {
        var target = id == _root.Id ? null : id;
        if (FocusedNodeId != target)
        {
            FocusedNodeId = target;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _watcher.SetSource(ItemsSource ?? _items);
        }
        else if (change.Property == RootProperty || change.Property == KeySelectorProperty || change.Property == LabelSelectorProperty ||
                 change.Property == ValueSelectorProperty || change.Property == ColorValueSelectorProperty ||
                 change.Property == ChildrenSelectorProperty || change.Property == ColorLabelProperty ||
                 change.Property == ColorDomainMinimumProperty || change.Property == ColorDomainMaximumProperty)
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
        }
        else if (change.Property == FocusedNodeIdProperty)
        {
            OnCenterChanged();
            var id = Center.Id;
            FocusedNodeChanged?.Invoke(this, new TreemapFocusedNodeChangedEventArgs(id, _flat.TryGetValue(id, out var node) ? node.Node : null));
        }
        else if (change.Property == ValueFormatterProperty || change.Property == ColorFormatterProperty || change.Property == EmptyLabelProperty)
        {
            _scaleVersion = -1;
            UpdateTotal();
            _stage.OnPaletteChanged();
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watcher.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watcher.Detach();
    }

    private void Rebuild()
    {
        if (Root is { } root)
        {
            _root = root;
        }
        else
        {
            var top = new TreemapNode { Id = "__root", Label = Label ?? "All" };
            foreach (var item in _watcher.Snapshot())
            {
                top.Children.Add(Resolve(item, 0));
            }
            _root = top;
        }
        _flat = TreemapLayout.Flatten(_root);
        _branchOf = new Dictionary<string, int>();
        var rootFlat = _flat[_root.Id];
        for (var i = 0; i < rootFlat.Children.Count; i++)
        {
            var index = i;
            void Walk(string at)
            {
                _branchOf[at] = index;
                foreach (var child in _flat[at].Children)
                {
                    Walk(child);
                }
            }
            Walk(rootFlat.Children[i]);
        }
        var leaves = _flat.Values.Where(n => n.Depth > 0 && n.Children.Count == 0 && n.Color is not null).ToList();
        _colored = ColorLabel is not null && leaves.Count > 0;
        _colorLo = !double.IsNaN(ColorDomainMinimum) ? ColorDomainMinimum : leaves.Count > 0 ? leaves.Min(n => n.Color!.Value) : 0;
        _colorHi = !double.IsNaN(ColorDomainMaximum) ? ColorDomainMaximum : leaves.Count > 0 ? leaves.Max(n => n.Color!.Value) : 1;
        _scaleVersion = -1;
        UpdateTotal();
        _crumbs.OnTrailChanged();
        _stage.OnDataChanged();
        AutomationProperties.SetName(this, Label);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private TreemapNode Resolve(object item, int depth)
    {
        if (item is TreemapNode node && KeySelector is null && LabelSelector is null && ValueSelector is null && ColorValueSelector is null &&
            ChildrenSelector is null && _keyBinding is null && _labelBinding is null && _valueBinding is null && _colorBinding is null &&
            _childrenBinding is null)
        {
            return node;
        }
        var typed = item as TreemapNode;
        var id = KeySelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _keyBinding)) ?? typed?.Id ?? item.ToString() ?? string.Empty;
        var label = LabelSelector?.Invoke(item) ?? ChartsBMemberReader.ToText(_reader.Read(item, _labelBinding)) ?? typed?.Label ?? id;
        double? value = ValueSelector is { } valueSelector ? valueSelector(item) : _valueBinding is null ? typed?.Value : ChartsBMemberReader.ToDouble(_reader.Read(item, _valueBinding));
        double? color = ColorValueSelector is { } colorSelector ? colorSelector(item) : _colorBinding is null ? typed?.ColorValue : ChartsBMemberReader.ToDouble(_reader.Read(item, _colorBinding));
        var resolved = new TreemapNode(id, label, value is { } v && double.IsFinite(v) ? v : null, color is { } c && double.IsFinite(c) ? c : null) { Source = item };
        var children = ChildrenSelector?.Invoke(item) ?? (_childrenBinding is null ? typed?.Children : _reader.Read(item, _childrenBinding) as IEnumerable);
        if (children is not null && depth < 32)
        {
            foreach (var child in children)
            {
                if (child is not null)
                {
                    resolved.Children.Add(Resolve(child, depth + 1));
                }
            }
        }
        return resolved;
    }

    private void OnCenterChanged()
    {
        UpdateTotal();
        _crumbs.OnTrailChanged();
        _stage.OnCenterChanged();
        InvalidateVisual();
    }

    private void UpdateTotal()
    {
        var center = Center;
        var previous = _total.Content as string;
        var text = Format(center.Value);
        if (previous != text)
        {
            _total.Content = text;
        }
        AutomationProperties.SetHelpText(this, $"{center.Label}, {text}");
    }

    string IChartsBAccessible.ChartRoleDescription => "treemap";

    string IChartsBAccessible.ChartName => $"{Label}, {Center.Label}. Arrow keys move between tiles, Enter zooms in, Escape zooms out.";

    string IChartsBAccessible.ChartHelp => _stage.Reading ?? $"{Center.Label}, {Format(Center.Value)}";

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        _total.Measure(Size.Infinity);
        _crumbs.Measure(new Size(Math.Max(0, width - _total.DesiredSize.Width - 12), double.PositiveInfinity));
        _stage.Measure(new Size(width, PlotHeight));
        var bar = Math.Max(32, Math.Max(_crumbs.DesiredSize.Height, _total.DesiredSize.Height));
        var scale = _colored ? FigureGap + Math.Ceiling(_palette.TypeXS * 1.5) : 0;
        return new Size(width, bar + FigureGap + PlotHeight + scale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var bar = Math.Max(32, Math.Max(_crumbs.DesiredSize.Height, _total.DesiredSize.Height));
        var total = _total.DesiredSize;
        _total.Arrange(new Rect(width - total.Width, (bar - total.Height) / 2, total.Width, total.Height));
        _crumbs.Arrange(new Rect(-4, (bar - _crumbs.DesiredSize.Height) / 2, Math.Max(0, width - total.Width - 12 + 4), _crumbs.DesiredSize.Height));
        _stage.Arrange(new Rect(0, bar + FigureGap, width, PlotHeight));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (!_colored)
        {
            return;
        }
        var p = _palette;
        if (_scaleVersion != p.Version)
        {
            _scaleVersion = p.Version;
            _scaleLabel = ChartsBText.Make(ColorLabel ?? string.Empty, p, p.TypeXS, p.TextSecondary);
            _scaleLo = ChartsBText.Make(FormatColor(_colorLo), p, p.TypeXS, p.TextMuted);
            _scaleHi = ChartsBText.Make(FormatColor(_colorHi), p, p.TypeXS, p.TextMuted);
        }
        // The color scale: stepped swatches from light to dark, never a gradient.
        var y = _stage.Bounds.Bottom + FigureGap;
        var height = Math.Ceiling(p.TypeXS * 1.5);
        var x = 0.0;
        context.DrawText(_scaleLabel!, new Point(x, y + (height - _scaleLabel!.Height) / 2));
        x += _scaleLabel.WidthIncludingTrailingWhitespace + 4 + 8;
        context.DrawText(_scaleLo!, new Point(x, y + (height - _scaleLo!.Height) / 2));
        x += _scaleLo.WidthIncludingTrailingWhitespace + 8;
        var center = Center;
        var hue = center.Depth == 0 ? p.Foreground : HueOf(center.Id);
        for (var i = 0; i < Steps; i++)
        {
            var fill = ChartsBMath.MixIn(hue, (14 + (double)i / (Steps - 1) * 44) / 100, p.Surface);
            context.DrawRectangle(p.Brush(fill), null, new RoundedRect(new Rect(x + i * 20, y + (height - 10) / 2, 18, 10), 3));
        }
        x += Steps * 20 - 2 + 8;
        context.DrawText(_scaleHi!, new Point(x, y + (height - _scaleHi!.Height) / 2));
    }
}
