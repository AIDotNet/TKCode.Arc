using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The side panel of a <see cref="SupportConversation"/>: the customer (portrait, name, email, company), their facts
/// (plan, location, customer since, value, earlier conversations), tags, and the team's internal notes, newest first.
/// </summary>
internal sealed class SupportConversationPanel : StackPanel
{
    private readonly SupportConversation _owner;
    private readonly Avatar _avatar;
    private readonly TextBlock _name;
    private readonly TextBlock _email;
    private readonly TextBlock _company;
    private readonly Grid _facts;
    private readonly WrapPanel _tags;
    private readonly StackPanel _notes;
    private readonly TextBlock _notesHeading;

    public SupportConversationPanel(SupportConversation owner)
    {
        _owner = owner;
        Spacing = 18;
        Margin = new Thickness(18);
        _avatar = new Avatar { Size = AvatarSize.Large, HorizontalAlignment = HorizontalAlignment.Left };
        _name = SupportKit.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeBase, FontWeight.SemiBold, wrap: true);
        _email = SupportKit.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true);
        _company = SupportKit.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, wrap: true);
        Children.Add(new StackPanel { Spacing = 4, Children = { _avatar, _name, _email, _company } });
        _name.Margin = new Thickness(0, 6, 0, 0);

        _facts = SupportKit.Grid("Auto,*", columnSpacing: 12, rowSpacing: 8);
        Children.Add(Section("Details", _facts));
        _tags = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        Children.Add(Section("Tags", _tags));

        _notes = new StackPanel { Spacing = 8 };
        _notesHeading = Heading("Internal notes");
        Children.Add(new StackPanel { Spacing = 8, Children = { _notesHeading, _notes } });
        AutomationProperties.SetName(this, "Customer details");
    }

    public IReadOnlyList<Control> NoteCards => _notes.Children.ToList();

    public void Refresh()
    {
        var customer = _owner.EffectiveCustomer;
        _avatar.DisplayName = customer?.Name;
        _avatar.Source = customer?.Avatar is { } url ? ImageSource.Parse(url) : null;
        _name.Text = customer?.Name ?? "Customer";
        _email.Text = customer?.Email;
        _email.IsVisible = customer?.Email is not null;
        _company.Text = customer?.Company;
        _company.IsVisible = customer?.Company is not null;

        _facts.Children.Clear();
        _facts.RowDefinitions.Clear();
        if (customer is not null)
        {
            AddFact("Plan", customer.Plan);
            AddFact("Location", customer.Location);
            AddFact("Customer since", customer.CustomerSince?.ToString("MMM yyyy", SupportConversation.Culture));
            AddFact("Lifetime value", customer.LifetimeValue);
            AddFact("Conversations", customer.PastConversations > 0 ? customer.PastConversations.ToString(SupportConversation.Culture) : null);
        }
        ((Control)_facts.Parent!).IsVisible = _facts.Children.Count > 0;

        _tags.Children.Clear();
        foreach (var tag in customer?.Tags ?? Array.Empty<string>())
        {
            _tags.Children.Add(SupportKit.Pill(tag, ArcDesignTokenKind.Surface, ArcDesignTokenKind.TextSecondary, ArcIcons.Tag));
        }
        ((Control)_tags.Parent!).IsVisible = _tags.Children.Count > 0;

        _notes.Children.Clear();
        foreach (var note in _owner.Notes.OrderByDescending(n => n.CreatedAt))
        {
            var head = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    SupportKit.Glyph(SupportKit.StickyNote, 13, ArcDesignTokenKind.Warning),
                    SupportKit.Text(note.Author, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold),
                    SupportKit.Text(SupportKit.Ago(note.CreatedAt, _owner.Clock(), SupportConversation.Culture), ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS),
                },
            };
            var card = SupportKit.Panel(new StackPanel
            {
                Spacing = 4,
                Children = { head, SupportKit.Text(note.Text, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, wrap: true) },
            }, ArcDesignTokenKind.RadiusStep12, ArcDesignTokenKind.WarningSubtle, new Thickness(12, 10), edge: false);
            AutomationProperties.SetName(card, $"Note by {note.Author}: {note.Text}");
            _notes.Children.Add(card);
        }
        if (_notes.Children.Count == 0)
        {
            _notes.Children.Add(SupportKit.Text("No notes yet. Switch the composer to Internal note to leave one for your team.",
                ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, wrap: true));
        }
        _notesHeading.Text = _owner.Notes.Count == 0 ? "Internal notes" : $"Internal notes · {_owner.Notes.Count}";
    }

    private void AddFact(string label, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }
        var row = _facts.RowDefinitions.Count;
        _facts.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _facts.Children.Add(SupportKit.Text(label, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS).At(0, row));
        var text = SupportKit.Text(value, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS, wrap: true);
        text.HorizontalAlignment = HorizontalAlignment.Right;
        text.TextAlignment = TextAlignment.Right;
        _facts.Children.Add(text.At(1, row));
    }

    private static TextBlock Heading(string text)
    {
        var heading = SupportKit.Text(text, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.SemiBold);
        AutomationProperties.SetHeadingLevel(heading, 3);
        return heading;
    }

    private static StackPanel Section(string title, Control content) =>
        new() { Spacing = 8, Children = { Heading(title), content } };
}

