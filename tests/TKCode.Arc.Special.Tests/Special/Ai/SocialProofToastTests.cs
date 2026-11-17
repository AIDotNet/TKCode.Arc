using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The social proof toast: formatting, the cycle, the stack's geometry, hover and focus, dismissing, selecting and placement.</summary>
public class SocialProofToastTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A stack whose own timer never fires during a test: the cycle is driven with <c>Advance</c>.</summary>
    private static SocialProofToast Create(params SocialProofEvent[] events)
    {
        var stack = new SocialProofToast
        {
            Interval = TimeSpan.FromHours(1),
            InitialDelay = TimeSpan.FromHours(1),
            Now = () => s_now,
        };
        stack.Events.AddRange(events);
        return stack;
    }

    private static SocialProofEvent Event(string id, string name = "Sofia", string action = "started a Pro trial") =>
        new(id, name, action) { Meta = "Madrid", Time = s_now.AddMinutes(-2) };

    private static void Next(SocialProofToast stack) => stack.Advance(TimeSpan.FromHours(2).TotalSeconds);

    /// <summary>The tests' own invented feed (the sample the library used to ship; the Gallery now passes its own).</summary>
    private static SocialProofToastSimulation Sample() => new()
    {
        Events =
        [
            Simulated("s1", "Sofia", "started a Pro trial", "Madrid", TimeSpan.FromMinutes(14)),
            Simulated("s2", "Contoso Labs", "upgraded to Business", "48 seats", TimeSpan.FromMinutes(12), "Verified purchase", SocialProofActor.Company),
            Simulated("s3", "Mateo", "bought the Team plan", "Lisbon", TimeSpan.FromMinutes(11), "Verified purchase"),
            Simulated("s4", "Northwind AI", "started a Business trial", "12 seats", TimeSpan.FromMinutes(9), actor: SocialProofActor.Company),
            Simulated("s5", "Amara", "upgraded to Pro", "Nairobi", TimeSpan.FromMinutes(7)),
            Simulated("s6", "Fabrikam", "renewed Enterprise", "120 seats", TimeSpan.FromMinutes(6), "Verified purchase", SocialProofActor.Company),
            Simulated("s7", "Lena", "started a Pro trial", "Hamburg", TimeSpan.FromMinutes(4)),
            Simulated("s8", "Harbor & Pine", "upgraded to Business", "26 seats", TimeSpan.FromMinutes(3), "Verified purchase", SocialProofActor.Company),
            Simulated("s9", "Kenji", "switched to yearly billing", "Osaka", TimeSpan.FromMinutes(2)),
            Simulated("s10", "Priya", "started a Pro trial", "Pune", TimeSpan.FromSeconds(20)),
        ],
    };

    private static SocialProofToastSimulationEvent Simulated(string id, string name, string action, string meta, TimeSpan ago,
        string? verified = null, SocialProofActor actor = SocialProofActor.Person) =>
        new() { Event = new SocialProofEvent(id, name, action) { Meta = meta, VerifiedLabel = verified, Actor = actor }, Ago = ago };

    // ---- Formatting ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10, "Just now")]
    [InlineData(59, "Just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(4 * 60 + 30, "4 min ago")]
    [InlineData(2 * 3600 + 100, "2 h ago")]
    [InlineData(3 * 86400, "3 d ago")]
    public void Relative_Time_Uses_Whole_Units(int secondsAgo, string expected) =>
        SocialProofToastFormat.Relative(s_now.AddSeconds(-secondsAgo), s_now, SocialProofToastLabels.Default).ShouldBe(expected);

    [Fact]
    public void Meta_Joins_Context_And_Time_And_Prefers_TimeText()
    {
        var labels = SocialProofToastLabels.Default;
        var e = Event("1");
        SocialProofToastFormat.Meta(e, s_now, labels).ShouldBe("Madrid · 2 min ago");
        e.TimeText = "yesterday";
        SocialProofToastFormat.Meta(e, s_now, labels).ShouldBe("Madrid · yesterday");
        e.Meta = null;
        SocialProofToastFormat.Meta(e, s_now, labels).ShouldBe("yesterday");
        e.TimeText = null;
        e.Time = null;
        SocialProofToastFormat.Meta(e, s_now, labels).ShouldBe(string.Empty);
    }

    [Fact]
    public void Verified_Label_Defaults_And_Implies_The_Badge()
    {
        var labels = SocialProofToastLabels.Default;
        var e = Event("1");
        SocialProofToastFormat.Verified(e, labels).ShouldBeNull();
        e.IsVerified = true;
        SocialProofToastFormat.Verified(e, labels).ShouldBe("Verified");
        e.IsVerified = false;
        e.VerifiedLabel = "Verified purchase";
        SocialProofToastFormat.Verified(e, labels).ShouldBe("Verified purchase");
        SocialProofToastFormat.Accessible(e, s_now, labels).ShouldBe("Sofia started a Pro trial, Madrid · 2 min ago, Verified purchase");
    }

    [Theory]
    [InlineData("Contoso Labs", "C")]
    [InlineData("  harbor & pine", "H")]
    [InlineData("3D Forge", "3")]
    [InlineData("", "")]
    public void Monogram_Is_The_First_Letter(string name, string expected) =>
        SocialProofToastFormat.Monogram(name).ShouldBe(expected);

    [AvaloniaFact]
    public void A_Simulation_Pass_Copies_Its_Events_With_Times_Measured_Back_From_Now()
    {
        var simulation = Sample();
        var sample = simulation.Pass(s_now);
        sample.Length.ShouldBeGreaterThan(5);
        sample.Select(e => e.Id).Distinct().Count().ShouldBe(sample.Length);
        sample.ShouldContain(e => e.Actor == SocialProofActor.Company);
        sample.ShouldContain(e => e.Actor == SocialProofActor.Person);
        sample.ShouldContain(e => e.ShowsVerified);
        sample.ShouldAllBe(e => e.Time <= s_now);
        sample[0].Time.ShouldBe(s_now.AddMinutes(-14));
        sample[^1].Time.ShouldBe(s_now.AddSeconds(-20));
        // Each pass makes fresh copies, so a later pass reads "14 min ago" again instead of drifting.
        var later = simulation.Pass(s_now.AddHours(1));
        later[0].ShouldNotBeSameAs(sample[0]);
        later[0].Time.ShouldBe(s_now.AddHours(1).AddMinutes(-14));
        simulation.Events[0].Event!.Time.ShouldBeNull();
    }

    // ---- The cycle -------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Without_Events_It_Plays_The_Simulation_After_The_Initial_Delay()
    {
        var stack = new SocialProofToast { Now = () => s_now, Simulation = Sample() };
        var shown = new List<SocialProofEvent>();
        stack.Shown += (_, e) => shown.Add(e.Event);
        var window = TestHelpers.Show(stack);
        try
        {
            stack.VisibleEvents.Count.ShouldBe(0);
            stack.Remaining.ShouldBe(0.8, 0.2);
            stack.IsCycling.ShouldBeTrue();
            stack.Advance(0.3);
            stack.VisibleEvents.Count.ShouldBe(0);
            stack.Advance(1);
            stack.VisibleEvents.Single().Name.ShouldBe("Sofia");
            shown.Single().Id.ShouldBe("s1");
            stack.Remaining.ShouldBe(4, 0.2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Events_Or_A_Simulation_The_Stack_Stays_Empty_Until_A_Push()
    {
        var stack = new SocialProofToast { Now = () => s_now };
        var window = TestHelpers.Show(stack);
        try
        {
            stack.VisibleEvents.Count.ShouldBe(0);
            stack.IsCycling.ShouldBeFalse();
            stack.Advance(10);
            stack.VisibleEvents.Count.ShouldBe(0);
            stack.Push(Event("p1", "Lena"));
            stack.VisibleEvents.Single().Name.ShouldBe("Lena");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Events_Win_Over_The_Simulation_And_Clearing_The_Simulation_Stops_It()
    {
        var stack = new SocialProofToast { Now = () => s_now, Simulation = Sample(), Prefill = 2 };
        var window = TestHelpers.Show(stack);
        try
        {
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["s2", "s1"]);
            stack.IsCycling.ShouldBeTrue();

            // Clearing the simulation stops the feed and empties the stack.
            stack.Simulation = null;
            Dispatcher.UIThread.RunJobs();
            stack.VisibleEvents.Count.ShouldBe(0);
            stack.IsCycling.ShouldBeFalse();

            // The app's own events win over a simulation set again.
            stack.Events.Add(Event("1", "Mateo"));
            stack.Simulation = Sample();
            Dispatcher.UIThread.RunJobs();
            stack.VisibleEvents.Select(e => e.Name).ShouldBe(["Mateo"]);
            Next(stack);
            stack.VisibleEvents.ShouldAllBe(e => !e.Id.StartsWith('s'));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Prefill_Shows_Toasts_At_Once_Newest_In_Front()
    {
        var stack = Create(Event("1", "Sofia"), Event("2", "Mateo"), Event("3", "Amara"));
        stack.Prefill = 2;
        var window = TestHelpers.Show(stack);
        try
        {
            stack.VisibleEvents.Select(e => e.Name).ShouldBe(["Mateo", "Sofia"]);
            stack.LiveCards.ShouldAllBe(c => c.Opacity == 1);
            stack.ShownCount.ShouldBe(2);
            Next(stack);
            stack.VisibleEvents[0].Name.ShouldBe("Amara");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Older_Toasts_Fade_Out_Past_MaxVisible()
    {
        var stack = Create(Event("1"), Event("2"), Event("3"), Event("4"), Event("5"));
        stack.MaxVisible = 3;
        var window = TestHelpers.Show(stack);
        try
        {
            for (var i = 0; i < 5; i++)
            {
                Next(stack);
            }
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["5", "4", "3"]);
            // Motion is off, so the fade finished at once and the toast was taken out: at most MaxVisible are mounted.
            stack.ExitingCount.ShouldBe(0);
            stack.Stack.Children.OfType<SocialProofToastCard>().Count().ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loop_Starts_Over_And_Loop_False_Stops_After_The_Last_Event()
    {
        var stack = Create(Event("1"), Event("2"));
        var window = TestHelpers.Show(stack);
        try
        {
            Next(stack);
            Next(stack);
            Next(stack);
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["1", "2"]);
            stack.IsCycling.ShouldBeTrue();

            stack.Loop = false;
            stack.Restart();
            Next(stack);
            stack.Advance(TimeSpan.FromHours(2).TotalSeconds).ShouldBeFalse();
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["2", "1"]);
            Next(stack);
            stack.VisibleEvents.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Limit_Stops_The_Session()
    {
        var stack = Create(Event("1"), Event("2"), Event("3"), Event("4"));
        stack.Limit = 2;
        var window = TestHelpers.Show(stack);
        try
        {
            Next(stack);
            Next(stack);
            stack.ShownCount.ShouldBe(2);
            stack.IsCycling.ShouldBeFalse();
            Next(stack);
            stack.ShownCount.ShouldBe(2);
            stack.VisibleEvents.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IsPaused_Stops_The_Timer_And_Keeps_The_Remaining_Time()
    {
        var stack = Create(Event("1"));
        stack.InitialDelay = TimeSpan.FromSeconds(3);
        var window = TestHelpers.Show(stack);
        try
        {
            stack.Advance(1);
            stack.IsPaused = true;
            stack.IsCycling.ShouldBeFalse();
            stack.IsPaused = false;
            stack.IsCycling.ShouldBeTrue();
            stack.Remaining.ShouldBe(2, 0.2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Push_Shows_An_Event_Now_And_Restarts_The_Countdown()
    {
        var stack = Create(Event("1"));
        stack.Interval = TimeSpan.FromSeconds(5);
        var shown = 0;
        stack.Shown += (_, _) => shown++;
        var window = TestHelpers.Show(stack);
        try
        {
            Next(stack);
            stack.Advance(3);
            stack.Push(Event("live", "Kenji", "upgraded to Pro"));
            stack.VisibleEvents[0].Name.ShouldBe("Kenji");
            stack.Remaining.ShouldBe(5, 0.2);
            shown.ShouldBe(2);
            // Pushing an event already on screen replaces it instead of showing it twice.
            stack.Push(Event("1", "Sofia"));
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["1", "live"]);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Geometry, hover and focus ---------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Covered_Toasts_Step_Back_And_Shrink_And_Hide_Their_Content()
    {
        var stack = Create(Event("1"), Event("2"), Event("3"));
        stack.Prefill = 3;
        var window = TestHelpers.Show(stack);
        try
        {
            var cards = stack.LiveCards;
            cards[0].OffsetY.ShouldBe(0);
            cards[0].CurrentScale.ShouldBe(1);
            cards[1].OffsetY.ShouldBe(-10);
            cards[1].CurrentScale.ShouldBe(0.95, 0.0001);
            cards[2].OffsetY.ShouldBe(-20);
            cards[2].CurrentScale.ShouldBe(0.9, 0.0001);
            cards[0].Classes.Contains(SocialProofToastCard.CoveredPseudoClass).ShouldBeFalse();
            cards[1].Classes.Contains(SocialProofToastCard.CoveredPseudoClass).ShouldBeTrue();
            cards[0].ZIndex.ShouldBeGreaterThan(cards[1].ZIndex);
            // Every toast is 64px high and the stack reserves the fanned height, so nothing resizes on hover.
            cards[0].Bounds.Height.ShouldBe(64);
            stack.DesiredSize.Height.ShouldBe(64 + 72 * 2);
            stack.Bounds.Width.ShouldBe(400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Fans_The_Stack_Out_And_Pauses_It()
    {
        var stack = Create(Event("1"), Event("2"), Event("3"));
        stack.Prefill = 3;
        var other = new Avalonia.Controls.Button { Content = "Elsewhere" };
        var window = TestHelpers.Show(new StackPanel { Children = { stack, other } });
        try
        {
            stack.IsCycling.ShouldBeTrue();
            stack.LiveCards[1].DismissButton!.Focus(NavigationMethod.Tab).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            stack.IsFanned.ShouldBeTrue();
            stack.IsCycling.ShouldBeFalse();
            stack.Classes.Contains(SocialProofToast.ExpandedPseudoClass).ShouldBeTrue();
            stack.LiveCards[1].OffsetY.ShouldBe(-72);
            stack.LiveCards[2].OffsetY.ShouldBe(-144);
            stack.LiveCards[1].CurrentScale.ShouldBe(1);
            stack.LiveCards[1].Classes.Contains(SocialProofToastCard.CoveredPseudoClass).ShouldBeFalse();

            other.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            stack.IsFanned.ShouldBeFalse();
            stack.IsCycling.ShouldBeTrue();
            stack.LiveCards[1].OffsetY.ShouldBe(-10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ExpandOnHover_False_Pauses_Without_Fanning()
    {
        var stack = Create(Event("1"), Event("2"));
        stack.Prefill = 2;
        stack.ExpandOnHover = false;
        var window = TestHelpers.Show(stack);
        try
        {
            stack.LiveCards[0].DismissButton!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            stack.IsFanned.ShouldBeFalse();
            stack.IsCycling.ShouldBeFalse();
            stack.LiveCards[1].OffsetY.ShouldBe(-10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Over_The_Stack_Pauses_And_Fans_It_Out()
    {
        var stack = Create(Event("1"), Event("2"));
        stack.Prefill = 2;
        var window = TestHelpers.Show(new StackPanel { Children = { stack, new Border { Height = 60 } } });
        try
        {
            var card = stack.LiveCards[0];
            var center = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            stack.IsFanned.ShouldBeTrue();
            stack.IsCycling.ShouldBeFalse();
            // While fanned the gaps count as the stack; below it the pointer has left.
            window.MouseMove(new Point(390, 2), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            stack.IsFanned.ShouldBeTrue();
            window.MouseMove(new Point(200, 290), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            stack.IsFanned.ShouldBeFalse();
            stack.IsCycling.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Top_Corners_Stack_Downward()
    {
        var stack = Create(Event("1"), Event("2"));
        stack.Prefill = 2;
        stack.Position = SocialProofToastPosition.TopRight;
        var window = TestHelpers.Show(stack);
        try
        {
            stack.Stack.IsTopAnchored.ShouldBeTrue();
            stack.LiveCards[1].OffsetY.ShouldBe(10);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Dismissing and selecting ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Dismiss_Button_Removes_The_Toast_Confirms_It_And_Raises_Dismissed()
    {
        var stack = Create(Event("1", "Sofia"), Event("2", "Mateo"));
        stack.Prefill = 2;
        SocialProofEvent? dismissed = null;
        stack.Dismissed += (_, e) => dismissed = e.Event;
        var window = TestHelpers.Show(stack);
        try
        {
            var front = stack.LiveCards[0];
            AutomationProperties.GetName(front.DismissButton!).ShouldBe("Dismiss: Mateo started a Pro trial");
            front.DismissButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            dismissed!.Id.ShouldBe("2");
            stack.VisibleEvents.Single().Id.ShouldBe("1");
            stack.StatusText.Text.ShouldBe("Dismissed: Mateo started a Pro trial");
            AutomationProperties.GetLiveSetting(stack.StatusText).ShouldBe(AutomationLiveSetting.Polite);
            // The rest close the gap and focus moves to the next toast.
            stack.LiveCards[0].OffsetY.ShouldBe(0);
            stack.LiveCards[0].DismissButton!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Dismisses_The_Focused_Toast_Then_Moves_Focus_On()
    {
        var stack = Create(Event("1"), Event("2"), Event("3"));
        stack.Prefill = 3;
        var window = TestHelpers.Show(stack);
        try
        {
            stack.LiveCards[1].DismissButton!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["3", "1"]);
            stack.LiveCards[1].DismissButton!.IsFocused.ShouldBeTrue();
            stack.IsFanned.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            stack.VisibleEvents.Select(e => e.Id).ShouldBe(["3"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selectable_Toasts_Are_Buttons_That_Raise_Selected()
    {
        var stack = Create(Event("1", "Sofia"));
        stack.Prefill = 1;
        SocialProofEvent? selected = null;
        stack.Selected += (_, e) => selected = e.Event;
        var window = TestHelpers.Show(stack);
        try
        {
            var card = stack.LiveCards[0];
            card.Focusable.ShouldBeFalse();
            card.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            selected.ShouldBeNull();

            stack.IsSelectable = true;
            card.Focusable.ShouldBeTrue();
            card.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            selected!.Name.ShouldBe("Sofia");
            stack.VisibleEvents.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Parts, labels and placement -------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Toast_Shows_Its_Mark_Sentence_Meta_And_Badge()
    {
        var company = new SocialProofEvent("c", "Contoso Labs", "upgraded to Business")
        {
            Actor = SocialProofActor.Company,
            Meta = "48 seats",
            TimeText = "4 min ago",
            VerifiedLabel = "Verified purchase",
        };
        var stack = Create(Event("p", "Sofia"), company);
        stack.Prefill = 2;
        var window = TestHelpers.Show(stack);
        try
        {
            var front = stack.LiveCards[0];
            front.AvatarView!.IsVisible.ShouldBeFalse();
            front.MonogramView!.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(front, SocialProofToastCard.InitialPart).Text.ShouldBe("C");
            front.TitleBlock!.Inlines!.Count.ShouldBe(2);
            front.MetaBlock!.Text.ShouldBe("48 seats · 4 min ago");
            front.Badge!.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(front, SocialProofToastCard.BadgeTextPart).Text.ShouldBe("Verified purchase");
            AutomationProperties.GetName(front).ShouldBe("Contoso Labs upgraded to Business, 48 seats · 4 min ago, Verified purchase");

            var person = stack.LiveCards[1];
            person.AvatarView!.IsVisible.ShouldBeTrue();
            person.AvatarView.DisplayName.ShouldBe("Sofia");
            person.MonogramView!.IsVisible.ShouldBeFalse();
            person.Badge!.IsVisible.ShouldBeFalse();
            person.MetaBlock!.Text.ShouldBe("Madrid · 2 min ago");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Badge_Follows_The_Meta_And_Keeps_Its_Width_When_The_Meta_Truncates()
    {
        var verified = new SocialProofEvent("v", "Mateo", "bought the Team plan")
        {
            // Short words: the headless test font is much wider than the real one.
            Meta = "Rio",
            VerifiedLabel = "OK",
        };
        var bare = new SocialProofEvent("b", "Lena", "joined the waitlist") { IsVerified = true };
        var stack = Create(verified, bare);
        stack.Prefill = 2;
        var window = TestHelpers.Show(stack);
        try
        {
            // No meta and no time: the badge starts the line.
            var front = stack.LiveCards[0];
            front.MetaBlock!.IsVisible.ShouldBeFalse();
            front.Badge!.Bounds.X.ShouldBe(0);

            // The badge sits right after the meta (8px apart), not at the far edge of a wide toast.
            var back = stack.LiveCards[1];
            back.Badge!.Bounds.X.ShouldBe(back.MetaBlock!.Bounds.Width + 8, 0.5);
            var row = (Control)back.Badge.GetVisualParent()!;
            back.Badge.Bounds.Right.ShouldBeLessThan(row.Bounds.Width - 20);

            // Narrow: the meta truncates and the badge keeps its full width.
            var badgeWidth = back.Badge.Bounds.Width;
            var metaWidth = back.MetaBlock.Bounds.Width;
            stack.Width = 170;
            stack.HorizontalAlignment = HorizontalAlignment.Left;
            Dispatcher.UIThread.RunJobs();
            back.Badge.Bounds.Width.ShouldBe(badgeWidth, 0.5);
            back.MetaBlock.Bounds.Width.ShouldBeLessThan(metaWidth - 10);
            back.Badge.Bounds.X.ShouldBe(back.MetaBlock.Bounds.Width + 8, 0.5);
            back.Badge.Bounds.Right.ShouldBeLessThanOrEqualTo(row.Bounds.Width + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Logo_Shows_For_A_Person_Without_A_Portrait_In_A_Round_Slot()
    {
        var mark = Avalonia.Media.Geometry.Parse("M4 12h16");
        var person = new SocialProofEvent("p", "Lena", "joined the waitlist") { Logo = mark };
        var company = new SocialProofEvent("c", "Fabrikam", "renewed Enterprise") { Logo = mark, Actor = SocialProofActor.Company };
        var stack = Create(person, company);
        stack.Prefill = 2;
        var window = TestHelpers.Show(stack);
        try
        {
            var companyCard = stack.LiveCards[0];
            companyCard.Classes.Contains(SocialProofToastCard.CompanyPseudoClass).ShouldBeTrue();
            companyCard.MonogramView!.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<ArcGlyph>(companyCard, SocialProofToastCard.LogoPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(companyCard, SocialProofToastCard.InitialPart).IsVisible.ShouldBeFalse();

            var personCard = stack.LiveCards[1];
            personCard.Classes.Contains(SocialProofToastCard.CompanyPseudoClass).ShouldBeFalse();
            personCard.AvatarView!.IsVisible.ShouldBeFalse();
            personCard.MonogramView!.IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<ArcGlyph>(personCard, SocialProofToastCard.LogoPart).IsVisible.ShouldBeTrue();
            // Round for a person, a rounded square for a company.
            personCard.MonogramView.CornerRadius.TopLeft.ShouldBeGreaterThan(companyCard.MonogramView.CornerRadius.TopLeft);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hiding_The_Control_Hides_A_Corner_Stack()
    {
        var stack = Create(Event("1"));
        stack.Prefill = 1;
        stack.Position = SocialProofToastPosition.BottomRight;
        var window = TestHelpers.Show(stack);
        try
        {
            stack.Stack.Parent.ShouldBeOfType<SocialProofToastCorner>();
            stack.Stack.IsEffectivelyVisible.ShouldBeTrue();
            stack.IsVisible = false;
            stack.Stack.IsVisible.ShouldBeFalse();
            stack.IsVisible = true;
            stack.Stack.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Minimized_Window_Pauses_The_Cycle()
    {
        var stack = Create(Event("1"));
        var window = TestHelpers.Show(stack);
        try
        {
            stack.IsCycling.ShouldBeTrue();
            window.WindowState = WindowState.Minimized;
            stack.IsCycling.ShouldBeFalse();
            window.WindowState = WindowState.Normal;
            stack.IsCycling.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Region_Times_And_Buttons()
    {
        var stack = Create(Event("1", "Sofia"));
        stack.Prefill = 1;
        var window = TestHelpers.Show(stack);
        try
        {
            AutomationProperties.GetName(stack.Stack).ShouldBe("Recent activity");
            stack.Labels = new SocialProofToastLabels
            {
                Region = "Actividad reciente",
                MinutesAgo = m => $"hace {m} min",
                Dismiss = s => "Cerrar: " + s,
            };
            AutomationProperties.GetName(stack.Stack).ShouldBe("Actividad reciente");
            stack.LiveCards[0].MetaBlock!.Text.ShouldBe("Madrid · hace 2 min");
            AutomationProperties.GetName(stack.LiveCards[0].DismissButton!).ShouldBe("Cerrar: Sofia started a Pro trial");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Corner_Position_Moves_The_Stack_To_The_Overlay_And_Back()
    {
        var stack = Create(Event("1"));
        stack.Prefill = 1;
        var window = TestHelpers.Show(stack);
        try
        {
            window.Width = 800;
            stack.Position = SocialProofToastPosition.BottomRight;
            Dispatcher.UIThread.RunJobs();
            stack.Stack.Parent.ShouldBeOfType<SocialProofToastCorner>();
            stack.Stack.HorizontalAlignment.ShouldBe(HorizontalAlignment.Right);
            stack.Stack.VerticalAlignment.ShouldBe(VerticalAlignment.Bottom);
            stack.Stack.Width.ShouldBe(360);
            stack.Stack.Margin.ShouldBe(new Thickness(24));

            // Under 480px the stack spans the width with 16px gutters.
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            stack.Stack.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
            stack.Stack.Margin.ShouldBe(new Thickness(16));

            stack.Position = SocialProofToastPosition.Inline;
            Dispatcher.UIThread.RunJobs();
            stack.Stack.Parent.ShouldBeOfType<Panel>().Name.ShouldBe(SocialProofToast.HostPart);
            stack.VisibleEvents.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Corner_Stack_Leaves_The_Overlay_With_The_Control()
    {
        var stack = Create(Event("1"));
        stack.Position = SocialProofToastPosition.BottomLeft;
        var host = new ContentControl { Content = stack };
        var window = TestHelpers.Show(host);
        try
        {
            stack.Stack.Parent.ShouldBeOfType<SocialProofToastCorner>();
            host.Content = null;
            Dispatcher.UIThread.RunJobs();
            stack.Stack.Parent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Exiting_Toasts_Are_Inert()
    {
        var stack = Create(Event("1"), Event("2"));
        stack.Prefill = 2;
        var window = TestHelpers.Show(stack, Motion.ArcMotionLevel.Full);
        try
        {
            var front = stack.LiveCards[0];
            stack.Dismiss(front, moveFocus: false);
            front.IsExiting.ShouldBeTrue();
            front.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetAccessibilityView(front).ShouldBe(AccessibilityView.Raw);
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return stack.ExitingCount == 0;
            }).ShouldBeTrue();
            stack.Stack.Children.ShouldNotContain(front);
        }
        finally
        {
            window.Close();
        }
    }
}
