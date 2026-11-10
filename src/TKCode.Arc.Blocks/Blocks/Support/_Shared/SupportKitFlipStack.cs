using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A vertical stack whose children glide to their new places when the order changes (a re-sort, a vote that moves a row
/// up): after each arrange, a child that moved starts from where it was drawn and springs to its slot through its
/// render transform, so layout happens once. An interrupted glide keeps its speed. Children new to the stack, and every
/// child while motion is reduced or off, are placed at once. Code-driven because the start of each glide is a measured
/// layout position that no ControlTheme transition can know.
/// </summary>
internal sealed class SupportKitFlipStack : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<SupportKitFlipStack, double>(nameof(Spacing));

    private readonly Dictionary<Control, Flight> _flights = new();

    static SupportKitFlipStack()
    {
        AffectsMeasure<SupportKitFlipStack>(SpacingProperty);
    }

    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Whether any child is gliding (tests).</summary>
    public bool IsGliding => _flights.Values.Any(f => f.Motion?.IsAnimating == true);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var visible = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            if (!child.IsVisible)
            {
                continue;
            }
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
            visible++;
        }
        height += Math.Max(0, visible - 1) * Spacing;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        var animate = ArcMotion.IsFull(this);
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(0, y, finalSize.Width, height));
            Glide(child, y, animate);
            y += height + Spacing;
        }
        // Forget children that left.
        if (_flights.Count > Children.Count)
        {
            foreach (var gone in _flights.Keys.Where(c => !Children.Contains(c)).ToList())
            {
                _flights[gone].Motion?.Dispose();
                _flights.Remove(gone);
            }
        }
        return finalSize;
    }

    private void Glide(Control child, double y, bool animate)
    {
        if (!_flights.TryGetValue(child, out var flight))
        {
            _flights[child] = new Flight { Y = y };
            return;
        }
        var delta = flight.Y - y;
        flight.Y = y;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }
        if (!animate)
        {
            flight.Motion?.Jump(0);
            return;
        }
        if (flight.Motion is null)
        {
            var transform = new TranslateTransform();
            child.RenderTransform = transform;
            flight.Motion = new MotionValue<double>(child, 0, MotionVectors.Double);
            flight.Motion.Changed += value => transform.Y = value;
        }
        var velocity = flight.Motion.Velocity;
        flight.Motion.Jump(flight.Motion.Value + delta);
        _ = flight.Motion.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), velocity);
    }

    private sealed class Flight
    {
        public double Y;
        public MotionValue<double>? Motion;
    }
}
