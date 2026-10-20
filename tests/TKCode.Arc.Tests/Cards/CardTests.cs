using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Cards;

public class CardTests
{
    private static Card CreateCard(Control? action = null, object? details = null) => new()
    {
        Title = "Harbour redesign",
        Description = "New booking flow and room pages.",
        Media = new Border { Height = 120, Background = Brushes.SteelBlue },
        Avatar = new Avatar { DisplayName = "Maya Chen" },
        Meta = "Maya Chen",
        Status = "Updated 2 hours ago",
        Action = action,
        Details = details,
        Width = 280
    };

    private static Window Show(Card card, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { card } }, level);
        window.Width = 900;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var card = CreateCard();
        var window = Show(card);
        try
        {
            var surface = TestHelpers.Descendant<Border>(card, "PART_Surface");
            surface.CornerRadius.ShouldBe(new CornerRadius(26));
            card.BorderThickness.ShouldBe(new Thickness(1));
            TestHelpers.Descendant<StackPanel>(card, "PART_Body").Margin.ShouldBe(new Thickness(20));
            TestHelpers.Descendant<Border>(card, "PART_Media").IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<Decorator>(card, "PART_Zoom").MinHeight.ShouldBe(112);
            TestHelpers.Descendant<Border>(card, "PART_AvatarSlot").Bounds.Width.ShouldBe(32);
            var title = TestHelpers.Descendant<CardTitle>(card, "PART_Title");
            title.FontSize.ShouldBe(18);
            title.Focusable.ShouldBeFalse();
            AutomationProperties.GetHeadingLevel(title).ShouldBe(3);
            card.IsInteractive.ShouldBeFalse();
            TestHelpers.Descendant<CardStatusLine>(card, "PART_Status").IsVisible.ShouldBeTrue();
            AutomationProperties.GetLiveSetting(TestHelpers.Descendant<CardStatusLine>(card, "PART_Status"))
                .ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(card).ShouldBe("Harbour redesign");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plain_Card_Hides_Empty_Parts()
    {
        var card = new Card { Title = "Plain", Width = 240 };
        var window = Show(card);
        try
        {
            TestHelpers.Descendant<Border>(card, "PART_Media").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, "PART_Description").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Grid>(card, "PART_Footer").IsVisible.ShouldBeFalse();
            card.Bounds.Height.ShouldBeLessThan(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Mouse_Hover_Lifts_But_Touch_Does_Not()
    {
        var card = CreateCard(details: "More");
        var window = Show(card);
        try
        {
            var center = card.TranslatePoint(new Point(140, 60), window)!.Value;
            window.MouseMove(center);
            Dispatcher.UIThread.RunJobs();
            card.Classes.Contains(Card.HoverPseudoClass).ShouldBeTrue();
            card.BorderBrush.ShouldNotBeNull();
            window.MouseMove(new Point(890, 690));
            Dispatcher.UIThread.RunJobs();
            card.Classes.Contains(Card.HoverPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Title_Is_The_Trigger_And_Escape_Returns_Focus()
    {
        var details = new TextBlock { Text = "Scope, milestones, and open questions." };
        var card = CreateCard(details: details);
        var opened = new List<bool>();
        card.DetailsOpenChanged += (_, e) => opened.Add(e.IsOpen);
        var window = Show(card);
        try
        {
            var title = TestHelpers.Descendant<CardTitle>(card, "PART_Title");
            card.IsInteractive.ShouldBeTrue();
            title.Focusable.ShouldBeTrue();
            title.Focus(NavigationMethod.Tab);
            card.Classes.Contains(Card.TriggerFocusPseudoClass).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsDetailsOpen.ShouldBeTrue();
            card.Presence!.IsOpen.ShouldBeTrue();
            var look = card.QuickLook!;
            TopLevel.GetTopLevel(look).ShouldNotBeNull();
            look.Title.ShouldBe("Harbour redesign");
            AutomationProperties.GetName(look).ShouldBe("Harbour redesign");
            AutomationProperties.GetHelpText(look).ShouldBe("New booking flow and room pages.");
            TestHelpers.Descendant<TextBlock>(look, "PART_Title").FontSize.ShouldBe(22);
            TopLevel.GetTopLevel(details).ShouldNotBeNull();
            // Focus moves into the quick look (its close button comes first).
            look.CloseButton!.IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsDetailsOpen.ShouldBeFalse();
            card.Presence.State.ShouldBe(ModalState.Closed);
            title.IsFocused.ShouldBeTrue();
            opened.ShouldBe(new[] { true, false });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Press_Opens_But_Action_Press_Does_Not()
    {
        var action = new Button { Content = "Open", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        var clicks = 0;
        action.Click += (_, _) => clicks++;
        var card = CreateCard(action, "More");
        var window = Show(card);
        try
        {
            var actionPoint = action.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(actionPoint, MouseButton.Left);
            window.MouseUp(actionPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            clicks.ShouldBe(1);
            card.IsDetailsOpen.ShouldBeFalse();

            var mediaPoint = card.TranslatePoint(new Point(140, 40), window)!.Value;
            window.MouseDown(mediaPoint, MouseButton.Left);
            card.Classes.Contains(Card.PressedPseudoClass).ShouldBeTrue();
            window.MouseUp(mediaPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            card.IsDetailsOpen.ShouldBeTrue();
            TestHelpers.Descendant<CardTitle>(card, "PART_Title").IsFocused.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slot_Controls_Move_Into_The_Quick_Look_And_Back()
    {
        var action = new Button { Content = "Open" };
        var card = CreateCard(action, "More");
        var window = Show(card);
        try
        {
            var media = (Control)card.Media!;
            var cardPresenter = TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(card, "PART_ActionPresenter");
            var heightBefore = card.Bounds.Height;
            card.IsDetailsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var look = card.QuickLook!;
            look.IsVisualAncestorOf(action).ShouldBeTrue();
            look.IsVisualAncestorOf(media).ShouldBeTrue();
            cardPresenter.Content.ShouldBeNull();
            // Placeholders keep the card's layout while its controls are away.
            card.Bounds.Height.ShouldBe(heightBefore, 0.5);
            action.Parent.ShouldBe(card);

            card.IsDetailsOpen = false;
            Dispatcher.UIThread.RunJobs();
            card.IsVisualAncestorOf(action).ShouldBeTrue();
            card.IsVisualAncestorOf(media).ShouldBeTrue();
            cardPresenter.Width.ShouldBe(double.NaN);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shared_Element_Flight_Grows_From_The_Card_And_Lands_Back()
    {
        var card = CreateCard(new Button { Content = "Open" }, "Scope and milestones.");
        var window = Show(card, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            card.IsDetailsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var flight = card.Flight!;
            var look = card.QuickLook!;
            var frame = TestHelpers.Descendant<SharedElementFrame>(look, "PART_Frame");
            var surface = TestHelpers.Descendant<Border>(card, "PART_Surface");
            flight.IsActive.ShouldBeTrue();
            flight.PairedKeys.ShouldContain("card");
            flight.PairedKeys.ShouldContain("media");
            flight.PairedKeys.ShouldContain("title");
            flight.PairedKeys.ShouldContain("byline");
            flight.PairedKeys[0].ShouldBe("card");
            surface.Opacity.ShouldBe(0);

            // Frame 0: the quick look sits exactly on the card.
            var cardBox = new Rect(surface.Bounds.Size).TransformToAABB(surface.TransformToVisual(window)!.Value);
            var lookBox = new Rect(frame.Bounds.Size).TransformToAABB(frame.TransformToVisual(window)!.Value);
            lookBox.X.ShouldBe(cardBox.X, 0.5);
            lookBox.Y.ShouldBe(cardBox.Y, 0.5);
            lookBox.Width.ShouldBe(cardBox.Width, 0.5);
            lookBox.Height.ShouldBe(cardBox.Height, 0.5);
            frame.IsFlying.ShouldBeTrue();
            frame.CurrentRadius.TopLeft.ShouldBe(26, 0.01);

            clock.Advance(TimeSpan.FromMilliseconds(100));
            Dispatcher.UIThread.RunJobs();
            flight.Progress.ShouldBeGreaterThan(0.2);
            flight.Progress.ShouldBeLessThan(1);
            frame.CurrentRadius.TopLeft.ShouldBeGreaterThan(26);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            flight.Progress.ShouldBe(1);
            flight.IsActive.ShouldBeFalse();
            frame.IsFlying.ShouldBeFalse();
            surface.Opacity.ShouldBe(0);
            card.Presence!.State.ShouldBe(ModalState.Open);
            card.Presence.ScrimOpacity.ShouldBe(1, 0.001);

            card.IsDetailsOpen = false;
            Dispatcher.UIThread.RunJobs();
            look.Classes.Contains(CardQuickLook.ReturningPseudoClass).ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(80));
            Dispatcher.UIThread.RunJobs();
            flight.Progress.ShouldBeLessThan(1);
            surface.Opacity.ShouldBe(0);

            // Reopening mid-return reverses from where it is.
            var midway = flight.Progress;
            card.IsDetailsOpen = true;
            Dispatcher.UIThread.RunJobs();
            // The spring keeps its velocity: it eases out of the return before growing again.
            clock.Advance(TimeSpan.FromMilliseconds(200));
            Dispatcher.UIThread.RunJobs();
            flight.Progress.ShouldBeGreaterThan(midway);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            card.Presence.State.ShouldBe(ModalState.Open);

            card.IsDetailsOpen = false;
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            card.Presence.State.ShouldBe(ModalState.Closed);
            surface.Opacity.ShouldBe(1);
            flight.IsActive.ShouldBeFalse();
            card.IsVisualAncestorOf((Control)card.Media!).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Without_A_Flight()
    {
        var card = CreateCard(details: "More");
        var window = Show(card, ArcMotionLevel.Reduced);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            card.IsDetailsOpen = true;
            Dispatcher.UIThread.RunJobs();
            card.Flight!.IsActive.ShouldBeFalse();
            TestHelpers.Descendant<Border>(card, "PART_Surface").Opacity.ShouldBe(1);
            card.QuickLook!.Opacity.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromMilliseconds(400));
            Dispatcher.UIThread.RunJobs();
            card.QuickLook.Opacity.ShouldBe(1, 0.001);
            card.IsDetailsOpen = false;
            clock.Advance(TimeSpan.FromMilliseconds(400));
            Dispatcher.UIThread.RunJobs();
            card.Presence!.State.ShouldBe(ModalState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Status_Change_Rolls_In_And_Updates_The_Live_Region()
    {
        var card = CreateCard();
        var window = Show(card, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            var status = TestHelpers.Descendant<CardStatusLine>(card, "PART_Status");
            card.Status = "Updated just now";
            Dispatcher.UIThread.RunJobs();
            status.IsRolling.ShouldBeTrue();
            AutomationProperties.GetName(status).ShouldBe("Updated just now");
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            status.IsRolling.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_Button_Closes_With_Its_Reason()
    {
        var card = CreateCard(details: "More");
        var reasons = new List<DismissReason>();
        card.DetailsOpenChanged += (_, e) => reasons.Add(e.Reason);
        var window = Show(card);
        try
        {
            card.OpenDetails();
            Dispatcher.UIThread.RunJobs();
            var close = card.QuickLook!.CloseButton!;
            AutomationProperties.GetName(close).ShouldBe("Close quick look");
            close.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsDetailsOpen.ShouldBeFalse();
            reasons.ShouldBe(new[] { DismissReason.Programmatic, DismissReason.CloseButton });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Details_IsDetailsOpen_Stays_False()
    {
        var card = CreateCard();
        var window = Show(card);
        try
        {
            card.IsDetailsOpen = true;
            Dispatcher.UIThread.RunJobs();
            card.IsDetailsOpen.ShouldBeFalse();
            card.QuickLook.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
