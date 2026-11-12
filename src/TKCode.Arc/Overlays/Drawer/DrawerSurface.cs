using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace TKCode.Arc;

/// <summary>Edge a <see cref="Drawer"/> slides in from.</summary>
public enum DrawerSide
{
    Left,
    Top,
    Right,
    Bottom
}

/// <summary>
/// The panel of a <see cref="Drawer"/>: a header that doubles as the drag handle, a scrolling body and optional actions.
/// Left and right panels are min(30rem, viewport − 16px) wide and full height; top and bottom panels are full width
/// and at most min(32rem, viewport) tall. Below a 40rem viewport the panel nearly fills the width and tightens its
/// padding and radius (<c>:narrow</c>).
/// </summary>
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(DragAreaPart, typeof(Control))]
[PseudoClasses(NarrowPseudoClass, LeftPseudoClass, TopPseudoClass, RightPseudoClass, BottomPseudoClass,
    FullScreenPseudoClass, GrabberPseudoClass, CustomHeaderPseudoClass, NoHeaderPseudoClass)]
public class DrawerSurface : ModalSurface
{
    public const string HeaderPart = "PART_Header";
    public const string DragAreaPart = "PART_HeaderArea";
    public const string FullScreenPseudoClass = ":fullscreen";
    public const string GrabberPseudoClass = ":grabber";
    public const string CustomHeaderPseudoClass = ":custom-header";
    public const string NoHeaderPseudoClass = ":no-header";
    public const string NarrowPseudoClass = ":narrow";
    public const string LeftPseudoClass = ":left";
    public const string TopPseudoClass = ":top";
    public const string RightPseudoClass = ":right";
    public const string BottomPseudoClass = ":bottom";

    public static readonly StyledProperty<DrawerSide> SideProperty =
        AvaloniaProperty.Register<DrawerSurface, DrawerSide>(nameof(Side), DrawerSide.Right);

    /// <summary>Width of side panels (Arc: 30rem).</summary>
    public static readonly StyledProperty<double> PanelSizeProperty =
        AvaloniaProperty.Register<DrawerSurface, double>(nameof(PanelSize), 480);

    /// <summary>Maximum height of top and bottom panels (Arc: 32rem).</summary>
    public static readonly StyledProperty<double> PanelMaxHeightProperty =
        AvaloniaProperty.Register<DrawerSurface, double>(nameof(PanelMaxHeight), 512);

    /// <summary>Space a side panel leaves free in the viewport (Arc: 16px; 12px when narrow).</summary>
    public static readonly StyledProperty<double> ViewportGutterProperty =
        AvaloniaProperty.Register<DrawerSurface, double>(nameof(ViewportGutter), 16);

    public static readonly StyledProperty<double> NarrowViewportGutterProperty =
        AvaloniaProperty.Register<DrawerSurface, double>(nameof(NarrowViewportGutter), 12);

    /// <summary>Viewport width below which the panel is <c>:narrow</c> (Arc: 40rem).</summary>
    public static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<DrawerSurface, double>(nameof(NarrowBreakpoint), 640);

    /// <summary>
    /// Fills the viewport along the panel's axis (side panels: the whole width; top and bottom panels: the whole
    /// height) and drops the corner radius, the hairline and the gutter (<c>:fullscreen</c>).
    /// </summary>
    public static readonly StyledProperty<bool> IsFullScreenProperty =
        AvaloniaProperty.Register<DrawerSurface, bool>(nameof(IsFullScreen));

    /// <summary>False hides the whole header (title, description and close button); write your own at the top of the content.</summary>
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<DrawerSurface, bool>(nameof(ShowHeader), true);

    /// <summary>Replaces the default title row of the header; the header's padding and hairline are dropped.</summary>
    public static readonly StyledProperty<object?> HeaderContentProperty =
        AvaloniaProperty.Register<DrawerSurface, object?>(nameof(HeaderContent));

    /// <summary>Shows the grabber bar; null (default) shows it on bottom panels only.</summary>
    public static readonly StyledProperty<bool?> ShowGrabberProperty =
        AvaloniaProperty.Register<DrawerSurface, bool?>(nameof(ShowGrabber));

    /// <summary>Corner radius of the panel; null (default) follows the theme.</summary>
    public static readonly StyledProperty<CornerRadius?> PanelCornerRadiusProperty =
        AvaloniaProperty.Register<DrawerSurface, CornerRadius?>(nameof(PanelCornerRadius));

    /// <summary>Fixed height of top and bottom panels; null (default) sizes to the content, up to <see cref="PanelMaxHeight"/>.</summary>
    public static readonly StyledProperty<double?> PanelHeightProperty =
        AvaloniaProperty.Register<DrawerSurface, double?>(nameof(PanelHeight));

    public bool IsFullScreen { get => GetValue(IsFullScreenProperty); set => SetValue(IsFullScreenProperty, value); }
    public bool ShowHeader { get => GetValue(ShowHeaderProperty); set => SetValue(ShowHeaderProperty, value); }
    public object? HeaderContent { get => GetValue(HeaderContentProperty); set => SetValue(HeaderContentProperty, value); }
    public bool? ShowGrabber { get => GetValue(ShowGrabberProperty); set => SetValue(ShowGrabberProperty, value); }
    public CornerRadius? PanelCornerRadius { get => GetValue(PanelCornerRadiusProperty); set => SetValue(PanelCornerRadiusProperty, value); }
    public double? PanelHeight { get => GetValue(PanelHeightProperty); set => SetValue(PanelHeightProperty, value); }

