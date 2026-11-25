// The revert arrow next to removed lines in side-by-side mode (upstream's createViewZoneMarginArrow in
// src/vs/editor/browser/widget/diffEditor/components/diffEditorViewZones/diffEditorViewZones.ts, commit b57d5c9e; MIT
// license): the margin of the empty space in the modified editor where lines were removed.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Editor.Diff;

namespace TKCode.Arc;

/// <summary>An arrow in the gutter of the modified editor that puts removed lines back.</summary>
internal sealed class DiffEditorRevertArrowView : Control
{
    private readonly DiffEditor _owner;
    private readonly CodeEditor _editor;
    private bool _hovered;

    public DiffEditorRevertArrowView(DiffEditor owner, CodeEditor editor, LineRangeMapping change)
    {
        _owner = owner;
        _editor = editor;
        Change = change;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>The removal it reverts.</summary>
    public LineRangeMapping Change { get; }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            _owner.Revert(Change);
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // transparent background, so the whole margin takes the pointer
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var brush = _hovered ? _owner.Foreground ?? _owner.RevertArrowBrush : _owner.RevertArrowBrush;
        if (brush is null || _editor.View is not { } view)
        {
            return;
        }

        foreach (var column in view.GetGutterLayout().Columns)
        {
            if (column.Column.Id == DiffEditor.RevertColumnId)
            {
                DiffEditor.DrawGlyph(context, DiffEditor.RevertArrowGlyph, brush, new Rect(column.X, 0, column.Width, Math.Min(view.LineHeight, Bounds.Height)));
                return;
            }
        }
    }
}
