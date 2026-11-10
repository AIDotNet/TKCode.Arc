namespace TKCode.Arc;

/// <summary>The sample team, articles and history of a <see cref="SupportWidget"/> without articles (an invented booking app).</summary>
internal static class SupportWidgetSamples
{
    public static readonly string[] Replies =
    [
        "Thanks for the message! I am looking into it now and will get back to you in a moment.",
        "Got it. Could you tell me which device you are using? That helps me reproduce it.",
        "That should be fixed now. Let me know if anything still looks off.",
    ];

    public static void Fill(SupportWidget widget, DateTimeOffset now)
    {
        if (widget.Agents.Count == 0)
        {
            widget.Agents.Add(new ChatParticipant("ines", "Ines Duarte"));
            widget.Agents.Add(new ChatParticipant("kofi", "Kofi Mensah"));
            widget.Agents.Add(new ChatParticipant("lena", "Lena Park"));
        }
        widget.Articles.AddRange(
        [
            new("reschedule", "Move or cancel a booking", "Change the time of a booking up to two hours before it starts.",
                "Open **Bookings**, pick the booking and select **Reschedule** or **Cancel**.\n\nYou can change a booking up to two hours before it starts. After that, contact the venue."),
            new("refunds", "When will I get my refund?", "Refunds reach your card within 5 to 10 business days.",
                "Refunds go back to the card you paid with.\n\n- Cards: 5 to 10 business days\n- Wallet credit: right away"),
            new("group", "Book for a group", "Add up to eight guests to one booking and split the payment.",
                "Select **Add guests** while booking. Each guest gets their own confirmation.\n\nTo split the cost, turn on **Split payment** and enter each guest's email."),
            new("account", "Change your email or password", "Update your sign-in details from Account settings.",
                "Open **Account → Sign-in** to change your email or password. We send a confirmation to the new address."),
            new("notifications", "Turn reminders on or off", "Choose which reminders you get by email or on your phone.",
                "Open **Account → Notifications** and pick the reminders you want."),
        ]);
        if (widget.Conversations.Count == 0)
        {
            var earlier = new SupportWidgetConversation("c-earlier", "Refund for a cancelled class");
            earlier.Messages.Add(new ChatMessage("e1", widget.VisitorId, "Hi, my yoga class was cancelled. Will I be refunded?", now.AddDays(-3)));
            earlier.Messages.Add(new ChatMessage("e2", "kofi", "Yes! The refund is on its way and should arrive within a week.", now.AddDays(-3).AddMinutes(4)));
            widget.Conversations.Add(earlier);
        }
    }
}
