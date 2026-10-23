using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A spinner's angle: it turns once every <c>turnSeconds</c> (linear), advanced by frame time. With an owner it runs on
/// its own Core <see cref="LiveLoop"/> (started and stopped by the caller; code-driven because a ControlTheme cannot
/// animate a drawn arc), without one the owner's own loop calls <see cref="Advance"/>. Each component keeps its own
/// measured period (the tool call and the plan turn in 0.8 s, the agent status face in 0.9 s).
/// </summary>
internal sealed class AiSpin : IDisposable
{
    private readonly double _degreesPerSecond;
    private readonly LiveLoop? _loop;

    /// <summary>A spinner without a loop of its own: the caller advances it.</summary>
    public AiSpin(double turnSeconds)
    {
        _degreesPerSecond = 360 / turnSeconds;
    }

    /// <summary>A spinner on a <see cref="LiveLoop"/> of <paramref name="owner"/>; <paramref name="frame"/> runs after each step.</summary>
    public AiSpin(Visual owner, double turnSeconds, Action frame) : this(turnSeconds)
    {
        _loop = new LiveLoop(owner, dt =>
        {
            Advance(dt);
            frame();
            return true;
        });
    }

    /// <summary>The rotation in degrees, from 0 up to 360.</summary>
    public double Angle { get; private set; }

    /// <summary>True while the loop runs (always false without one).</summary>
    public bool IsRunning => _loop?.IsRunning == true;

    /// <summary>Turns by <paramref name="seconds"/> of frame time.</summary>
    public void Advance(double seconds) => Angle = (Angle + seconds * _degreesPerSecond) % 360;

    /// <summary>Back to 12 o'clock.</summary>
    public void Reset() => Angle = 0;

    /// <summary>Starts the loop (no-op without one).</summary>
    public void Start() => _loop?.Start();

    /// <summary>Stops the loop (the angle stays).</summary>
    public void Stop() => _loop?.Stop();

    /// <summary>Sets <see cref="Start"/> or <see cref="Stop"/> from <paramref name="running"/>.</summary>
    public void Run(bool running)
    {
        if (running)
        {
            Start();
        }
        else
        {
            Stop();
        }
    }

    /// <summary>The transform that turns a drawing by the current angle about <paramref name="center"/>.</summary>
    public Matrix Turn(Point center) => TurnBy(Angle, center);

    /// <summary>The transform that turns a drawing by <paramref name="degrees"/> about <paramref name="center"/>.</summary>
    public static Matrix TurnBy(double degrees, Point center) =>
        Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(degrees * Math.PI / 180) *
        Matrix.CreateTranslation(center.X, center.Y);

    /// <summary>A quarter of the circle at <paramref name="center"/> from 12 o'clock clockwise (the spinner's arc).</summary>
    public static StreamGeometry QuarterArc(Point center, double radius)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(center.X, center.Y - radius), false);
            ctx.ArcTo(new Point(center.X + radius, center.Y), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        return geometry;
    }

    /// <inheritdoc />
    public void Dispose() => _loop?.Dispose();
}
