namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented texts the smart form fill demos paste, each with the result a read returns, checked by hand: an email
/// signature and messy call notes. The people, companies and numbers are made up (emails on example.com, phone numbers in
/// the 555 range); the model named is a real one from <see cref="AiModelCatalog"/>. Demo content only.
/// </summary>
internal static class SmartFormFillDemoData
{
    /// <summary>An email signature under a short reply.</summary>
    public const string Signature =
        "Thanks again for the walkthrough today. Happy to set up a pilot with our dispatch team next week.\n\n" +
        "Best,\n" +
        "Maya Okafor\n" +
        "Head of Partnerships | Fernbrook Freight\n" +
        "maya.okafor@example.com\n" +
        "m. +1 (415) 555-0142 · o. +1 (415) 555-0100\n" +
        "500 Harbor Way, Suite 300, San Francisco";

    /// <summary>Messy call notes.</summary>
    public const string Notes =
        "call notes 10/14 - spoke w/ daniel reyes, runs ops at bluepine studio (i think?). wants a demo for his team before Q1.\n" +
        "email: d.reyes@example.com / cell 312.555.0187\n" +
        "they're moving offices, somewhere in chicago, will confirm";

    /// <summary>The model named beside the paste area.</summary>
    public static string ModelName => AiModelCatalog.DeepSeekV41Flash.Name;

    /// <summary>The samples behind Try sample, each with its hand-checked result.</summary>
    public static IReadOnlyList<SmartFormFillSample> Samples { get; } =
    [
        new("Email signature", Signature)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new("Maya Okafor", 0.98, "Maya Okafor"),
                ["title"] = new("Head of Partnerships", 0.95, "Head of Partnerships"),
                ["company"] = new("Fernbrook Freight", 0.96, "Fernbrook Freight"),
                ["email"] = new("maya.okafor@example.com", 0.99, "maya.okafor@example.com"),
                ["phone"] = new("+1 (415) 555-0142", 0.74, "+1 (415) 555-0142", "Two numbers in the text; picked the mobile."),
                ["address"] = new("500 Harbor Way, Suite 300, San Francisco, CA", 0.68, "500 Harbor Way, Suite 300, San Francisco",
                    "The state was inferred from the city."),
            },
        },
        new("Messy notes", Notes)
        {
            Result = new SmartFormFillResult
            {
                ["name"] = new("Daniel Reyes", 0.92, "daniel reyes"),
                ["title"] = new("Head of Operations", 0.56, "runs ops", "Paraphrased from \"runs ops\"."),
                ["company"] = new("Bluepine Studio", 0.71, "bluepine studio", "The notes add \"i think?\"."),
                ["email"] = new("d.reyes@example.com", 0.99, "d.reyes@example.com"),
                ["phone"] = new("+1 312 555 0187", 0.9, "312.555.0187"),
                ["address"] = new("Chicago, IL", 0.45, "somewhere in chicago", "Only the city is known; the office is moving."),
            },
        },
    ];
}
