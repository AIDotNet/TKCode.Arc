using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Media;

public class CardStackTests
{
    private static readonly string[] s_cities = ["Lisbon", "Kyoto", "Oaxaca", "Tbilisi", "Hobart"];

    private static (CardStack Stack, Window Window) Show(CardStack stack, ArcMotionLevel level = ArcMotionLevel.Off, double width = 480)
    {
        stack.Width = width;
        var window = TestHelpers.Show(new ScrollViewer { Content = stack }, level);
        window.Width = 600;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (stack, window);
    }

    private static CardStack Make() => new() { ItemsSource = s_cities };

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static CardStackStage Stage(CardStack stack) => TestHelpers.Descendant<CardStackStage>(stack, CardStack.StagePart);

    private static MediaButton Button(CardStack stack, string name) => TestHelpers.Descendant<MediaButton>(stack, name);

    [AvaloniaFact]
    public void Shows_The_Top_Four_Cards_With_Depth()
    {
        var (stack, window) = Show(Make());
        try
        {
            var panel = stack.Panel!;
            panel.RealizedCount.ShouldBe(4);
            var top = panel.CardAt(0)!;
            top.IsInteractive.ShouldBeTrue();
            top.ZIndex.ShouldBe(4);
            AutomationProperties.GetName(top).ShouldBe("Lisbon, 1 of 5");
            top.Bounds.Width.ShouldBe(320);
            top.Bounds.Height.ShouldBe(304);
            var second = panel.CardAt(1)!;
            second.IsInteractive.ShouldBeFalse();
            second.Depth.Value.ShouldBe(1);
            second.Opacity.ShouldBe(1);
            // The last rendered card waits invisibly one step behind.
            panel.CardAt(3)!.Opacity.ShouldBe(0);
            // Each card behind is 5% smaller and peeks 12px out below (scaled from its bottom edge).
            var matrix = ((Avalonia.Media.MatrixTransform)second.RenderTransform!).Matrix;
            matrix.M11.ShouldBe(0.95, 1e-9);
            matrix.M32.ShouldBe(304 * 0.05 + 12, 1e-9);
            Button(stack, CardStack.UndoButtonPart).IsSoftDisabled.ShouldBeTrue();
            Button(stack, CardStack.LeftButtonPart).IsSoftDisabled.ShouldBeFalse();
            stack.CurrentIndex.ShouldBe(0);
            var stagePeer = ControlAutomationPeer.CreatePeerForElement(Stage(stack));
            stagePeer.GetLocalizedControlType().ShouldBe("card stack");
            stagePeer.GetName().ShouldBe("Cards");
            stagePeer.GetHelpText().ShouldContain("pass or keep");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Decide_Undo_And_Announce()
    {
        var (stack, window) = Show(Make());
        var decided = new List<(object? Item, CardDecision Decision)>();
        var undone = new List<object?>();
        stack.Decided += (_, e) => decided.Add((e.Item, e.Decision));
        stack.DecisionUndone += (_, e) => undone.Add(e.Item);
        try
        {
            Stage(stack).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            stack.Panel!.CardAt(0)!.Classes.Contains(CardStackCard.StageFocusPseudoClass).ShouldBeTrue();
            Press(window, PhysicalKey.ArrowRight);
            decided.ShouldBe([("Lisbon", CardDecision.Right)]);
            stack.CurrentIndex.ShouldBe(1);
            stack.LastAnnouncement.ShouldBe("Lisbon: Keep. 4 left.");
            // Motion off: the thrown card is gone at once and the next one is on top.
            stack.Panel!.CardAt(0).ShouldBeNull();
            stack.Panel.CardAt(1)!.IsInteractive.ShouldBeTrue();
            stack.Panel.CardAt(4).ShouldNotBeNull();
            stack.CanUndo.ShouldBeTrue();

            Press(window, PhysicalKey.ArrowLeft);
            decided[^1].ShouldBe(("Kyoto", CardDecision.Left));
            stack.LastAnnouncement.ShouldBe("Kyoto: Pass. 3 left.");

            Press(window, PhysicalKey.Backspace);
            undone.ShouldBe(["Kyoto"]);
            stack.CurrentIndex.ShouldBe(1);
            stack.LastAnnouncement.ShouldBe("Kyoto restored.");
            stack.Panel.CardAt(1)!.IsInteractive.ShouldBeTrue();

            Press(window, PhysicalKey.Z, RawInputModifiers.Control);
            undone.ShouldBe(["Kyoto", "Lisbon"]);
            stack.CurrentIndex.ShouldBe(0);
            stack.CanUndo.ShouldBeFalse();
            Button(stack, CardStack.UndoButtonPart).IsSoftDisabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Outcomes_Name_The_Decision_And_The_Last_Card_Says_All_Reviewed()
    {
        var stack = new CardStack { ItemsSource = new[] { "Lisbon", "Kyoto" }, LeftOutcome = "skipped", RightOutcome = "shortlisted" };
        var (_, window) = Show(stack);
        try
        {
            stack.Decide(CardDecision.Left);
            stack.LastAnnouncement.ShouldBe("Lisbon skipped. 1 left.");
            stack.Decide(CardDecision.Right);
            stack.LastAnnouncement.ShouldBe("Kyoto shortlisted. All cards reviewed.");
            Dispatcher.UIThread.RunJobs();
            stack.Classes.Contains(CardStack.ReviewedPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<Border>(stack, CardStack.EmptyPart).IsVisible.ShouldBeTrue();
            Button(stack, CardStack.LeftButtonPart).IsSoftDisabled.ShouldBeTrue();
            Button(stack, CardStack.RightButtonPart).IsSoftDisabled.ShouldBeTrue();
            stack.Decide(CardDecision.Right);
            stack.CurrentIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Start_Over_Brings_Every_Card_Back()
    {
        var (stack, window) = Show(Make());
        var resets = 0;
        stack.StackReset += (_, _) => resets++;
        try
        {
            for (var i = 0; i < 5; i++)
            {
                stack.Decide(CardDecision.Right);
            }
            Dispatcher.UIThread.RunJobs();
            stack.Panel!.RealizedCount.ShouldBe(0);
            var startOver = Button(stack, CardStack.StartOverButtonPart);
            startOver.IsEffectivelyVisible.ShouldBeTrue();
            stack.ResetCommand.CanExecute(null).ShouldBeTrue();
            startOver.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            resets.ShouldBe(1);
            stack.CurrentIndex.ShouldBe(0);
            stack.LastAnnouncement.ShouldBe("All cards are back in the stack.");
            stack.Panel.RealizedCount.ShouldBe(4);
            stack.Panel.CardAt(0)!.Opacity.ShouldBe(1);
            stack.Panel.CardAt(0)!.X.Value.ShouldBe(0);
            TestHelpers.Descendant<Border>(stack, CardStack.EmptyPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Far_Enough_Throws_And_A_Short_Drag_Springs_Home()
    {
        var (stack, window) = Show(Make());
        try
        {
            var top = stack.Panel!.CardAt(0)!;
            var center = top.TranslatePoint(new Point(160, 100), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseMove(center + new Point(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(center + new Point(60, 4), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            top.IsDragging.ShouldBeTrue();
            top.Classes.Contains(CardStackCard.LiftedPseudoClass).ShouldBeTrue();
            top.X.Value.ShouldBe(60);
            // Grabbing the top half tips the card the way the hand moves.
            top.Tilt.ShouldBe(1);
            Thread.Sleep(80);
            window.MouseUp(center + new Point(60, 4), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.CurrentIndex.ShouldBe(0);
            top.X.Value.ShouldBe(0);

            window.MouseDown(center, MouseButton.Left);
            window.MouseMove(center + new Point(-10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(center + new Point(-150, 0), RawInputModifiers.LeftMouseButton);
            Thread.Sleep(80);
            window.MouseUp(center + new Point(-150, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            stack.CurrentIndex.ShouldBe(1);
            stack.LastAnnouncement.ShouldBe("Lisbon: Pass. 4 left.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Stacks_Realize_Four_Cards()
    {
        var stack = new CardStack { ItemsSource = Enumerable.Range(1, 2000).Select(i => $"Card {i}").ToList() };
        var (_, window) = Show(stack);
        try
        {
            stack.Panel!.RealizedCount.ShouldBe(4);
            for (var i = 0; i < 10; i++)
            {
                stack.Decide(CardDecision.Left);
            }
            Dispatcher.UIThread.RunJobs();
            stack.Panel.RealizedCount.ShouldBe(4);
            AutomationProperties.GetName(stack.Panel.CardAt(10)!).ShouldBe("Card 11, 11 of 2000");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Stacks_Shorten_The_Cards_And_Keep_Undo_As_An_Icon()
    {
        var (stack, window) = Show(Make(), width: 340);
        try
        {
            stack.Classes.Contains(CardStack.CompactPseudoClass).ShouldBeTrue();
            stack.Classes.Contains(CardStack.NarrowPseudoClass).ShouldBeTrue();
            stack.Panel!.CardAt(0)!.Bounds.Height.ShouldBe(280);
            var undo = Button(stack, CardStack.UndoButtonPart);
            undo.IsLabelHidden.ShouldBeTrue();
            undo.Bounds.Width.ShouldBe(44);
            AutomationProperties.GetName(undo).ShouldBe("Undo");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Throws_With_Velocity_Lifts_The_Stack_And_Flies_Back()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (stack, window) = Show(Make(), ArcMotionLevel.Full);
        try
        {
            var top = stack.Panel!.CardAt(0)!;
            var second = stack.Panel.CardAt(1)!;
            // Dragging the top card lifts the cards behind.
            var center = top.TranslatePoint(new Point(160, 100), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseMove(center + new Point(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(center + new Point(90, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            second.Depth.Value.ShouldBe(1 - 90.0 / 160 * 0.4, 1e-3);
            top.Classes.Contains(CardStackCard.LiftedPseudoClass).ShouldBeTrue();
            Thread.Sleep(80);
            window.MouseUp(center + new Point(90, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            // Below the threshold it springs home.
            stack.CurrentIndex.ShouldBe(0);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            top.X.Value.ShouldBe(0, 0.01);
            second.Depth.Value.ShouldBe(1, 1e-3);

            stack.Decide(CardDecision.Right);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Dispatcher.UIThread.RunJobs();
            top.X.Value.ShouldBeGreaterThan(40);
            top.Classes.Contains(CardStackCard.FlyingPseudoClass).ShouldBeTrue();
            second.Depth.Value.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromSeconds(1));
            MediaTestSupport.WaitFor(() => stack.FlyingIndices.Count == 0).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            stack.Panel.CardAt(0).ShouldBeNull();
            second.Depth.Value.ShouldBe(0, 1e-3);

            // Undo flies the card back from where it left.
            stack.Undo();
            Dispatcher.UIThread.RunJobs();
            var back = stack.Panel.CardAt(0)!;
            back.X.Value.ShouldBeGreaterThan(200);
            clock.Advance(TimeSpan.FromSeconds(1.2));
            Dispatcher.UIThread.RunJobs();
            back.X.Value.ShouldBe(0, 0.01);
            back.Fade.Value.ShouldBe(1);
            back.IsInteractive.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Release_And_Exit_Rules_Match_Arc()
    {
        CardStackMath.ReleaseDirection(new Point(60, 0), default, 320).ShouldBe(0);
        CardStackMath.ReleaseDirection(new Point(120, 0), default, 320).ShouldBe(1);
        CardStackMath.ReleaseDirection(new Point(-120, 0), default, 320).ShouldBe(-1);
        // A quick flick decides after only a little travel; flinging back cancels a long drag.
        CardStackMath.ReleaseDirection(new Point(20, 0), new Vector(800, 0), 320).ShouldBe(1);
        CardStackMath.ReleaseDirection(new Point(140, 0), new Vector(-400, 0), 320).ShouldBe(0);
        var exit = CardStackMath.ExitFor(new Point(0, 0), new Vector(900, -120), 1, 320);
        exit.X.ShouldBeGreaterThan(500);
        exit.Y.ShouldBeLessThan(0);
        var steep = CardStackMath.ExitFor(new Point(0, 0), new Vector(250, 2000), 1, 320);
        (steep.X / 512).ShouldBe(0.45, 1e-9);
        CardStackMath.ResistY(30).ShouldBe(30);
        CardStackMath.ResistY(400).ShouldBeLessThan(48 + 140);
        CardStackMath.UnresistY(CardStackMath.ResistY(120)).ShouldBe(120, 1e-6);
        CardStackMath.Presence(2.5).ShouldBe(0.5);
        CardStackMath.KeepStamp(56).ShouldBe(0.5);
        CardStackMath.PassStamp(-56).ShouldBe(0.5);
    }
}
