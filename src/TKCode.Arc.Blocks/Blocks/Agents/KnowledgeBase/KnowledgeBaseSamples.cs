namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="KnowledgeBase"/>: the support library of Kestrel Lane, a made-up bicycle maker.
/// Four documents are indexed, one waits in the queue (the built-in pipeline indexes it as soon as the card is shown)
/// and one failed. The texts are short excerpts, so every chunk count is real.
/// </summary>
public static class KnowledgeBaseSamples
{
    /// <summary>The sample assistant's name: Pip.</summary>
    public const string AssistantName = "Pip";

    /// <summary>The day the samples were last indexed: 9 November 2026.</summary>
    public static readonly DateTimeOffset Today = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A question that finds passages in three documents.</summary>
    public const string SampleQuery = "refund for a damaged battery";

    private const string Returns = """
        Returns and refunds

        You can return any bike, part or accessory within 30 days of delivery. Items must be unused, with the original tags and packaging. Bikes that have been ridden outdoors count as used, but a short test ride on a clean indoor floor is fine.

        To start a return, open your order in the account page and choose Return an item. We email a prepaid label within one business day. Pack the bike in its original box with the pedals removed and the handlebar turned sideways.

        Refunds go back to the original payment method within 5 business days after the item reaches our warehouse. Shipping costs are refunded only when the item arrived damaged or was not what you ordered. Gift cards are refunded as store credit.

        A damaged delivery should be reported within 7 days, with photos of the box and the damage. We then send a replacement part or a full refund, whichever you prefer, and you keep the damaged item until the courier collects it.

        Custom builds, worn tyres, used saddles and opened chain lube cannot be returned. Electric bike batteries can only be returned unopened, because a battery that has been charged must go back through a safety check.
        """;

    private const string Warranty = """
        Warranty terms

        Every Kestrel Lane frame is covered for life against defects in materials and workmanship for the first owner. Forks, wheels and paint are covered for two years. Wear parts such as chains, brake pads, cassettes and tyres are not covered.

        Electric drive systems carry a two-year warranty. The battery is covered for two years or 500 full charge cycles, whichever comes first, and it must keep at least 70 percent of its rated capacity in that time. A battery that drops below that level is replaced free of charge.

        The warranty does not cover damage from crashes, jumps, racing, commercial use, rental, or changes to the frame. Water damage to the battery or the motor after riding through deep water or washing with a pressure washer is also not covered.

        To make a claim, send the serial number, a photo of the part and a short description to the support team. Most claims are decided within three business days. Approved parts ship for free, and a partner shop can fit them at no cost to you.
        """;

    private const string Assembly = """
        Assembly guide: Coast 7

        The Coast 7 arrives 90 percent assembled. You need a 4 mm and a 5 mm hex key, a 15 mm pedal wrench and about twenty minutes. Unpack the bike next to the box and keep the packaging until you are sure you will keep the bike.

        Fit the handlebar first. Remove the four stem bolts, place the bar in the clamp with the cables running forward, and tighten the bolts in a cross pattern to 5 Nm. Check that the gap above and below the bar is the same.

        Pedals are marked L and R. The right pedal threads on clockwise, the left pedal anticlockwise. Grease the threads and tighten them firmly with the pedal wrench.

        Set the saddle height so your leg is almost straight with the heel on the pedal at its lowest point. Never raise the seat post above the minimum insert mark. Pump the tyres to the pressure printed on their sides, between 3.5 and 4.5 bar.

        Before the first ride, squeeze both brakes hard and push the bike forward: the wheels must not turn. If a brake feels soft, see the brake section of the service guide or visit a partner shop.
        """;

    private const string Shipping = """
        Shipping FAQ

        Orders placed before 2 pm on a business day leave our warehouse the same day. Standard delivery takes 3 to 5 business days and is free above 150 euros. Express delivery takes one business day and costs 19 euros.

        Bikes ship in a large box and need a signature on delivery. If nobody is home, the courier leaves a card and tries again the next business day. After three attempts the bike returns to the warehouse and we refund the order minus the shipping cost.

        Electric bikes ship by road only, because their batteries cannot travel by air. Delivery to islands can take two extra days.

        You get a tracking link by email as soon as the label is printed. Tracking can take a few hours to show the first scan.
        """;

