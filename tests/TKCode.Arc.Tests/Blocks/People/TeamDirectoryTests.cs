using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.People;

/// <summary>
/// <see cref="TeamDirectory"/> on invented samples (six people in three teams): the search and filter rules, the selection
/// kept by id, events, the empty state, the search box keys, copying, the width breakpoints, screen reader names and the
/// motion levels (rows closing gaps and fading, the highlights gliding, the profile cross-fading).
/// </summary>
public class TeamDirectoryTests
{
    internal static TeamDirectoryPerson[] Samples() =>
    [
        P("lena", "Lena Hartmann", "Interaction designer", "Design", true, "Checkout redesign", "Vienna", "CET"),
        P("theo", "Theo Brandt", "Mobile engineer", "Engineering", true, "Offline sync layer", "Manchester", "GMT"),
        P("priya", "Priya Nair", "Head of design", "Design", false, "Brand refresh", "Malmö", "CET"),
        P("jonas", "Jonas Weber", "Backend engineer", "Engineering", true, "Billing service", "Munich", "CET"),
        P("camila", "Camila Ortiz", "Office manager", "Operations", false, "Hiring logistics", "Boston", "ET"),
        P("fiona", "Fiona Byrne", "Account manager", "Operations", true, "Renewals program", "Cork", "GMT"),
    ];

    private static TeamDirectoryPerson P(string id, string name, string role, string team, bool available, string focus,
        string city, string tz) => new(name, role, team, id)
    {
        Availability = available ? TeamDirectoryAvailability.Available : TeamDirectoryAvailability.Away,
        Focus = focus,
        City = city,
        TimeZone = tz,
        Email = id + "@northwind.test",
        Bio = "Prototypes and reviews. Ask about anything.",
    };

