using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The load balance bar: an 8 px rounded track with one segment per expert, as wide as its share of the routes, with a
/// 2 px surface gap between neighbouring non-empty segments. Segments are muted, take the choice's color while their
/// expert holds the current token, and turn secondary while their expert is inspected. Widths slide over 480 ms; colors
/// fade over 240 ms.
/// </summary>
internal sealed class MixtureOfExpertsLoadBar : Control
{
    private MixtureOfExperts? _owner;
    private MixtureOfExpertsTween[] _weights = [];
    private MixtureOfExpertsFade[] _looks = [];
    private double _until;

    public void Attach(MixtureOfExperts owner, int experts)
    {
        _owner = owner;
        if (_weights.Length != experts)
        {
            _weights = new MixtureOfExpertsTween[experts];
            _looks = new MixtureOfExpertsFade[experts];
            for (var i = 0; i < experts; i++)
            {
                _weights[i] = new MixtureOfExpertsTween(0);
                _looks[i] = new MixtureOfExpertsFade(MixtureOfExpertsLooks.LoadNormal);
            }
        }
        InvalidateVisual();
    }

    public double Weight(int expert) => _weights[expert].Target;

    public int Look(int expert) => _looks[expert].To;

    public void Apply(IReadOnlyList<int> loads, int[] looks, double wall, bool animate)
    {
        var changed = false;
        for (var i = 0; i < _weights.Length; i++)
        {
            if (_weights[i].Set(i < loads.Count ? loads[i] : 0, wall, animate ? 0.48 : 0))
            {
                changed = true;
                _until = Math.Max(_until, _weights[i].Until);
            }
            if (_looks[i].Set(i < looks.Length ? looks[i] : 0, wall, animate ? 0.24 : 0))
            {
                changed = true;
                _until = Math.Max(_until, _looks[i].Until);
            }
        }
        if (changed)
        {
            InvalidateVisual();
        }
    }

    public void Tick(double wall)
    {
        if (_until > 0 && wall <= _until + 0.05)
        {
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, 8);

    public override void Render(DrawingContext context)
    {
        var owner = _owner;
        var w = Bounds.Width;
        if (owner is null || !owner.Palette.IsReady || w < 2)
        {
            return;
        }
        var palette = owner.Palette;
        var wall = owner.Wall;
        var track = new RoundedRect(new Rect(0, 0, w, 8), 4);
        context.DrawRectangle(palette.Brush(palette.LoadTrack), null, track);
        double total = 0;
        for (var i = 0; i < _weights.Length; i++)
        {
            total += Math.Max(0, _weights[i].Value(wall));
        }
        if (total <= 0.0001)
        {
            return;
        }
        using (context.PushClip(track))
        {
            double x = 0;
            var drawn = false;
            for (var i = 0; i < _weights.Length; i++)
            {
                var weight = Math.Max(0, _weights[i].Value(wall));
                var width = w * weight / total;
                if (width <= 0.01)
                {
                    continue;
                }
                // A 2 px surface gap before every segment but the first, shrinking with a vanishing segment.
                var gap = drawn ? Math.Min(2, width) : 0;
                var mix = _looks[i].Mix(wall);
                var from = ColorOf(palette, _looks[i].From);
                var to = ColorOf(palette, _looks[i].To);
                var rect = new Rect(x + gap, 0, Math.Max(0, width - gap), 8);
                var (fa, ta) = MixtureOfExpertsPart.FadeOpacity(from, to, mix);
                if (fa > 0)
                {
                    using (context.PushOpacity(fa))
                    {
                        context.FillRectangle(palette.Brush(from), rect);
                    }
                }
                if (ta > 0)
                {
                    using (context.PushOpacity(ta))
                    {
                        context.FillRectangle(palette.Brush(to), rect);
                    }
                }
                x += width;
                drawn = true;
            }
        }
    }

    private static Color ColorOf(MixtureOfExpertsPalette p, int look) => look switch
    {
        MixtureOfExpertsLooks.LoadFirst => p.Rank[0],
        MixtureOfExpertsLooks.LoadSecond => p.Rank[1],
        MixtureOfExpertsLooks.LoadInspected => p.TextSecondary,
        _ => p.LoadSegment
    };
}

/// <summary>
/// The footer: the route text on the left and the load balance on the right (<c>LoadWidth</c> wide, 32 px apart), or
/// the load balance under the text when the footer is narrower than <see cref="TwoColumnMinWidth"/> (a 640 px card).
/// </summary>
internal sealed class MixtureOfExpertsFooterPanel : Panel
{
    /// <summary>The narrowest footer with two columns (a 640 card: 24 padding and a 1 px border each side).</summary>
    public const double TwoColumnMinWidth = 590;

    public static readonly StyledProperty<double> LoadWidthProperty =
        AvaloniaProperty.Register<MixtureOfExpertsFooterPanel, double>(nameof(LoadWidth), 220);

    public double LoadWidth { get => GetValue(LoadWidthProperty); set => SetValue(LoadWidthProperty, value); }

    public bool IsStacked { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : TwoColumnMinWidth;
        IsStacked = width < TwoColumnMinWidth;
        var left = Children[0];
        var right = Children[1];
        if (IsStacked)
        {
            left.Measure(new Size(width, double.PositiveInfinity));
            right.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, left.DesiredSize.Height + 16 + right.DesiredSize.Height);
        }
        left.Measure(new Size(Math.Max(0, width - LoadWidth - 32), double.PositiveInfinity));
        right.Measure(new Size(LoadWidth, double.PositiveInfinity));
        return new Size(width, Math.Max(left.DesiredSize.Height, right.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var left = Children[0];
        var right = Children[1];
        if (IsStacked)
        {
            left.Arrange(new Rect(0, 0, finalSize.Width, left.DesiredSize.Height));
            right.Arrange(new Rect(0, left.DesiredSize.Height + 16, finalSize.Width, right.DesiredSize.Height));
        }
        else
        {
            left.Arrange(new Rect(0, 0, Math.Max(0, finalSize.Width - LoadWidth - 32), finalSize.Height));
            right.Arrange(new Rect(finalSize.Width - LoadWidth, 0, LoadWidth, right.DesiredSize.Height));
        }
        return finalSize;
    }
}
