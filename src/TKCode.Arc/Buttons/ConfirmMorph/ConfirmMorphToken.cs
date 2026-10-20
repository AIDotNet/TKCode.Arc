using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ConfirmMorphToken : AbstractControlDesignToken
{
    /// <summary>Height of the buttons inside a face (pill height − 8).</summary>
    public double InnerButtonExtent { get; set; }

    /// <summary>Resting trigger padding (Arc <c>0 16px 0 14px</c>).</summary>
    public Thickness TriggerInset { get; set; }

    /// <summary>Cancel / Confirm / Undo / Retry padding.</summary>
    public Thickness AnswerInset { get; set; }

    /// <summary>Face padding (0 4px) and gap between its parts (2px).</summary>
    public Thickness FaceInset { get; set; }
    public double FaceGap { get; set; }

    /// <summary>Question padding (0 8px 0 12px) and its maximum width (16rem).</summary>
    public Thickness PromptInset { get; set; }
    public double PromptMaxWidth { get; set; }

    /// <summary>Status padding per face: result, pending, result without undo, countdown.</summary>
    public Thickness StatusInset { get; set; }
    public Thickness PendingStatusInset { get; set; }
    public Thickness SoloStatusInset { get; set; }
    public Thickness CountingStatusInset { get; set; }
    public double StatusGap { get; set; }

    /// <summary>Trigger fills: foreground at 5% hover, 9% pressed, 6% keyboard focus.</summary>
    public Color TriggerHoverColor { get; set; }
    public Color TriggerPressColor { get; set; }
    public Color TriggerFocusColor { get; set; }

    /// <summary>Secondary answer fills: foreground at 7% hover, 11% pressed, 8% keyboard focus.</summary>
    public Color AnswerHoverColor { get; set; }
    public Color AnswerPressColor { get; set; }
    public Color AnswerFocusColor { get; set; }

    /// <summary>Danger confirm fills: danger at 13% rest, 18% hover, 22% pressed, 20% keyboard focus.</summary>
    public Color ConfirmRestColor { get; set; }
    public Color ConfirmHoverColor { get; set; }
    public Color ConfirmPressColor { get; set; }
    public Color ConfirmFocusColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        InnerButtonExtent = 28;
        TriggerInset = new Thickness(14, 0, 16, 0);
        AnswerInset = new Thickness(12, 0);
        FaceInset = new Thickness(4, 0);
        FaceGap = 2;
        PromptInset = new Thickness(12, 0, 8, 0);
        PromptMaxWidth = 256;
        StatusInset = new Thickness(12, 0, 10, 0);
        PendingStatusInset = new Thickness(12, 0, 16, 0);
        SoloStatusInset = new Thickness(12, 0, 14, 0);
        CountingStatusInset = new Thickness(12, 0, 6, 0);
        StatusGap = 8;
        TriggerHoverColor = Oklch.WithAlpha(n.Foreground, 0.05);
        TriggerPressColor = Oklch.WithAlpha(n.Foreground, 0.09);
        TriggerFocusColor = Oklch.WithAlpha(n.Foreground, 0.06);
        AnswerHoverColor = Oklch.WithAlpha(n.Foreground, 0.07);
        AnswerPressColor = Oklch.WithAlpha(n.Foreground, 0.11);
        AnswerFocusColor = Oklch.WithAlpha(n.Foreground, 0.08);
        ConfirmRestColor = Oklch.WithAlpha(n.Danger, 0.13);
        ConfirmHoverColor = Oklch.WithAlpha(n.Danger, 0.18);
        ConfirmPressColor = Oklch.WithAlpha(n.Danger, 0.22);
        ConfirmFocusColor = Oklch.WithAlpha(n.Danger, 0.20);
    }
}
