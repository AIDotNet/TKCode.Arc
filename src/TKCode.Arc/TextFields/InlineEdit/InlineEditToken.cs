using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class InlineEditToken : AbstractControlDesignToken
{
    /// <summary>The box sits a border and padding outside the text, so the text lines up with the content around it.</summary>
    public Thickness RootMargin { get; set; }

    /// <summary>Room reserved for the actions in every state (66px; 38px multiline), so the text never rewraps.</summary>
    public Thickness LinePadding { get; set; }
    public Thickness MultilineLinePadding { get; set; }

    /// <summary>Title: 22px / 500 on a 28px line, padding 3 8. Body: 14px / 400 on a 21px line, padding 5 8.</summary>
    public Thickness TitlePadding { get; set; }
    public Thickness BodyPadding { get; set; }
    public double TitleLineHeight { get; set; }
    public double BodyLineHeight { get; set; }

    /// <summary>While editing a single line keeps extra room (min 5em, +16px) so the next character never waits for the box.</summary>
    public Thickness TitleEditingPadding { get; set; }
    public Thickness BodyEditingPadding { get; set; }
    public double TitleEditingMinWidth { get; set; }
    public double BodyEditingMinWidth { get; set; }

    /// <summary>Invalid halo (danger 24%) and the failed-save wash (danger 30% border, 9% fill).</summary>
    public Color InvalidRing { get; set; }
    public Color FlashBorder { get; set; }
    public Color FlashFill { get; set; }

    /// <summary>Save: foreground mixed 86% into the background on hover.</summary>
    public Color SaveHoverFill { get; set; }

    public double ActionSize { get; set; }
    public double SlotMinHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        RootMargin = new Thickness(-9, 0, 0, 0);
        LinePadding = new Thickness(0, 0, 66, 0);
        MultilineLinePadding = new Thickness(0, 0, 38, 0);
        TitlePadding = new Thickness(8, 3);
        BodyPadding = new Thickness(8, 5);
        TitleLineHeight = 28;
        BodyLineHeight = 21;
        TitleEditingPadding = new Thickness(8, 3, 24, 3);
        BodyEditingPadding = new Thickness(8, 5, 24, 5);
        TitleEditingMinWidth = 5 * 22;
        BodyEditingMinWidth = 5 * 14;
        InvalidRing = Oklch.WithAlpha(n.Danger, 0.24);
        FlashBorder = Oklch.WithAlpha(n.Danger, 0.30);
        FlashFill = Oklch.WithAlpha(n.Danger, 0.09);
        SaveHoverFill = Oklch.MixOklab(n.Foreground, n.Background, 0.14);
        ActionSize = 28;
        SlotMinHeight = 28;
    }
}
