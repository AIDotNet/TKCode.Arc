namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented support conversation the PII redaction demos play: a double charge request with a name, a card, an
/// email and a phone number, and the model's reply with placeholders. Every value is made up: the name is invented, the
/// email is on example.com, the phone number is in the fictional 555 range and the card number is a well-known test
/// number (it passes Luhn and belongs to nobody). The model named is a real one from <see cref="AiModelCatalog"/>.
/// Demo content only.
/// </summary>
internal static class PiiRedactionDemoData
{
    /// <summary>The name the detector always flags.</summary>
    public const string Name = "Juniper Vale";

    /// <summary>The names the detector always flags.</summary>
    public static IReadOnlyList<string> Names { get; } = [Name];

    /// <summary>A double charge support request with a name, a test card, an email and a phone number.</summary>
    public const string Message =
        "Hi, I'm Juniper Vale. I was charged twice for my March order on my card 4242 4242 4242 4242. " +
        "Please send the refund confirmation to juniper.vale@example.com or call me at (555) 555-0142.";

    /// <summary>The model's reply, placeholders included (the guard restores them).</summary>
    public const string Reply =
        "Sorry about the double charge, [NAME_1]. I can see two payments for the March order on [CARD_1], and I have " +
        "refunded the second one. A confirmation is on its way to [EMAIL_1], and if anything looks off we will call you " +
        "at [PHONE_1]. The refund reaches your card in 3 to 5 business days.";

    /// <summary>The model the safe prompt goes to.</summary>
    public static string ModelName => AiModelCatalog.Grok47.Name;

    /// <summary>A long model name, to show it truncating in a narrow flow.</summary>
    public static string LongModelName => AiModelCatalog.Grok420NonReasoning.Name;
}
