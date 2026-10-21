using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SecuritySettings"/>: card rhythm, the summary tile and meter, and the password form.</summary>
[ControlDesignToken]
internal sealed class SecuritySettingsToken : AbstractControlDesignToken
{
    /// <summary>Gap between the cards.</summary>
    public double GroupGap { get; set; }

    /// <summary>Width below which the meter moves under the summary text and "Sign out other sessions" under the list.</summary>
    public double CompactThreshold { get; set; }

    /// <summary>
    /// Stack width of rows whose control is small (switches, a session's badge or "Sign out"): they keep the control beside
    /// the text down to this width instead of the usual 440.
    /// </summary>
    public double InlineRowStackWidth { get; set; }

    /// <summary>Space above and below the summary (the card adds 4px of its own).</summary>
    public Thickness SummaryInset { get; set; }

    /// <summary>Side of the shield tile.</summary>
    public double ShieldTile { get; set; }

    /// <summary>Side of the shield glyph.</summary>
    public double ShieldGlyph { get; set; }

    /// <summary>Corner radius of the shield tile.</summary>
    public CornerRadius ShieldTileRadius { get; set; }

    /// <summary>Gap after the shield tile.</summary>
    public Thickness ShieldTileMargin { get; set; }

    /// <summary>Gap between the summary title and its count.</summary>
    public double SummaryTextGap { get; set; }

    /// <summary>One meter segment.</summary>
    public double MeterSegmentWidth { get; set; }

    /// <summary>Thickness of the meter.</summary>
    public double MeterSegmentHeight { get; set; }

    /// <summary>Corner radius of a meter segment.</summary>
    public CornerRadius MeterSegmentRadius { get; set; }

    /// <summary>Gap between meter segments.</summary>
    public double MeterGap { get; set; }

    /// <summary>Gap before the meter beside the text.</summary>
    public Thickness MeterMargin { get; set; }

    /// <summary>Gap above the meter under the text (compact).</summary>
    public Thickness CompactMeterMargin { get; set; }

    /// <summary>Space around the password form beside the row's icon tile (aligned with the row's text).</summary>
    public Thickness FormInset { get; set; }

    /// <summary>Space around the password form once rows stack.</summary>
    public Thickness StackedFormInset { get; set; }

    /// <summary>Widest the password fields grow.</summary>
    public double FormMaxWidth { get; set; }

    /// <summary>Gap between the fields and the buttons of the password form.</summary>
    public double FormGap { get; set; }

    /// <summary>Gap between the form's buttons.</summary>
    public double FormActionsGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        GroupGap = 16;
        CompactThreshold = 560;
        InlineRowStackWidth = 280;
        SummaryInset = new Thickness(0, 16);
        ShieldTile = 44;
        ShieldGlyph = 22;
        ShieldTileRadius = new CornerRadius(12);
        ShieldTileMargin = new Thickness(0, 0, 16, 0);
        SummaryTextGap = 2;
        MeterSegmentWidth = 28;
        MeterSegmentHeight = 6;
        MeterSegmentRadius = new CornerRadius(3);
        MeterGap = 4;
        MeterMargin = new Thickness(16, 0, 0, 0);
        CompactMeterMargin = new Thickness(0, 14, 0, 0);
        // The row's icon tile (34) plus its gap (16): the form lines up with the row's text.
        FormInset = new Thickness(50, 4, 0, 20);
        StackedFormInset = new Thickness(0, 4, 0, 20);
        FormMaxWidth = 400;
        FormGap = 16;
        FormActionsGap = 8;
    }
}
