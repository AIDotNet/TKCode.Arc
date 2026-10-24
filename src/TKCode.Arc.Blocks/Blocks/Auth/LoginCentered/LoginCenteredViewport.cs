// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): useViewHeight in login-centered.tsx.
using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The card's viewport: one step on show, the leaving one popped out of layout above it, and a height that follows the
/// content on the smooth spring only while the view changes. Otherwise the height is the step's own (auto), so a field
/// message opening inside a step moves the card at once, without lag. Clips the leaving step while the height glides.
/// </summary>
/// <remarks>
/// Code-driven: the target height is measured (it is the new step's height), and a change of that height during the glide
/// retargets the running spring with its velocity (Arc's ResizeObserver calling <c>glide(measured)</c> again).
/// </remarks>
internal sealed class LoginCenteredViewport : LoginCenteredPresence
{
    private MotionValue<double>? _height;
    private bool _gliding;
    private bool _pendingGlide;
    private double _glideFrom;
    private double _lastHeight;
    private bool _measuring;

    public LoginCenteredViewport()
    {
        PresenceStyle = LoginCenteredMotion.Step;
        ContentAlignment = HorizontalAlignment.Stretch;
        ClipToBounds = true;
    }

    /// <summary>True while the height follows the spring rather than the step (tests read it).</summary>
    internal bool IsGliding => _gliding;

    /// <summary>The height being shown right now (tests read it).</summary>
    internal double ShownHeight => _lastHeight;

    /// <summary>
    /// Shows <paramref name="step"/>, entering from 12px towards <paramref name="direction"/> (1 forward, −1 back) while
    /// the old step leaves 8px the other way, and glides the height from the current one to the new step's.
    /// </summary>
    public void ShowStep(Control step, int direction, bool animate)
    {
        var from = _gliding && _height is not null ? _height.Value : _lastHeight;
        Show(step, animate, direction * 12, direction * -8);
        if (animate)
        {
            _pendingGlide = true;
            _glideFrom = from;
        }
        else
        {
            StopGlide();
        }
    }

    private void StopGlide()
    {
        _pendingGlide = false;
        if (_gliding)
        {
            _gliding = false;
            _height?.Stop();
        }
    }

    private MotionValue<double> HeightValue()
    {
        if (_height is null)
        {
            _height = new MotionValue<double>(this, 0, MotionVectors.Double);
            _height.Changed += _ =>
            {
                if (!_measuring)
                {
                    InvalidateMeasure();
                }
            };
        }
        return _height;
    }

    private async void Glide(double to)
    {
        var height = HeightValue();
        _gliding = true;
        var reached = await height.AnimateTo(to, MotionSpec.Spring(LoginCenteredMotion.Smooth));
        if (reached && height.Target.Equals(to))
        {
            // Back to auto: the step's own height from now on.
            _gliding = false;
            if (!_measuring)
            {
                InvalidateMeasure();
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            var content = base.MeasureOverride(availableSize);
            var to = content.Height;
            if (_pendingGlide)
            {
                _pendingGlide = false;
                var from = _glideFrom;
                if (from > 0 && to > 0 && Math.Abs(from - to) > 0.01 && VisualRoot is not null)
                {
                    var height = HeightValue();
                    if (!_gliding)
                    {
                        height.Jump(from);
                    }
                    Glide(to);
                }
            }
            else if (_gliding && _height is not null && Math.Abs(_height.Target - to) > 0.01)
            {
                Glide(to);
            }
            var shown = _gliding && _height is not null ? _height.Value : to;
            _lastHeight = shown;
            return new Size(content.Width, shown);
        }
        finally
        {
            _measuring = false;
        }
    }
}
