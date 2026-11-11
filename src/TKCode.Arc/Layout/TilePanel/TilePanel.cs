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
/// <b>Auto span</b> (<see cref="IsAutoSpan"/>, off by default). Children that set neither <see cref="RowSpanProperty">RowSpan</see>
/// nor <see cref="ColumnSpanProperty">ColumnSpan</see> get 1×1, 1×2, 2×1 or 2×2 from the panel, picked from their index and
/// <see cref="Seed"/> within <see cref="MaxRowSpan"/> / <see cref="MaxColumnSpan"/>. Such a tile goes to the first free cell
/// and takes a size that fits there, so it never leaves a hole before it; larger tiles are kept from touching each other, and
/// none straddles the last of <see cref="Rows"/>. The same children, seed and grid always give the same tiles.
/// </para>
/// <para>
/// <b>Auto fill</b> (<see cref="IsAutoFill"/>, off by default). After placement, each tile in child order grows into empty
/// cells: first to the right, one column at a time while every cell of the new column next to it is empty, then down one row
/// at a time likewise. Then every hole left is offered to the tiles around it: a neighbour on any side grows over it when the
/// whole strip it needs is empty. Tiles never pass the span limits or the grid (the grid's height is <see cref="Rows"/> or
/// the rows already used, whichever is more), so a hole stays empty only when no neighbour can cover it within its limits.
/// With <see cref="IsAutoSpan"/> and <see cref="Rows"/> set as well, the panel sizes the automatic tiles to cover the
/// <see cref="Columns"/> × <see cref="Rows"/> grid.
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

    /// <summary>
    /// Lets tiles grow into empty cells after placement: to their right and below them, then from any side into the holes left
    /// (see the remarks of <see cref="TilePanel"/>).
    /// </summary>
    public static readonly StyledProperty<bool> IsAutoFillProperty =
        AvaloniaProperty.Register<TilePanel, bool>(nameof(IsAutoFill));

    /// <summary>
    /// Lets the panel size the children that set neither <c>RowSpan</c> nor <c>ColumnSpan</c>: 1×1, 1×2, 2×1 or 2×2, picked
    /// from the child's index and <see cref="Seed"/> (see the remarks of <see cref="TilePanel"/>).
    /// </summary>
    public static readonly StyledProperty<bool> IsAutoSpanProperty =
        AvaloniaProperty.Register<TilePanel, bool>(nameof(IsAutoSpan));

    /// <summary>
    /// Picks the arrangement when <see cref="IsAutoSpan"/> is on: the same seed always gives the same tiles; another seed gives
    /// another arrangement.
    /// </summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<TilePanel, int>(nameof(Seed));

    /// <summary>How many rows a child covers (at least 1; attached).</summary>
    public static readonly AttachedProperty<int> RowSpanProperty =
        AvaloniaProperty.RegisterAttached<TilePanel, Control, int>("RowSpan", 1, coerce: CoerceSpan);

    /// <summary>How many columns a child covers (at least 1; attached).</summary>
    public static readonly AttachedProperty<int> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<TilePanel, Control, int>("ColumnSpan", 1, coerce: CoerceSpan);

    // Share of automatic tiles that get 2×2, and 1×2 or 2×1, when no grid has to be filled (an extra 0.6 cell per tile).
    private const double BigShare = 0.12;
    private const double MediumShare = 0.24;

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
            MaxColumnSpanProperty, IsAutoFillProperty, IsAutoSpanProperty, SeedProperty);
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

    /// <inheritdoc cref="IsAutoSpanProperty"/>
    public bool IsAutoSpan
    {
        get => GetValue(IsAutoSpanProperty);
        set => SetValue(IsAutoSpanProperty, value);
    }

    /// <inheritdoc cref="SeedProperty"/>
    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
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

        // Placement on an (along, across) grid of owners (child index, -1 = free): "across" is the fixed axis, "along" the one
        // the grid grows on.
        var owner = new List<int[]>();
        var alongUsed = 0;
        var autoSpan = IsAutoSpan;
        var isAuto = autoSpan ? new bool[count] : null;
        var spread = autoSpan ? new bool[count] : null;
        var autoLeft = 0;
        if (autoSpan)
        {
            for (var i = 0; i < count; i++)
            {
                var child = children[i];
                isAuto![i] = child.IsVisible && !child.IsSet(RowSpanProperty) && !child.IsSet(ColumnSpanProperty);
                autoLeft += isAuto[i] ? 1 : 0;
            }
        }
        // Automatic tiles stay within the shared rows (row-major with Rows set) rather than straddling their last line.
        var autoAlongLimit = rowMajor && rows > 0 ? rows : int.MaxValue;
        // With auto fill and a fixed grid, the cells the automatic tiles may add beyond one each so that the grid is covered.
        var hasBudget = IsAutoFill && autoLeft > 0 && rowMajor && rows > 0;
        var budget = hasBudget ? Math.Max(0, across * rows - MinimumCells(children, isAuto!, maxRowSpan, maxColumnSpan)) : 0;
        var seed = Seed;
        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                _row[i] = -1;
                continue;
            }
            int along, at, spanAlong, spanAcross;
            if (isAuto is not null && isAuto[i])
            {
                (along, at) = FirstFree(owner, across);
                var spans = new SpanChoice(rowMajor, across, autoAlongLimit, maxRowSpan, maxColumnSpan);
                (spanAlong, spanAcross) = ChooseSpan(owner, spread!, spans, seed, i, along, at, budget, autoLeft, hasBudget);
                var added = spanAlong * spanAcross - 1;
                spread![i] = added > 0;
                budget = Math.Max(0, budget - added);
                autoLeft--;
            }
            else
            {
                var rowSpan = Math.Min(GetRowSpan(child), maxRowSpan);
                var columnSpan = Math.Min(GetColumnSpan(child), maxColumnSpan);
                spanAcross = rowMajor ? columnSpan : rowSpan;
                spanAlong = rowMajor ? rowSpan : columnSpan;
                (along, at) = FirstFit(owner, across, spanAlong, spanAcross);
            }
            Occupy(owner, across, along, at, spanAlong, spanAcross, i);
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
                while (spanAcross < maxAcross && at + spanAcross < across && IsFree(owner, along, spanAlong, at + spanAcross, 1))
                {
                    Occupy(owner, across, along, at + spanAcross, spanAlong, 1, i);
                    spanAcross++;
                }
                while (spanAlong < maxAlong && along + spanAlong < alongLimit && IsFree(owner, along + spanAlong, 1, at, spanAcross))
                {
                    Occupy(owner, across, along + spanAlong, at, 1, spanAcross, i);
                    spanAlong++;
                }
                alongUsed = Math.Max(alongUsed, along + spanAlong);
                Store(i, rowMajor, along, at, spanAlong, spanAcross);
            }
            FillHoles(owner, rowMajor, across, alongLimit, maxAlong, maxAcross);
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

    private static (int Along, int At) FirstFit(List<int[]> owner, int across, int spanAlong, int spanAcross)
    {
        for (var along = 0; ; along++)
        {
            for (var at = 0; at + spanAcross <= across; at++)
            {
                if (IsFree(owner, along, spanAlong, at, spanAcross))
                {
                    return (along, at);
                }
            }
        }
    }

    /// <summary>The first free cell in reading order (rows, then columns within a row; the other way round column-major).</summary>
    private static (int Along, int At) FirstFree(List<int[]> owner, int across)
    {
        for (var along = 0; along < owner.Count; along++)
        {
            var line = owner[along];
            for (var at = 0; at < across; at++)
            {
                if (line[at] < 0)
                {
                    return (along, at);
                }
            }
        }
        return (owner.Count, 0);
    }

    private static bool IsFree(List<int[]> owner, int along, int spanAlong, int at, int spanAcross)
    {
        for (var a = along; a < along + spanAlong && a < owner.Count; a++)
        {
            var line = owner[a];
            for (var c = at; c < at + spanAcross; c++)
            {
                if (line[c] >= 0)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void Occupy(List<int[]> owner, int across, int along, int at, int spanAlong, int spanAcross, int index)
    {
        while (owner.Count < along + spanAlong)
        {
            var line = new int[across];
            Array.Fill(line, -1);
            owner.Add(line);
        }
        for (var a = along; a < along + spanAlong; a++)
        {
            for (var c = at; c < at + spanAcross; c++)
            {
                owner[a][c] = index;
            }
        }
    }

    private static int OwnerAt(List<int[]> owner, int across, int along, int at) =>
        along < 0 || at < 0 || at >= across || along >= owner.Count ? -1 : owner[along][at];

    /// <summary>The cells the visible tiles cover at their smallest: the explicit spans, and one cell per automatic tile.</summary>
    private static int MinimumCells(Controls children, bool[] isAuto, int maxRowSpan, int maxColumnSpan)
    {
        var cells = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsVisible)
            {
                continue;
            }
            cells += isAuto[i] ? 1 : Math.Min(GetRowSpan(child), maxRowSpan) * Math.Min(GetColumnSpan(child), maxColumnSpan);
        }
        return cells;
    }

    /// <summary>The grid and limits an automatic tile's size is chosen within.</summary>
    private readonly record struct SpanChoice(bool RowMajor, int Across, int AlongLimit, int MaxRowSpan, int MaxColumnSpan);

    /// <summary>
    /// The (along, across) spans of an automatic tile at its first free cell. A size class is drawn from the child's index and
    /// the seed (2×2, then 1×2 or 2×1, then 1×1); a class that does not fit there, breaks a limit or would touch another larger
    /// automatic tile falls back to the next smaller one, so 1×1 always remains. With a budget (auto fill on a fixed grid) the
    /// odds follow the cells still to cover per remaining tile, and a size never adds more cells than are left.
    /// </summary>
    private static (int Along, int Across) ChooseSpan(List<int[]> owner, bool[] spread, SpanChoice grid, int seed, int index,
        int along, int at, int budget, int autoLeft, bool hasBudget)
    {
        double big;
        double medium;
        var keepApart = true;
        var needed = 0;
        if (hasBudget)
        {
            var perTile = autoLeft > 0 ? (double)budget / autoLeft : 0;
            if (perTile >= 3)
            {
                (big, medium) = (1, 0);
            }
            else if (perTile >= 1)
            {
                big = (perTile - 1) / 2;
                medium = 1 - big;
            }
            else
            {
                // Same mix as without a budget, scaled to add perTile cells per tile on average.
                big = BigShare * perTile / (3 * BigShare + MediumShare);
                medium = MediumShare * perTile / (3 * BigShare + MediumShare);
            }
            // A tight budget needs every tile it can get: larger tiles may then touch.
            keepApart = perTile < 1;
            // Cells the later tiles cannot add even if all of them were 2×2.
            needed = Math.Max(0, budget - 3 * (autoLeft - 1));
        }
        else
        {
            (big, medium) = (BigShare, MediumShare);
        }

        var draw = LayoutSeed.Unit(seed, index);
        var wideFirst = LayoutSeed.Unit(seed, index, 1) < 0.5;
        var size = draw < big ? 3 : draw < big + medium ? 1 : 0;
        if (needed >= 2)
        {
            size = 3;
        }
        else if (needed == 1)
        {
            size = Math.Max(size, 1);
        }

        // (rowSpan, columnSpan) candidates from the drawn class down to 1×1.
        Span<(int Rows, int Columns)> candidates = stackalloc (int, int)[4];
        var n = 0;
        if (size == 3)
        {
            candidates[n++] = (2, 2);
        }
        if (size >= 1)
        {
            candidates[n++] = wideFirst ? (1, 2) : (2, 1);
            candidates[n++] = wideFirst ? (2, 1) : (1, 2);
        }
        for (var k = 0; k < n; k++)
        {
            var (rowSpan, columnSpan) = candidates[k];
            if (rowSpan > grid.MaxRowSpan || columnSpan > grid.MaxColumnSpan)
            {
                continue;
            }
            if (hasBudget && rowSpan * columnSpan - 1 > budget)
            {
                continue;
            }
            var spanAlong = grid.RowMajor ? rowSpan : columnSpan;
            var spanAcross = grid.RowMajor ? columnSpan : rowSpan;
            if (at + spanAcross > grid.Across || (along < grid.AlongLimit && along + spanAlong > grid.AlongLimit))
            {
                continue;
            }
            if (!IsFree(owner, along, spanAlong, at, spanAcross))
            {
                continue;
            }
            if (keepApart && TouchesLarger(owner, spread, grid.Across, along, at, spanAlong, spanAcross))
            {
                continue;
            }
            return (spanAlong, spanAcross);
        }
        return (1, 1);
    }

    /// <summary>Whether a block has a larger automatic tile on its ring of neighbouring cells (corners included).</summary>
    private static bool TouchesLarger(List<int[]> owner, bool[] spread, int across, int along, int at, int spanAlong, int spanAcross)
    {
        for (var a = along - 1; a <= along + spanAlong; a++)
        {
            for (var c = at - 1; c <= at + spanAcross; c++)
            {
                var inside = a >= along && a < along + spanAlong && c >= at && c < at + spanAcross;
                if (inside)
                {
                    continue;
                }
                var o = OwnerAt(owner, across, a, c);
                if (o >= 0 && spread[o])
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Offers every empty cell of the grid (up to <paramref name="alongLimit"/>) to the tiles around it until none can grow:
    /// the neighbour before or after it on either axis grows one line over it when that whole line is empty and its span
    /// limit allows. Each growth covers at least one cell, so this ends.
    /// </summary>
    private void FillHoles(List<int[]> owner, bool rowMajor, int across, int alongLimit, int maxAlong, int maxAcross)
    {
        bool grew;
        do
        {
            grew = false;
            for (var along = 0; along < alongLimit; along++)
            {
                for (var at = 0; at < across; at++)
                {
                    if (OwnerAt(owner, across, along, at) >= 0)
                    {
                        continue;
                    }
                    grew |= TryGrowInto(owner, rowMajor, across, alongLimit, maxAlong, maxAcross, along, at);
                }
            }
        }
        while (grew);
    }

    private bool TryGrowInto(List<int[]> owner, bool rowMajor, int across, int alongLimit, int maxAlong, int maxAcross, int along,
        int at)
    {
        // Neighbours: before across, before along, after across, after along.
        ReadOnlySpan<(int DAlong, int DAt)> sides = [(0, -1), (-1, 0), (0, 1), (1, 0)];
        foreach (var (dAlong, dAt) in sides)
        {
            var i = OwnerAt(owner, across, along + dAlong, at + dAt);
            if (i < 0)
            {
                continue;
            }
            var tAlong = rowMajor ? _row[i] : _column[i];
            var tAt = rowMajor ? _column[i] : _row[i];
            var tSpanAlong = rowMajor ? _rowSpan[i] : _columnSpan[i];
            var tSpanAcross = rowMajor ? _columnSpan[i] : _rowSpan[i];
            if (dAlong == 0)
            {
                // The hole is beside the tile: grow across by one line over the tile's whole along-range.
                if (tSpanAcross >= maxAcross || tAlong + tSpanAlong > alongLimit)
                {
                    continue;
                }
                if (!IsFree(owner, tAlong, tSpanAlong, at, 1))
                {
                    continue;
                }
                Occupy(owner, across, tAlong, at, tSpanAlong, 1, i);
                Store(i, rowMajor, tAlong, Math.Min(tAt, at), tSpanAlong, tSpanAcross + 1);
            }
            else
            {
                // The hole is above or below the tile: grow along by one line over the tile's whole across-range.
                if (tSpanAlong >= maxAlong || !IsFree(owner, along, 1, tAt, tSpanAcross))
                {
                    continue;
                }
                Occupy(owner, across, along, tAt, 1, tSpanAcross, i);
                Store(i, rowMajor, Math.Min(tAlong, along), tAt, tSpanAlong + 1, tSpanAcross);
            }
            return true;
        }
        return false;
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
