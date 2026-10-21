using System.Globalization;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A color role a cell renderer draws with. Each follows the theme, the accent and light or dark.</summary>
public enum DataGridTone
{
    /// <summary>Muted: no meaning attached.</summary>
    Neutral,

    /// <summary>The accent color.</summary>
    Accent,

    /// <summary>Good news: won, paid, healthy.</summary>
    Success,

    /// <summary>Needs attention: at risk, pending, overdue soon.</summary>
    Warning,

    /// <summary>Bad news: lost, failed, overdue.</summary>
    Danger
}

/// <summary>The fill, text color, outline and glyph stroke of one <see cref="DataGridTone"/>.</summary>
/// <param name="Fill">Background of a pill or a bar.</param>
/// <param name="Foreground">Text and solid shapes.</param>
/// <param name="Border">One pixel outline for a pill.</param>
/// <param name="Stroke">Stroke for a 24 x 24 icon drawn at 16 px.</param>
public readonly record struct DataGridToneBrushes(IBrush Fill, IBrush Foreground, IPen Border, IPen Stroke);

/// <summary>
/// The colors and pens a <see cref="DataGridCellRenderer"/> draws with. They are resolved from the ArcDesign tokens and
/// follow the theme and the accent, so a renderer never holds a color of its own.
/// </summary>
public sealed class DataGridRenderPalette
{
    private readonly DataGridPalette _palette;

    internal DataGridRenderPalette(DataGridPalette palette) => _palette = palette;

    internal DataGridPalette Inner => _palette;

    /// <summary>Primary text.</summary>
    public IBrush Foreground => _palette.ForegroundBrush;

    /// <summary>Secondary text.</summary>
    public IBrush SecondaryForeground => _palette.SecondaryBrush;

    /// <summary>Muted text.</summary>
    public IBrush MutedForeground => _palette.MutedBrush;

    /// <summary>The accent color.</summary>
    public IBrush Accent => _palette.AccentBrush;

    /// <summary>A muted surface, such as the track of a progress bar.</summary>
    public IBrush SurfaceMuted => _palette.BadgeBrush;

    /// <summary>A one pixel accent line, for underlines.</summary>
    public IPen AccentLine => _palette.SelectionPen;

    /// <summary>A one pixel accent line at lower strength, for resting underlines.</summary>
    public IPen AccentLineSubtle => _palette.LinkLine;

    /// <summary>Stroke for a 24 x 24 icon drawn at 16 px, in the secondary text color.</summary>
    public IPen IconStroke => _palette.Tone(DataGridTone.Neutral).Stroke;

    /// <summary>The brushes and pens of a tone.</summary>
    public DataGridToneBrushes Tone(DataGridTone tone) => _palette.Tone(tone);
}

