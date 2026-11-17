using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>The wallet stack control: template, keyboard, pointer, gestures, announcements, events, motion.</summary>
public class WalletStackTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (WalletStack Stack, Window Window) Show(ArcMotionLevel level = ArcMotionLevel.Off, IList<WalletStackCard>? cards = null)
    {
        var stack = new WalletStack { Label = WalletStackSamples.Label, Cards = cards ?? WalletStackSamples.Cards() };
        var window = TestHelpers.Show(stack, level);
        window.Width = 420;
        window.Height = 720;
        Pump();
        return (stack, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static WalletStackCardView View(WalletStack stack, int index) => stack.Stage!.Views[index];

    private static Point Center(WalletStack stack, int index, Window window, double fy = 0.5)
    {
        var view = View(stack, index);
        return view.TranslatePoint(new Point(view.Bounds.Width / 2, view.Bounds.Height * fy), window)!.Value;
    }

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static void Drag(Window window, Point from, double dy, int steps = 12)
    {
        window.MouseMove(from, RawInputModifiers.None);
        window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        for (var i = 1; i <= steps; i++)
        {
            window.MouseMove(from + new Vector(0, dy * i / steps), RawInputModifiers.LeftMouseButton);
        }
        // Pause so the release carries no momentum.
        Thread.Sleep(120);
        window.MouseMove(from + new Vector(0, dy), RawInputModifiers.LeftMouseButton);
        Thread.Sleep(120);
        window.MouseUp(from + new Vector(0, dy), MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    [AvaloniaFact]
    public void Template_builds_the_header_the_stage_and_one_card_per_entry()
    {
        var (stack, _) = Show();
        stack.Bounds.Width.ShouldBe(296);
        stack.Stage!.Views.Count.ShouldBe(4);
        stack.Stage.Geometry.CardWidth.ShouldBe(296, 0.01);
        stack.Stage.Bounds.Height.ShouldBe(406.66, 1.5);
        stack.CountPresenter!.Text.ShouldBe("1 of 4");
        stack.AllCardsButton!.IsVisible.ShouldBeFalse();
        stack.Position.ShouldBe(0);
        stack.SelectedCard.ShouldBeNull();
        // Resting poses: the front card at 99.97, the others above it; the first card is drawn last (on top).
        stack.Stage.Slots[0].Pose.Y.ShouldBe(99.97, 0.05);
        stack.Stage.Slots[3].Pose.Y.ShouldBe(66.37, 0.05);
        var children = stack.Stage.GetVisualChildren().ToList();
        children.IndexOf(stack.Stage.Slots[0]).ShouldBeGreaterThan(children.IndexOf(stack.Stage.Slots[3]));
    }

    [AvaloniaFact]
    public void Cards_are_named_and_only_the_front_card_is_a_tab_stop()
    {
        var (stack, _) = Show();
        AutomationProperties.GetName(View(stack, 0)).ShouldBe("Larkspur Current account ending in 5306, 1 of 4, available $2,914.35");
        AutomationProperties.GetName(View(stack, 3)).ShouldBe("Pebble Gym Member ending in 2093, 4 of 4, class credit $21.60");
        View(stack, 0).IsTabStop.ShouldBeTrue();
        View(stack, 1).IsTabStop.ShouldBeFalse();
        AutomationProperties.GetName(stack.Stage!).ShouldBe(WalletStackSamples.Label);
        AutomationProperties.GetHelpText(stack.Stage!).ShouldNotBeNullOrEmpty();
        var peer = ControlAutomationPeer.CreatePeerForElement(View(stack, 0));
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        ((IExpandCollapseProvider)peer).ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
    }

    [AvaloniaFact]
    public void Arrow_keys_cycle_without_wrapping_and_focus_follows()
    {
        var (stack, window) = Show();
        var positions = new List<int>();
        stack.PositionChanged += (_, e) => positions.Add(e.NewPosition);
        View(stack, 0).Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.ArrowDown);
        stack.Position.ShouldBe(1);
        View(stack, 1).IsFocused.ShouldBeTrue();
        View(stack, 1).IsTabStop.ShouldBeTrue();
        View(stack, 0).IsTabStop.ShouldBeFalse();
        stack.CountPresenter!.Text.ShouldBe("2 of 4");
        stack.Announcer!.Announcement.ShouldBe("Obsidian Charge card in front, 2 of 4");
        Key(window, PhysicalKey.ArrowRight);
        stack.Position.ShouldBe(2);
        Key(window, PhysicalKey.End);
        stack.Position.ShouldBe(3);
        Key(window, PhysicalKey.ArrowDown);
        stack.Position.ShouldBe(3);
        Key(window, PhysicalKey.ArrowLeft);
        stack.Position.ShouldBe(2);
        Key(window, PhysicalKey.Home);
        stack.Position.ShouldBe(0);
        Key(window, PhysicalKey.ArrowUp);
        stack.Position.ShouldBe(0);
        positions.ShouldBe(new[] { 1, 2, 3, 2, 0 });
        // Off: the stack position follows at once; the front card's pose is the front pose.
        stack.StackPosition.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Enter_opens_arrows_switch_and_escape_closes_keeping_focus()
    {
        var (stack, window) = Show();
        var events = new List<(WalletStackCard? Old, WalletStackCard? New)>();
        stack.SelectedCardChanged += (_, e) => events.Add((e.OldCard, e.NewCard));
        var cards = stack.Cards!;
        View(stack, 0).Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.Enter);
        stack.SelectedCard.ShouldBe(cards[0]);
        stack.IsOpen.ShouldBeTrue();
        stack.Classes.Contains(":open").ShouldBeTrue();
        stack.Announcer!.Announcement.ShouldBe("Larkspur Current account open");
        stack.AllCardsButton!.IsVisible.ShouldBeTrue();
        stack.AllCardsButton.IsTabStop.ShouldBeTrue();
        stack.OpenAmount.ShouldBe(1, 1e-9);
        stack.Stage!.Slots[0].Pose.Y.ShouldBe(0, 1e-9);
        stack.Stage.Slots[0].Pose.Scale.ShouldBe(0.88, 1e-9);
        stack.Stage.Slots[1].Pose.Y.ShouldBe(377, 1e-9);
        stack.Stage.Detail.Card.ShouldBe(cards[0]);
        stack.Stage.Detail.BalanceText.ShouldBe("$2,914.35");
        stack.Stage.Detail.RowControls.Count.ShouldBe(3);
        AutomationProperties.GetName(stack.Stage.Detail).ShouldBe("Larkspur activity");
        View(stack, 0).IsExpanded.ShouldBeTrue();

        Key(window, PhysicalKey.ArrowDown);
        stack.SelectedCard.ShouldBe(cards[1]);
        View(stack, 1).IsFocused.ShouldBeTrue();
        stack.Stage.Slots[1].Pose.Y.ShouldBe(0, 1e-9);
        // The old card drops into the slot the new one rose from.
        stack.Stage.Slots[0].Pose.Y.ShouldBe(377, 1e-9);

        Key(window, PhysicalKey.Escape);
        stack.SelectedCard.ShouldBeNull();
        stack.Position.ShouldBe(1);
        View(stack, 1).IsFocused.ShouldBeTrue();
        stack.Announcer.Announcement.ShouldBe("Obsidian Charge card in front, 2 of 4");
        events.Count.ShouldBe(3);
        events[0].ShouldBe((null, cards[0]));
        events[1].ShouldBe((cards[0], cards[1]));
        events[2].ShouldBe((cards[1], null));

        // Space opens again, Space closes.
        Key(window, PhysicalKey.Space);
        stack.SelectedCard.ShouldBe(cards[1]);
        Key(window, PhysicalKey.Space);
        stack.SelectedCard.ShouldBeNull();
    }

    [AvaloniaFact]
    public void All_cards_button_closes_and_returns_focus_to_the_card()
    {
        var (stack, window) = Show();
        stack.Open(2);
        Pump();
        stack.AllCardsButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        stack.SelectedCard.ShouldBeNull();
        View(stack, 2).IsFocused.ShouldBeTrue();
        stack.Position.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Clicking_an_exposed_card_opens_it_and_clicking_the_open_card_closes()
    {
        var (stack, window) = Show();
        // Card 2 shows its top edge above the front card.
        var point = Center(stack, 2, window, 0.04);
        window.MouseMove(point, RawInputModifiers.None);
        Pump();
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
        stack.SelectedCard.ShouldBe(stack.Cards![2]);
        stack.Position.ShouldBe(2);
        var open = Center(stack, 2, window);
        window.MouseDown(open, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(open, MouseButton.Left, RawInputModifiers.None);
        Pump();
        stack.SelectedCard.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Vertical_drag_cycles_after_half_a_card_and_small_moves_stay_clicks()
    {
        var (stack, window) = Show();
        var d = WalletStackLayout.CardDistance(stack.Stage!.Geometry, 4, stack.FanAmount);
        Drag(window, Center(stack, 0, window), d * 0.4);
        stack.Position.ShouldBe(0);
        stack.SelectedCard.ShouldBeNull();
        Drag(window, Center(stack, 0, window), d * 0.6);
        stack.Position.ShouldBe(1);
        stack.Announcer!.Announcement.ShouldBe("Obsidian Charge card in front, 2 of 4");
        // A press that moves less than the threshold is a click.
        var p = Center(stack, 1, window);
        window.MouseDown(p, MouseButton.Left, RawInputModifiers.None);
        window.MouseMove(p + new Vector(0, 5), RawInputModifiers.LeftMouseButton);
        window.MouseUp(p + new Vector(0, 5), MouseButton.Left, RawInputModifiers.None);
        Pump();
        stack.SelectedCard.ShouldBe(stack.Cards![1]);
    }

    [AvaloniaFact]
    public void Pulling_the_open_card_down_closes_it_past_half()
    {
        var (stack, window) = Show();
        stack.Open(0);
        Pump();
        Drag(window, Center(stack, 0, window), 100);
        stack.SelectedCard.ShouldBe(stack.Cards![0]);
        Drag(window, Center(stack, 0, window), 130);
        stack.SelectedCard.ShouldBeNull();
        stack.Position.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Position_and_selected_card_are_two_way()
    {
        var (stack, _) = Show();
        stack.Position = 3;
        stack.StackPosition.ShouldBe(3);
        stack.CountPresenter!.Text.ShouldBe("4 of 4");
        stack.SelectedCard = stack.Cards![1];
        stack.Position.ShouldBe(1);
        stack.OpenAmount.ShouldBe(1, 1e-9);
        stack.SelectedCard = null;
        stack.OpenAmount.ShouldBe(0, 1e-9);
        stack.Position = 99;
        stack.Position.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Replacing_cards_keeps_the_open_card_by_id()
    {
        var (stack, _) = Show();
        stack.Open(1);
        var fresh = WalletStackSamples.Cards();
        stack.Cards = fresh;
        Pump();
        stack.SelectedCard.ShouldBe(fresh[1]);
        stack.Stage!.Views.Count.ShouldBe(4);
        stack.Cards = new List<WalletStackCard> { WalletStackSamples.Cards()[0] };
        Pump();
        stack.SelectedCard.ShouldBeNull();
        stack.Position.ShouldBe(0);
        stack.CountPresenter!.Text.ShouldBe("1 of 1");
    }

    [AvaloniaFact]
    public void Currency_culture_and_labels_reformat_the_texts()
    {
        var (stack, _) = Show(cards: WalletStackSamples.TravelCards());
        stack.Currency = "EUR";
        stack.Culture = new System.Globalization.CultureInfo("de-DE");
        stack.Labels = new WalletStackLabels
        {
            CountBefore = _ => "Karte ",
            CountAfter = total => $" von {total}",
            CardName = (card, position, total, balance) => $"{card.Issuer} {card.LastFour}, {balance}",
        };
        Pump();
        stack.CountPresenter!.Text.ShouldBe("Karte 1 von 3");
        AutomationProperties.GetName(View(stack, 0))!.Replace('\u00a0', ' ').ShouldBe("Halde Bank 7712, 1.284,50 €");
        stack.Open(0);
        stack.Stage!.Detail.BalanceText!.Replace('\u00a0', ' ').ShouldBe("1.284,50 €");
    }

    [AvaloniaFact]
    public void Full_motion_springs_open_and_staggers_the_rows()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (stack, _) = Show(ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
        stack.Open(0);
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        var mid = stack.OpenAmount;
        mid.ShouldBeInRange(0.05, 0.9);
        var rows = stack.Stage!.Detail.RowControls;
        rows[0].Opacity.ShouldBeGreaterThan(rows[2].Opacity);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        stack.OpenAmount.ShouldBe(1, 0.001);
        stack.Stage.Slots[0].Pose.Y.ShouldBe(0, 0.1);
        rows.ShouldAllBe(r => r.Opacity > 0.99);
        stack.Stage.Detail.BalanceText.ShouldBe("$2,914.35");
        stack.AllCardsButton!.Opacity.ShouldBe(1, 0.01);
        stack.CountPresenter!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Full_motion_keyboard_presses_keep_the_spring_moving()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (stack, window) = Show(ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
        View(stack, 0).Focus(NavigationMethod.Tab);
        Key(window, PhysicalKey.ArrowDown);
        clock.Advance(TimeSpan.FromMilliseconds(80), Frame);
        var between = stack.StackPosition;
        between.ShouldBeInRange(0.01, 0.99);
        Key(window, PhysicalKey.ArrowDown);
        stack.Position.ShouldBe(2);
        clock.Advance(TimeSpan.FromMilliseconds(16), Frame);
        stack.StackPosition.ShouldBeGreaterThan(between);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        stack.StackPosition.ShouldBe(2, 0.001);
    }

    [AvaloniaFact]
    public void Mouse_hover_fans_and_tilts_but_reduced_motion_keeps_cards_flat()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (stack, window) = Show(ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
        var view = View(stack, 0);
        window.MouseMove(Center(stack, 0, window, 0.1), RawInputModifiers.None);
        Pump();
        clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
        stack.FanAmount.ShouldBe(1, 0.001);
        stack.Stage!.Slots[0].Pose.Y.ShouldBe(144.77, 0.1);
        view.Tilt.Tilt.Y.ShouldBeLessThan(-0.5);
        view.Tilt.Light.ShouldBe(1, 0.01);

        ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
        window.MouseMove(Center(stack, 0, window, 0.2), RawInputModifiers.None);
        Pump();
        view.Tilt.Tilt.ShouldBe(default);
        view.Tilt.Light.ShouldBe(WalletStackTiltLight.RestLight);
    }
}
