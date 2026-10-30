namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented session the tone tuner demos play: a billing reply to Sofia Ramirez about a duplicate charge, written out
/// for every formality, detail and voice. Sentences a dial does not touch are the same string in every version, so only the
/// changed ones crossfade. The people, invoice, amounts and card are invented; the model is a real one from
/// <see cref="AiModelCatalog"/>. Demo content only.
/// </summary>
internal static class ToneTunerDemoData
{
    public const string RecipientName = "Sofia Ramirez";

    public const string RecipientSubject = "Re: Duplicate charge on INV-20481";

    /// <summary>The model the status names while a rewrite runs.</summary>
    public static string Model => AiModelCatalog.ClaudeSonnet55.Name;

    /// <summary>The draft the session starts from: the reply at the starting tone.</summary>
    public static string Draft { get; } = Compose(ToneSettings.Default);

    /// <summary>The reply in every other tone (3 formalities × 3 details × 4 voices, the starting tone left out).</summary>
    public static IReadOnlyList<ToneTunerVersion> Versions { get; } =
    [
        .. from f in Enumerable.Range(0, 3)
           from d in Enumerable.Range(0, 3)
           from v in new[] { ToneVoice.Neutral, ToneVoice.Friendly, ToneVoice.Confident, ToneVoice.Empathetic }
           let settings = new ToneSettings(f, d, v)
           where settings != ToneSettings.Default
           select new ToneTunerVersion(settings, Compose(settings)),
    ];

    /// <summary>The reply in <paramref name="value"/>'s tone.</summary>
    public static string Compose(ToneSettings value)
    {
        var f = Math.Clamp(value.Formality, 0, 2);
        var d = Math.Clamp(value.Detail, 0, 2);
        var v = value.Voice;
        var paragraphs = new List<string> { Greeting(f, v) };
        var body = new List<string?> { Acknowledge(f, v), d >= 1 ? Cause(f, d) : null, Refund(f, v), Timing(f, d), d == 2 ? Prevention(f) : null };
        paragraphs.Add(string.Join(" ", body.Where(sentence => sentence is not null)));
        if (d >= 1)
        {
            var closer = Closer(f, v);
            if (v == ToneVoice.Empathetic)
            {
                closer += " " + Patience(f);
            }
            paragraphs.Add(closer);
        }
        paragraphs.Add(SignOff(f, v));
        return string.Join("\n\n", paragraphs);
    }

    private static string Greeting(int f, ToneVoice v) => (f, v) switch
    {
        (2, _) => "Dear Ms. Ramirez,",
        (_, ToneVoice.Friendly) => "Hi Sofia!",
        (0, _) => "Hey Sofia,",
        _ => "Hi Sofia,",
    };

    private static string Acknowledge(int f, ToneVoice v) => (v, f) switch
    {
        (ToneVoice.Friendly, 0) => "Thanks so much for flagging the double charge on INV-20481!",
        (ToneVoice.Friendly, 1) => "Thanks so much for letting us know about the duplicate charge on INV-20481.",
        (ToneVoice.Friendly, _) => "Thank you very much for bringing the duplicate charge on invoice INV-20481 to our attention.",
        (ToneVoice.Confident, 0) => "You're right, INV-20481 was charged twice.",
        (ToneVoice.Confident, 1) => "You're right: INV-20481 was charged twice.",
        (ToneVoice.Confident, _) => "You are correct that invoice INV-20481 was charged twice.",
        (ToneVoice.Empathetic, 0) => "I'm so sorry you got charged twice for INV-20481.",
        (ToneVoice.Empathetic, 1) => "I'm sorry you were charged twice for INV-20481, and I understand how frustrating that is.",
        (ToneVoice.Empathetic, _) => "I am sorry that you were charged twice for invoice INV-20481, and I understand how frustrating this must be.",
        (_, 0) => "Thanks for flagging the double charge on INV-20481.",
        (_, 1) => "Thank you for letting us know about the duplicate charge on INV-20481.",
        _ => "Thank you for bringing the duplicate charge on invoice INV-20481 to our attention.",
    };

