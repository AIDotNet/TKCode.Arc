namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented email the reply draft demos answer: a request to move a launch review, three quick replies and a reply
/// written for each quick reply, tone and length. Every name and address is made up; the model credited is a real one
/// from <see cref="AiModelCatalog"/>. Demo content only.
/// </summary>
internal static class ReplyDraftDemoData
{
    /// <summary>Who signs the replies.</summary>
    public const string Signature = "Sam";

    /// <summary>The email: a request to move a launch review.</summary>
    public static ReplyDraftMessage Message { get; } = new()
    {
        FromName = "Maya Chen",
        FromEmail = "maya.chen@harborline.example",
        Subject = "Can we move Thursday's launch review?",
        Body = "Hi Sam,\n\n"
            + "The pricing page copy came back from legal with a few changes, and I don't think we can hold the launch review on "
            + "Thursday at 2pm without them.\n\n"
            + "Could we push it to next Tuesday at 10am instead? That gives us time to fold in their notes and rerun the "
            + "screenshots. If Tuesday doesn't work, Wednesday afternoon is open on my side too.\n\n"
            + "Thanks,\nMaya",
        ReceivedAt = new DateTimeOffset(DateTime.Today.AddHours(9).AddMinutes(41)),
    };

    /// <summary>The quick replies.</summary>
    public static IReadOnlyList<ReplyDraftSuggestion> Suggestions { get; } =
    [
        new("tuesday", "Tuesday works"),
        new("wednesday", "Try Wednesday"),
        new("thursday", "Keep Thursday"),
    ];

    /// <summary>The model credited on the reply (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.MistralMedium35.Name);

    // Four sentences per quick reply and tone: Short takes the first, Medium the first two, Long all four in two paragraphs.
    private static readonly Dictionary<(string Id, ReplyDraftTone Tone), string[]> s_sentences = new()
    {
        [("tuesday", ReplyDraftTone.Friendly)] =
        [
            "Tuesday at 10am works great for me, so let's move the launch review there.",
            "Thanks for chasing legal on the pricing copy. Having their notes in first will make the review much smoother.",
            "I'll update the invite and ask Priya to rerun the screenshots on Monday afternoon.",
            "If anything else comes back from legal before then, just drop it in the doc and I'll take a look.",
        ],
        [("tuesday", ReplyDraftTone.Concise)] =
        [
            "Tuesday at 10am works. I'll move the review.",
            "Please share legal's notes once they're in.",
            "I'll update the invite today and get the screenshots rerun on Monday.",
            "Anything else from legal can go straight into the doc.",
        ],
        [("tuesday", ReplyDraftTone.Formal)] =
        [
            "Thank you for letting me know. Tuesday at 10am suits me, and I will reschedule the launch review accordingly.",
            "I appreciate you following up with legal on the pricing copy; reviewing their changes beforehand will make the session more productive.",
            "I will send an updated invitation today and arrange for the screenshots to be regenerated on Monday.",
            "Should legal raise any further points before then, please add them to the review document and I will read them ahead of the meeting.",
        ],
        [("wednesday", ReplyDraftTone.Friendly)] =
        [
            "Tuesday morning is tricky for me, but Wednesday afternoon would be perfect.",
            "How about 2pm? That still gives us a full day to fold in legal's notes.",
            "I'll hold the slot on my calendar and move the invite once you confirm.",
            "And thanks for flagging this early, it's much easier to shift now than on Thursday morning.",
        ],
        [("wednesday", ReplyDraftTone.Concise)] =
        [
            "Tuesday doesn't work for me. Wednesday afternoon does.",
            "Let's do 2pm.",
            "I'll move the invite once you confirm.",
            "That leaves a day for legal's notes and the screenshots.",
        ],
        [("wednesday", ReplyDraftTone.Formal)] =
        [
            "Thank you for the suggestion. Unfortunately, Tuesday morning is not possible for me; Wednesday afternoon would be preferable.",
            "Would 2pm on Wednesday be convenient? It would still allow time to incorporate legal's comments.",
            "I will reserve the slot and update the invitation as soon as you confirm.",
            "Thank you as well for raising this early, which makes the change considerably easier to arrange.",
        ],
        [("thursday", ReplyDraftTone.Friendly)] =
        [
            "I'd love to keep Thursday at 2pm if we can, since the launch date depends on it.",
            "Could we review everything except the pricing page, and pick up legal's changes in a quick follow-up?",
            "I'm happy to set up a 15-minute slot on Friday for just the pricing copy.",
            "Let me know if that feels too tight and we can figure something else out.",
        ],
        [("thursday", ReplyDraftTone.Concise)] =
        [
            "Let's keep Thursday at 2pm; the launch date depends on it.",
            "We can review everything but pricing and cover legal's changes separately.",
            "I'll add a 15-minute pricing check on Friday.",
            "Tell me if that's too tight.",
        ],
        [("thursday", ReplyDraftTone.Formal)] =
        [
            "If possible, I would prefer to keep the review on Thursday at 2pm, as the launch date depends on it.",
            "Perhaps we could review all materials except the pricing page and address legal's changes in a brief follow-up.",
            "I would be glad to schedule a 15-minute session on Friday dedicated to the pricing copy.",
            "Please let me know if this timeline seems too tight, and we can consider alternatives.",
        ],
    };

    /// <summary>A reply for every quick reply, tone and length (3 × 3 × 3).</summary>
    public static IReadOnlyList<ReplyDraftVersion> Replies { get; } =
    [
        .. from suggestion in new[] { "tuesday", "wednesday", "thursday" }
           from tone in new[] { ReplyDraftTone.Friendly, ReplyDraftTone.Concise, ReplyDraftTone.Formal }
           from length in new[] { ReplyDraftLength.Short, ReplyDraftLength.Medium, ReplyDraftLength.Long }
           select new ReplyDraftVersion(suggestion, tone, length, Reply(suggestion, tone, length)),
    ];

    /// <summary>A new simulation of the email (each card gets its own).</summary>
    public static ReplyDraftSimulation Simulation() => new()
    {
        Message = Message,
        Suggestions = Suggestions,
        Model = Model,
        Replies = Replies,
    };

    /// <summary>The reply for a quick reply, tone and length, greeting Maya and signed by Sam.</summary>
    public static string Reply(string suggestionId, ReplyDraftTone tone, ReplyDraftLength length)
    {
        var sentences = s_sentences[(suggestionId, tone)];
        var greeting = tone == ReplyDraftTone.Formal ? "Dear Maya," : "Hi Maya,";
        var closing = tone switch
        {
            ReplyDraftTone.Formal => "Kind regards,\n" + Signature,
            ReplyDraftTone.Concise => Signature,
            _ => "Thanks!\n" + Signature,
        };
        var body = length switch
        {
            ReplyDraftLength.Short => sentences[0],
            ReplyDraftLength.Long => sentences[0] + " " + sentences[1] + "\n\n" + sentences[2] + " " + sentences[3],
            _ => sentences[0] + " " + sentences[1],
        };
        return greeting + "\n\n" + body + "\n\n" + closing;
    }
}
