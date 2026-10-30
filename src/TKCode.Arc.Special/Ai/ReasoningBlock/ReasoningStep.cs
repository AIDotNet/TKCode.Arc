using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// One step of a model's reasoning: an optional heading and the text under it. Change <see cref="Text"/> in place as the
/// model streams; the <see cref="ReasoningBlock"/> follows every change.
/// </summary>
public sealed class ReasoningStep : AvaloniaObject
{
    /// <summary>The step's heading (a few words); empty for a step that is only text.</summary>
    public static readonly StyledProperty<string?> HeadingProperty =
        AvaloniaProperty.Register<ReasoningStep, string?>(nameof(Heading));

    /// <summary>The step's text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReasoningStep, string?>(nameof(Text));

    /// <summary>Creates an empty step.</summary>
    public ReasoningStep()
    {
    }

    /// <summary>Creates a step with a heading and text.</summary>
    public ReasoningStep(string? heading, string? text)
    {
        Heading = heading;
        Text = text;
    }

    /// <inheritdoc cref="HeadingProperty"/>
    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
