namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// What the SpeechPlayer demos read: an invented travel reply credited to a real model from <see cref="AiModelCatalog"/>,
/// and four invented voices (the catalog lists text models only, no text to speech voices). Demo content only: no audio
/// plays and nothing is sent anywhere.
/// </summary>
internal static class SpeechPlayerDemoData
{
    /// <summary>The reply the preview reads.</summary>
    public const string Reply =
        "Lisbon in early May is a lovely choice. Mornings start near 16°C and afternoons reach the low twenties, so pack light layers and shoes with good grip for the steep, cobbled hills.\n\n" +
        "Spend your first morning in Alfama before the crowds arrive, then ride tram 28 across the old town. On day two, take an early train to Sintra and come back in time for sunset at a miradouro.";

    /// <summary>The model the preview's reply is credited to.</summary>
    public static string Author { get; } = AiModelCatalog.ClaudeSonnet55.Name;

    /// <summary>The model the chat-column demo's reply is credited to.</summary>
    public static string ChatAuthor { get; } = AiModelCatalog.ClaudeHaiku55.Name;

    /// <summary>Four invented voices with a one-line description each.</summary>
    public static IReadOnlyList<SpeechVoice> Voices { get; } =
    [
        new SpeechVoice("ava", "Ava", "Warm and conversational"),
        new SpeechVoice("leo", "Leo", "Calm, lower register"),
        new SpeechVoice("maya", "Maya", "Bright and upbeat"),
        new SpeechVoice("noah", "Noah", "Clear and steady"),
    ];
}
