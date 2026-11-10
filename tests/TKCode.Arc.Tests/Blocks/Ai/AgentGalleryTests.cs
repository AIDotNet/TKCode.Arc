using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A sample gallery in a window with its waits in the test's hands; records the events.</summary>
internal sealed class AgentGalleryRig : IDisposable
{
    public AgentGalleryRig(double width = 1000, Action<AgentGallery>? configure = null)
    {
        Gallery = new AgentGallery { Delay = Delays.Delay };
        Gallery.AgentOpened += (_, e) => Opened.Add(e.Agent.Id);
        Gallery.AgentInstalled += (_, e) => Installed.Add(e.Agent.Id);
        Gallery.AgentUninstalled += (_, e) => Uninstalled.Add(e.Agent.Id);
        Gallery.InstallFailed += (_, e) => Failed.Add((e.Agent.Id, e.Message));
        Gallery.PromptChosen += (_, e) => Prompts.Add(e.Prompt);
        configure?.Invoke(Gallery);
        Window = TestHelpers.Show(Gallery);
        Window.Width = width;
        Window.Height = 1400;
        Pump();
    }

    public AgentGallery Gallery { get; }
    public Window Window { get; }
    public ManualDelays Delays { get; } = new();
    public List<string> Opened { get; } = new();
    public List<string> Installed { get; } = new();
    public List<string> Uninstalled { get; } = new();
    public List<(string Id, string Message)> Failed { get; } = new();
    public List<string> Prompts { get; } = new();

    public string[] Ids => Gallery.Shown.Select(a => a.Id).ToArray();

    public void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    public void Key(Key key, PhysicalKey physical)
    {
        Window.KeyPressQwerty(physical, RawInputModifiers.None);
        Pump();
    }

    public void Dispose() => Window.Close();
}

public class AgentGalleryTests
{
    private static void Click(AvaloniaButton? button)
    {
        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    #region Browse

    [AvaloniaFact]
    public void Shows_The_Sample_Catalog_By_Popularity()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        Assert.Equal(14, gallery.ResultCount);
        Assert.Equal("inbox-sorter", rig.Ids[0]);
        Assert.Equal("quill-draft", rig.Ids[1]);
        Assert.Equal("14 agents", gallery.CountText!.Text);
        Assert.Equal("Agents", gallery.TitleText!.Text);
        Assert.Equal(14, gallery.ShownCards.Count);
        Assert.All(gallery.ShownCards, c => Assert.True(c.IsVisible));
        // All and the six categories.
        Assert.Equal(["All", "Writing", "Research", "Coding", "Data", "Operations", "Support"],
            gallery.CategoryBar!.Options!.Select(o => o.Label).ToArray());
        // 1000 wide less the paddings fits three 248 px columns.
        Assert.Equal(3, gallery.CardGrid!.Columns);
    }

    [AvaloniaFact]
    public void A_Card_Shows_Its_Agent()
    {
        using var rig = new AgentGalleryRig();
        var card = rig.Gallery.CardOf("quill-draft")!;
        Assert.Equal("Quill Draft", card.NameText!.Text);
        Assert.Equal("4.8 (2,310)", card.RatingText!.Text);
        Assert.Equal("48.2K installs", card.InstallsText!.Text);
        Assert.True(card.IsFeaturedShown);
        Assert.Equal(["Drafting", "Tone matching"], card.ShownTags.ToArray());
        Assert.Equal("Install", card.InstallButton!.Content);
        Assert.Equal("Quill Draft by Fernhill Studio", AutomationProperties.GetName(card));
        Assert.Equal("Install Quill Draft", AutomationProperties.GetName(card.InstallButton));
        // Nobody rated the newest support agent yet.
        Assert.Equal("New", rig.Gallery.CardOf("reply-coach")!.RatingText!.Text);
    }

