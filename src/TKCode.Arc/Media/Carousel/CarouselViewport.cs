using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The slide viewport of a <see cref="Carousel"/> (Arc <c>role="group" aria-label="Slides"</c>, focusable). Only the
/// horizontal axis clips, so shadows and focus rings inside the active slide are never cut off at the top or bottom.
/// </summary>
internal sealed class CarouselViewport : Panel
{
    private readonly RectangleGeometry _clip = new();

    public CarouselViewport()
    {
        Focusable = true;
        IsTabStop = true;
        FocusAdorner = null;
        Clip = _clip;
        AutomationProperties.SetName(this, "Slides");
    }

    internal Carousel? Owner { get; set; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        // overflow-x: clip; overflow-y: visible.
        var rect = new Rect(0, -4096, finalSize.Width, finalSize.Height + 8192);
        if (_clip.Rect != rect)
        {
            _clip.Rect = rect;
            InvalidateVisual();
        }
        return arranged;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnViewportFocusChanged(IsFocused && Classes.Contains(":focus-visible"));
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnViewportFocusChanged(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselViewportPeer(this);

    private sealed class CarouselViewportPeer : ControlAutomationPeer
    {
        public CarouselViewportPeer(CarouselViewport owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
