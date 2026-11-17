using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary><see cref="AccountHealth"/>: score and band, renewal days, actions and the projected score, the usage metric switch and the compact layout.</summary>
public class AccountHealthTests
{
    private static AccountHealth Own()
    {
        var account = new AccountHealth { AccountName = "Test", Today = new DateTime(2026, 3, 1), RenewalDate = new DateTime(2026, 3, 31) };
        account.HealthHistory.AddRange([80, 70, 62]);
        account.Usage.Add(new AccountHealthUsageMetric("Seats", [10, 12, 9], "seats") { Limit = 20 });
        account.Usage.Add(new AccountHealthUsageMetric("Logins", [100, 90, 80]));
        account.Tickets.Add(new AccountHealthTicket("#1", "Broken export", AccountHealthTicketPriority.Urgent, AccountHealthTicketStatus.Open));
        account.Tickets.Add(new AccountHealthTicket("#2", "Old question", AccountHealthTicketPriority.Low, AccountHealthTicketStatus.Solved));
        account.Actions.Add(new AccountHealthAction("Call the sponsor", null, 8));
        account.Actions.Add(new AccountHealthAction("Fix the export", null, 50));
        return account;
    }

    [AvaloniaFact]
    public void Shows_Samples_Without_History()
    {
        var account = new AccountHealth();
        var window = TestHelpers.Show(account);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        account.IsShowingSamples.ShouldBeTrue();
        account.Score.ShouldBe(64);
        account.DaysToRenewal.ShouldBe(46);
        account.ProjectedScore.ShouldBe(66);
        window.Close();
    }

    [AvaloniaFact]
    public void Score_Band_And_Renewal_Come_From_The_Data()
    {
        var account = Own();
        var window = TestHelpers.Show(account);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        account.Score.ShouldBe(62);
        AccountHealth.BandOf(62).ShouldBe(1);
        AccountHealth.BandOf(75).ShouldBe(0);
        AccountHealth.BandOf(49).ShouldBe(2);
        account.DaysToRenewal.ShouldBe(30);
        account.RenewalLeft!.Text.ShouldBe("in 30 days");
        account.Today = new DateTime(2026, 4, 5);
        account.RenewalLeft!.Text.ShouldBe("5 days overdue");
        account.TicketRows!.Children.Count.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Ticking_Actions_Projects_The_Score_Up_To_100()
    {
        var account = Own();
        var events = new List<AccountHealthActionToggledEventArgs>();
        account.ActionToggled += (_, e) => events.Add(e);
        var window = TestHelpers.Show(account);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        var boxes = account.ActionRows!.Children.OfType<Checkbox>().ToList();
        boxes.Count.ShouldBe(2);
        boxes.Single(b => ((AccountHealthAction)b.Tag!).Impact == 8).IsChecked = true;
        account.ProjectedScore.ShouldBe(70);
        events.Single().ProjectedScore.ShouldBe(70);
        account.Projection!.IsVisible.ShouldBeTrue();
        boxes.Single(b => ((AccountHealthAction)b.Tag!).Impact == 50).IsChecked = true;
        account.ProjectedScore.ShouldBe(100);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Metric_Switch_Changes_The_Chart()
    {
        var account = Own();
        var window = TestHelpers.Show(account);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        account.Chart!.Label.ShouldBe("Seats");
        account.Chart.YMaximum.ShouldBe(20);
        account.SelectedMetric = 1;
        account.Chart.Label.ShouldBe("Logins");
        account.MetricSwitch!.SelectedIndex.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Narrow_Width_Stacks_The_Tiles()
    {
        var account = Own();
        var window = TestHelpers.Show(new Border { Width = 600, Child = account });
        Dispatcher.UIThread.RunJobs();
        account.IsCompact.ShouldBeTrue();
        account.Classes.Contains(AccountHealth.CompactPseudoClass).ShouldBeTrue();
        window.Close();
    }
}
