// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): the banner's exit (AnimatePresence).

using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc;

/// <summary>
/// Holds the banner and closes its gap when dismissed: the opacity goes to 0 in 160 ms (standard curve) while the height
/// goes to 0 on Arc's smooth spring (visual duration .4, no bounce); nothing scales. Reduced motion fades in 120 ms and
/// removes the banner at the end with no height animation; Off removes it at once. Always clips (Arc's
/// <c>overflow: hidden</c>).
/// </summary>
/// <remarks>
/// Code-driven because the height is a measured value that must lay out around it (the page below moves up) and the exit
/// finishes only when both the fade and the spring are done, which a ControlTheme transition cannot report.
/// </remarks>
internal sealed class CtaBannerHost : Decorator
{
    private static readonly SpringSpec s_smooth = SpringSpec.FromVisual(0.4, 0);

    private readonly MotionValue<double> _height;
    private readonly MotionValue<double> _fade;
    private int _exitId;

    public CtaBannerHost()
    {
        ClipToBounds = true;
        _height = new MotionValue<double>(this, 1, MotionVectors.Double);
        _height.Changed += _ => InvalidateMeasure();
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += value => Opacity = Math.Clamp(value, 0, 1);
    }

    /// <summary>True once the banner is gone (or going: input is already off).</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The share of the banner's height that is laid out (1 open, 0 closed).</summary>
    public double HeightFactor => _height.Value;

    /// <summary>Closes the banner with the exit motion; <paramref name="done"/> runs after the last frame (not when restored mid-way).</summary>
    public async void Close(Action? done)
    {
        if (IsClosed)
        {
            return;
        }
        IsClosed = true;
        IsHitTestVisible = false;
        var id = ++_exitId;
        var level = ArcMotion.GetEffectiveLevel(this);
        var reached = true;
        if (level == ArcMotionLevel.Off || !IsLoaded)
        {
            _fade.Jump(0);
            _height.Jump(0);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            reached = await _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            if (reached && id == _exitId)
            {
                _height.Jump(0);
            }
        }
        else
        {
            var fade = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            var height = _height.AnimateTo(0, MotionSpec.Spring(s_smooth));
            reached = await fade && await height;
        }
        if (reached && id == _exitId)
        {
            done?.Invoke();
        }
    }

    /// <summary>Brings the banner back at once (Arc re-mounts it).</summary>
    public void Open()
    {
        _exitId++;
        IsClosed = false;
        IsHitTestVisible = true;
        _fade.Jump(1);
        _height.Jump(1);
    }

    /// <summary>Starts closed (a section whose <c>IsOpen</c> is false from the start).</summary>
    public void SetClosed()
    {
        _exitId++;
        IsClosed = true;
        IsHitTestVisible = false;
        _fade.Jump(0);
        _height.Jump(0);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize);
        var factor = Math.Max(0, _height.Value);
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * factor);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        return finalSize;
    }
}
