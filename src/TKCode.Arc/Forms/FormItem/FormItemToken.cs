namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class FormItemToken : AbstractControlDesignToken
{
    /// <summary>Gap between the label column and the editor when the label is on the left (Space4).</summary>
    public double LabelColumnGap { get; set; }

    /// <summary>Gap between the label and its asterisk or "Optional" mark (Space1).</summary>
    public double RequiredMarkGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LabelColumnGap = 16;
        RequiredMarkGap = 4;
    }
}
