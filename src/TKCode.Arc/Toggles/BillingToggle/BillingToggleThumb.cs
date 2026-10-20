using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The one thumb under a <see cref="BillingToggle"/>'s options. It takes the selected option's measured x and width and
/// glides on a critically damped spring (visual duration 0.34s, no bounce), which is not one of the GlidingIndicator
/// presets, so the thumb keeps its own motion value. Drawn in <see cref="Render"/>; the glide never relayouts.
/// </summary>
internal sealed class BillingToggleThumb : Control, ITogglesIndicator
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<BillingToggleThumb, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<BillingToggleThumb, BoxShadows>(nameof(Shadow));

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>Arc's <c>glide</c>: <c>{ type: "spring", visualDuration: 0.34, bounce: 0 }</c>.</summary>
    internal static readonly SpringSpec Glide = SpringSpec.FromVisual(0.34, 0);

    private MotionValue<Rect>? _rect;
    private bool _shown;

    static BillingToggleThumb()
    {
        AffectsRender<BillingToggleThumb>(FillProperty, ShadowProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<BillingToggleThumb>(false);
    }

    public Visual Visual => this;

    internal Rect CurrentRect => _rect?.Value ?? default;

    public void MoveTo(Rect target, bool animate)
    {
        if (_rect is null)
        {
            _rect = new MotionValue<Rect>(this, target, MotionVectors.Rect);
            _rect.Changed += _ => InvalidateVisual();
        }
        if (!_shown || !animate || !IsLoaded)
        {
            _shown = true;
            _rect.Jump(target);
            return;
        }
        _ = _rect.AnimateTo(target, MotionSpec.Spring(Glide));
    }

    public void Hide(bool animate)
    {
        _shown = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_shown || _rect is null)
        {
            return;
        }
        var rect = _rect.Value;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = rect.Height / 2;
        context.DrawRectangle(Fill, null, new RoundedRect(rect, radius), Shadow);
    }
}
