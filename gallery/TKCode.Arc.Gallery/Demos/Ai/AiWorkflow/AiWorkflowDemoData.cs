namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The support flow the AI workflow demos share: a new email, a Claude Sonnet 5.5 step that labels it, a branch on the
/// label, then a chat message and an issue on the urgent path or a drafted reply and a ticket on the other one; and the
/// add step catalog. The models are real (<see cref="AiModelCatalog"/>); every person, company, address, key and number
/// is invented. Demo content only.
/// </summary>
internal static class AiWorkflowDemoData
{
    public const string Title = "Escalate urgent support email";

    public const string Description = "Runs on every new email to support@example.com";

    /// <summary>
    /// The app of the model steps: Anthropic, whose Claude models label, draft and summarize here. The mark is a neutral
    /// sparkle in the accent; no brand mark is drawn.
    /// </summary>
    public static AiWorkflowApp Claude { get; } =
        new(AiModelCatalog.Anthropic.Key, AiModelCatalog.Anthropic.Name, ArcIcons.Sparkles, isAccent: true);

    private static string Classifier => AiModelCatalog.ClaudeSonnet55.Name;

    private static string Writer => AiModelCatalog.ClaudeHaiku55.Name;

    /// <summary>The sample flow, a fresh copy on every read (a demo may change its copy).</summary>
    public static List<AiWorkflowStep> Steps =>
    [
        new()
        {
            Id = "trigger",
            Kind = AiWorkflowStepKind.Trigger,
            App = AiWorkflowApp.Mail,
            Title = "New email",
            Detail = "support@example.com",
            Activity = "Fetching the latest email",
            Duration = TimeSpan.FromMilliseconds(420),
            Output = new AiWorkflowEmailOutput
            {
                From = "Dana Whitfield",
                Address = "dana@contoso.example",
                Subject = "Checkout fails with error 502 since this morning",
                Preview = "Hi team, since about 9:00 every checkout on our store ends in a 502 error. Customers are writing to us and we are losing orders. Can someone look at this today?"
            }
        },
        new()
        {
            Id = "classify",
            Kind = AiWorkflowStepKind.Ai,
            App = Claude,
            Title = "Classify with " + Classifier,
            Detail = "Label urgency and topic",
            Activity = "Reading the email and choosing a label",
            Duration = TimeSpan.FromMilliseconds(1240),
            Output = new AiWorkflowLabelOutput
            {
                Label = "Urgent",
                Confidence = 0.94,
                Reason = "Checkout fails for every customer since this morning, which stops orders, and the sender asks for a fix today."
            }
        },
        new()
        {
            Id = "route",
            Kind = AiWorkflowStepKind.Branch,
            App = AiWorkflowApp.Branch,
            Title = "Route by label",
            Detail = "If the label is Urgent",
            Activity = "Checking the label",
            Duration = TimeSpan.FromMilliseconds(180),
            Output = new AiWorkflowRouteOutput { PathId = "urgent", Note = "Label is Urgent" },
            Paths =
            [
                new()
                {
                    Id = "urgent",
                    Label = "Urgent",
                    Steps =
                    [
                        new()
                        {
                            Id = "post",
                            Kind = AiWorkflowStepKind.Action,
                            App = AiWorkflowApp.Chat,
                            Title = "Post message",
                            Detail = "#support",
                            Activity = "Posting to #support",
                            Duration = TimeSpan.FromMilliseconds(560),
                            Output = new AiWorkflowMessageOutput
                            {
                                Channel = "#support",
                                Author = "Workflow bot",
                                Text = "Urgent: checkout fails with error 502 for every customer since 9:00. Dana Whitfield (Contoso) is waiting for an update."
                            }
                        },
                        new()
                        {
                            Id = "issue",
                            Kind = AiWorkflowStepKind.Action,
                            App = AiWorkflowApp.Tracker,
                            Title = "Create issue",
                            Detail = "Support team",
                            Activity = "Creating the issue",
                            Duration = TimeSpan.FromMilliseconds(700),
                            Output = new AiWorkflowRecordOutput
                            {
                                Id = "SUP-1042",
                                Title = "Checkout fails with error 502",
                                Meta = "Urgent, assigned to On call"
                            }
                        }
                    ]
                },
                new()
                {
                    Id = "other",
                    Label = "Everything else",
                    Steps =
                    [
                        new()
                        {
                            Id = "draft",
                            Kind = AiWorkflowStepKind.Ai,
                            App = Claude,
                            Title = "Draft a reply",
                            Detail = Writer,
                            Activity = "Writing a reply",
                            Duration = TimeSpan.FromMilliseconds(980),
                            Output = new AiWorkflowTextOutput
                            {
                                Text = "Thanks for writing in. We have your message and someone from the team will get back to you within one business day."
                            }
                        },
                        new()
                        {
                            Id = "ticket",
                            Kind = AiWorkflowStepKind.Action,
                            App = AiWorkflowApp.Helpdesk,
                            Title = "Open ticket",
                            Detail = "Normal priority",
                            Activity = "Opening the ticket",
                            Duration = TimeSpan.FromMilliseconds(520),
                            Output = new AiWorkflowRecordOutput { Id = "#4821", Title = "Question from Dana Whitfield", Meta = "Normal priority" }
                        }
                    ]
                }
            ]
        }
    ];

