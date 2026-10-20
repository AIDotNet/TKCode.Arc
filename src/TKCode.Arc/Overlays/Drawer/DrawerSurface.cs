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
[PseudoClasses(NarrowPseudoClass)]
public class DrawerSurface : ModalSurface
{
    public const string HeaderPart = "PART_Header";
    public const string NarrowPseudoClass = ":narrow";

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

    public DrawerSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }
    public double PanelSize { get => GetValue(PanelSizeProperty); set => SetValue(PanelSizeProperty, value); }
    public double PanelMaxHeight { get => GetValue(PanelMaxHeightProperty); set => SetValue(PanelMaxHeightProperty, value); }
    public double ViewportGutter { get => GetValue(ViewportGutterProperty); set => SetValue(ViewportGutterProperty, value); }
    public double NarrowViewportGutter { get => GetValue(NarrowViewportGutterProperty); set => SetValue(NarrowViewportGutterProperty, value); }
    public double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    /// <summary>The header (drag handle) of the applied template.</summary>
    public Control? Header { get; private set; }

    static DrawerSurface()
    {
        CloseButtonNameProperty.OverrideDefaultValue<DrawerSurface>("Close drawer");
        AffectsMeasure<DrawerSurface>(SideProperty, PanelSizeProperty, PanelMaxHeightProperty, ViewportGutterProperty,
            NarrowViewportGutterProperty, NarrowBreakpointProperty);
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Header = e.NameScope.Find<Control>(HeaderPart);
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
            base.MeasureOverride(new Size(width, viewport.Height));
            return new Size(width, double.IsFinite(viewport.Height) ? viewport.Height : 0);
        }
        var maxHeight = double.IsFinite(viewport.Height) ? Math.Min(PanelMaxHeight, viewport.Height) : PanelMaxHeight;
        var desired = base.MeasureOverride(new Size(viewport.Width, maxHeight));
        return new Size(double.IsFinite(viewport.Width) ? viewport.Width : desired.Width, Math.Min(desired.Height, maxHeight));
    }
}
