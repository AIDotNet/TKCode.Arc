// Ported from Arc's free signup-form block (MIT, Copyright (c) 2026 Elia Kuratli): .footer of signup-form.module.css.
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The footer row (Arc's <c>display: flex; flex-wrap: wrap; justify-content: space-between; gap: 4px 16px</c>): the
/// status (first child, <c>flex: 1 1 12rem</c>) takes the room the sign-in prompt (second child, no wrap) leaves; when the
/// two do not fit side by side at the status' 192px basis, the prompt moves to a second line at the start.
/// </summary>
internal sealed class SignupFormFooterPanel : Panel
{
    /// <summary>The status' flex basis (12rem).</summary>
    public const double StatusBasis = 192;

    public const double ColumnGap = 16;
    public const double RowGap = 4;

    /// <summary>True while the prompt sits on its own line (tests read it).</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var status = Children[0];
        var prompt = Children[1];
        prompt.Measure(Size.Infinity);
        var promptSize = prompt.DesiredSize;
        var width = availableSize.Width;
        if (double.IsInfinity(width))
        {
            status.Measure(Size.Infinity);
            IsWrapped = false;
            return new Size(status.DesiredSize.Width + ColumnGap + promptSize.Width, Math.Max(status.DesiredSize.Height, promptSize.Height));
        }
        IsWrapped = StatusBasis + ColumnGap + promptSize.Width > width;
        if (IsWrapped)
        {
            status.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, status.DesiredSize.Height + RowGap + promptSize.Height);
        }
        status.Measure(new Size(Math.Max(0, width - ColumnGap - promptSize.Width), double.PositiveInfinity));
        return new Size(width, Math.Max(status.DesiredSize.Height, promptSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var status = Children[0];
        var prompt = Children[1];
        var promptSize = prompt.DesiredSize;
        if (IsWrapped)
        {
            var statusHeight = status.DesiredSize.Height;
            status.Arrange(new Rect(0, 0, finalSize.Width, statusHeight));
            prompt.Arrange(new Rect(0, statusHeight + RowGap, promptSize.Width, promptSize.Height));
            return finalSize;
        }
        var row = Math.Max(status.DesiredSize.Height, promptSize.Height);
        var statusWidth = Math.Max(0, finalSize.Width - ColumnGap - promptSize.Width);
        status.Arrange(new Rect(0, (row - status.DesiredSize.Height) / 2, statusWidth, status.DesiredSize.Height));
        prompt.Arrange(new Rect(finalSize.Width - promptSize.Width, (row - promptSize.Height) / 2, promptSize.Width, promptSize.Height));
        return finalSize;
    }
}
