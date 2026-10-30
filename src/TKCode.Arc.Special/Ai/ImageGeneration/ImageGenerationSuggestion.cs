using Avalonia;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A prompt offered in the empty grid: a quiet pill with one line of text that trims to the grid's width (a stock Arc
/// button sizes to its label instead), so a long prompt never spills out of the grid.
/// </summary>
internal sealed class ImageGenerationSuggestion : AvaloniaButton
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ImageGenerationSuggestion, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
