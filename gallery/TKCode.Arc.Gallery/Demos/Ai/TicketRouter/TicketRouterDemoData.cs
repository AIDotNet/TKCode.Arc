namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The inbox the ticket router demos share: four lanes, a stream of ten messages from made-up customers with the answers
/// the simulated classifier gives (two of them below the review threshold) and the tickets already in the lanes. The
/// classifier is a real model (<see cref="AiModelCatalog"/>); people, companies and numbers are invented. Demo content only.
/// </summary>
internal static class TicketRouterDemoData
{
    /// <summary>The classifier named in the header: a real model.</summary>
    public static string Model => AiModelCatalog.MistralSmall4.Name;

    /// <summary>The model named by the demo with its own classifier.</summary>
    public static string YourModel => AiModelCatalog.Ministral38B.Name;

    /// <summary>The lanes: Billing, Engineering, Success and Needs review, with invented assignees and earlier counts.</summary>
    public static IList<TicketLane> Lanes { get; } =
    [
        new TicketLane(TicketRouter.BillingLaneId, "Billing", "Andre Williams", "Priya Nair") { Count = 9 },
        new TicketLane(TicketRouter.EngineeringLaneId, "Engineering", "Mei Tanaka", "Jonas Berg", "Ada Okafor") { Count = 14 },
        new TicketLane(TicketRouter.SuccessLaneId, "Success", "Sofia Alvarez", "Liam Chen") { Count = 6 },
        new TicketLane(TicketRouter.ReviewLaneId, "Needs review") { IsReview = true },
    ];

    /// <summary>The stream, in arrival order, each with the simulated classifier's answer.</summary>
    public static IList<TicketMessage> Tickets { get; } =
    [
        Message("t-101", TicketChannel.Email, "Hannah Brooks", "Lumen Bakery", "Charged twice for the March invoice",
            "Hi, our card shows two charges of $49 on the 3rd and we only have one workspace.", "now",
            TicketIntent.Billing, TicketSentiment.Negative, 0.94),
        Message("t-102", TicketChannel.Chat, "Kofi Mensah", "Harbor Labs", "Export to CSV times out on large boards",
            "Every export over about 5,000 rows spins and then fails with a timeout.", "1m",
            TicketIntent.Bug, TicketSentiment.Negative, 0.91),
        Message("t-103", TicketChannel.Messages, "Elena Petrova", "Northgate Studio", "Could we get dark mode in the editor?",
            "The team works late and the bright canvas is rough. Any plans?", "2m",
            TicketIntent.Feature, TicketSentiment.Positive, 0.88),
        Message("t-104", TicketChannel.HelpDesk, "Marcus Reed", "Pinewood Dental", "Not sure this is the right place",
            "We switched plans last week and some things look different now, can someone check?", "3m",
            TicketIntent.Billing, TicketSentiment.Neutral, 0.54),
        Message("t-105", TicketChannel.Email, "Yuki Sato", "Orbit Freight", "Refund for the unused annual seats",
            "We removed eight seats in January. Can the unused months be refunded?", "4m",
            TicketIntent.Refund, TicketSentiment.Neutral, 0.9),
        Message("t-106", TicketChannel.Chat, "Grace Liu", "Fernway Health", "SSO login loops back to the sign-in page",
            "After the identity provider redirect we land on sign-in again, every browser.", "5m",
            TicketIntent.Bug, TicketSentiment.Negative, 0.96),
        Message("t-107", TicketChannel.Messages, "Omar Haddad", "Quill & Co", "How do I add a guest to one project?",
            "A freelancer needs access to a single project only, not the whole workspace.", "6m",
            TicketIntent.Question, TicketSentiment.Neutral, 0.83),
        Message("t-108", TicketChannel.HelpDesk, "Nora Lindqvist", "Brightside Schools", "Love the new reports, one idea",
            "The weekly report is great. Being able to schedule it per team would be even better.", "7m",
            TicketIntent.Feature, TicketSentiment.Positive, 0.62),
        Message("t-109", TicketChannel.Email, "Diego Ramos", "Cobalt Coffee", "Invoice needs our VAT number",
            "Our accountant needs the VAT number on the invoices from now on.", "8m",
            TicketIntent.Billing, TicketSentiment.Neutral, 0.89),
        Message("t-110", TicketChannel.Chat, "Amara Nwosu", "Tidewater Co-op", "Webhooks stopped firing overnight",
            "No events since about 2am. Nothing changed on our side as far as we know.", "9m",
            TicketIntent.Bug, TicketSentiment.Negative, 0.93),
    ];

