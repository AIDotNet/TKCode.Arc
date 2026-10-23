using Avalonia.Collections;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for the Prompt studio run parts (spec section 10: a refund-reply prompt for "Northwind Kites", a
/// made-up kite shop). Nothing here comes from Arc's preview.
/// </summary>
internal static class PromptRunSamples
{
    public static readonly string[] Variables = ["buyer_name", "ticket_id", "refund_rule", "agent_sign"];

    public const string Reply =
        "Hi Mira,\n\n" +
        "Thanks for the photos on ticket NK-3307. A cracked spar is covered by our parts promise, so a replacement set " +
        "leaves the workshop today at no cost to you. You do not need to send the broken one back.\n\n" +
        "Damaged parts are replaced free within 60 days, and your kite arrived three weeks ago, so you are well inside " +
        "that window. The tracking link follows by email once the courier scans the parcel.\n\n" +
        "Northwind Kites Support";

    public const string Error = "The model is busy right now (503). Nothing was charged, so you can try again.";

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

    /// <summary>The values of the first case (the fourth variable, agent_sign, is left empty).</summary>
    public static AvaloniaDictionary<string, string> Values()
    {
        var values = new AvaloniaDictionary<string, string>();
        foreach (var pair in Cases()[0].Values)
        {
            values[pair.Key] = pair.Value;
        }
        return values;
    }
}
