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
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>Card to article in a window: grid, open and close, morph, pull, keyboard, focus and motion levels.</summary>
public class CardToArticleTests
{
    private static (CardToArticle Control, Window Window) Create(ArcMotionLevel level = ArcMotionLevel.Off, double width = 1440, double height = 1000)
    {
        var control = new CardToArticle { Items = CardToArticleSamples.Stories() };
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = height;
        Settle();
        return (control, window);
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static Rect Box(Visual visual, Visual root) =>
        new Rect(visual.Bounds.Size).TransformToAABB(visual.TransformToVisual(root)!.Value);

    private static void Run(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), TimeSpan.FromMilliseconds(1000.0 / 60));
        Settle();
    }

    [AvaloniaFact]
    public void The_grid_has_two_columns_of_4_3_cards_and_one_when_narrow()
    {
        var (control, window) = Create();
        try
        {
            control.Bounds.Width.ShouldBe(680);
            control.Cards.Count.ShouldBe(4);
            control.Cards[0].Bounds.Size.ShouldBe(new Size(332, 249));
            control.Cards[1].Bounds.X.ShouldBe(348);
            control.Cards[2].Bounds.Y.ShouldBe(265);
            control.Classes.Contains(":narrow").ShouldBeFalse();
            control.Width = 472;
            Settle();
            control.Cards[1].Bounds.ShouldBe(new Rect(0, 354 + 12, 472, 354));
            control.Classes.Contains(":narrow").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_are_named_by_title_and_reading_time()
    {
        var (control, window) = Create();
        try
        {
            AutomationProperties.GetName(control.Cards[0]).ShouldBe("Reading the clouds before a long hike, 5 min read");
            control.Labels = new CardToArticleLabels { CardName = item => "Read " + item.Title };
            AutomationProperties.GetName(control.Cards[1]).ShouldBe("Read A morning aboard the island ferry");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_hero_sizes_its_type_by_its_width_and_balances_the_title()
    {
        var (control, window) = Create();
        try
        {
            var hero = control.Cards[0].Hero!;
            hero.Bounds.Width.ShouldBe(332);
            hero.TitleFontSize.ShouldBe(24.568, 0.001);
            hero.TitleLines.ShouldBeGreaterThan(0);
            hero.TitleWrapWidth.ShouldBeLessThanOrEqualTo(332 * 0.89 + 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_a_card_opens_its_article_and_escape_closes_it_back_to_the_card()
    {
        var (control, window) = Create();
        var changes = new List<CardToArticleOpenChangedEventArgs>();
        control.OpenItemChanged += (_, e) => changes.Add(e);
        try
        {
            var card = control.Cards[1];
            var center = card.TranslatePoint(new Point(100, 100), window)!.Value;
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Settle();
            control.OpenItemId.ShouldBe("island-ferry");
            control.OpenItem!.Title.ShouldBe("A morning aboard the island ferry");
            changes.Single().Reason.ShouldBe(CardToArticleChangeReason.Click);
            control.Presence!.State.ShouldBe(ModalState.Open);
            var article = control.Article!;
            AutomationProperties.GetName(article).ShouldBe("A morning aboard the island ferry");
            // The sheet: 680 wide, 24 from the top, the window height minus 48.
            Box(article.Frame!, window).ShouldBe(new Rect(380, 24, 680, 952));
            article.Hero!.Bounds.Size.ShouldBe(new Size(680, 510));
            // Focus is on the close button, the only stop in the dialog.
            window.FocusManager!.GetFocusedElement().ShouldBe(article.CloseButton);
            AutomationProperties.GetName(article.CloseButton!).ShouldBe("Close article");
            card.IsAway.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            control.OpenItemId.ShouldBeNull();
            changes[^1].Reason.ShouldBe(CardToArticleChangeReason.Escape);
            changes[^1].OldId.ShouldBe("island-ferry");
            control.Presence.State.ShouldBe(ModalState.Closed);
            card.IsAway.ShouldBeFalse();
            window.FocusManager!.GetFocusedElement().ShouldBe(card);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_and_space_open_a_focused_card_and_the_close_button_closes()
    {
        var (control, window) = Create();
        var changes = new List<CardToArticleOpenChangedEventArgs>();
        control.OpenItemChanged += (_, e) => changes.Add(e);
        try
        {
            control.Cards[2].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            control.OpenItemId.ShouldBe("overnight-bread");
            changes[^1].Reason.ShouldBe(CardToArticleChangeReason.Keyboard);
            var close = control.Article!.CloseButton!;
            close.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            control.OpenItemId.ShouldBeNull();
            changes[^1].Reason.ShouldBe(CardToArticleChangeReason.CloseButton);

            control.Cards[3].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Settle();
            control.OpenItemId.ShouldBe("desert-moss");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_stays_on_the_close_button()
    {
        var (control, window) = Create();
        try
        {
            control.Open("cloud-reading");
            Settle();
            var close = control.Article!.CloseButton!;
            window.FocusManager!.GetFocusedElement().ShouldBe(close);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Settle();
            window.FocusManager!.GetFocusedElement().ShouldBe(close);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
            Settle();
            window.FocusManager!.GetFocusedElement().ShouldBe(close);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_and_page_keys_scroll_the_article_and_move_the_progress_bar()
    {
        var (control, window) = Create(height: 700);
        try
        {
            control.Open("cloud-reading");
            Settle();
            var article = control.Article!;
            var scroller = article.Scroller!;
            var max = scroller.Extent.Height - scroller.Viewport.Height;
            max.ShouldBeGreaterThan(0);
            article.ReadingProgress.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Settle();
            scroller.Offset.Y.ShouldBe(40);
            article.ReadingProgress.ShouldBe(40 / max, 0.001);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Settle();
            article.ReadingProgress.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Settle();
            article.ReadingProgress.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_sheet_is_inset_8_12_in_a_phone_window()
    {
        var (control, window) = Create(width: 390, height: 844);
        try
        {
            control.Open("cloud-reading");
            Settle();
            var article = control.Article!;
            Box(article.Frame!, window).ShouldBe(new Rect(8, 12, 374, 820));
            article.Body!.Padding.Left.ShouldBe(23.4, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_article_grows_out_of_the_card_and_lands_back_on_it()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (control, window) = Create(ArcMotionLevel.Full);
        try
        {
            var card = control.Cards[0];
            var cardBox = Box(card, window);
            control.Open("cloud-reading");
            Settle();
            var article = control.Article!;
            var frame = article.Frame!;
            var flight = control.Flight!;
            flight.IsActive.ShouldBeTrue();
            flight.PairedKeys.ShouldContain("sheet");
            flight.PairedKeys.ShouldContain("hero");
            // Frame 0: the sheet sits on the card, the hero on the card's picture.
            var start = Box(frame, window);
            start.X.ShouldBe(cardBox.X, 0.5);
            start.Width.ShouldBe(cardBox.Width, 0.5);
            start.Height.ShouldBe(cardBox.Height, 0.5);
            Box(article.Hero!, window).Width.ShouldBe(cardBox.Width, 0.5);
            Box(article.Hero!, window).Height.ShouldBe(cardBox.Height, 0.5);
            control.BodyRevealValue.ShouldBe(0);

            Run(clock, 0.2);
            var mid = Box(frame, window);
            mid.Width.ShouldBeGreaterThan(cardBox.Width + 50);
            mid.Width.ShouldBeLessThan(680);
            // The hero scales uniformly: its visual height is three quarters of the sheet's visual width.
            var hero = Box(article.Hero!, window);
            hero.Width.ShouldBe(mid.Width, 0.5);
            hero.Height.ShouldBe(mid.Width * 0.75, 0.5);
            // The text keeps its own size (counter-scaled) and rides on the hero's bottom edge.
            var body = Box(article.Body!, window);
            body.Width.ShouldBe(680, 0.5);
            body.Y.ShouldBeGreaterThanOrEqualTo(hero.Bottom - 0.5);
            // The close button keeps its 36px size.
            Box(article.CloseButton!, window).Width.ShouldBeLessThanOrEqualTo(36.5);
            control.BodyRevealValue.ShouldBeGreaterThan(0);

            Run(clock, 1.5);
            flight.IsActive.ShouldBeFalse();
            Box(frame, window).ShouldBe(new Rect(380, 24, 680, 952));
            control.Presence!.State.ShouldBe(ModalState.Open);
            control.ScrimValue.ShouldBe(1);

            control.Close();
            Settle();
            Run(clock, 0.1);
            Box(frame, window).Width.ShouldBeLessThan(680);
            card.IsAway.ShouldBeTrue();
            Run(clock, 1.5);
            control.Presence.State.ShouldBe(ModalState.Closed);
            card.IsAway.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reopening_mid_close_reverses_with_the_spring_velocity()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (control, window) = Create(ArcMotionLevel.Full);
        try
        {
            control.Open("cloud-reading");
            Settle();
            Run(clock, 1.5);
            control.Close();
            Settle();
            Run(clock, 0.15);
            var flight = control.Flight!;
            var midway = flight.Progress;
            midway.ShouldBeLessThan(1);
            flight.Velocity.ShouldBeLessThan(0);
            control.Open("cloud-reading");
            Settle();
            ReferenceEquals(flight, control.Flight).ShouldBeTrue();
            // Still moving home for a moment (velocity kept), then growing again.
            Run(clock, 1.0 / 60);
            flight.Progress.ShouldBeLessThan(midway);
            Run(clock, 1.5);
            control.Presence!.State.ShouldBe(ModalState.Open);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_the_hero_down_lifts_the_article_and_past_120_closes_it()
    {
        var (control, window) = Create();
        var changes = new List<CardToArticleOpenChangedEventArgs>();
        control.OpenItemChanged += (_, e) => changes.Add(e);
        try
        {
            control.Open("cloud-reading");
            Settle();
            var article = control.Article!;
            var start = article.Hero!.TranslatePoint(new Point(300, 300), window)!.Value;
            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(start + new Vector(0, 60), RawInputModifiers.LeftMouseButton);
            Settle();
            article.PullDistance.ShouldBe(60);
            var lift = article.RenderTransform!.Value;
            lift.M11.ShouldBe(0.96, 1e-6);
            // Short, slow pull (the pointer rested before letting go, so no flick): spring back (Off motion: at once).
            Thread.Sleep(150);
            window.MouseUp(start + new Vector(0, 60), MouseButton.Left, RawInputModifiers.None);
            Settle();
            article.PullDistance.ShouldBe(0);
            control.OpenItemId.ShouldBe("cloud-reading");

            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(start + new Vector(0, 130), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(0, 130), MouseButton.Left, RawInputModifiers.None);
            Settle();
            control.OpenItemId.ShouldBeNull();
            changes[^1].Reason.ShouldBe(CardToArticleChangeReason.Drag);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_mouse_press_on_the_text_does_not_pull()
    {
        var (control, window) = Create();
        try
        {
            control.Open("cloud-reading");
            Settle();
            var article = control.Article!;
            var start = article.Body!.TranslatePoint(new Point(200, 60), window)!.Value;
            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(start + new Vector(0, 200), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(0, 200), MouseButton.Left, RawInputModifiers.None);
            Settle();
            article.PullDistance.ShouldBe(0);
            control.OpenItemId.ShouldBe("cloud-reading");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_wheel_at_the_top_pulls_and_closes_past_120()
    {
        var (control, window) = Create();
        var changes = new List<CardToArticleOpenChangedEventArgs>();
        control.OpenItemChanged += (_, e) => changes.Add(e);
        try
        {
            control.Open("cloud-reading");
            Settle();
            var article = control.Article!;
            var point = article.Hero!.TranslatePoint(new Point(300, 300), window)!.Value;
            window.MouseWheel(point, new Vector(0, 1), RawInputModifiers.None);
            Settle();
            article.PullDistance.ShouldBe(30);
            article.CheckWheelIdle(article.ClockNow + TimeSpan.FromSeconds(1));
            Settle();
            article.PullDistance.ShouldBe(0);
            for (var i = 0; i < 5; i++)
            {
                window.MouseWheel(point, new Vector(0, 1), RawInputModifiers.None);
            }
            Settle();
            control.OpenItemId.ShouldBeNull();
            changes[^1].Reason.ShouldBe(CardToArticleChangeReason.Wheel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_open_item_id_opens_and_closes_the_article()
    {
        var (control, window) = Create();
        var changes = new List<CardToArticleOpenChangedEventArgs>();
        control.OpenItemChanged += (_, e) => changes.Add(e);
        try
        {
            control.OpenItemId = "desert-moss";
            Settle();
            control.Presence!.State.ShouldBe(ModalState.Open);
            control.CardFor("desert-moss")!.IsAway.ShouldBeTrue();
            changes[^1].Reason.ShouldBe(CardToArticleChangeReason.Programmatic);
            // Switching while open swaps the story in place.
            control.OpenItemId = "overnight-bread";
            Settle();
            control.Article!.Item!.Id.ShouldBe("overnight-bread");
            control.CardFor("desert-moss")!.IsAway.ShouldBeFalse();
            control.CardFor("overnight-bread")!.IsAway.ShouldBeTrue();
            control.OpenItemId = null;
            Settle();
            control.Presence.State.ShouldBe(ModalState.Closed);
            control.OpenItemId = "nope";
            Settle();
            control.Presence.State.ShouldBe(ModalState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_article_open_before_the_control_is_shown_appears_without_a_morph()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var control = new CardToArticle { Items = CardToArticleSamples.Stories(), OpenItemId = "island-ferry" };
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        try
        {
            window.Width = 1440;
            window.Height = 1000;
            Settle();
            control.Presence!.State.ShouldBe(ModalState.Open);
            control.Flight!.IsActive.ShouldBeFalse();
            control.BodyRevealValue.ShouldBe(1);
            control.ScrimValue.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_puts_the_sheet_in_place_and_fades_only()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (control, window) = Create(ArcMotionLevel.Reduced);
        try
        {
            control.Open("cloud-reading");
            Settle();
            control.Flight!.IsActive.ShouldBeFalse();
            Box(control.Article!.Frame!, window).ShouldBe(new Rect(380, 24, 680, 952));
            control.BodyRevealValue.ShouldBe(0);
            Run(clock, 0.1);
            control.BodyRevealValue.ShouldBeGreaterThan(0);
            // No travel: the text sits right under the hero.
            control.Article.Body!.RenderTransform!.Value.M32.ShouldBe(0, 0.01);
            Run(clock, 0.5);
            control.BodyRevealValue.ShouldBe(1);
            control.Close();
            Settle();
            control.Presence!.State.ShouldBe(ModalState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_report_expanded_while_their_article_is_open()
    {
        var (control, window) = Create();
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control.Cards[0]);
            var expand = peer.GetProvider<IExpandCollapseProvider>()!;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            Settle();
            control.OpenItemId.ShouldBe("cloud-reading");
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_the_open_story_closes_the_article()
    {
        var (control, window) = Create();
        try
        {
            control.Open("overnight-bread");
            Settle();
            control.Items.RemoveAt(2);
            Settle();
            control.OpenItemId.ShouldBeNull();
            control.Presence!.State.ShouldBe(ModalState.Closed);
            control.Cards.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }
}
