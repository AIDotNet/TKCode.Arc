using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class RadioCardsTests
{
    private static RadioCards Plans(RadioCardsLayout layout = RadioCardsLayout.Grid) => new()
    {
        Layout = layout,
        Width = 600,
        ItemsSource = new[]
        {
            new RadioCardOption("starter", "Starter", "For individuals", "$9"),
            new RadioCardOption("team", "Team", "For small teams", "$29") { Icon = ArcIcons.Check },
            new RadioCardOption("enterprise", "Enterprise", "Custom contracts", "Contact us") { IsDisabled = true, DisabledReason = "Talk to sales first" },
            new RadioCardOption("growth", "Growth", "For scaling teams", "$59")
        }
    };

    [AvaloniaFact]
    public void Grid_Fills_Columns_By_MinColumnWidth()
    {
        var cards = Plans();
        var window = TestHelpers.Show(cards);
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        try
        {
            // floor((600 + 10) / (180 + 10)) = 3 columns of (600 - 20) / 3.
            var first = cards.ContainerFromIndex(0)!;
            var fourth = cards.ContainerFromIndex(3)!;
            first.Bounds.Width.ShouldBe(580.0 / 3, 1);
            fourth.Bounds.X.ShouldBe(0);
            fourth.Bounds.Y.ShouldBeGreaterThan(first.Bounds.Bottom);
            cards.Layout = RadioCardsLayout.List;
            Dispatcher.UIThread.RunJobs();
            cards.ContainerFromIndex(1)!.Bounds.Width.ShouldBe(600);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Skip_Disabled_Cards_And_Home_End_Jump()
    {
        var cards = Plans();
        cards.SelectedValue = "team";
        var window = TestHelpers.Show(cards);
        try
        {
            cards.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            cards.SelectedValue.ShouldBe("growth");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            cards.SelectedValue.ShouldBe("starter");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            cards.SelectedValue.ShouldBe("growth");
            cards.ContainerFromIndex(3)!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_To_Left_Mirrors_Left_And_Right()
    {
        var cards = Plans();
        cards.FlowDirection = FlowDirection.RightToLeft;
        cards.SelectedValue = "team";
        var window = TestHelpers.Show(cards);
        try
        {
            cards.ContainerFromIndex(1)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            cards.SelectedValue.ShouldBe("starter");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Card_Shows_Its_Reason_And_Ring_Follows_Selection()
    {
        var cards = Plans();
        cards.SelectedValue = "growth";
        var window = TestHelpers.Show(cards);
        try
        {
            var disabled = cards.ContainerFromIndex(2).ShouldBeOfType<RadioCard>();
            disabled.ShownDescription.ShouldBe("Talk to sales first");
            AutomationProperties.GetHelpText(disabled).ShouldBe("Talk to sales first");
            var ring = TestHelpers.Descendant<GlidingIndicator>(cards);
            var card = cards.ContainerFromIndex(3)!;
            ring.CurrentRect.Width.ShouldBe(card.Bounds.Width - 1.5, 0.01);
            ring.CurrentRect.Y.ShouldBe(card.Bounds.Y + 0.75, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Selects_The_Focused_Card()
    {
        var cards = Plans();
        var window = TestHelpers.Show(cards);
        try
        {
            cards.SelectedIndex.ShouldBe(-1);
            cards.ContainerFromIndex(0)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            cards.SelectedValue.ShouldBe("starter");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ring_Glides_On_Arcs_Own_Spring()
    {
        var clock = new TKCode.Arc.Motion.ManualFrameClock();
        using var _ = clock.Install();
        var cards = Plans();
        cards.SelectedIndex = 0;
        var window = TestHelpers.Show(cards, TKCode.Arc.Motion.ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var ring = TestHelpers.Descendant<GlidingIndicator>(cards);
            var from = ring.CurrentRect;
            cards.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            var card = cards.ContainerFromIndex(1)!;
            var target = new Rect(card.TranslatePoint(default, ring)!.Value, card.Bounds.Size).Deflate(0.75);
            var t = CorePrimitives.CoreFrames.Run(clock, 6);
            // Arc: physical(0.42, 0.14), not the morph preset (0.42, 0.16).
            RadioCards.RingGlide.ShouldBe(TKCode.Arc.Motion.SpringSpec.FromVisual(0.42, 0.14));
            var (dx, _) = TKCode.Arc.Motion.SpringSolver.Evaluate(RadioCards.RingGlide, from.X - target.X, 0, t);
            ring.CurrentRect.X.ShouldBe(target.X + dx, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
