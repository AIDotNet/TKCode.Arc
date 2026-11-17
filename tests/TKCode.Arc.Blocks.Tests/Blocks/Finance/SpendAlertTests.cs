using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Finance;

/// <summary><see cref="SpendAlert"/>: the math, crossing and dismissing alerts, the status, the editor and the meter marks.</summary>
public class SpendAlertTests
{
    private static (SpendAlert Alert, Window Window) Show(double width = 700)
    {
        var alert = new SpendAlert();
        SpendAlertSamples.Apply(alert);
        var window = TestHelpers.Show(new Border { Width = width, Child = alert });
        Pump();
        return (alert, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [Fact]
    public void Math_Projects_The_Pace_And_Finds_The_Highest_Crossed_Alert()
    {
        SpendAlertMath.Projected(412.80m, 18, 30).ShouldBe(688m);
        SpendAlertMath.Projected(100m, 0, 30).ShouldBe(100m);
        SpendThreshold[] alerts = [new(0.5), new(0.8), new(1.0), new(0.9, isEnabled: false)];
        SpendAlertMath.Crossed(alerts, 200, 560, 600)!.Share.ShouldBe(0.8);
        SpendAlertMath.Crossed(alerts, 500, 560, 600).ShouldBeNull();
        SpendAlertMath.Crossed(alerts, 560, 200, 600).ShouldBeNull();
        SpendAlertMath.Crossed(alerts, 590, 600, 600)!.Share.ShouldBe(1.0);
        SpendAlertMath.Status(alerts, 100, 600, 300).ShouldBe(SpendAlertStatus.OnTrack);
        SpendAlertMath.Status(alerts, 250, 600, 700).ShouldBe(SpendAlertStatus.AtRisk);
        SpendAlertMath.Status(alerts, 320, 600, 500).ShouldBe(SpendAlertStatus.Warning);
        SpendAlertMath.Status(alerts, 601, 600, 601).ShouldBe(SpendAlertStatus.Over);
        new SpendThreshold(5).Share.ShouldBe(SpendAlertMath.MaxShare);
    }

    [AvaloniaFact]
    public void Rising_Past_An_Alert_Shows_The_Banner_Once_And_Dismiss_Hides_It()
    {
        var (alert, window) = Show();
        try
        {
            alert.Status.ShouldBe(SpendAlertStatus.Warning);
            alert.RaisedThreshold.ShouldBeNull();
            alert.Classes.Contains(":alerting").ShouldBeFalse();
            var crossed = new List<double>();
            alert.ThresholdCrossed += (_, e) => crossed.Add(e.Threshold.Share);

            alert.AddSpending(80m);
            Pump();
            crossed.ShouldBe([0.8]);
            alert.RaisedThreshold!.Share.ShouldBe(0.8);
            alert.Classes.Contains(":alerting").ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(alert, "PART_BannerTitle").Text.ShouldBe("You've used 82% of your Dining out budget");
            alert.LastAnnouncement.ShouldStartWith("You've used 82%");

            alert.AddSpending(5m);
            crossed.Count.ShouldBe(1);

            alert.DismissAlert();
            alert.Classes.Contains(":alerting").ShouldBeFalse();

            alert.AddSpending(120m);
            Pump();
            crossed.ShouldBe([0.8, 1.0]);
            alert.Status.ShouldBe(SpendAlertStatus.Over);
            TestHelpers.Descendant<SpendAlertBanner>(alert).Classes.Contains("danger").ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(alert, "PART_BannerBody").Text.ShouldBe("You are $17.80 over budget.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Raising_The_Budget_Clears_An_Alert_That_No_Longer_Holds()
    {
        var (alert, window) = Show();
        try
        {
            alert.AddSpending(80m);
            alert.RaisedThreshold.ShouldNotBeNull();
            alert.Budget = 2000m;
            alert.RaisedThreshold.ShouldBeNull();
            alert.Status.ShouldBe(SpendAlertStatus.OnTrack);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Editor_Writes_Back_Adds_Up_To_The_Limit_And_Removes()
    {
        var (alert, window) = Show();
        try
        {
            var edits = 0;
            alert.ThresholdsEdited += (_, _) => edits++;
            var switches = alert.GetVisualDescendants().OfType<Switch>().ToList();
            switches.Count.ShouldBe(3);
            AutomationProperties.GetName(switches[1]).ShouldBe("Alert at 80%");
            switches[1].IsChecked = false;
            alert.Thresholds[1].IsEnabled.ShouldBeFalse();
            edits.ShouldBe(1);
            TestHelpers.Descendant<SpendAlertMeter>(alert).Marks.Count.ShouldBe(2);

            alert.AddThreshold()!.Share.ShouldBe(1.1);
            alert.AddThreshold()!.Share.ShouldBe(1.2);
            alert.AddThreshold().ShouldBeNull();
            TestHelpers.Descendant<Button>(alert, "PART_Add").IsEnabled.ShouldBeFalse();

            alert.Thresholds[0].Share = 0.6;
            Pump();
            alert.GetVisualDescendants().OfType<NumberField>().Where(f => f.Name != "PART_Budget").First().Value.ShouldBe(60m);

            alert.Thresholds.RemoveAt(0);
            alert.GetVisualDescendants().OfType<Switch>().Count().ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Budget_Field_And_Read_Only_Mode()
    {
        var (alert, window) = Show(width: 420);
        try
        {
            alert.IsCompact.ShouldBeTrue();
            var field = TestHelpers.Descendant<NumberField>(alert, "PART_Budget");
            field.Value.ShouldBe(600m);
            field.Value = 900m;
            alert.Budget.ShouldBe(900m);
            alert.IsEditable = false;
            field.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Button>(alert, "PART_Add").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
