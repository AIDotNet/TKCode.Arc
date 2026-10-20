using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The control row of a <see cref="Slider"/>: a 6px track, the fill between the range ends (or from the start), and 4px
/// ticks; ticks inside the fill take the "on" color and are cut exactly at the fill's edge. Drawn in one
/// <see cref="Render"/>; thumbs and bubbles are children moved by render transforms.
/// </summary>
internal sealed class SliderRail : SliderRailBase
{
    internal Slider? Slider { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Slider is not { } slider)
        {
            return;
        }
        var thickness = TrackThickness;
        var cy = Bounds.Height / 2;
        var track = new Rect(TrackLeft, cy - thickness / 2, TrackWidth, thickness);
        context.DrawRectangle(TrackBrush, null, new RoundedRect(track, thickness / 2));

        var (from, to) = slider.FillSpan();
        var x0 = ToPixel(from);
        var x1 = ToPixel(to);
        var fill = new Rect(x0, track.Y, Math.Max(0, x1 - x0), thickness);
        var fillShape = new RoundedRect(fill, Math.Min(thickness / 2, fill.Width / 2));
        if (fill.Width > 0)
        {
            context.DrawRectangle(FillBrush, null, fillShape);
        }

        var marks = slider.Marks;
        if (marks.Count == 0)
        {
            return;
        }
        var radius = TickDiameter / 2;
        var min = slider.Minimum;
        var max = slider.Maximum;
        for (var i = 0; i < marks.Count; i++)
        {
            var value = marks[i].Value;
            if (value > min && value < max)
            {
                context.DrawEllipse(TickBrush, null, new Point(ToPixel(slider.ToPosition(value)), cy), radius, radius);
            }
        }
        if (fill.Width <= 0 || TickOnBrush is not { } on)
        {
            return;
        }
        using (context.PushClip(fillShape))
        {
            for (var i = 0; i < marks.Count; i++)
            {
                var value = marks[i].Value;
                if (value <= min || value >= max)
                {
                    continue;
                }
                var x = ToPixel(slider.ToPosition(value));
                if (x >= x0 - radius && x <= x1 + radius)
                {
                    context.DrawEllipse(on, null, new Point(x, cy), radius, radius);
                }
            }
        }
    }
}
