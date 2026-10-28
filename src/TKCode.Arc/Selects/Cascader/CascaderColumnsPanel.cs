using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Lays the columns of a <see cref="Cascader"/> side by side and draws the dividers between them. The panel is as
/// wide as its columns; when a column comes or goes, the width springs to the new total (the same springs the
/// MorphSelect panel uses) while the columns keep their own positions, so the menu grows to the right.
/// <para>Why code: the total width comes from the measured columns, and a ControlTheme transition cannot
/// interpolate an Auto width.</para>
/// The theme sets the row look on this panel; every column it creates copies it.
/// </summary>
internal sealed class CascaderColumnsPanel : Control
{
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(RowHeight), 36);

    public static readonly StyledProperty<CornerRadius> RowCornerRadiusProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, CornerRadius>(nameof(RowCornerRadius));

    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, Thickness>(nameof(RowPadding));

    public static readonly StyledProperty<double> ChevronInsetProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(ChevronInset), 10);

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> PathBrushProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, IBrush?>(nameof(PathBrush));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<IBrush?> DividerBrushProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, IBrush?>(nameof(DividerBrush));

    public static readonly StyledProperty<double> DisabledOpacityProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(DisabledOpacity), 0.45);

    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(MinColumnWidth), 160);

    public static readonly StyledProperty<double> MaxColumnWidthProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(MaxColumnWidth), 280);

    public static readonly StyledProperty<double> ColumnWidthProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(ColumnWidth), double.NaN);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(ColumnGap), 6);

    public static readonly StyledProperty<double> MaxColumnHeightProperty =
        AvaloniaProperty.Register<CascaderColumnsPanel, double>(nameof(MaxColumnHeight), 300);

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public CornerRadius RowCornerRadius { get => GetValue(RowCornerRadiusProperty); set => SetValue(RowCornerRadiusProperty, value); }
    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }
    public double ChevronInset { get => GetValue(ChevronInsetProperty); set => SetValue(ChevronInsetProperty, value); }
    public IBrush? HighlightBrush { get => GetValue(HighlightBrushProperty); set => SetValue(HighlightBrushProperty, value); }
    public IBrush? PathBrush { get => GetValue(PathBrushProperty); set => SetValue(PathBrushProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }
    public double DisabledOpacity { get => GetValue(DisabledOpacityProperty); set => SetValue(DisabledOpacityProperty, value); }
    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }
    public double MaxColumnWidth { get => GetValue(MaxColumnWidthProperty); set => SetValue(MaxColumnWidthProperty, value); }

    /// <summary>Fixed width of every column; NaN sizes each column to its content.</summary>
    public double ColumnWidth { get => GetValue(ColumnWidthProperty); set => SetValue(ColumnWidthProperty, value); }
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public double MaxColumnHeight { get => GetValue(MaxColumnHeightProperty); set => SetValue(MaxColumnHeightProperty, value); }

    private readonly List<CascaderColumn> _columns = new();
    private readonly List<double> _widths = new();
    private MotionValue<double>? _width;
    private double _targetWidth;
    private bool _animate;

    static CascaderColumnsPanel()
    {
        AffectsMeasure<CascaderColumnsPanel>(ColumnWidthProperty, ColumnGapProperty, MinColumnWidthProperty,
            MaxColumnWidthProperty, MaxColumnHeightProperty, RowPaddingProperty);
        AffectsRender<CascaderColumnsPanel>(DividerBrushProperty, ColumnGapProperty);
        ClipToBoundsProperty.OverrideDefaultValue<CascaderColumnsPanel>(true);
    }

    public IReadOnlyList<CascaderColumn> Columns => _columns;

    /// <summary>Total width the columns want, which the animated width settles on.</summary>
    public double TargetWidth => _targetWidth;

    /// <summary>The width the panel currently reports (it springs to <see cref="TargetWidth"/>).</summary>
    public double CurrentWidth => _width?.Value ?? _targetWidth;

    /// <summary>True while the width is still moving toward <see cref="TargetWidth"/>.</summary>
    public bool IsWidthAnimating => _width?.IsAnimating == true;

    /// <summary>
    /// Replaces the columns. With <paramref name="animate"/> the width springs from where it is (a running spring
    /// is redirected, keeping its velocity); without it, the menu is laid out in one go (first frame of an open).
    /// </summary>
    public void SetColumns(IReadOnlyList<CascaderColumn> columns, bool animate)
    {
        _animate = animate;
        var same = columns.Count == _columns.Count;
        for (var i = 0; same && i < columns.Count; i++)
        {
            same = ReferenceEquals(columns[i], _columns[i]);
        }
        if (!same)
        {
            foreach (var column in _columns)
            {
                if (!columns.Contains(column))
                {
                    VisualChildren.Remove(column);
                    LogicalChildren.Remove(column);
                }
            }
            _columns.Clear();
            _columns.AddRange(columns);
            foreach (var column in _columns)
            {
                if (!VisualChildren.Contains(column))
                {
                    Adopt(column);
                    VisualChildren.Add(column);
                    LogicalChildren.Add(column);
                }
            }
        }
        InvalidateMeasure();
    }

    private void Adopt(CascaderColumn column)
    {
        var options = column.Options;
        options.Bind(SelectOptionView.RowHeightProperty, this.GetObservable(RowHeightProperty));
        options.Bind(SelectOptionView.RowCornerRadiusProperty, this.GetObservable(RowCornerRadiusProperty));
        options.Bind(SelectOptionView.RowPaddingProperty, this.GetObservable(RowPaddingProperty));
        options.Bind(SelectOptionView.CheckInsetProperty, this.GetObservable(ChevronInsetProperty));
        options.Bind(SelectOptionView.HighlightBrushProperty, this.GetObservable(HighlightBrushProperty));
        options.Bind(SelectOptionView.PathBrushProperty, this.GetObservable(PathBrushProperty));
        options.Bind(SelectOptionView.ForegroundProperty, this.GetObservable(ForegroundProperty));
        options.Bind(SelectOptionView.CheckForegroundProperty, this.GetObservable(ForegroundProperty));
        options.Bind(SelectOptionView.MutedForegroundProperty, this.GetObservable(MutedForegroundProperty));
        options.Bind(SelectOptionView.DisabledOpacityProperty, this.GetObservable(DisabledOpacityProperty));
        column.Scroll.Bind(MaxHeightProperty, this.GetObservable(MaxColumnHeightProperty));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var fontSize = TextElement.GetFontSize(this);
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyle.Normal, FontWeight.Normal);
        var fixedWidth = ColumnWidth;
        var gap = ColumnGap;
        _widths.Clear();
        var total = 0.0;
        var height = 0.0;
        for (var i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var width = double.IsNaN(fixedWidth)
                ? column.MeasureContentWidth(MinColumnWidth, MaxColumnWidth, fontSize, typeface, RowPadding)
                : fixedWidth;
            _widths.Add(width);
            column.Measure(new Size(width, double.PositiveInfinity));
            height = Math.Max(height, column.DesiredSize.Height);
            total += width + (i > 0 ? gap : 0);
        }
        var changed = Math.Abs(total - _targetWidth) > 0.01;
        _targetWidth = total;
        if (_width is null)
        {
            _width = new MotionValue<double>(this, total, MotionVectors.Double);
            _width.Changed += _ => InvalidateMeasure();
        }
        else if (changed)
        {
            if (_animate)
            {
                _ = _width.AnimateTo(total, MotionSpec.Spring(total < _width.Value ? MorphSelectSurface.Narrow : MorphSelectSurface.Widen));
            }
            else
            {
                _width.Jump(total);
            }
        }
        return new Size(Math.Min(_width.Value, double.IsInfinity(availableSize.Width) ? _width.Value : availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        for (var i = 0; i < _columns.Count; i++)
        {
            var width = i < _widths.Count ? _widths[i] : _columns[i].DesiredSize.Width;
            _columns[i].Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + ColumnGap;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DividerBrush is not { } brush || _columns.Count < 2)
        {
            return;
        }
        var x = 0.0;
        var pen = new Pen(brush, 1);
        for (var i = 0; i < _columns.Count - 1; i++)
        {
            x += (i < _widths.Count ? _widths[i] : 0) + ColumnGap / 2;
            var snapped = Math.Round(x) + 0.5;
            context.DrawLine(pen, new Point(snapped, 4), new Point(snapped, Math.Max(4, Bounds.Height - 4)));
            x += ColumnGap / 2;
        }
    }
}
