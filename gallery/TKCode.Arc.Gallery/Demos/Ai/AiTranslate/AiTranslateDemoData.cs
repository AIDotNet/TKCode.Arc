namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented message the AI translate demos play: a short French release note from an invented product lead, with
/// translations into the other five languages made ahead of time. The product, people and numbers are invented; the model
/// credited is a real one from <see cref="AiModelCatalog"/>. Demo content only.
/// </summary>
internal static class AiTranslateDemoData
{
    public const string Text =
        "La version 4.2 est disponible. Les exports PDF sont désormais deux fois plus rapides et les tableaux gardent leur mise en forme. " +
        "Merci à toutes celles et ceux qui ont signalé des bugs cette semaine.";

    public const string Timestamp = "Oct 27, 09:14";

    /// <summary>The model credited in the status line.</summary>
    public static string ModelName => AiModelCatalog.Gemini38Flash.Name;

    public static AiTranslateAuthor Author { get; } = new() { Name = "Camille Laurent", Role = "Product lead" };

    /// <summary>The self playing tour: English, Deutsch, 日本語 and Português, then back to the original.</summary>
    public static IReadOnlyList<AiTranslateLanguage> Tour { get; } =
    [
        AiTranslateLanguage.English, AiTranslateLanguage.German, AiTranslateLanguage.Japanese, AiTranslateLanguage.Portuguese,
    ];

    /// <summary>The release note in the other five languages.</summary>
    public static IReadOnlyDictionary<AiTranslateLanguage, string> Translations { get; } = new Dictionary<AiTranslateLanguage, string>
    {
        [AiTranslateLanguage.English] =
            "Version 4.2 is out. PDF exports are now twice as fast, and tables keep their formatting. " +
            "Thanks to everyone who reported bugs this week.",
        [AiTranslateLanguage.German] =
            "Version 4.2 ist da. PDF-Exporte sind jetzt doppelt so schnell, und Tabellen behalten ihre Formatierung. " +
            "Danke an alle, die diese Woche Fehler gemeldet haben.",
        [AiTranslateLanguage.Spanish] =
            "Ya está disponible la versión 4.2. Las exportaciones a PDF ahora son el doble de rápidas y las tablas conservan su formato. " +
            "Gracias a todas las personas que informaron de errores esta semana.",
        [AiTranslateLanguage.Japanese] =
            "バージョン4.2を公開しました。PDFの書き出しが2倍速くなり、表の書式も保たれるようになりました。" +
            "今週バグを報告してくださった皆さん、ありがとうございます。",
        [AiTranslateLanguage.Portuguese] =
            "A versão 4.2 já está disponível. As exportações em PDF agora são duas vezes mais rápidas e as tabelas mantêm a formatação. " +
            "Obrigado a todos que relataram bugs esta semana.",
    };
}
