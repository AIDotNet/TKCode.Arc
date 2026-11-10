using Avalonia;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The button face of an <see cref="ActionMorph"/>: its glyph and label. A plain Avalonia button, so Enter, Space and
/// the invoke pattern come with it; the keyboard ring is the morph's, drawn around the whole surface.
/// </summary>
internal sealed class ActionMorphFace : AvaloniaButton
{
    /// <summary>The 24 × 24 stroked glyph before the label.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ActionMorphFace, Geometry?>(nameof(Glyph));

    /// <summary>The label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ActionMorphFace, string?>(nameof(Label));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
}