    private const string Battery = """
        Battery care

        Charge the battery at room temperature, between 10 and 30 degrees. Charging a cold battery straight after a winter ride shortens its life; let it warm up indoors for an hour first.

        For storage longer than a month, leave the battery at 40 to 60 percent and check it every eight weeks. Never store a battery fully empty: below a certain voltage it cannot be charged again and is not covered by the warranty.

        Clean the contacts with a dry cloth. Do not open the battery case or use a charger from another brand. If the battery is dropped, swells, smells or gets hot while resting, stop using it and contact support for a free safety check.

        A damaged battery must never be posted. Support books a specialist courier to collect it, and a refund or replacement follows the warranty terms.
        """;

    /// <summary>The six sample documents, newest first. Indexed ones carry their chunks; Battery care is queued.</summary>
    public static IReadOnlyList<KnowledgeDocument> Documents()
    {
        return
        [
            Indexed("returns", "Returns and refunds.pdf", KnowledgeDocumentKind.Pdf, 188_416, Returns, Today.AddDays(-1)),
            Indexed("warranty", "Warranty terms.md", KnowledgeDocumentKind.Markdown, 11_878, Warranty, Today.AddDays(-3)),
            Indexed("assembly", "Assembly guide - Coast 7.pdf", KnowledgeDocumentKind.Pdf, 2_411_724, Assembly, Today.AddDays(-6)),
            Indexed("shipping", "Shipping FAQ.md", KnowledgeDocumentKind.Markdown, 8_602, Shipping, Today.AddDays(-12)),
            new KnowledgeDocument
            {
                Id = "battery",
                Name = "Battery care.txt",
                Kind = KnowledgeDocumentKind.Text,
                SizeBytes = 6_144,
                Text = Battery,
                Status = KnowledgeDocumentStatus.Queued
            },
            new KnowledgeDocument
            {
                Id = "dealers",
                Name = "Dealer price list Q4.xlsx",
                Kind = KnowledgeDocumentKind.Spreadsheet,
                SizeBytes = 49_152,
                Status = KnowledgeDocumentStatus.Failed,
                Error = "The workbook is password protected"
            }
        ];
    }

    /// <summary>The same documents, all indexed (no queue, no failure): a still picture for screenshots and tests.</summary>
    public static IReadOnlyList<KnowledgeDocument> IndexedDocuments()
    {
        var documents = Documents().Take(5).ToArray();
        var battery = documents[4];
        battery.Chunks = KnowledgeBaseText.Chunk(battery.Id, battery.Text);
        battery.ChunkCount = battery.Chunks.Count;
        battery.Progress = 1;
        battery.UpdatedAt = Today;
        battery.Status = KnowledgeDocumentStatus.Ready;
        return documents;
    }

    /// <summary>
    /// An indexing pipeline that stands in for a backend: it waits <paramref name="stepTime"/> per step, fails the first
    /// time it sees a file whose name contains <paramref name="failName"/> (null fails none) with "Parser timed out",
    /// and otherwise chunks the document's text like the built-in pipeline.
    /// </summary>
    public static Func<KnowledgeDocument, IProgress<KnowledgeIndexProgress>, CancellationToken, Task<IReadOnlyList<KnowledgeChunk>>>
        SimulatedIndexer(TimeSpan stepTime, string? failName = null)
    {
        var failed = new HashSet<string>();
        return async (document, progress, cancellation) =>
        {
            progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Uploading, 0.1));
            await Task.Delay(stepTime, cancellation);
            progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Parsing, 0.35));
            await Task.Delay(stepTime, cancellation);
            if (failName is not null && document.Name.Contains(failName, StringComparison.OrdinalIgnoreCase) && failed.Add(document.Id))
            {
                throw new InvalidOperationException("Parser timed out");
            }
            for (var step = 1; step <= 4; step++)
            {
                progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.35 + 0.65 * step / 4));
                await Task.Delay(stepTime, cancellation);
            }
            return KnowledgeBaseText.Chunk(document.Id, document.Text ?? document.Name);
        };
    }

    private static KnowledgeDocument Indexed(string id, string name, KnowledgeDocumentKind kind, long size, string text, DateTimeOffset updated)
    {
        var chunks = KnowledgeBaseText.Chunk(id, text);
        return new KnowledgeDocument
        {
            Id = id,
            Name = name,
            Kind = kind,
            SizeBytes = size,
            Text = text,
            Chunks = chunks,
            ChunkCount = chunks.Count,
            Progress = 1,
            UpdatedAt = updated,
            Status = KnowledgeDocumentStatus.Ready
        };
    }
}
