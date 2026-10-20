using TKCode.Arc.Motion;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;

namespace TKCode.Arc.Tests.Illustrations;

/// <summary>What each illustration's story does: payments, failover and search.</summary>
public class IllustrationScenarioTests
{
    private static Window Show(Control illustration)
    {
        var window = TestHelpers.Show(illustration, ArcMotionLevel.Full);
        window.Width = 900;
        window.Height = 500;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Run(Illustration illustration, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 0.05)
        {
            illustration.Advance(0.05);
        }
    }

    [AvaloniaFact]
    public void Every_Fourth_Payment_Is_Declined_And_Approved_Ones_Settle()
    {
        var payment = new PaymentFlow();
        var decisions = new List<bool>();
        payment.PaymentCompleted += (_, e) => decisions.Add(e.Approved);
        var window = Show(payment);
        try
        {
            payment.Amount.ShouldBe(42m);
            payment.Currency.ShouldBe("USD");
            payment.DeclineEvery.ShouldBe(4);
            Run(payment, 8.4 * 4 + 0.2);
            decisions.ShouldBe([true, true, true, false]);
            payment.Balance.ShouldBe(1_284m + 3 * 42m);
            payment.PaymentCount.ShouldBe(4);
            payment.FormatAmount(1_234.5m).ShouldBe("$1,234.50");
            payment.Currency = "EUR";
            payment.FormatAmount(129.9m).ShouldBe("€129.90");
            payment.Currency = "JPY";
            payment.FormatAmount(980m).ShouldBe("¥980");
            payment.Currency = "CHF";
            payment.FormatAmount(5m).ShouldBe("CHF 5.00");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Declines_Can_Be_Turned_Off()
    {
        var payment = new PaymentFlow { DeclineEvery = 0 };
        var decisions = new List<bool>();
        payment.PaymentCompleted += (_, e) => decisions.Add(e.Approved);
        var window = Show(payment);
        try
        {
            Run(payment, 8.4 * 4 + 0.2);
            decisions.ShouldAllBe(approved => approved);
            decisions.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Primary_Region_Moves_Traffic_And_Promotes_The_Next_Database()
    {
        var failover = new MultiRegionFailover { IsAutomatic = false };
        var promoted = new List<int>();
        failover.PrimaryRegionChanged += (_, index) => promoted.Add(index);
        var window = Show(failover);
        try
        {
            failover.PrimaryRegion.ShouldBe(0);
            failover.TrafficRegion.ShouldBe(0);
            Run(failover, 2);
            failover.Step.ShouldBe(0);

            failover.FailRegion(0);
            failover.Step.ShouldBe(1);
            Run(failover, 3);
            failover.TrafficRegion.ShouldBe(1);
            failover.PrimaryRegion.ShouldBe(1);
            promoted.ShouldBe([1]);
            failover.Step.ShouldBe(3);

            // Failing it again does nothing; recovering brings it back as a replica.
            failover.FailRegion(0);
            failover.RecoverRegion(0);
            failover.Step.ShouldBe(4);
            Run(failover, 2);
            failover.PrimaryRegion.ShouldBe(1);

            // A replica failing does not move traffic or the primary.
            failover.FailRegion(2);
            Run(failover, 3);
            failover.TrafficRegion.ShouldBe(1);
            failover.PrimaryRegion.ShouldBe(1);
            promoted.ShouldBe([1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Automatic_Story_Fails_Over_Recovers_And_Fails_Back()
    {
        var failover = new MultiRegionFailover();
        var steps = new List<int>();
        failover.StepChanged += (_, e) => steps.Add(e.Step);
        var window = Show(failover);
        try
        {
            Run(failover, 7);
            failover.PrimaryRegion.ShouldBe(1);
            Run(failover, 7);
            failover.PrimaryRegion.ShouldBe(0);
            failover.TrafficRegion.ShouldBe(0);
            steps.Distinct().Order().ShouldBe([0, 1, 2, 3, 4, 5]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_Regions_Work_Too()
    {
        var failover = new MultiRegionFailover { Regions = ["a", "b"], IsAutomatic = false };
        var window = Show(failover);
        try
        {
            failover.FailRegion(0);
            Run(failover, 3);
            failover.PrimaryRegion.ShouldBe(1);
            failover.FailRegion(1);
            Run(failover, 3);
            // No healthy region left: nothing to fail over to.
            failover.PrimaryRegion.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Search_Ranks_The_Right_Documents()
    {
        var index = new VectorSearchIndex(VectorSearch.DefaultDocuments);
        var refund = index.Search(VectorSearchIndex.Embed("refund my annual plan"), 3);
        refund[0].Document.Title.ShouldBe("Refund policy for annual plans");
        refund.ShouldAllBe(r => r.Document.Topic == "Billing");
        refund.Select(r => r.Rank).ShouldBe([1, 2, 3]);
        refund[0].Score.ShouldBeGreaterThan(refund[1].Score);

        var security = index.Search(VectorSearchIndex.Embed("reset two-factor on a new device"), 3);
        security[0].Document.Title.ShouldBe("Reset two-factor authentication");
        security.ShouldAllBe(r => r.Document.Topic == "Security");

        index.Search(VectorSearchIndex.Embed("my package is delayed"), 1)[0].Document.Title.ShouldBe("Track a delayed package");

        // An empty question still answers (every score is zero) instead of failing.
        index.Search(VectorSearchIndex.Embed("   "), 2).Count.ShouldBe(2);
        VectorSearchIndex.Tokens("The Plans were Delayed").ShouldBe(["plan", "were", "delay"]);
    }

    [AvaloniaFact]
    public void The_Map_Is_Deterministic_And_Keeps_Topics_Together()
    {
        var a = new VectorSearchIndex(VectorSearch.DefaultDocuments);
        var b = new VectorSearchIndex(VectorSearch.DefaultDocuments);
        a.Positions.ShouldBe(b.Positions);
        // Each document is nearer to its own topic's centre than to any other topic's.
        var centres = a.Documents.Select((d, i) => (d.Topic, a.Positions[i]))
                       .GroupBy(x => x.Topic)
                       .ToDictionary(g => g.Key, g => (X: g.Average(x => x.Item2.X), Y: g.Average(x => x.Item2.Y)));
        var misplaced = 0;
        for (var i = 0; i < a.Documents.Count; i++)
        {
            var (x, y) = a.Positions[i];
            var nearest = centres.MinBy(c => (c.Value.X - x) * (c.Value.X - x) + (c.Value.Y - y) * (c.Value.Y - y)).Key;
            if (nearest != a.Documents[i].Topic)
            {
                misplaced++;
            }
        }
        misplaced.ShouldBeLessThanOrEqualTo(2);
    }

    [AvaloniaFact]
    public void Search_Shows_Any_Question_And_Reports_Its_Answers()
    {
        var search = new VectorSearch();
        var changed = 0;
        search.ResultsChanged += (_, _) => changed++;
        var window = Show(search);
        try
        {
            search.TopK.ShouldBe(3);
            search.Search("change my password");
            search.Query.ShouldBe("change my password");
            Run(search, 5);
            changed.ShouldBeGreaterThan(0);
            search.Results.Count.ShouldBe(3);
            search.Results[0].Document.Title.ShouldBe("Change your password");
            search.Step.ShouldBe(3);

            search.TopK = 5;
            Dispatcher.UIThread.RunJobs();
            search.Search("invoice for last month");
            Run(search, 5);
            search.Results.Count.ShouldBe(5);
            search.Results[0].Document.Topic.ShouldBe("Billing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Documents_And_Queries_Are_Searched()
    {
        var search = new VectorSearch
        {
            Documents =
            [
                new VectorSearchDocument("Overnight oats", "Breakfast", "oats morning"),
                new VectorSearchDocument("Lemon salmon", "Dinner", "fish salmon oven"),
                new VectorSearchDocument("Chocolate mousse", "Dessert", "chocolate sweet")
            ],
            Queries = ["salmon for dinner"],
            TopK = 1
        };
        var window = Show(search);
        try
        {
            Run(search, 5);
            search.Results.Single().Document.Title.ShouldBe("Lemon salmon");
        }
        finally
        {
            window.Close();
        }
    }
}
