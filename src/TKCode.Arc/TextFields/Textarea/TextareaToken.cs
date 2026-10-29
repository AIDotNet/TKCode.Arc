using Avalonia;

namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class TextareaToken : AbstractControlDesignToken
{
    /// <summary>Arc: <c>min-height: 110px</c>.</summary>
    public double FrameMinHeight { get; set; }

    /// <summary>Arc: <c>padding: var(--space-3)</c> on every side.</summary>
    public Thickness FramePadding { get; set; }

    /// <summary>Arc: <c>opacity: .6</c> when disabled.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Pulls the grip into the frame padding, 2px from the border.</summary>
    public Thickness ResizeGripMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FrameMinHeight = 110;
        FramePadding = new Thickness(12);
        DisabledOpacity = 0.6;
        ResizeGripMargin = new Thickness(0, 0, -10, -10);
    }
}
