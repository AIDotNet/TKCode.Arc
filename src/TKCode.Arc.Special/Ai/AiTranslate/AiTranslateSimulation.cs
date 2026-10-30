namespace TKCode.Arc;

/// <summary>
/// A message an <see cref="AiTranslate"/> plays without a translator, from data you give it: the text, its byline, the
/// model credited and translations made ahead of time. Set it as <see cref="AiTranslate.Simulation"/> while the card has
/// no <see cref="AiTranslate.Text"/>: picking a language streams the ready translation in place after a short wait (a
/// language without one shows the failure), the status says it is simulated and nothing was sent, and unless
/// <see cref="AiTranslate.AutoPlay"/> is false the card tours <see cref="Tour"/> by itself until someone presses a pointer
/// or a key inside it.
/// </summary>
public sealed class AiTranslateSimulation
{
    /// <summary>The message, in its original language.</summary>
    public string? Text { get; set; }

    /// <summary>The language of <see cref="Text"/>; null detects it from the text and the chip reads Detected.</summary>
    public AiTranslateLanguage? SourceLanguage { get; set; }

    /// <summary>The translations streamed when a language is picked. A language that is missing shows the failure.</summary>
    public IReadOnlyDictionary<AiTranslateLanguage, string>? Translations { get; set; }

    /// <summary>Who wrote the message, shown in the byline; null shows no byline.</summary>
    public AiTranslateAuthor? Author { get; set; }

    /// <summary>Shown after the author's role, for example "Oct 27, 09:14".</summary>
    public string? Timestamp { get; set; }

    /// <summary>The model credited in the status line (its idle label), when the card has no <see cref="AiTranslate.ModelName"/>.</summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// The languages the self playing tour picks in turn before it shows the original again, and loops. Null tours every
    /// language that has a translation, in the picker's order.
    /// </summary>
    public IReadOnlyList<AiTranslateLanguage>? Tour { get; set; }

    /// <summary>The ready translation for <paramref name="language"/>, or null.</summary>
    internal string? Translation(AiTranslateLanguage language) =>
        Translations is { } translations && translations.TryGetValue(language, out var text) ? text : null;
}
