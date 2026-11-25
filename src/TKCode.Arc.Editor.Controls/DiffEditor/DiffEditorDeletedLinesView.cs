// The removed lines of a change in the inline diff editor (upstream's view zone with renderLines in
// src/vs/editor/browser/widget/diffEditor/components/diffEditorViewZones/renderLines.ts and diffEditorViewZones.ts,
// commit b57d5c9e; MIT license): the original lines in a view zone of the modified editor, with the removed-line
// background and the removed characters highlighted.
// Differences: drawn in the editor's foreground without syntax colors (upstream renders the original model's tokens);
// columns are placed on the monospace grid (tabs expanded at the editor's tab size), so wide characters are not
// measured; the lines are not selectable (upstream: line-delete-selectable and a copy menu).

using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Diff;

namespace TKCode.Arc;

/// <summary>Removed lines shown between the lines of the modified text.</summary>
internal sealed class DiffEditorDeletedLinesView : Control
{
    private readonly DiffEditor _owner;
    private readonly CodeEditor _editor;

    public DiffEditorDeletedLinesView(DiffEditor owner, CodeEditor editor, string[] lines, int firstLineNumber, DetailedLineRangeMapping change)
    {
        _owner = owner;
        _editor = editor;
        Lines = lines;
        FirstLineNumber = firstLineNumber;
        Change = change;
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    /// <summary>The removed lines (of the original text).</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>The original line number of the first of <see cref="Lines"/>.</summary>
    public int FirstLineNumber { get; }

    /// <summary>The change the lines belong to.</summary>
    public DetailedLineRangeMapping Change { get; }

    /// <summary>The removed characters of a line (index into <see cref="Lines"/>) as start and end columns (1-based, end exclusive).</summary>
    internal IReadOnlyList<(int StartColumn, int EndColumn)> GetRemovedColumns(int index)
    {
        var result = new List<(int, int)>();
        var lineNumber = FirstLineNumber + index;
        var maxColumn = Lines[index].Length + 1;
        foreach (var inner in Change.InnerChanges ?? [])
        {
            var range = inner.OriginalRange;
            if (range.IsEmpty || lineNumber < range.StartLineNumber || lineNumber > range.EndLineNumber)
            {
                continue;
            }

            var start = range.StartLineNumber == lineNumber ? range.StartColumn : 1;
            // a range that goes on past this line takes its line break: to the right edge
            var end = range.EndLineNumber == lineNumber ? Math.Min(range.EndColumn, maxColumn) : int.MaxValue;
            if (end > start || end == int.MaxValue)
            {
                result.Add((Math.Min(start, maxColumn), end));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_editor.View is not { } view || view.LineHeight <= 0)
        {
            return;
        }

        var lineHeight = view.LineHeight;
        var charWidth = view.CharWidth;
        var tabSize = Math.Max(1, _editor.TabSize);
        var width = Bounds.Width;
        var charBrush = DiffEditor.WithOpacity(_owner.RemovedTextBrush, _owner.CharHighlightOpacity);
        var typeface = new Typeface(TextElement.GetFontFamily(_editor));
        var fontSize = TextElement.GetFontSize(_editor);
        var foreground = _editor.Foreground ?? _owner.Foreground;

        for (var i = 0; i < Lines.Count; i++)
        {
            var top = i * lineHeight;
            if (_owner.RemovedLineBrush is { } lineBrush)
            {
                context.FillRectangle(lineBrush, new Rect(0, top, width, lineHeight));
            }

            var (text, visibleColumns) = ExpandTabs(Lines[i], tabSize);
            if (charBrush is not null)
            {
                foreach (var (startColumn, endColumn) in GetRemovedColumns(i))
                {
                    var x1 = visibleColumns[Math.Clamp(startColumn - 1, 0, visibleColumns.Length - 1)] * charWidth;
                    var x2 = endColumn == int.MaxValue ? width : visibleColumns[Math.Clamp(endColumn - 1, 0, visibleColumns.Length - 1)] * charWidth;
                    if (x2 > x1)
                    {
                        context.FillRectangle(charBrush, new Rect(x1, top, x2 - x1, lineHeight));
                    }
                }
            }

            if (text.Length > 0 && foreground is not null)
            {
                var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, foreground);
                context.DrawText(formatted, new Point(0, top + (lineHeight - formatted.Height) / 2));
            }
        }
    }

    /// <summary>The line with tabs as spaces, and the visible column (0-based) of each column of the line.</summary>
    internal static (string Text, int[] VisibleColumns) ExpandTabs(string line, int tabSize)
    {
        var builder = new StringBuilder(line.Length);
        var visibleColumns = new int[line.Length + 1];
        var visible = 0;
        for (var i = 0; i < line.Length; i++)
        {
            visibleColumns[i] = visible;
            if (line[i] == '\t')
            {
                var spaces = tabSize - (visible % tabSize);
                builder.Append(' ', spaces);
                visible += spaces;
            }
            else
            {
                builder.Append(line[i]);
                visible++;
            }
        }

        visibleColumns[line.Length] = visible;
        return (builder.ToString(), visibleColumns);
    }
}
