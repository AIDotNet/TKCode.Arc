// The gutter's columns, in Monaco's layout order (src/vs/editor/common/viewLayout/... editorLayoutInfo: glyphMargin,
// lineNumbers, decorationsLeft / lineDecorationsWidth with the folding chevrons; commit b57d5c9e, MIT license): columns
// before the line numbers (the glyph margin), the line numbers, then columns after them (lines decorations, folding),
// then the gap before the text. Upstream's columns are fixed options (glyphMargin, folding, lineDecorationsWidth);
// here a feature adds or enables a column with its width, a draw callback and a pointer handler. With no column
// enabled the gutter is the line numbers alone, as before.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>One visible view line of a gutter column, as its draw callback gets it.</summary>
/// <param name="ViewLineNumber">The view line.</param>
/// <param name="ModelLineNumber">The model line the view line shows.</param>
/// <param name="IsFirstOfModelLine">Whether this is the first view line of the model line (wrapped continuations are not).</param>
/// <param name="Bounds">The cell: the column's x and width, the line's y and height, relative to the view.</param>
/// <param name="HasCursor">Whether a cursor is on the view line.</param>
internal readonly record struct CodeEditorGutterLine(int ViewLineNumber, int ModelLineNumber, bool IsFirstOfModelLine, Rect Bounds, bool HasCursor);

/// <summary>A pointer event on a gutter column.</summary>
/// <param name="Event">The Avalonia event.</param>
/// <param name="ViewLineNumber">The view line under the pointer.</param>
/// <param name="ModelLineNumber">Its model line.</param>
/// <param name="Bounds">The cell under the pointer.</param>
internal sealed record CodeEditorGutterPointerEvent(PointerEventArgs Event, int ViewLineNumber, int ModelLineNumber, Rect Bounds);

/// <summary>
/// A column of the gutter. Built-in ones are the glyph margin (before the line numbers) and the lines decorations
/// column (after them), which draw the decorations' <c>GlyphMarginClassName</c> and <c>LinesDecorationsClassName</c>;
/// both start hidden. A feature shows one by setting <see cref="Width"/> and <see cref="IsVisible"/>, or adds its own
/// (the folding column) with <see cref="CodeEditorGutter.Add"/>.
/// </summary>
internal sealed class CodeEditorGutterColumn
{
    private double _width;
    private bool _isVisible;

    public CodeEditorGutterColumn(string id, double order, double width = 0, bool isVisible = true)
    {
        Id = id;
        Order = order;
        _width = width;
        _isVisible = isVisible;
    }

    /// <summary>The column's id (also the detail of the mouse target over it).</summary>
    public string Id { get; }

    /// <summary>
    /// The place: below 0 before the line numbers, from 0 after them; lower first. The built-in orders are
    /// <see cref="CodeEditorGutter.GlyphMarginOrder"/>, <see cref="CodeEditorGutter.LinesDecorationsOrder"/> and
    /// <see cref="CodeEditorGutter.FoldingOrder"/>.
    /// </summary>
    public double Order { get; }

    /// <summary>Whether the column is before the line numbers.</summary>
    public bool IsBeforeLineNumbers => Order < 0;

    /// <summary>The width in pixels.</summary>
    public double Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                _width = value;
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Whether the column takes space and draws.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                Changed?.Invoke();
            }
        }
    }

    /// <summary>Draws one visible line of the column (after the built-in drawing of margin decorations, if any).</summary>
    public Action<DrawingContext, CodeEditorGutterLine>? Draw { get; set; }

    /// <summary>Handles a pointer press on the column; true keeps the editor from selecting the line.</summary>
    public Func<CodeEditorGutterPointerEvent, bool>? PointerPressed { get; set; }

    /// <summary>The pointer moved over the column (hover effects); null when it left it.</summary>
    public Action<CodeEditorGutterPointerEvent?>? PointerMoved { get; set; }

    /// <summary>The pointer cursor over the column, or null for the default arrow.</summary>
    public Cursor? Cursor { get; set; }

    /// <summary>Raised when the width or visibility changed (the editor lays its text out again).</summary>
    internal event Action? Changed;

    /// <summary>Asks the gutter to draw again (e.g. after a hover state changed).</summary>
    public void Invalidate() => Invalidated?.Invoke();

    internal event Action? Invalidated;

    internal double EffectiveWidth => _isVisible ? Math.Max(0, _width) : 0;
}

/// <summary>The gutter columns of one editor.</summary>
internal sealed class CodeEditorGutter
{
    /// <summary>The order of the glyph margin (before the line numbers).</summary>
    public const double GlyphMarginOrder = -100;

    /// <summary>The order of the lines decorations column (right after the line numbers).</summary>
    public const double LinesDecorationsOrder = 100;

    /// <summary>The order of the folding column (after the lines decorations).</summary>
    public const double FoldingOrder = 200;

    /// <summary>The id of the glyph margin.</summary>
    public const string GlyphMarginId = "glyphMargin";

    /// <summary>The id of the lines decorations column.</summary>
    public const string LinesDecorationsId = "linesDecorations";

    private readonly List<CodeEditorGutterColumn> _columns = new();

    public CodeEditorGutter()
    {
        GlyphMargin = new CodeEditorGutterColumn(GlyphMarginId, GlyphMarginOrder, 18, isVisible: false);
        LinesDecorations = new CodeEditorGutterColumn(LinesDecorationsId, LinesDecorationsOrder, 10, isVisible: false);
        Add(GlyphMargin);
        Add(LinesDecorations);
    }

    /// <summary>The glyph margin (Monaco's glyphMargin): decorations' GlyphMarginClassName; hidden until a feature shows it.</summary>
    public CodeEditorGutterColumn GlyphMargin { get; }

    /// <summary>The lines decorations column (Monaco's lineDecorationsWidth): decorations' LinesDecorationsClassName; hidden until shown.</summary>
    public CodeEditorGutterColumn LinesDecorations { get; }

    /// <summary>The columns, in order.</summary>
    public IReadOnlyList<CodeEditorGutterColumn> Columns => _columns;

    /// <summary>Raised when a column was added, removed, resized, shown or hidden.</summary>
    public event Action? LayoutChanged;

    /// <summary>Raised when a column asked to be drawn again.</summary>
    public event Action? Invalidated;

    /// <summary>The column <paramref name="id"/>, or null.</summary>
    public CodeEditorGutterColumn? Get(string id) => _columns.FirstOrDefault(c => c.Id == id);

    /// <summary>Adds a column at its <see cref="CodeEditorGutterColumn.Order"/>; dispose the result to remove it.</summary>
    public IDisposable Add(CodeEditorGutterColumn column)
    {
        var index = 0;
        while (index < _columns.Count && _columns[index].Order <= column.Order)
        {
            index++;
        }

        _columns.Insert(index, column);
        column.Changed += OnColumnChanged;
        column.Invalidated += OnColumnInvalidated;
        LayoutChanged?.Invoke();
        return RegistrationDisposable.ToDisposable(() =>
        {
            if (_columns.Remove(column))
            {
                column.Changed -= OnColumnChanged;
                column.Invalidated -= OnColumnInvalidated;
                LayoutChanged?.Invoke();
            }
        });
    }

    private void OnColumnChanged() => LayoutChanged?.Invoke();

    private void OnColumnInvalidated() => Invalidated?.Invoke();
}