    public DrawerSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public double PanelSize { get => GetValue(PanelSizeProperty); set => SetValue(PanelSizeProperty, value); }
    public double PanelMaxHeight { get => GetValue(PanelMaxHeightProperty); set => SetValue(PanelMaxHeightProperty, value); }
    public double ViewportGutter { get => GetValue(ViewportGutterProperty); set => SetValue(ViewportGutterProperty, value); }
    public double NarrowViewportGutter { get => GetValue(NarrowViewportGutterProperty); set => SetValue(NarrowViewportGutterProperty, value); }
    public double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    /// <summary>The header (drag handle) of the applied template.</summary>
    public Control? Header { get; private set; }

    /// <summary>The area at the top (header and grabber) the panel is dragged by; it stays while the header is hidden.</summary>
    public Control? DragArea { get; private set; }

    static DrawerSurface()
    {
        CloseButtonNameProperty.OverrideDefaultValue<DrawerSurface>("Close drawer");
        AffectsMeasure<DrawerSurface>(SideProperty, PanelSizeProperty, PanelMaxHeightProperty, ViewportGutterProperty,
            NarrowViewportGutterProperty, NarrowBreakpointProperty, IsFullScreenProperty, PanelHeightProperty);
    }

    public DrawerSurface()
    {
        UpdateSidePseudoClasses();
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Header = e.NameScope.Find<Control>(HeaderPart);
        DragArea = e.NameScope.Find<Control>(DragAreaPart) ?? Header;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SideProperty || change.Property == ShowGrabberProperty)
        {
            UpdateSidePseudoClasses();
        }
        else if (change.Property == IsFullScreenProperty)
        {
            PseudoClasses.Set(FullScreenPseudoClass, IsFullScreen);
        }
        else if (change.Property == ShowHeaderProperty)
        {
            PseudoClasses.Set(NoHeaderPseudoClass, !ShowHeader);
        }
        else if (change.Property == HeaderContentProperty)
        {
            PseudoClasses.Set(CustomHeaderPseudoClass, HeaderContent is not null);
        }
        else if (change.Property == PanelCornerRadiusProperty)
        {
            // A local value outranks the theme's per-side radius; clearing it hands the radius back to the theme.
            if (PanelCornerRadius is { } radius)
            {
                SetValue(CornerRadiusProperty, radius);
            }
            else
            {
                ClearValue(CornerRadiusProperty);
            }
        }
    }

    // The theme selects on :left / :right / :top / :bottom: a property selector (^[Side=Right]) costs an activator and a
    // boxed comparison per style, ten of them per surface.
    private void UpdateSidePseudoClasses()
    {
        var side = Side;
        PseudoClasses.Set(LeftPseudoClass, side == DrawerSide.Left);
        PseudoClasses.Set(TopPseudoClass, side == DrawerSide.Top);
        PseudoClasses.Set(RightPseudoClass, side == DrawerSide.Right);
        PseudoClasses.Set(BottomPseudoClass, side == DrawerSide.Bottom);
        PseudoClasses.Set(GrabberPseudoClass, ShowGrabber ?? side == DrawerSide.Bottom);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The available size is the viewport (the modal layer).
        var viewport = availableSize;
        var narrow = double.IsFinite(viewport.Width) && viewport.Width < NarrowBreakpoint;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (Side is DrawerSide.Left or DrawerSide.Right)
        {
            var gutter = narrow ? NarrowViewportGutter : ViewportGutter;
            var width = double.IsFinite(viewport.Width) ? Math.Max(0, Math.Min(PanelSize, viewport.Width - gutter)) : PanelSize;
            if (IsFullScreen && double.IsFinite(viewport.Width))
            {
                width = viewport.Width;
            }
            base.MeasureOverride(new Size(width, viewport.Height));
            return new Size(width, double.IsFinite(viewport.Height) ? viewport.Height : 0);
        }
        var maxHeight = double.IsFinite(viewport.Height) ? Math.Min(PanelMaxHeight, viewport.Height) : PanelMaxHeight;
        double? fixedHeight = null;
        if (IsFullScreen && double.IsFinite(viewport.Height))
        {
            fixedHeight = viewport.Height;
        }
        else if (PanelHeight is { } panelHeight)
        {
            fixedHeight = double.IsFinite(viewport.Height) ? Math.Min(panelHeight, viewport.Height) : panelHeight;
        }
        if (fixedHeight is { } height)
        {
            base.MeasureOverride(new Size(viewport.Width, height));
            return new Size(double.IsFinite(viewport.Width) ? viewport.Width : 0, height);
        }
        var desired = base.MeasureOverride(new Size(viewport.Width, maxHeight));
        return new Size(double.IsFinite(viewport.Width) ? viewport.Width : desired.Width, Math.Min(desired.Height, maxHeight));
    }
}
