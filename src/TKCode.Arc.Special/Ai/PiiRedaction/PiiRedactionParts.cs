using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The message field of a <see cref="PiiRedaction"/> before a scan: a frameless multiline text box drawn in the same
/// place, font and line height as the text layers that replace it, so the switch to the marked message moves no word.
/// Cmd or Ctrl plus Enter asks for a scan.
/// </summary>
internal sealed class PiiRedactionEditor : TextBox
{
    static PiiRedactionEditor()
    {
        AcceptsReturnProperty.OverrideDefaultValue<PiiRedactionEditor>(true);
        TextWrappingProperty.OverrideDefaultValue<PiiRedactionEditor>(TextWrapping.Wrap);
    }

    protected override Type StyleKeyOverride => typeof(PiiRedactionEditor);

    /// <summary>Raised on Cmd or Ctrl plus Enter.</summary>
    public event EventHandler? ScanRequested;

    /// <summary>Raised when the person changes the text (typing, pasting, deleting); not when the card sets it.</summary>
    public event EventHandler? Edited;

    /// <summary>Set while the card writes the text, so <see cref="Edited"/> only reports the person's changes.</summary>
    internal bool IsSyncing { get; set; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            e.Handled = true;
            ScanRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty && !IsSyncing)
        {
            Edited?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// One type cell of a <see cref="PiiRedaction"/>: a dot in the type's color, the type's name, and under it the value
/// ("Not checked", a shimmering "Checking", the token or "None found") that crossfades in place. A cell that found
/// something takes a light tint of its color; the words say the same, so nothing depends on the color.
/// </summary>
internal sealed class PiiRedactionTypeCell : TemplatedControl
{
    internal const string FoundPseudoClass = ":found";

    /// <summary>The type's name ("Email").</summary>
    public static readonly StyledProperty<string?> TypeNameProperty =
        AvaloniaProperty.Register<PiiRedactionTypeCell, string?>(nameof(TypeName));

    /// <summary>The value under the name.</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<PiiRedactionTypeCell, string?>(nameof(Value));

    /// <summary>Whether the value shimmers (the scan is checking this type).</summary>
    public static readonly StyledProperty<bool> IsCheckingProperty =
        AvaloniaProperty.Register<PiiRedactionTypeCell, bool>(nameof(IsChecking));

    /// <summary>Whether the scan found this type.</summary>
    public static readonly StyledProperty<bool> IsFoundProperty =
        AvaloniaProperty.Register<PiiRedactionTypeCell, bool>(nameof(IsFound));

    /// <summary>The type's color (the dot).</summary>
    public static readonly StyledProperty<IBrush?> TypeBrushProperty =
        AvaloniaProperty.Register<PiiRedactionTypeCell, IBrush?>(nameof(TypeBrush));

    /// <summary>The tint of a cell that found something.</summary>
    public static readonly StyledProperty<IBrush?> TintBrushProperty =
        AvaloniaProperty.Register<PiiRedactionTypeCell, IBrush?>(nameof(TintBrush));

    public string? TypeName { get => GetValue(TypeNameProperty); set => SetValue(TypeNameProperty, value); }

    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public bool IsChecking { get => GetValue(IsCheckingProperty); set => SetValue(IsCheckingProperty, value); }

    public bool IsFound { get => GetValue(IsFoundProperty); set => SetValue(IsFoundProperty, value); }

    public IBrush? TypeBrush { get => GetValue(TypeBrushProperty); set => SetValue(TypeBrushProperty, value); }

    public IBrush? TintBrush { get => GetValue(TintBrushProperty); set => SetValue(TintBrushProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFoundProperty)
        {
            PseudoClasses.Set(FoundPseudoClass, IsFound);
        }
        if (change.Property == TypeNameProperty || change.Property == ValueProperty)
        {
            Avalonia.Automation.AutomationProperties.SetName(this, TypeName + ", " + Value);
        }
    }
}

/// <summary>
/// One node of the flow strip: a glyph and a label (You, the guard, the model). The active node's glyph takes the accent
/// and its label shimmers while it works.
/// </summary>
internal sealed class PiiRedactionFlowNode : TemplatedControl
{
    internal const string ActivePseudoClass = ":active";

    /// <summary>The node's 24 × 24 stroked glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<PiiRedactionFlowNode, Geometry?>(nameof(Glyph));

    /// <summary>The glyph's color when the app gives the model's mark a color of its own.</summary>
    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<PiiRedactionFlowNode, IBrush?>(nameof(GlyphBrush));

    /// <summary>The node's label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PiiRedactionFlowNode, string?>(nameof(Label));

    /// <summary>Whether the node is working (its label shimmers).</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<PiiRedactionFlowNode, bool>(nameof(IsActive));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    private ArcGlyph? _glyph;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ArcGlyph>("PART_Glyph");
        ApplyGlyphBrush();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(ActivePseudoClass, IsActive);
        }
        else if (change.Property == GlyphBrushProperty)
        {
            ApplyGlyphBrush();
        }
    }

