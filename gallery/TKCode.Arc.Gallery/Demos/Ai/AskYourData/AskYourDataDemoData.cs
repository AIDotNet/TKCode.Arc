using System.Globalization;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The model and database the ask your data demos play: an invented product's signups, MRR and churn by plan on an
/// "analytics" PostgreSQL database. A question about churn, MRR (or revenue) or signups gets its dataset; any other
/// question runs a search that matches no rows. Every number is invented. Demo content only.
/// </summary>
internal static class AskYourDataDemoData
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The question the card starts with.</summary>
    public const string Question = "Weekly signups by plan since August";

    /// <summary>The suggestions offered in the empty state.</summary>
    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "Weekly signups by plan since August",
        "MRR by plan over the last 6 months",
        "Churn rate by plan per month",
    ];

    /// <summary>The database the answers come from.</summary>
    public static AskYourDataSource Source { get; } = new() { Name = "analytics", Dialect = "PostgreSQL" };

    /// <summary>The three answers, in the order they are matched: churn, then MRR or revenue, then signups.</summary>
    public static IList<AskYourDataSimulationAnswer> Answers { get; } =
    [
        new()
        {
            Keywords = ["churn"],
            Sql = """
                WITH monthly AS (
                  SELECT date_trunc('month', m.month)::date AS month,
                         p.name AS plan,
                         count(*) FILTER (WHERE m.churned) AS churned,
                         count(*) AS active
                  FROM account_months m
                  JOIN plans p ON p.id = m.plan_id
                  WHERE m.month >= date '2026-05-01' AND p.name <> 'Free'
                  GROUP BY 1, 2
                )
                SELECT month, plan, round(100.0 * churned / active, 1) AS churn_rate
                FROM monthly
                ORDER BY month, plan;
                """,
            RowsScanned = 9_630,
            Result = Churn(),
        },
        new()
        {
            Keywords = ["mrr", "revenue"],
            Sql = """
                SELECT
                  date_trunc('month', s.period_start)::date AS month,
                  p.name AS plan,
                  sum(s.amount_cents) / 100.0 AS mrr
                FROM subscriptions s
                JOIN plans p ON p.id = s.plan_id
                WHERE s.status = 'active'
                  AND s.period_start >= date '2026-05-01'
                  AND p.name <> 'Free'
                GROUP BY month, plan
                ORDER BY month, plan;
                """,
            RowsScanned = 4_812,
            Result = Mrr(),
        },
        new()
        {
            Keywords = ["signup", "sign up", "sign-up"],
            Sql = """
                SELECT
                  date_trunc('week', u.created_at)::date AS week,
                  p.name AS plan,
                  count(*) AS signups
                FROM users u
                JOIN plans p ON p.id = u.plan_id
                WHERE u.created_at >= date '2026-08-03'
                GROUP BY week, plan
                ORDER BY week, plan;
                """,
            RowsScanned = 18_342,
            Result = Signups(),
        },
    ];

    /// <summary>Any other question: a search for its longest word that matches no rows.</summary>
    public static AskYourDataSimulationAnswer Fallback { get; } = new()
    {
        Sql = "SELECT e.name, count(*) AS events\nFROM events e\nWHERE e.name ILIKE '%" + AskYourDataSimulationAnswer.KeywordToken
              + "%'\nGROUP BY e.name\nORDER BY events DESC\nLIMIT 50;",
        RowsScanned = 2_406,
        Result = new AskYourDataResult { Title = string.Empty, RowCount = 0, DurationMs = 38 },
    };

    /// <summary>A new simulation of the model and database (tests and code use it; the XAML demos spell it out).</summary>
    public static AskYourDataSimulation Simulation() => new()
    {
        Question = Question,
        Suggestions = Suggestions,
        Source = Source,
        Answers = Answers,
        Fallback = Fallback,
    };

    /// <summary>Weekly signups by plan, 12 weeks from Aug 3: stacked bars with totals.</summary>
    public static AskYourDataResult Signups()
    {
        string[] weeks = ["Aug 3", "Aug 10", "Aug 17", "Aug 24", "Aug 31", "Sep 7", "Sep 14", "Sep 21", "Sep 28", "Oct 5", "Oct 12", "Oct 19"];
        double[] free = [52, 61, 58, 66, 70, 64, 73, 79, 75, 84, 88, 93];
        double[] pro = [21, 26, 24, 27, 30, 29, 33, 35, 34, 38, 41, 44];
        double[] team = [8, 10, 9, 11, 12, 11, 13, 14, 15, 16, 17, 19];
        double[] enterprise = [1, 2, 1, 2, 3, 2, 2, 3, 3, 4, 3, 4];
        var total = free.Sum() + pro.Sum() + team.Sum() + enterprise.Sum();
        return new AskYourDataResult
        {
            Title = "Weekly signups by plan",
            Subtitle = "Aug 3 to Oct 25 · " + total.ToString("N0", s_enUs) + " signups",
            Categories = weeks,
            Series =
            [
                new AskYourDataSeries { Id = "free", Label = "Free", Values = free },
                new AskYourDataSeries { Id = "pro", Label = "Pro", Values = pro },
                new AskYourDataSeries { Id = "team", Label = "Team", Values = team },
                new AskYourDataSeries { Id = "enterprise", Label = "Enterprise", Values = enterprise },
            ],
            CategoryLabel = "Week",
            Insight = "Weekly signups nearly doubled, from 82 to 160. Free drives the volume, and Pro more than doubled, from 21 to 44 a week.",
            RowCount = 48,
            DurationMs = 214,
        };
    }

    /// <summary>MRR by paid plan, May to October: lines, the legend shows the latest month.</summary>
    public static AskYourDataResult Mrr()
    {
        string[] months = ["May", "Jun", "Jul", "Aug", "Sep", "Oct"];
        double[] pro = [31_200, 33_000, 34_900, 36_100, 38_400, 40_200];
        double[] team = [18_400, 19_900, 21_500, 23_800, 25_100, 27_600];
        double[] enterprise = [42_000, 42_000, 48_500, 48_500, 55_000, 61_300];
        return new AskYourDataResult
        {
            Title = "MRR by plan",
            Subtitle = "May to Oct · paid plans",
            Categories = months,
            Series =
            [
                new AskYourDataSeries { Id = "pro", Label = "Pro", Values = pro },
                new AskYourDataSeries { Id = "team", Label = "Team", Values = team },
                new AskYourDataSeries { Id = "enterprise", Label = "Enterprise", Values = enterprise },
            ],
            LegendValue = AskYourDataLegendValue.Last,
            Chart = AskYourDataChartKind.Line,
            CategoryLabel = "Month",
            FormatValue = Thousands,
            Insight = "MRR reached $129.1k in October, up from $91.6k in May. Enterprise added the most, $19.3k, as two annual contracts started.",
            RowCount = 18,
            DurationMs = 1240,
        };
    }

    /// <summary>Monthly churn rate by paid plan: rates, so bars group side by side and nothing is totalled.</summary>
    public static AskYourDataResult Churn()
    {
        string[] months = ["May", "Jun", "Jul", "Aug", "Sep", "Oct"];
        double[] pro = [4.1, 3.9, 4.4, 3.6, 3.4, 3.1];
        double[] team = [2.8, 2.6, 2.9, 2.4, 2.2, 2.0];
        double[] enterprise = [0.9, 1.1, 0.8, 0.7, 0.9, 0.6];
        return new AskYourDataResult
        {
            Title = "Monthly churn rate by plan",
            Subtitle = "May to Oct · paid plans",
            Categories = months,
            Series =
            [
                new AskYourDataSeries { Id = "pro", Label = "Pro", Values = pro },
                new AskYourDataSeries { Id = "team", Label = "Team", Values = team },
                new AskYourDataSeries { Id = "enterprise", Label = "Enterprise", Values = enterprise },
            ],
            Stacked = false,
            CategoryLabel = "Month",
            FormatValue = Percent,
            Insight = "Churn fell on every paid plan since May. Pro is still the highest at 3.1%, down from 4.1%.",
            RowCount = 18,
            DurationMs = 87,
        };
    }

    private static string Thousands(double value) =>
        value >= 1000 ? "$" + (value / 1000).ToString("0.#", s_enUs) + "k" : "$" + value.ToString("0", s_enUs);

    private static string Percent(double value) => value.ToString("0.0", s_enUs) + "%";
}
