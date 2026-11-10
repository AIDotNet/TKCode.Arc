namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class BackToTopToken : AbstractControlDesignToken
{
    /// <summary>Arrow size of the small button (36px circle).</summary>
    public double IconSizeSM { get; set; }

    /// <summary>Arrow size of the medium button (44px circle).</summary>
    public double IconSizeMD { get; set; }

    /// <summary>Arrow size of the large button (50px circle).</summary>
    public double IconSizeLG { get; set; }

    /// <summary>Scale of the hidden button; it grows to 1 as it appears (full motion only).</summary>
    public double HiddenScale { get; set; }

    /// <summary>How far below its place the hidden button sits; it rises by this much as it appears (full motion only).</summary>
    public double HiddenOffset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        IconSizeSM = 16;
        IconSizeMD = 18;
        IconSizeLG = 20;
        HiddenScale = 0.86;
        HiddenOffset = 8;
    }
}
