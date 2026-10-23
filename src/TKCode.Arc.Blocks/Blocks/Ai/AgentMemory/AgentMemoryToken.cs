using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and timing of <see cref="AgentMemory"/>, measured from the memory card of the AI group (design reference
/// only, ADR 0015), and the accent-independent tints it derives from the neutrals. Colors that follow the accent, radii
/// and fonts come from ArcDesign tokens in the theme. Paddings are written as Avalonia thicknesses (left, top, right,
/// bottom); the compact ones apply below <see cref="CompactWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class AgentMemoryToken : AbstractControlDesignToken
{
    /// <summary>The card is at most this wide (640) and centered.</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>Below this width of the control (its container) the card tightens its padding and the filters scroll (440).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Header padding (20 20 8).</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>Header padding in a narrow card (16 16 8).</summary>
    public Thickness CompactHeaderPadding { get; set; }

    /// <summary>Padding of the switches block (20 at the sides, 6 below).</summary>
    public Thickness SettingsPadding { get; set; }

    /// <summary>Padding of the switches block in a narrow card (16 at the sides, 6 below).</summary>
    public Thickness CompactSettingsPadding { get; set; }

    /// <summary>Height of the toolbar with the filters and the search (52).</summary>
    public double ToolbarHeight { get; set; }

    /// <summary>The filters' room in the toolbar: 8 at the start, 60 at the end for the closed search button.</summary>
    public Thickness ToolbarPadding { get; set; }

    /// <summary>The filters' room in a narrow toolbar (4, 56).</summary>
    public Thickness CompactToolbarPadding { get; set; }

    /// <summary>Padding of the captured pill's row (16 at the start, 20 at the end, 10 below).</summary>
    public Thickness PillRowPadding { get; set; }

    /// <summary>Padding of the captured pill's row in a narrow card (12, 16, 10).</summary>
    public Thickness CompactPillRowPadding { get; set; }

    /// <summary>Padding of the paused notice that replaces the pill (20 at the sides, 12 below).</summary>
    public Thickness PausedRowPadding { get; set; }

    /// <summary>Padding of the paused notice in a narrow card (16 at the sides, 12 below).</summary>
    public Thickness CompactPausedRowPadding { get; set; }

    /// <summary>Space around the list's rows: 8 at the sides, 10 below.</summary>
    public Thickness ListPadding { get; set; }

    /// <summary>Space around the list's rows in a narrow card: 4 at the sides, 10 below.</summary>
    public Thickness CompactListPadding { get; set; }

    /// <summary>The list fades out over this height at its top edge while scrolled (20).</summary>
    public double ListFadeTop { get; set; }

    /// <summary>The list fades out over this height at its bottom edge while more follows (36).</summary>
    public double ListFadeBottom { get; set; }

    /// <summary>Height of the add field (56).</summary>
    public double AddFieldHeight { get; set; }

    /// <summary>Padding of the add field: 20 at the start, 8 above and below, 12 at the end.</summary>
    public Thickness AddFieldPadding { get; set; }

    /// <summary>Padding of the add field in a narrow card (16, 8, 8, 8).</summary>
    public Thickness CompactAddFieldPadding { get; set; }

    /// <summary>The tint behind a hovered or focused row: the muted surface at 70%.</summary>
    public Color RowHoverBackground { get; set; }

    /// <summary>The tint behind a hovered Edit or Forget button: the foreground at 7%.</summary>
    public Color ActionHoverBackground { get; set; }

    /// <summary>The tint of the add field while it has the focus: the muted surface at 55%.</summary>
    public Color AddFocusBackground { get; set; }

    /// <summary>The search box takes this long to open and close (240ms).</summary>
    public TimeSpan SearchDuration { get; set; }

    /// <summary>How long the captured pill stays on "Saved to" before it folds away (1.8s).</summary>
    public TimeSpan CapturedHold { get; set; }

    /// <summary>How long the add field shows "Added to" before it clears (2s).</summary>
    public TimeSpan AddedHold { get; set; }

    /// <summary>How long a filed or added row's landing tint lasts (1.6s).</summary>
    public TimeSpan ArrivalDuration { get; set; }

    /// <summary>How long a save takes when no commit hook is set (380ms).</summary>
    public TimeSpan SimulatedSave { get; set; }

    /// <summary>How long the built-in keyword guess shows "Sorting into a group" (650ms, like Arc's preview).</summary>
    public TimeSpan SimulatedSort { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        CardMaxWidth = 640;
        CompactWidth = 440;
        HeaderPadding = new Thickness(20, 20, 20, 8);
        CompactHeaderPadding = new Thickness(16, 16, 16, 8);
        SettingsPadding = new Thickness(20, 0, 20, 6);
        CompactSettingsPadding = new Thickness(16, 0, 16, 6);
        ToolbarHeight = 52;
        ToolbarPadding = new Thickness(8, 0, 60, 0);
        CompactToolbarPadding = new Thickness(4, 0, 56, 0);
        PillRowPadding = new Thickness(16, 0, 20, 10);
        CompactPillRowPadding = new Thickness(12, 0, 16, 10);
        PausedRowPadding = new Thickness(20, 0, 20, 12);
        CompactPausedRowPadding = new Thickness(16, 0, 16, 12);
        ListPadding = new Thickness(8, 0, 8, 10);
        CompactListPadding = new Thickness(4, 0, 4, 10);
        ListFadeTop = 20;
        ListFadeBottom = 36;
        AddFieldHeight = 56;
        AddFieldPadding = new Thickness(20, 8, 12, 8);
        CompactAddFieldPadding = new Thickness(16, 8, 8, 8);
        RowHoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.7);
        ActionHoverBackground = Oklch.WithAlpha(n.Foreground, 0.07);
        AddFocusBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.55);
        SearchDuration = TimeSpan.FromMilliseconds(240);
        CapturedHold = TimeSpan.FromMilliseconds(1800);
        AddedHold = TimeSpan.FromMilliseconds(2000);
        ArrivalDuration = TimeSpan.FromMilliseconds(1600);
        SimulatedSave = TimeSpan.FromMilliseconds(380);
        SimulatedSort = TimeSpan.FromMilliseconds(650);
    }
}
