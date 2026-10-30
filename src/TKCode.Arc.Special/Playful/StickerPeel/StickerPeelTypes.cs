using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The outline of a sticker's die cut. The peel starts from the real edge of this shape.</summary>
public enum StickerPeelShape
{
    /// <summary>A circle (the smaller side is its diameter). It peels from the point of the rim nearest the pointer.</summary>
    Circle,

    /// <summary>A rectangle with rounded corners. It peels from a corner along its diagonal.</summary>
    Rounded,

    /// <summary>A pill: a rectangle with fully rounded ends. It peels from a corner along its diagonal.</summary>
    Pill
}

/// <summary>Where a sticker sits on its <see cref="StickerPeel"/> surface: the centre as fractions of the surface and the angle.</summary>
/// <param name="X">Centre across the surface, from 0 to 1.</param>
/// <param name="Y">Centre down the surface, from 0 to 1.</param>
/// <param name="Rotate">Rotation in degrees.</param>
public readonly record struct StickerPlacement(double X, double Y, double Rotate = 0);

/// <summary>
/// One sticker of a <see cref="StickerPeel"/>. Items are read when they are added to the surface; change a sticker by
/// replacing its item.
/// </summary>
public sealed class StickerPeelItem
{
    /// <summary>Stable id, used as the key in <see cref="StickerPeel.Positions"/>.</summary>
    public string Id { get; set; } = "";

    /// <summary>Accessible name, such as "Laptop sticker".</summary>
    public string Label { get; set; } = "";

    /// <summary>The printed face inside the white die cut border. Give it a fixed size.</summary>
    [Content]
    public object? Content { get; set; }

    /// <summary>Starting centre across the surface, from 0 to 1.</summary>
    public double X { get; set; } = 0.5;

    /// <summary>Starting centre down the surface, from 0 to 1.</summary>
    public double Y { get; set; } = 0.5;

    /// <summary>Starting rotation in degrees.</summary>
    public double Rotate { get; set; }

    /// <summary>Outline of the die cut.</summary>
    public StickerPeelShape Shape { get; set; } = StickerPeelShape.Rounded;
}

/// <summary>Data of <see cref="StickerPeel.PositionsChangedEvent"/>.</summary>
public sealed class StickerPeelPositionsChangedEventArgs : RoutedEventArgs
{
    internal StickerPeelPositionsChangedEventArgs(RoutedEvent routedEvent, object source, string id,
        IReadOnlyDictionary<string, StickerPlacement> positions)
        : base(routedEvent, source)
    {
        Id = id;
        Positions = positions;
    }

    /// <summary>Id of the sticker that was placed.</summary>
    public string Id { get; }

    /// <summary>The placement of every sticker, ready to save and to pass back as <see cref="StickerPeel.Positions"/>.</summary>
    public IReadOnlyDictionary<string, StickerPlacement> Positions { get; }
}
