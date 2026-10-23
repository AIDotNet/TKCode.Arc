using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The panel of the selected tab inside the page scroller (Arc's panelSlide). Two pooled presenters take turns: the
/// incoming panel slides 12px in from the side of travel (x on the smooth spring, opacity standard + enter) while the
/// outgoing one leaves the layout, drifts 8px the other way and fades in 120ms (instant + standard). Reduced motion
/// crossfades in 120ms without travel (panelFade); Off switches at once. The page scroller follows the new panel's
/// height directly. Code-driven because entering and leaving run different specs on the same properties and the
/// slide spring must keep its velocity when the selection changes again mid-way.
/// </summary>
internal sealed class PageHeaderBody : Control
{
    private static readonly Easing LinearEase = new LinearEasing();

    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<PageHeaderBody, IBrush?>(nameof(FocusRingBrush));

    public static readonly StyledProperty<CornerRadius> FocusRingRadiusProperty =
        AvaloniaProperty.Register<PageHeaderBody, CornerRadius>(nameof(FocusRingRadius));

    public IBrush? FocusRingBrush
    {
        get => GetValue(FocusRingBrushProperty);
        set => SetValue(FocusRingBrushProperty, value);
    }

    public CornerRadius FocusRingRadius
    {
        get => GetValue(FocusRingRadiusProperty);
        set => SetValue(FocusRingRadiusProperty, value);
    }

    private sealed class Slot
    {
        public required ContentPresenter Presenter { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Opacity { get; set; }
        public MotionValue<double>? X { get; set; }
        public int Generation { get; set; }
    }

    private readonly Slot _a;
    private readonly Slot _b;
    private Slot _current;
    private Slot? _exiting;
    private Pen? _ringPen;

    static PageHeaderBody()
    {
        FocusableProperty.OverrideDefaultValue<PageHeaderBody>(true);
        AffectsRender<PageHeaderBody>(FocusRingBrushProperty, FocusRingRadiusProperty);
    }

    public PageHeaderBody()
    {
        _a = CreateSlot();
        _b = CreateSlot();
        _current = _a;
    }

    /// <summary>The presenter showing the selected panel.</summary>
    internal ContentPresenter CurrentPresenter => _current.Presenter;

    /// <summary>The presenter of the panel that is leaving, if any.</summary>
    internal ContentPresenter? ExitingPresenter => _exiting?.Presenter;

    private Slot CreateSlot()
    {
        var translate = new TranslateTransform();
        var presenter = new ContentPresenter { RenderTransform = translate };
        LogicalChildren.Add(presenter);
        VisualChildren.Add(presenter);
        return new Slot { Presenter = presenter, Translate = translate };
    }

    /// <summary>Shows <paramref name="content"/>; <paramref name="direction"/> is +1 when the new tab is after the old one.</summary>
    public void Show(object? content, IDataTemplate? template, int direction, bool animate)
    {
        var presenter = _current.Presenter;
        if (Equals(presenter.Content, content) && ReferenceEquals(presenter.ContentTemplate, template))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || !IsEffectivelyVisible || presenter.Content is null || content is null)
        {
            FinishExit();
            ResetSlot(_current);
            Assign(presenter, content, template);
            InvalidateMeasure();
            return;
        }

        if (_exiting is not null && Equals(_exiting.Presenter.Content, content))
        {
            // Back to the panel that is still leaving: the presenters swap roles, nothing is re-templated.
            (_current, _exiting) = (_exiting, _current);
        }
        else
        {
            FinishExit();
            var next = ReferenceEquals(_current, _a) ? _b : _a;
            _exiting = _current;
            _current = next;
            ResetSlot(next);
            Assign(next.Presenter, content, template);
            next.Presenter.Opacity = 0;
        }

        var dir = direction >= 0 ? 1 : -1;
        var full = level == ArcMotionLevel.Full;
        StartExit(_exiting, dir, full);
        StartEnter(_current, dir, full);
        InvalidateMeasure();
    }

    private void StartEnter(Slot slot, int dir, bool full)
    {
        SetInert(slot, false);
        slot.Generation++;
        slot.Opacity ??= CreateOpacity(slot);
        slot.X ??= CreateX(slot);
        if (!slot.Opacity.IsAnimating)
        {
            slot.Opacity.Jump(slot.Presenter.Opacity);
        }
        if (full)
        {
            if (slot.Presenter.Opacity <= 0.001)
            {
                slot.X.Jump(dir * 12);
            }
            _ = slot.X.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            _ = slot.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else
        {
            slot.X.Jump(0);
            _ = slot.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, LinearEase));
        }
    }

    private async void StartExit(Slot slot, int dir, bool full)
    {
        SetInert(slot, true);
        var generation = ++slot.Generation;
        slot.Opacity ??= CreateOpacity(slot);
        slot.X ??= CreateX(slot);
        if (!slot.Opacity.IsAnimating)
        {
            slot.Opacity.Jump(slot.Presenter.Opacity);
        }
        Task<bool> fade;
        if (full)
        {
            var spec = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
            _ = slot.X.AnimateTo(-8 * dir, spec);
            fade = slot.Opacity.AnimateTo(0, spec);
        }
        else
        {
            slot.X.Jump(0);
            fade = slot.Opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, LinearEase));
        }
        await fade;
        if (ReferenceEquals(_exiting, slot) && slot.Generation == generation)
        {
            FinishExit();
        }
    }

    private void FinishExit()
    {
        if (_exiting is null)
        {
            return;
        }
        var slot = _exiting;
        _exiting = null;
        slot.Generation++;
        slot.Opacity?.Stop();
        slot.X?.Stop();
        Assign(slot.Presenter, null, null);
        ResetSlot(slot);
        InvalidateMeasure();
    }

    /// <summary>
    /// Puts content and template on a presenter. A presenter builds the template it has whenever its content changes, and a template
    /// that is attached while the content is still null is built with null (a template reading its data then throws): the
    /// template goes on last, and only with content.
    /// </summary>
    private static void Assign(ContentPresenter presenter, object? content, IDataTemplate? template)
    {
        presenter.ContentTemplate = null;
        presenter.Content = content;
        presenter.ContentTemplate = content is null ? null : template;
    }

    private static void ResetSlot(Slot slot)
    {
        slot.Opacity?.Jump(1);
        slot.X?.Jump(0);
        slot.Presenter.Opacity = 1;
        slot.Translate.X = 0;
        SetInert(slot, false);
    }

    /// <summary>A leaving panel ignores the pointer and the Tab key while it fades (Arc: popLayout exit).</summary>
    private static void SetInert(Slot slot, bool inert)
    {
        slot.Presenter.IsHitTestVisible = !inert;
        KeyboardNavigation.SetTabNavigation(slot.Presenter, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
    }

    private MotionValue<double> CreateOpacity(Slot slot)
    {
        var value = new MotionValue<double>(this, slot.Presenter.Opacity, MotionVectors.Double);
        value.Changed += v => slot.Presenter.Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    private MotionValue<double> CreateX(Slot slot)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v => slot.Translate.X = v;
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _exiting?.Presenter.Measure(availableSize);
        var presenter = _current.Presenter;
        presenter.Measure(availableSize);
        return presenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _current.Presenter.Arrange(new Rect(finalSize));
        if (_exiting is not null)
        {
            // popLayout: the leaving panel is out of the flow and keeps its own size where it was.
            var leaving = _exiting.Presenter;
            leaving.Arrange(new Rect(0, 0, finalSize.Width, leaving.DesiredSize.Height));
        }
        return finalSize;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!IsFocused || !Classes.Contains(":focus-visible") || FocusRingBrush is not { } brush)
        {
            return;
        }
        if (_ringPen is null || !ReferenceEquals(_ringPen.Brush, brush))
        {
            _ringPen = new Pen(brush, 2);
        }
        var rect = new Rect(Bounds.Size).Inflate(4);
        var r = FocusRingRadius;
        context.DrawRectangle(null, _ringPen, new RoundedRect(rect, r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft));
    }
}
