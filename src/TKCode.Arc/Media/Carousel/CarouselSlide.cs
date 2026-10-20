using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Container of one <see cref="Carousel"/> slide (Arc <c>role="tabpanel" aria-roledescription="slide"</c>). Only the
/// active slide is interactive; the others are inert: no hit testing, no keyboard focus inside, hidden from assistive
/// technology. Scale and opacity follow the track position (set by the track panel every frame, no layout).
/// </summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class CarouselSlide : ContentControl
{
    public const string ActivePseudoClass = ":active";

    private readonly ScaleTransform _scale = new();
    private bool _isActive = true;

    public CarouselSlide()
    {
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
        Focusable = false;
        IsTabStop = false;
    }

    /// <summary>Item index this container currently shows.</summary>
    public int Index { get; set; } = -1;

    public bool IsActive => _isActive;

    public void SetActive(bool active)
    {
        _isActive = active;
        IsHitTestVisible = active;
        KeyboardNavigation.SetTabNavigation(this, active ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        PseudoClasses.Set(ActivePseudoClass, active);
    }

    public void SetLabel(string label) => AutomationProperties.SetName(this, label);

    /// <summary>Shrinks and dims with the distance from the center (in steps; capped at one).</summary>
    public void SetDistance(double distance)
    {
        var t = Math.Min(1, Math.Abs(distance));
        var scale = 1 - (1 - CarouselMath.NeighbourScale) * t;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Opacity = 1 - (1 - CarouselMath.NeighbourOpacity) * t;
    }

    protected override Type StyleKeyOverride => typeof(CarouselSlide);

    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselSlidePeer(this);

    private sealed class CarouselSlidePeer : ControlAutomationPeer
    {
        public CarouselSlidePeer(CarouselSlide owner)
            : base(owner)
        {
        }

        private bool Active => ((CarouselSlide)Owner).IsActive;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "slide";

        protected override bool IsContentElementCore() => Active;

        protected override bool IsControlElementCore() => Active;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => Active ? base.GetChildrenCore() : [];
    }
}
