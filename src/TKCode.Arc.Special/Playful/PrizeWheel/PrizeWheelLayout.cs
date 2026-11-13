using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Places the wheel and the reward panel: side by side, centred, when the control is at least
/// <see cref="SideBySideMinWidth"/> wide (a container query, not the window), otherwise the panel under the wheel. The
/// first child is the wheel's stage, the second the panel; a hidden panel leaves the wheel alone.
/// </summary>
internal sealed class PrizeWheelLayout : Panel
{
    public static readonly StyledProperty<double> StageMaxSizeProperty =
        AvaloniaProperty.Register<PrizeWheelLayout, double>(nameof(StageMaxSize), 480);

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<PrizeWheelLayout, double>(nameof(PanelWidth), 320);

    public static readonly StyledProperty<double> StackedPanelMaxWidthProperty =
        AvaloniaProperty.Register<PrizeWheelLayout, double>(nameof(StackedPanelMaxWidth), 480);

    public static readonly StyledProperty<double> SideBySideMinWidthProperty =
        AvaloniaProperty.Register<PrizeWheelLayout, double>(nameof(SideBySideMinWidth), 736);

    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<PrizeWheelLayout, double>(nameof(ColumnGap), 40);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<PrizeWheelLayout, double>(nameof(RowGap), 24);

    private double _stage;
    private double _panel;

    static PrizeWheelLayout()
    {
        AffectsMeasure<PrizeWheelLayout>(StageMaxSizeProperty, PanelWidthProperty, StackedPanelMaxWidthProperty,
            SideBySideMinWidthProperty, ColumnGapProperty, RowGapProperty);
    }

    public double StageMaxSize { get => GetValue(StageMaxSizeProperty); set => SetValue(StageMaxSizeProperty, value); }

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    public double StackedPanelMaxWidth { get => GetValue(StackedPanelMaxWidthProperty); set => SetValue(StackedPanelMaxWidthProperty, value); }

    public double SideBySideMinWidth { get => GetValue(SideBySideMinWidthProperty); set => SetValue(SideBySideMinWidthProperty, value); }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>True when the panel sits under the wheel.</summary>
    public bool IsStacked { get; private set; }

    private Control? Stage => Children.Count > 0 ? Children[0] : null;

    private Control? Side => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var side = Side;
        var max = StageMaxSize;
        if (side is null)
        {
            IsStacked = false;
            var limit = Math.Min(width, availableSize.Height);
            _stage = double.IsFinite(limit) ? Math.Min(limit, max) : double.IsFinite(width) ? Math.Min(width, max) : max;
            _stage = Math.Max(0, _stage);
            Stage?.Measure(new Size(_stage, _stage));
            return new Size(_stage, _stage);
        }
        IsStacked = double.IsFinite(width) && width < SideBySideMinWidth;
        if (IsStacked)
        {
            _stage = Math.Max(0, Math.Min(max, width));
            _panel = Math.Max(0, Math.Min(StackedPanelMaxWidth, width));
            Stage?.Measure(new Size(_stage, _stage));
            side.Measure(new Size(_panel, double.PositiveInfinity));
            return new Size(Math.Max(_stage, _panel), _stage + RowGap + side.DesiredSize.Height);
        }
        _panel = PanelWidth;
        _stage = double.IsFinite(width) ? Math.Max(0, Math.Min(max, width - ColumnGap - _panel)) : max;
        Stage?.Measure(new Size(_stage, _stage));
        side.Measure(new Size(_panel, double.PositiveInfinity));
        return new Size(_stage + ColumnGap + _panel, Math.Max(_stage, side.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var side = Side;
        if (side is null)
        {
            var s = Math.Min(_stage, Math.Min(finalSize.Width, finalSize.Height));
            Stage?.Arrange(new Rect((finalSize.Width - s) / 2, (finalSize.Height - s) / 2, s, s));
            return finalSize;
        }
        if (IsStacked)
        {
            Stage?.Arrange(new Rect((finalSize.Width - _stage) / 2, 0, _stage, _stage));
            side.Arrange(new Rect((finalSize.Width - _panel) / 2, _stage + RowGap, _panel, side.DesiredSize.Height));
            return finalSize;
        }
        var total = _stage + ColumnGap + _panel;
        var left = Math.Max(0, (finalSize.Width - total) / 2);
        var height = Math.Max(finalSize.Height, Math.Max(_stage, side.DesiredSize.Height));
        Stage?.Arrange(new Rect(left, (height - _stage) / 2, _stage, _stage));
        side.Arrange(new Rect(left + _stage + ColumnGap, (height - side.DesiredSize.Height) / 2, _panel, side.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>The wheel's square: the drawing fills it and a hub-sized host for the keyboard focus ring sits on the hub.</summary>
internal sealed class PrizeWheelStage : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var side = Math.Min(availableSize.Width, availableSize.Height);
        if (!double.IsFinite(side))
        {
            side = 480;
        }
        foreach (var child in Children)
        {
            child.Measure(new Size(side, side));
        }
        return new Size(side, side);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var side = Math.Min(finalSize.Width, finalSize.Height);
        var square = new Rect((finalSize.Width - side) / 2, (finalSize.Height - side) / 2, side, side);
        var hub = side * PrizeWheelFace.HubRadius * 2 / PrizeWheelFace.Box;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Arrange(i == 0 ? square : new Rect(square.Center.X - hub / 2, square.Center.Y - hub / 2, hub, hub));
        }
        return finalSize;
    }
}

/// <summary>
/// The reward slot: the idle text, a hidden sizer card and the live card share one cell. The slot is as tall as its
/// tallest child, so the layout does not jump when a card appears. The idle text is centred beside the wheel and at the
/// top under it.
/// </summary>
internal sealed class PrizeWheelSlot : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var stacked = this.FindAncestorOfType<PrizeWheelLayout>()?.IsStacked ?? false;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            if (i == 0)
            {
                // The idle text.
                var height = child.DesiredSize.Height;
                var top = stacked ? 0 : (finalSize.Height - height) / 2;
                child.Arrange(new Rect(0, top, finalSize.Width, height));
            }
            else
            {
                child.Arrange(new Rect(finalSize));
            }
        }
        return finalSize;
    }
}