    /// <summary>The tickets already in the lanes when the inbox opens, newest first.</summary>
    public static IList<RoutedTicket> Routed { get; } = CreateRouted();

    private static RoutedTicket[] CreateRouted()
    {
        var earlier = new[]
        {
            Message("t-091", TicketChannel.Email, "Ravi Kapoor", "Summit Outdoors", "Upgrade to the team plan",
                "We'd like to move from monthly to the yearly team plan.", "12m", TicketIntent.Billing, TicketSentiment.Positive, 0.92),
            Message("t-092", TicketChannel.Chat, "Lea Fischer", "Atelier Nord", "Images fail to upload from iPad",
                "Uploads stop at 90% from the tablet app.", "14m", TicketIntent.Bug, TicketSentiment.Negative, 0.9),
            Message("t-093", TicketChannel.Messages, "Sam Carter", "Maple Analytics", "Keyboard shortcuts for comments",
                "Would love a shortcut to jump to the next comment.", "15m", TicketIntent.Feature, TicketSentiment.Positive, 0.86),
            Message("t-094", TicketChannel.HelpDesk, "Ines Duarte", "Velo Repair", "Question about our account",
                "Something changed with our account, not sure what.", "17m", TicketIntent.Question, TicketSentiment.Neutral, 0.48),
            Message("t-095", TicketChannel.Email, "Tom Becker", "Lakeside Clinic", "Receipt for last payment",
                "Could you send the receipt for the last payment again?", "19m", TicketIntent.Billing, TicketSentiment.Neutral, 0.91),
            Message("t-096", TicketChannel.Chat, "Zara Ahmed", "Kite Studio", "Search misses new documents",
                "Documents created today do not show up in search.", "21m", TicketIntent.Bug, TicketSentiment.Negative, 0.88),
        };
        return
        [
            new RoutedTicket(earlier[0], earlier[0].Sample, TicketRouter.BillingLaneId, "Priya Nair"),
            new RoutedTicket(earlier[1], earlier[1].Sample, TicketRouter.EngineeringLaneId, "Jonas Berg"),
            new RoutedTicket(earlier[2], earlier[2].Sample, TicketRouter.SuccessLaneId, "Liam Chen"),
            new RoutedTicket(earlier[3], earlier[3].Sample, TicketRouter.ReviewLaneId, null, TicketRouter.SuccessLaneId),
            new RoutedTicket(earlier[4], earlier[4].Sample, TicketRouter.BillingLaneId, "Andre Williams"),
            new RoutedTicket(earlier[5], earlier[5].Sample, TicketRouter.EngineeringLaneId, "Mei Tanaka"),
        ];
    }

    private static TicketMessage Message(string id, TicketChannel channel, string customer, string company, string subject,
        string preview, string receivedAt, TicketIntent intent, TicketSentiment sentiment, double confidence) =>
        new(id, channel, customer, company, subject, preview)
        {
            ReceivedAt = receivedAt,
            Sample = new TicketClassification(intent, sentiment, confidence),
        };

    /// <summary>A new simulation of the inbox (tests and code use it; the XAML demos spell it out).</summary>
    public static TicketRouterSimulation Simulation() => new()
    {
        Model = Model,
        Lanes = Lanes,
        Tickets = Tickets,
        Routed = Routed,
    };
}
