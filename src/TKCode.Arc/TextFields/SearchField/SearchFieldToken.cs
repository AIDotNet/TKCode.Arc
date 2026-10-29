
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SearchFieldToken : AbstractControlDesignToken
{
    /// <summary>Leading glass: 18px, Lucide default stroke.</summary>
    public double SearchIconSize { get; set; }
    public double IconStroke { get; set; }

    /// <summary>The clear button's reserved slot and the button itself (24px), with a 16px cross.</summary>
    public double ClearSlotSize { get; set; }
    public double ClearIconSize { get; set; }

    /// <summary>The clear button pops in from 0.8 (P26).</summary>
    public double ClearEnterScale { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SearchIconSize = 18;
        IconStroke = 2;
        ClearSlotSize = 24;
        ClearIconSize = 16;
        ClearEnterScale = 0.8;
    }
}
