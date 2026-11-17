using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// TeamSwitcher: the sample workspaces, the trigger's face, the menu's keyboard model (open, move, filter, Enter, Escape,
/// Tab), Cmd/Ctrl+number shortcuts, switching through the app's delegate (pending, done, failed, superseded), create and
/// join (simulated and delegated), the fixed panel height, labels, the tile and the plain logic behind them.
/// </summary>
public class TeamSwitcherTests
{
    /// <summary>The tests' own invented workspaces (the sample the library used to ship; the Gallery now passes its own).</summary>
    private static List<TeamSwitcherWorkspace> SampleWorkspaces() =>
    [
        new("northwind", "Northwind", "Enterprise", 214, "Admin"),
        new("northwind-labs", "Northwind Labs", "Pro", 12, "Owner"),
        new("contoso", "Contoso", "Business", 86, "Member"),
        new("fabrikam", "Fabrikam", "Pro", 31, "Admin"),
        new("wide-world", "Wide World Importers", "Free", 4, "Owner"),
        new("proseware", "Proseware", "Enterprise", 1280, "Guest"),
    ];

    private static List<TeamSwitcherSimulationInvite> SampleInvites() =>
    [
        new() { Code = "ADATUM-4821", Workspace = new("adatum", "Adatum", "Pro", 18, "Member") },
        new() { Code = "TAILSPIN-0917", Workspace = new("tailspin", "Tailspin Toys", "Business", 57, "Member") },
    ];

    private static TeamSwitcherSimulation Sample() => new()
    {
        Workspaces = SampleWorkspaces(),
        Invites = SampleInvites(),
        CreatedPlan = "Free",
        CreatedRole = "Owner",
    };

