using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// A tile grid like a start screen: the panel is divided into equal cells, each child covers
/// <see cref="GetRowSpan">RowSpan</see> × <see cref="GetColumnSpan">ColumnSpan</see> of them, and children go, in order, to the
/// first place where they fit without overlapping. Usable on its own or as an <see cref="ItemsControl.ItemsPanel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Grid.</b> <see cref="Columns"/> fixes the column count; 0 fits as many columns of at least <see cref="MinCellSize"/> as
/// the width allows. Children fill rows left to right, top to bottom, and the grid grows by rows as needed. When the width
/// is unbounded (a horizontal <see cref="ScrollViewer"/>) and <see cref="Columns"/> is 0, <see cref="Rows"/> fixes the row
/// count instead and children fill columns top to bottom, left to right, the grid growing by columns.
/// </para>
/// <para>
/// <b>Cells.</b> With a bounded width the columns share it; with <see cref="Rows"/> set and a bounded height the rows share
/// the height. A side that nothing sizes copies the other (square cells); when neither is sized, cells are
/// <see cref="MinCellSize"/> square. Tiles that do not fit in <see cref="Rows"/> rows continue on further rows of the same
/// size, so no tile is ever dropped.
/// </para>
/// <para>
/// <b>Placement.</b> First fit, in child order: a tile takes the first free position scanning rows from the top and, within
/// a row, columns from the left (columns then rows in the column-major case). Later small tiles can fill holes left before
/// larger ones. Spans are clamped to <see cref="MaxRowSpan"/> / <see cref="MaxColumnSpan"/> and to the grid. The result
/// depends only on the children, their spans and the grid size.
/// </para>
/// <para>
/// <b>Auto fill</b> (<see cref="IsAutoFill"/>, off by default). After placement, each tile in child order grows into empty
/// cells: first to the right, one column at a time while every cell of the new column next to it is empty, then down one row
/// at a time likewise, never past the span limits or the grid (the grid's height is <see cref="Rows"/> or the rows already
/// used, whichever is more). Holes that no tile can reach stay empty.
/// </para>
/// </remarks>
public class TilePanel : Panel
{
    /// <summary>Number of columns; 0 fits as many columns of at least <see cref="MinCellSize"/> as the width allows.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<TilePanel, int>(nameof(Columns), coerce: CoerceCount);

    /// <summary>
    /// Number of rows the height is shared between (tiles that do not fit continue on further rows); 0 sizes rows like
    /// columns. Also fixes the row count when the grid grows sideways (unbounded width and <see cref="Columns"/> 0).
    /// </summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<TilePanel, int>(nameof(Rows), coerce: CoerceCount);

    /// <summary>The gap between neighbouring cells.</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<TilePanel, double>(nameof(Spacing), 8, coerce: CoerceNonNegative);

    /// <summary>The narrowest cell when <see cref="Columns"/> is 0, and the cell size when neither side is otherwise sized.</summary>
    public static readonly StyledProperty<double> MinCellSizeProperty =
        AvaloniaProperty.Register<TilePanel, double>(nameof(MinCellSize), 96, coerce: CoerceNonNegative);

    /// <summary>The most rows a tile may span; 0 means no limit beyond the grid.</summary>
    public static readonly StyledProperty<int> MaxRowSpanProperty =
        AvaloniaProperty.Register<TilePanel, int>(nameof(MaxRowSpan), coerce: CoerceCount);

    /// <summary>The most columns a tile may span; 0 means no limit beyond the grid.</summary>
    public static readonly StyledProperty<int> MaxColumnSpanProperty =
        AvaloniaProperty.Register<TilePanel, int>(nameof(MaxColumnSpan), coerce: CoerceCount);

    /// <summary>Lets tiles grow into the empty cells to their right and below them after placement (see the remarks of <see cref="TilePanel"/>).</summary>
    public static readonly StyledProperty<bool> IsAutoFillProperty =
        AvaloniaProperty.Register<TilePanel, bool>(nameof(IsAutoFill));

