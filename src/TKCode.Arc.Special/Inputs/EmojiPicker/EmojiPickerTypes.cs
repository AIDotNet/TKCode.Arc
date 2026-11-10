using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The sections of an <see cref="EmojiPicker"/>: recently used, then the Unicode emoji groups in CLDR order.</summary>
public enum EmojiCategory
{
    /// <summary>The emoji picked most recently (<see cref="EmojiPicker.Recent"/>).</summary>
    Recent,

    Smileys,
    People,
    Nature,
    Food,
    Travel,
    Activities,
    Objects,
    Symbols,
    Flags
}

/// <summary>The skin tone an <see cref="EmojiPicker"/> applies to the people and hands that have tones.</summary>
public enum EmojiSkinTone
{
    /// <summary>The default (yellow) form.</summary>
    Default,

    Light,
    MediumLight,
    Medium,
    MediumDark,
    Dark
}

/// <summary>Arguments of <see cref="EmojiPicker.EmojiSelected"/>.</summary>
public class EmojiSelectedEventArgs : RoutedEventArgs
{
    public EmojiSelectedEventArgs(RoutedEvent routedEvent, string emoji, string baseEmoji, string name, EmojiCategory category)
        : base(routedEvent)
    {
        Emoji = emoji;
        BaseEmoji = baseEmoji;
        Name = name;
        Category = category;
    }

    /// <summary>The picked emoji with the chosen skin tone applied: the text to insert.</summary>
    public string Emoji { get; }

    /// <summary>The same emoji without a skin tone (the form kept in <see cref="EmojiPicker.Recent"/>).</summary>
    public string BaseEmoji { get; }

    /// <summary>The emoji's Unicode short name in English ("waving hand").</summary>
    public string Name { get; }

    /// <summary>The Unicode group it belongs to (never <see cref="EmojiCategory.Recent"/>).</summary>
    public EmojiCategory Category { get; }
}
