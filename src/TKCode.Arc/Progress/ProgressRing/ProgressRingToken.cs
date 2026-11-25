using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class ProgressRingToken : AbstractControlDesignToken
{
    /// <summary>Default diameter of a Small <see cref="ProgressRing"/>.</summary>
    public double DiameterSM { get; set; }

    /// <summary>Default diameter of a Medium <see cref="ProgressRing"/>.</summary>
    public double DiameterMD { get; set; }

    /// <summary>Default diameter of a Large <see cref="ProgressRing"/>.</summary>
    public double DiameterLG { get; set; }

    /// <summary>Stroke thickness for Small size.</summary>
    public double StrokeThicknessSM { get; set; }

    /// <summary>Stroke thickness for Medium size.</summary>
    public double StrokeThicknessMD { get; set; }

    /// <summary>Stroke thickness for Large size.</summary>
    public double StrokeThicknessLG { get; set; }

    /// <summary>Center percentage font size for Small size.</summary>
    public double ValueFontSizeSM { get; set; }

    /// <summary>Center percentage font size for Medium size.</summary>
    public double ValueFontSizeMD { get; set; }

    /// <summary>Center percentage font size for Large size.</summary>
    public double ValueFontSizeLG { get; set; }

    /// <summary>Completion checkmark size for Small size.</summary>
    public double CheckSizeSM { get; set; }

    /// <summary>Completion checkmark size for Medium size.</summary>
    public double CheckSizeMD { get; set; }

    /// <summary>Completion checkmark size for Large size.</summary>
    public double CheckSizeLG { get; set; }

    /// <summary>Margin above the optional label below the ring.</summary>
    public Thickness LabelMargin { get; set; }

    /// <summary>Line height of the label below the ring.</summary>
    public double LabelLineHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DiameterSM = 20;
        DiameterMD = 40;
        DiameterLG = 64;
        StrokeThicknessSM = 2;
        StrokeThicknessMD = 3;
        StrokeThicknessLG = 4;
        ValueFontSizeSM = 9;
        ValueFontSizeMD = 11;
        ValueFontSizeLG = 14;
        CheckSizeSM = 10;
        CheckSizeMD = 16;
        CheckSizeLG = 24;
        LabelMargin = new Thickness(0, 8, 0, 0);
        LabelLineHeight = 19.6;
    }

    public override void ApplyCompact(bool isDarkMode)
    {
        base.ApplyCompact(isDarkMode);
        DiameterSM = 16;
        DiameterMD = 32;
        DiameterLG = 52;
        StrokeThicknessSM = 2;
        StrokeThicknessMD = 2.5;
        StrokeThicknessLG = 3.5;
        ValueFontSizeSM = 8;
        ValueFontSizeMD = 10;
        ValueFontSizeLG = 12;
        CheckSizeSM = 8;
        CheckSizeMD = 14;
        CheckSizeLG = 20;
        LabelMargin = new Thickness(0, 6, 0, 0);
        LabelLineHeight = 13 * 1.38;
    }
}