/// <summary>The sample conversation of a <see cref="SupportConversation"/> with no messages when it first loads.</summary>
internal static class SupportConversationSamples
{
    public static void Fill(SupportConversation view, DateTimeOffset now)
    {
        var agent = view.AgentId;
        if (view.Participants.Count == 0)
        {
            view.Participants.Add(new ChatParticipant("cust", "Mara Lindqvist"));
            view.Participants.Add(new ChatParticipant(agent, view.AgentName));
            view.Participants.Add(new ChatParticipant("kofi", "Kofi Mensah"));
        }
        view.Customer ??= new SupportCustomer
        {
            Name = "Mara Lindqvist",
            Email = "mara@harbourlight.example",
            Company = "Harbourlight Studio",
            Plan = "Team, 12 seats",
            Location = "Gothenburg, SE",
            CustomerSince = now.AddMonths(-19),
            LifetimeValue = "$4,280",
            PastConversations = 6,
            Tags = ["Annual plan", "Design agency"],
        };
        view.Subject ??= "Charged twice for the March invoice";
        view.Messages.Add(new ChatMessage("m1", "cust", "Hi! I was charged twice for March. Both charges show on my card from yesterday.", now.AddMinutes(-42)));
        view.Messages.Add(new ChatMessage("m2", "cust", "Invoice number is **INV-20931** if that helps.", now.AddMinutes(-41)));
        view.Messages.Add(new ChatMessage("m3", "kofi", "Thanks Mara, I am looking into it now.", now.AddMinutes(-30)) { Status = ChatMessageStatus.Read });
        view.Messages.Add(new ChatMessage("m4", "cust", "Great, thank you. Will the refund go back to the same card?", now.AddMinutes(-6)));
        if (view.Notes.Count == 0)
        {
            view.Notes.Add(new SupportNote("n1", "Kofi Mensah", "Payment provider shows a retry after a timeout. The second charge is safe to refund.", now.AddMinutes(-25)));
        }
        if (view.CannedReplies.Count == 0)
        {
            view.CannedReplies.Add(new SupportCannedReply("Refund issued", "Hi {name}, I have refunded the duplicate charge. It should reach your card within 5 to 10 business days."));
            view.CannedReplies.Add(new SupportCannedReply("Need more details", "Thanks {name}. Could you send the last four digits of the card and the date of the charge?"));
            view.CannedReplies.Add(new SupportCannedReply("Closing check-in", "Hi {name}, is there anything else I can help with? If not, I will close this conversation."));
        }
    }
}
