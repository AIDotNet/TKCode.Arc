using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A query that fails once with a database error, then answers (simulated).</summary>
public partial class AskYourDataErrorsDemo : UserControl
{
    private int _attempts;

    public AskYourDataErrorsDemo()
    {
        InitializeComponent();
        Card.Source = AskYourDataDemoData.Source;
        Card.Suggestions = [];
        Card.GenerateSql = _ => Task.FromResult("""
            SELECT date_trunc('week', o.refunded_at)::date AS week,
                   count(*) AS refunds
            FROM orders o
            WHERE o.refunded_at >= date_trunc('quarter', now())
            GROUP BY week
            ORDER BY week;
            """);
        Card.RunQuery = RunAsync;
    }

    private async Task<AskYourDataResult> RunAsync(AskYourDataQueryRequest request)
    {
        request.ReportProgress(0, 8_420);
        await Task.Delay(700, request.CancellationToken);
        if (++_attempts % 2 == 1)
        {
            throw new InvalidOperationException(
                "ERROR: column o.refunded_at does not exist\nHINT: Perhaps you meant to reference the column \"o.refund_at\".");
        }
        request.ReportProgress(8_420, 8_420);
        return new AskYourDataResult
        {
            Title = "Refunds by week",
            Subtitle = "This quarter · sample data",
            Categories = ["Oct 5", "Oct 12", "Oct 19", "Oct 26"],
            Series = [new AskYourDataSeries { Id = "refunds", Label = "Refunds", Values = [14, 9, 12, 7] }],
            CategoryLabel = "Week",
            Insight = "Refunds fell to 7 last week, the lowest this quarter.",
            RowCount = 4,
            DurationMs = 702,
        };
    }
}
