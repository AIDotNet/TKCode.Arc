namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FormToken : AbstractControlDesignToken
{
    /// <summary>Space kept above the first invalid item when a failed submit scrolls to it, so its label stays in view (Space6).</summary>
    public double ErrorScrollMargin { get; set; }

    /// <summary>Upper limit of an automatic left label column; longer labels wrap.</summary>
    public double AutoLabelMaxWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ErrorScrollMargin = 24;
        AutoLabelMaxWidth = 240;
    }
}
