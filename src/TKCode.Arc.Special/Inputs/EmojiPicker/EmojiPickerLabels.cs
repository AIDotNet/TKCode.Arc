namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="EmojiPicker"/> shows or says, in English by default. Replace the whole object to translate
/// or reword it. Emoji names come from the Unicode data and stay in English.
/// </summary>
public sealed class EmojiPickerLabels
{
    /// <summary>The English defaults.</summary>
    public static EmojiPickerLabels Default { get; } = new();

    /// <summary>The search field's placeholder and name.</summary>
    public string Search { get; init; } = "Search emoji";

    /// <summary>The title of the search results.</summary>
    public string SearchResults { get; init; } = "Search results";

    /// <summary>What the picker says when nothing matches.</summary>
    public string NoResults { get; init; } = "No emoji found";

    /// <summary>The number of matches, for screen readers: "12 emoji found".</summary>
    public Func<int, string> ResultCount { get; init; } = count => count == 1 ? "1 emoji found" : count + " emoji found";

    /// <summary>The name of the category tabs.</summary>
    public string Categories { get; init; } = "Emoji categories";

    /// <summary>The name of the emoji grid.</summary>
    public string Grid { get; init; } = "Emoji";

    /// <summary>The title of each section.</summary>
    public Func<EmojiCategory, string> Category { get; init; } = category => category switch
    {
        EmojiCategory.Recent => "Recently used",
        EmojiCategory.Smileys => "Smileys and emotion",
        EmojiCategory.People => "People and body",
        EmojiCategory.Nature => "Animals and nature",
        EmojiCategory.Food => "Food and drink",
        EmojiCategory.Travel => "Travel and places",
        EmojiCategory.Activities => "Activities",
        EmojiCategory.Objects => "Objects",
        EmojiCategory.Symbols => "Symbols",
        _ => "Flags"
    };

    /// <summary>The skin tone button's name.</summary>
    public string SkinTone { get; init; } = "Skin tone";

    /// <summary>The name of each skin tone.</summary>
    public Func<EmojiSkinTone, string> Tone { get; init; } = tone => tone switch
    {
        EmojiSkinTone.Light => "Light",
        EmojiSkinTone.MediumLight => "Medium light",
        EmojiSkinTone.Medium => "Medium",
        EmojiSkinTone.MediumDark => "Medium dark",
        EmojiSkinTone.Dark => "Dark",
        _ => "Default"
    };

    /// <summary>The preview line's text when no emoji is under the pointer or the keyboard.</summary>
    public string PreviewHint { get; init; } = "Pick an emoji";

    /// <summary>The position of the active emoji, for screen readers: "3 of 168".</summary>
    public Func<int, int, string> Position { get; init; } = (index, count) => index + " of " + count;
}
