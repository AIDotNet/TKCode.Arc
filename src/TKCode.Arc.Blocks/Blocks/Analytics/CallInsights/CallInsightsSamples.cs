namespace TKCode.Arc;

/// <summary>The invented call <see cref="CallInsights"/> shows while it has no data of its own: a 24-minute discovery call.</summary>
internal static class CallInsightsSamples
{
    public const string Title = "Discovery call · Brightmoor Freight";

    public const string Subtitle = "Tue, 24 min · Maya Okafor, Leo Brandt with Priya Raman, Tom Whitaker";

    private static TimeSpan T(int minutes, int seconds) => new(0, minutes, seconds);

    public static CallInsightsSpeaker[] Speakers() =>
    [
        new("maya", "Maya Okafor", "Account executive", isHost: true),
        new("leo", "Leo Brandt", "Solutions engineer", isHost: true),
        new("priya", "Priya Raman", "Head of operations"),
        new("tom", "Tom Whitaker", "IT lead"),
    ];

    public static CallInsightsUtterance[] Utterances() =>
    [
        new("maya", T(0, 0), T(0, 38), "Thanks for making the time, both of you. I'd love to start with how dispatch works for you today.", 0.4),
        new("priya", T(0, 40), T(1, 52), "Sure. We run about forty trucks out of two depots, and most planning still happens in a shared spreadsheet that one person owns.", 0),
        new("maya", T(1, 54), T(2, 20), "What happens when that person is out?", 0.1),
        new("priya", T(2, 22), T(3, 30), "Honestly, things slip. Last month we missed three pickups in one week and lost a customer over it.", -0.6),
        new("leo", T(3, 32), T(4, 10), "That's painful. Were those missed because the plan was wrong, or because drivers never saw the change?", 0),
        new("priya", T(4, 12), T(5, 5), "Mostly the second. Changes go out by text and some drivers just don't read them in time.", -0.4),
        new("tom", T(5, 8), T(5, 50), "And every new tool we add means another login for the drivers. That's my worry here.", -0.3),
        new("leo", T(5, 52), T(7, 10), "Fair. Drivers sign in once on the phone app and stay signed in. Dispatch changes show up as a card they have to confirm.", 0.3),
        new("tom", T(7, 12), T(7, 40), "Does it connect to our telematics boxes, or is that separate?", 0),
        new("leo", T(7, 42), T(9, 0), "We read positions from the two common telematics vendors directly. If yours is different we have an open import that takes a file every minute.", 0.2),
        new("tom", T(9, 2), T(9, 30), "Okay, that could work. I'd need to see the security review before we go further.", 0.1),
        new("maya", T(9, 32), T(10, 6), "Absolutely, I'll send our security pack today. Who else should be in that review?", 0.3),
        new("tom", T(10, 8), T(10, 34), "Our compliance lead, Grace. I'll loop her in.", 0.2),
        new("maya", T(10, 36), T(11, 20), "Priya, if the missed pickups went away, what would that be worth to the team?", 0.2),
        new("priya", T(11, 22), T(12, 40), "Keeping that one customer alone was worth more than any software we pay for. And dispatch would get its evenings back.", 0.6),
        new("leo", T(12, 42), T(14, 0), "Let me show you the board quickly. This is a day at a depot your size, with the late changes highlighted in amber.", 0.4),
        new("priya", T(14, 2), T(14, 40), "Oh, that's nice. Can I drag a stop from one truck to another?", 0.7),
        new("leo", T(14, 42), T(15, 20), "Yes, and the driver gets the card right away. If they don't confirm in ten minutes, dispatch sees it in red.", 0.5),
        new("priya", T(15, 22), T(15, 46), "That alone would have saved us last month.", 0.8),
        new("tom", T(15, 48), T(16, 30), "What does pricing look like? We looked at another planner, Routewise, and it was per truck.", -0.1),
        new("maya", T(16, 32), T(17, 50), "We price per active vehicle per month, with the driver app included. For forty trucks you'd sit in our middle tier.", 0.2),
        new("tom", T(17, 52), T(18, 30), "That's higher than what Routewise quoted, to be honest.", -0.5),
        new("maya", T(18, 32), T(19, 40), "Understood. Their quote doesn't include the driver app or telematics, so it's worth comparing the full setup. I can put both side by side for you.", 0.2),
        new("priya", T(19, 42), T(20, 20), "That would help me make the case internally.", 0.4),
        new("maya", T(20, 22), T(21, 10), "Great. What would a pilot need to look like for you to say yes?", 0.4),
        new("priya", T(21, 12), T(22, 10), "One depot, four weeks, and we track missed pickups against last quarter.", 0.5),
        new("tom", T(22, 12), T(22, 40), "And the security review has to be signed off first.", 0.1),
        new("maya", T(22, 42), T(23, 30), "Perfect. I'll send the security pack and a pilot plan by Friday, and Leo will set up a sandbox with your depot.", 0.6),
        new("priya", T(23, 32), T(24, 0), "Sounds good. Thanks, both.", 0.7),
    ];

    public static CallInsightsMoment[] Moments() =>
    [
        new(T(2, 22), CallInsightsMomentKind.Objection, "Missed three pickups and lost a customer"),
        new(T(5, 8), CallInsightsMomentKind.Objection, "Worried about another login for drivers"),
        new(T(7, 12), CallInsightsMomentKind.Question, "Asked about the telematics connection"),
        new(T(9, 2), CallInsightsMomentKind.Commitment, "Security review needed before next step"),
        new(T(15, 48), CallInsightsMomentKind.Competitor, "Routewise named as the alternative"),
        new(T(17, 52), CallInsightsMomentKind.Pricing, "Price felt higher than the other quote"),
        new(T(21, 12), CallInsightsMomentKind.Commitment, "Pilot: one depot, four weeks"),
    ];

    public static CallInsightsTopic[] Topics() =>
    [
        new("Current process", T(0, 40), T(5, 5)),
        new("Driver app", T(5, 8), T(7, 10)),
        new("Integrations", T(7, 12), T(9, 0)),
        new("Security", T(9, 2), T(10, 34)),
        new("Value", T(10, 36), T(12, 40)),
        new("Product demo", T(12, 42), T(15, 46)),
        new("Pricing", T(15, 48), T(20, 20)),
        new("Pilot plan", T(20, 22), T(23, 30)),
        new("Security", T(22, 12), T(22, 40)),
    ];

    public static CallInsightsNextStep[] NextSteps() =>
    [
        new("Send the security pack to Tom and Grace", "Maya", "Today", isDone: true),
        new("Side-by-side cost of the full setup against Routewise", "Maya", "Thu"),
        new("Pilot plan: one depot, four weeks, missed pickups tracked", "Maya", "Fri"),
        new("Sandbox with the north depot's routes", "Leo", "Fri"),
    ];
}
