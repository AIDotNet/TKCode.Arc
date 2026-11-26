using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>Geometry and backdrop of <see cref="ModalPage"/>: sheet top gap, corner radius, content padding and scrim.</summary>
[ControlDesignToken]
internal sealed class ModalPageToken : AbstractControlDesignToken
{
    /// <summary>Scrim color behind the modal surface.</summary>
    public Color ScrimColor { get; set; }

    /// <summary>Top offset in px when <see cref="ModalPage.Presentation"/> is <see cref="ModalPagePresentation.Sheet"/>: 16.</summary>
    public double SheetTopGap { get; set; }

    /// <summary>Top corner radius of the sheet presentation surface: 16.</summary>
    public CornerRadius SheetRadius { get; set; }

    /// <summary>Grabber width in px for the dismissible sheet presentation: 36.</summary>
    public double GrabberWidth { get; set; }

    /// <summary>Grabber height in px: 4.</summary>
    public double GrabberHeight { get; set; }

    /// <summary>Padding around the content inside the modal page: 0.</summary>
    public Thickness ContentInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        ScrimColor = isDarkMode ? Color.FromArgb(148, 0, 0, 0) : Color.FromArgb(96, 0, 0, 0);
        SheetTopGap = 16;
        SheetRadius = new CornerRadius(16, 16, 0, 0);
        GrabberWidth = 36;
        GrabberHeight = 4;
        ContentInset = default;
    }
}
