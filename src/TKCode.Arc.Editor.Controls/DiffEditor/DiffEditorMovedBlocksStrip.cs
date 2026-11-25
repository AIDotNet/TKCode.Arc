// The strip between the two editors (upstream's sash and MovedBlocksLinesFeature in
// src/vs/editor/browser/widget/diffEditor/features/movedBlocksLinesFeature.ts, commit b57d5c9e; MIT license): a line
// between the editors and, when blocks of lines moved, a bracket at each block connected from the original block to
// the modified one.
// Differences: the connections are drawn in a strip of their own between the editors (upstream draws them over the
// left edge of the modified editor, in lanes, with arrows); there is no "compare moved text" action and the sash does
// not resize the editors.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Diff;

namespace TKCode.Arc;

/// <summary>The separator between the two editors; connects moved blocks.</summary>
internal sealed class DiffEditorMovedBlocksStrip : Control
{
    /// <summary>The width when blocks moved.</summary>
    public static readonly StyledProperty<double> MovedBlocksWidthProperty =
        AvaloniaProperty.Register<DiffEditorMovedBlocksStrip, double>(nameof(MovedBlocksWidth), 20);

    /// <summary>The width when no block moved (the separator line).</summary>
    public static readonly StyledProperty<double> SeparatorWidthProperty =
        AvaloniaProperty.Register<DiffEditorMovedBlocksStrip, double>(nameof(SeparatorWidth), 1);

    private DiffEditor? _owner;

    static DiffEditorMovedBlocksStrip()
    {
        AffectsMeasure<DiffEditorMovedBlocksStrip>(MovedBlocksWidthProperty, SeparatorWidthProperty);
    }

    public DiffEditorMovedBlocksStrip()
    {
        ClipToBounds = true;
    }

    public double MovedBlocksWidth
    {
        get => GetValue(MovedBlocksWidthProperty);
        set => SetValue(MovedBlocksWidthProperty, value);
    }

    public double SeparatorWidth
    {
        get => GetValue(SeparatorWidthProperty);
        set => SetValue(SeparatorWidthProperty, value);
    }

    internal void Attach(DiffEditor owner) => _owner = owner;

    internal void Detach() => _owner = null;

    private bool HasMoves => _owner?.Diff is { Moves.Count: > 0 };

    /// <summary>Sizes itself again (the width depends on whether blocks moved).</summary>
    public new void InvalidateVisual()
    {
        InvalidateMeasure();
        base.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(HasMoves ? MovedBlocksWidth : SeparatorWidth, 0);

    /// <summary>The vertical extent of a line range in an editor, relative to the editor's top; null when off screen or unknown.</summary>
    internal static (double Top, double Bottom)? GetExtent(CodeEditor editor, LineRange range)
    {
        if (editor.View is not { LineHeight: > 0 } view || editor.ViewModel is not { } viewModel)
        {
            return null;
        }

        var scrollTop = viewModel.ViewLayout.GetCurrentScrollTop();
        var top = editor.GetTopForLineNumber(range.StartLineNumber) - scrollTop;
        var lastLine = Math.Max(range.StartLineNumber, range.EndLineNumberExclusive - 1);
        var bottom = editor.GetTopForLineNumber(lastLine) + view.LineHeight - scrollTop;
        if (range.IsEmpty)
        {
            bottom = top;
        }

        return (top, bottom);
    }

    /// <summary>The connections drawn now: original extent, modified extent (relative to the strip).</summary>
    internal IReadOnlyList<((double Top, double Bottom) Original, (double Top, double Bottom) Modified)> GetConnections()
    {
        var result = new List<((double, double), (double, double))>();
        if (_owner is not { Diff: { } diff, OriginalEditor: { } original, ModifiedEditor: { } modified })
        {
            return result;
        }

        foreach (var move in diff.Moves)
        {
            if (GetExtent(original, move.LineRangeMapping.Original) is { } o && GetExtent(modified, move.LineRangeMapping.Modified) is { } m)
            {
                result.Add((o, m));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_owner is not { } owner)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (owner.SeparatorBrush is { } separator)
        {
            var separatorWidth = Math.Min(width, SeparatorWidth);
            context.FillRectangle(separator, new Rect((width - separatorWidth) / 2, 0, separatorWidth, height));
        }

        if (owner.MovedBlockBrush is not { } brush || width < 6)
        {
            return;
        }

        var pen = new Pen(brush, 1.5);
        const double inset = 2;
        foreach (var (o, m) in GetConnections())
        {
            if (Math.Max(o.Bottom, m.Bottom) < 0 || Math.Min(o.Top, m.Top) > height)
            {
                continue;
            }

            // brackets at both blocks
            var left = new StreamGeometry();
            using (var ctx = left.Open())
            {
                ctx.BeginFigure(new Point(inset + 3, o.Top + 1), false);
                ctx.LineTo(new Point(inset, o.Top + 1));
                ctx.LineTo(new Point(inset, o.Bottom - 1));
                ctx.LineTo(new Point(inset + 3, o.Bottom - 1));
                ctx.EndFigure(false);

                ctx.BeginFigure(new Point(width - inset - 3, m.Top + 1), false);
                ctx.LineTo(new Point(width - inset, m.Top + 1));
                ctx.LineTo(new Point(width - inset, m.Bottom - 1));
                ctx.LineTo(new Point(width - inset - 3, m.Bottom - 1));
                ctx.EndFigure(false);

                // the connection, from the middle of the original block to the middle of the modified one
                var from = new Point(inset, (o.Top + o.Bottom) / 2);
                var to = new Point(width - inset, (m.Top + m.Bottom) / 2);
                ctx.BeginFigure(from, false);
                ctx.CubicBezierTo(new Point(width / 2, from.Y), new Point(width / 2, to.Y), to);
                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, pen, left);
        }
    }
}