/// <summary>Everything a <see cref="DataGridCellRenderer"/> needs to draw one cell.</summary>
public readonly struct DataGridCellRenderContext
{
    private readonly DataGrid _grid;
    private readonly DataGridCellsLayer _layer;
    private readonly object _key;
    private readonly int _displayRow;
    private readonly int _accessor;
    private readonly int _version;

    internal DataGridCellRenderContext(DataGrid grid, DataGridCellsLayer layer, DrawingContext drawing, Rect bounds, object? value, object row, object key,
                                       int displayRow, int accessor, int version, string text, CultureInfo culture, DataGridRenderPalette palette,
                                       double fontSize, double smallFontSize, Point? pointer)
    {
        _grid = grid;
        _displayRow = displayRow;
        _layer = layer;
        _key = key;
        _accessor = accessor;
        _version = version;
        Drawing = drawing;
        Bounds = bounds;
        Value = value;
        Row = row;
        Text = text;
        Culture = culture;
        Palette = palette;
        FontSize = fontSize;
        SmallFontSize = smallFontSize;
        Pointer = pointer;
    }

    /// <summary>The drawing context; everything is drawn in its coordinates, which <see cref="Bounds"/> and <see cref="Pointer"/> share.</summary>
    public DrawingContext Drawing { get; }

    /// <summary>The content area of the cell: the cell without its inline padding, as high as the row.</summary>
    public Rect Bounds { get; }

    /// <summary>The normalized value: a string, a <see cref="decimal"/>, a <see cref="DateOnly"/> or null.</summary>
    public object? Value { get; }

    /// <summary>The row object (a <see cref="DataGridRow"/> or one of your own classes).</summary>
    public object Row { get; }

    /// <summary>The column's text for the value, from <see cref="DataGridCellRenderer.GetText"/>.</summary>
    public string Text { get; }

    /// <summary>The culture the grid formats with.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Theme colors and pens.</summary>
    public DataGridRenderPalette Palette { get; }

    /// <summary>The font size of the cell text.</summary>
    public double FontSize { get; }

    /// <summary>The smaller font size, for pills and captions.</summary>
    public double SmallFontSize { get; }

    /// <summary>The pointer position (in the coordinates of <see cref="Bounds"/>) while it is over this cell, else null.</summary>
    public Point? Pointer { get; }

    /// <summary>False when edits to this cell are refused (a read-only column or cell, or a grid with a data source), so a renderer can look disabled.</summary>
    public bool IsEditable => _grid.IsCellEditable(_displayRow, _accessor);

    /// <summary>
    /// The width of one line of text, trimmed to <paramref name="maxWidth"/>. Draw with <see cref="DrawText"/> using the same
    /// <paramref name="part"/>, flags and width, so the layout is built once and reused across frames.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxWidth">Where the text is trimmed with an ellipsis.</param>
    /// <param name="medium">Medium weight.</param>
    /// <param name="small">The smaller font size.</param>
    /// <param name="part">Tells several texts of one cell apart (0, 1, 2, ...).</param>
    public double MeasureText(string text, double maxWidth, bool medium = false, bool small = false, int part = 0)
    {
        var layout = _layer.RendererText(_key, _accessor, part, _version, text, maxWidth, null, medium, small ? SmallFontSize : FontSize);
        return layout is null ? 0 : Math.Min(maxWidth, layout.WidthIncludingTrailingWhitespace);
    }

    /// <summary>
    /// Draws one line of text starting at <paramref name="x"/>, centered vertically in <see cref="Bounds"/> and trimmed to
    /// <paramref name="maxWidth"/>; returns the width drawn.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="x">Left edge, in the coordinates of <see cref="Drawing"/>.</param>
    /// <param name="maxWidth">Where the text is trimmed with an ellipsis.</param>
    /// <param name="brush">Text color; take it from <see cref="Palette"/> so it follows the theme.</param>
    /// <param name="medium">Medium weight.</param>
    /// <param name="small">The smaller font size.</param>
    /// <param name="part">Tells several texts of one cell apart (0, 1, 2, ...).</param>
    public double DrawText(string text, double x, double maxWidth, IBrush brush, bool medium = false, bool small = false, int part = 0)
    {
        var layout = _layer.RendererText(_key, _accessor, part, _version, text, maxWidth, brush, medium, small ? SmallFontSize : FontSize);
        if (layout is null)
        {
            return 0;
        }
        layout.Draw(Drawing, new Point(x, Math.Round(Bounds.Y + (Bounds.Height - layout.Height) / 2)));
        return Math.Min(maxWidth, layout.WidthIncludingTrailingWhitespace);
    }
}

/// <summary>A pointer press over a cell, for <see cref="DataGridCellRenderer.OnPointerPressed"/>.</summary>
public readonly struct DataGridCellPointerContext
{
    private readonly DataGrid _grid;
    private readonly DataGridCellsLayer _layer;
    private readonly object _key;
    private readonly int _displayRow;
    private readonly int _accessor;
    private readonly int _version;

    internal DataGridCellPointerContext(DataGrid grid, DataGridCellsLayer layer, Rect bounds, Point position, object? value, object row, object key,
                                        int displayRow, int accessor, int version, string text, DataGridColumn column, CultureInfo culture,
                                        double fontSize, double smallFontSize)
    {
        _grid = grid;
        _displayRow = displayRow;
        _layer = layer;
        _key = key;
        _accessor = accessor;
        _version = version;
        Bounds = bounds;
        Position = position;
        Value = value;
        Row = row;
        Text = text;
        Column = column;
        Culture = culture;
        FontSize = fontSize;
        SmallFontSize = smallFontSize;
    }

    /// <summary>The content area of the cell, in the same coordinates as <see cref="Position"/>.</summary>
    public Rect Bounds { get; }

    /// <summary>The pointer position.</summary>
    public Point Position { get; }

    /// <summary>The normalized value.</summary>
    public object? Value { get; }

    /// <summary>The row object.</summary>
    public object Row { get; }

    /// <summary>The column's text for the value.</summary>
    public string Text { get; }

    /// <summary>The column.</summary>
    public DataGridColumn Column { get; }

    /// <summary>The culture the grid formats with.</summary>
    public CultureInfo Culture { get; }

    /// <summary>The font size of the cell text.</summary>
    public double FontSize { get; }

    /// <summary>The smaller font size.</summary>
    public double SmallFontSize { get; }

    /// <summary>The width of one line of text as <see cref="DataGridCellRenderContext.MeasureText"/> measures it, for hit testing.</summary>
    public double MeasureText(string text, double maxWidth, bool medium = false, bool small = false, int part = 0)
    {
        var layout = _layer.RendererText(_key, _accessor, part, _version, text, maxWidth, null, medium, small ? SmallFontSize : FontSize);
        return layout is null ? 0 : Math.Min(maxWidth, layout.WidthIncludingTrailingWhitespace);
    }

    /// <summary>False when edits to this cell are refused (a read-only column or cell, or a grid with a data source).</summary>
    public bool IsEditable => _grid.IsCellEditable(_displayRow, _accessor);

    /// <summary>
    /// Writes a new value to this cell through the path an edit takes: <see cref="DataGrid.CellEditEnding"/> can refuse it, it
    /// is one undo step and raises <see cref="DataGrid.CellsChanged"/>. The value is a string, a <see cref="decimal"/>, a
    /// <see cref="DateOnly"/>, a <see cref="bool"/> or null, matching the column's type. Returns whether the cell changed.
    /// </summary>
    public bool SetValue(object? value) => _grid.SetCellValue(_displayRow, _accessor, value);

    /// <summary>Raises <see cref="DataGrid.CellInvoked"/> for this cell.</summary>
    /// <param name="part">Tells the actionable things of one cell apart, such as "open" or "delete"; null for a cell with one.</param>
    public void Invoke(string? part = null) => _grid.RaiseCellInvoked(Row, _key, Column, Value, part);
}

