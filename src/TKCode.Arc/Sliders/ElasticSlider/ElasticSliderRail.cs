using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The control row of an <see cref="ElasticSlider"/>. Only the band stretches: past either end the track grows away
/// from its anchored end and thins as it does (a press thickens it a little); the ticks never distort and the thumb and
/// tag ride the stretch. Track, fill and ticks are drawn in one <see cref="Render"/>.
/// </summary>
internal sealed class ElasticSliderRail : SliderRailBase
{
    internal ElasticSlider? Slider { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Slider is not { } slider)
        {
            return;
        }
        var thickness = TrackThickness;
        var cy = Bounds.Height / 2;
        var left = TrackLeft;
        var width = TrackWidth;
        var over = slider.Over;
        var scaleX = 1 + Math.Abs(over) / width;
        var scaleY = (1 + slider.Press * 0.45) * (1 - Math.Abs(over) / ElasticSlider.Stretch * 0.32);
        var originX = over < 0 ? left + width : left;
        var transform = Matrix.CreateTranslation(-originX, -cy) * Matrix.CreateScale(scaleX, Math.Max(0.01, scaleY)) * Matrix.CreateTranslation(originX, cy);
        var track = new Rect(left, cy - thickness / 2, width, thickness);
        using (context.PushTransform(transform))
        {
            context.DrawRectangle(TrackBrush, null, new RoundedRect(track, thickness / 2));
            var at = Math.Clamp(slider.ShownPosition, 0, SliderMath.Span) / SliderMath.Span * width;
            if (at > 0)
            {
                var fill = new Rect(left, track.Y, at, thickness);
                context.DrawRectangle(FillBrush, null, new RoundedRect(fill, Math.Min(thickness / 2, at / 2)));
            }
        }

        var marks = slider.Marks;
        var radius = TickDiameter / 2;
        var min = slider.Minimum;
        var max = slider.Maximum;
        for (var i = 0; i < marks.Count; i++)
        {
            var value = marks[i].Value;
            if (value <= min || value >= max)
            {
                continue;
            }
            var scale = slider.TickScale(i);
            var brush = value <= slider.Value ? TickOnBrush : TickBrush;
            context.DrawEllipse(brush, null, new Point(ToPixel(slider.ToPosition(value)), cy), radius * scale, radius * scale);
        }
    }
}
