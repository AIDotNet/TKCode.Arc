using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Pointer tilt and specular light of one card (Arc Wallet stack, design reference): the pointer's position over the
/// card, normalized to [−1, 1], drives <c>rotateY = 5°·nx</c>, <c>rotateX = −5°·ny</c> under a 900px perspective, and a
/// highlight that sits under the pointer and brightens from 0.25 to 1. Tilt and light share one Smooth spring, so they
/// trail the pointer slightly and keep their velocity when the pointer changes direction.
/// </summary>
/// <remarks>
/// Code-driven because the light and the tilt follow a continuous pointer position (a ControlTheme transition restarts
/// from zero velocity on every move). Under reduced motion or with motion off the card stays flat and the light stays
/// centred at rest. Shared candidate: HoloCard needs the same tilt + light (and <see cref="Perspective"/>).
/// </remarks>
internal sealed class WalletStackTiltLight : IDisposable
{
    /// <summary>The largest tilt in degrees.</summary>
    public const double MaxAngle = 5;

    /// <summary>The perspective distance in px (CSS <c>perspective(900px)</c>).</summary>
    public const double Depth = 900;

    /// <summary>The light's opacity at rest.</summary>
    public const double RestLight = 0.25;

    private readonly Visual _owner;
    private readonly MotionValue<Vector> _tilt;
    private readonly MotionValue<double> _light;

    public WalletStackTiltLight(Visual owner)
    {
        _owner = owner;
        _tilt = new MotionValue<Vector>(owner, default, MotionVectors.Vector);
        _light = new MotionValue<double>(owner, RestLight, MotionVectors.Double);
        _tilt.Changed += _ => Changed?.Invoke();
        _light.Changed += _ => Changed?.Invoke();
    }

    /// <summary>Raised whenever the tilt or the light moves.</summary>
    public event Action? Changed;

    /// <summary>The pointer position the card leans toward, each axis in [−1, 1] (0 = flat).</summary>
    public Vector Tilt => _tilt.Value;

    /// <summary>The light's opacity (0.25 at rest, 1 under the pointer).</summary>
    public double Light => _light.Value;

    /// <summary>Whether the card is tilted or lit away from rest.</summary>
    public bool IsActive => _tilt.Value != default || Math.Abs(_light.Value - RestLight) > 1e-4 || _tilt.IsAnimating || _light.IsAnimating;

    /// <summary>Leans toward the pointer at (<paramref name="nx"/>, <paramref name="ny"/>) in [−1, 1] and lights up.</summary>
    public void PointAt(double nx, double ny)
    {
        if (!ArcMotion.IsFull(_owner))
        {
            Rest();
            return;
        }
        var target = new Vector(Math.Clamp(nx, -1, 1), Math.Clamp(ny, -1, 1));
        if (_tilt.Target != target || !_tilt.IsAnimating && _tilt.Value != target)
        {
            _ = _tilt.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        if (_light.Target != 1)
        {
            _ = _light.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    /// <summary>Settles flat with the light back at rest (the pointer left the card).</summary>
    public void Leave()
    {
        if (!ArcMotion.IsFull(_owner))
        {
            Rest();
            return;
        }
        if (_tilt.Target != default || _tilt.Value != default)
        {
            _ = _tilt.AnimateTo(default, MotionSpec.Spring(ArcSpring.Smooth));
        }
        if (_light.Target != RestLight || _light.Value != RestLight)
        {
            _ = _light.AnimateTo(RestLight, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    /// <summary>Jumps flat with the light at rest.</summary>
    public void Rest()
    {
        if (_tilt.Value != default || _tilt.IsAnimating)
        {
            _tilt.Jump(default);
        }
        if (_light.Value != RestLight || _light.IsAnimating)
        {
            _light.Jump(RestLight);
        }
    }

    /// <summary>
    /// The projective matrix of CSS <c>perspective(depth) rotateX(ax) rotateY(ay)</c> about the centre of a
    /// <paramref name="size"/> box, in Avalonia's row-vector convention (the w row carries the perspective terms).
    /// </summary>
    public static Matrix Perspective(double rotateXDegrees, double rotateYDegrees, Size size, double depth = Depth)
    {
        if (rotateXDegrees == 0 && rotateYDegrees == 0)
        {
            return Matrix.Identity;
        }
        var ax = rotateXDegrees * Math.PI / 180;
        var ay = rotateYDegrees * Math.PI / 180;
        var (sx, cx) = Math.SinCos(ax);
        var (sy, cy) = Math.SinCos(ay);
        // p = Rx·Ry·(x, y, 0): x' = x·cy; y' = y·cx + x·sy·sx; z' = y·sx − x·sy·cx; w = 1 − z'/depth.
        var rotate = new Matrix(
            cy, sy * sx, sy * cx / depth,
            0, cx, -sx / depth,
            0, 0, 1);
        var cxBox = size.Width / 2;
        var cyBox = size.Height / 2;
        return Matrix.CreateTranslation(-cxBox, -cyBox) * rotate * Matrix.CreateTranslation(cxBox, cyBox);
    }

    /// <summary>The current tilt as a matrix about the centre of <paramref name="size"/>.</summary>
    public Matrix ToMatrix(Size size)
    {
        var tilt = _tilt.Value;
        return Perspective(-MaxAngle * tilt.Y, MaxAngle * tilt.X, size);
    }

    public void Dispose()
    {
        _tilt.Dispose();
        _light.Dispose();
    }
}
