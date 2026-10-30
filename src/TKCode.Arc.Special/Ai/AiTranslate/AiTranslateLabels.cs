namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AiTranslate"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the phrases that name a language are format functions.
/// </summary>
public sealed class AiTranslateLabels
{
    /// <summary>The English defaults.</summary>
    public static AiTranslateLabels Default { get; } = new();

    /// <summary>The picker's label while the original is on screen.</summary>
    public string Translate { get; init; } = "Translate";

    /// <summary>The name of the language menu.</summary>
    public string TranslateTo { get; init; } = "Translate to";

    /// <summary>The note after the source language in the menu.</summary>
    public string Original { get; init; } = "Original";

    /// <summary>The toggle while a translation is on screen.</summary>
    public string ShowOriginal { get; init; } = "Show original";

    /// <summary>The toggle while the original is on screen and a translation is remembered.</summary>
    public string ShowTranslation { get; init; } = "Show translation";

    /// <summary>The button after a failure.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The status after a failure.</summary>
    public string Failed { get; init; } = "Couldn't translate";

    /// <summary>The status while the original is on screen after a translation.</summary>
    public string ShowingOriginal { get; init; } = "Showing original";

    /// <summary>The status while a translation streams, from the language's English name: "Translating to German".</summary>
    public Func<string, string> TranslatingTo { get; init; } = language => "Translating to " + language;

    /// <summary>The status of a finished translation, from the source language's English name: "Translated from French".</summary>
    public Func<string, string> TranslatedFrom { get; init; } = language => "Translated from " + language;

    /// <summary>Added to every announcement while the translations are simulated.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent";

    /// <summary>The chip when the language was detected from the text: "Detected · French".</summary>
    public Func<string, string> Detected { get; init; } = language => "Detected · " + language;

    /// <summary>The chip when the app passed the language: "Written in French".</summary>
    public Func<string, string> WrittenIn { get; init; } = language => "Written in " + language;

    /// <summary>The failure when no ready translation exists for a language and no translator is set.</summary>
    public Func<string, string> Missing { get; init; } = language => "No " + language + " translation is available.";

    /// <summary>The card's accessible name, from the author's name (null without a byline).</summary>
    public Func<string?, string> Region { get; init; } = name => string.IsNullOrEmpty(name) ? "Message" : "Message from " + name;

    /// <summary>A language's English name for statuses and the chip: "German".</summary>
    public Func<AiTranslateLanguage, string> LanguageName { get; init; } = language => language switch
    {
        AiTranslateLanguage.German => "German",
        AiTranslateLanguage.French => "French",
        AiTranslateLanguage.Spanish => "Spanish",
        AiTranslateLanguage.Japanese => "Japanese",
        AiTranslateLanguage.Portuguese => "Portuguese",
        _ => "English",
    };

    /// <summary>A language's own name for the picker and its menu: "Deutsch".</summary>
    public Func<AiTranslateLanguage, string> NativeName { get; init; } = language => language switch
    {
        AiTranslateLanguage.German => "Deutsch",
        AiTranslateLanguage.French => "Français",
        AiTranslateLanguage.Spanish => "Español",
        AiTranslateLanguage.Japanese => "日本語",
        AiTranslateLanguage.Portuguese => "Português",
        _ => "English",
    };
}
