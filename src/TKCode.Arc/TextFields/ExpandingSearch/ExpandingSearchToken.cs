using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ExpandingSearchToken : AbstractControlDesignToken
{
    /// <summary>The glass sits at the leading edge, centred in the 44px circle: <c>left: calc(44px / 2 - 10px)</c>.</summary>
    public Thickness IconMargin { get; set; }
    public double GlassSize { get; set; }

    /// <summary>The field's text inset: <c>padding: 0 40px 0 38px</c>.</summary>
    public Thickness FieldPadding { get; set; }

    /// <summary>Clear button: 28px round, 7px from the trailing edge.</summary>
    public double ClearButtonSize { get; set; }
    public Thickness ClearButtonMargin { get; set; }
    public double ClearGlassSize { get; set; }

    /// <summary>Focus halo: 3px around the shell.</summary>
    public Thickness FocusRingMargin { get; set; }

    public double TextLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        IconMargin = new Thickness(12, 0, 0, 0);
        GlassSize = 18;
        FieldPadding = new Thickness(38, 0, 40, 0);
        ClearButtonSize = 28;
        ClearButtonMargin = new Thickness(0, 0, 7, 0);
        ClearGlassSize = 16;
        FocusRingMargin = new Thickness(-3);
        TextLineHeight = 14 * 1.4;
    }
}
