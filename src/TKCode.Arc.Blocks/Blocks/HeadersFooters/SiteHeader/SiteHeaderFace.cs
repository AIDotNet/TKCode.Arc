// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli): the panel face variants of site-header.tsx.
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The body of the mega panel: faces stacked in one cell. Only the current face takes room; a leaving face is laid out
/// at the top over it and ignores the pointer (Arc: <c>position: absolute</c> and <c>inert</c>), so the panel's height is
/// always the current face's natural height.
/// </summary>
internal sealed class SiteHeaderFaceHost : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var height = 0.0;
        var width = 0.0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            if (child is not SiteHeaderFace { IsLeaving: true })
            {
                height = Math.Max(height, child.DesiredSize.Height);
            }
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            var leaving = child is SiteHeaderFace { IsLeaving: true };
            child.Arrange(new Rect(0, 0, finalSize.Width, leaving ? child.DesiredSize.Height : Math.Max(finalSize.Height, child.DesiredSize.Height)));
        }
        return finalSize;
    }
}

/// <summary>
/// One face of the mega panel with Arc's face motion. Entering: opacity 0 to 1 (20 ms delay, 160 ms enter curve), x from
/// the side of travel (±36) to 0 and, when the panel was just opened (no direction), y from -6 to 0, both on the SLIDE
/// spring (visualDuration .4), blur 2 to 0 over 240 ms. Leaving: opacity to 0 and blur to 2 over 120 ms, x to
/// -direction × 21.6 on SLIDE. Reduced motion only fades (160 / 120 ms); Off jumps.
/// </summary>
/// <remarks>
/// Code-driven: the face is kept alive after its item changed and animated out while the next one animates in, with
/// velocity carried by the springs; a ControlTheme cannot keep a removed element on screen.
/// </remarks>
internal sealed class SiteHeaderFace : Decorator
{
    private const double Travel = 36;

    private static readonly SpringSpec s_slide = SpringSpec.FromVisual(0.4, 0);

    private readonly TranslateTransform _translate = new();
    private MotionValue<double>? _x;
    private MotionValue<double>? _y;
    private MotionValue<double>? _opacity;
    private MotionValue<double>? _blur;

    public SiteHeaderFace()
    {
        RenderTransform = _translate;
    }

    /// <summary>True once the face has started to leave: it floats out of layout and takes no input.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>The panel item this face shows.</summary>
    public string? ItemValue { get; set; }

    /// <summary>The offset the face is drawn at now (tests read it).</summary>
    public Vector Offset => new(_translate.X, _translate.Y);

    private MotionValue<double> Make(ref MotionValue<double>? field, double initial, Action<double> apply)
    {
        if (field is null)
        {
            field = new MotionValue<double>(this, initial, MotionVectors.Double);
            field.Changed += apply;
        }
        return field;
    }

    private MotionValue<double> X => Make(ref _x, 0, v => _translate.X = v);

    private MotionValue<double> Y => Make(ref _y, 0, v => _translate.Y = v);

    private MotionValue<double> Opacity01 => Make(ref _opacity, 1, v => Opacity = v);

    private MotionValue<double> Blur => Make(ref _blur, 0, v => Effect = ArcBlur.Get(v));

    /// <summary>Plays the entrance for a panel item reached with <paramref name="direction"/> (-1, 0, 1).</summary>
    public void Enter(int direction)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        if (level == ArcMotionLevel.Full)
        {
            X.Jump(direction * Travel);
            Y.Jump(direction != 0 ? 0 : -6);
            Blur.Jump(2);
            _ = X.AnimateTo(0, MotionSpec.Spring(s_slide));
            _ = Y.AnimateTo(0, MotionSpec.Spring(s_slide));
            _ = Blur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
            Opacity01.Jump(0);
            _ = Opacity01.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter, TimeSpan.FromMilliseconds(20)));
            return;
        }
        // Reduced: a short fade only.
        Opacity01.Jump(0);
        _ = Opacity01.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    /// <summary>Marks the face as leaving and plays its exit; the task ends when every part has finished.</summary>
    public Task Leave(int direction)
    {
        IsLeaving = true;
        IsHitTestVisible = false;
        Focusable = false;
        InvalidateMeasure();
        (Parent as Control)?.InvalidateMeasure();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return Task.CompletedTask;
        }
        if (level == ArcMotionLevel.Full)
        {
            return Task.WhenAll(
                X.AnimateTo(-direction * Travel * 0.6, MotionSpec.Spring(s_slide)),
                Blur.AnimateTo(2, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)),
                Opacity01.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)));
        }
        return Opacity01.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }
}
