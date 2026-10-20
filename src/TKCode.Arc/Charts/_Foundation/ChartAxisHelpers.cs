using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Category (time) axis labels under the plot.</summary>
internal static class ChartCategoryAxis
{
    /// <summary>
    /// Indices of the labels that fit: at most one per <paramref name="spacing"/> pixels, counted back from the
    /// latest so the newest label always shows (LineChart 76px, Streamgraph 72px).
    /// </summary>
    public static void Picks(ChartData data, double width, double spacing, List<int> output)
    {
        output.Clear();
        if (width <= 0)
        {
            return;
        }
        var labeled = 0;
        for (var i = 0; i < data.Count; i++)
        {
            if (!string.IsNullOrEmpty(data.AxisLabels[i]))
            {
                labeled++;
            }
        }
        if (labeled == 0)
        {
            return;
        }
        var room = Math.Max(2, Math.Floor(width / spacing));
        var stride = (int)Math.Ceiling(labeled / room);
        var rank = 0;
        for (var i = data.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(data.AxisLabels[i]))
            {
                continue;
            }
            if (rank % stride == 0)
            {
                output.Add(i);
            }
            rank++;
        }
        output.Reverse();
    }

    /// <summary>
    /// Draws the picked labels: each sits at its share of the width and shifts left by the same share of its own
    /// width, so the first is left-aligned, the last right-aligned and the rest in between.
    /// </summary>
    public static void Draw(DrawingContext context, ChartPalette palette, ChartTextCache texts, ChartData data,
                            List<int> picks, double left, double width, double top)
    {
        var last = data.Count - 1;
        foreach (var index in picks)
        {
            var label = data.AxisLabels[index];
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }
            var share = last > 0 ? (double)index / last : 0.5;
            var text = texts.Get(palette, label, palette.SizeXS, palette.TextMuted);
            var x = left + share * width - share * text.WidthIncludingTrailingWhitespace;
            context.DrawText(text, new Point(Math.Round(x), top));
        }
    }
}

/// <summary>
/// The loading pulse (P21): while a chart loads, its shapes breathe between 22% and 45% opacity on the in-out
/// curve (1.4s each way), only while attached and at full motion; reduced motion holds 32%. When loading ends
/// the shapes return to full opacity (standard).
/// </summary>
internal sealed class ChartBreath
{
    private readonly Visual _owner;
    private readonly MotionValue<double> _opacity;
    private bool _loading;
    private int _generation;

    public ChartBreath(Visual owner, Action changed)
    {
        _owner = owner;
        _opacity = new MotionValue<double>(owner, 1, MotionVectors.Double);
        _opacity.Changed += _ => changed();
    }

    public double Opacity => _opacity.Value;

    public bool IsLoading => _loading;

    public void SetLoading(bool loading)
    {
        if (_loading == loading)
        {
            return;
        }
        _loading = loading;
        _generation++;
        var level = ArcMotion.GetEffectiveLevel(_owner);
        if (!loading)
        {
            _ = _opacity.AnimateTo(1, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            return;
        }
        if (level != ArcMotionLevel.Full)
        {
            _ = _opacity.AnimateTo(0.32, level == ArcMotionLevel.Off ? MotionSpec.Instant : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            return;
        }
        _ = Loop(_generation);
    }

    private async Task Loop(int generation)
    {
        var duration = TimeSpan.FromMilliseconds(1400);
        _opacity.Jump(0.32);
        var up = true;
        while (_loading && generation == _generation && TopLevel.GetTopLevel(_owner) is not null && _owner.IsEffectivelyVisible
               && ArcMotion.GetEffectiveLevel(_owner) == ArcMotionLevel.Full)
        {
            var done = await _opacity.AnimateTo(up ? 0.45 : 0.22, MotionSpec.Tween(duration, ArcEasings.InOut));
            if (!done)
            {
                return;
            }
            up = !up;
        }
    }
}
