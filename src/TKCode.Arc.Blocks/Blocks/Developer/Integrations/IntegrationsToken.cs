using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="Integrations"/>, measured from the Developer group's integrations directory (design reference
/// only, ADR 0027), and the row hover tint it derives from the neutrals. Colors that follow the accent or a status
/// (Success, Danger), type sizes and fonts come from ArcDesign tokens. Paddings are Avalonia thicknesses (left, top,
/// right, bottom). "Tight" applies at or below <see cref="TightWidth"/>, "compact" at or below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class IntegrationsToken : AbstractControlDesignToken
{
    /// <summary>Below this width the search moves under the title (720).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>At or below this width the side paddings tighten from 20 to 16 (520).</summary>
    public double TightWidth { get; set; }

    /// <summary>At or below this width the rows stack their text, the filters scroll and the settings button hides (480).</summary>
    public double CompactWidth { get; set; }

    /// <summary>The block's running text: 13 on an 18.2 line, letter spacing −0.13.</summary>
    public double CopySize { get; set; }

    public double CopyLineHeight { get; set; }

    public double CopyLetterSpacing { get; set; }

    /// <summary>The title: 16 on a 17.6 line, letter spacing −0.16; 2 between it and the subtitle.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleGap { get; set; }

    /// <summary>Header padding (20, 16, 20, 0), tight (16, 16, 16, 0) and compact (16, 14, 16, 0); 24 between title and search, 12 when stacked.</summary>
    public Thickness HeaderPadding { get; set; }

    public Thickness TightHeaderPadding { get; set; }

    public Thickness CompactHeaderPadding { get; set; }

    public double HeaderGap { get; set; }

    public double HeaderStackGap { get; set; }

    /// <summary>The search field: 260 wide beside the title.</summary>
    public double SearchWidth { get; set; }

    /// <summary>Filter row padding (8, 12, 8, 0), tight (6, 12, 6, 0) and compact (6, 8, 6, 4); 2 between pills.</summary>
    public Thickness FilterPadding { get; set; }

    public Thickness TightFilterPadding { get; set; }

    public Thickness CompactFilterPadding { get; set; }

    public double FilterGap { get; set; }

    /// <summary>A filter pill: 28 high, 12 at the sides, 6 between its text and its count.</summary>
    public double FilterHeight { get; set; }

    public Thickness FilterInlinePadding { get; set; }

    public double FilterInnerGap { get; set; }

    /// <summary>Space above the list (12).</summary>
    public double ListTopGap { get; set; }

    /// <summary>A row: at least 60 high, padding (20, 10); tight (16, 10); compact at least 72.9, padding (16, 12).</summary>
    public double RowMinHeight { get; set; }

    public double CompactRowMinHeight { get; set; }

    public Thickness RowPadding { get; set; }

    public Thickness TightRowPadding { get; set; }

    public Thickness CompactRowPadding { get; set; }

    /// <summary>The 20px mark column and the 12 between columns.</summary>
    public double MarkSize { get; set; }

    public double ColumnGap { get; set; }

    /// <summary>The app's name: 13 on a 14.3 line, letter spacing −0.39; 8 to its category; 1 between the two text lines.</summary>
    public double NameLineHeight { get; set; }

    public double NameLetterSpacing { get; set; }

    public double NameGap { get; set; }

    public double BodyGap { get; set; }

    /// <summary>The 12px lines (category, description, hints): a 16.8 line.</summary>
    public double SmallLineHeight { get; set; }

    /// <summary>4 between the settings button and the pill.</summary>
    public double ActionsGap { get; set; }

    /// <summary>The connect pill: 32 high, padding (10, 0, 12, 0), 6 between its icon and its text, a 16 icon slot.</summary>
    public double PillHeight { get; set; }

    public Thickness PillPadding { get; set; }

    public double PillGap { get; set; }

    public double PillIconSlot { get; set; }

    /// <summary>The pill's icons: plus 15 (stroke 1.75), check 16 (stroke 2.25), spinner 13.</summary>
    public double PlusSize { get; set; }

    public double PlusStroke { get; set; }

    public double CheckStroke { get; set; }

    public double SpinnerSize { get; set; }

    /// <summary>The row's hover tint: the muted surface mixed 55% into the surface.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The empty list: padding (16, 32, 16, 24), 4 between lines, 8 above the button.</summary>
    public Thickness EmptyPadding { get; set; }

    public double EmptyGap { get; set; }

    public double EmptyButtonGap { get; set; }

    /// <summary>The drawer: 24 between its sections, 12 inside the status block, 4 under the permissions heading, 12 above the list.</summary>
    public double SheetSectionGap { get; set; }

    public double SheetStatusGap { get; set; }

    public double SheetLeadGap { get; set; }

    public double SheetListGap { get; set; }

    /// <summary>A permission: padding (0, 12), 12 between icon and text, a 16 icon.</summary>
    public Thickness ScopePadding { get; set; }

    public double ScopeGap { get; set; }

    public double ScopeIconSize { get; set; }

    /// <summary>The settings shown once connected: padding (8, 24, 8, 8) inside a frame reaching 8 past the sides (focus rings).</summary>
    public Thickness SettingsPadding { get; set; }

    public Thickness SettingsMargin { get; set; }

    /// <summary>20 above the disconnect button inside the danger zone; 12 between it and its note.</summary>
    public double DangerTopGap { get; set; }

    public double DangerGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        NarrowWidth = 720;
        TightWidth = 520;
        CompactWidth = 480;
        CopySize = 13;
        CopyLineHeight = 18.2;
        CopyLetterSpacing = -0.13;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.16;
        SubtitleGap = 2;
        HeaderPadding = new Thickness(20, 16, 20, 0);
        TightHeaderPadding = new Thickness(16, 16, 16, 0);
        CompactHeaderPadding = new Thickness(16, 14, 16, 0);
        HeaderGap = 24;
        HeaderStackGap = 12;
        SearchWidth = 260;
        FilterPadding = new Thickness(8, 12, 8, 0);
        TightFilterPadding = new Thickness(6, 12, 6, 0);
        CompactFilterPadding = new Thickness(6, 8, 6, 4);
        FilterGap = 2;
        FilterHeight = 28;
        FilterInlinePadding = new Thickness(12, 0);
        FilterInnerGap = 6;
        ListTopGap = 12;
        RowMinHeight = 60;
        CompactRowMinHeight = 72.9;
        RowPadding = new Thickness(20, 10);
        TightRowPadding = new Thickness(16, 10);
        CompactRowPadding = new Thickness(16, 12);
        MarkSize = 20;
        ColumnGap = 12;
        NameLineHeight = 14.3;
        NameLetterSpacing = -0.39;
        NameGap = 8;
        BodyGap = 1;
        SmallLineHeight = 16.8;
        ActionsGap = 4;
        PillHeight = 32;
        PillPadding = new Thickness(10, 0, 12, 0);
        PillGap = 6;
        PillIconSlot = 16;
        PlusSize = 15;
        PlusStroke = 1.75;
        CheckStroke = 2.25;
        SpinnerSize = 13;
        RowHoverBackground = Oklch.MixOklab(n.Surface, n.SurfaceMuted, 0.55);
        EmptyPadding = new Thickness(16, 32, 16, 24);
        EmptyGap = 4;
        EmptyButtonGap = 8;
        SheetSectionGap = 24;
        SheetStatusGap = 12;
        SheetLeadGap = 4;
        SheetListGap = 12;
        ScopePadding = new Thickness(0, 12);
        ScopeGap = 12;
        ScopeIconSize = 16;
        SettingsPadding = new Thickness(8, 24, 8, 8);
        SettingsMargin = new Thickness(-8, 0);
        DangerTopGap = 20;
        DangerGap = 12;
    }
}
