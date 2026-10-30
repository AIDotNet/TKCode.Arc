using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="TeamSwitcher"/>: the root's width cap, the trigger (height, padding, tile and type), the menu
/// panel (inset, radius, gap to the trigger, the search row, the section heading, six 48px rows, the action rows) and the
/// row parts (tile, role, shortcut keycap, check). Colors, fonts and the focus ring come from the ArcDesign tokens in the
/// theme.
/// </summary>
[ControlDesignToken]
internal sealed class TeamSwitcherToken : AbstractControlDesignToken
{
    /// <summary>The root is up to this wide and fills narrower containers.</summary>
    public double MaxWidth { get; set; }

    /// <summary>The trigger: its height, padding, corner radius, the space between its parts and its tile.</summary>
    public double TriggerHeight { get; set; }

    public Thickness TriggerPadding { get; set; }

    public CornerRadius TriggerCornerRadius { get; set; }

    /// <summary>The keyboard focus ring, 4px outside the trigger.</summary>
    public CornerRadius TriggerFocusRingCornerRadius { get; set; }

    public double TriggerGap { get; set; }

    public double TriggerTileSize { get; set; }

    /// <summary>The trigger's chevron and spinner.</summary>
    public double TriggerGlyphSize { get; set; }

    /// <summary>A workspace's name and the plan line under it (trigger and rows).</summary>
    public double NameLineHeight { get; set; }

    public double PlanFontSize { get; set; }

    public double PlanLineHeight { get; set; }

    /// <summary>The menu panel: the inset around its content, its corner radius and the gap under the trigger.</summary>
    public Thickness MenuPadding { get; set; }

    public CornerRadius MenuCornerRadius { get; set; }

    public double MenuGap { get; set; }

    /// <summary>
    /// The menu is as wide as the trigger but never narrower than this: in a narrow sidebar it reaches past the sidebar's
    /// edge so names stay readable next to the role and the shortcut.
    /// </summary>
    public double MenuMinWidth { get; set; }

    /// <summary>The search row at the top of the menu.</summary>
    public Thickness SearchMargin { get; set; }

    /// <summary>The "Workspaces" heading over the list.</summary>
    public double SectionHeight { get; set; }

    public Thickness SectionPadding { get; set; }

    /// <summary>A workspace row: 48px for touch, its padding, tile, the gap between its parts and the highlight's radius.</summary>
    public double RowHeight { get; set; }

    public Thickness RowPadding { get; set; }

    public double RowTileSize { get; set; }

    public double RowGap { get; set; }

    public CornerRadius HighlightCornerRadius { get; set; }

    /// <summary>How many rows the list shows before it scrolls (the panel's height is fixed from it).</summary>
    public int VisibleRows { get; set; }

    /// <summary>The Create workspace and Join with invite rows, and the hairline above them.</summary>
    public double ActionHeight { get; set; }

    public double ActionGlyphSize { get; set; }

    public Thickness SeparatorMargin { get; set; }

    /// <summary>The role at the end of a row.</summary>
    public double RoleFontSize { get; set; }

    /// <summary>The shortcut keycap: height, padding, type and radius.</summary>
    public double KeycapHeight { get; set; }

    public Thickness KeycapPadding { get; set; }

    public double KeycapFontSize { get; set; }

    public CornerRadius KeycapCornerRadius { get; set; }

    /// <summary>The check that marks the active workspace.</summary>
    public double CheckSize { get; set; }

    /// <summary>The create and join views: their padding, the header's height and the error line under the field.</summary>
    public Thickness ViewPadding { get; set; }

    public double ViewHeaderHeight { get; set; }

    public Thickness FormErrorMargin { get; set; }

    /// <summary>How far the views shift sideways as they crossfade (follows the navigation direction).</summary>
    public double ViewShift { get; set; }

    /// <summary>The tint behind a lettered tile: its series color at this opacity.</summary>
    public double TileTintOpacity { get; set; }

    /// <summary>
    /// The letter or mark on an app-supplied brand color without its own foreground: the light or the dark ink, whichever
    /// contrasts more with that color. A brand color does not follow the theme, so neither does its ink.
    /// </summary>
    public Color TileInkLight { get; set; }

    public Color TileInkDark { get; set; }

    /// <summary>The trigger's hover fill and the list highlight: SurfaceMuted at these opacities.</summary>
    public double HoverOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        MaxWidth = 360;
        TriggerHeight = 52;
        TriggerPadding = new Thickness(8, 0, 12, 0);
        TriggerCornerRadius = new CornerRadius(14);
        TriggerFocusRingCornerRadius = new CornerRadius(18);
        TriggerGap = 10;
        TriggerTileSize = 32;
        TriggerGlyphSize = 16;
        NameLineHeight = 18;
        PlanFontSize = 12;
        PlanLineHeight = 16;
        MenuPadding = new Thickness(6);
        MenuCornerRadius = new CornerRadius(18);
        MenuGap = 6;
        MenuMinWidth = 288;
        SearchMargin = new Thickness(0, 0, 0, 4);
        SectionHeight = 26;
        SectionPadding = new Thickness(10, 0, 10, 0);
        RowHeight = 48;
        RowPadding = new Thickness(10, 0, 10, 0);
        RowTileSize = 28;
        RowGap = 10;
        HighlightCornerRadius = new CornerRadius(12);
        VisibleRows = 6;
        ActionHeight = 40;
        ActionGlyphSize = 16;
        SeparatorMargin = new Thickness(4, 4, 4, 4);
        RoleFontSize = 12;
        KeycapHeight = 20;
        KeycapPadding = new Thickness(5, 0);
        KeycapFontSize = 11;
        KeycapCornerRadius = new CornerRadius(6);
        CheckSize = 16;
        ViewPadding = new Thickness(6, 0, 6, 6);
        ViewHeaderHeight = 44;
        FormErrorMargin = new Thickness(6, 8, 6, 0);
        ViewShift = 6;
        TileTintOpacity = 0.16;
        TileInkLight = Colors.White;
        TileInkDark = Color.FromRgb(0x18, 0x18, 0x1B);
        HoverOpacity = 1;
    }
}
