using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The panel of a <see cref="BottomSheet"/>: grabber, header (the drag handle), scrolling body. It is as tall as the
/// largest detent plus a hidden extension below the window edge (so an upward stretch never shows a gap); detents are
/// translations of that panel. Width min(100%, 36rem), centred.
/// </summary>
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(GrabberPart, typeof(AvaloniaButton))]
[TemplatePart(BodyPart, typeof(ScrollViewer))]
[TemplatePart(AnnouncerPart, typeof(TextBlock))]
[PseudoClasses(ExpandedPseudoClass, DraggingPseudoClass, ScrolledPseudoClass, NarrowPseudoClass)]
public class BottomSheetSurface : ModalSurface
{
    public const string HeaderPart = "PART_Header";
    public const string GrabberPart = "PART_Grabber";
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";
    public const string ExpandedPseudoClass = ":expanded";
    public const string DraggingPseudoClass = ":dragging";
    public const string ScrolledPseudoClass = ":scrolled";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Largest detent as a fraction of the viewport height (sets the panel height).</summary>
    public static readonly StyledProperty<double> MaxDetentProperty =
        AvaloniaProperty.Register<BottomSheetSurface, double>(nameof(MaxDetent), 0.92);

    /// <summary>Hidden surface below the window edge (Arc: 160px).</summary>
    public static readonly StyledProperty<double> ExtensionProperty =
        AvaloniaProperty.Register<BottomSheetSurface, double>(nameof(Extension), 160);

    /// <summary>Maximum panel width (Arc: 36rem).</summary>
    public static readonly StyledProperty<double> PanelMaxWidthProperty =
        AvaloniaProperty.Register<BottomSheetSurface, double>(nameof(PanelMaxWidth), 576);

    /// <summary>Viewport width below which the paddings tighten (Arc: 40rem).</summary>
    public static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<BottomSheetSurface, double>(nameof(NarrowBreakpoint), 640);

    public double MaxDetent { get => GetValue(MaxDetentProperty); set => SetValue(MaxDetentProperty, value); }
    public double Extension { get => GetValue(ExtensionProperty); set => SetValue(ExtensionProperty, value); }
    public double PanelMaxWidth { get => GetValue(PanelMaxWidthProperty); set => SetValue(PanelMaxWidthProperty, value); }
    public double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    public Control? Header { get; private set; }
    public AvaloniaButton? Grabber { get; private set; }
    public ScrollViewer? Body { get; private set; }
    internal TextBlock? Announcer { get; private set; }

    /// <summary>Raised after the template parts are available.</summary>
    internal event EventHandler? PartsReady;

    static BottomSheetSurface()
    {
        CloseButtonNameProperty.OverrideDefaultValue<BottomSheetSurface>("Close");
        FocusableProperty.OverrideDefaultValue<BottomSheetSurface>(true);
        AffectsMeasure<BottomSheetSurface>(MaxDetentProperty, ExtensionProperty, PanelMaxWidthProperty, NarrowBreakpointProperty);
    }

    internal void SetExpanded(bool expanded)
    {
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        if (Grabber is { } grabber)
        {
            AutomationProperties.SetName(grabber, expanded ? "Collapse sheet" : "Expand sheet");
        }
    }

    internal void SetDragging(bool dragging) => PseudoClasses.Set(DraggingPseudoClass, dragging);

    internal void SetScrolled(bool scrolled) => PseudoClasses.Set(ScrolledPseudoClass, scrolled);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Header = e.NameScope.Find<Control>(HeaderPart);
        Grabber = e.NameScope.Find<AvaloniaButton>(GrabberPart);
        Body = e.NameScope.Find<ScrollViewer>(BodyPart);
        Announcer = e.NameScope.Find<TextBlock>(AnnouncerPart);
        SetExpanded(PseudoClasses.Contains(ExpandedPseudoClass));
        PartsReady?.Invoke(this, EventArgs.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The bottom margin is −Extension, so the available height is the viewport plus the extension.
        var extension = Extension;
        var viewportHeight = double.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height - extension) : 0;
        var width = double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, PanelMaxWidth) : PanelMaxWidth;
        PseudoClasses.Set(NarrowPseudoClass, double.IsFinite(availableSize.Width) && availableSize.Width < NarrowBreakpoint);
        var height = Math.Clamp(MaxDetent, 0, 1) * viewportHeight + extension;
        base.MeasureOverride(new Size(width, height));
        return new Size(width, height);
    }
}
