using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Cards;

public class MetricCardTests
{
    [AvaloniaFact]
    public void Default_Card_Matches_Arc_Geometry()
    {
        var card = new MetricCard { Label = "Orders", Value = 1204, Context = "vs last week", Change = "+12%", Width = 300 };
        var window = TestHelpers.Show(new StackPanel { Children = { card } });
        try
        {
            var border = card.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Card");
            border.CornerRadius.TopLeft.ShouldBe(34);
            border.Padding.ShouldBe(new Avalonia.Thickness(24));
            card.ChangeDirection.ShouldBe(MetricChangeDirection.Up);
            card.Classes.ShouldContain(":up");
            card.LabelSwap!.CurrentBlock!.Text.ShouldBe("Orders");
            card.ContextSwap!.CurrentBlock!.Text.ShouldBe("vs last week");
            card.ChangeSwap!.CurrentBlock!.Text.ShouldBe("+12%");
            TestHelpers.Descendant<AnimatedCounter>(card).Value.ShouldBe(1204);
            AutomationProperties.GetName(card).ShouldBe("Orders");
            card.GetVisualDescendants().OfType<MetricCardSparkline>().First().IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Change_Sign_Sets_Direction_Glyph_And_Chip_Presence()
    {
        var card = new MetricCard { Label = "Latency", Value = 182, Change = "−8%", IsChangeGlyphVisible = true };
        var window = TestHelpers.Show(new StackPanel { Children = { card } });
        try
        {
            card.ChangeDirection.ShouldBe(MetricChangeDirection.Down);
            card.Classes.ShouldContain(":down");
            card.ChangeGlyph.ShouldBe(MessagesIcons.TrendingDown);
            card.Change = "flat";
            card.ChangeDirection.ShouldBe(MetricChangeDirection.None);
            card.ChangeGlyph.ShouldBeNull();
            card.Change = null;
            Dispatcher.UIThread.RunJobs();
            card.HasChange.ShouldBeFalse();
            var presence = card.GetVisualDescendants().OfType<TKCode.Arc.Primitives.PresenceHost>().First();
            presence.IsPresent.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_Rolls_From_The_Side_The_Number_Moved_Toward()
    {
        MetricCard.AmountIn("1,204 orders").ShouldBe(1204);
        MetricCard.AmountIn("−8%").ShouldBe(-8);
        MetricCard.AmountIn("+0.6 pts").ShouldBe(0.6);
        double.IsNaN(MetricCard.AmountIn("vs last week")).ShouldBeTrue();

        var card = new MetricCard { Label = "Orders", Change = "+12%" };
        var window = TestHelpers.Show(new StackPanel { Children = { card } });
        try
        {
            card.Change = "+4%";
            card.ChangeSwap!.Direction.ShouldBe(-1);
            card.Change = "+20%";
            card.ChangeSwap.Direction.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sparkline_Shows_With_Two_Or_More_Points()
    {
        var card = new MetricCard { Label = "Orders", Value = 10, Sparkline = new List<double> { 1, 3, 2, 5 } };
        var window = TestHelpers.Show(new StackPanel { Children = { card } });
        try
        {
            var sparkline = card.GetVisualDescendants().OfType<MetricCardSparkline>().First();
            sparkline.IsVisible.ShouldBeTrue();
            sparkline.Bounds.Width.ShouldBe(96);
            sparkline.RevealProgress.ShouldBe(1);
            card.Sparkline = new List<double> { 4 };
            card.HasSparkline.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
