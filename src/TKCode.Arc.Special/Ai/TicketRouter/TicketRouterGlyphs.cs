using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The 24 × 24 Lucide glyphs of <see cref="TicketRouter"/>: neutral channel glyphs (never a brand mark), the intents and
/// the sentiments. Glyphs Core does not ship are parsed once and cached by <see cref="ArcIcons.Parse"/>.
/// </summary>
internal static class TicketRouterGlyphs
{
    private const string Circle = "M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0";

    /// <summary>Lucide <c>life-buoy</c>, the help desk channel.</summary>
    public static Geometry LifeBuoy => ArcIcons.Parse(
        Circle + "M8 12a4 4 0 1 0 8 0a4 4 0 1 0-8 0M4.93 4.93l4.24 4.24M14.83 9.17l4.24-4.24M14.83 14.83l4.24 4.24M9.17 14.83l-4.24 4.24");

    /// <summary>Lucide <c>frown</c>, the negative sentiment.</summary>
    public static Geometry Frown => ArcIcons.Parse(Circle + "M16 16s-1.5-2-4-2-4 2-4 2M9 9h.01M15 9h.01");

    /// <summary>Lucide <c>meh</c>, the neutral sentiment.</summary>
    public static Geometry Meh => ArcIcons.Parse(Circle + "M8 15h8M9 9h.01M15 9h.01");

    public static Geometry Channel(TicketChannel channel) => channel switch
    {
        TicketChannel.Chat => ArcIcons.MessageCircle,
        TicketChannel.Messages => ArcIcons.MessageSquare,
        TicketChannel.HelpDesk => LifeBuoy,
        _ => ArcIcons.Mail,
    };

    public static Geometry Intent(TicketIntent intent) => intent switch
    {
        TicketIntent.Billing => ArcIcons.CreditCard,
        TicketIntent.Refund => ArcIcons.RotateCcw,
        TicketIntent.Bug => ArcIcons.Bug,
        TicketIntent.Feature => ArcIcons.Lightbulb,
        _ => ArcIcons.CircleHelp,
    };

    public static Geometry Sentiment(TicketSentiment sentiment) => sentiment switch
    {
        TicketSentiment.Positive => ArcIcons.Smile,
        TicketSentiment.Negative => Frown,
        _ => Meh,
    };
}
