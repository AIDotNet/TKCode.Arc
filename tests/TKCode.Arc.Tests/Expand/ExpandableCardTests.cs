using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Expand;

public class ExpandableCardTests
{
    private static ExpandableCard Card(double width = double.NaN, double expandedWidth = double.NaN) => new()
    {
        Header = "Pro plan",
        Description = "$24 per month · billed yearly",
        CollapsedWidth = width,
        ExpandedWidth = expandedWidth,
        Content = new Button { Content = "Upgrade" }
    };

    private static ExpandableCardPanel Panel(ExpandableCard card) => card.GetVisualDescendants().OfType<ExpandableCardPanel>().First();

    private static ExpandableCardTrigger Trigger(ExpandableCard card) => card.GetVisualDescendants().OfType<ExpandableCardTrigger>().First();

    private static Border Surface(ExpandableCard card) => card.GetVisualDescendants().OfType<Border>().First(b => b.Name == ExpandableCard.CardPart);

    [AvaloniaFact]
    public void Collapsed_Card_Is_Centered_At_Its_Width_With_A_Closed_Panel()
    {
        var card = Card(width: 300);
        var window = TestHelpers.Show(card);
        try
        {
            Surface(card).Bounds.Width.ShouldBe(300);
            Surface(card).Bounds.X.ShouldBe((card.Bounds.Width - 300) / 2, 0.5);
            Panel(card).Bounds.Height.ShouldBe(0);
            Trigger(card).Bounds.Height.ShouldBeGreaterThanOrEqualTo(64);
            card.GetVisualDescendants().OfType<Border>().First(b => b.Name == ExpandableCard.DetailsPart).IsHitTestVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Header_Opens_The_Details_And_Grows_To_The_Expanded_Width()
    {
        var card = Card(width: 260, expandedWidth: 360);
        var window = TestHelpers.Show(card);
        try
        {
            Trigger(card).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsExpanded.ShouldBeTrue();
            Surface(card).Bounds.Width.ShouldBe(360);
            Panel(card).Bounds.Height.ShouldBeGreaterThan(0);
            card.DetailsOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Inside_Closes_And_Returns_Focus_To_The_Header()
    {
        var card = Card();
        card.IsExpanded = true;
        var window = TestHelpers.Show(card);
        try
        {
            var inner = (Button)card.Content!;
            inner.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsExpanded.ShouldBeFalse();
            Trigger(card).IsFocused.ShouldBeTrue();
            Panel(card).Bounds.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Fills_The_Container_Without_A_Width()
    {
        var card = Card();
        var window = TestHelpers.Show(card);
        try
        {
            Surface(card).Bounds.Width.ShouldBe(card.Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Morph_Springs_Width_And_Height_Together_With_Full_Motion()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var card = Card(width: 200, expandedWidth: 340);
        var window = TestHelpers.Show(card, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            card.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(80));
            Dispatcher.UIThread.RunJobs();
            card.CardWidth.ShouldBeGreaterThan(200);
            card.CardWidth.ShouldBeLessThan(340);
            Panel(card).Bounds.Height.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            card.CardWidth.ShouldBe(340, 0.01);
            card.DetailsOpacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changed_Summary_Words_Roll_And_Unchanged_Ones_Stay()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var card = Card();
        var window = TestHelpers.Show(card, Motion.ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50));
            Dispatcher.UIThread.RunJobs();
            var summary = card.GetVisualDescendants().OfType<ExpandableCardSummary>().First();
            card.Description = "$24 per month · billed monthly";
            Dispatcher.UIThread.RunJobs();
            summary.LeavingCount.ShouldBe(1);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            Dispatcher.UIThread.RunJobs();
            summary.LeavingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nothing_That_Resizes_While_The_Card_Opens_Draws_An_Uneven_Border()
    {
        // The card's width and height spring while it opens: an uneven BorderThickness would rebuild a path geometry every
        // frame, so the divider above the details is a filled strip.
        var card = Card();
        card.IsExpanded = true;
        var window = TestHelpers.Show(card);
        try
        {
            card.GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible).ShouldAllBe(b => b.BorderThickness.IsUniform);
        }
        finally
        {
            window.Close();
        }
    }
}
