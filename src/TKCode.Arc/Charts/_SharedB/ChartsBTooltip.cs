using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Swatch drawn at the start of a tooltip row (Waffle uses the cell shape as a second channel).</summary>
internal enum ChartsBSwatchShape
{
    Square,
    Round,
    Framed
}

/// <summary>One line of a <see cref="ChartsBTooltip"/>: text on the left, an optional value on the right.</summary>
internal sealed class ChartsBTipRow
{
    public FormattedText? Left { get; init; }
    public FormattedText? Right { get; init; }
    public Color? Swatch { get; init; }
    public ChartsBSwatchShape Shape { get; init; }
    public double Indent { get; init; }
    public double MarginTop { get; init; }
    public double MinHeight { get; init; }
}

/// <summary>
/// The floating readout shared by team B's charts (P17): a raised surface bubble drawn by its chart, gliding on its own
/// spring between targets, fading and scaling (0.96 → 1) on show and hide, and holding its last content while it fades.
/// Position math (flip near edges, clamp inside the chart) stays with each chart because Arc places each differently.
/// </summary>
internal sealed class ChartsBTooltip
{
    private readonly Visual _owner;
    private readonly SpringSpec _follow;
    private readonly MotionValue<Point> _position;
    private readonly MotionValue<double> _presence;
    private readonly List<ChartsBTipRow> _rows = new();
    private bool _open;

    public ChartsBTooltip(Visual owner, SpringSpec follow, Action invalidate)
    {
        _owner = owner;
        _follow = follow;
        _position = new MotionValue<Point>(owner, default, MotionVectors.Point);
        _presence = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _position.Changed += _ => invalidate();
        _presence.Changed += _ => invalidate();
    }

    public Thickness Padding { get; set; } = new(12, 10);
    public double MinWidth { get; set; } = 150;
    public double MaxWidth { get; set; } = 240;
    public double Radius { get; set; } = 14;
    public double RowGap { get; set; } = 2;

    /// <summary>Gap between left text and right value.</summary>
    public double ColumnGap { get; set; } = 12;

    /// <summary>Measured bubble size for the current rows.</summary>
    public Size Size { get; private set; }

    public bool IsOpen => _open;

    public bool IsVisible => _presence.Value > 0.001;

    /// <summary>Replaces the content and measures it.</summary>
    public void SetRows(IEnumerable<ChartsBTipRow> rows)
    {
        _rows.Clear();
        _rows.AddRange(rows);
        var inner = MaxWidth - Padding.Left - Padding.Right;
        var width = 0.0;
        var height = 0.0;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var lead = row.Indent + (row.Swatch is null ? 0 : 18);
            var right = row.Right?.WidthIncludingTrailingWhitespace ?? 0;
            if (row.Left is { } left)
            {
                var room = Math.Max(10, inner - lead - (row.Right is null ? 0 : right + ColumnGap));
                if (left.WidthIncludingTrailingWhitespace > room)
                {
                    left.MaxTextWidth = room;
                    left.MaxLineCount = 1;
                    left.Trimming = TextTrimming.CharacterEllipsis;
                }
            }
            var rowWidth = lead + (row.Left?.WidthIncludingTrailingWhitespace ?? 0) + (row.Right is null ? 0 : ColumnGap + right);
            width = Math.Max(width, rowWidth);
            height += (i > 0 ? RowGap : 0) + row.MarginTop + RowHeight(row);
        }
        Size = new Size(Math.Clamp(width + Padding.Left + Padding.Right, MinWidth, MaxWidth), height + Padding.Top + Padding.Bottom);
    }

    /// <summary>Moves to <paramref name="topLeft"/>: the first show jumps there, later moves glide on the follow spring.</summary>
    public void Show(Point topLeft)
    {
        var full = ArcMotion.IsFull(_owner);
        if (!_open && _presence.Value <= 0.001 || !full)
        {
            _position.Jump(topLeft);
        }
        else if (_position.Target != topLeft)
        {
            _ = _position.AnimateTo(topLeft, MotionSpec.Spring(_follow));
        }
        if (!_open)
        {
            _open = true;
            Fade(1);
        }
    }

    public void Hide()
    {
        if (!_open)
        {
            return;
        }
        _open = false;
        Fade(0);
    }

    private void Fade(double target)
    {
        if (ArcMotion.IsFull(_owner))
        {
            _ = _presence.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        else
        {
            _presence.Jump(target);
        }
    }

    public void Draw(DrawingContext context, ChartsBPalette palette)
    {
        var presence = _presence.Value;
        if (presence <= 0.001 || _rows.Count == 0)
        {
            return;
        }
        var origin = _position.Value;
        var rect = new Rect(origin, Size);
        var scale = 0.96 + 0.04 * presence;
        var center = rect.Center;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        using (context.PushOpacity(presence))
        {
            context.DrawRectangle(palette.Brush(palette.SurfaceRaised), palette.Pen(palette.Border, 1),
                new RoundedRect(rect.Deflate(0.5), Radius), palette.ShadowFloating);
            var y = rect.Y + Padding.Top;
            var right = rect.Right - Padding.Right;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                y += (i > 0 ? RowGap : 0) + row.MarginTop;
                var height = RowHeight(row);
                var x = rect.X + Padding.Left + row.Indent;
                if (row.Swatch is { } swatch)
                {
                    DrawSwatch(context, palette, new Rect(x, y + (height - 10) / 2, 10, 10), swatch, row.Shape);
                    x += 18;
                }
                if (row.Left is { } left)
                {
                    context.DrawText(left, new Point(x, y + (height - left.Height) / 2));
                }
                if (row.Right is { } value)
                {
                    context.DrawText(value, new Point(right - value.WidthIncludingTrailingWhitespace, y + (height - value.Height) / 2));
                }
                y += height;
            }
        }
    }

    /// <summary>A 10px legend swatch: a 3px-radius square, a disc, or a square with a thin surface frame.</summary>
    public static void DrawSwatch(DrawingContext context, ChartsBPalette palette, Rect rect, Color color, ChartsBSwatchShape shape)
    {
        var fill = palette.Brush(color);
        var radius = shape == ChartsBSwatchShape.Round ? rect.Width / 2 : 3;
        context.DrawRectangle(fill, null, new RoundedRect(rect, radius));
        if (shape == ChartsBSwatchShape.Framed)
        {
            context.DrawRectangle(null, palette.Pen(palette.Surface, 1), new RoundedRect(rect.Deflate(2.5), 1.5));
        }
    }

    private static double RowHeight(ChartsBTipRow row)
        => Math.Max(row.MinHeight, Math.Max(row.Left?.Height ?? 0, row.Right?.Height ?? 0));
}