/// <summary>
/// Draws the cells of a <see cref="DataGridColumn"/> in place of the plain text: a status badge, a progress bar, a link, an
/// icon. The grid keeps drawing rows itself, so a renderer is a few draw calls over the cell, never a control. Sorting,
/// filtering, totals, copy and export keep working on the column's value; <see cref="GetText"/> decides the text that copy
/// and screen readers see.
/// </summary>
/// <remarks>
/// A renderer is shared by every cell of its column, so it must not keep state per cell. Draw with
/// <see cref="DataGridCellRenderContext.Palette"/> so colors follow the theme.
/// </remarks>
public abstract class DataGridCellRenderer
{
    /// <summary>Raised by <see cref="NotifyChanged"/> so the column redraws.</summary>
    internal event Action? Changed;

    /// <summary>Draws one cell inside <see cref="DataGridCellRenderContext.Bounds"/>. Called every frame for the cells in view, so it should not allocate.</summary>
    public abstract void Render(in DataGridCellRenderContext context);

    /// <summary>
    /// The text of a value for copy, search display and screen readers. The default is the text the column's type shows
    /// (grouped digits, the currency symbol, the date).
    /// </summary>
    public virtual string GetText(object? value, DataGridCellFormat format, CultureInfo culture) => DataGridValueFormat.Format(value, format, culture);

    /// <summary>Whether the pointer at <see cref="DataGridCellPointerContext.Position"/> is over something that reacts to a click; the grid then shows a hand cursor and keeps repainting for hover.</summary>
    public virtual bool IsInteractiveAt(in DataGridCellPointerContext context) => false;

    /// <summary>
    /// A press over the cell. Return true to consume it (the cell is then not selected); typically call
    /// <see cref="DataGridCellPointerContext.Invoke"/> first.
    /// </summary>
    public virtual bool OnPointerPressed(in DataGridCellPointerContext context) => false;

    /// <summary>Tells the column that a property of the renderer changed, so the cells are drawn again.</summary>
    protected void NotifyChanged() => Changed?.Invoke();
}

/// <summary>Arguments of <see cref="DataGrid.CellInvoked"/>.</summary>
public sealed class DataGridCellInvokedEventArgs : RoutedEventArgs
{
    internal DataGridCellInvokedEventArgs(RoutedEvent routedEvent, object row, object? rowKey, DataGridColumn column, object? value, string? part)
        : base(routedEvent)
    {
        Row = row;
        RowKey = rowKey;
        Column = column;
        Value = value;
        Part = part;
    }

    /// <summary>The row object.</summary>
    public object Row { get; }

    /// <summary>The row's key.</summary>
    public object? RowKey { get; }

    /// <summary>The column.</summary>
    public DataGridColumn Column { get; }

    /// <summary>The cell's normalized value.</summary>
    public object? Value { get; }

    /// <summary>What was clicked, as the renderer names it (for example "open"); null when the cell has one actionable thing.</summary>
    public string? Part { get; }
}
