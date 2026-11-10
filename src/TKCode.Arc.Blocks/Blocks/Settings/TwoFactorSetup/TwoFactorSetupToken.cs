using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="TwoFactorSetup"/> and the fixed ink and paper of its QR code.</summary>
[ControlDesignToken]
internal sealed class TwoFactorSetupToken : AbstractControlDesignToken
{
    /// <summary>Space inside the card.</summary>
    public Thickness FrameInset { get; set; }

    /// <summary>Space inside the card below <see cref="NarrowWidth"/>.</summary>
    public Thickness FrameInsetNarrow { get; set; }

    /// <summary>The card's default maximum width.</summary>
    public double PreferredWidth { get; set; }

    /// <summary>Below this width the QR code stacks over its instructions and the buttons fill the row.</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Side of the shield tile beside the heading.</summary>
    public double HeaderTile { get; set; }

    /// <summary>Space between the heading and the progress.</summary>
    public Thickness StepperMargin { get; set; }

    /// <summary>Line height of step titles.</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Line height of body text.</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Space between a step title and its description.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Space between a step's description and its content.</summary>
    public Thickness BodyMargin { get; set; }

    /// <summary>Space above the buttons.</summary>
    public Thickness ActionsMargin { get; set; }

    /// <summary>Side of the QR tile (paper included).</summary>
    public double QrSize { get; set; }

    /// <summary>The QR modules: near black in both themes, because scanners expect dark on light.</summary>
    public Color QrInk { get; set; }

    /// <summary>The QR paper: near white in both themes.</summary>
    public Color QrPaper { get; set; }

    /// <summary>Gap between the QR tile and the instructions.</summary>
    public double QrGap { get; set; }

    /// <summary>Gap between the numbered instructions.</summary>
    public double InstructionGap { get; set; }

    /// <summary>Side of an instruction's number disc.</summary>
    public double InstructionDisc { get; set; }

    /// <summary>Space inside the box that holds the key or the backup codes.</summary>
    public Thickness WellInset { get; set; }

    /// <summary>Gap between the columns of backup codes.</summary>
    public double CodeColumnGap { get; set; }

    /// <summary>Gap between the rows of backup codes.</summary>
    public double CodeRowGap { get; set; }

    /// <summary>Diameter of the success mark on the last step.</summary>
    public double ResultMark { get; set; }

    /// <summary>Size of the drawn check inside the success mark.</summary>
    public double ResultGlyph { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var paper = ArcPalette.Neutrals(false);
        FrameInset = new Thickness(28);
        FrameInsetNarrow = new Thickness(20, 22);
        PreferredWidth = 560;
        NarrowWidth = 460;
        HeaderTile = 40;
        StepperMargin = new Thickness(0, 22, 0, 24);
        TitleLineHeight = 26;
        BodyLineHeight = 20;
        DescriptionMargin = new Thickness(0, 6, 0, 0);
        BodyMargin = new Thickness(0, 18, 0, 0);
        ActionsMargin = new Thickness(0, 26, 0, 0);
        QrSize = 176;
        QrInk = paper.Foreground;
        QrPaper = Colors.White;
        QrGap = 24;
        InstructionGap = 10;
        InstructionDisc = 20;
        WellInset = new Thickness(16, 14);
        CodeColumnGap = 24;
        CodeRowGap = 8;
        ResultMark = 56;
        ResultGlyph = 28;
    }
}
