using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for <see cref="PromptStudio"/> (spec section 10): the "Refund reply" prompt of "Northwind Kites", a
/// made-up kite shop; four versions by made-up people with a few word changes between them; three test cases; five
/// made-up models with geometric marks and sample prices; and a scripted run handler that writes a reply per test case.
/// Nothing here comes from Arc's preview. The Gallery copies this file.
/// </summary>
internal static class PromptStudioSamples
{
    /// <summary>The moment the relative times are measured from.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public const string Title = "Refund reply";

    public static readonly string[] Variables = ["buyer_name", "ticket_id", "refund_rule", "agent_sign"];

    public const string SystemV4 =
        "You answer refund questions for Northwind Kites. Keep it friendly and under 100 words.\n\n" +
        "Apply this rule: {{refund_rule}}\n\n" +
        "- Address {{buyer_name}} once by first name.\n" +
        "- Mention ticket {{ticket_id}}.\n" +
        "- Never invent shipping dates.\n" +
        "- Sign as {{agent_sign}}.";

    public const string UserV4 = "Write the reply to the last message from {{buyer_name}} about ticket {{ticket_id}}.";

    public const string SystemV3 =
        "You answer refund questions for Northwind Kites. Keep it friendly and under 120 words.\n\n" +
        "Apply this rule: {{refund_rule}}\n\n" +
        "- Address {{buyer_name}} once by first name.\n" +
        "- Mention ticket {{ticket_id}}.\n" +
        "- Sign as {{agent_sign}}.";

    public const string UserV3 = "Write a reply to the last message from {{buyer_name}}.";

    public const string SystemV2 =
        "You answer refund questions for Northwind Kites. Keep it short and friendly.\n\n" +
        "Apply this rule: {{refund_rule}}\n\n" +
        "- Greet {{buyer_name}} by first name.\n" +
        "- Mention ticket {{ticket_id}}.\n" +
        "- Sign as {{agent_sign}}.";

    public const string UserV2 = "Write a reply to {{buyer_name}}.";

    public const string SystemV1 =
        "You answer refund questions for a kite shop. Be polite.\n\n" +
        "Policy: {{refund_rule}}\n\n" +
        "- Greet {{buyer_name}}.\n" +
        "- Mention ticket {{ticket_id}}.";

    public const string UserV1 = "Reply to {{buyer_name}}.";

    public static PromptVersion[] Versions()
    {
        var rui = new PromptAuthor { Name = "Rui Tanaka" };
        return
        [
            new() { Id = "v4", Label = "v4", Message = "Name the rule before the apology", Author = rui, CreatedAt = Now.AddDays(-2), System = SystemV4, User = UserV4 },
            new() { Id = "v3", Label = "v3", Message = "Trim to one paragraph", Author = new PromptAuthor { Name = "Lena Brooks" }, CreatedAt = Now.AddDays(-9), System = SystemV3, User = UserV3 },
            new() { Id = "v2", Label = "v2", Message = "Add sign-off variable", Author = new PromptAuthor { Name = "Omar Haddad" }, CreatedAt = Now.AddDays(-16), System = SystemV2, User = UserV2 },
            new() { Id = "v1", Label = "v1", Message = "Initial draft from help macros", Author = rui, CreatedAt = Now.AddDays(-29), System = SystemV1, User = UserV1 }
        ];
    }

    public static PromptTestCase[] Cases() =>
    [
        Case("spar", "Broken spar", ("buyer_name", "Mira Okafor"), ("ticket_id", "NK-3307"),
            ("refund_rule", "Damaged parts are replaced free within 60 days.")),
        Case("late", "Late parcel", ("buyer_name", "Tomas Lindqvist"), ("ticket_id", "NK-3412"),
            ("refund_rule", "Parcels over 7 days late get shipping refunded.")),
        Case("policy", "Out of policy", ("buyer_name", "Ana Ferreira"), ("ticket_id", "NK-2958"),
            ("refund_rule", "Refunds close 45 days after delivery."))
    ];

    public static PromptTestCase Case(string id, string label, params (string Name, string Value)[] values)
    {
        var testCase = new PromptTestCase { Id = id, Label = label };
        foreach (var (name, value) in values)
        {
            testCase.Values[name] = value;
        }
        return testCase;
    }