    /// <summary>How many rows a child covers (at least 1; attached).</summary>
    public static readonly AttachedProperty<int> RowSpanProperty =
        AvaloniaProperty.RegisterAttached<TilePanel, Control, int>("RowSpan", 1, coerce: CoerceSpan);

    /// <summary>How many columns a child covers (at least 1; attached).</summary>
    public static readonly AttachedProperty<int> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<TilePanel, Control, int>("ColumnSpan", 1, coerce: CoerceSpan);

    // Placement of each child from the last measure (indexed like Children): cell row/column and spans; row -1 = collapsed.
    private int[] _row = [];
    private int[] _column = [];
    private int[] _rowSpan = [];
    private int[] _columnSpan = [];
    private int _usedColumns;
    private int _usedRows;
    private double _cellWidth;
    private double _cellHeight;
    private bool _widthShared;
    private bool _heightShared;
    private int _sharedRows;

    static TilePanel()
    {
        AffectsMeasure<TilePanel>(ColumnsProperty, RowsProperty, SpacingProperty, MinCellSizeProperty, MaxRowSpanProperty,
            MaxColumnSpanProperty, IsAutoFillProperty);
        AffectsParentMeasure<TilePanel>(RowSpanProperty, ColumnSpanProperty);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="RowsProperty"/>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <inheritdoc cref="SpacingProperty"/>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc cref="MinCellSizeProperty"/>
    public double MinCellSize
    {
        get => GetValue(MinCellSizeProperty);
        set => SetValue(MinCellSizeProperty, value);
    }

    /// <inheritdoc cref="MaxRowSpanProperty"/>
    public int MaxRowSpan
    {
        get => GetValue(MaxRowSpanProperty);
        set => SetValue(MaxRowSpanProperty, value);
    }

    /// <inheritdoc cref="MaxColumnSpanProperty"/>
    public int MaxColumnSpan
    {
        get => GetValue(MaxColumnSpanProperty);
        set => SetValue(MaxColumnSpanProperty, value);
    }

    /// <inheritdoc cref="IsAutoFillProperty"/>
    public bool IsAutoFill
    {
        get => GetValue(IsAutoFillProperty);
        set => SetValue(IsAutoFillProperty, value);
    }

    /// <summary>The number of columns of the last layout pass.</summary>
    public int ColumnCount => _usedColumns;

    /// <summary>The number of rows of the last layout pass.</summary>
    public int RowCount => _usedRows;

    /// <summary>How many rows <paramref name="control"/> covers.</summary>
    public static int GetRowSpan(Control control) => control.GetValue(RowSpanProperty);

    /// <summary>Sets how many rows <paramref name="control"/> covers.</summary>
    public static void SetRowSpan(Control control, int value) => control.SetValue(RowSpanProperty, value);

    /// <summary>How many columns <paramref name="control"/> covers.</summary>
    public static int GetColumnSpan(Control control) => control.GetValue(ColumnSpanProperty);

    /// <summary>Sets how many columns <paramref name="control"/> covers.</summary>
    public static void SetColumnSpan(Control control, int value) => control.SetValue(ColumnSpanProperty, value);

    /// <summary>
    /// The cell a child was placed at in the last layout pass, with the spans it got after clamping and auto fill; null for a
    /// collapsed child or one that is not a child of this panel.
    /// </summary>
    public TilePlacement? GetPlacement(Control child)
    {
        var index = Children.IndexOf(child);
        if (index < 0 || index >= _row.Length || _row[index] < 0)
        {
            return null;
        }
        return new TilePlacement(_row[index], _column[index], _rowSpan[index], _columnSpan[index]);
    }

    private static int CoerceCount(AvaloniaObject sender, int value) => Math.Max(0, value);

    private static int CoerceSpan(AvaloniaObject sender, int value) => Math.Max(1, value);

    private static double CoerceNonNegative(AvaloniaObject sender, double value) => double.IsNaN(value) ? 0 : Math.Max(0, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var spacing = Spacing;
        var children = Children;
        var count = children.Count;
        EnsureCapacity(count);

        var rows = Rows;
        var widthFinite = double.IsFinite(availableSize.Width);
        var heightFinite = double.IsFinite(availableSize.Height);
        // Row-major (columns fixed, grid grows down) unless the width is unbounded with an automatic column count and a row count.
        var rowMajor = Columns > 0 || widthFinite || rows <= 0;
        var maxRowSpan = MaxRowSpan > 0 ? MaxRowSpan : int.MaxValue;
        var maxColumnSpan = MaxColumnSpan > 0 ? MaxColumnSpan : int.MaxValue;

        int across;
        if (rowMajor)
        {
            if (Columns > 0)
            {
                across = Columns;
            }
            else if (widthFinite)
            {
                across = MinCellSize > 0
                    ? Math.Max(1, (int)Math.Floor((availableSize.Width + spacing) / (MinCellSize + spacing)))
                    : 1;
            }
            else
            {
                // Unbounded both ways with nothing fixed: a square-ish grid from the number of cells the tiles cover.
                var cells = 0;
                foreach (var child in children)
                {
                    if (child.IsVisible)
                    {
                        cells += Math.Min(GetRowSpan(child), maxRowSpan) * Math.Min(GetColumnSpan(child), maxColumnSpan);
                    }
                }
                across = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(cells)));
            }
            maxColumnSpan = Math.Min(maxColumnSpan, across);
            if (rows > 0)
            {
                maxRowSpan = Math.Min(maxRowSpan, rows);
            }
        }
        else
        {
            across = rows;
            maxRowSpan = Math.Min(maxRowSpan, across);
        }

