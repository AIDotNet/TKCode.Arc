using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="KanbanBoard"/> (Arc Kanban board, design reference, measured at
/// 1440 and 390 wide): the toolbar, the view switch, cards, column heads, the composer and the details drawer. Colors,
/// radii of the large surfaces, shadows and type come from the ArcDesign tokens in the theme; the board grid (gaps, cell
/// padding, lane rows) lives in <c>KanbanLayout</c>, the drag physics in <c>KanbanDragPhysics</c>.
/// </summary>
[ControlDesignToken]
internal sealed class KanbanBoardToken : AbstractControlDesignToken
{
    /// <summary>The toolbar: 20/24/16 around (16/16/12 compact), title and count 12 apart, the switch 12 from them.</summary>
    public Thickness ToolbarPadding { get; set; }

    public Thickness CompactToolbarPadding { get; set; }

    public double ToolbarGap { get; set; }

    /// <summary>The board title: 22/24.2, letter spacing −0.66; the count 4 from its number.</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double CountGap { get; set; }

    /// <summary>The view switch: 3 inside a 1px border, options 28 high with 12 to the sides (the drawer's: 30 high).</summary>
    public Thickness SwitchInset { get; set; }

    public double SwitchOptionHeight { get; set; }

    public Thickness SwitchOptionPadding { get; set; }

    public double DrawerSwitchOptionHeight { get; set; }

    public double SwitchOptionGap { get; set; }

    /// <summary>A card: radius 16, 12 inside, rows 8 apart, meta row 26.8, 12/16.8 small text, 14/19.6 title.</summary>
    public CornerRadius CardRadius { get; set; }

    public Thickness CardInset { get; set; }

    public double CardRowGap { get; set; }

    public double CardMetaHeight { get; set; }

    public double SmallLineHeight { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>The keyboard focus ring: 2px, 2px outside the element (radius + 4).</summary>
    public Thickness FocusRingOutset { get; set; }

    public CornerRadius CardFocusRadius { get; set; }

    /// <summary>Avatars: 22 on cards, 24 on lane heads, 20 in the owner menu and trigger.</summary>
    public double CardAvatarDiameter { get; set; }

    public double LaneAvatarDiameter { get; set; }

    public double MenuAvatarDiameter { get; set; }

    /// <summary>A label's 6px dot, 5 from its name; meta glyphs 13 with 4 to their text, items 12 apart (4 between rows).</summary>
    public double LabelDotDiameter { get; set; }

    public double LabelDotGap { get; set; }

    public double MetaGlyphSize { get; set; }

    public double MetaGlyphGap { get; set; }

    public double MetaItemGap { get; set; }

    /// <summary>A cell's radius (22) and the slot's (16).</summary>
    public CornerRadius CellRadius { get; set; }

    public CornerRadius SlotRadius { get; set; }

    /// <summary>A column head: 12 left, 4 right, parts 8 apart; the "+" a 30 circle with a 15 glyph.</summary>
    public Thickness ColumnHeadPadding { get; set; }

    public double ColumnHeadGap { get; set; }

    public double HeadButtonDiameter { get; set; }

    public double HeadGlyphSize { get; set; }

    /// <summary>The over-limit chip: 1/8 inside, a pill.</summary>
    public Thickness ChipPadding { get; set; }

    /// <summary>The Add card row: radius 12, 8 to the sides, glyph 14, 6 to the text.</summary>
    public CornerRadius AddRowRadius { get; set; }

    public Thickness AddRowPadding { get; set; }

    public double AddRowGlyphSize { get; set; }

    public double AddRowGap { get; set; }

    /// <summary>The composer: a 2-line box (64 high, radius 16, 12 inside), 8 above 30 high buttons 12 to the sides.</summary>
    public double ComposerBoxHeight { get; set; }

    public CornerRadius ComposerBoxRadius { get; set; }

    public Thickness ComposerBoxPadding { get; set; }

    public double ComposerButtonHeight { get; set; }

    public Thickness ComposerButtonPadding { get; set; }

    public double ComposerButtonGap { get; set; }

    /// <summary>A lane head: 12 above, 12 to the left, avatar 8 from the name.</summary>
    public Thickness LaneHeadPadding { get; set; }

    public double LaneHeadGap { get; set; }

    /// <summary>The drawer: 400 wide at most, 8 from the edges, 20 inside, sections 16 apart.</summary>
    public double DrawerMaxWidth { get; set; }

    public Thickness DrawerInset { get; set; }

    public Thickness DrawerPadding { get; set; }

    public double DrawerGap { get; set; }

    /// <summary>The drawer's title: 22/30.8, letter spacing −0.66.</summary>
    public double DrawerTitleLineHeight { get; set; }

    /// <summary>The drawer's facts: label column 96, rows 36 / 34.</summary>
    public double FactLabelWidth { get; set; }

    public double FactRowHeight { get; set; }

    /// <summary>The owner trigger: 36 high, 12 to the sides, avatar 8 from the name, chevron 15.</summary>
    public double TriggerHeight { get; set; }

    public Thickness TriggerPadding { get; set; }

    /// <summary>The notes box: 3 lines (84.8), radius 16, 12 inside.</summary>
    public double NotesHeight { get; set; }

    public CornerRadius NotesRadius { get; set; }

    /// <summary>Checklist: the progress line 4 high, rows 33.6 with 7 above and below, boxes 18 (radius 6) 12 from the text.</summary>
    public double ProgressHeight { get; set; }

    public double StepRowHeight { get; set; }

    public double StepGap { get; set; }

    /// <summary>The add-step field: 34 high, radius 14, 12 to the sides.</summary>
    public double StepFieldHeight { get; set; }

    public CornerRadius StepFieldRadius { get; set; }

    /// <summary>The narrowest width at which the drawer's status options stay on one row (below: two by two).</summary>
    public double StatusStackWidth { get; set; }

    /// <summary>The narrowest column of a wide board, the fixed column of the compact board, and where compact starts.</summary>
    public double MinColumnWidth { get; set; }

    public double CompactColumnWidth { get; set; }

    public double CompactWidth { get; set; }

    /// <summary>Hover fill of icon buttons and the Add card row: Foreground at 5%.</summary>
    public Color HoverFill { get; set; }

    /// <summary>The scrim behind the drawer: Foreground at 10% (it lightens in dark mode, as measured).</summary>
    public Color ScrimFill { get; set; }

    /// <summary>The over-limit chip's fill: Warning at 12%.</summary>
    public Color ChipFill { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        ToolbarPadding = new Thickness(24, 20, 24, 16);
        CompactToolbarPadding = new Thickness(16, 16, 16, 12);
        ToolbarGap = 12;
        TitleLineHeight = 24.2;
        TitleLetterSpacing = -0.66;
        CountGap = 4;
        SwitchInset = new Thickness(3);
        SwitchOptionHeight = 28;
        SwitchOptionPadding = new Thickness(12, 0);
        DrawerSwitchOptionHeight = 30;
        SwitchOptionGap = 2;
        CardRadius = new CornerRadius(16);
        CardInset = new Thickness(12);
        CardRowGap = 8;
        CardMetaHeight = 26.8;
        SmallLineHeight = 16.8;
        BodyLineHeight = 19.6;
        FocusRingOutset = new Thickness(-4);
        CardFocusRadius = new CornerRadius(20);
        CardAvatarDiameter = 22;
        LaneAvatarDiameter = 24;
        MenuAvatarDiameter = 20;
        LabelDotDiameter = 6;
        LabelDotGap = 5;
        MetaGlyphSize = 13;
        MetaGlyphGap = 4;
        MetaItemGap = 12;
        CellRadius = new CornerRadius(22);
        SlotRadius = new CornerRadius(16);
        ColumnHeadPadding = new Thickness(12, 0, 4, 0);
        ColumnHeadGap = 8;
        HeadButtonDiameter = 30;
        HeadGlyphSize = 15;
        ChipPadding = new Thickness(8, 1);
        AddRowRadius = new CornerRadius(12);
        AddRowPadding = new Thickness(8, 0);
        AddRowGlyphSize = 14;
        AddRowGap = 6;
        ComposerBoxHeight = 65.2;
        ComposerBoxRadius = new CornerRadius(16);
        ComposerBoxPadding = new Thickness(12);
        ComposerButtonHeight = 30;
        ComposerButtonPadding = new Thickness(12, 0);
        ComposerButtonGap = 8;
        LaneHeadPadding = new Thickness(12, 12, 12, 0);
        LaneHeadGap = 8;
        DrawerMaxWidth = 400;
        DrawerInset = new Thickness(8);
        DrawerPadding = new Thickness(20);
        DrawerGap = 16;
        DrawerTitleLineHeight = 30.8;
        FactLabelWidth = 96;
        FactRowHeight = 34;
        TriggerHeight = 36;
        TriggerPadding = new Thickness(12, 0);
        NotesHeight = 84.8;
        NotesRadius = new CornerRadius(16);
        ProgressHeight = 4;
        StepRowHeight = 33.6;
        StepGap = 12;
        StepFieldHeight = 34;
        StepFieldRadius = new CornerRadius(14);
        StatusStackWidth = 440;
        MinColumnWidth = 251;
        CompactColumnWidth = 235;
        CompactWidth = 560;
        HoverFill = Oklch.WithAlpha(n.Foreground, 0.05);
        ScrimFill = Oklch.WithAlpha(n.Foreground, 0.10);
        ChipFill = Oklch.WithAlpha(n.Warning, 0.12);
    }
}
