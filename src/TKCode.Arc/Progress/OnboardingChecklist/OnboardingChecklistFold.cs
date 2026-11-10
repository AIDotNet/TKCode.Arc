// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): the collapse transitions.
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Which of the source's folds an <see cref="OnboardingChecklistFold"/> plays.</summary>
internal enum OnboardingChecklistFoldKind
{
    /// <summary>Rows, panels and the error slot (Arc <c>collapse</c>).</summary>
    Row,

    /// <summary>The card's body when it hides and shows.</summary>
    Body,

    /// <summary>A digit column that joins or leaves a number: its width, on the morph spring.</summary>
    Place
}

/// <summary>
/// Opens and closes its child's height (or width) from the measured size, with the opacity on a tween of its own, and
/// says when a close has finished so the owner can remove the row. Code-driven because a ControlTheme cannot follow a
/// measured size or report the end of a close; SpringSizeDecorator and PresenceHost have neither the separate opacity
/// timing nor the source's closing springs (0.32 and 0.34). A closed fold takes no pointer or keyboard focus.
/// </summary>
internal sealed class OnboardingChecklistFold : Decorator
{
    private static readonly SpringSpec s_rowClose = SpringSpec.FromVisual(0.32, 0);
    private static readonly SpringSpec s_bodyClose = SpringSpec.FromVisual(0.34, 0);
    private static readonly TimeSpan s_rowOpenDelay = TimeSpan.FromSeconds(0.04);
    private static readonly TimeSpan s_bodyOpenDelay = TimeSpan.FromSeconds(0.05);
    private static readonly TimeSpan s_reducedFade = TimeSpan.FromSeconds(0.15);

    private MotionValue<double>? _size;
    private MotionValue<double>? _fade;
    private RectangleGeometry? _clip;
    private double _rest = 1;
    private int _run;

    public OnboardingChecklistFoldKind Kind { get; set; }

    public bool IsOpen { get; private set; } = true;

    /// <summary>Raised when a close has finished and the child is hidden.</summary>
    public event EventHandler? Closed;

    private double Fraction => _size?.Value ?? _rest;

    /// <summary>True while the size is moving.</summary>
    internal bool IsMoving => _size?.IsAnimating == true;

    public void SetOpen(bool open, bool animate)
    {
        if (open == IsOpen)
        {
            return;
        }
        IsOpen = open;
        ExpandInert.Apply(this, !open);
        if (open && Child is { } child)
        {
            child.IsVisible = true;
        }
        var level = animate && this.IsAttachedToVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var run = ++_run;
        if (level == ArcMotionLevel.Off || (level == ArcMotionLevel.Reduced && (Kind != OnboardingChecklistFoldKind.Row || !open)))
        {
            Jump(open);
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            // Reduced motion: the height is there at once and only the opacity runs.
            JumpSize(1);
            var reduced = FadeValue();
            reduced.Jump(0);
            _ = reduced.AnimateTo(1, MotionSpec.Tween(s_reducedFade, ArcEasings.Standard));
            return;
        }
        _ = FadeValue().AnimateTo(open ? 1 : 0, FadeSpec(open));
        _ = FinishWhenDone(SizeValue().AnimateTo(open ? 1 : 0, SizeSpec(open)), run);
    }

    private MotionSpec SizeSpec(bool open)
    {
        if (Kind == OnboardingChecklistFoldKind.Place)
        {
            return MotionSpec.Spring(ArcSpring.Morph);
        }
        if (open)
        {
            return MotionSpec.Spring(ArcSpring.Smooth);
        }
        return MotionSpec.Spring(Kind == OnboardingChecklistFoldKind.Body ? s_bodyClose : s_rowClose);
    }

    private MotionSpec FadeSpec(bool open)
    {
        if (Kind == OnboardingChecklistFoldKind.Place || !open)
        {
            return MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        }
        return MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, Kind == OnboardingChecklistFoldKind.Body ? s_bodyOpenDelay : s_rowOpenDelay);
    }

    private async Task FinishWhenDone(Task<bool> size, int run)
    {
        // The value completes its task from the frame, on the UI thread.
        var reached = await size;
        if (reached && run == _run && !IsOpen)
        {
            Finish();
        }
    }

    private void Finish()
    {
        if (Child is { } child)
        {
            child.IsVisible = false;
        }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Jump(bool open)
    {
        JumpSize(open ? 1 : 0);
        _fade?.Jump(open ? 1 : 0);
        Opacity = open ? 1 : 0;
        if (!open)
        {
            Finish();
        }
    }

    private void JumpSize(double value)
    {
        _rest = value;
        _size?.Jump(value);
        InvalidateMeasure();
    }

    private MotionValue<double> SizeValue()
    {
        if (_size is null)
        {
            _size = new MotionValue<double>(this, _rest, MotionVectors.Double);
            _size.Changed += _ => InvalidateMeasure();
        }
        return _size;
    }

    private MotionValue<double> FadeValue()
    {
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, Opacity, MotionVectors.Double);
            _fade.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        }
        return _fade;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChildProperty && !IsOpen && change.NewValue is Control child)
        {
            child.IsVisible = false;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        var fraction = Math.Max(0, Fraction);
        if (Kind == OnboardingChecklistFoldKind.Place)
        {
            child.Measure(availableSize.WithWidth(double.PositiveInfinity));
            return new Size(child.DesiredSize.Width * fraction, child.DesiredSize.Height);
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * fraction);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(Kind == OnboardingChecklistFoldKind.Place
                ? new Rect(0, 0, child.DesiredSize.Width, finalSize.Height)
                : new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
        }
        UpdateClip(finalSize);
        return finalSize;
    }

    /// <summary>Clips only while it folds, so focus rings stay whole at rest. A digit column clips sideways only, so a turning digit is never cut.</summary>
    private void UpdateClip(Size size)
    {
        if (!IsMoving && Fraction >= 1)
        {
            if (Clip is not null)
            {
                Clip = null;
            }
            return;
        }
        _clip ??= new RectangleGeometry();
        _clip.Rect = Kind == OnboardingChecklistFoldKind.Place
            ? new Rect(0, -size.Height, size.Width, size.Height * 3)
            : new Rect(size);
        Clip = _clip;
        // A changed clip geometry reaches the composition layer only with a redraw.
        InvalidateVisual();
    }
}
