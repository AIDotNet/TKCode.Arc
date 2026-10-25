using AtomUI.Theme.DesignTokens;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ColorPickerToken : AbstractControlDesignToken
{
    /// <summary>The swatch trigger and the panel head: 44px tall pill (radius 22), 32px chip, 10px gap.</summary>
    public double TriggerHeight { get; set; }
    public CornerRadius TriggerRadius { get; set; }
    public Thickness TriggerInset { get; set; }
    public Thickness HeadInset { get; set; }
    public double ChipDiameter { get; set; }
    public double TriggerGap { get; set; }

    /// <summary>Label (12px) and hex (14px) on Arc's 1.15 line.</summary>
    public double NameLineHeight { get; set; }
    public double HexLineHeight { get; set; }
    public Thickness TriggerFocusRingMargin { get; set; }
    public CornerRadius TriggerFocusRingRadius { get; set; }

    /// <summary>Panel: min(19rem, viewport − 32px) wide; body inset 8 / 14 (plus the 1px border), 14px rhythm.</summary>
    public double PanelWidth { get; set; }
    public Thickness BodyInset { get; set; }
    public double BodyGap { get; set; }
    public double AreaHeight { get; set; }
    public double AreaRadius { get; set; }
    public double TrackThickness { get; set; }
    public Thickness SliderMargin { get; set; }

    /// <summary>Value field: 40px row (2px extra above), radius 14; format button at least 56px wide.</summary>
    public double FieldHeight { get; set; }
    public Thickness FieldMargin { get; set; }
    public CornerRadius FieldRadius { get; set; }
    public double FormatMinWidth { get; set; }
    public Thickness FormatInset { get; set; }
    public Thickness InputInset { get; set; }
    public Thickness ErrorMargin { get; set; }

    /// <summary>Contrast row: 36 × 28 sample (radius 9), 22px grade pill.</summary>
    public double SampleWidth { get; set; }
    public double SampleHeight { get; set; }
    public CornerRadius SampleRadius { get; set; }
    public double GradeHeight { get; set; }
    public Thickness GradeInset { get; set; }

    /// <summary>Done button: 32px, radius 12.</summary>
    public double IconButtonSize { get; set; }
    public CornerRadius IconButtonRadius { get; set; }

    /// <summary>Accent-independent colors: panel fill, checkerboard, the floating drop shadow, the format hover and grade fills.</summary>
    public Color PanelFill { get; set; }
    public Color CheckerColor { get; set; }
    public Color CheckerBase { get; set; }
    public BoxShadows FloatShadow { get; set; }
    public Color FormatHoverFill { get; set; }
    public Color GradePassFill { get; set; }
    public Color GradeLargeFill { get; set; }
    public Color GradeFailFill { get; set; }

    public CornerRadius FormatFocusRingRadius { get; set; }
    public CornerRadius AddFocusRingRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FormatFocusRingRadius = new CornerRadius(10);
        AddFocusRingRadius = new CornerRadius(999);
        TriggerHeight = 44;
        TriggerRadius = new CornerRadius(22);
        TriggerInset = new Thickness(6, 5, 16, 5);
        HeadInset = new Thickness(7, 6, 7, 6);
        ChipDiameter = 32;
        TriggerGap = 10;
        NameLineHeight = 12 * 1.15;
        HexLineHeight = 14 * 1.15;
        TriggerFocusRingMargin = new Thickness(-4);
        TriggerFocusRingRadius = new CornerRadius(26);
        PanelWidth = 304;
        BodyInset = new Thickness(15, 8, 15, 15);
        BodyGap = 14;
        AreaHeight = 164;
        AreaRadius = 16;
        TrackThickness = 14;
        SliderMargin = new Thickness(0, -4);
        FieldHeight = 40;
        FieldMargin = new Thickness(0, 2, 0, 0);
        FieldRadius = new CornerRadius(14);
        FormatMinWidth = 56;
        FormatInset = new Thickness(10, 0);
        InputInset = new Thickness(10, 0);
        ErrorMargin = new Thickness(2, -10, 2, -4);
        SampleWidth = 36;
        SampleHeight = 28;
        SampleRadius = new CornerRadius(9);
        GradeHeight = 22;
        GradeInset = new Thickness(8, 0);
        IconButtonSize = 32;
        IconButtonRadius = new CornerRadius(12);

        var n = ArcPalette.Neutrals(isDarkMode);
        PanelFill = isDarkMode ? n.SurfaceRaised : n.Surface;
        CheckerColor = isDarkMode ? Oklch.ToColor(0.42, 0, 0) : Oklch.ToColor(0.88, 0, 0);
        CheckerBase = isDarkMode ? Oklch.ToColor(0.30, 0, 0) : Colors.White;
        FloatShadow = isDarkMode
            ? new BoxShadows(new BoxShadow { OffsetY = 20, Blur = 44, Color = Color.FromArgb(128, 0, 0, 0) })
            : new BoxShadows(new BoxShadow { OffsetY = 18, Blur = 36, Color = Color.FromArgb(31, 0, 0, 0) },
                [new BoxShadow { OffsetY = 2, Blur = 6, Color = Color.FromArgb(15, 0, 0, 0) }]);
        FormatHoverFill = Oklch.WithAlpha(n.Foreground, 0.05);
        GradePassFill = Oklch.WithAlpha(n.Success, 0.14);
        GradeLargeFill = Oklch.WithAlpha(n.Warning, 0.16);
        GradeFailFill = Oklch.WithAlpha(n.Danger, 0.12);
    }
}