/// <summary>The odds list: one column, or two (filled row by row) when it is at least <see cref="TwoColumnMinWidth"/> wide.</summary>
internal sealed class PrizeWheelOddsList : Control
{
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<PrizeWheelOddsList, double>(nameof(ColumnGap), 20);

    public static readonly StyledProperty<double> TwoColumnMinWidthProperty =
        AvaloniaProperty.Register<PrizeWheelOddsList, double>(nameof(TwoColumnMinWidth), 420);

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<PrizeWheelOddsList, IBrush?>(nameof(LineBrush));

    private readonly List<PrizeWheelOddsRow> _rows = [];
    private int _columns = 1;
    private IPen? _pen;

    static PrizeWheelOddsList()
    {
        AffectsMeasure<PrizeWheelOddsList>(ColumnGapProperty, TwoColumnMinWidthProperty);
        AffectsRender<PrizeWheelOddsList>(LineBrushProperty);
    }

    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    public double TwoColumnMinWidth { get => GetValue(TwoColumnMinWidthProperty); set => SetValue(TwoColumnMinWidthProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    /// <summary>The rows, in order.</summary>
    internal IReadOnlyList<PrizeWheelOddsRow> Rows => _rows;

    /// <summary>The number of columns at the last measure.</summary>
    internal int Columns => _columns;

    internal void Clear()
    {
        foreach (var row in _rows)
        {
            LogicalChildren.Remove(row);
            VisualChildren.Remove(row);
        }
        _rows.Clear();
        InvalidateMeasure();
    }

    internal void Add(PrizeWheelOddsRow row)
    {
        _rows.Add(row);
        LogicalChildren.Add(row);
        VisualChildren.Add(row);
        InvalidateMeasure();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty)
        {
            _pen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 320;
        _columns = width >= TwoColumnMinWidth ? 2 : 1;
        var column = ColumnWidth(width);
        var height = 0.0;
        for (var i = 0; i < _rows.Count; i += _columns)
        {
            var row = 0.0;
            for (var c = 0; c < _columns && i + c < _rows.Count; c++)
            {
                _rows[i + c].Measure(new Size(column, double.PositiveInfinity));
                row = Math.Max(row, _rows[i + c].DesiredSize.Height);
            }
            height += row;
        }
        return new Size(width, _rows.Count == 0 ? 0 : height + 1);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var column = ColumnWidth(finalSize.Width);
        var top = 1.0;
        for (var i = 0; i < _rows.Count; i += _columns)
        {
            var row = 0.0;
            for (var c = 0; c < _columns && i + c < _rows.Count; c++)
            {
                row = Math.Max(row, _rows[i + c].DesiredSize.Height);
            }
            for (var c = 0; c < _columns && i + c < _rows.Count; c++)
            {
                _rows[i + c].Arrange(new Rect(c * (column + ColumnGap), top, column, row));
            }
            top += row;
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (_rows.Count == 0 || LineBrush is null)
        {
            return;
        }
        // The list's top rule, one per column.
        _pen ??= new ImmutablePen(LineBrush.ToImmutable(), 1);
        var column = ColumnWidth(Bounds.Width);
        for (var c = 0; c < _columns && c < _rows.Count; c++)
        {
            var left = c * (column + ColumnGap);
            context.DrawLine(_pen, new Point(left, 0.5), new Point(left + column, 0.5));
        }
    }

    private double ColumnWidth(double width) => _columns == 2 ? Math.Max(0, (width - ColumnGap) / 2) : width;
}

/// <summary>The code and its Copy button on one row, or the button under the code at full width when they do not fit.</summary>
internal sealed class PrizeWheelCodeLayout : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PrizeWheelCodeLayout, double>(nameof(Gap), 8);

    private bool _stacked;

    static PrizeWheelCodeLayout()
    {
        AffectsMeasure<PrizeWheelCodeLayout>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var code = Children[0];
        var button = Children[1];
        button.Measure(Size.Infinity);
        code.Measure(Size.Infinity);
        var width = availableSize.Width;
        // Keep the code's natural width when it fits; a very narrow card puts the button under it.
        _stacked = double.IsFinite(width) && code.DesiredSize.Width + Gap + button.DesiredSize.Width > width
                   && width - Gap - button.DesiredSize.Width < 120;
        if (_stacked)
        {
            code.Measure(new Size(width, double.PositiveInfinity));
            button.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, code.DesiredSize.Height + Gap + button.DesiredSize.Height);
        }
        var rest = double.IsFinite(width) ? Math.Max(0, width - Gap - button.DesiredSize.Width) : code.DesiredSize.Width;
        code.Measure(new Size(rest, double.PositiveInfinity));
        var height = Math.Max(code.DesiredSize.Height, button.DesiredSize.Height);
        return new Size(double.IsFinite(width) ? width : rest + Gap + button.DesiredSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var code = Children[0];
        var button = Children[1];
        if (_stacked)
        {
            code.Arrange(new Rect(0, 0, finalSize.Width, code.DesiredSize.Height));
            button.Arrange(new Rect(0, code.DesiredSize.Height + Gap, finalSize.Width, button.DesiredSize.Height));
            return finalSize;
        }
        var buttonWidth = button.DesiredSize.Width;
        var rest = Math.Max(0, finalSize.Width - Gap - buttonWidth);
        code.Arrange(new Rect(0, (finalSize.Height - code.DesiredSize.Height) / 2, rest, code.DesiredSize.Height));
        button.Arrange(new Rect(finalSize.Width - buttonWidth, (finalSize.Height - button.DesiredSize.Height) / 2, buttonWidth,
            button.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>A box with a dashed hairline border around the reward code (a <see cref="Border"/> cannot dash).</summary>
internal sealed class PrizeWheelDashedBox : Decorator
{
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<PrizeWheelDashedBox>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<PrizeWheelDashedBox>();

    private static readonly ImmutableDashStyle s_dashes = new([3, 3], 0);
    private IPen? _pen;

    static PrizeWheelDashedBox()
    {
        AffectsRender<PrizeWheelDashedBox>(BorderBrushProperty, CornerRadiusProperty);
    }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BorderBrushProperty)
        {
            _pen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (BorderBrush is null)
        {
            return;
        }
        _pen ??= new ImmutablePen(BorderBrush.ToImmutable(), 1, s_dashes);
        var rect = new Rect(Bounds.Size).Deflate(0.5);
        context.DrawRectangle(Brushes.Transparent, _pen, new RoundedRect(rect, CornerRadius.TopLeft));
    }
}
