using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The trial banner: the date and state rules, the copy per state, billing, compare, dismiss, snooze, the pill and the container query.</summary>
public class TrialBannerTests
{
    /// <summary>A clock the test sets: UTC, so local days are UTC days.</summary>
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly DateTimeOffset s_now = new(2026, 10, 27, 15, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gives a banner the invented trial the tests use (the package ships none) wherever the test left it unset: a Pro trial
    /// of the Northwind workspace, 9 days left on Oct 27, 2026 (so it ends Nov 5), losing SAML SSO and 12 seats.
    /// </summary>
    private static TrialBanner Sample(TrialBanner banner)
    {
        if (!banner.IsSet(TrialBanner.PlanProperty))
        {
            banner.Plan = "Pro";
        }
        if (!banner.IsSet(TrialBanner.WorkspaceProperty))
        {
            banner.Workspace = "Northwind";
        }
        if (!banner.IsSet(TrialBanner.LossSummaryProperty))
        {
            banner.LossSummary = "SAML SSO and 12 seats";
        }
        if (banner.Features.Count == 0)
        {
            banner.Features.AddRange(
            [
                new TrialFeature("sso", "SAML single sign-on", "for every member", ArcIcons.Key),
                new TrialFeature("alerts", "Alerts in team chat", "incidents and mentions", ArcIcons.Bell),
                new TrialFeature("sync", "Two-way issue sync", "status and comments", ArcIcons.RefreshCw),
                new TrialFeature("seats", "Team seats", "12 seats", ArcIcons.Users)
            ]);
        }
        if (!banner.IsSet(TrialBanner.DaysLeftProperty) && !banner.IsSet(TrialBanner.EndsAtProperty))
        {
            banner.DaysLeft = 9;
        }
        if (!banner.IsSet(TrialBanner.TimeProviderProperty))
        {
            banner.TimeProvider = new TestClock(s_now);
        }
        return banner;
    }

    private static string TextOf(ArcContentSwap? swap) => (swap?.Content as TextBlock)?.Text ?? string.Empty;

    // ---- Pure rules ----

    [Fact]
    public void Explicit_Days_Win_And_The_Date_Follows_Them_From_Today()
    {
        var clock = new TestClock(s_now);
        var (days, end, ended) = TrialBannerMath.Resolve(5, null, clock);
        days.ShouldBe(5);
        end.ShouldBe(new DateOnly(2026, 11, 1));
        ended.ShouldBeFalse();
        TrialBannerMath.Resolve(0, null, clock).Ended.ShouldBeTrue();
    }

    [Fact]
    public void Without_Data_There_Are_No_Days()
    {
        var (days, end, ended) = TrialBannerMath.Resolve(null, null, new TestClock(s_now));
        days.ShouldBe(0);
        end.ShouldBe(new DateOnly(2026, 10, 27));
        ended.ShouldBeFalse();
    }

    [Fact]
    public void Days_Count_Calendar_Days_From_The_End_Date()
    {
        var clock = new TestClock(s_now);
        TrialBannerMath.Resolve(null, new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), clock).Days.ShouldBe(7);

        // Ends later today: still running, the last day.
        var later = TrialBannerMath.Resolve(null, s_now.AddHours(3), clock);
        later.Days.ShouldBe(0);
        later.Ended.ShouldBeFalse();

        // Ended an hour ago.
        TrialBannerMath.Resolve(null, s_now.AddHours(-1), clock).Ended.ShouldBeTrue();
        TrialBannerMath.Resolve(null, s_now.AddDays(-2), clock).Ended.ShouldBeTrue();
    }

    [Theory]
    [InlineData(9, false, false, TrialStatus.Active)]
    [InlineData(4, false, false, TrialStatus.Active)]
    [InlineData(3, false, false, TrialStatus.Warning)]
    [InlineData(0, false, false, TrialStatus.Warning)]
    [InlineData(0, true, false, TrialStatus.Expired)]
    [InlineData(2, false, true, TrialStatus.Upgraded)]
    [InlineData(0, true, true, TrialStatus.Upgraded)]
    public void Status_Follows_Days_And_Warn_At(int days, bool ended, bool upgraded, TrialStatus expected) =>
        TrialBannerMath.StatusOf(days, ended, 3, upgraded).ShouldBe(expected);