    private static readonly ModelEffort[] AllLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
    private static readonly ModelEffort[] ThreeLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];

    // Invented marks on the 24 grid: a circle, a diamond, a triangle, a hexagon and a four-point star.
    private static readonly Geometry Circle = Geometry.Parse("M12 3 A9 9 0 1 1 11.99 3 Z");
    private static readonly Geometry Diamond = Geometry.Parse("M12 2 L22 12 L12 22 L2 12 Z");
    private static readonly Geometry Triangle = Geometry.Parse("M12 3 L22 20 L2 20 Z");
    private static readonly Geometry Hexagon = Geometry.Parse("M12 2 L21 7 L21 17 L12 22 L3 17 L3 7 Z");
    private static readonly Geometry Star = Geometry.Parse("M12 2 L14.5 9.5 L22 12 L14.5 14.5 L12 22 L9.5 14.5 L2 12 L9.5 9.5 Z");

    /// <summary>Five made-up models; every price is a sample. The second ("Atlas Medium", High by default) is the usual pick.</summary>
    public static AiModel[] Models() =>
    [
        new()
        {
            Id = "atlas-large", Name = "Atlas Large", Mark = Circle, MarkColor = new SolidColorBrush(Color.Parse("#C2410C")),
            InputPrice = 6m, OutputPrice = 30m, IsSamplePrice = true, EffortLevels = AllLevels, DefaultEffort = ModelEffort.High
        },
        new()
        {
            Id = "atlas-medium", Name = "Atlas Medium", Mark = Diamond, MarkColor = new SolidColorBrush(Color.Parse("#C2410C")),
            InputPrice = 1.5m, OutputPrice = 7m, IsSamplePrice = true, EffortLevels = AllLevels, DefaultEffort = ModelEffort.High
        },
        new()
        {
            Id = "atlas-small", Name = "Atlas Small", Mark = Triangle, MarkColor = new SolidColorBrush(Color.Parse("#C2410C")),
            InputPrice = 0.2m, OutputPrice = 0.8m, IsSamplePrice = true, EffortLevels = ThreeLevels, DefaultEffort = ModelEffort.Medium
        },
        new()
        {
            Id = "comet-pro", Name = "Comet Pro", Mark = Hexagon, InputPrice = 2.5m, OutputPrice = 12m, IsSamplePrice = true,
            EffortLevels = ThreeLevels, DefaultEffort = ModelEffort.Low
        },
        new()
        {
            Id = "nova-flash", Name = "Nova Flash", Mark = Star, MarkColor = new SolidColorBrush(Color.Parse("#2563EB")),
            InputPrice = 0.1m, OutputPrice = 0.4m, IsSamplePrice = true
        }
    ];

    /// <summary>The reply the scripted handler writes for each test case.</summary>
    public static string ReplyFor(string? testCaseId) => testCaseId switch
    {
        "late" =>
            "Hi Tomas,\n\n" +
            "Sorry that ticket NK-3412 is taking so long. Your parcel is now more than a week late, so we have refunded the " +
            "shipping fee to your card; it shows up within a few days.\n\n" +
            "The kite itself is still on its way, and we will write again as soon as the courier scans it.\n\n" +
            "Northwind Kites Support",
        "policy" =>
            "Hi Ana,\n\n" +
            "Thanks for writing about ticket NK-2958. Refunds close 45 days after delivery, and your order arrived a little " +
            "before that, so we cannot refund it.\n\n" +
            "If a part fails, we can still repair it at cost; reply to this message and we will send the details.\n\n" +
            "Northwind Kites Support",
        _ =>
            "Hi Mira,\n\n" +
            "Thanks for the photos on ticket NK-3307. A cracked spar is covered by our parts promise, so a replacement set " +
            "leaves the workshop today at no cost to you. You do not need to send the broken one back.\n\n" +
            "Damaged parts are replaced free within 60 days, and your kite arrived three weeks ago, so you are well inside " +
            "that window.\n\n" +
            "Northwind Kites Support"
    };

    /// <summary>The error the scripted handler throws on the first run of the "Out of policy" case.</summary>
    public const string BusyError = "The model is busy right now (503). Nothing was charged, so you can try again.";

    /// <summary>
    /// A run handler for demos: it thinks for a moment, then writes the test case's reply a few words at a time. The first
    /// run of the "Out of policy" case fails with <see cref="BusyError"/> (to show Retry).
    /// </summary>
    public static PromptRunHandler ScriptedHandler(Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= static (time, token) => Task.Delay(time, token);
        var failed = false;
        return Run;

        async IAsyncEnumerable<string> Run(PromptRunRequest request, [EnumeratorCancellation] CancellationToken token)
        {
            await delay(TimeSpan.FromMilliseconds(500 + 150 * (int)request.Effort), token);
            if (request.TestCaseId == "policy" && !failed)
            {
                failed = true;
                throw new InvalidOperationException(BusyError);
            }
            foreach (var chunk in Words(ReplyFor(request.TestCaseId), 5))
            {
                yield return chunk;
                await delay(TimeSpan.FromMilliseconds(50), token);
            }
        }
    }

    /// <summary><paramref name="text"/> in pieces of <paramref name="count"/> words, each with the spaces after it.</summary>
    public static IEnumerable<string> Words(string text, int count)
    {
        var pieces = System.Text.RegularExpressions.Regex.Matches(text, @"\S+\s*");
        for (var i = 0; i < pieces.Count; i += count)
        {
            var end = Math.Min(pieces.Count, i + count);
            yield return string.Concat(Enumerable.Range(i, end - i).Select(j => pieces[j].Value));
        }
    }

    /// <summary>A studio with the sample prompt: versions (v4 open and in production), variables, test cases and models.</summary>
    public static PromptStudio Studio(Action<PromptStudio>? configure = null)
    {
        var studio = new PromptStudio { Title = Title, Now = Now };
        foreach (var version in Versions())
        {
            studio.Versions.Add(version);
        }
        foreach (var name in Variables)
        {
            studio.Variables.Add(name);
        }
        foreach (var testCase in Cases())
        {
            studio.TestCases.Add(testCase);
        }
        var models = Models();
        foreach (var model in models)
        {
            studio.Models.Add(model);
        }
        studio.SelectedModel = models[1];
        configure?.Invoke(studio);
        return studio;
    }
}
