// Ported from vscode (src/vs/editor/contrib/folding/browser/folding.css and the firstLineDecorationClassName codicons
// of foldingDecorations.ts, commit b57d5c9e), MIT license: the folding column of the gutter.
// Differences: the column is the controller's own (CodeEditorGutter.FoldingOrder, after the lines decorations); it
// draws a chevron (down when expanded, right when collapsed) from the folding decoration that starts on each line,
// in the GutterGlyph theme color; with showFoldingControls 'mouseover' the expanded ones show while the pointer is
// over the gutter, at once (upstream fades them in); a press on a line with a chevron does not select the line.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Editor.Contrib.Folding;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

internal sealed partial class FoldingController
{
    /// <summary>The id of the folding column of the gutter.</summary>
    public const string FoldingColumnId = "folding";

    private static readonly Cursor s_handCursor = new(StandardCursorType.Hand);

    private CodeEditorGutterColumn? _column;
    private IDisposable? _columnRegistration;
    private bool _gutterHovered;

    /// <summary>What the folding column shows on a line.</summary>
    internal enum FoldingControlKind
    {
        /// <summary>Nothing: no region starts on the line.</summary>
        None,

        /// <summary>An expanded region that shows its chevron only while the pointer is over the gutter.</summary>
        ExpandedOnHover,

        /// <summary>An expanded region that always shows its chevron.</summary>
        Expanded,

        /// <summary>A collapsed region (its chevron always shows).</summary>
        Collapsed,
    }

    /// <summary>The folding column (null after disposal).</summary>
    internal CodeEditorGutterColumn? FoldingColumn => _column;

    /// <summary>Whether the pointer is over the gutter (the chevrons of expanded regions show).</summary>
    internal bool IsGutterHovered => _gutterHovered;

    private void CreateGutterColumn()
    {
        _column = new CodeEditorGutterColumn(FoldingColumnId, CodeEditorGutter.FoldingOrder, GetColumnWidth(), IsColumnVisible())
        {
            Draw = DrawFoldingControl,
            PointerPressed = OnColumnPressed,
            PointerMoved = OnColumnPointerMoved,
        };
        _columnRegistration = _editor.Gutter.Add(_column);
    }

    private void RemoveGutterColumn()
    {
        _columnRegistration?.Dispose();
        _columnRegistration = null;
        _column = null;
    }

    private void UpdateGutterColumn()
    {
        if (_column is not { } column)
        {
            return;
        }

        column.Width = GetColumnWidth();
        column.IsVisible = IsColumnVisible();
    }

    private bool IsColumnVisible() => _isEnabled && ShowFoldingControls != ShowFoldingControls.Never;

    private double GetColumnWidth() => _editor.View?.FoldingWidth ?? 18;

    private void InvalidateGutter() => _column?.Invalidate();

    private void OnDidChangeModelDecorations(ModelDecorationsChangedEvent e) => InvalidateGutter();

    /// <summary>What the folding column shows on a model line.</summary>
    internal FoldingControlKind GetFoldingControlAt(int lineNumber)
    {
        if (_foldingModel is null || lineNumber < 1 || lineNumber > _editor.Model.GetLineCount())
        {
            return FoldingControlKind.None;
        }

        var kind = FoldingControlKind.None;
        foreach (var decoration in _editor.Model.GetLineDecorations(lineNumber, _editor.EditorId))
        {
            if (decoration.Range.StartLineNumber != lineNumber || decoration.Options.FirstLineDecorationClassName is not { } className)
            {
                continue;
            }

            if (className.Contains(FoldingDecorationClassNames.FoldingCollapsed, StringComparison.Ordinal)
                || className.Contains(FoldingDecorationClassNames.FoldingManualCollapsed, StringComparison.Ordinal))
            {
                return FoldingControlKind.Collapsed;
            }

            if (className.Contains(FoldingDecorationClassNames.FoldingExpanded, StringComparison.Ordinal)
                || className.Contains(FoldingDecorationClassNames.FoldingManualExpanded, StringComparison.Ordinal))
            {
                var always = className.Contains(FoldingDecorationClassNames.AlwaysShowFoldIcons, StringComparison.Ordinal);
                if (always || kind == FoldingControlKind.None)
                {
                    kind = always ? FoldingControlKind.Expanded : FoldingControlKind.ExpandedOnHover;
                }
            }
        }

        return kind;
    }

