using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The layer over the whole block where sent addresses fly from their chips to their new pending rows. Each flight is a
/// copy of the address on a muted pill that starts where the chip was and springs (visual duration 0.5, no bounce) toward
/// the row's address, which it re-reads every frame because the rows above it are still growing. The pill's tint fades out
/// from 290 to 600 ms; when the flight lands the copy is removed and the row's own address shows. Render transforms only.
/// </summary>
internal sealed class TeamMembersFlightLayer : Panel
{
    /// <summary>The flight's spring: Arc's visualDuration 0.5, bounce 0 (fitted ω 10.5, ζ 1).</summary>
    internal static readonly SpringSpec FlightSpring = SpringSpec.FromVisual(0.5, 0);

    /// <summary>When the pill's tint starts fading (290 ms after the send) and how long it takes (310 ms).</summary>
    internal static readonly TimeSpan TintDelay = TimeSpan.FromMilliseconds(290);

    internal static readonly TimeSpan TintDuration = TimeSpan.FromMilliseconds(310);

    public TeamMembersFlightLayer()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    /// <summary>The copies in flight.</summary>
    public IEnumerable<TeamMembersFlightGhost> Ghosts => Children.OfType<TeamMembersFlightGhost>();

    /// <summary>
    /// Flies <paramref name="text"/> from <paramref name="from"/> (the top-left of the copy, in this layer's coordinates) to
    /// wherever <paramref name="to"/> says the landing spot is now (null keeps the last known spot). Completes when it lands.
    /// </summary>
    public async Task FlyAsync(string text, Point from, Func<Point?> to)
    {
        var ghost = new TeamMembersFlightGhost { Text = text };
        var shift = new TranslateTransform(from.X, from.Y);
        ghost.RenderTransform = shift;
        Children.Add(ghost);
        var last = to() ?? from;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        progress.Changed += p =>
        {
            last = to() ?? last;
            shift.X = from.X + (last.X - from.X) * p;
            shift.Y = from.Y + (last.Y - from.Y) * p;
        };
        var tint = new MotionValue<double>(this, 1, MotionVectors.Double);
        tint.Changed += v => ghost.TintOpacity = v;
        _ = tint.AnimateTo(0, MotionSpec.Tween(TintDuration, new Avalonia.Animation.Easings.LinearEasing(), TintDelay));
        try
        {
            await progress.AnimateTo(1, MotionSpec.Spring(FlightSpring));
        }
        finally
        {
            Children.Remove(ghost);
            progress.Dispose();
            tint.Dispose();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(child.DesiredSize));
        }
        return finalSize;
    }
}

/// <summary>A flying copy of an address: the text (13) on a SurfaceMuted pill with padding (10, 5).</summary>
internal sealed class TeamMembersFlightGhost : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TeamMembersFlightGhost, string?>(nameof(Text));

    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<TeamMembersFlightGhost, double>(nameof(TintOpacity), 1);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>The opacity of the pill behind the text.</summary>
    public double TintOpacity { get => GetValue(TintOpacityProperty); set => SetValue(TintOpacityProperty, value); }
}