    private static (TeamSwitcher Switcher, Window Window) Open(Action<TeamSwitcher>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var switcher = new TeamSwitcher
        {
            SimulatedDelay = TimeSpan.Zero,
            Simulation = Sample(),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        setup?.Invoke(switcher);
        var window = TestHelpers.Show(switcher, level);
        window.Width = 420;
        window.Height = 800;
        Pump();
        return (switcher, window);
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

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static TeamSwitcherMenu OpenMenu(TeamSwitcher switcher, Window window)
    {
        switcher.Trigger!.Focus(NavigationMethod.Tab);
        Pump();
        Press(window, PhysicalKey.Enter);
        switcher.IsOpen.ShouldBeTrue();
        return switcher.Menu!;
    }

    private static string? HighlightedName(TeamSwitcherMenu menu) => menu.Highlighted switch
    {
        TeamSwitcherRow row => row.Workspace!.Name,
        TeamSwitcherActionRow action => action.Text,
        _ => null
    };

    [AvaloniaFact]
    public void Shows_The_Simulations_Workspaces_With_No_Data()
    {
        var (switcher, window) = Open();
        try
        {
            switcher.UsesSimulation.ShouldBeTrue();
            switcher.EffectiveWorkspaces.Count.ShouldBe(6);
            switcher.CurrentWorkspace!.Name.ShouldBe("Northwind");
            var face = switcher.Face!;
            face.Workspace!.Name.ShouldBe("Northwind");
            face.Line.ShouldBe(TeamSwitcherFaceLine.Plan);
            face.LineText.ShouldBe("Enterprise · 214 members");
            AutomationProperties.GetName(switcher.Trigger!).ShouldBe("Northwind, Enterprise plan");
            switcher.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fills_Narrow_Containers_And_Caps_At_360()
    {
        var (switcher, window) = Open();
        try
        {
            switcher.Bounds.Width.ShouldBe(360);
            window.Width = 300;
            Pump();
            switcher.Bounds.Width.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Menu_Matches_The_Trigger_But_Never_Gets_Narrower_Than_288()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            var surface = TestHelpers.Descendant<Border>(menu, TeamSwitcherMenu.SurfacePart);
            surface.Bounds.Width.ShouldBe(360);
            switcher.Close();
            switcher.Width = 228;
            switcher.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            Pump();
            OpenMenu(switcher, window);
            Pump();
            switcher.Bounds.Width.ShouldBe(228);
            surface.Bounds.Width.ShouldBe(288);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_On_The_Trigger_Opens_With_Focus_In_Search_And_The_Highlight_On_The_Active_Row()
    {
        var (switcher, window) = Open(s => s.Value = "contoso");
        try
        {
            var menu = OpenMenu(switcher, window);
            Pump();
            menu.Search!.IsFocused.ShouldBeTrue();
            switcher.Trigger!.IsExpanded.ShouldBeTrue();
            HighlightedName(menu).ShouldBe("Contoso");
            menu.Items.Count.ShouldBe(8);
            menu.Rows.Single(r => r.IsCurrent).Workspace!.Id.ShouldBe("contoso");
            AutomationProperties.GetHelpText(menu.Search).ShouldStartWith("Contoso, Business · 86 members, Member");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ArrowDown_On_The_Trigger_Opens_The_Menu()
    {
        var (switcher, window) = Open();
        try
        {
            switcher.Trigger!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            switcher.IsOpen.ShouldBeTrue();
            switcher.Menu!.Search!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Wrap_Through_Rows_And_Actions_And_Home_End_Jump()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            HighlightedName(menu).ShouldBe("Northwind");
            Press(window, PhysicalKey.ArrowUp);
            HighlightedName(menu).ShouldBe("Join with invite");
            Press(window, PhysicalKey.ArrowUp);
            HighlightedName(menu).ShouldBe("Create workspace");
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            HighlightedName(menu).ShouldBe("Northwind");
            Press(window, PhysicalKey.End);
            HighlightedName(menu).ShouldBe("Join with invite");
            Press(window, PhysicalKey.Home);
            HighlightedName(menu).ShouldBe("Northwind");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Filters_By_Name_Plan_Or_Role()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.Search!.Text = "pro";
            Pump();
            menu.Rows.Where(r => r.IsVisible).Select(r => r.Workspace!.Name)
                .ShouldBe(["Northwind Labs", "Fabrikam", "Proseware"]);
            HighlightedName(menu).ShouldBe("Northwind Labs");
            menu.Search.Text = "guest";
            Pump();
            menu.Rows.Where(r => r.IsVisible).Select(r => r.Workspace!.Name).ShouldBe(["Proseware"]);
            // Shortcuts keep the place in the full list.
            menu.Rows.Single(r => r.IsVisible).Shortcut.ShouldEndWith("6");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Search_Starts_On_Its_First_Match_Even_From_A_Row_Scrolled_Out_Of_View()
    {
        var (switcher, window) = Open(s =>
        {
            for (var i = 1; i <= 12; i++)
            {
                s.Workspaces.Add(new TeamSwitcherWorkspace("w" + i, "Team " + i, i % 2 == 0 ? "Pro" : "Free", i, "Member"));
            }
        });
        try
        {
            var menu = OpenMenu(switcher, window);
            Press(window, PhysicalKey.ArrowUp);
            Press(window, PhysicalKey.ArrowUp);
            Press(window, PhysicalKey.ArrowUp);
            HighlightedName(menu).ShouldBe("Team 12");
            menu.Search!.Text = "free";
            Pump();
            HighlightedName(menu).ShouldBe("Team 1");
            menu.Highlight!.CurrentOpacity.ShouldBe(1);
            menu.Search.Text = "zzz";
            Pump();
            HighlightedName(menu).ShouldBe("Create workspace");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Match_Then_Enter_Opens_Create_With_The_Name_Filled_In()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.Search!.Text = "Zephyr Studio";
            Pump();
            menu.Empty!.IsVisible.ShouldBeTrue();
            HighlightedName(menu).ShouldBe("Create workspace");
            Press(window, PhysicalKey.Enter);
            menu.View.ShouldBe(TeamSwitcherView.Create);
            menu.NameInput!.Text.ShouldBe("Zephyr Studio");
            menu.NameInput.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Goes_Back_Then_Clears_Then_Closes_And_Returns_Focus()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.Search!.Text = "con";
            Pump();
            menu.OpenForm(TeamSwitcherView.Join);
            Pump();
            menu.View.ShouldBe(TeamSwitcherView.Join);
            Press(window, PhysicalKey.Escape);
            menu.View.ShouldBe(TeamSwitcherView.List);
            menu.Search.IsFocused.ShouldBeTrue();
            menu.Search.Text.ShouldBe("con");
            Press(window, PhysicalKey.Escape);
            menu.Search.Text.ShouldBe(string.Empty);
            switcher.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            switcher.IsOpen.ShouldBeFalse();
            switcher.Trigger!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Out_Of_The_Search_Closes_The_Menu()
    {
        var (switcher, window) = Open();
        try
        {
            OpenMenu(switcher, window);
            Press(window, PhysicalKey.Tab);
            switcher.IsOpen.ShouldBeFalse();
            switcher.Trigger!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_Height_Never_Changes_Between_Filters_And_Views()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            var listView = menu.ListView!;
            var height = listView.Bounds.Height;
            height.ShouldBeGreaterThan(6 * 48);
            menu.Search!.Text = "guest";
            Pump();
            listView.Bounds.Height.ShouldBe(height);
            menu.Search.Text = "nothing like this";
            Pump();
            listView.Bounds.Height.ShouldBe(height);
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.CreateView!.Bounds.Height.ShouldBe(height - menu.CreateView.Margin.Top - menu.CreateView.Margin.Bottom);
            listView.Bounds.Height.ShouldBe(height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Create_Form_Fits_Inside_The_Panel()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            var view = menu.CreateView!;
            view.IsVisible.ShouldBeTrue();
            var input = menu.NameInput!;
            var submit = menu.CreateSubmit!;
            var inputBottom = input.TranslatePoint(new Point(0, input.Bounds.Height), view)!.Value.Y;
            var submitTop = submit.TranslatePoint(default, view)!.Value.Y;
            var submitBottom = submit.TranslatePoint(new Point(0, submit.Bounds.Height), view)!.Value.Y;
            inputBottom.ShouldBeLessThan(submitTop);
            submitBottom.ShouldBeLessThanOrEqualTo(view.Bounds.Height + 0.5);
            input.Label.ShouldBe("Workspace name");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Create_Form_With_An_Error_Fits_With_A_Single_Workspace()
    {
        var (switcher, window) = Open(s => s.Workspaces.Add(new TeamSwitcherWorkspace("solo", "Solo", "Free", 1, "Owner")));
        try
        {
            var menu = OpenMenu(switcher, window);
            var height = menu.ListView!.Bounds.Height;
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.NameInput!.Text = "solo";
            menu.Submit();
            Pump();
            menu.FormError.ShouldNotBeNull();
            var view = menu.CreateView!;
            var line = TestHelpers.Descendant<Grid>(view, TeamSwitcherMenu.CreateErrorPart);
            var submit = menu.CreateSubmit!;
            var lineBottom = line.TranslatePoint(new Point(0, line.Bounds.Height), view)!.Value.Y;
            var submitTop = submit.TranslatePoint(default, view)!.Value.Y;
            var submitBottom = submit.TranslatePoint(new Point(0, submit.Bounds.Height), view)!.Value.Y;
            lineBottom.ShouldBeLessThanOrEqualTo(submitTop);
            submitBottom.ShouldBeLessThanOrEqualTo(view.Bounds.Height + 0.5);
            menu.ListView.Bounds.Height.ShouldBe(height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Highlight_Sits_On_The_Highlighted_Row()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            Press(window, PhysicalKey.ArrowDown);
            Pump();
            var row = (TeamSwitcherRow)menu.Highlighted!;
            var origin = row.TranslatePoint(default, menu.ListView!)!.Value;
            var rect = menu.Highlight!.CurrentRect;
            rect.Y.ShouldBe(origin.Y, 0.5);
            rect.Height.ShouldBe(48, 0.5);
            menu.Highlight.CurrentOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Switches_Through_The_Delegate_With_A_Pending_State()
    {
        var gate = new TaskCompletionSource();
        TeamSwitcherWorkspace? loaded = null;
        var (switcher, window) = Open(s => s.SwitchHandler = (w, _) =>
        {
            loaded = w;
            return gate.Task;
        });
        try
        {
            TeamSwitcherValueChangedEventArgs? changed = null;
            switcher.ValueChanged += (_, e) => changed = e;
            var menu = OpenMenu(switcher, window);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            switcher.IsOpen.ShouldBeFalse();
            switcher.Trigger!.IsFocused.ShouldBeTrue();
            loaded!.Id.ShouldBe("contoso");
            switcher.IsSwitching.ShouldBeTrue();
            switcher.Classes.Contains(":switching").ShouldBeTrue();
            switcher.Face!.Workspace!.Name.ShouldBe("Contoso");
            switcher.Face.Line.ShouldBe(TeamSwitcherFaceLine.Switching);
            switcher.Value.ShouldBeNull();
            switcher.Announcement.ShouldBe("Switching to Contoso");
            AutomationProperties.GetItemStatus(switcher.Trigger).ShouldBe("Switching…");
            changed.ShouldBeNull();

            gate.SetResult();
            Pump();
            switcher.IsSwitching.ShouldBeFalse();
            switcher.Value.ShouldBe("contoso");
            switcher.Face.Line.ShouldBe(TeamSwitcherFaceLine.Plan);
            switcher.Face.LineText.ShouldBe("Business · 86 members");
            changed!.Id.ShouldBe("contoso");
            changed.Previous!.Id.ShouldBe("northwind");
            switcher.Announcement.ShouldBe("Switched to Contoso");
            menu.Rows.Single(r => r.IsCurrent).Workspace!.Id.ShouldBe("contoso");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Switch_Reverts_And_Shows_The_Message()
    {
        var (switcher, window) = Open(s => s.SwitchHandler = (_, _) => Task.FromException(new InvalidOperationException("No access to Fabrikam.")));
        try
        {
            TeamSwitcherSwitchFailedEventArgs? failed = null;
            switcher.SwitchFailed += (_, e) => failed = e;
            switcher.Choose(switcher.EffectiveWorkspaces[3]);
            Pump();
            switcher.Value.ShouldBeNull();
            switcher.CurrentWorkspace!.Id.ShouldBe("northwind");
            switcher.Face!.Workspace!.Id.ShouldBe("northwind");
            switcher.Face.Line.ShouldBe(TeamSwitcherFaceLine.Error);
            switcher.ErrorText.ShouldBe("No access to Fabrikam.");
            failed!.Workspace.Id.ShouldBe("fabrikam");
            switcher.Announcement.ShouldBe("Could not switch to Fabrikam. No access to Fabrikam.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Newer_Switch_Cancels_The_Older_One()
    {
        var tokens = new List<CancellationToken>();
        var gates = new List<TaskCompletionSource>();
        var (switcher, window) = Open(s => s.SwitchHandler = (_, token) =>
        {
            tokens.Add(token);
            var gate = new TaskCompletionSource();
            gates.Add(gate);
            return gate.Task;
        });
        try
        {
            var list = switcher.EffectiveWorkspaces;
            switcher.Choose(list[1]);
            switcher.Choose(list[2]);
            Pump();
            tokens[0].IsCancellationRequested.ShouldBeTrue();
            tokens[1].IsCancellationRequested.ShouldBeFalse();
            gates[0].SetResult();
            Pump();
            switcher.Value.ShouldBeNull();
            switcher.PendingWorkspace!.Id.ShouldBe("contoso");
            gates[1].SetResult();
            Pump();
            switcher.Value.ShouldBe("contoso");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Value_Abandons_A_Load_In_Flight()
    {
        CancellationToken token = default;
        var (switcher, window) = Open(s => s.SwitchHandler = (_, t) =>
        {
            token = t;
            return new TaskCompletionSource().Task;
        });
        try
        {
            switcher.Choose(switcher.EffectiveWorkspaces[1]);
            Pump();
            switcher.Value = "proseware";
            Pump();
            token.IsCancellationRequested.ShouldBeTrue();
            switcher.IsSwitching.ShouldBeFalse();
            switcher.Face!.Workspace!.Id.ShouldBe("proseware");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Number_Switches_While_Focus_Is_Inside()
    {
        var (switcher, window) = Open();
        try
        {
            switcher.Trigger!.Focus(NavigationMethod.Tab);
            Pump();
            Press(window, PhysicalKey.Digit3, RawInputModifiers.Control);
            switcher.Value.ShouldBe("contoso");
            switcher.Announcement.ShouldBe("Switched to Contoso. Simulated, nothing was sent.");
            Press(window, PhysicalKey.Digit9, RawInputModifiers.Meta);
            switcher.Value.ShouldBe("contoso");
            Press(window, PhysicalKey.Digit6, RawInputModifiers.Meta);
            switcher.Value.ShouldBe("proseware");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Global_Shortcuts_Work_Anywhere_Only_When_On()
    {
        var switcher = new TeamSwitcher { SimulatedDelay = TimeSpan.Zero, Simulation = Sample() };
        var other = new Avalonia.Controls.Button { Content = "Elsewhere" };
        var window = TestHelpers.Show(new StackPanel { Children = { switcher, other } });
        try
        {
            other.Focus(NavigationMethod.Tab);
            Pump();
            Press(window, PhysicalKey.Digit2, RawInputModifiers.Control);
            switcher.Value.ShouldBeNull();
            switcher.UsesGlobalShortcuts = true;
            Press(window, PhysicalKey.Digit2, RawInputModifiers.Control);
            switcher.Value.ShouldBe("northwind-labs");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulated_Create_Adds_The_Workspace_And_Switches_To_It()
    {
        var (switcher, window) = Open();
        try
        {
            TeamSwitcherWorkspaceAddedEventArgs? added = null;
            switcher.WorkspaceAdded += (_, e) => added = e;
            var menu = OpenMenu(switcher, window);
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.NameInput!.Text = "Blue Yonder";
            Press(window, PhysicalKey.Enter);
            Pump();
            added!.Kind.ShouldBe(TeamSwitcherAddKind.Created);
            added.IsSimulated.ShouldBeTrue();
            added.Workspace.Id.ShouldBe("blue-yonder");
            added.Workspace.Role.ShouldBe("Owner");
            switcher.EffectiveWorkspaces.Count.ShouldBe(7);
            switcher.Value.ShouldBe("blue-yonder");
            switcher.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulated_Create_Refuses_Empty_And_Taken_Names()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.NameInput!.Text = "  ";
            menu.Submit();
            Pump();
            menu.FormError.ShouldBe("Enter a name for the workspace.");
            menu.NameInput.IsInvalid.ShouldBeTrue();
            menu.NameInput.Text = "contoso";
            menu.Submit();
            Pump();
            menu.FormError.ShouldBe("You already have a workspace called “contoso”.");
            // The message pairs an alert icon with the text (not color alone).
            var line = TestHelpers.Descendant<Grid>(menu.CreateView!, TeamSwitcherMenu.CreateErrorPart);
            line.IsVisible.ShouldBeTrue();
            line.Children.OfType<ArcGlyph>().Single().Data.ShouldBe(ArcIcons.CircleAlert);
            AutomationProperties.GetHelpText(menu.NameInput).ShouldEndWith("You already have a workspace called “contoso”.");
            switcher.IsOpen.ShouldBeTrue();
            // Going back and opening the form again starts clean.
            menu.Back();
            Pump();
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.FormError.ShouldBeNull();
            menu.NameInput.IsInvalid.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Create_Delegate_Errors_Show_Under_The_Field_And_Back_Cancels()
    {
        var calls = new List<CancellationToken>();
        var gate = new TaskCompletionSource<TeamSwitcherWorkspace?>();
        var (switcher, window) = Open(s => s.CreateHandler = (name, token) =>
        {
            calls.Add(token);
            return name == "fail" ? Task.FromException<TeamSwitcherWorkspace?>(new InvalidOperationException("Workspace limit reached.")) : gate.Task;
        });
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.NameInput!.Text = "fail";
            menu.Submit();
            Pump();
            menu.FormError.ShouldBe("Workspace limit reached.");
            menu.NameInput.Text = "slow";
            menu.Submit();
            Pump();
            menu.IsSubmitting.ShouldBeTrue();
            menu.CreateSubmit!.IsLoading.ShouldBeTrue();
            menu.Back();
            Pump();
            calls[1].IsCancellationRequested.ShouldBeTrue();
            menu.View.ShouldBe(TeamSwitcherView.List);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Join_Accepts_A_Sample_Invite_Link_And_Refuses_Others()
    {
        var (switcher, window) = Open();
        try
        {
            var menu = OpenMenu(switcher, window);
            menu.OpenForm(TeamSwitcherView.Join);
            Pump();
            menu.InviteInput!.Description.ShouldBe("Sample invites: ADATUM-4821, TAILSPIN-0917");
            menu.InviteInput.Text = "NOPE-1";
            menu.Submit();
            Pump();
            menu.FormError.ShouldBe("This invite is invalid or has expired.");
            menu.InviteInput.Text = "https://example.com/invite/tailspin-0917";
            menu.Submit();
            Pump();
            switcher.Value.ShouldBe("tailspin");
            switcher.EffectiveWorkspaces.Last().Name.ShouldBe("Tailspin Toys");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Workspaces_And_No_Simulation_The_Trigger_Says_There_Is_No_Workspace()
    {
        var (switcher, window) = Open(s => s.Simulation = null);
        try
        {
            switcher.UsesSimulation.ShouldBeFalse();
            switcher.EffectiveWorkspaces.ShouldBeEmpty();
            switcher.CurrentWorkspace.ShouldBeNull();
            switcher.Face!.Workspace.ShouldBeNull();
            AutomationProperties.GetName(switcher.Trigger!).ShouldBe("No workspace");
            Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(switcher.Face).OfType<TextBlock>().Any(t => t.Text == "No workspace").ShouldBeTrue();
            var menu = OpenMenu(switcher, window);
            menu.Empty!.IsVisible.ShouldBeTrue();
            Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(menu.Empty).OfType<TextBlock>().Any(t => t.Text == "No workspace").ShouldBeTrue();
            menu.OpenForm(TeamSwitcherView.Join);
            Pump();
            menu.InviteInput!.Description.ShouldBe("Ask a workspace admin for an invite.");
            menu.InviteInput.Text = "ADATUM-4821";
            menu.Submit();
            Pump();
            menu.FormError.ShouldBe("This invite is invalid or has expired.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Workspaces_Win_Over_The_Simulation_And_Clearing_It_Empties_The_Switcher()
    {
        var (switcher, window) = Open(s => s.Workspaces.Add(new TeamSwitcherWorkspace("own", "Own Team", "Pro", 3, "Owner")));
        try
        {
            switcher.UsesSimulation.ShouldBeFalse();
            switcher.EffectiveWorkspaces.Select(w => w.Id).ShouldBe(["own"]);
            switcher.SimulatedInvites.ShouldBeEmpty();
            switcher.Workspaces.Clear();
            Pump();
            switcher.UsesSimulation.ShouldBeTrue();
            switcher.EffectiveWorkspaces.Count.ShouldBe(6);
            switcher.CurrentWorkspace!.Name.ShouldBe("Northwind");

            switcher.Simulation = null;
            Pump();
            switcher.EffectiveWorkspaces.ShouldBeEmpty();
            switcher.CurrentWorkspace.ShouldBeNull();
            AutomationProperties.GetName(switcher.Trigger!).ShouldBe("No workspace");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Created_Workspaces_Are_Deduplicated_By_Id()
    {
        var (switcher, window) = Open(s =>
        {
            s.Workspaces.Add(new TeamSwitcherWorkspace("a", "Alpha", "Pro", 3, "Owner"));
            s.Workspaces.Add(new TeamSwitcherWorkspace("b", "Beta"));
            s.CreateHandler = (name, _) => Task.FromResult<TeamSwitcherWorkspace?>(new TeamSwitcherWorkspace("b", name));
        });
        try
        {
            switcher.UsesSimulation.ShouldBeFalse();
            var menu = OpenMenu(switcher, window);
            menu.OpenForm(TeamSwitcherView.Create);
            Pump();
            menu.NameInput!.Text = "Beta again";
            menu.Submit();
            Pump();
            switcher.EffectiveWorkspaces.Count.ShouldBe(2);
            switcher.Value.ShouldBe("b");
            switcher.Workspaces.Add(new TeamSwitcherWorkspace("c", "Gamma"));
            Pump();
            switcher.EffectiveWorkspaces.Select(w => w.Id).ShouldBe(["a", "b", "c"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Starts_Open_Without_Taking_Focus()
    {
        var (switcher, window) = Open(s => s.IsOpen = true);
        try
        {
            Pump();
            switcher.Popup!.IsOpen.ShouldBeTrue();
            switcher.Menu!.Search!.IsFocused.ShouldBeFalse();
            switcher.IsOpen = false;
            Pump();
            switcher.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OpenChanged_Reports_Both_Ways()
    {
        var (switcher, window) = Open();
        try
        {
            var seen = new List<bool>();
            switcher.OpenChanged += (_, e) => seen.Add(e.IsOpen);
            switcher.Open();
            Pump();
            switcher.Close();
            Pump();
            seen.ShouldBe([true, false]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Ignores_Shortcuts_And_Open()
    {
        var (switcher, window) = Open(s => s.IsEnabled = false);
        try
        {
            switcher.Open();
            Pump();
            switcher.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (switcher, window) = Open(s => s.Labels = new TeamSwitcherLabels
        {
            Search = "Arbeitsbereich suchen",
            CreateWorkspace = "Arbeitsbereich erstellen",
            PlanLine = (plan, members) => plan + " / " + members,
        });
        try
        {
            switcher.Face!.LineText.ShouldBe("Enterprise / 214");
            var menu = OpenMenu(switcher, window);
            menu.Search!.Placeholder.ShouldBe("Arbeitsbereich suchen");
            menu.CreateRow!.Text.ShouldBe("Arbeitsbereich erstellen");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Popup_Only_Fades()
    {
        var (switcher, window) = Open(level: ArcMotionLevel.Reduced);
        try
        {
            switcher.Open();
            Pump();
            switcher.Popup!.OpenMotion!.FadeOnly.ShouldBeTrue();
            switcher.Popup.CloseMotion!.FadeOnly.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Popup_Scales_From_98_Percent()
    {
        var (switcher, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            switcher.Open();
            Pump();
            var enter = switcher.Popup!.OpenMotion!;
            enter.FadeOnly.ShouldBeFalse();
            enter.Scale.ShouldBe(0.98);
            enter.Offset.ShouldBe(default);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tile_Draws_A_Letter_Or_The_Mark()
    {
        var workspace = new TeamSwitcherWorkspace("x", "  zeta works");
        var tile = new TeamSwitcherTile { Workspace = workspace, TileSize = 40 };
        var window = TestHelpers.Show(tile);
        try
        {
            tile.Letter.ShouldBe("Z");
            tile.TintIndex.ShouldBe(TeamSwitcherLogic.TintIndex("  zeta works"));
            tile.DesiredSize.ShouldBe(new Size(40, 40));
            AutomationProperties.GetName(tile).ShouldBe("  zeta works");
            workspace.Logo = Geometry.Parse("M0,0 L24,0 L24,24 Z");
            tile.Letter.ShouldBeNull();
            workspace.Logo = null;
            tile.Letter.ShouldBe("Z");
            workspace.LogoImage = new DrawingImage(new GeometryDrawing
            {
                Brush = Brushes.Teal,
                Geometry = Geometry.Parse("M0,0 L48,0 L48,24 L0,24 Z")
            });
            tile.Letter.ShouldBeNull();
            // On a brand color the ink is whichever of white and near-black reads better, in either theme.
            ((ISolidColorBrush)tile.InkOn(new SolidColorBrush(Color.Parse("#4F46E5")))).Color.ShouldBe(Colors.White);
            ((ISolidColorBrush)tile.InkOn(new SolidColorBrush(Color.Parse("#FDE047")))).Color.ShouldBe(Color.FromRgb(0x18, 0x18, 0x1B));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shortcut_Number_Needs_Command_Or_Control_Alone()
    {
        TeamSwitcher.ShortcutNumber(new KeyEventArgs { Key = Key.D4, KeyModifiers = KeyModifiers.Control }).ShouldBe(4);
        TeamSwitcher.ShortcutNumber(new KeyEventArgs { Key = Key.NumPad2, KeyModifiers = KeyModifiers.Meta }).ShouldBe(2);
        TeamSwitcher.ShortcutNumber(new KeyEventArgs { Key = Key.D4, KeyModifiers = KeyModifiers.None }).ShouldBeNull();
        TeamSwitcher.ShortcutNumber(new KeyEventArgs { Key = Key.D4, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift }).ShouldBeNull();
        TeamSwitcher.ShortcutNumber(new KeyEventArgs { Key = Key.D0, KeyModifiers = KeyModifiers.Control }).ShouldBeNull();
    }

    [Fact]
    public void Filter_Monogram_Tint_Slug_And_Invite_Logic()
    {
        var list = SampleWorkspaces();
        var invites = SampleInvites();
        TeamSwitcherLogic.Filter(list, "").Count.ShouldBe(6);
        TeamSwitcherLogic.Filter(list, "  ADMIN ").ShouldBe([0, 3]);
        TeamSwitcherLogic.Filter(list, "enterprise").ShouldBe([0, 5]);
        TeamSwitcherLogic.Monogram("acme").ShouldBe("A");
        TeamSwitcherLogic.Monogram("  42 Labs").ShouldBe("4");
        TeamSwitcherLogic.Monogram("").ShouldBe("?");
        TeamSwitcherLogic.TintIndex("Northwind").ShouldBe(TeamSwitcherLogic.TintIndex("NORTHWIND"));
        TeamSwitcherLogic.TintIndex("Northwind").ShouldBeInRange(0, 4);
        TeamSwitcherLogic.Slug("Blue Yonder, Inc.", []).ShouldBe("blue-yonder-inc");
        TeamSwitcherLogic.Slug("Contoso", ["contoso", "contoso-2"]).ShouldBe("contoso-3");
        TeamSwitcherLogic.Slug("!!!", []).ShouldBe("workspace");
        TeamSwitcherLogic.InviteCode(" https://example.com/join/adatum-4821/?ref=mail ").ShouldBe("ADATUM-4821");
        TeamSwitcherLogic.InviteCode("tailspin-0917").ShouldBe("TAILSPIN-0917");
        var labels = TeamSwitcherLabels.Default;
        TeamSwitcherLogic.Join("ADATUM-4821", list, labels, invites).Name.ShouldBe("Adatum");
        Should.Throw<InvalidOperationException>(() => TeamSwitcherLogic.Join("", list, labels, invites)).Message.ShouldBe("Paste an invite link or code.");
        var joined = new List<TeamSwitcherWorkspace>(list) { new("adatum", "Adatum") };
        Should.Throw<InvalidOperationException>(() => TeamSwitcherLogic.Join("adatum-4821", joined, labels, invites)).Message
            .ShouldBe("You are already a member of Adatum.");
        // Without a simulation's invites, every code is refused; a created workspace has no plan or role.
        Should.Throw<InvalidOperationException>(() => TeamSwitcherLogic.Join("ADATUM-4821", list, labels, [])).Message
            .ShouldBe("This invite is invalid or has expired.");
        var created = TeamSwitcherLogic.Create("Acme", list, labels);
        created.Plan.ShouldBeNull();
        created.Role.ShouldBeNull();
        created.Members.ShouldBe(1);
        TeamSwitcherLogic.InviteCodes(invites).ShouldBe("ADATUM-4821, TAILSPIN-0917");
    }

    [Fact]
    public void Default_Labels_Format_Plan_Lines_And_Shortcuts()
    {
        var labels = TeamSwitcherLabels.Default;
        labels.PlanLine("Pro", 1).ShouldBe("Pro · 1 member");
        labels.PlanLine("Enterprise", 1280).ShouldBe("Enterprise · 1,280 members");
        labels.PlanLine(null, 3).ShouldBe("3 members");
        labels.PlanLine("Free", null).ShouldBe("Free");
        labels.PlanLine(null, null).ShouldBe(string.Empty);
        labels.Shortcut(1, true).ShouldBe("⌘1");
        labels.Shortcut(9, false).ShouldBe("Ctrl 9");
        labels.Trigger("Acme", null).ShouldBe("Acme");
        labels.WithSimulated("Created Acme", true).ShouldBe("Created Acme. Simulated, nothing was sent.");
    }
}