    /// <summary>The add step catalog (four triggers, two model steps, six actions and a branch), a fresh copy on every read.</summary>
    public static List<AiWorkflowCatalogItem> Catalog =>
    [
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Mail, Title = "New email", Detail = "support@example.com",
            Activity = "Fetching the latest email", Duration = TimeSpan.FromMilliseconds(420),
            Output = new AiWorkflowEmailOutput
            {
                From = "Dana Whitfield", Address = "dana@contoso.example", Subject = "Checkout fails with error 502 since this morning",
                Preview = "Hi team, since about 9:00 every checkout on our store ends in a 502 error."
            }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Schedule, Title = "On a schedule", Detail = "Weekdays at 9:00",
            Activity = "Waiting for 9:00", Duration = TimeSpan.FromMilliseconds(300),
            Output = new AiWorkflowTextOutput { Text = "Started at 9:00 on Monday." }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Payments, Title = "Payment failed", Detail = "Any customer",
            Activity = "Fetching the latest event", Duration = TimeSpan.FromMilliseconds(380),
            Output = new AiWorkflowRecordOutput { Id = "evt_8f2k", Title = "Card declined for Fabrikam", Meta = "Sample amount 120.00" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Webhook, Title = "Webhook received", Detail = "POST /hooks/support",
            Activity = "Waiting for a request", Duration = TimeSpan.FromMilliseconds(260),
            Output = new AiWorkflowTextOutput { Text = "{ \"type\": \"ticket.created\", \"id\": 4821 }" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Ai, App = Claude, Title = "Summarize with " + Classifier, Detail = "Three bullet points",
            Activity = "Summarizing", Duration = TimeSpan.FromMilliseconds(1100),
            Output = new AiWorkflowTextOutput { Text = "Checkout fails with a 502 for every customer. It started around 9:00. The sender asks for a fix today." }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Ai, App = Claude, Title = "Extract fields with " + Writer, Detail = "Order number, company",
            Activity = "Extracting fields", Duration = TimeSpan.FromMilliseconds(860),
            Output = new AiWorkflowRecordOutput { Id = "fields", Title = "Company: Contoso, order: none given", Meta = "2 of 2 fields found" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Chat, Title = "Post message", Detail = "#support",
            Activity = "Posting to #support", Duration = TimeSpan.FromMilliseconds(560),
            Output = new AiWorkflowMessageOutput { Channel = "#support", Author = "Workflow bot", Text = "New email from Dana Whitfield: checkout fails with error 502." }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Tracker, Title = "Create issue", Detail = "Support team",
            Activity = "Creating the issue", Duration = TimeSpan.FromMilliseconds(700),
            Output = new AiWorkflowRecordOutput { Id = "SUP-1043", Title = "Checkout fails with error 502", Meta = "Assigned to On call" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Docs, Title = "Create page", Detail = "Incident notes",
            Activity = "Creating the page", Duration = TimeSpan.FromMilliseconds(640),
            Output = new AiWorkflowRecordOutput { Id = "Incident notes", Title = "Checkout 502, morning of the incident", Meta = "In Support / Incidents" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Sheets, Title = "Add row", Detail = "Escalations log",
            Activity = "Adding a row", Duration = TimeSpan.FromMilliseconds(480),
            Output = new AiWorkflowRecordOutput { Id = "Row 128", Title = "Contoso, Urgent, checkout", Meta = "Escalations log" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Crm, Title = "Update contact", Detail = "Add a note",
            Activity = "Updating Dana Whitfield", Duration = TimeSpan.FromMilliseconds(520),
            Output = new AiWorkflowRecordOutput { Id = "Dana Whitfield", Title = "Note added: checkout incident", Meta = "Contoso" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Repo, Title = "Open issue", Detail = "web-store",
            Activity = "Opening the issue", Duration = TimeSpan.FromMilliseconds(620),
            Output = new AiWorkflowRecordOutput { Id = "#982", Title = "502 on checkout", Meta = "web-store" }
        },
        new()
        {
            Kind = AiWorkflowStepKind.Branch, App = AiWorkflowApp.Branch, Title = "Branch", Detail = "Split by a condition",
            Activity = "Checking the condition", Duration = TimeSpan.FromMilliseconds(180),
            Output = new AiWorkflowRouteOutput { PathId = "yes", Note = "Condition met" },
            Paths = [new() { Id = "yes", Label = "Yes" }, new() { Id = "no", Label = "No" }]
        }
    ];

    /// <summary>A simulation of the sample flow with its catalog (the code-behind demos use it; the XAML ones spell it out).</summary>
    public static AiWorkflowSimulation Simulation() => new()
    {
        Title = Title,
        Description = Description,
        Steps = Steps,
        Catalog = Catalog,
    };
}