    [Fact]
    public void Ring_Shows_The_Days_Left_Of_The_Trial()
    {
        TrialBannerMath.RingValue(TrialStatus.Active, 7, 14).ShouldBe(0.5);
        TrialBannerMath.RingValue(TrialStatus.Active, 20, 14).ShouldBe(1);
        TrialBannerMath.RingValue(TrialStatus.Expired, 0, 14).ShouldBe(0);
        TrialBannerMath.RingValue(TrialStatus.Upgraded, 2, 14).ShouldBe(1);
    }

    [Fact]
    public void Snooze_Options_Depend_On_The_State()
    {
        TrialBannerMath.SnoozeOptions(TrialStatus.Active, 9, 3)
            .ShouldBe([TrialSnoozeOption.Tomorrow, TrialSnoozeOption.ThreeDays, TrialSnoozeOption.UntilWarning]);
        // The warning starts in three days: "for 3 days" already says that.
        TrialBannerMath.SnoozeOptions(TrialStatus.Active, 6, 3).ShouldBe([TrialSnoozeOption.Tomorrow, TrialSnoozeOption.ThreeDays]);
        TrialBannerMath.SnoozeOptions(TrialStatus.Warning, 2, 3).ShouldBe([TrialSnoozeOption.Tomorrow]);
        TrialBannerMath.SnoozeOptions(TrialStatus.Expired, 0, 3).ShouldBe([TrialSnoozeOption.Tomorrow, TrialSnoozeOption.ThreeDays]);
        TrialBannerMath.SnoozeOptions(TrialStatus.Upgraded, 5, 3).ShouldBeEmpty();
    }

