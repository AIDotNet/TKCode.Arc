using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The full-window lightbox of a <see cref="LightboxGallery"/>, shown on the window's overlay layer: the picture area,
/// a top bar with the counter, zoom buttons and close, arrows at the sides and the caption underneath. It is a dialog to
/// assistive technology.
/// </summary>
[TemplatePart(ViewportPart, typeof(LightboxGalleryViewport))]
[TemplatePart(ChromePart, typeof(Panel))]
[TemplatePart(CounterPart, typeof(TextBlock))]
[TemplatePart(CaptionPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(CloseButtonPart, typeof(Button))]
[TemplatePart(ZoomInPart, typeof(Button))]
[TemplatePart(ZoomOutPart, typeof(Button))]
[TemplatePart(PreviousPart, typeof(Button))]
[TemplatePart(NextPart, typeof(Button))]
[PseudoClasses(ZoomedPseudoClass, SinglePseudoClass, CompactPseudoClass)]
internal sealed class LightboxGalleryView : TemplatedControl
{
    internal const string ViewportPart = "PART_Viewport";
    internal const string ChromePart = "PART_Chrome";
    internal const string CounterPart = "PART_Counter";
    internal const string CaptionPart = "PART_Caption";
    internal const string StatusPart = "PART_Status";
    internal const string CloseButtonPart = "PART_Close";
    internal const string ZoomInPart = "PART_ZoomIn";
    internal const string ZoomOutPart = "PART_ZoomOut";
    internal const string PreviousPart = "PART_Previous";
    internal const string NextPart = "PART_Next";
    internal const string ZoomedPseudoClass = ":zoomed";
    internal const string SinglePseudoClass = ":single";
    internal const string CompactPseudoClass = ":compact";

    private readonly LightboxGallery _owner;
    private Button? _close;
    private Button? _zoomIn;
    private Button? _zoomOut;
    private Button? _previous;
    private Button? _next;

    public LightboxGalleryView(LightboxGallery owner)
    {
        _owner = owner;
    }

    internal LightboxGalleryViewport? Viewport { get; private set; }

    internal Panel? Chrome { get; private set; }

    internal TextBlock? Counter { get; private set; }

    internal TextBlock? Caption { get; private set; }

    internal TextBlock? Status { get; private set; }

    internal Button? CloseButton => _close;

    internal Button? ZoomInButton => _zoomIn;

    internal Button? ZoomOutButton => _zoomOut;

    internal Button? PreviousButton => _previous;

    internal Button? NextButton => _next;

    /// <summary>Raised when a new template has been applied (the owner re-reads the parts).</summary>
    internal event EventHandler? PartsChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook(_close, OnClose);
        Unhook(_zoomIn, OnZoomIn);
        Unhook(_zoomOut, OnZoomOut);
        Unhook(_previous, OnPrevious);
        Unhook(_next, OnNext);
        if (Viewport is not null)
        {
            Viewport.Owner = null;
        }
        Viewport = e.NameScope.Find<LightboxGalleryViewport>(ViewportPart);
        Chrome = e.NameScope.Find<Panel>(ChromePart);
        Counter = e.NameScope.Find<TextBlock>(CounterPart);
        Caption = e.NameScope.Find<TextBlock>(CaptionPart);
        Status = e.NameScope.Find<TextBlock>(StatusPart);
        _close = Hook(e, CloseButtonPart, OnClose);
        _zoomIn = Hook(e, ZoomInPart, OnZoomIn);
        _zoomOut = Hook(e, ZoomOutPart, OnZoomOut);
        _previous = Hook(e, PreviousPart, OnPrevious);
        _next = Hook(e, NextPart, OnNext);
        if (Viewport is not null)
        {
            Viewport.Owner = _owner;
        }
        PartsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Button? Hook(TemplateAppliedEventArgs e, string name, EventHandler<RoutedEventArgs> handler)
    {
        var button = e.NameScope.Find<Button>(name);
        if (button is not null)
        {
            button.Click += handler;
        }
        return button;
    }

    private static void Unhook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        PseudoClasses.Set(CompactPseudoClass, e.NewSize.Width < 640);
    }

    internal void SetFlags(bool zoomed, bool single)
    {
        PseudoClasses.Set(ZoomedPseudoClass, zoomed);
        PseudoClasses.Set(SinglePseudoClass, single);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => _owner.RequestClose(Primitives.DismissReason.CloseButton);

    private void OnZoomIn(object? sender, RoutedEventArgs e) => Viewport?.ZoomBy(1.6);

    private void OnZoomOut(object? sender, RoutedEventArgs e) => Viewport?.ZoomBy(1 / 1.6);

    private void OnPrevious(object? sender, RoutedEventArgs e) => _owner.ShowPrevious();

    private void OnNext(object? sender, RoutedEventArgs e) => _owner.ShowNext();

    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && _owner.HandleLightboxKey(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LightboxGalleryViewAutomationPeer(this, _owner);

    private sealed class LightboxGalleryViewAutomationPeer(LightboxGalleryView view, LightboxGallery owner) : ControlAutomationPeer(view)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Window;

        protected override string? GetNameCore() => owner.Label;
    }
}
