using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>Where a compass or edge button docks the dragged pane.</summary>
internal enum DockCompassSlot
{
    /// <summary>Join the group under the pointer as a tab.</summary>
    Center,

    /// <summary>Split the group: new group on its left.</summary>
    Left,

    /// <summary>Split the group: new group on its right.</summary>
    Right,

    /// <summary>Split the group: new group above it.</summary>
    Top,

    /// <summary>Split the group: new group below it.</summary>
    Bottom,

    /// <summary>Beside the whole document area, on its left.</summary>
    AreaLeft,

    /// <summary>Beside the whole document area, on its right.</summary>
    AreaRight,

    /// <summary>Beside the whole document area, above it.</summary>
    AreaTop,

    /// <summary>Beside the whole document area, below it.</summary>
    AreaBottom,

    /// <summary>Along the left edge of the whole layout.</summary>
    EdgeLeft,

    /// <summary>Along the right edge of the whole layout.</summary>
    EdgeRight,

    /// <summary>Along the top edge of the whole layout.</summary>
    EdgeTop,

    /// <summary>Along the bottom edge of the whole layout.</summary>
    EdgeBottom
}

/// <summary>
/// One button of the drag compass (spec 7): a 36px raised square with an 18px panel glyph that turns accent while the pointer
/// is over it (<c>:hot</c>; the pointer is captured by the drag, so this is set by the layer, not by <c>:pointerover</c>).
/// Disabled when the dragged pane may not go there. It takes no input: the drag hit-tests the compass geometrically.
/// </summary>
[PseudoClasses(HotPseudoClass)]
internal sealed class DockCompassButton : TemplatedControl
{
    public const string HotPseudoClass = ":hot";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<DockCompassButton, Geometry?>(nameof(Glyph));

    // Lucide panel-top, panel-bottom and app-window (not in the Core subset).
    private static readonly Geometry s_panelTop = ArcIcons.Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M3 9h18");
    private static readonly Geometry s_panelBottom = ArcIcons.Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M3 15h18");
    private static readonly Geometry s_appWindow = ArcIcons.Parse("M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1 -2 2h-16a2 2 0 0 1 -2 -2v-12a2 2 0 0 1 2 -2z M10 4v4 M2 8h20 M6 4v4");

    private readonly ScaleTransform _scale = new();

    static DockCompassButton()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<DockCompassButton>(false);
        FocusableProperty.OverrideDefaultValue<DockCompassButton>(false);
    }

    public DockCompassButton(DockCompassSlot slot)
    {
        Slot = slot;
        Glyph = GlyphOf(slot);
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
        AutomationProperties.SetName(this, DockText.Get(NameOf(slot)));
    }

    public DockCompassSlot Slot { get; }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <summary>Whether the pointer is over this button.</summary>
    public bool IsHot { get; private set; }

    public void SetHot(bool hot)
    {
        IsHot = hot;
        PseudoClasses.Set(HotPseudoClass, hot);
    }

    /// <summary>Reveal progress: opacity, and with <paramref name="scaled"/> a 0.92→1 scale (spec 8).</summary>
    public void SetReveal(double progress, bool scaled)
    {
        Opacity = Math.Clamp(progress, 0, 1);
        var scale = scaled ? 0.92 + 0.08 * Math.Clamp(progress, 0, 1) : 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
    }

    internal static bool IsGroupSlot(DockCompassSlot slot) => slot <= DockCompassSlot.Bottom;

    internal static bool IsAreaSlot(DockCompassSlot slot) => slot is >= DockCompassSlot.AreaLeft and <= DockCompassSlot.AreaBottom;

    internal static bool IsEdgeSlot(DockCompassSlot slot) => slot >= DockCompassSlot.EdgeLeft;

    /// <summary>The side a slot docks to (Center for the middle button).</summary>
    internal static DockPosition PositionOf(DockCompassSlot slot) => slot switch
    {
        DockCompassSlot.Left or DockCompassSlot.AreaLeft or DockCompassSlot.EdgeLeft => DockPosition.Left,
        DockCompassSlot.Right or DockCompassSlot.AreaRight or DockCompassSlot.EdgeRight => DockPosition.Right,
        DockCompassSlot.Top or DockCompassSlot.AreaTop or DockCompassSlot.EdgeTop => DockPosition.Top,
        DockCompassSlot.Bottom or DockCompassSlot.AreaBottom or DockCompassSlot.EdgeBottom => DockPosition.Bottom,
        _ => DockPosition.Center
    };

    private static Geometry GlyphOf(DockCompassSlot slot) => PositionOf(slot) switch
    {
        DockPosition.Left => ArcIcons.PanelLeft,
        DockPosition.Right => ArcIcons.PanelRight,
        DockPosition.Top => s_panelTop,
        DockPosition.Bottom => s_panelBottom,
        _ => s_appWindow
    };

    private static DockLangResourceKind NameOf(DockCompassSlot slot) => PositionOf(slot) switch
    {
        DockPosition.Left => DockLangResourceKind.IndicatorLeft,
        DockPosition.Right => DockLangResourceKind.IndicatorRight,
        DockPosition.Top => DockLangResourceKind.IndicatorTop,
        DockPosition.Bottom => DockLangResourceKind.IndicatorBottom,
        _ => DockLangResourceKind.IndicatorCenter
    };

    protected override AutomationPeer OnCreateAutomationPeer() => new DockCompassButtonAutomationPeer(this);

    private sealed class DockCompassButtonAutomationPeer : ControlAutomationPeer
    {
        public DockCompassButtonAutomationPeer(DockCompassButton owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
    }
}

/// <summary>
/// The card that trails the pointer during a dock drag (spec 7): it looks like the dragged tab, raised with the floating
/// shadow at 92% opacity; a group drag adds the number of other panes.
/// </summary>
internal sealed class DockDragGhost : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockDragGhost, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DockDragGhost, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> ExtraProperty =
        AvaloniaProperty.Register<DockDragGhost, string?>(nameof(Extra));

    static DockDragGhost()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<DockDragGhost>(false);
        FocusableProperty.OverrideDefaultValue<DockDragGhost>(false);
    }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>"+2" for a group drag with two more panes; null otherwise.</summary>
    public string? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }
}
