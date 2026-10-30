using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The invented program the referral card tests play (the package ships none): three paid, one signed up, two invited; a
/// friend who signed up subscribes, an invited one signs up and subscribes, and the fifth paid referral unlocks the
/// milestone (five paid referrals, three free months).
/// </summary>
internal static class ReferralCardTestProgram
{
    public const string Link = "https://northwind.app/r/emma20";

    public static ReferralMilestone Milestone { get; } = new() { Goal = 5, Reward = "3 months free", Detail = "Applied to your next invoice" };

    public static List<Referral> Program(DateTimeOffset now) =>
    [
        new() { Id = "sample-6", Email = "jonas.weber@example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-1) },
        new() { Id = "sample-5", Email = "ana.souza@example.com", Stage = ReferralStage.Invited, InvitedAt = now.AddDays(-3) },
        new() { Id = "sample-4", Name = "Priya Raman", Email = "priya.raman@example.com", Stage = ReferralStage.SignedUp, InvitedAt = now.AddDays(-6) },
        new() { Id = "sample-3", Name = "Leo Martins", Email = "leo.martins@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-12) },
        new() { Id = "sample-2", Name = "Hana Sato", Email = "hana.sato@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-19) },
        new() { Id = "sample-1", Name = "Marcus Bell", Email = "marcus.bell@example.com", Stage = ReferralStage.Earned, InvitedAt = now.AddDays(-27) },
    ];

    public static ReferralCardSimulation Simulation() => new()
    {
        Referrals = Program(DateTimeOffset.Now),
        Beats =
        [
            new(TimeSpan.FromSeconds(1.4), "sample-4", ReferralStage.Earned),
            new(TimeSpan.FromSeconds(2.2), "sample-5", ReferralStage.SignedUp, "Ana Souza"),
            new(TimeSpan.FromSeconds(2.4), "sample-5", ReferralStage.Earned)
        ]
    };
}

/// <summary>
/// The referral card: address parsing and validation, amounts, the link's parts and share URLs, the sample program, chips
/// and their keyboard model, sending (success, failure, simulated), sharing, following a replaced list, the milestone, the
/// narrow layout and labels.
/// </summary>
public class ReferralCardTests
{
    private static readonly Func<TimeSpan, CancellationToken, Task> s_never = (_, token) => Task.Delay(Timeout.Infinite, token);
    private static readonly Func<TimeSpan, CancellationToken, Task> s_instant = (_, _) => Task.CompletedTask;

    /// <summary>A card with the test program's link, a $20 reward and its milestone, playing its simulation unless setup changes it.</summary>
    private static (ReferralCard Card, Window Window) Open(Action<ReferralCard>? setup = null, double width = 600,
                                                           ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var card = WithProgram(new ReferralCard { Delay = s_never });
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 1000;
        Pump();
        return (card, window);
    }

    private static ReferralCard WithProgram(ReferralCard card)
    {
        card.Link = ReferralCardTestProgram.Link;
        card.Reward = 20;
        card.Milestone = ReferralCardTestProgram.Milestone;
        card.Simulation = ReferralCardTestProgram.Simulation();
        return card;
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Referral> Program() =>
    [
        new() { Id = "c", Email = "carla@example.com", Stage = ReferralStage.Invited, InvitedAt = DateTimeOffset.Now },
        new() { Id = "b", Name = "Ben Ode", Email = "ben@example.com", Stage = ReferralStage.SignedUp, InvitedAt = DateTimeOffset.Now.AddDays(-2) },
        new() { Id = "a", Name = "Ada Moss", Email = "ada@example.com", Stage = ReferralStage.Earned, InvitedAt = DateTimeOffset.Now.AddDays(-5) },
    ];

    // ------------------------------------------------------------------ pure logic

    [Theory]
    [InlineData("ana@example.com", true)]
    [InlineData("first.last+tag@mail.example.co", true)]
    [InlineData("ana@example", false)]
    [InlineData("ana@@example.com", false)]
    [InlineData("@example.com", false)]
    [InlineData("ana@example.c", false)]
    [InlineData("ana example@example.com", false)]
    [InlineData("ana..b@example.com", false)]
    [InlineData("ana@-example.com", false)]
    [InlineData("", false)]
    public void IsEmail_Accepts_Plain_Addresses_Only(string text, bool expected) =>
        ReferralCardFormat.IsEmail(text).ShouldBe(expected);

    [Fact]
    public void Split_Takes_Commas_Spaces_Semicolons_And_New_Lines()
    {
        ReferralCardFormat.Split("a@x.com, b@x.com;c@x.com\n d@x.com\t<e@x.com>")
            .ShouldBe(["a@x.com", "b@x.com", "c@x.com", "d@x.com", "e@x.com"]);
        ReferralCardFormat.Split("  ,, ").ShouldBeEmpty();
    }

    [Fact]
    public void Money_Writes_The_Symbol_Or_The_Code()
    {
        ReferralCardFormat.Money(20, "USD").ShouldBe("$20");
        ReferralCardFormat.Money(1240, "eur").ShouldBe("€1,240");
        ReferralCardFormat.Money(20, "SEK").ShouldBe("20 SEK");
        ReferralCardFormat.MoneyAffixes("SEK").ShouldBe((null, " SEK"));
        ReferralCardFormat.MoneyAffixes("GBP").ShouldBe(("£", null));
    }

    [Fact]
    public void SplitLink_Mutes_The_Domain_And_Keeps_The_Code()
    {
        ReferralCardFormat.SplitLink("https://northwind.app/r/emma20").ShouldBe(("northwind.app/r/", "emma20"));
        ReferralCardFormat.SplitLink("emma20").ShouldBe((string.Empty, "emma20"));
    }

    [Fact]
    public void ShareUrl_Builds_Mailto_And_Fills_Formats()
    {
        var link = "https://northwind.app/r/emma20";
        var mail = ReferralCard.BuildShareUrl(ReferralShareChannel.Email, link, "Join me", subject: "Hi there");
        mail.ShouldBe("mailto:?subject=Hi%20there&body=Join%20me%0A%0Ahttps%3A%2F%2Fnorthwind.app%2Fr%2Femma20");
        ReferralCard.BuildShareUrl(ReferralShareChannel.Post, link, "Join me", "https://social.example/share?u={url}&t={text}")
            .ShouldBe("https://social.example/share?u=https%3A%2F%2Fnorthwind.app%2Fr%2Femma20&t=Join%20me");
        ReferralCard.BuildShareUrl(ReferralShareChannel.Network, link).ShouldBe(link);
    }

    [Fact]
    public void Count_And_Filled_Follow_The_Stages()
    {
        ReferralCardFormat.Count(Program()).ShouldBe((3, 2, 1));
        ReferralCardFormat.Segments(30).ShouldBe(12);
        ReferralCardFormat.Filled(4, 5).ShouldBe(4);
        ReferralCardFormat.Filled(15, 30).ShouldBe(6);
        ReferralCardFormat.Filled(9, 5).ShouldBe(5);
    }

    // ------------------------------------------------------------------ the sample

    [AvaloniaFact]
    public void Card_Without_Data_Or_Simulation_Is_Empty()
    {
        var card = new ReferralCard { Delay = s_instant };
        var window = TestHelpers.Show(card);
        window.Height = 1000;
        Pump();
        try
        {
            card.Simulation.ShouldBeNull();
            card.Link.ShouldBe(string.Empty);
            card.Reward.ShouldBe(0);
            card.Milestone.ShouldBeNull();
            card.DisplayedTitle.ShouldBe("Invite friends");
            card.DisplayedDescription.ShouldBe("Share your invite link, or invite friends by email.");
            card.Funnel.ShouldBe((0, 0, 0L));
            card.Items.Count.ShouldBe(0);
            card.RowViews.Count.ShouldBe(0);
            card.Bar!.IsEffectivelyVisible.ShouldBeFalse();
            card.Simulates.ShouldBeFalse();
            Pump();
            card.ProgramStarted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Referrals_Win_Over_The_Simulation()
    {
        var (card, window) = Open(c =>
        {
            c.Delay = s_instant;
            c.Referrals = Program();
        });
        try
        {
            Pump();
            card.Simulates.ShouldBeFalse();
            card.ProgramStarted.ShouldBeFalse();
            card.Items.Count.ShouldBe(3);
            card.Funnel.ShouldBe((3, 2, 20L));
            card.Referrals = null;
            card.Items.Count.ShouldBe(6, "without its own list the card shows the simulation's program");
            card.Funnel.ShouldBe((6, 4, 60L));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clearing_The_Simulation_Stops_The_Program_And_Empties_The_Card()
    {
        var (card, window) = Open();
        try
        {
            card.Items.Count.ShouldBe(6);
            card.Simulation = null;
            Pump();
            card.Simulates.ShouldBeFalse();
            card.Items.Count.ShouldBe(0);
            card.Funnel.ShouldBe((0, 0, 0L));
            card.Simulation = ReferralCardTestProgram.Simulation();
            Pump();
            card.Items.Count.ShouldBe(6, "a new simulation shows its own program");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Shows_Its_Program()
    {
        var (card, window) = Open();
        try
        {
            card.DisplayedTitle.ShouldBe("Give $20, get $20");
            card.Funnel.ShouldBe((6, 4, 60L));
            card.Bar!.Segments.ShouldBe(5);
            card.Bar.FillTarget.ShouldBe(3);
            card.Bar.IsUnlocked.ShouldBeFalse();
            AutomationProperties.GetHelpText(card.Bar).ShouldBe("3 of 5 paid referrals");
            var rows = card.RowViews;
            rows.Count.ShouldBe(6);
            rows[0].NameText.ShouldBe("jonas.weber@example.com");
            rows[0].StageText.ShouldBe("Invited");
            rows[2].StageText.ShouldBe("Signed up");
            rows[3].StageText.ShouldBe("Earned $20");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Simulation_Plays_Once_And_Unlocks_The_Milestone()
    {
        // The program starts by itself once the card is on screen; its pauses are skipped here.
        var reached = new List<ReferralMilestone>();
        var card = WithProgram(new ReferralCard { Delay = s_instant });
        card.MilestoneReached += (_, e) => reached.Add(e.Milestone);
        var window = TestHelpers.Show(card);
        window.Height = 1000;
        try
        {
            TestHelpers.PumpUntil(() => card.IsUnlocked).ShouldBeTrue();
            Pump();
            card.ProgramStarted.ShouldBeTrue();
            card.Funnel.ShouldBe((6, 5, 100L));
            card.IsUnlocked.ShouldBeTrue();
            card.Bar!.IsUnlocked.ShouldBeTrue();
            reached.Count.ShouldBe(1);
            reached[0].Reward.ShouldBe("3 months free");
            card.LastAnnouncement.ShouldBe("Milestone unlocked: 3 months free");
            card.RowViews.Count.ShouldBe(6);
            card.RowViews[2].StageText.ShouldBe("Earned $20");
            card.RowViews[1].NameText.ShouldBe("Ana Souza");
            card.RowViews[1].StageText.ShouldBe("Earned $20");

            await card.PlayProgramAsync();
            reached.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Plays_The_Program_Twice_As_Fast()
    {
        var (card, window) = Open(level: ArcMotionLevel.Reduced);
        try
        {
            card.Pace(TimeSpan.FromSeconds(2)).ShouldBe(TimeSpan.FromSeconds(1));
        }
        finally
        {
            window.Close();
        }
        var (full, fullWindow) = Open(level: ArcMotionLevel.Full);
        try
        {
            full.Pace(TimeSpan.FromSeconds(2)).ShouldBe(TimeSpan.FromSeconds(2));
        }
        finally
        {
            fullWindow.Close();
        }
    }

    // ------------------------------------------------------------------ chips

    [AvaloniaFact]
    public void Enter_Comma_Space_And_Tab_Turn_The_Draft_Into_Chips()
    {
        var (card, window) = Open(c => c.Referrals = []);
        try
        {
            var editor = card.Editor!;
            editor.Text = "a@example.com";
            card.HandleEmailKey(Key.Enter, KeyModifiers.None).ShouldBeTrue();
            editor.Text = "b@example.com";
            card.HandleEmailText(",").ShouldBeTrue();
            editor.Text = "c@example.com";
            card.HandleEmailText(" ").ShouldBeTrue();
            editor.Text = "d@example.com";
            card.HandleEmailKey(Key.Tab, KeyModifiers.None).ShouldBeTrue();
            card.ChipEmails.ShouldBe(["a@example.com", "b@example.com", "c@example.com", "d@example.com"]);
            editor.Text.ShouldBe(string.Empty);
            card.Hint.ShouldBe((ReferralCardHintKind.Ready, "4 addresses ready. Press Enter or Send."));
            // Tab in an empty field moves focus as usual.
            card.HandleEmailKey(Key.Tab, KeyModifiers.None).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Enter_In_The_Field_Adds_A_Chip()
    {
        var (card, window) = Open(c => c.Referrals = []);
        try
        {
            var editor = card.Editor!;
            editor.Focus();
            editor.Text = "ana@example.com";
            editor.CaretIndex = editor.Text.Length;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.ChipEmails.ShouldBe(["ana@example.com"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_In_An_Empty_Field_Removes_The_Last_Chip()
    {
        var (card, window) = Open(c => c.Referrals = []);
        try
        {
            card.AddAddresses(["a@example.com", "b@example.com"]);
            card.HandleEmailKey(Key.Back, KeyModifiers.None).ShouldBeTrue();
            card.ChipEmails.ShouldBe(["a@example.com"]);
            card.Editor!.Text = "x";
            card.Editor.CaretIndex = 1;
            card.HandleEmailKey(Key.Back, KeyModifiers.None).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Adds_Every_Address()
    {
        var (card, window) = Open(c => c.Referrals = []);
        try
        {
            card.HandleEmailPaste("a@example.com, b@example.com\nc@example.com").ShouldBeTrue();
            card.ChipEmails.Count.ShouldBe(3);
            card.HandleEmailPaste("single@example.com").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Invalid_Duplicate_Invited_And_Over_Limit_Addresses_Are_Refused_With_A_Note()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = Program();
            c.MaxInvites = 2;
        });
        try
        {
            card.AddAddresses(["not-an-address"]);
            card.Chips.Single().IsValid.ShouldBeFalse();
            AutomationProperties.GetName(card.Chips[0]).ShouldBe("not-an-address, not a valid address");
            card.Hint.ShouldBe((ReferralCardHintKind.Error, "not-an-address is not a valid email address."));
            card.RemoveChip(card.Chips[0], focus: false);
            card.Hint.Kind.ShouldBe(ReferralCardHintKind.Idle);

            card.AddAddresses(["new@example.com", "NEW@example.com"]);
            card.Hint.Text.ShouldBe("NEW@example.com is already in the list.");
            card.AddAddresses(["ada@example.com"]);
            card.Hint.Text.ShouldBe("ada@example.com was already invited.");
            card.AddAddresses(["two@example.com", "three@example.com"]);
            card.ChipEmails.ShouldBe(["new@example.com", "two@example.com"]);
            card.Hint.Text.ShouldBe("You can send up to 2 invites at a time.");
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ sending

    [AvaloniaFact]
    public async Task Send_Refuses_Nothing_And_Invalid_Chips()
    {
        var (card, window) = Open(c => c.Referrals = []);
        try
        {
            await card.SendAsync();
            card.Hint.ShouldBe((ReferralCardHintKind.Error, "Add at least one email address."));
            card.AddAddresses(["oops"]);
            await card.SendAsync();
            card.Hint.ShouldBe((ReferralCardHintKind.Error, "Fix or remove the addresses marked as not valid."));
            card.IsSending.ShouldBeFalse();
            card.Classes.Contains(":sending").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Send_Calls_The_Handler_And_Adds_The_Addresses_As_Invited()
    {
        var gate = new TaskCompletionSource();
        IReadOnlyList<string>? received = null;
        var (card, window) = Open(c =>
        {
            c.Referrals = Program();
            c.InviteHandler = (emails, _) =>
            {
                received = emails;
                return gate.Task;
            };
        });
        try
        {
            ReferralInvitesEventArgs? sent = null;
            card.InvitesSent += (_, e) => sent = e;
            card.AddAddresses(["x@example.com", "y@example.com"]);
            var sending = card.SendAsync();
            card.IsSending.ShouldBeTrue();
            card.Editor!.IsReadOnly.ShouldBeTrue();
            card.Editor.IsEnabled.ShouldBeTrue();
            card.SendButton!.IsLoading.ShouldBeTrue();
            card.Hint.Text.ShouldBe("Sending 2 invites…");
            received.ShouldBe(["x@example.com", "y@example.com"]);

            gate.SetResult();
            await sending;
            Pump();
            card.IsSending.ShouldBeFalse();
            card.Editor.IsReadOnly.ShouldBeFalse();
            card.Chips.ShouldBeEmpty();
            card.Hint.ShouldBe((ReferralCardHintKind.Sent, "Invites sent to 2 friends."));
            sent.ShouldNotBeNull();
            sent.IsSimulated.ShouldBeFalse();
            sent.Emails.ShouldBe(["x@example.com", "y@example.com"]);
            card.Funnel.Invited.ShouldBe(5);
            card.RowViews[0].NameText.ShouldBe("x@example.com");
            card.RowViews[0].StageText.ShouldBe("Invited");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Failed_Send_Keeps_The_Chips_And_Shows_The_Reason()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = [];
            c.InviteHandler = (_, _) => Task.FromException(new InvalidOperationException("One address bounced."));
        });
        try
        {
            card.AddAddresses(["x@example.com"]);
            await card.SendAsync();
            card.ChipEmails.ShouldBe(["x@example.com"]);
            card.Hint.ShouldBe((ReferralCardHintKind.Error, "One address bounced."));
            card.LastAnnouncement.ShouldBe("One address bounced.");
            card.Items.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Enter_In_An_Empty_Field_Sends()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = [];
            c.Simulate = false;
            c.Delay = s_instant;
        });
        try
        {
            card.AddAddresses(["x@example.com"]);
            card.HandleEmailKey(Key.Enter, KeyModifiers.None).ShouldBeTrue();
            await Task.Yield();
            Pump();
            card.Items.Count.ShouldBe(1);
            card.Hint.Text.ShouldBe("Simulated 1 invite, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Simulated_Send_Moves_Its_First_Address_Through_The_Stages()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = [];
            c.Simulate = true;
            c.Delay = s_instant;
        });
        try
        {
            ReferralInvitesEventArgs? sent = null;
            card.InvitesSent += (_, e) => sent = e;
            card.AddAddresses(["first@example.com", "second@example.com"]);
            await card.SendAsync();
            Pump();
            sent!.IsSimulated.ShouldBeTrue();
            var first = card.Items.Single(r => r.Email == "first@example.com");
            first.Stage.ShouldBe(ReferralStage.Earned);
            card.Items.Single(r => r.Email == "second@example.com").Stage.ShouldBe(ReferralStage.Invited);
            card.Funnel.ShouldBe((2, 1, 20L));
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ sharing and copy

    [AvaloniaFact]
    public void Share_Raises_The_Url_And_Says_What_Happened()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = [];
            c.ShareMessage = "Join me";
        });
        try
        {
            card.Share(ReferralShareChannel.Post);
            card.LastAnnouncement.ShouldBe("Simulated, nothing was posted.");
            card.IsShareConfirmed(ReferralShareChannel.Post).ShouldBeTrue();

            ReferralShareEventArgs? args = null;
            card.ShareRequested += (_, e) =>
            {
                args = e;
                e.Handled = true;
            };
            card.Share(ReferralShareChannel.Email);
            args.ShouldNotBeNull();
            args.Channel.ShouldBe(ReferralShareChannel.Email);
            args.Url.ShouldStartWith("mailto:?body=Join%20me");
            args.Link.ShouldBe(card.Link);
            card.LastAnnouncement.ShouldBe("Share by email: opened");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Share_Buttons_Are_Named_And_The_Copy_Button_Holds_The_Link()
    {
        var (card, window) = Open(c => c.Link = "https://northwind.app/r/kai");
        try
        {
            var names = card.GetVisualDescendantsOf<Button>().Select(AutomationProperties.GetName).ToList();
            names.ShouldContain("Share in a post");
            names.ShouldContain("Share with your network");
            names.ShouldContain("Share by email");
            var copy = TestHelpers.Descendant<CopyButton>(card);
            copy.Value.ShouldBe("https://northwind.app/r/kai");
            AutomationProperties.GetName(copy).ShouldBe("Copy invite link");
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------------ data

    [AvaloniaFact]
    public void A_Replaced_List_Updates_Rows_In_Place()
    {
        var program = Program();
        var (card, window) = Open(c => c.Referrals = program);
        try
        {
            var reached = 0;
            card.MilestoneReached += (_, _) => reached++;
            card.Milestone = new ReferralMilestone { Goal = 2, Reward = "A free month" };
            Pump();
            var benRow = card.RowViews[1];
            benRow.StageText.ShouldBe("Signed up");
            card.Referrals = [program[0], program[1].WithStage(ReferralStage.Earned), program[2]];
            Pump();
            card.RowViews[1].ShouldBeSameAs(benRow);
            benRow.StageText.ShouldBe("Earned $20");
            card.Funnel.ShouldBe((3, 2, 40L));
            card.IsUnlocked.ShouldBeTrue();
            reached.ShouldBe(1);
            card.Classes.Contains(":unlocked").ShouldBeTrue();

            card.Referrals = [];
            Pump();
            card.RowViews.ShouldBeEmpty();
            card.Funnel.ShouldBe((0, 0, 0L));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Null_Milestone_Hides_The_Bar()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = Program();
            c.Milestone = null;
        });
        try
        {
            card.Bar!.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Card_Puts_The_Share_Buttons_On_Their_Own_Row()
    {
        var (card, window) = Open(c => c.Referrals = Program(), width: 420);
        try
        {
            card.Classes.Contains(":narrow").ShouldBeTrue();
            var share = card.GetVisualDescendantsOf<Button>().First(b => AutomationProperties.GetName(b) == "Share in a post");
            var grid = (Grid)share.Parent!;
            Grid.GetRow(grid).ShouldBe(1);
            grid.ColumnDefinitions.All(c => c.Width.IsStar).ShouldBeTrue();
            window.Width = 600;
            Pump();
            card.Classes.Contains(":narrow").ShouldBeFalse();
            Grid.GetRow(grid).ShouldBe(0);
            Grid.GetColumn(grid).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Currency_Reword_The_Card()
    {
        var (card, window) = Open(c =>
        {
            c.Referrals = Program();
            c.Reward = 15;
            c.Currency = "EUR";
            c.Labels = new ReferralCardLabels
            {
                Title = reward => $"Offrez {reward}",
                Send = "Envoyer",
                StageEarned = reward => $"{reward} gagnés",
            };
        });
        try
        {
            card.DisplayedTitle.ShouldBe("Offrez €15");
            card.SendButton!.Content.ShouldBe("Envoyer");
            card.RowViews[2].StageText.ShouldBe("€15 gagnés");
            card.Funnel.EarnedTotal.ShouldBe(15);
            card.Title = "Custom";
            card.DisplayedTitle.ShouldBe("Custom");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Funnel_Numbers_Roll_In_600ms_On_The_Standard_Curve()
    {
        var (card, window) = Open();
        try
        {
            var numbers = card.GetVisualDescendantsOf<TKCode.Arc.Primitives.RollingDigits>().ToList();
            numbers.Count.ShouldBe(3);
            numbers.ShouldAllBe(n => Equals(n.WheelMotion, MotionSpec.Tween(TimeSpan.FromMilliseconds(600), ArcEasings.Standard)));
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class ReferralCardTestExtensions
{
    public static IEnumerable<T> GetVisualDescendantsOf<T>(this Avalonia.Visual root) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<T>();
}
