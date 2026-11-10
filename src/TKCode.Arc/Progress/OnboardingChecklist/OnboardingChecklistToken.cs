using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class OnboardingChecklistToken : AbstractControlDesignToken
{
    /// <summary>Padding of the header; the card has none of its own, so hiding the body leaves an evenly padded bar.</summary>
    public Thickness HeaderPadding { get; set; }

    /// <summary>The gap between the ring (or the check) and the text, and between the text and the button.</summary>
    public double ColumnGap { get; set; }
    public Thickness BodyPadding { get; set; }
    public Thickness StepPadding { get; set; }

    /// <summary>The ring's box and the check's press target.</summary>
    public double LeadSize { get; set; }

    /// <summary>The ring's outer diameter (Arc: r = 17 with a stroke of 3).</summary>
    public double RingSize { get; set; }
    public double RingThickness { get; set; }
    public double CheckSize { get; set; }
    public Thickness CheckThickness { get; set; }
    public double TitleMinHeight { get; set; }
    public double HeadingLineHeight { get; set; }
    public double HeadingLetterSpacing { get; set; }
    public double BodyLineHeight { get; set; }
    public double HeadingGap { get; set; }
    public Thickness HidePadding { get; set; }
    public double HideGap { get; set; }
    public Thickness PanelPadding { get; set; }

    /// <summary>Pulls the description up under the title.</summary>
    public Thickness DescriptionMargin { get; set; }

    /// <summary>Arc caps the description at 46ch.</summary>
    public double DescriptionMaxWidth { get; set; }
    public Thickness ErrorPadding { get; set; }
    public Thickness ActionMargin { get; set; }
    public Thickness ActionPadding { get; set; }
    public double ActionGap { get; set; }
    public double GlyphSize { get; set; }
    public double SpinnerSize { get; set; }
    public double GroupMinHeight { get; set; }
    public Thickness GroupMargin { get; set; }
    public Thickness GroupPadding { get; set; }
    public Thickness FocusRingMargin { get; set; }

    /// <summary>The row and group hover: 60% of the muted surface.</summary>
    public Color HoverBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        HeaderPadding = new Thickness(20);
        ColumnGap = 12;
        BodyPadding = new Thickness(8);
        StepPadding = new Thickness(12, 4);
        LeadSize = 40;
        RingSize = 37;
        RingThickness = 3;
        CheckSize = 22;
        CheckThickness = new Thickness(1.5);
        TitleMinHeight = 40;
        HeadingLineHeight = 22.4;
        HeadingLetterSpacing = -0.16;
        BodyLineHeight = 19.6;
        HeadingGap = 1;
        HidePadding = new Thickness(12, 0, 8, 0);
        HideGap = 4;
        PanelPadding = new Thickness(0, 0, 0, 12);
        DescriptionMargin = new Thickness(0, -4, 0, 0);
        DescriptionMaxWidth = 368;
        ErrorPadding = new Thickness(0, 8, 0, 0);
        ActionMargin = new Thickness(0, 12, 0, 0);
        ActionPadding = new Thickness(16, 0, 12, 0);
        ActionGap = 8;
        GlyphSize = 16;
        SpinnerSize = 14;
        GroupMinHeight = 44;
        GroupMargin = new Thickness(0, 2, 0, 0);
        GroupPadding = new Thickness(64, 0, 12, 0);
        FocusRingMargin = new Thickness(-2);
        HoverBackground = Oklch.WithAlpha(n.SurfaceMuted, 0.6);
    }
}
