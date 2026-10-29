using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FileDropzoneToken : AbstractControlDesignToken
{
    /// <summary>Trigger padding: 26 above and 34 below; a folded prompt 16 / 16; rows inside 26 / 26 (16 / 8 folded).</summary>
    public Thickness TriggerPadding { get; set; }

    public Thickness TriggerPaddingCompact { get; set; }

    public Thickness TriggerPaddingInside { get; set; }

    public Thickness TriggerPaddingCompactInside { get; set; }

    /// <summary>A failed row's edge: the danger color mixed 26% into the border.</summary>
    public Color FailedRowBorderColor { get; set; }

    /// <summary>The paper sheets' small double shadow.</summary>
    public BoxShadows SheetShadow { get; set; }

    public CornerRadius SheetRadius { get; set; }

    public double RowMinHeight { get; set; }

    public CornerRadius ThumbRadius { get; set; }

    public double RetryHeight { get; set; }

    public double RemoveSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        TriggerPadding = new Thickness(20, 26, 20, 34);
        TriggerPaddingCompact = new Thickness(20, 16, 20, 16);
        TriggerPaddingInside = new Thickness(20, 26, 20, 26);
        TriggerPaddingCompactInside = new Thickness(20, 16, 20, 8);
        FailedRowBorderColor = Oklch.MixOklab(neutrals.Border, neutrals.Danger, 0.26);
        SheetShadow = BoxShadows.Parse("0 1 2 0 #0D000000, 0 3 8 0 #0A000000");
        SheetRadius = new CornerRadius(7);
        RowMinHeight = 58;
        ThumbRadius = new CornerRadius(10);
        RetryHeight = 30;
        RemoveSize = 32;
    }
}
