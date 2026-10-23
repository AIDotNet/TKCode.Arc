using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An invented prompt (refund replies for a kite shop), versions, test cases, models and a run handler for the prompt studio demos.</summary>
internal static class PromptStudioDemoData
{
    public const string System =
        "You answer refund questions for Northwind Kites. Keep it friendly and under 100 words.\n\nApply this rule: {{refund_rule}}\n\n- Address {{buyer_name}} once by first name.\n- Mention ticket {{ticket_id}}.\n- Never invent shipping dates.\n- Sign as {{agent_sign}}.";

    public const string User = "Write the reply to the last message from {{buyer_name}} about ticket {{ticket_id}}.";

    public static string[] Variables { get; } = ["buyer_name", "ticket_id", "refund_rule", "agent_sign"];

    public static PromptVersion[] Versions()
    {
        var rui = new PromptAuthor { Name = "Rui Tanaka" };
        var lena = new PromptAuthor { Name = "Lena Brooks" };
        var omar = new PromptAuthor { Name = "Omar Haddad" };
        var now = DateTimeOffset.Now;
        return
        [
            new()
            {
                Id = "v4", Label = "v4", Message = "Name the rule before the apology", Author = rui, CreatedAt = now.AddDays(-2),
                System = System, User = User
            },
            new()
            {
                Id = "v3", Label = "v3", Message = "Trim to one paragraph", Author = lena, CreatedAt = now.AddDays(-8),
                System = System.Replace("Name the rule", "State the rule").Replace("under 100 words", "in one short paragraph").Replace("- Never invent shipping dates.\n", string.Empty),
                User = "Reply to {{buyer_name}} about ticket {{ticket_id}}."
            },
            new()
            {
                Id = "v2", Label = "v2", Message = "Add sign-off variable", Author = omar, CreatedAt = now.AddDays(-15),
                System = "You answer refund questions for Northwind Kites. Keep it friendly.\n\nApply this rule: {{refund_rule}}\n\n- Address {{buyer_name}} once.\n- Sign as {{agent_sign}}.",
                User = "Reply to {{buyer_name}}."
            },
            new()
            {
                Id = "v1", Label = "v1", Message = "Initial draft from help macros", Author = rui, CreatedAt = now.AddDays(-28),
                System = "You answer refund questions for Northwind Kites.\n\nApply this rule: {{refund_rule}}",
                User = "Reply to the customer."
            }
        ];
    }

    public static PromptTestCase[] TestCases() =>
    [
        Case("broken", "Broken spar", "Mira Okafor", "NK-3307", "Damaged parts are replaced free within 60 days."),
        Case("late", "Late parcel", "Tomas Lindqvist", "NK-3412", "Parcels over 7 days late get shipping refunded."),
        Case("policy", "Out of policy", "Ana Ferreira", "NK-2958", "Refunds close 45 days after delivery.")
    ];

    private static PromptTestCase Case(string id, string label, string buyer, string ticket, string rule)
    {
        var testCase = new PromptTestCase { Id = id, Label = label };
        testCase.Values["buyer_name"] = buyer;
        testCase.Values["ticket_id"] = ticket;
        testCase.Values["refund_rule"] = rule;
        return testCase;
    }

    public static AiModel[] Models() =>
    [
        Model("atlas-large", "Atlas Large", "Atlas L", 1.5m, 7m, "#7B4FD6", ModelEffort.Medium),
        Model("atlas-medium", "Atlas Medium", "Atlas M", 0.6m, 2.4m, "#2F6FEB", ModelEffort.High),
        Model("atlas-small", "Atlas Small", "Atlas S", 0.2m, 0.8m, "#2E9F6B", ModelEffort.Low),
        Model("comet-pro", "Comet Pro", "Comet", 1.0m, 4m, "#E8742A", ModelEffort.Medium),
        Model("nova-flash", "Nova Flash", "Nova", 0.1m, 0.4m, "#C0392B", ModelEffort.Low)
    ];

    private static AiModel Model(string id, string name, string shortName, decimal input, decimal output, string color, ModelEffort effort) => new()
    {
        Id = id, Name = name, ShortName = shortName, InputPrice = input, OutputPrice = output, IsSamplePrice = true,
        EffortLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max],
        DefaultEffort = effort,
        Mark = Geometry.Parse("M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z"),
        MarkColor = new SolidColorBrush(Color.Parse(color))
    };

    /// <summary>Writes a reply for the rendered prompt; the "Out of policy" case fails once, as an overloaded model would.</summary>
    public sealed class Runner
    {
        private bool _failedOnce;

        public async IAsyncEnumerable<string> Run(PromptRunRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(700, cancellationToken);
            if (request.TestCaseId == "policy" && !_failedOnce)
            {
                _failedOnce = true;
                throw new InvalidOperationException("The model is busy right now (503). Nothing was charged, so you can try again.");
            }

            var buyer = request.Values.GetValueOrDefault("buyer_name") is { Length: > 0 } b ? b.Split(' ')[0] : "there";
            var ticket = request.Values.GetValueOrDefault("ticket_id") is { Length: > 0 } t ? t : "your ticket";
            var rule = request.Values.GetValueOrDefault("refund_rule") is { Length: > 0 } r ? r : "our usual policy";
            var reply = $"Hi {buyer},\n\nThanks for writing about {ticket}. Here is how we handle it: {rule} I have started that for you now, and you will get a confirmation by email.\n\nNorthwind Kites Support";
            foreach (var word in reply.Split(' '))
            {
                await Task.Delay(35, cancellationToken);
                yield return word + " ";
            }
        }
    }
}
