using Avalonia.Animation.Easings;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One CSS-style transition of a value on the stage's clock: it starts from whatever is on screen, so a new target in the
/// middle of a move never cuts. No allocation; the easing is a shared instance.
/// </summary>
internal struct BuildingBlocksTween
{
    private double _from;
    private double _to;
    private double _start;
    private double _duration;
    private Easing? _easing;

    /// <summary>The value it is heading to.</summary>
    public readonly double Target => _to;

    /// <summary>The value at <paramref name="now"/>.</summary>
    public readonly double Value(double now)
    {
        var t = now - _start;
        if (_duration <= 0 || t >= _duration)
        {
            return t < 0 ? _from : _to;
        }
        if (t <= 0)
        {
            return _from;
        }
        var p = (_easing ?? ArcEasings.Standard).Ease(t / _duration);
        return _from + (_to - _from) * p;
    }

    /// <summary>Still moving (or waiting for its delay) at <paramref name="now"/>.</summary>
    public readonly bool IsMoving(double now) => now - _start < _duration || (now < _start && Math.Abs(_from - _to) > 1e-9);

    /// <summary>Heads to <paramref name="target"/> from the value at <paramref name="now"/>, after <paramref name="delay"/> seconds.</summary>
    public void To(double target, double now, double duration, double delay = 0, Easing? easing = null)
    {
        if (Math.Abs(target - _to) < 1e-9)
        {
            return;
        }
        _from = Value(now);
        _to = target;
        _start = now + Math.Max(0, delay);
        _duration = Math.Max(0, duration);
        _easing = easing;
    }

    /// <summary>Lands on <paramref name="value"/> at once.</summary>
    public void Jump(double value)
    {
        _from = _to = value;
        _duration = 0;
        _start = double.NegativeInfinity;
    }
}

/// <summary>The tint the stage and the status mark share: an explicit color, else the accent or the foreground.</summary>
internal static class BuildingBlocksPalette
{
    public static Color Tint(Color? tint, BuildingBlocksTone tone, IBrush? accent, IBrush? foreground) =>
        tint ?? (tone == BuildingBlocksTone.Neutral ? ColorOf(foreground, Colors.Black) : ColorOf(accent, Color.Parse("#2F6FEB")));

    /// <summary>The ink drawn on the tint (the button's label): the theme's pair when it has one, else black or white by contrast.</summary>
    public static Color OnTint(Color? tint, BuildingBlocksTone tone, IBrush? accentForeground, IBrush? background)
    {
        if (tint is { } custom)
        {
            return Oklch.Contrast(custom, Colors.White) >= Oklch.Contrast(custom, Colors.Black) ? Colors.White : Colors.Black;
        }
        return tone == BuildingBlocksTone.Neutral ? ColorOf(background, Colors.White) : ColorOf(accentForeground, Colors.White);
    }

    public static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;
}