    private sealed class Rig : IDisposable
    {
        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 900, Action<TeamDirectory>? configure = null)
        {
            Block = new TeamDirectory();
            Block.People.AddRange(Samples());
            configure?.Invoke(Block);
            Block.PersonSelected += (_, e) => Selected.Add(e);
            Block.EmailCopied += (_, e) => Copies.Add(e);
            Host = new Border { Width = width, Child = Block, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1200;
            Window.Height = 1000;
            Pump();
        }

        public TeamDirectory Block { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<TeamDirectoryPersonSelectedEventArgs> Selected { get; } = new();

        public List<TeamDirectoryEmailCopiedEventArgs> Copies { get; } = new();

        public TeamDirectoryList List => Block.List!;

        public TeamDirectoryDetailHost Detail => Block.Detail!;

        public TeamDirectoryFilterStrip Filters => Block.Filters!;

        public TeamDirectoryProfile Profile => Detail.CurrentProfile!;

        public TeamDirectoryRow Row(string key) => List.Rows.First(r => r.Person.Key == key);

        public TeamDirectoryFilterButton Filter(string? team) => Filters.Buttons.First(b => b.Team == team);

        public string[] Keys => List.Rows.Select(r => r.Person.Key).ToArray();

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Advance(ManualFrameClock clock, TimeSpan time)
        {
            clock.Advance(time);
            Pump();
        }

        public void Click(Avalonia.Controls.Button button)
        {
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }

        public void Dispose() => Window.Close();
    }

    #region Rules (no UI)

    [Fact]
    public void Search_Trims_Ignores_Case_And_Matches_One_Piece()
    {
        var lena = Samples()[0];
        TeamDirectoryQuery.Matches(lena, "  lena  ").ShouldBeTrue();
        TeamDirectoryQuery.Matches(lena, "HARTMANN").ShouldBeTrue();
        TeamDirectoryQuery.Matches(lena, "lena hart").ShouldBeTrue();
        // One substring, not words: the other order does not match.
        TeamDirectoryQuery.Matches(lena, "hartmann lena").ShouldBeFalse();
        TeamDirectoryQuery.Matches(lena, "").ShouldBeTrue();
        TeamDirectoryQuery.Matches(lena, "   ").ShouldBeTrue();
    }

    [Fact]
    public void Search_Covers_Name_Role_Team_And_City_Only()
    {
        var people = Samples();
        var camila = people[4];
        // "operations" is Camila's team, not in her role.
        TeamDirectoryQuery.Matches(camila, "operations").ShouldBeTrue();
        TeamDirectoryQuery.Matches(camila, "office").ShouldBeTrue();
        TeamDirectoryQuery.Matches(camila, "boston").ShouldBeTrue();
        TeamDirectoryQuery.Matches(camila, "northwind").ShouldBeFalse();
        TeamDirectoryQuery.Matches(camila, "ET").ShouldBeFalse();
        TeamDirectoryQuery.Matches(camila, "hiring").ShouldBeFalse();
        TeamDirectoryQuery.Matches(camila, "anything").ShouldBeFalse();
    }

    [Fact]
    public void Search_Does_Not_Fold_Accents()
    {
        var priya = Samples()[2];
        TeamDirectoryQuery.Matches(priya, "malmö").ShouldBeTrue();
        TeamDirectoryQuery.Matches(priya, "MALMÖ").ShouldBeTrue();
        TeamDirectoryQuery.Matches(priya, "malmo").ShouldBeFalse();
    }

    [Fact]
    public void Shown_Person_Is_The_Picked_One_Else_The_First_Match()
    {
        var people = Samples();
        var visible = TeamDirectoryQuery.Visible(people, "Engineering", null, null);
        visible.Select(p => p.Key).ShouldBe(["theo", "jonas"]);
        TeamDirectoryQuery.Displayed(visible, "jonas")!.Key.ShouldBe("jonas");
        TeamDirectoryQuery.Displayed(visible, "lena")!.Key.ShouldBe("theo");
        TeamDirectoryQuery.Displayed(visible, null)!.Key.ShouldBe("theo");
        TeamDirectoryQuery.Displayed([], "lena").ShouldBeNull();
    }

    [Fact]
    public void Teams_Follow_First_Appearance_Unless_Given_And_Counts_Are_Totals()
    {
        var people = Samples();
        TeamDirectoryQuery.Teams(people, null).ShouldBe(["Design", "Engineering", "Operations"]);
        TeamDirectoryQuery.Teams(people, ["Operations", "Design"]).ShouldBe(["Operations", "Design"]);
        TeamDirectoryQuery.Teams(people, []).ShouldBe(["Design", "Engineering", "Operations"]);
        TeamDirectoryQuery.Count(people, null).ShouldBe(6);
        TeamDirectoryQuery.Count(people, "Design").ShouldBe(2);
    }

    #endregion

    #region Structure, search and filter

    [AvaloniaFact]
    public void Shows_Everyone_With_Totals_And_The_First_Profile()
    {
        using var rig = new Rig();
        rig.Block.CountText!.Text.ShouldBe("6");
        rig.Filters.Buttons.Select(b => $"{b.Label} {b.CountText}").ShouldBe(["All 6", "Design 2", "Engineering 2", "Operations 2"]);
        rig.Filter(null).IsChecked.ShouldBe(true);
        rig.Keys.ShouldBe(["lena", "theo", "priya", "jonas", "camila", "fiona"]);
        rig.Block.DisplayedPerson!.Key.ShouldBe("lena");
        rig.Row("lena").IsChecked.ShouldBe(true);
        rig.List.Highlighted.ShouldBe(rig.Row("lena"));
        rig.Profile.NameText.ShouldBe("Lena Hartmann");
        rig.Profile.StatusText.ShouldBe("Available");
        rig.Profile.TimeZoneText.ShouldBe("CET");
        rig.Block.MatchCount.ShouldBe(6);
        rig.Block.SelectedPersonId.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Rows_Are_52_High_And_2_Apart_With_The_Highlight_On_The_Shown_Row()
    {
        using var rig = new Rig();
        rig.Row("theo").Bounds.Top.ShouldBe(54, 0.01);
        rig.Row("theo").Bounds.Height.ShouldBe(52, 0.01);
        rig.List.Bounds.Height.ShouldBe(6 * 52 + 5 * 2, 0.01);
        rig.List.Indicator!.CurrentRect.ShouldBe(rig.Row("lena").Bounds);
    }

    [AvaloniaFact]
    public void Search_Filters_On_Every_Change_And_Keeps_The_Counts()
    {
        using var rig = new Rig();
        rig.Block.SearchText = "ma";
        rig.Pump();
        // Hartmann, Manchester, Malmö, manager.
        rig.Keys.ShouldBe(["lena", "theo", "priya", "camila", "fiona"]);
        rig.Block.SearchText = "man";
        rig.Pump();
        rig.Keys.ShouldBe(["lena", "theo", "camila", "fiona"]);
        rig.Block.CountText!.Text.ShouldBe("6");
        rig.Filters.Buttons.Select(b => b.CountText).ShouldBe(["6", "2", "2", "2"]);
        rig.Block.MatchCount.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Team_And_Search_Combine_And_Keep_Each_Other()
    {
        using var rig = new Rig();
        rig.Block.SearchText = "e";
        rig.Click(rig.Filter("Engineering"));
        rig.Block.SelectedTeam.ShouldBe("Engineering");
        rig.Filter("Engineering").IsChecked.ShouldBe(true);
        rig.Filter(null).IsChecked.ShouldBe(false);
        rig.Keys.ShouldBe(["theo", "jonas"]);
        rig.Block.SearchText.ShouldBe("e");
        rig.Block.SearchText = "jonas";
        rig.Pump();
        rig.Keys.ShouldBe(["jonas"]);
        rig.Block.SelectedTeam.ShouldBe("Engineering");
        rig.Click(rig.Filter(null));
        rig.Block.SelectedTeam.ShouldBeNull();
        rig.Keys.ShouldBe(["jonas"]);
    }

    [AvaloniaFact]
    public void The_Pick_Is_Kept_By_Id_And_Comes_Back()
    {
        using var rig = new Rig();
        rig.Click(rig.Row("jonas"));
        rig.Block.SelectedPersonId.ShouldBe("jonas");
        rig.Block.DisplayedPerson!.Key.ShouldBe("jonas");
        rig.Click(rig.Filter("Design"));
        // Jonas is filtered out: the first match shows, the pick is unchanged.
        rig.Block.DisplayedPerson!.Key.ShouldBe("lena");
        rig.Block.SelectedPersonId.ShouldBe("jonas");
        rig.Row("lena").IsChecked.ShouldBe(true);
        rig.Click(rig.Filter(null));
        rig.Block.DisplayedPerson!.Key.ShouldBe("jonas");
        rig.Profile.Person.Key.ShouldBe("jonas");
        // None of that was a pick.
        rig.Selected.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void PersonSelected_Is_Raised_For_Picks_Only()
    {
        var command = new RecordingCommand();
        using var rig = new Rig(configure: b => b.PersonSelectedCommand = command);
        rig.Click(rig.Row("priya"));
        rig.Selected.Count.ShouldBe(1);
        rig.Selected[0].Person.Key.ShouldBe("priya");
        rig.Selected[0].PreviousPerson!.Key.ShouldBe("lena");
        command.Parameters.ShouldBe([rig.Selected[0].Person]);
        // Picking the person already picked changes nothing.
        rig.Click(rig.Row("priya"));
        rig.Selected.Count.ShouldBe(1);
        // Code and filtering do not count as picks.
        rig.Block.SelectPerson("fiona");
        rig.Pump();
        rig.Block.DisplayedPerson!.Key.ShouldBe("fiona");
        rig.Block.SearchText = "theo";
        rig.Pump();
        rig.Selected.Count.ShouldBe(1);
        command.Parameters.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_First_Person_Counts_As_Picked_At_First()
    {
        using var rig = new Rig();
        rig.Click(rig.Row("lena"));
        rig.Selected.ShouldBeEmpty();
        rig.Block.SearchText = "eng";
        rig.Pump();
        // Theo shows as the first match; picking him is a real change.
        rig.Click(rig.Row("theo"));
        rig.Selected.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Enter_And_Space_Pick_The_Focused_Row()
    {
        using var rig = new Rig();
        rig.Row("camila").Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Block.DisplayedPerson!.Key.ShouldBe("camila");
        rig.Row("fiona").Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Space);
        rig.Block.DisplayedPerson!.Key.ShouldBe("fiona");
        rig.Selected.Select(e => e.Person.Key).ShouldBe(["camila", "fiona"]);
        // Rows never toggle themselves off.
        rig.Key(PhysicalKey.Space);
        rig.Row("fiona").IsChecked.ShouldBe(true);
    }

    [AvaloniaFact]
    public void No_Match_Shows_The_Message_And_Clear_Filters_Resets_Both()
    {
        using var rig = new Rig();
        rig.Click(rig.Row("theo"));
        rig.Click(rig.Filter("Design"));
        rig.Block.SearchText = "xyz";
        rig.Pump();
        rig.Block.Classes.Contains(":empty-results").ShouldBeTrue();
        rig.List.Rows.ShouldBeEmpty();
        rig.Block.EmptyMessage!.IsVisible.ShouldBeTrue();
        rig.Block.NoMatchText!.Text.ShouldBe("Nobody found for “xyz”");
        rig.Detail.Current.ShouldBeOfType<TeamDirectoryNoProfile>();
        rig.Block.DisplayedPerson.ShouldBeNull();
        rig.List.Indicator!.CurrentOpacity.ShouldBe(0);
        rig.Click(rig.Block.ClearFiltersButton!);
        rig.Block.SearchText.ShouldBe(string.Empty);
        rig.Block.SelectedTeam.ShouldBeNull();
        rig.Block.EmptyMessage.IsVisible.ShouldBeFalse();
        rig.Keys.Length.ShouldBe(6);
        // The pick survived.
        rig.Block.DisplayedPerson!.Key.ShouldBe("theo");
        rig.List.Indicator.CurrentRect.ShouldBe(rig.Row("theo").Bounds);
    }

    #endregion

    #region Search box

    [AvaloniaFact]
    public void Typing_Updates_SearchText_And_The_Clear_Button()
    {
        using var rig = new Rig();
        var input = rig.Block.SearchInput!;
        rig.Block.ClearSearchButton!.IsVisible.ShouldBeFalse();
        input.Text = "fio";
        rig.Pump();
        rig.Block.SearchText.ShouldBe("fio");
        rig.Block.ClearSearchButton.IsVisible.ShouldBeTrue();
        rig.Keys.ShouldBe(["fiona"]);
        rig.Block.SearchText = "jo";
        rig.Pump();
        input.Text.ShouldBe("jo");
    }

    [AvaloniaFact]
    public void Escape_Clears_The_Search_And_Keeps_The_Focus()
    {
        using var rig = new Rig();
        rig.Block.FocusSearch().ShouldBeTrue();
        rig.Block.SearchText = "camila";
        rig.Pump();
        rig.Key(PhysicalKey.Escape);
        rig.Block.SearchText.ShouldBe(string.Empty);
        rig.Block.SearchInput!.IsFocused.ShouldBeTrue();
        // With nothing typed Escape is left alone.
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        rig.Block.SearchInput.RaiseEvent(args);
        args.Handled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Clear_Button_Empties_The_Search_And_Focuses_The_Input()
    {
        using var rig = new Rig();
        rig.Block.SearchText = "o";
        rig.Pump();
        rig.Click(rig.Block.ClearSearchButton!);
        rig.Block.SearchText.ShouldBe(string.Empty);
        rig.Block.SearchInput!.IsFocused.ShouldBeTrue();
        rig.Block.ClearSearchButton!.IsVisible.ShouldBeFalse();
    }

    #endregion

    #region Copy

    [AvaloniaFact]
    public void A_Copy_Shows_Copied_Announces_And_Rests_Again()
    {
        var copied = new List<string>();
        using var rig = new Rig(configure: b => b.CopyAsync = (text, _) =>
        {
            copied.Add(text);
            return Task.CompletedTask;
        });
        var profile = rig.Profile;
        var button = profile.CopyButton!;
        AutomationProperties.GetName(button).ShouldBe("Copy email address");
        button.Label.ShouldBe("Copy");
        rig.Click(button);
        TestHelpers.PumpUntil(() => rig.Copies.Count == 1).ShouldBeTrue();
        copied.ShouldBe(["lena@northwind.test"]);
        profile.CopyState.ShouldBe(CopyState.Copied);
        button.Label.ShouldBe("Copied");
        button.Classes.Contains(":copied").ShouldBeTrue();
        AutomationProperties.GetName(button).ShouldBe("Email address copied");
        profile.Announcer!.Announcement.ShouldBe("Email address of Lena Hartmann copied.");
        rig.Copies[0].Succeeded.ShouldBeTrue();
        rig.Copies[0].Person.Key.ShouldBe("lena");
        rig.Copies[0].Email.ShouldBe("lena@northwind.test");
        profile.ResetCopy();
        profile.CopyState.ShouldBe(CopyState.Idle);
        button.Label.ShouldBe("Copy");
        profile.Announcer.Announcement.ShouldBeNull();
        AutomationProperties.GetName(button).ShouldBe("Copy email address");
    }

    [AvaloniaFact]
    public void A_Failed_Copy_Shows_Retry_And_Reports_The_Error()
    {
        using var rig = new Rig(configure: b => b.CopyAsync = (_, _) => throw new InvalidOperationException("Clipboard busy"));
        var profile = rig.Profile;
        rig.Click(profile.CopyButton!);
        TestHelpers.PumpUntil(() => rig.Copies.Count == 1).ShouldBeTrue();
        profile.CopyState.ShouldBe(CopyState.Error);
        profile.CopyButton!.Label.ShouldBe("Retry");
        profile.CopyButton.Classes.Contains(":error").ShouldBeTrue();
        // The name stays; the failure is announced.
        AutomationProperties.GetName(profile.CopyButton).ShouldBe("Copy email address");
        profile.Announcer!.Announcement.ShouldBe("Could not copy the email address. Try again.");
        rig.Copies[0].Succeeded.ShouldBeFalse();
        rig.Copies[0].Error.ShouldBeOfType<InvalidOperationException>();
    }

    [AvaloniaFact]
    public void The_Copy_State_Belongs_To_The_Profile_And_Rests_After_The_Delay()
    {
        using var rig = new Rig(configure: b =>
        {
            b.CopyAsync = (_, _) => Task.CompletedTask;
            b.CopyResetDelay = TimeSpan.FromMilliseconds(1800);
        });
        var first = rig.Profile;
        first.CopyResetDelay.ShouldBe(TimeSpan.FromMilliseconds(1800));
        rig.Click(first.CopyButton!);
        TestHelpers.PumpUntil(() => first.CopyState == CopyState.Copied).ShouldBeTrue();
        // A second success while Copied keeps the state (and does not restart the time).
        first.ShowCopyResult(true);
        first.CopyState.ShouldBe(CopyState.Copied);
        // The reset timer runs (headless timers only fire on a pushed frame; ResetCopy is what it calls).
        first.IsCopyResetPending.ShouldBeTrue();
        rig.Click(rig.Row("theo"));
        rig.Profile.ShouldNotBe(first);
        rig.Profile.CopyState.ShouldBe(CopyState.Idle);
        rig.Profile.CopyButton!.Label.ShouldBe("Copy");
        // The old profile left: its timer stopped with it.
        first.IsCopyResetPending.ShouldBeFalse();
        first.ResetCopy();
        first.CopyState.ShouldBe(CopyState.Idle);
        first.IsCopyResetPending.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Email_Link_Raises_An_Event_That_Can_Take_Over()
    {
        using var rig = new Rig();
        var clicked = new List<string>();
        rig.Block.EmailLinkClicked += (_, e) =>
        {
            clicked.Add(e.Person.Key);
            e.Handled = true;
        };
        rig.Profile.EmailLink!.Text.ShouldBe("lena@northwind.test");
        rig.Click(rig.Profile.EmailLink);
        clicked.ShouldBe(["lena"]);
    }

    #endregion

    #region Breakpoints and frame

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        using var rig = new Rig(width: 700);
        rig.Block.Classes.Contains(":narrow").ShouldBeFalse();
        rig.Detail.IsCopyIconOnly.ShouldBeFalse();
        rig.Detail.LabelWidth.ShouldBe(96);
        rig.Detail.ContentPadding.ShouldBe(new Thickness(24));
        // Two columns: the list is max(196, 36%).
        rig.List.Bounds.Width.ShouldBe((700 - 2) * 0.36 - 16, 0.6);

        rig.Host.Width = 600;
        rig.Pump();
        rig.Block.Classes.Contains(":narrow").ShouldBeTrue();
        rig.Block.Classes.Contains(":stacked").ShouldBeFalse();
        rig.Detail.IsCopyIconOnly.ShouldBeTrue();
        rig.Profile.CopyButton!.IsIconOnly.ShouldBeTrue();
        rig.Profile.CopyButton.Bounds.Width.ShouldBe(28, 0.01);
        rig.Detail.LabelWidth.ShouldBe(76);
        rig.Detail.ContentPadding.ShouldBe(new Thickness(20));

        rig.Host.Width = 480;
        rig.Pump();
        rig.Block.Classes.Contains(":stacked").ShouldBeTrue();
        rig.Profile.CopyButton.IsIconOnly.ShouldBeFalse();
        rig.Detail.ContentPadding.ShouldBe(new Thickness(16, 20));
        // One column: the profile sits under the list, full width.
        var detailTop = rig.Detail.TranslatePoint(default, rig.Block)!.Value.Y;
        var listBottom = rig.List.TranslatePoint(new Point(0, rig.List.Bounds.Height), rig.Block)!.Value.Y;
        detailTop.ShouldBeGreaterThan(listBottom);
        rig.Detail.Bounds.Width.ShouldBe(480 - 2, 0.6);
        rig.Filter("Design").IsCountVisible.ShouldBeTrue();

        rig.Host.Width = 380;
        rig.Pump();
        rig.Block.Classes.Contains(":compact").ShouldBeTrue();
        rig.Filter("Design").IsCountVisible.ShouldBeFalse();
        rig.Detail.LabelWidth.ShouldBe(84);
    }

    [AvaloniaFact]
    public void Two_Columns_Keep_The_Body_At_Least_432_High()
    {
        using var rig = new Rig(width: 900);
        TestHelpers.Descendant<TeamDirectoryBody>(rig.Block).Bounds.Height.ShouldBeGreaterThanOrEqualTo(432);
        rig.Host.Width = 450;
        rig.Pump();
        var body = TestHelpers.Descendant<TeamDirectoryBody>(rig.Block);
        body.IsStacked.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Framed_Draws_A_Card_And_Flat_Does_Not()
    {
        using var rig = new Rig();
        rig.Block.Classes.Contains(":framed").ShouldBeTrue();
        rig.Block.BorderThickness.ShouldBe(new Thickness(1));
        rig.Block.CornerRadius.ShouldBe(new CornerRadius(16));
        rig.Block.IsFramed = false;
        rig.Pump();
        rig.Block.BorderThickness.ShouldBe(new Thickness(0));
        rig.Block.CornerRadius.ShouldBe(new CornerRadius(0));
    }

    #endregion

    #region Data and words

    [AvaloniaFact]
    public void A_Source_Collection_Is_Followed()
    {
        var source = new ObservableCollection<TeamDirectoryPerson>(Samples().Take(3));
        using var rig = new Rig(configure: b => b.PeopleSource = source);
        rig.Keys.ShouldBe(["lena", "theo", "priya"]);
        rig.Block.CountText!.Text.ShouldBe("3");
        source.Add(P("ada", "Ada Kim", "Support lead", "Support", true, "Help center", "Seoul", "KST"));
        rig.Pump();
        rig.Keys.ShouldBe(["lena", "theo", "priya", "ada"]);
        rig.Filters.Buttons.Select(b => b.Label).ShouldBe(["All", "Design", "Engineering", "Support"]);
        source.RemoveAt(0);
        rig.Pump();
        rig.Block.DisplayedPerson!.Key.ShouldBe("theo");
    }

    [AvaloniaFact]
    public void Changing_A_Person_Updates_The_Row_The_Profile_And_The_Search()
    {
        using var rig = new Rig();
        rig.Block.SearchText = "len";
        rig.Pump();
        var lena = rig.Block.EffectivePeople[0];
        lena.Role = "Design lead";
        rig.Pump();
        rig.Profile.RoleText.ShouldBe("Design lead");
        lena.Name = "Helena Hartmann";
        rig.Pump();
        rig.Keys.ShouldBe(["lena"]);
        rig.Profile.NameText.ShouldBe("Helena Hartmann");
        lena.Availability = TeamDirectoryAvailability.Away;
        rig.Pump();
        rig.Profile.StatusText.ShouldBe("Away");
        rig.Profile.Classes.Contains(":away").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        using var rig = new Rig(configure: b => b.Teams.AddRange(["Operations", "Design", "Engineering"]));
        rig.Filters.Buttons.Select(b => b.Label).ShouldBe(["All", "Operations", "Design", "Engineering"]);
        rig.Block.Labels = new TeamDirectoryLabels
        {
            Title = "Personas",
            AllTeams = "Todos",
            StatusLabel = "Estado",
            Available = "Disponible",
            Copy = "Copiar",
            NoMatches = q => $"Sin resultados: {q}",
        };
        rig.Pump();
        TestHelpers.Descendant<TextBlock>(rig.Block, TeamDirectory.TitlePart).Text.ShouldBe("Personas");
        rig.Filter(null).Label.ShouldBe("Todos");
        rig.Profile.StatusLabel.ShouldBe("Estado");
        rig.Profile.StatusText.ShouldBe("Disponible");
        rig.Profile.CopyButton!.Label.ShouldBe("Copiar");
        rig.Block.SearchText = "zz";
        rig.Pump();
        rig.Block.NoMatchText!.Text.ShouldBe("Sin resultados: zz");
    }

    [AvaloniaFact]
    public void Screen_Reader_Names_Are_Set()
    {
        using var rig = new Rig();
        AutomationProperties.GetName(rig.Filters).ShouldBe("Team filter");
        AutomationProperties.GetName(rig.List).ShouldBe("Directory");
        AutomationProperties.GetName(rig.Filter("Design")).ShouldBe("Design, 2");
        AutomationProperties.GetName(rig.Row("priya")).ShouldBe("Priya Nair, Head of design");
        AutomationProperties.GetName(TestHelpers.Descendant<Avatar>(rig.Row("priya"))).ShouldBe("Priya Nair, away");
        AutomationProperties.GetName(TestHelpers.Descendant<Avatar>(rig.Row("theo"))).ShouldBe("Theo Brandt, online");
        AutomationProperties.GetName(rig.Block.SearchInput!).ShouldBe("Search people");
        rig.Block.SearchInput!.PlaceholderText.ShouldBe("Find by name, role or city");
        AutomationProperties.GetName(rig.Block.ClearSearchButton!).ShouldBe("Clear search");
        AutomationProperties.GetName(rig.Profile).ShouldBe("Lena Hartmann, Interaction designer");
        AutomationProperties.GetLiveSetting(rig.Detail).ShouldBe(AutomationLiveSetting.Polite);
        TestHelpers.Descendant<Avatar>(rig.Row("theo")).Status.ShouldBe(AvatarStatus.Online);
        TestHelpers.Descendant<Avatar>(rig.Row("priya")).Status.ShouldBe(AvatarStatus.Offline);
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void Full_Motion_Closes_Gaps_On_The_Layout_Spring_And_Fades_Leavers()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var priya = rig.Row("priya");
        var camila = rig.Row("camila");
        rig.Click(rig.Filter("Design"));
        rig.Keys.ShouldBe(["lena", "priya"]);
        // Priya moved up one row and starts from where she was drawn.
        rig.List.Motion(priya).Offset.Value.Y.ShouldBe(54, 0.5);
        rig.List.LeavingRows.Count().ShouldBe(4);
        camila.IsHitTestVisible.ShouldBeFalse();
        rig.Advance(clock, TimeSpan.FromMilliseconds(100));
        var y100 = rig.List.Motion(priya).Offset.Value.Y;
        y100.ShouldBeInRange(25, 50);
        rig.List.Motion(camila).Opacity.Value.ShouldBeInRange(0.05, 0.75);
        rig.Advance(clock, TimeSpan.FromMilliseconds(120));
        // The fade is over (≈170ms) and the leavers are gone; the spring is still settling.
        rig.List.LeavingRows.ShouldBeEmpty();
        rig.List.Children.Contains(camila).ShouldBeFalse();
        var min = double.MaxValue;
        for (var i = 0; i < 60; i++)
        {
            rig.Advance(clock, TimeSpan.FromMilliseconds(16));
            min = Math.Min(min, rig.List.Motion(priya).Offset.Value.Y);
        }
        // Critically damped: no overshoot past the new place.
        min.ShouldBeGreaterThanOrEqualTo(-0.01);
        rig.List.Motion(priya).Offset.Value.Y.ShouldBe(0, 0.2);
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_New_Rows_In_In_Place()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full, configure: b => b.SelectedTeam = "Design");
        rig.Click(rig.Filter(null));
        var theo = rig.Row("theo");
        rig.List.Motion(theo).Opacity.Value.ShouldBe(0, 0.05);
        rig.List.Motion(theo).Offset.Value.ShouldBe(default);
        rig.Advance(clock, TimeSpan.FromMilliseconds(80));
        rig.List.Motion(theo).Opacity.Value.ShouldBeInRange(0.3, 0.95);
        rig.Advance(clock, TimeSpan.FromMilliseconds(120));
        rig.List.Motion(theo).Opacity.Value.ShouldBe(1, 0.001);
    }

    [AvaloniaFact]
    public void Full_Motion_Glides_The_Row_Highlight_Without_Overshoot()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var indicator = rig.List.Indicator!;
        var target = rig.Row("camila").Bounds;
        rig.Click(rig.Row("camila"));
        indicator.CurrentRect.Top.ShouldBe(0, 0.5);
        rig.Advance(clock, TimeSpan.FromMilliseconds(100));
        indicator.CurrentRect.Top.ShouldBeInRange(40, 180);
        var max = 0.0;
        for (var i = 0; i < 70; i++)
        {
            rig.Advance(clock, TimeSpan.FromMilliseconds(16));
            max = Math.Max(max, indicator.CurrentRect.Top);
        }
        max.ShouldBeLessThanOrEqualTo(target.Top + 0.01);
        indicator.CurrentRect.Top.ShouldBe(target.Top, 0.3);
    }

    [AvaloniaFact]
    public void Full_Motion_Glides_The_Team_Pill()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var pill = rig.Filters.Indicator!;
        var all = rig.Filter(null).Bounds;
        var design = rig.Filter("Design").Bounds;
        pill.CurrentRect.ShouldBe(all);
        rig.Click(rig.Filter("Design"));
        rig.Advance(clock, TimeSpan.FromMilliseconds(100));
        pill.CurrentRect.Left.ShouldBeInRange(all.Left + 5, design.Left - 5);
        pill.CurrentRect.Width.ShouldBeInRange(all.Width, design.Width);
        rig.Advance(clock, TimeSpan.FromMilliseconds(1000));
        pill.CurrentRect.Left.ShouldBe(design.Left, 0.2);
        pill.CurrentRect.Width.ShouldBe(design.Width, 0.2);
    }

    [AvaloniaFact]
    public void Full_Motion_Cross_Fades_The_Profile_With_A_Rise()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        var old = rig.Profile;
        rig.Click(rig.Row("jonas"));
        var incoming = rig.Profile;
        incoming.ShouldNotBe(old);
        rig.Detail.Leaving.ShouldBe([old]);
        old.IsLeaving.ShouldBeTrue();
        rig.Detail.MotionOf(incoming).Y.ShouldBe(4, 0.3);
        rig.Detail.MotionOf(incoming).Opacity.ShouldBe(0, 0.1);
        rig.Advance(clock, TimeSpan.FromMilliseconds(90));
        rig.Detail.MotionOf(incoming).Opacity.ShouldBeInRange(0.4, 0.99);
        rig.Detail.MotionOf(old).Opacity.ShouldBeInRange(0.01, 0.6);
        rig.Advance(clock, TimeSpan.FromMilliseconds(150));
        rig.Detail.Leaving.ShouldBeEmpty();
        rig.Detail.MotionOf(incoming).Opacity.ShouldBe(1, 0.01);
        rig.Detail.MotionOf(incoming).Y.ShouldBe(0, 0.05);
    }

    [AvaloniaFact]
    public void An_Interrupted_Profile_Fades_Out_From_Where_It_Is()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Click(rig.Row("theo"));
        var second = rig.Profile;
        rig.Advance(clock, TimeSpan.FromMilliseconds(60));
        var reached = rig.Detail.MotionOf(second).Opacity;
        rig.Click(rig.Row("fiona"));
        // Three views can overlap for a moment.
        rig.Detail.Children.Count.ShouldBe(3);
        rig.Detail.MotionOf(second).Opacity.ShouldBe(reached, 0.05);
        rig.Advance(clock, TimeSpan.FromMilliseconds(400));
        rig.Detail.Children.Count.ShouldBe(1);
        rig.Profile.Person.Key.ShouldBe("fiona");
    }

    [AvaloniaFact]
    public void Reduced_Motion_Swaps_Everything_At_Once()
    {
        using var rig = new Rig(ArcMotionLevel.Reduced);
        rig.Click(rig.Row("priya"));
        rig.Detail.Children.Count.ShouldBe(1);
        rig.List.Indicator!.CurrentRect.ShouldBe(rig.Row("priya").Bounds);
        rig.Click(rig.Filter("Operations"));
        rig.List.LeavingRows.ShouldBeEmpty();
        rig.List.Children.OfType<TeamDirectoryRow>().Count().ShouldBe(2);
        rig.List.Motion(rig.Row("camila")).Opacity.Value.ShouldBe(1);
        rig.List.Motion(rig.Row("camila")).Offset.Value.ShouldBe(default);
        rig.Filters.Indicator!.CurrentRect.ShouldBe(rig.Filter("Operations").Bounds);
        rig.Detail.Children.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Resizes_Snap_The_Highlights()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(ArcMotionLevel.Full, width: 900);
        rig.Click(rig.Row("theo"));
        rig.Advance(clock, TimeSpan.FromMilliseconds(1500));
        rig.Host.Width = 380;
        rig.Pump();
        rig.List.Indicator!.CurrentRect.ShouldBe(rig.Row("theo").Bounds);
        rig.Filters.Indicator!.CurrentRect.ShouldBe(rig.Filter(null).Bounds);
    }

    #endregion

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }
}
