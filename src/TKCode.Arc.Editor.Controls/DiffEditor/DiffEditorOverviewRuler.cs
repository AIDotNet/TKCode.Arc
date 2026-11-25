// The overview ruler of the diff editor (upstream's OverviewRulerFeature in
// src/vs/editor/browser/widget/diffEditor/features/overviewRulerFeature.ts, commit b57d5c9e; MIT license): the changes
// of the whole document in a narrow strip at the right edge, removed lines (original) in the left half and inserted
// lines (modified) in the right half, with the visible part of the document; a press scrolls there.
// Differences: drawn as one control with two lanes (upstream: two canvases and the editors' own overview rulers);
// inline, the removed lines are placed where their view zone is in the modified editor.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Marks where the changes are in the whole document; a press scrolls there.</summary>
internal sealed class DiffEditorOverviewRuler : Control
{
    private const double MinMarkHeight = 2;

    private DiffEditor? _owner;
    private bool _dragging;

    public DiffEditorOverviewRuler()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    internal void Attach(DiffEditor owner) => _owner = owner;

    internal void Detach() => _owner = null;

    /// <summary>The marks drawn now: lane (0 removed, 1 inserted) and vertical extent in the ruler.</summary>
    internal IReadOnlyList<(int Lane, double Top, double Height)> GetMarks()
    {
        var result = new List<(int, double, double)>();
        if (_owner is not { Diff: { } diff, ModifiedEditor: { } modified } owner || Bounds.Height <= 0)
        {
            return result;
        }

        var sideBySide = owner.RenderSideBySide && owner.OriginalEditor is not null;
        var scale = Bounds.Height / GetScrollHeight(modified);
        var lineHeight = modified.View?.LineHeight ?? 0;
        foreach (var change in diff.Changes)
        {
            if (!change.Original.IsEmpty)
            {
                if (sideBySide && owner.OriginalEditor is { } original)
                {
                    var top = original.GetTopForLineNumber(change.Original.StartLineNumber);
                    result.Add((0, top * scale, Math.Max(MinMarkHeight, change.Original.Length * (original.View?.LineHeight ?? lineHeight) * scale)));
                }
                else
                {
                    // inline: the removed lines are a view zone before the change's first modified line
                    var height = change.Original.Length * lineHeight;
                    var top = modified.GetTopForLineNumber(change.Modified.StartLineNumber) - height;
                    if (change.Modified.StartLineNumber > modified.Model.GetLineCount())
                    {
                        top = GetScrollHeight(modified) - height;
                    }

                    result.Add((0, Math.Max(0, top) * scale, Math.Max(MinMarkHeight, height * scale)));
                }
            }

            if (!change.Modified.IsEmpty)
            {
                var top = modified.GetTopForLineNumber(change.Modified.StartLineNumber);
                result.Add((1, top * scale, Math.Max(MinMarkHeight, change.Modified.Length * lineHeight * scale)));
            }
        }

        return result;
    }

    private static double GetScrollHeight(CodeEditor editor)
    {
        if (editor.ViewModel is not { } viewModel)
        {
            return 1;
        }

        return Math.Max(1, Math.Max(viewModel.ViewLayout.GetScrollHeight(), editor.GetLayoutSize().Height));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (_owner is not { ModifiedEditor: { } modified } owner)
        {
            return;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        // the visible part of the document
        if (owner.OverviewViewportBrush is { } viewportBrush && modified.ViewModel is { } viewModel)
        {
            var scrollHeight = GetScrollHeight(modified);
            var scale = height / scrollHeight;
            var top = viewModel.ViewLayout.GetCurrentScrollTop() * scale;
            var visible = Math.Min(scrollHeight, modified.GetLayoutSize().Height) * scale;
            context.FillRectangle(viewportBrush, new Rect(0, top, width, Math.Max(MinMarkHeight, visible)));
        }

        var laneWidth = width / 2;
        foreach (var (lane, top, markHeight) in GetMarks())
        {
            var brush = lane == 0 ? owner.RemovedTextBrush : owner.InsertedTextBrush;
            if (brush is not null)
            {
                context.FillRectangle(brush, new Rect(lane * laneWidth, top, laneWidth, markHeight));
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        e.Pointer.Capture(this);
        ScrollToPoint(e.GetPosition(this).Y);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            ScrollToPoint(e.GetPosition(this).Y);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    /// <summary>Scrolls so that the document's point at <paramref name="y"/> of the ruler is in the middle of the editor.</summary>
    internal void ScrollToPoint(double y)
    {
        if (_owner is not { ModifiedEditor: { } modified } owner || Bounds.Height <= 0)
        {
            return;
        }

        var scrollHeight = GetScrollHeight(modified);
        owner.ScrollTo(y / Bounds.Height * scrollHeight - modified.GetLayoutSize().Height / 2);
        InvalidateVisual();
    }
}
