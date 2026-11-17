using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.SocialProof;

public class TeamShowcaseTests
{
    private static Window Show(Control control, double width = 1100)
    {
        var window = TestHelpers.Show(control);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Show_The_Sample_Team_With_A_Department_Filter()
    {
        var team = new TeamShowcase();
        var window = Show(team);
        try
        {
            team.Members.Count.ShouldBe(0);
            team.EffectiveMembers.Count.ShouldBe(10);
            team.Cards.Count.ShouldBe(10);
            team.Departments.ShouldBe(["Design", "Engineering", "Product", "Operations"]);
            team.Filter!.ItemCount.ShouldBe(5);
            team.Classes.Contains(":no-filter").ShouldBeFalse();
            team.Grid!.Columns.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Department_Hides_The_Others_And_Announces_The_Count()
    {
        var team = new TeamShowcase();
        var window = Show(team);
        try
        {
            team.Filter!.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            team.SelectedDepartment.ShouldBe("Design");
            team.VisibleMembers.Select(m => m.Name).ShouldBe(["Mara Okonjo", "Jonah Whitfield", "Lena Hoffmann"]);
            team.Cards.Count(c => c.IsVisible).ShouldBe(3);
            team.Announcer!.Announcement.ShouldBe("3 people in Design");

            team.SelectedDepartment = null;
            Dispatcher.UIThread.RunJobs();
            team.Filter.SelectedIndex.ShouldBe(0);
            team.Cards.Count(c => c.IsVisible).ShouldBe(10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Members_Replace_The_Samples_And_One_Department_Hides_The_Filter()
    {
        var team = new TeamShowcase();
        team.Members.Add(new TeamShowcaseMember { Name = "A Person", Role = "Role", Department = "Only" });
        team.Members.Add(new TeamShowcaseMember { Name = "B Person", Role = "Role", Department = "Only" });
        var window = Show(team);
        try
        {
            team.Cards.Select(c => c.MemberName).ShouldBe(["A Person", "B Person"]);
            team.Classes.Contains(":no-filter").ShouldBeTrue();
            team.SelectedDepartment = "Nobody";
            Dispatcher.UIThread.RunJobs();
            team.Classes.Contains(":empty").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Card_Pins_Its_Bio_And_Raises_MemberInvoked()
    {
        var team = new TeamShowcase();
        TeamShowcaseMember? invoked = null;
        team.MemberInvoked += (_, e) => invoked = e.Member;
        var window = Show(team);
        try
        {
            var card = team.Cards[2];
            card.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            card.IsRevealed.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBe(card.Member);
            card.IsPinned.ShouldBeTrue();

            // Pinning another card unpins the first.
            team.Cards[3].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsPinned.ShouldBeFalse();
            team.Cards[3].IsPinned.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            team.Cards[3].IsPinned.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_By_Column_And_Row()
    {
        var team = new TeamShowcase();
        var window = Show(team);
        try
        {
            team.FocusFirstMember();
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            team.Cards[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            team.Cards[5].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            team.Cards[9].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            team.Cards[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Columns_Follow_The_Width()
    {
        TeamShowcaseGrid.ColumnsFor(1000, 200, 4, 20).ShouldBe(4);
        TeamShowcaseGrid.ColumnsFor(640, 200, 4, 20).ShouldBe(3);
        TeamShowcaseGrid.ColumnsFor(400, 200, 4, 20).ShouldBe(1);
        TeamShowcaseGrid.ColumnsFor(150, 200, 4, 20).ShouldBe(1);
        TeamShowcaseGrid.ColumnsFor(2000, 200, 3, 20).ShouldBe(3);
    }

    [AvaloniaFact]
    public void Portraits_Are_Stable_For_A_Name()
    {
        TeamShowcasePortrait.SeedOf("Mara Okonjo").ShouldBe(TeamShowcasePortrait.SeedOf("Mara Okonjo"));
        TeamShowcasePortrait.SeedOf("Mara Okonjo").ShouldNotBe(TeamShowcasePortrait.SeedOf("Teo Lindgren"));
        var shapes = Enumerable.Range(0, 200).Select(i => new TeamShowcasePortrait { Seed = i * 7 }.HairShape).Distinct().Count();
        shapes.ShouldBe(5);
    }
}