    [Fact]
    public void Snoozes_End_At_The_Start_Of_A_Local_Day()
    {
        var clock = new TestClock(s_now);
        TrialBannerMath.SnoozeUntil(TrialSnoozeOption.Tomorrow, 9, 3, clock).ShouldBe(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero));
        TrialBannerMath.SnoozeUntil(TrialSnoozeOption.ThreeDays, 9, 3, clock).ShouldBe(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero));
        TrialBannerMath.SnoozeUntil(TrialSnoozeOption.UntilWarning, 9, 3, clock).ShouldBe(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Default_Labels_Read_Naturally()
    {
        var labels = TrialBannerLabels.Default;
        labels.Days(1).ShouldBe("1 day");
        labels.Days(9).ShouldBe("9 days");
        labels.Date(new DateOnly(2026, 11, 5)).ShouldBe("Nov 5");
        labels.Weekday(new DateOnly(2026, 10, 29)).ShouldBe("Thursday");
        labels.WarningDetail(0, "SSO").ShouldBe("Less than a day left to keep SSO.");
        labels.AnnounceUpgraded("Pro", true).ShouldBe("Payment method added. You're on Pro. (Simulated.)");
    }

    // ---- The control ----

    [AvaloniaFact]
    public void Without_A_Trial_The_Banner_Is_Empty()
    {
        var banner = new TrialBanner { TimeProvider = new TestClock(s_now) };
        var window = TestHelpers.Show(banner);
        try
        {
            banner.Plan.ShouldBe(string.Empty);
            banner.Workspace.ShouldBe(string.Empty);
            banner.LossSummary.ShouldBe(string.Empty);
            banner.Features.Count.ShouldBe(0);
            banner.HasTrial.ShouldBeFalse();
            banner.Status.ShouldBe(TrialStatus.Active);
            banner.Classes.Contains(":warning").ShouldBeFalse();
            TextOf(banner.BannerParts.Title).ShouldBe("No trial in progress");
            TextOf(banner.BannerParts.Detail).ShouldBe(string.Empty);
            TextOf(banner.BannerParts.Mark).ShouldBe("\u2013");
            banner.BannerParts.Ring!.Value.ShouldBe(0);
            banner.BannerParts.Primary!.IsVisible.ShouldBeFalse();
            banner.BannerParts.Compare!.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(banner).ShouldBe("No trial in progress");
            _ = banner.AddPaymentAsync();
            Dispatcher.UIThread.RunJobs();
            banner.IsUpgraded.ShouldBeFalse();

            // The app's trial arrives.
            banner.Plan = "Pro";
            banner.LossSummary = "SAML SSO and 12 seats";
            banner.DaysLeft = 9;
            Dispatcher.UIThread.RunJobs();
            banner.HasTrial.ShouldBeTrue();
            TextOf(banner.BannerParts.Title).ShouldBe("9 days left in your Pro trial");
            TextOf(banner.BannerParts.Detail).ShouldBe("Keep SAML SSO and 12 seats after Nov 5.");
            banner.BannerParts.Primary.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Banner_With_A_Trial_Shows_It()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner);
        try
        {
            banner.Status.ShouldBe(TrialStatus.Active);
            banner.ActiveFace.ShouldBeSameAs(banner.BannerParts.Root);
            TextOf(banner.BannerParts.Title).ShouldBe("9 days left in your Pro trial");
            TextOf(banner.BannerParts.Detail).ShouldBe("Keep SAML SSO and 12 seats after Nov 5.");
            TextOf(banner.BannerParts.Mark).ShouldBe("9");
            banner.BannerParts.Ring!.Value.ShouldBe(9 / 14.0, 0.0001);
            banner.BannerParts.Primary!.Content.ShouldBe("Add payment method");
            banner.BannerParts.Compare!.Content.ShouldBe("Compare plans");
            AutomationProperties.GetName(banner).ShouldBe("Pro trial");
            banner.BannerParts.Menu!.Items.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Last_Days_Take_The_Warning_Tone_And_Name_The_End_Day()
    {
        var banner = Sample(new TrialBanner { DaysLeft = 2 });
        var window = TestHelpers.Show(banner);
        try
        {
            banner.Status.ShouldBe(TrialStatus.Warning);
            banner.Classes.Contains(TrialBanner.WarningPseudoClass).ShouldBeTrue();
            TextOf(banner.BannerParts.Title).ShouldBe("Your Pro trial ends Thursday");
            TextOf(banner.BannerParts.Detail).ShouldBe("2 days left to keep SAML SSO and 12 seats.");

            banner.DaysLeft = 1;
            TextOf(banner.BannerParts.Title).ShouldBe("Your Pro trial ends tomorrow");
            banner.PillFace!.Label.ShouldBe("Ends tomorrow");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expired_Shows_A_Lock_And_What_The_Workspace_Lost()
    {
        var banner = Sample(new TrialBanner { DaysLeft = 0, Workspace = "Contoso", Variant = TrialBannerVariant.Card });
        var window = TestHelpers.Show(banner);
        try
        {
            banner.Status.ShouldBe(TrialStatus.Expired);
            banner.ActiveFace.ShouldBeSameAs(banner.CardParts.Root);
            banner.CardParts.Mark!.Content.ShouldBeOfType<ArcGlyph>().Data.ShouldBe(ArcIcons.Lock);
            TextOf(banner.CardParts.Lead).ShouldBe("Contoso lost access to:");
            TextOf(banner.BannerParts.Title).ShouldBe("Your Pro trial has ended");
            TextOf(banner.BannerParts.Detail).ShouldBe("Add a payment method to get SAML SSO and 12 seats back.");
            banner.CardParts.Features!.Children.Count.ShouldBe(4);
            banner.CardParts.Ring!.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Lists_The_Features_Given()
    {
        var banner = Sample(new TrialBanner { Variant = TrialBannerVariant.Card });
        // The test's own features replace the fixture's.
        banner.Features.Clear();
        banner.Features.Add(new TrialFeature("audit", "Audit log", "1 year"));
        var window = TestHelpers.Show(banner);
        try
        {
            banner.CardParts.Features!.Children.Count.ShouldBe(1);
            banner.Features.Add(new TrialFeature("api", "API access"));
            banner.CardParts.Features.Children.Count.ShouldBe(2);
            banner.CardParts.Heading!.Text.ShouldBe("Pro trial");
            TextOf(banner.CardParts.Detail).ShouldBe("9 days left · ends Nov 5");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Days_From_The_End_Date_Change_At_Midnight()
    {
        var clock = new TestClock(s_now);
        var banner = Sample(new TrialBanner { TimeProvider = clock, EndsAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero) });
        var window = TestHelpers.Show(banner);
        try
        {
            TextOf(banner.BannerParts.Title).ShouldBe("5 days left in your Pro trial");
            clock.Now = clock.Now.AddDays(2);
            // ArcToday raises Changed at local midnight; the banner then counts again.
            banner.Refresh(instant: false);
            TextOf(banner.BannerParts.Title).ShouldBe("Your Pro trial ends Sunday");
            banner.Status.ShouldBe(TrialStatus.Warning);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Add_Payment_Shows_Busy_Then_The_Upgraded_State()
    {
        var gate = new TaskCompletionSource();
        CancellationToken? token = null;
        var banner = Sample(new TrialBanner
        {
            AddPaymentHandler = ct =>
            {
                token = ct;
                return gate.Task;
            }
        });
        var window = TestHelpers.Show(banner);
        try
        {
            var primary = banner.BannerParts.Primary!;
            primary.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            banner.Phase.ShouldBe(TrialBillingPhase.Busy);
            primary.IsLoading.ShouldBeTrue();
            TextOf(banner.BannerParts.Detail).ShouldBe("Opening checkout…");
            token.ShouldNotBeNull();

            gate.SetResult();
            TestHelpers.PumpUntil(() => banner.IsUpgraded).ShouldBeTrue();
            banner.Status.ShouldBe(TrialStatus.Upgraded);
            primary.IsLoading.ShouldBeFalse();
            primary.Content.ShouldBe("Manage billing");
            banner.BannerParts.Compare!.IsVisible.ShouldBeFalse();
            banner.BannerParts.Snooze!.IsVisible.ShouldBeFalse();
            TextOf(banner.BannerParts.Title).ShouldBe("You're on Pro");
            banner.BannerParts.Mark!.Content.ShouldBeOfType<ArcGlyph>().Data.ShouldBe(ArcIcons.Check);
            banner.LiveRegion!.Announcement.ShouldBe("Payment method added. You're on Pro.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Checkout_Offers_Try_Again()
    {
        var calls = 0;
        var banner = Sample(new TrialBanner
        {
            AddPaymentHandler = _ =>
            {
                calls++;
                return calls == 1 ? Task.FromException(new InvalidOperationException("Checkout failed")) : Task.CompletedTask;
            }
        });
        var window = TestHelpers.Show(banner);
        try
        {
            banner.AddPaymentAsync().Wait(TimeSpan.Zero);
            TestHelpers.PumpUntil(() => banner.Phase == TrialBillingPhase.Failed).ShouldBeTrue();
            banner.BannerParts.Primary!.Content.ShouldBe("Try again");
            TextOf(banner.BannerParts.Detail).ShouldBe("Checkout didn't open. Nothing was charged.");
            banner.LiveRegion!.Announcement.ShouldBe("Checkout didn't open. Try again.");
            banner.IsUpgraded.ShouldBeFalse();

            _ = banner.AddPaymentAsync();
            TestHelpers.PumpUntil(() => banner.IsUpgraded).ShouldBeTrue();
            calls.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Handler_Billing_Is_Simulated()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner);
        try
        {
            _ = banner.AddPaymentAsync();
            banner.Phase.ShouldBe(TrialBillingPhase.Busy);
            TestHelpers.PumpUntil(() => banner.IsUpgraded, 5000).ShouldBeTrue();
            banner.LiveRegion!.Announcement.ShouldBe("Payment method added. You're on Pro. (Simulated.)");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compare_Plans_Shows_A_Note_Unless_Handled()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner);
        try
        {
            banner.BannerParts.Compare!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            TextOf(banner.BannerParts.Detail).ShouldBe("Your plans page would open here (simulated).");
            banner.ClearNote();
            TextOf(banner.BannerParts.Detail).ShouldStartWith("Keep SAML SSO");

            var raised = 0;
            banner.ComparePlansRequested += (_, e) =>
            {
                raised++;
                e.Handled = true;
            };
            banner.RequestComparePlans();
            raised.ShouldBe(1);
            banner.Note.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismiss_Folds_Into_The_Pill_And_The_Pill_Opens_It_Again()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner);
        try
        {
            var dismissed = 0;
            banner.Dismissed += (_, _) => dismissed++;
            banner.BannerParts.Dismiss!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            dismissed.ShouldBe(1);
            banner.IsCollapsed.ShouldBeTrue();
            banner.ActiveFace.ShouldBeSameAs(banner.PillFace);
            banner.Classes.Contains(TrialBanner.FoldedPseudoClass).ShouldBeTrue();
            banner.PillFace!.IsFocused.ShouldBeTrue();
            banner.LiveRegion!.Announcement.ShouldBe("Trial banner dismissed.");
            banner.DesiredSize.Height.ShouldBeLessThan(40);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            banner.IsCollapsed.ShouldBeFalse();
            banner.ActiveFace.ShouldBeSameAs(banner.BannerParts.Root);
            banner.BannerParts.Primary!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapse_To_None_Renders_Nothing()
    {
        var banner = Sample(new TrialBanner { CollapseTo = TrialBannerCollapse.None });
        var window = TestHelpers.Show(banner);
        try
        {
            banner.Dismiss();
            Dispatcher.UIThread.RunJobs();
            banner.ActiveFace.ShouldBeNull();
            banner.DesiredSize.Height.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Snooze_Reports_When_It_Ends()
    {
        var clock = new TestClock(s_now);
        var banner = Sample(new TrialBanner { TimeProvider = clock });
        var window = TestHelpers.Show(banner);
        try
        {
            TrialSnoozedEventArgs? args = null;
            banner.Snoozed += (_, e) => args = e;
            var menu = banner.BannerParts.Menu!;
            var entry = menu.Items.OfType<MenuEntry>().Single(m => m.Tag is TrialSnoozeOption.UntilWarning);
            entry.Header.ShouldBe("Until Nov 2, when the warning starts");
            entry.PerformClick();
            Dispatcher.UIThread.RunJobs();
            args.ShouldNotBeNull();
            args.Option.ShouldBe(TrialSnoozeOption.UntilWarning);
            args.Until.ShouldBe(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
            banner.IsCollapsed.ShouldBeTrue();
            banner.LiveRegion!.Announcement.ShouldBe("Trial banner snoozed until Nov 2.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Snooze_Menu_Opens_From_The_Keyboard()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner);
        try
        {
            banner.BannerParts.Snooze!.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            banner.BannerParts.Menu!.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            banner.BannerParts.Menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Tools_Follow_The_Flags()
    {
        var banner = Sample(new TrialBanner { IsDismissible = false, IsSnoozable = false });
        var window = TestHelpers.Show(banner);
        try
        {
            banner.BannerParts.Dismiss!.IsVisible.ShouldBeFalse();
            banner.BannerParts.Snooze!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pill_Opens_The_Card_In_A_Popover()
    {
        var banner = Sample(new TrialBanner { Variant = TrialBannerVariant.Pill, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
        var window = TestHelpers.Show(banner);
        try
        {
            banner.ActiveFace.ShouldBeSameAs(banner.PillFace);
            banner.PillFace!.Label.ShouldBe("9 days left");
            AutomationProperties.GetName(banner.PillFace).ShouldBe("Pro trial, 9 days left. Show trial details");

            banner.PillFace.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            banner.DetailsPopover.ShouldNotBeNull();
            banner.DetailsPopover.IsOpen.ShouldBeTrue();
            var card = banner.PopoverCard!;
            TestHelpers.PumpUntil(() => card.CardParts.Detail is not null).ShouldBeTrue();
            card.ActiveFace.ShouldBeSameAs(card.CardParts.Root);
            TextOf(card.CardParts.Detail).ShouldBe("9 days left · ends Nov 5");

            // The card's actions run on the pill's banner: a dismiss closes the popover and keeps the pill.
            var dismissed = 0;
            banner.Dismissed += (_, _) => dismissed++;
            card.CardParts.Dismiss!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            dismissed.ShouldBe(1);
            banner.DetailsPopover.IsOpen.ShouldBeFalse();
            banner.ActiveFace.ShouldBeSameAs(banner.PillFace);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Banner_Puts_The_Actions_Under_The_Copy()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner);
        try
        {
            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            var row = TestHelpers.Descendant<TrialBannerRow>(banner);
            row.IsCompact.ShouldBeFalse();
            var primary = banner.BannerParts.Primary!;
            var title = banner.BannerParts.Title!;
            primary.TranslatePoint(default, row)!.Value.X.ShouldBeGreaterThan(title.TranslatePoint(default, row)!.Value.X);
            TextBlock.GetMaxLines(banner.BannerParts.Detail!).ShouldBe(1);

            window.Width = 380;
            Dispatcher.UIThread.RunJobs();
            row.IsCompact.ShouldBeTrue();
            banner.Classes.Contains(TrialBanner.CompactPseudoClass).ShouldBeTrue();
            var copyLeft = title.TranslatePoint(default, row)!.Value.X;
            var actions = banner.BannerParts.Compare!.GetVisualParent()!;
            var actionsAt = ((Visual)actions).TranslatePoint(default, row)!.Value;
            actionsAt.X.ShouldBe(copyLeft, 0.5);
            actionsAt.Y.ShouldBeGreaterThan(title.TranslatePoint(default, row)!.Value.Y);
            TextBlock.GetMaxLines(banner.BannerParts.Detail!).ShouldBe(2);
            banner.BannerParts.Detail!.MinHeight.ShouldBe(36);
            banner.Bounds.Width.ShouldBe(380);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var banner = Sample(new TrialBanner
        {
            Plan = "Team",
            Labels = new TrialBannerLabels
            {
                ActiveTitle = (days, plan) => $"{plan}: noch {days}",
                Days = d => $"{d} Tage",
                AddPayment = "Zahlungsmethode hinzufügen"
            }
        });
        var window = TestHelpers.Show(banner);
        try
        {
            TextOf(banner.BannerParts.Title).ShouldBe("Team: noch 9 Tage");
            banner.BannerParts.Primary!.Content.ShouldBe("Zahlungsmethode hinzufügen");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Folding_Morphs_The_Surface_With_Full_Motion()
    {
        var banner = Sample(new TrialBanner());
        var window = TestHelpers.Show(banner, Motion.ArcMotionLevel.Full);
        try
        {
            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            var stage = banner.Stage!;
            var before = stage.SurfaceRect;
            before.Width.ShouldBe(800, 1);
            banner.Dismiss();
            Dispatcher.UIThread.RunJobs();
            stage.IsMorphing.ShouldBeTrue();
            TestHelpers.PumpUntil(() => !stage.IsMorphing, 5000).ShouldBeTrue();
            stage.SurfaceRect.Width.ShouldBeLessThan(200);
            banner.BannerParts.Root!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Actions_Trade_Labels_Quietly_Through_The_Button_Options()
    {
        foreach (var variant in new[] { TrialBannerVariant.Banner, TrialBannerVariant.Card })
        {
            var banner = Sample(new TrialBanner { Variant = variant });
            var window = TestHelpers.Show(banner);
            try
            {
                var face = variant == TrialBannerVariant.Banner ? banner.BannerParts : banner.CardParts;
                foreach (var action in new[] { face.Primary!, face.Compare! })
                {
                    action.LabelSwapMode.ShouldBe(ContentSwapMode.Label);
                    action.LabelSpring.ShouldBe(Motion.ArcSpring.Quick);
                    TestHelpers.Descendant<ArcContentSwap>(action, "PART_Label").Mode.ShouldBe(ContentSwapMode.Label);
                    TestHelpers.Descendant<SpringSizeDecorator>(action, "PART_LabelSlot").Spring.ShouldBe(Motion.ArcSpring.Quick);
                }
                // Other buttons keep Arc's defaults.
                face.Dismiss?.LabelSwapMode.ShouldBe(ContentSwapMode.BlurSwap);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
