using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A block whose height follows its content on the plan's layout spring and that folds open or closed with
/// <see cref="IsOpen"/>, fading its content in or out meanwhile (the footer, Add step, the empty-plan sentence, a step's
/// failure block). Code-driven because the spring is <see cref="AgentPlanMotion.Layout"/>, which no preset matches. While
/// closed the content is hidden, so nothing inside it can take the focus.
/// </summary>
internal sealed class AgentPlanCollapse : Decorator
{
    /// <summary>Defines the <see cref="IsOpen"/> property.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AgentPlanCollapse, bool>(nameof(IsOpen), true);

    private readonly AgentPlanHeight _height;
    private readonly MotionValue<double> _fade;
    private readonly RectangleGeometry _clip = new();
    private bool _measuredOnce;

    public AgentPlanCollapse()
    {
        _height = new AgentPlanHeight(this);
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += v =>
        {
            if (Child is { } child)
            {
                child.Opacity = v;
            }
        };
        ClipToBounds = false;
    }

    /// <summary>Open shows the content at its height; closed folds it to nothing.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>How long the content fades in when it opens (default 160ms).</summary>
    public TimeSpan EnterFade { get; set; } = AgentPlanMotion.RowEnter;

    /// <summary>How long the content fades out when it closes (default 60ms).</summary>
    public TimeSpan ExitFade { get; set; } = AgentPlanMotion.BlockExit;

    /// <summary>The height now (tests).</summary>
    public double CurrentHeight => _height.Value;

    /// <summary>True while the height moves.</summary>
    public bool IsMoving => _height.IsMoving;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            OnOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == ChildProperty && change.NewValue is Control child)
        {
            if (!IsOpen)
            {
                _fade.Jump(0);
            }
            child.Opacity = _fade.Value;
            child.IsVisible = IsOpen;
        }
    }

    private void OnOpenChanged(bool open)
    {
        var child = Child;
        if (child is null)
        {
            return;
        }
        var animate = IsLoaded && _measuredOnce && AgentPlanMotion.Fades(this);
        if (open)
        {
            child.IsVisible = true;
            if (animate)
            {
                _ = _fade.AnimateTo(1, AgentPlanMotion.Enter(EnterFade));
            }
            else
            {
                _fade.Jump(1);
            }
        }
        else if (animate)
        {
            FadeOut();
        }
        else
        {
            _fade.Jump(0);
            child.IsVisible = false;
        }
        InvalidateMeasure();
    }

    private async void FadeOut()
    {
        // Once faded out the content is hidden, so nothing folded away takes the focus; the height keeps springing to 0.
        var done = await _fade.AnimateTo(0, AgentPlanMotion.Enter(ExitFade));
        if (done && !IsOpen && Child is { } child)
        {
            child.IsVisible = false;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var child = Child;
        if (child is null)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var target = IsOpen ? child.DesiredSize.Height : 0;
        _height.To(target, animate: _measuredOnce);
        _measuredOnce = true;
        var height = _height.Value;
        return new Size(child.DesiredSize.Width, Math.Min(height, availableSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child is null)
        {
            return finalSize;
        }
        var full = Math.Max(child.DesiredSize.Height, finalSize.Height);
        child.Arrange(new Rect(0, 0, finalSize.Width, IsOpen ? full : child.DesiredSize.Height));
        // Clip only while the content is taller than the block (moving or folded), so focus rings stay whole at rest.
        if (child.DesiredSize.Height > finalSize.Height + 0.5 || !IsOpen)
        {
            _clip.Rect = new Rect(-8, 0, finalSize.Width + 16, finalSize.Height);
            Clip = _clip;
            InvalidateVisual();
        }
        else if (Clip is not null)
        {
            Clip = null;
            InvalidateVisual();
        }
        return finalSize;
    }
}
