// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): the motion of sign-in.tsx (stepMotion,
// useStepHeight, the shared account avatar, the ring, the badge and rise()). Code-driven: steps enter and leave as
// presences with a direction, the height target is measured, and the avatar flies between two separate steps.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class SignIn
{
    /// <summary>What the signed in step has to play once its parts exist.</summary>
    private sealed record DoneEntrance(ArcMotionLevel Level, Rect? MorphFrom, SignInCodeStep? FollowStep);

    private DoneEntrance? _doneEntrance;

    /// <summary>The avatar flight that runs now (tests).</summary>
    internal MotionValue<double>? AvatarMorph { get; private set; }

    private ArcMotionLevel EffectiveLevel => IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    /// <summary>Swaps the step (Arc's <c>AnimatePresence mode="popLayout"</c> with <c>custom={direction, reduce}</c>).</summary>
    private void TransitionTo(SignInStep next, int direction)
    {
        if (_host is null)
        {
            return;
        }
        var level = EffectiveLevel;
        var old = _current;
        _doneEntrance = null;
        if (next == SignInStep.Done)
        {
            Rect? from = null;
            SignInCodeStep? follow = null;
            // The code step's avatar grows into the signed in one (Arc's layoutId="account-avatar"; an email code only).
            if (level == ArcMotionLevel.Full && old is SignInCodeStep { HasParts: true } code && Method is { Kind: SignInMethodKind.EmailCode } &&
                code.Avatar.Bounds.Width > 0)
            {
                from = LayoutRect(code.Avatar, _host);
                follow = code;
            }
            _doneEntrance = new DoneEntrance(level, from, follow);
        }
        _viewport?.BeginGlide();
        if (old is not null)
        {
            ExitStep(old, direction, level);
        }
        ShowStep(next, direction, animate: false);
        if (_current is { } entering)
        {
            EnterStep(entering, direction, still: next == SignInStep.Done, level);
        }
    }

    private void EnterStep(SignInStepBase step, int direction, bool still) => EnterStep(step, direction, still, EffectiveLevel);

    /// <summary>
    /// Arc's <c>stepMotion.enter → center</c>: from 28px on the side it comes from, faded and blurred 4px; x on the smooth
    /// spring, opacity over 240ms on the enter curve after 50ms, the blur over 240ms. Reduced: a 120ms fade. The signed in
    /// step is <c>still</c>: it is simply there.
    /// </summary>
    private static void EnterStep(SignInStepBase step, int direction, bool still, ArcMotionLevel level)
    {
        if (still || level == ArcMotionLevel.Off)
        {
            return;
        }
        var layer = step.Motion;
        if (level == ArcMotionLevel.Full)
        {
            layer.X.Jump(28 * direction);
            layer.Opacity.Jump(0);
            layer.Blur.Jump(4);
            _ = layer.X.AnimateTo(0, SignInMotion.Spring(SignInMotion.Smooth));
            _ = layer.Opacity.AnimateTo(1, SignInMotion.Tween(240, ArcEasings.Enter, 50));
            _ = layer.Blur.AnimateTo(0, SignInMotion.Tween(240, ArcEasings.Enter));
            return;
        }
        layer.Opacity.Jump(0);
        _ = layer.Opacity.AnimateTo(1, SignInMotion.Tween(120, SignInMotion.EaseOut));
    }

    /// <summary>
    /// Arc's <c>stepMotion.exit</c>: the step leaves the flow at once and slides 20px the other way on the smooth spring
    /// while it fades (180ms, standard curve) into a 4px blur; it is removed once the slide settles. Reduced and off: gone at once.
    /// </summary>
    private async void ExitStep(SignInStepBase step, int direction, ArcMotionLevel level)
    {
        var host = _host;
        if (host is null)
        {
            return;
        }
        host.BeginExit(step);
        if (level != ArcMotionLevel.Full)
        {
            host.Remove(step);
            return;
        }
        var layer = step.Motion;
        _ = layer.Opacity.AnimateTo(0, SignInMotion.Tween(180, ArcEasings.Standard));
        _ = layer.Blur.AnimateTo(4, SignInMotion.Tween(180, SignInMotion.EaseOut));
        await layer.X.AnimateTo(-20 * direction, SignInMotion.Spring(SignInMotion.Smooth));
        if (host.Children.Contains(step) && !ReferenceEquals(host.Current, step))
        {
            host.Remove(step);
        }
    }

    /// <summary>The signed in step's own entrance: the avatar, the ring, the badge and the three rising groups.</summary>
    private void OnDonePartsReady(SignInDoneStep done)
    {
        var entrance = _doneEntrance;
        if (entrance is null || !ReferenceEquals(done, _current))
        {
            return;
        }
        _doneEntrance = null;
        var level = entrance.Level;
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        var full = level == ArcMotionLevel.Full;

        // The ring draws itself clockwise from 12 o'clock (600ms in-out after 220ms) as it fades in (120ms). Reduced: drawn.
        if (full)
        {
            var ring = done.Ring;
            var ringLayer = new SignInLayer(ring);
            var draw = new MotionValue<double>(ring, 0, MotionVectors.Double);
            draw.Changed += v => ring.Value = v;
            ringLayer.Opacity.Jump(0);
            draw.Jump(0);
            _ = draw.AnimateTo(1, SignInMotion.Tween(ArcMotionPresets.Considered.TotalMilliseconds * 1.25, ArcEasings.InOut, 220));
            _ = ringLayer.Opacity.AnimateTo(1, SignInMotion.Tween(120, SignInMotion.EaseOut, 220));

            // The check pops in last (snappy spring after 780ms).
            var badge = new SignInLayer(done.Badge);
            badge.Opacity.Jump(0);
            badge.Scale.Jump(0.4);
            _ = badge.Opacity.AnimateTo(1, SignInMotion.Spring(SignInMotion.Snappy, 780));
            _ = badge.Scale.AnimateTo(1, SignInMotion.Spring(SignInMotion.Snappy, 780));
        }

        // The avatar: flown in from the code step, or (passkey, provider) grown from .7 as it fades in on the morph spring.
        if (full && entrance.MorphFrom is { } from && entrance.FollowStep is { } follow)
        {
            StartAvatarMorph(done, from, follow);
        }
        else if (full)
        {
            var avatar = new SignInLayer(done.Avatar);
            avatar.Opacity.Jump(0);
            avatar.Scale.Jump(0.7);
            _ = avatar.Opacity.AnimateTo(1, SignInMotion.Spring(SignInMotion.Morph));
            _ = avatar.Scale.AnimateTo(1, SignInMotion.Spring(SignInMotion.Morph));
        }

        // rise(index): the welcome, the details and Sign out rise 10px out of a 4px blur, .14s + index × 80ms late.
        Control[] items = [done.Heading, done.Details, done.SignOutHost];
        for (var i = 0; i < items.Length; i++)
        {
            var layer = new SignInLayer(items[i]);
            layer.Opacity.Jump(0);
            if (!full)
            {
                _ = layer.Opacity.AnimateTo(1, SignInMotion.Tween(120, SignInMotion.EaseOut));
                continue;
            }
            var delay = 140 + i * ArcMotionPresets.StaggerLine.TotalMilliseconds;
            layer.Y.Jump(10);
            layer.Blur.Jump(4);
            _ = layer.Y.AnimateTo(0, SignInMotion.Spring(SignInMotion.Smooth, delay));
            _ = layer.Opacity.AnimateTo(1, SignInMotion.Tween(240, ArcEasings.Enter, delay));
            _ = layer.Blur.AnimateTo(0, SignInMotion.Tween(240, SignInMotion.EaseOut, delay));
        }
    }

    /// <summary>
    /// Arc's shared layout avatar: one box flies from the code step's 32px avatar to the signed in 88px one on the morph
    /// spring. The new avatar rides the box and fades in over the first half (motion's circOut crossfade); the old one rides
    /// the same box inside its leaving step and fades over 50–95% of the flight. Starts after the new step's first layout.
    /// </summary>
    private void StartAvatarMorph(SignInDoneStep done, Rect from, SignInCodeStep followStep)
    {
        var lead = done.Avatar;
        var follow = followStep.Avatar;
        var leadTransform = new MatrixTransform();
        var followTransform = new MatrixTransform();
        lead.RenderTransformOrigin = RelativePoint.TopLeft;
        lead.RenderTransform = leadTransform;
        lead.Opacity = 0;
        var progress = new MotionValue<double>(lead, 0, MotionVectors.Double);
        AvatarMorph = progress;
        progress.Changed += p => ApplyAvatarMorph(p, from, lead, leadTransform, follow, followTransform, followStep);
        EventHandler? onLayout = null;
        onLayout = async (_, _) =>
        {
            if (lead.Bounds.Width <= 0 || _host is null)
            {
                return;
            }
            done.LayoutUpdated -= onLayout;
            follow.RenderTransformOrigin = RelativePoint.TopLeft;
            follow.RenderTransform = followTransform;
            ApplyAvatarMorph(0, from, lead, leadTransform, follow, followTransform, followStep);
            await progress.AnimateTo(1, SignInMotion.Spring(SignInMotion.Morph));
            leadTransform.Matrix = Matrix.Identity;
            lead.Opacity = 1;
            if (ReferenceEquals(AvatarMorph, progress))
            {
                AvatarMorph = null;
            }
        };
        done.LayoutUpdated += onLayout;
    }

    private void ApplyAvatarMorph(double p, Rect from, SignInAvatar lead, MatrixTransform leadTransform, SignInAvatar follow,
        MatrixTransform followTransform, SignInCodeStep followStep)
    {
        if (_host is null)
        {
            return;
        }
        var to = LayoutRect(lead, _host);
        if (to.Width <= 0 || to.Height <= 0 || from.Width <= 0 || from.Height <= 0)
        {
            return;
        }
        var box = new Rect(
            from.X + (to.X - from.X) * p,
            from.Y + (to.Y - from.Y) * p,
            from.Width + (to.Width - from.Width) * p,
            from.Height + (to.Height - from.Height) * p);
        leadTransform.Matrix = Matrix.CreateScale(box.Width / to.Width, box.Height / to.Height) *
                               Matrix.CreateTranslation(box.X - to.X, box.Y - to.Y);
        lead.Opacity = SignInMotion.CircOut(Math.Min(1, 2 * p));
        // The old avatar sits in a step that is sliding away; take its slide out so both ride the same box.
        var slide = followStep.Motion.Offset;
        followTransform.Matrix = Matrix.CreateScale(box.Width / from.Width, box.Height / from.Height) *
                                 Matrix.CreateTranslation(box.X - from.X - slide.X, box.Y - from.Y - slide.Y);
        follow.Opacity = 1 - Math.Clamp((p - 0.5) / 0.45, 0, 1);
    }

    /// <summary>The layout box of <paramref name="visual"/> in <paramref name="ancestor"/>'s space, without render transforms.</summary>
    internal static Rect LayoutRect(Visual visual, Visual ancestor)
    {
        var x = 0.0;
        var y = 0.0;
        Visual? current = visual;
        while (current is not null && !ReferenceEquals(current, ancestor))
        {
            x += current.Bounds.X;
            y += current.Bounds.Y;
            current = current.GetVisualParentSafe();
        }
        return new Rect(x, y, visual.Bounds.Width, visual.Bounds.Height);
    }
}

internal static class SignInVisualExtensions
{
    public static Visual? GetVisualParentSafe(this Visual visual) => Avalonia.VisualTree.VisualExtensions.GetVisualParent(visual);
}