    /// <summary>Whether the chevron of a model line is drawn now.</summary>
    internal bool IsFoldingControlVisible(int lineNumber) => GetFoldingControlAt(lineNumber) switch
    {
        FoldingControlKind.Collapsed or FoldingControlKind.Expanded => true,
        FoldingControlKind.ExpandedOnHover => _gutterHovered,
        _ => false,
    };

    private void DrawFoldingControl(DrawingContext context, CodeEditorGutterLine line)
    {
        if (!line.IsFirstOfModelLine || _editor.View is not { } view)
        {
            return;
        }

        var kind = GetFoldingControlAt(line.ModelLineNumber);
        if (kind == FoldingControlKind.None || (kind == FoldingControlKind.ExpandedOnHover && !_gutterHovered))
        {
            return;
        }

        if ((view.Resolve(CodeEditorThemeColor.GutterGlyph) ?? view.LineNumberBrush) is not { } brush)
        {
            return;
        }

        // a chevron in a square of the line's height, at most the column's width (upstream: codicon at 140%)
        var cell = line.Bounds;
        var size = Math.Min(cell.Width, cell.Height) * 0.75;
        var cx = cell.X + (cell.Width / 2);
        var cy = cell.Y + (cell.Height / 2);
        var half = size * 0.25;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            if (kind == FoldingControlKind.Collapsed)
            {
                // chevron right
                g.BeginFigure(new Point(cx - (half / 2), cy - half), false);
                g.LineTo(new Point(cx + (half / 2), cy));
                g.LineTo(new Point(cx - (half / 2), cy + half));
            }
            else
            {
                // chevron down
                g.BeginFigure(new Point(cx - half, cy - (half / 2)), false);
                g.LineTo(new Point(cx, cy + (half / 2)));
                g.LineTo(new Point(cx + half, cy - (half / 2)));
            }

            g.EndFigure(false);
        }

        var pen = new ImmutablePen(brush.ToImmutable(), Math.Max(1, size / 10), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawGeometry(null, pen, geometry);
    }

    // a press on a chevron does not select the line; the region toggles on release (OnEditorMouseUp)
    private bool OnColumnPressed(CodeEditorGutterPointerEvent e) => GetFoldingControlAt(e.ModelLineNumber) != FoldingControlKind.None;

    private void OnColumnPointerMoved(CodeEditorGutterPointerEvent? e)
    {
        if (_column is { } column)
        {
            column.Cursor = e is not null && GetFoldingControlAt(e.ModelLineNumber) != FoldingControlKind.None ? s_handCursor : null;
        }
    }

    private void OnEditorMouseMove(CodeEditorMouseEvent e) =>
        SetGutterHovered(e.Target.Type is CodeEditorMouseTargetType.GutterGlyphMargin
            or CodeEditorMouseTargetType.GutterLineNumbers
            or CodeEditorMouseTargetType.GutterLineDecorations
            or CodeEditorMouseTargetType.GutterViewZone);

    private void OnEditorMouseLeave(PointerEventArgs e) => SetGutterHovered(false);

    /// <summary>Sets whether the pointer is over the gutter (the chevrons of expanded regions show).</summary>
    internal void SetGutterHovered(bool hovered)
    {
        if (_gutterHovered == hovered)
        {
            return;
        }

        _gutterHovered = hovered;
        if (ShowFoldingControls == ShowFoldingControls.Mouseover)
        {
            InvalidateGutter();
        }
    }
}