    [AvaloniaFact]
    public void Categories_Search_And_Sort_Combine()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        gallery.SelectedCategoryKey = "coding";
        rig.Pump();
        Assert.Equal(["patch-pilot", "test-weaver"], rig.Ids);
        Assert.Equal("coding", gallery.CategoryBar!.SelectedKey);
        Assert.Equal("2 agents shown", gallery.LastAnnouncement);

        gallery.SelectedCategoryKey = null;
        gallery.SearchText = "  draft ";
        rig.Pump();
        // Name, pitch and tags all count; the search box follows the property.
        Assert.Equal(["inbox-sorter", "quill-draft"], rig.Ids);
        Assert.Equal("  draft ", gallery.SearchBox!.Text);
        Assert.Equal("2 agents match “draft”", gallery.LastAnnouncement);
        Assert.Equal("draft", gallery.CardOf("quill-draft")!.NameText!.Query);
        Assert.Equal(1, gallery.CardOf("quill-draft")!.NameText!.MatchCount);

        gallery.SearchText = null;
        gallery.Sort = AgentGallerySort.Newest;
        rig.Pump();
        Assert.Equal("reply-coach", rig.Ids[0]);
        Assert.Equal(1, gallery.SortSwitch!.SelectedIndex);

        gallery.Sort = AgentGallerySort.TopRated;
        rig.Pump();
        Assert.Equal("lit-scout", rig.Ids[0]);
        Assert.Equal("reply-coach", rig.Ids[^1]);
    }

    [AvaloniaFact]
    public void The_Controls_Write_Back_To_The_Properties()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        gallery.SearchBox!.Text = "ledger";
        rig.Pump();
        Assert.Equal("ledger", gallery.SearchText);
        Assert.Equal(["ledger-check"], rig.Ids);

        gallery.CategoryBar!.SelectedKey = "support";
        rig.Pump();
        Assert.Equal("support", gallery.SelectedCategoryKey);

        gallery.SortSwitch!.SelectedIndex = 2;
        rig.Pump();
        Assert.Equal(AgentGallerySort.TopRated, gallery.Sort);
    }

    [AvaloniaFact]
    public void No_Match_Shows_The_Empty_State_With_Its_Ways_Out()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        gallery.SelectedCategoryKey = "writing";
        gallery.SearchText = "zebra";
        rig.Pump();
        Assert.Equal(0, gallery.ResultCount);
        Assert.Contains(":empty", gallery.Classes);
        Assert.True(gallery.EmptyView!.IsVisible);
        Assert.False(gallery.CardGrid!.IsVisible);
        Assert.Equal("No agents match “zebra”", gallery.EmptyTitle!.Text);
        Assert.True(gallery.ClearSearchButton!.IsVisible);
        Assert.True(gallery.ShowAllButton!.IsVisible);

        Click(gallery.ShowAllButton);
        rig.Pump();
        Assert.Null(gallery.SelectedCategoryKey);
        Click(gallery.ClearSearchButton);
        rig.Pump();
        Assert.Null(gallery.SearchText);
        Assert.Equal(14, gallery.ResultCount);
        Assert.False(gallery.EmptyView.IsVisible);
    }

    [AvaloniaFact]
    public void Follows_An_Observable_List_And_Reuses_Its_Cards()
    {
        var agents = new ObservableCollection<AgentGalleryItem>(AgentGallerySamples.Agents().Take(3));
        using var rig = new AgentGalleryRig(configure: g => g.Agents = agents);
        var gallery = rig.Gallery;
        Assert.Equal(3, gallery.ResultCount);
        var first = gallery.ShownCards[0];
        agents.Add(new AgentGalleryItem { Id = "mine", Name = "My agent", Pitch = "Does my thing.", CategoryKey = "writing", Installs = 1_000_000 });
        rig.Pump();
        Assert.Equal(4, gallery.ResultCount);
        Assert.Equal("mine", rig.Ids[0]);
        Assert.Same(first, gallery.ShownCards[0]);
        Assert.Equal("1M installs", gallery.CardOf("mine")!.InstallsText!.Text);
        // An agent without a glyph shows its initial.
        Assert.Null(TestHelpers.Descendant<AgentGalleryMark>(gallery.CardOf("mine")!).Glyph);
    }

    [AvaloniaFact]
    public void Columns_Follow_The_Width()
    {
        Assert.Equal(1, AgentGalleryGrid.ColumnsFor(300, 248, 12, 4));
        Assert.Equal(2, AgentGalleryGrid.ColumnsFor(508, 248, 12, 4));
        Assert.Equal(3, AgentGalleryGrid.ColumnsFor(800, 248, 12, 4));
        Assert.Equal(4, AgentGalleryGrid.ColumnsFor(3000, 248, 12, 4));

        using var rig = new AgentGalleryRig(width: 420);
        Assert.Contains(":compact", rig.Gallery.Classes);
        Assert.Equal(1, rig.Gallery.CardGrid!.Columns);
        Assert.Equal(1, Grid.GetRow(rig.Gallery.SearchBox!));
    }

    #endregion

    #region Keyboard

    [Fact]
    public void Grid_Keys_Move_Like_A_Grid()
    {
        Assert.Equal(1, AgentGalleryQuery.MoveIndex(0, 10, 4, Key.Right));
        Assert.Null(AgentGalleryQuery.MoveIndex(0, 10, 4, Key.Left));
        Assert.Equal(4, AgentGalleryQuery.MoveIndex(0, 10, 4, Key.Down));
        Assert.Equal(9, AgentGalleryQuery.MoveIndex(7, 10, 4, Key.Down));
        Assert.Null(AgentGalleryQuery.MoveIndex(9, 10, 4, Key.Down));
        Assert.Equal(2, AgentGalleryQuery.MoveIndex(6, 10, 4, Key.Up));
        Assert.Equal(9, AgentGalleryQuery.MoveIndex(3, 10, 4, Key.End));
        Assert.Equal(0, AgentGalleryQuery.MoveIndex(3, 10, 4, Key.Home));
        Assert.Null(AgentGalleryQuery.MoveIndex(3, 10, 4, Key.A));
    }

    [AvaloniaFact]
    public void Only_The_Current_Card_Is_A_Tab_Stop_And_Arrows_Move_It()
    {
        using var rig = new AgentGalleryRig();
        var cards = rig.Gallery.ShownCards;
        Assert.True(KeyboardNavigation.GetIsTabStop(cards[0]));
        Assert.True(KeyboardNavigation.GetIsTabStop(cards[0].InstallButton!));
        Assert.All(cards.Skip(1), c => Assert.False(KeyboardNavigation.GetIsTabStop(c)));

        cards[0].Focus(NavigationMethod.Tab);
        rig.Key(Key.Right, PhysicalKey.ArrowRight);
        Assert.True(cards[1].IsFocused);
        var columns = rig.Gallery.CardGrid!.Columns;
        rig.Key(Key.Down, PhysicalKey.ArrowDown);
        Assert.True(cards[1 + columns].IsFocused);
        Assert.True(KeyboardNavigation.GetIsTabStop(cards[1 + columns]));
        Assert.False(KeyboardNavigation.GetIsTabStop(cards[1]));
        rig.Key(Key.End, PhysicalKey.End);
        Assert.True(cards[13].IsFocused);
    }

    [AvaloniaFact]
    public void Enter_Opens_The_Detail_And_Escape_Returns_To_The_Card()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        var card = gallery.ShownCards[2];
        var id = card.Agent!.Id;
        card.Focus(NavigationMethod.Tab);
        rig.Key(Key.Enter, PhysicalKey.Enter);
        Assert.Equal(id, gallery.OpenAgentId);
        Assert.Equal([id], rig.Opened);
        Assert.Contains(":detail", gallery.Classes);
        Assert.True(gallery.DetailView!.IsVisible);
        Assert.True(gallery.BackButton!.IsFocused);

        rig.Key(Key.Escape, PhysicalKey.Escape);
        Assert.Null(gallery.OpenAgentId);
        Assert.DoesNotContain(":detail", gallery.Classes);
        Assert.True(card.IsFocused);
    }

    [AvaloniaFact]
    public void Arrow_Down_In_The_Search_Moves_To_The_Cards()
    {
        using var rig = new AgentGalleryRig();
        rig.Gallery.SearchBox!.Focus(NavigationMethod.Tab);
        rig.Key(Key.Down, PhysicalKey.ArrowDown);
        Assert.True(rig.Gallery.ShownCards[0].IsFocused);
    }

    #endregion

    #region Detail

    [AvaloniaFact]
    public void The_Detail_View_Lists_Abilities_Permissions_And_Prompts()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        gallery.Open("inbox-sorter");
        rig.Pump();
        Assert.Equal("Inbox Sorter", gallery.DetailName!.Text);
        Assert.Equal("4.6 (1,874) · 61.5K installs · Operations · Added Nov 2, 2025", gallery.DetailStats!.Text);
        Assert.StartsWith("Rated 4.6 out of 5", AutomationProperties.GetName(gallery.DetailStats));
        Assert.Equal(3, gallery.AbilitiesList!.Children.Count);
        Assert.Equal(3, gallery.PermissionsList!.Children.Count);
        Assert.Equal("Create drafts, Sensitive", AutomationProperties.GetName(gallery.PermissionsList.Children[2]));
        Assert.Equal(3, gallery.PromptsList!.Children.Count);

        Click(gallery.PromptsList.Children[0] as AvaloniaButton);
        Assert.Equal(["What needs my answer today?"], rig.Prompts);

        Click(gallery.BackButton);
        rig.Pump();
        Assert.Null(gallery.OpenAgentId);
    }

    [AvaloniaFact]
    public void Searching_While_Open_Returns_To_The_Grid()
    {
        using var rig = new AgentGalleryRig(configure: g => g.OpenAgentId = "lit-scout");
        Assert.Contains(":detail", rig.Gallery.Classes);
        rig.Gallery.SearchText = "chart";
        rig.Pump();
        Assert.Null(rig.Gallery.OpenAgentId);
        Assert.Equal(["chart-sketch"], rig.Ids);
    }

    [AvaloniaFact]
    public void Unknown_Ids_Are_Ignored()
    {
        using var rig = new AgentGalleryRig();
        rig.Gallery.Open("nope");
        rig.Pump();
        Assert.Null(rig.Gallery.OpenAgentId);
        Assert.Empty(rig.Opened);
    }

    #endregion

    #region Install

    [AvaloniaFact]
    public void A_Simulated_Install_Shows_Progress_Then_Installed()
    {
        using var rig = new AgentGalleryRig();
        var gallery = rig.Gallery;
        var card = gallery.CardOf("quill-draft")!;
        Click(card.InstallButton);
        rig.Pump();
        Assert.Equal(AgentGalleryInstallState.Installing, gallery.GetInstallState("quill-draft"));
        Assert.True(card.InstallButton!.IsLoading);
        Assert.Equal("Installing", card.InstallButton.Content);
        Assert.Equal("Installing Quill Draft", gallery.LastAnnouncement);
        Assert.Equal([AgentGallerySamples.SimulatedInstallTime], rig.Delays.Pending);

        // Pressing again while it runs does nothing.
        Click(card.InstallButton);
        Assert.Single(rig.Delays.Pending);

        rig.Delays.FinishAll();
        rig.Pump();
        Assert.Equal(["quill-draft"], gallery.InstalledIds);
        Assert.Equal(["quill-draft"], rig.Installed);
        Assert.False(card.InstallButton.IsLoading);
        Assert.Equal("Installed", card.InstallButton.Content);
        Assert.Contains(":installed", card.Classes);
        Assert.Equal("Quill Draft by Fernhill Studio, Installed", AutomationProperties.GetName(card));
        Assert.Equal("Quill Draft installed", gallery.LastAnnouncement);

        // The installed card's button opens the detail view, where Uninstall removes it.
        Click(card.InstallButton);
        rig.Pump();
        Assert.Equal("quill-draft", gallery.OpenAgentId);
        Assert.False(gallery.DetailInstallButton!.IsVisible);
        Assert.True(gallery.DetailInstalledBadge!.IsVisible);
        Assert.True(gallery.UninstallButton!.IsVisible);
        Click(gallery.UninstallButton);
        rig.Pump();
        Assert.Empty(gallery.InstalledIds);
        Assert.Equal(["quill-draft"], rig.Uninstalled);
        Assert.True(gallery.DetailInstallButton.IsVisible);
        Assert.Equal("Install", card.InstallButton.Content);
    }

    [AvaloniaFact]
    public void A_Failed_Install_Shows_The_Message_And_Retries()
    {
        var calls = 0;
        using var rig = new AgentGalleryRig(configure: g => g.InstallAsync = (_, _) =>
        {
            calls++;
            return calls == 1 ? Task.FromException(new InvalidOperationException("Not approved yet.")) : Task.CompletedTask;
        });
        var gallery = rig.Gallery;
        gallery.Open("lit-scout");
        rig.Pump();
        Click(gallery.DetailInstallButton);
        rig.Pump();
        Assert.Equal(AgentGalleryInstallState.Failed, gallery.GetInstallState("lit-scout"));
        Assert.Equal([("lit-scout", "Not approved yet.")], rig.Failed);
        Assert.True(gallery.DetailError!.IsVisible);
        Assert.Equal("Not approved yet.", gallery.DetailErrorText!.Text);
        Assert.Equal("Retry", gallery.DetailInstallButton!.Content);
        Assert.Equal("Retry", gallery.CardOf("lit-scout")!.InstallButton!.Content);
        Assert.Equal("Lit Scout did not install. Not approved yet.", gallery.LastAnnouncement);

        Click(gallery.DetailInstallButton);
        rig.Pump();
        Assert.Equal(AgentGalleryInstallState.Installed, gallery.GetInstallState("lit-scout"));
        Assert.False(gallery.DetailError.IsVisible);
        Assert.Equal(["lit-scout"], rig.Installed);
    }

    [AvaloniaFact]
    public void Installed_Ids_From_Outside_Update_The_Cards()
    {
        using var rig = new AgentGalleryRig();
        rig.Gallery.InstalledIds = ["patch-pilot"];
        rig.Pump();
        Assert.Equal("Installed", rig.Gallery.CardOf("patch-pilot")!.InstallButton!.Content);
        Assert.Empty(rig.Installed);
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Cancels_A_Running_Install()
    {
        var rig = new AgentGalleryRig();
        _ = rig.Gallery.Install("quill-draft");
        rig.Pump();
        rig.Window.Content = null;
        rig.Pump();
        Assert.Equal(AgentGalleryInstallState.Available, rig.Gallery.GetInstallState("quill-draft"));
        Assert.Empty(rig.Installed);
        rig.Dispose();
    }

    #endregion

    [AvaloniaFact]
    public void Labels_Reword_The_Gallery()
    {
        using var rig = new AgentGalleryRig(configure: g => g.Labels = new AgentGalleryLabels
        {
            Title = "Assistants",
            AllCategories = "Everything",
            ResultCount = n => $"{n} found",
            Install = "Add"
        });
        Assert.Equal("Assistants", rig.Gallery.TitleText!.Text);
        Assert.Equal("Everything", rig.Gallery.CategoryBar!.Options![0].Label);
        Assert.Equal("14 found", rig.Gallery.CountText!.Text);
        Assert.Equal("Add", rig.Gallery.ShownCards[0].InstallButton!.Content);
        rig.Gallery.Subtitle = "";
        rig.Pump();
        Assert.False(rig.Gallery.SubtitleText!.IsVisible);
    }
}
