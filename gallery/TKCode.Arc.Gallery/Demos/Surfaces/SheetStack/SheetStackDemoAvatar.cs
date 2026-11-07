using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Builds the round avatars of the sheet stack demos from the invented sample choices (no photos).</summary>
internal static class SheetStackDemoAvatar
{
    /// <summary>
    /// A round avatar <paramref name="size"/> across: the choice's picture, its initials on its color, or (no choice, after
    /// "Remove photo") the person's initials on the muted surface.
    /// </summary>
    public static Control Create(SheetStackSampleAvatar? choice, string name, double size)
    {
        var frame = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            ClipToBounds = true,
            BorderThickness = new Thickness(choice is null ? 1 : 0),
        };
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        if (choice?.Image is { } image)
        {
            frame.Background = choice.Background;
            frame.Child = new Image { Source = image, Stretch = Stretch.UniformToFill };
            return frame;
        }
        var text = new TextBlock
        {
            Text = SheetStackSamples.Initials(name),
            FontSize = Math.Round(size * 0.36),
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (choice is null)
        {
            ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        }
        else
        {
            frame.Background = choice.Background;
            text.Foreground = choice.Foreground;
        }
        frame.Child = text;
        return frame;
    }
}