    private static string Cause(int f, int d) => (f, d) switch
    {
        (0, 1) => "A payment retry on our side went through twice.",
        (0, _) => "A payment retry on our side went through twice after our processor timed out on March 3.",
        (1, 1) => "A payment retry on our side went through twice after a timeout.",
        (1, _) => "A payment retry on our side went through twice after our processor timed out on March 3.",
        (_, 1) => "A payment retry on our side was processed twice following a timeout.",
        _ => "A payment retry on our side was processed twice after our payment processor timed out on March 3.",
    };

    private static string Refund(int f, ToneVoice v) => (v == ToneVoice.Confident, f) switch
    {
        (true, 0) => "We've already refunded the extra $249.00 to your card ending in 4417.",
        (true, 1) => "We've already refunded the duplicate $249.00 to your card ending in 4417.",
        (true, _) => "The duplicate $249.00 has already been refunded to your card ending in 4417.",
        (_, 0) => "We've refunded the extra $249.00 to your card ending in 4417.",
        (_, 1) => "We have refunded the duplicate $249.00 to your card ending in 4417.",
        _ => "We have issued a full refund of the duplicate $249.00 to your card ending in 4417.",
    };

    private static string Timing(int f, int d) => (d, f) switch
    {
        (0, 0) => "It'll show up in 5–7 business days.",
        (0, 1) => "Expect it within 5–7 business days.",
        (0, _) => "Please allow 5–7 business days.",
        (1, 0) => "It should show up within 5–7 business days.",
        (1, _) => "It should appear on your statement within 5–7 business days.",
        (_, 0) => "It should show up within 5–7 business days, depending on your bank.",
        (_, 1) => "It should appear on your statement within 5–7 business days, depending on your bank.",
        _ => "It should appear on your statement within 5–7 business days, depending on your bank's processing time.",
    };

    private static string Prevention(int f) => f switch
    {
        0 => "We've also added a check so a retry can't charge you twice again.",
        1 => "We've also added a safeguard so a retried payment can't be charged twice again.",
        _ => "We have also added a safeguard to ensure that a retried payment cannot be charged twice in future.",
    };

    private static string Closer(int f, ToneVoice v) => (v, f) switch
    {
        (ToneVoice.Friendly, 0) => "If anything still looks off, just reply here and I'll sort it out!",
        (ToneVoice.Friendly, 1) => "If anything still looks off, just reply here and I'll happily sort it out.",
        (ToneVoice.Friendly, _) => "Should anything else look incorrect, please reply to this email and I will gladly look into it personally.",
        (ToneVoice.Confident, 0) => "If anything still looks off, reply here and I'll fix it the same day.",
        (ToneVoice.Confident, 1) => "If anything still looks off, reply to this email and I'll fix it the same day.",
        (ToneVoice.Confident, _) => "Should anything else look incorrect, reply to this email and I will resolve it the same day.",
        (ToneVoice.Empathetic, 0) => "If anything still looks off, just reply here, I'm happy to help.",
        (ToneVoice.Empathetic, 1) => "If anything still looks off, reply to this email and I'll make sure it's put right.",
        (ToneVoice.Empathetic, _) => "Should anything else look incorrect, please reply to this email and I will make sure it is put right.",
        (_, 0) => "If anything still looks off, just reply here.",
        (_, 1) => "If anything still looks off, reply to this email and I'll take care of it.",
        _ => "Should anything else look incorrect, please reply to this email and I will look into it personally.",
    };

    private static string Patience(int f) => f switch
    {
        0 => "Thanks for bearing with us.",
        1 => "Thank you for your patience.",
        _ => "Thank you for your patience while we resolved this.",
    };

    private static string SignOff(int f, ToneVoice v) => (f, v) switch
    {
        (2, _) => "Kind regards,\nMaya Chen\nBilling Support",
        (_, ToneVoice.Friendly) or (0, _) => "Cheers,\nMaya",
        (_, ToneVoice.Empathetic) => "Warm regards,\nMaya",
        _ => "Best,\nMaya",
    };
}
