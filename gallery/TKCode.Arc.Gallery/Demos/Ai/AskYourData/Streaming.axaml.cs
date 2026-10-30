using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A card wired to a streaming SQL writer and a query runner that reports progress (both simulated).</summary>
public partial class AskYourDataStreamingDemo : UserControl
{
    private const string Query = """
        SELECT
          'Q' || extract(quarter FROM o.placed_at) AS quarter,
          r.name AS region,
          round(avg(o.total_cents) / 100.0, 2) AS avg_order_value
        FROM orders o
        JOIN regions r ON r.id = o.region_id
        WHERE o.placed_at >= date '2026-01-01'
        GROUP BY quarter, region
        ORDER BY quarter, region;
        """;

    public AskYourDataStreamingDemo()
    {
        InitializeComponent();
        Card.Source = new AskYourDataSource { Name = "shop", Dialect = "PostgreSQL" };
        Card.Suggestions = [];
        Card.StreamSql = request => StreamAsync(request.CancellationToken);
        Card.RunQuery = RunAsync;
    }

    /// <summary>The model's SQL in small chunks, as a streaming API delivers it.</summary>
    private static async IAsyncEnumerable<string> StreamAsync([EnumeratorCancellation] CancellationToken token)
    {
        await Task.Delay(300, token);
        for (var i = 0; i < Query.Length; i += 9)
        {
            await Task.Delay(45, token);
            yield return Query.Substring(i, Math.Min(9, Query.Length - i));
        }
    }

    /// <summary>The query: about a second of scanning with progress, then values by region (not stacked: bars group).</summary>
    private static async Task<AskYourDataResult> RunAsync(AskYourDataQueryRequest request)
    {
        const long total = 61_204;
        for (var step = 1; step <= 8; step++)
        {
            await Task.Delay(120, request.CancellationToken);
            request.ReportProgress(total * step / 8, total);
        }
        var money = CultureInfo.GetCultureInfo("en-US");
        return new AskYourDataResult
        {
            Title = "Average order value by region",
            Subtitle = "2026 by quarter · sample data",
            Categories = ["Q1", "Q2", "Q3", "Q4"],
            Series =
            [
                new AskYourDataSeries { Id = "north", Label = "North", Values = [84.2, 86.9, 91.4, 95.0] },
                new AskYourDataSeries { Id = "south", Label = "South", Values = [72.5, 74.1, 73.8, 79.6] },
                new AskYourDataSeries { Id = "west", Label = "West", Values = [91.0, 89.3, 94.7, 101.2] },
            ],
            Stacked = false,
            CategoryLabel = "Quarter",
            FormatValue = value => value.ToString("$0.00", money),
            Insight = "West leads every quarter and passed $100 in Q4. South trails by about $20 but grew 10% since Q1.",
            RowCount = 12,
            DurationMs = 960,
        };
    }
}
