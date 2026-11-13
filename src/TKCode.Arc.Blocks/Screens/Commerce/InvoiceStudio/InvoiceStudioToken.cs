using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the paper colors of <see cref="InvoiceStudio"/>'s printed preview.</summary>
[ControlDesignToken]
internal sealed class InvoiceStudioToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Width of the preview column beside the form.</summary>
    public double PreviewWidth { get; set; }

    /// <summary>Space around the preview beside the form (the gap is on its left).</summary>
    public Thickness PreviewMargin { get; set; }

    /// <summary>Block width below which the form and the preview take turns behind a switch.</summary>
    public double CompactThreshold { get; set; }

    /// <summary>Space inside the paper.</summary>
    public Thickness PaperPadding { get; set; }

    /// <summary>The paper of the preview: near white in both themes, like a printout.</summary>
    public Color PaperColor { get; set; }

    /// <summary>Text on the paper.</summary>
    public Color InkColor { get; set; }

    /// <summary>Secondary text on the paper.</summary>
    public Color InkMutedColor { get; set; }

    /// <summary>Rules on the paper.</summary>
    public Color RuleColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(24);
        PreviewWidth = 380;
        PreviewMargin = new Thickness(28, 0, 0, 0);
        CompactThreshold = 900;
        PaperPadding = new Thickness(28, 30);
        PaperColor = Color.Parse("#FDFCFA");
        InkColor = Color.Parse("#1D1D1F");
        InkMutedColor = Color.Parse("#6B6B70");
        RuleColor = Color.Parse("#E4E2DD");
    }
}