    /// <summary>A mark color of the app's own wins over the theme's muted and active colors.</summary>
    private void ApplyGlyphBrush()
    {
        if (_glyph is null)
        {
            return;
        }
        if (GlyphBrush is { } brush)
        {
            _glyph.Foreground = brush;
        }
        else
        {
            _glyph.ClearValue(ArcGlyph.ForegroundProperty);
        }
    }
}

/// <summary>
/// A connector of the flow strip: a hairline between two nodes and, during a hop, one dot travelling along it. The dot's
/// position is drawn from <see cref="Progress"/> (0 at the start, 1 at the end; <see cref="IsReversed"/> runs it right
/// to left), so a hop is one tween in <see cref="PiiRedaction"/> and a frame only redraws.
/// </summary>
internal sealed class PiiRedactionConnector : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, double>(nameof(Progress));

    public static readonly StyledProperty<bool> IsDotVisibleProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, bool>(nameof(IsDotVisible));

    public static readonly StyledProperty<bool> IsReversedProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, bool>(nameof(IsReversed));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, IBrush?>(nameof(DotBrush));

    public static readonly StyledProperty<double> LineWidthProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, double>(nameof(LineWidth), 1);

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<PiiRedactionConnector, double>(nameof(DotSize), 6);

    private ImmutablePen? _pen;
    private IBrush? _penBrush;
    private double _penWidth;

    static PiiRedactionConnector()
    {
        AffectsRender<PiiRedactionConnector>(ProgressProperty, IsDotVisibleProperty, IsReversedProperty, LineBrushProperty,
            DotBrushProperty, LineWidthProperty, DotSizeProperty);
    }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public bool IsDotVisible { get => GetValue(IsDotVisibleProperty); set => SetValue(IsDotVisibleProperty, value); }

    public bool IsReversed { get => GetValue(IsReversedProperty); set => SetValue(IsReversedProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? DotBrush { get => GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    public double LineWidth { get => GetValue(LineWidthProperty); set => SetValue(LineWidthProperty, value); }

    public double DotSize { get => GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }

    /// <summary>Where the dot's center is drawn, from the left (tests).</summary>
    internal double DotX
    {
        get
        {
            var r = DotSize / 2;
            var span = Math.Max(0, Bounds.Width - DotSize);
            var p = Math.Clamp(Progress, 0, 1);
            return r + span * (IsReversed ? 1 - p : p);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, DotSize);

    public override void Render(DrawingContext context)
    {
        var y = Bounds.Height / 2;
        if (LineBrush is { } line)
        {
            if (_pen is null || !ReferenceEquals(_penBrush, line) || _penWidth != LineWidth)
            {
                _penBrush = line;
                _penWidth = LineWidth;
                _pen = new ImmutablePen(line.ToImmutable(), LineWidth);
            }
            context.DrawLine(_pen, new Point(0, y), new Point(Bounds.Width, y));
        }
        if (IsDotVisible && DotBrush is { } dot)
        {
            var r = DotSize / 2;
            context.DrawEllipse(dot, null, new Point(DotX, y), r, r);
        }
    }
}

/// <summary>
/// Lays out the flow strip: the person's node, a connector, the guard's node, a connector and the model's node. The two
/// first nodes keep their size, the connectors share what is left (never less than their minimum) and the model's node
/// gets the rest, so a long model name truncates with an ellipsis instead of pushing the strip wider.
/// </summary>
internal sealed class PiiRedactionFlowPanel : Panel
{
    public static readonly StyledProperty<double> ConnectorMinWidthProperty =
        AvaloniaProperty.Register<PiiRedactionFlowPanel, double>(nameof(ConnectorMinWidth), 24);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PiiRedactionFlowPanel, double>(nameof(Gap), 8);

    static PiiRedactionFlowPanel()
    {
        AffectsMeasure<PiiRedactionFlowPanel>(ConnectorMinWidthProperty, GapProperty);
    }

    public double ConnectorMinWidth { get => GetValue(ConnectorMinWidthProperty); set => SetValue(ConnectorMinWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private double[] _widths = new double[5];

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 5)
        {
            return default;
        }
        var infinite = new Size(double.PositiveInfinity, availableSize.Height);
        Children[0].Measure(infinite);
        Children[2].Measure(infinite);
        var fixedWidth = Children[0].DesiredSize.Width + Children[2].DesiredSize.Width + 4 * Gap + 2 * ConnectorMinWidth;
        var width = double.IsInfinity(availableSize.Width) ? fixedWidth + 200 : availableSize.Width;
        var modelRoom = Math.Max(0, width - fixedWidth);
        Children[4].Measure(new Size(modelRoom, availableSize.Height));
        var model = Math.Min(modelRoom, Children[4].DesiredSize.Width);
        var connectors = Math.Max(ConnectorMinWidth, (width - Children[0].DesiredSize.Width - Children[2].DesiredSize.Width - model - 4 * Gap) / 2);
        Children[1].Measure(new Size(connectors, availableSize.Height));
        Children[3].Measure(new Size(connectors, availableSize.Height));
        _widths = [Children[0].DesiredSize.Width, connectors, Children[2].DesiredSize.Width, connectors, model];
        var height = Children.Max(c => c.DesiredSize.Height);
        return new Size(double.IsInfinity(availableSize.Width) ? _widths.Sum() + 4 * Gap : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count != 5)
        {
            return finalSize;
        }
        var x = 0.0;
        for (var i = 0; i < 5; i++)
        {
            var w = _widths[i];
            if (i == 4)
            {
                w = Math.Max(0, Math.Min(w, finalSize.Width - x));
            }
            Children[i].Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w + Gap;
        }
        return finalSize;
    }
}

/// <summary>The four type cells: one row of four, or two rows of two below 520px, with a gap between them.</summary>
internal sealed class PiiRedactionCellsPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<PiiRedactionCellsPanel, int>(nameof(Columns), 4);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PiiRedactionCellsPanel, double>(nameof(Gap), 8);

    static PiiRedactionCellsPanel()
    {
        AffectsMeasure<PiiRedactionCellsPanel>(ColumnsProperty, GapProperty);
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private int EffectiveColumns => Math.Max(1, Columns);

    private double CellWidth(double width) =>
        Math.Max(0, (width - Gap * (EffectiveColumns - 1)) / EffectiveColumns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = EffectiveColumns;
        var width = double.IsInfinity(availableSize.Width) ? 120 * columns + Gap * (columns - 1) : availableSize.Width;
        var cell = CellWidth(width);
        var rowHeight = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(cell, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }
        var rows = (Children.Count + columns - 1) / columns;
        return new Size(width, rows == 0 ? 0 : rows * rowHeight + (rows - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = EffectiveColumns;
        var cell = CellWidth(finalSize.Width);
        var rowHeight = Children.Count == 0 ? 0 : Children.Max(c => c.DesiredSize.Height);
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            Children[i].Arrange(new Rect(column * (cell + Gap), row * (rowHeight + Gap), cell, rowHeight));
        }
        return finalSize;
    }
}

/// <summary>
/// Fades one layer's opacity on a tween, from wherever it is (code-driven because a layer fades out in 100ms and in over
/// 160ms, and each layer goes both ways as Show original flips, which one ControlTheme transition per property cannot
/// express). Reduced motion keeps the fades; Off sets the value at once (<see cref="MotionValue{T}"/>).
/// </summary>
internal sealed class PiiRedactionFader
{
    private readonly Visual _target;
    private readonly MotionValue<double> _opacity;
    private double _goal;

    public PiiRedactionFader(Visual target, double initial)
    {
        _target = target;
        _goal = initial;
        target.Opacity = initial;
        _opacity = new MotionValue<double>(target, initial, MotionVectors.Double);
        _opacity.Changed += value => _target.Opacity = value;
    }

    /// <summary>The opacity the layer is heading to.</summary>
    public double Goal => _goal;

    /// <summary>Fades to <paramref name="target"/> over <paramref name="duration"/>; instant jumps.</summary>
    public void To(double target, TimeSpan duration, bool instant = false)
    {
        if (instant)
        {
            _goal = target;
            _opacity.Jump(target);
            _target.Opacity = target;
            return;
        }
        if (_goal == target && Math.Abs(_target.Opacity - target) < 0.001)
        {
            return;
        }
        _goal = target;
        _ = _opacity.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard));
    }
}