        // Placement on an (along, across) occupancy grid: "across" is the fixed axis, "along" the one the grid grows on.
        var occupied = new List<bool[]>();
        var alongUsed = 0;
        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                _row[i] = -1;
                continue;
            }
            var rowSpan = Math.Min(GetRowSpan(child), maxRowSpan);
            var columnSpan = Math.Min(GetColumnSpan(child), maxColumnSpan);
            var spanAcross = rowMajor ? columnSpan : rowSpan;
            var spanAlong = rowMajor ? rowSpan : columnSpan;
            var (along, at) = FirstFit(occupied, across, spanAlong, spanAcross);
            Occupy(occupied, across, along, at, spanAlong, spanAcross);
            alongUsed = Math.Max(alongUsed, along + spanAlong);
            Store(i, rowMajor, along, at, spanAlong, spanAcross);
        }

        if (IsAutoFill)
        {
            var alongLimit = rowMajor ? Math.Max(alongUsed, rows) : alongUsed;
            var maxAcross = rowMajor ? maxColumnSpan : maxRowSpan;
            var maxAlong = rowMajor ? maxRowSpan : maxColumnSpan;
            for (var i = 0; i < count; i++)
            {
                if (_row[i] < 0)
                {
                    continue;
                }
                var along = rowMajor ? _row[i] : _column[i];
                var at = rowMajor ? _column[i] : _row[i];
                var spanAlong = rowMajor ? _rowSpan[i] : _columnSpan[i];
                var spanAcross = rowMajor ? _columnSpan[i] : _rowSpan[i];
                while (spanAcross < maxAcross && at + spanAcross < across && IsFree(occupied, along, spanAlong, at + spanAcross, 1))
                {
                    Occupy(occupied, across, along, at + spanAcross, spanAlong, 1);
                    spanAcross++;
                }
                while (spanAlong < maxAlong && along + spanAlong < alongLimit && IsFree(occupied, along + spanAlong, 1, at, spanAcross))
                {
                    Occupy(occupied, across, along + spanAlong, at, 1, spanAcross);
                    spanAlong++;
                }
                alongUsed = Math.Max(alongUsed, along + spanAlong);
                Store(i, rowMajor, along, at, spanAlong, spanAcross);
            }
        }

        _usedColumns = rowMajor ? across : alongUsed;
        _usedRows = rowMajor ? Math.Max(alongUsed, rows) : across;
        _sharedRows = rowMajor ? rows : across;
        _widthShared = rowMajor && widthFinite;
        _heightShared = _sharedRows > 0 && heightFinite;
        (_cellWidth, _cellHeight) = CellSize(availableSize.Width, availableSize.Height, spacing);

        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            if (_row[i] < 0)
            {
                continue;
            }
            child.Measure(new Size(Extent(_columnSpan[i], _cellWidth, spacing), Extent(_rowSpan[i], _cellHeight, spacing)));
        }

        var width = _usedColumns > 0 ? Extent(_usedColumns, _cellWidth, spacing) : 0;
        var height = _usedRows > 0 ? Extent(_usedRows, _cellHeight, spacing) : 0;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var spacing = Spacing;
        var (cellWidth, cellHeight) = CellSize(finalSize.Width, finalSize.Height, spacing);
        var children = Children;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (i >= _row.Length || _row[i] < 0)
            {
                child.Arrange(default);
                continue;
            }
            child.Arrange(new Rect(
                _column[i] * (cellWidth + spacing),
                _row[i] * (cellHeight + spacing),
                Extent(_columnSpan[i], cellWidth, spacing),
                Extent(_rowSpan[i], cellHeight, spacing)));
        }
        return finalSize;
    }

    /// <summary>The cell size for a panel size, sharing the sides that the last measure shared and copying the other.</summary>
    private (double Width, double Height) CellSize(double width, double height, double spacing)
    {
        var cellWidth = _widthShared && _usedColumns > 0 ? Math.Max(0, (width - (_usedColumns - 1) * spacing) / _usedColumns) : double.NaN;
        var cellHeight = _heightShared ? Math.Max(0, (height - (_sharedRows - 1) * spacing) / _sharedRows) : double.NaN;
        if (double.IsNaN(cellWidth) && double.IsNaN(cellHeight))
        {
            return (MinCellSize, MinCellSize);
        }
        if (double.IsNaN(cellWidth))
        {
            return (cellHeight, cellHeight);
        }
        if (double.IsNaN(cellHeight))
        {
            return (cellWidth, cellWidth);
        }
        return (cellWidth, cellHeight);
    }

    private static double Extent(int span, double cell, double spacing) => Math.Max(0, span * cell + (span - 1) * spacing);

    private static (int Along, int At) FirstFit(List<bool[]> occupied, int across, int spanAlong, int spanAcross)
    {
        for (var along = 0; ; along++)
        {
            for (var at = 0; at + spanAcross <= across; at++)
            {
                if (IsFree(occupied, along, spanAlong, at, spanAcross))
                {
                    return (along, at);
                }
            }
        }
    }

    private static bool IsFree(List<bool[]> occupied, int along, int spanAlong, int at, int spanAcross)
    {
        for (var a = along; a < along + spanAlong && a < occupied.Count; a++)
        {
            var line = occupied[a];
            for (var c = at; c < at + spanAcross; c++)
            {
                if (line[c])
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void Occupy(List<bool[]> occupied, int across, int along, int at, int spanAlong, int spanAcross)
    {
        while (occupied.Count < along + spanAlong)
        {
            occupied.Add(new bool[across]);
        }
        for (var a = along; a < along + spanAlong; a++)
        {
            for (var c = at; c < at + spanAcross; c++)
            {
                occupied[a][c] = true;
            }
        }
    }

    private void Store(int i, bool rowMajor, int along, int at, int spanAlong, int spanAcross)
    {
        _row[i] = rowMajor ? along : at;
        _column[i] = rowMajor ? at : along;
        _rowSpan[i] = rowMajor ? spanAlong : spanAcross;
        _columnSpan[i] = rowMajor ? spanAcross : spanAlong;
    }

    private void EnsureCapacity(int count)
    {
        if (_row.Length != count)
        {
            _row = new int[count];
            _column = new int[count];
            _rowSpan = new int[count];
            _columnSpan = new int[count];
        }
    }
}

/// <summary>Where a <see cref="TilePanel"/> placed a child: its top-left cell and the cells it covers.</summary>
/// <param name="Row">The zero-based row of the tile's top-left cell.</param>
/// <param name="Column">The zero-based column of the tile's top-left cell.</param>
/// <param name="RowSpan">The rows the tile covers, after clamping and auto fill.</param>
/// <param name="ColumnSpan">The columns the tile covers, after clamping and auto fill.</param>
public readonly record struct TilePlacement(int Row, int Column, int RowSpan, int ColumnSpan);
