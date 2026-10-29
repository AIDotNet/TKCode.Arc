using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="TagInput"/> (Arc <c>tag-input.module.css</c>); colours come from ArcDesign tokens.</summary>
[ControlDesignToken]
internal sealed class TagInputToken : AbstractControlDesignToken
{
    /// <summary><c>.content</c>: equal 7px padding, so one row fills the minimum height exactly.</summary>
    public Thickness ContentInset { get; set; }

    /// <summary><c>.content</c>: <c>gap: 6px</c> between tags, the editor and wrapped rows.</summary>
    public double ItemGap { get; set; }

    /// <summary><c>.content</c>: <c>min-height: calc(var(--control-height-md) - 2px)</c>.</summary>
    public double ContentMinHeight { get; set; }

    /// <summary><c>.tag</c>: <c>min-height: 28px</c>.</summary>
    public double ChipMinHeight { get; set; }

    /// <summary><c>.tag</c>: <c>padding: 0 4px 0 9px</c>.</summary>
    public Thickness ChipInset { get; set; }

    /// <summary><c>.tag</c>: <c>gap: 3px</c> between the label and the remove button.</summary>
    public double ChipLabelGap { get; set; }

    /// <summary><c>.tag button</c>: a 22px circle.</summary>
    public double RemoveButtonDiameter { get; set; }

    /// <summary>The Lucide X inside the remove button: 14px, stroke 2.</summary>
    public double RemoveGlyphSize { get; set; }

    public double RemoveGlyphStroke { get; set; }

    /// <summary><c>.tag button:focus-visible</c>: <c>box-shadow: inset 0 0 0 1.5px var(--focus-ring)</c>.</summary>
    public Thickness RemoveFocusRingThickness { get; set; }

    /// <summary><c>.tag button:active</c>: <c>transform: scale(.96)</c>.</summary>
    public double RemovePressScale { get; set; }

    /// <summary><c>.content input</c>: <c>flex: 1 1 100px; min-width: 80px; min-height: 28px; padding: 0 5px</c>.</summary>
    public double EditorBasis { get; set; }

    public double EditorMinWidth { get; set; }

    public double EditorMinHeight { get; set; }

    public Thickness EditorInset { get; set; }

    /// <summary><c>.placeholder</c>: <c>padding: 0 12px</c> (the content and the editor padding).</summary>
    public Thickness PlaceholderInset { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ContentInset = new Thickness(7);
        ItemGap = 6;
        ContentMinHeight = 44 - 2;
        ChipMinHeight = 28;
        ChipInset = new Thickness(9, 0, 4, 0);
        ChipLabelGap = 3;
        RemoveButtonDiameter = 22;
        RemoveGlyphSize = 14;
        RemoveGlyphStroke = 2;
        RemoveFocusRingThickness = new Thickness(1.5);
        RemovePressScale = 0.96;
        EditorBasis = 100;
        EditorMinWidth = 80;
        EditorMinHeight = 28;
        EditorInset = new Thickness(5, 0);
        PlaceholderInset = new Thickness(12, 0);
    }
}
