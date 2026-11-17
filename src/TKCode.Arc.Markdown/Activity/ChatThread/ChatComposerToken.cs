using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="ChatComposer"/> (Arc chat-thread.module.css, composer section).</summary>
[ControlDesignToken]
internal sealed class ChatComposerToken : AbstractControlDesignToken
{
    /// <summary>Composer padding: <c>10px 12px 12px</c>, under a 1px subtle top border.</summary>
    public Thickness ComposerPadding { get; set; }

    /// <summary>Gap between the attach button and the box (6px).</summary>
    public double InputGap { get; set; }

    /// <summary>Attach button (40px, 1px above the bottom).</summary>
    public double AttachSize { get; set; }

    /// <summary>Attach glyph (18px).</summary>
    public double AttachGlyphSize { get; set; }

    /// <summary>The rounded box around the field and the send button (21px).</summary>
    public CornerRadius BoxRadius { get; set; }

    /// <summary>The field inside it (20px).</summary>
    public CornerRadius FieldRadius { get; set; }

    /// <summary>Text padding: <c>9px 6px 9px 16px</c>.</summary>
    public Thickness TextPadding { get; set; }

    /// <summary>Text line height (22px at 16px).</summary>
    public double TextLineHeight { get; set; }

    /// <summary>Tallest field: six lines plus padding (6 × 22 + 18).</summary>
    public double FieldMaxHeight { get; set; }

    /// <summary>Send button (32px, 4px from the box's right and bottom edges).</summary>
    public double SendSize { get; set; }

    /// <summary>Send button margin.</summary>
    public Thickness SendMargin { get; set; }

    /// <summary>Tray padding: <c>8px 8px 10px 4px</c>, 8px between tiles.</summary>
    public Thickness TrayPadding { get; set; }

    /// <summary>Gap between tray tiles (8px).</summary>
    public double TrayGap { get; set; }

    /// <summary>Picture tiles (56px, radius 12px); file tiles are 168px wide.</summary>
    public double TileSize { get; set; }

    /// <summary>File tile width (168px).</summary>
    public double FileTileWidth { get; set; }

    /// <summary>Tile radius (12px).</summary>
    public CornerRadius TileRadius { get; set; }

    /// <summary>The remove button on a tile (20px, 6px outside its corner).</summary>
    public double RemoveSize { get; set; }

    /// <summary>Remove button offset.</summary>
    public Thickness RemoveMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ComposerPadding = new Thickness(12, 10, 12, 12);
        InputGap = 6;
        AttachSize = 40;
        AttachGlyphSize = 18;
        BoxRadius = new CornerRadius(21);
        FieldRadius = new CornerRadius(20);
        TextPadding = new Thickness(16, 9, 6, 9);
        TextLineHeight = 22;
        FieldMaxHeight = 6 * 22 + 18;
        SendSize = 32;
        SendMargin = new Thickness(0, 0, 4, 4);
        TrayPadding = new Thickness(4, 8, 8, 10);
        TrayGap = 8;
        TileSize = 56;
        FileTileWidth = 168;
        TileRadius = new CornerRadius(12);
        RemoveSize = 20;
        RemoveMargin = new Thickness(0, -6, -6, 0);
    }
}
