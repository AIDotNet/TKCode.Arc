using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// What an <see cref="ArenaIdentity"/> shows: the blind name alone, or once revealed the model's mark (16px, 8px before the
/// name) and its name. An identity makes a new face for every swap, so the old one can fade out while the new one comes in;
/// a renamed model or a new blind name changes the face in place.
/// </summary>
internal sealed class ArenaIdentityFace : TemplatedControl
{
    /// <summary>The name shown.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArenaIdentityFace, string?>(nameof(Text));

    /// <summary>The model's mark (a <see cref="Geometry"/> or an <see cref="IImage"/>); null shows the name alone.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<ArenaIdentityFace, object?>(nameof(Mark));

    /// <summary>The fill of a geometry mark; null uses the text color.</summary>
    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<ArenaIdentityFace, IBrush?>(nameof(MarkColor));

    /// <summary>Whether the face shows a model (as opposed to the blind name); fixed for the face's life.</summary>
    public bool ShowsModel { get; init; }

    /// <summary>The model a revealed face shows; null for the blind face.</summary>
    public AiModel? Model { get; init; }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkColorProperty"/>
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }
}
