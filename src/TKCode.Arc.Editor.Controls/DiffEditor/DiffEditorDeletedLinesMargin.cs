// The gutter next to removed lines in the inline diff editor (upstream's inline-deleted-margin-view-zone with the
// delete signs, src/vs/editor/browser/widget/diffEditor/components/diffEditorViewZones/diffEditorViewZones.ts, commit
// b57d5c9e; MIT license).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The removed-line background and a − sign per removed line, in the gutter of the inline editor.</summary>
internal sealed class DiffEditorDeletedLinesMargin : Control
{
    private readonly DiffEditor _owner;
    private readonly CodeEditor _editor;

    public DiffEditorDeletedLinesMargin(DiffEditor owner, CodeEditor editor, int lineCount)
    {
        _owner = owner;
        _editor = editor;
        LineCount = lineCount;
        IsHitTestVisible = false;
    }

    /// <summary>The number of removed lines.</summary>
    public int LineCount { get; }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_owner.RemovedLineBrush is { } background)
        {
            context.FillRectangle(background, new Rect(Bounds.Size));
        }

        if (_owner.RemovedTextBrush is not { } sign || _editor.View is not { LineHeight: > 0 } view)
        {
            return;
        }

        foreach (var column in view.GetGutterLayout().Columns)
        {
            if (column.Column.Id != CodeEditorGutter.LinesDecorationsId)
            {
                continue;
            }

            for (var i = 0; i < LineCount; i++)
            {
                DiffEditor.DrawGlyph(context, DiffEditor.MinusGlyph, sign, new Rect(column.X, i * view.LineHeight, column.Width, view.LineHeight));
            }
        }
    }
}
