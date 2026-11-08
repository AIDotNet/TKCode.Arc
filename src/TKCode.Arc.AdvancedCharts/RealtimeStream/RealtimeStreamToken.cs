namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RealtimeStreamToken : AbstractControlDesignToken
{
    /// <summary>Smallest width of the value label column.</summary>
    public double Gutter { get; set; }

    /// <summary>Smallest width of the value label column on a plot narrower than 420 px.</summary>
    public double NarrowGutter { get; set; }

    /// <summary>Height of the time label row under the plot.</summary>
    public double TimeAxisHeight { get; set; }

    /// <summary>Least free space between two time labels; more labels are dropped to keep it.</summary>
    public double TimeLabelGap { get; set; }

    /// <summary>Stroke of the line.</summary>
    public double LineThickness { get; set; }

    /// <summary>Half the width of an anomaly marker.</summary>
    public double MarkerRadius { get; set; }

    /// <summary>Space between the header and the plot.</summary>
    public double HeaderGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        Gutter = 44;
        NarrowGutter = 32;
        TimeAxisHeight = 26;
        TimeLabelGap = 28;
        LineThickness = 2;
        MarkerRadius = 6;
        HeaderGap = 12;
    }
}
