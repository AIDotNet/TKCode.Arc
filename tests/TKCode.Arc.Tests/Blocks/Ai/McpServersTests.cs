using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="McpServers"/> on its invented samples (Field notes, Ledgerline, Orbit calendar, Sketchpad): the rows and their
/// status lines, opening a server, the tools' switches and Enable all, turning a server off and on, connecting (simulated
/// and through a hook, success, failure, throwing, cancellation), the error box and its log, adding with the form's
/// checks, removing with the confirmation, outside lists, the keyboard, the live region, the narrow layout and the live
/// dot. Every wait is released by hand.
/// </summary>
public class McpServersTests
{
    /// <summary>Waits the test releases by hand (the simulated connection).</summary>
    private sealed class Gate
    {
        private readonly List<TaskCompletionSource> _waits = new();

        public int Count => _waits.Count;

        public Task Wait(TimeSpan time, CancellationToken token)
        {
            var done = new TaskCompletionSource();
            token.Register(() => done.TrySetCanceled(token));
            _waits.Add(done);
            return done.Task;
        }

        public void ReleaseAll()
        {
            var waits = _waits.ToArray();
            _waits.Clear();
            foreach (var wait in waits)
            {
                wait.TrySetResult();
            }
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (McpServers Block, Window Window, Gate Gate) Show(double width = 680, ArcMotionLevel level = ArcMotionLevel.Off,
        Action<McpServers>? configure = null)
    {
        var gate = new Gate();
        var block = new McpServers { Delay = gate.Wait };
        configure?.Invoke(block);
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        Pump();
        return (block, window, gate);
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(AvaloniaButton? button)
    {
        button.ShouldNotBeNull();
        button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Pump();
    }

    private static McpServer ById(McpServers block, string id) => block.Servers!.Single(s => s.Id == id);

    #region Rows

    [AvaloniaFact]
    public void Defaults_Show_The_Four_Samples_With_Their_Status_Lines()
    {
        var (block, _, _) = Show();
        block.Rows.Select(r => r.Server.Id).ShouldBe(["field-notes", "ledgerline", "orbit-calendar", "sketchpad"]);
        var notes = block.RowOf(ById(block, "field-notes"))!;
        notes.StatusText.ShouldBe("Connected · 5 tools");
        notes.EndpointText.ShouldBe("https://notes.example.dev/mcp");
        notes.MarkText.ShouldBe("FN");
        notes.Dot.Status.ShouldBe(McpServerStatus.Connected);
        block.RowOf(ById(block, "ledgerline"))!.StatusText.ShouldBe("Connected · 3 of 4 tools");
        block.RowOf(ById(block, "ledgerline"))!.EndpointText.ShouldBe("ledgerline-mcp --books ./books/2026 --read-write");
        block.RowOf(ById(block, "orbit-calendar"))!.StatusText.ShouldBe("Couldn't connect");
        var sketch = block.RowOf(ById(block, "sketchpad"))!;
        sketch.StatusText.ShouldBe("Off");
        sketch.ServerSwitch.IsChecked.ShouldBe(false);
        sketch.Dot.Status.ShouldBe(McpServerStatus.Disabled);

        block.ConnectedCount.ShouldBe(2);
        block.EnabledToolCount.ShouldBe(8);
        block.SummaryText!.Text.ShouldBe("2 of 4 connected · 8 tools on");
        block.Rows.ShouldAllBe(r => !r.IsBodyBuilt);
    }

    [AvaloniaFact]
    public void Each_Block_Gets_Its_Own_Copy_Of_The_Samples()
    {
        var a = new McpServers();
        var b = new McpServers();
        a.Servers.ShouldNotBeSameAs(b.Servers);
        a.Servers![0].ShouldNotBeSameAs(b.Servers![0]);
    }

    [AvaloniaFact]
    public void The_Header_Names_The_Server_And_Reports_Expanded()
    {
        var (block, _, _) = Show();
        var row = block.Rows[0];
        AutomationProperties.GetName(row.Header).ShouldBe("Field notes");
        AutomationProperties.GetHelpText(row.Header).ShouldBe("Connected · 5 tools, https://notes.example.dev/mcp");
        AutomationProperties.GetName(row.ServerSwitch).ShouldBe("Use Field notes");
        var peer = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(row.Header);
        peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        peer.Expand();
        Pump();
        block.ExpandedServer.ShouldBe(row.Server);
        peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
    }

    #endregion

    #region Opening a server

    [AvaloniaFact]
    public void Opening_Builds_The_Body_Once_And_Only_One_Server_Is_Open()
    {
        var (block, _, _) = Show();
        var notes = block.Rows[0];
        var ledger = block.Rows[1];
        Click(notes.Header);
        notes.IsExpanded.ShouldBeTrue();
        notes.Classes.ShouldContain(":open");
        notes.IsBodyBuilt.ShouldBeTrue();
        notes.ToolRows.Select(t => t.Tool.Name).ShouldBe(["search_notes", "read_note", "list_notebooks", "create_note", "tag_note"]);
        notes.ToolsCount!.Text.ShouldBe("5 of 5 on");
        notes.ToggleAllButton!.Content.ShouldBe("Disable all");
        notes.ErrorBox!.IsVisible.ShouldBeFalse();
        notes.ReconnectButton!.IsVisible.ShouldBeTrue();

        Click(ledger.Header);
        notes.IsExpanded.ShouldBeFalse();
        ledger.IsExpanded.ShouldBeTrue();
        block.ExpandedServer.ShouldBe(ledger.Server);

        Click(ledger.Header);
        block.ExpandedServer.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Tools_That_Change_Data_Carry_The_Tag_And_A_Named_Switch()
    {
        var (block, _, _) = Show();
        block.ExpandedServer = ById(block, "field-notes");
        Pump();
        var rows = block.Rows[0].ToolRows;
        var search = rows.Single(r => r.Tool.Name == "search_notes");
        var create = rows.Single(r => r.Tool.Name == "create_note");
        AutomationProperties.GetName(search.Toggle).ShouldBe("search_notes");
        AutomationProperties.GetHelpText(search.Toggle).ShouldBe("Finds notes whose title or text matches a query.");
        AutomationProperties.GetName(create.Toggle).ShouldBe("create_note, Changes data");
        search.ShowsDivider.ShouldBeFalse();
        create.ShowsDivider.ShouldBeTrue();
    }

    #endregion

    #region Tools

    [AvaloniaFact]
    public void A_Tool_Switch_Writes_The_Tool_Raises_The_Event_And_Updates_The_Counts()
    {
        var (block, _, _) = Show();
        var server = ById(block, "field-notes");
        block.ExpandedServer = server;
        Pump();
        McpServersToolToggledEventArgs? raised = null;
        block.ToolToggled += (_, e) => raised = e;
        var row = block.Rows[0];
        row.ToolRows.Single(t => t.Tool.Name == "tag_note").Toggle.IsChecked = false;
        Pump();
        server.Tools.Single(t => t.Name == "tag_note").IsEnabled.ShouldBeFalse();
        raised.ShouldNotBeNull();
        raised.Server.ShouldBe(server);
        raised.Tools.Select(t => t.Name).ShouldBe(["tag_note"]);
        raised.IsEnabled.ShouldBeFalse();
        row.StatusText.ShouldBe("Connected · 4 of 5 tools");
        row.ToolsCount!.Text.ShouldBe("4 of 5 on");
        row.ToggleAllButton!.Content.ShouldBe("Enable all");
        block.EnabledToolCount.ShouldBe(7);
        block.LastAnnouncement.ShouldBe("tag_note off");
    }

    [AvaloniaFact]
    public void Enable_All_And_Disable_All_Change_Every_Tool_In_One_Event()
    {
        var (block, _, _) = Show();
        var server = ById(block, "ledgerline");
        block.ExpandedServer = server;
        Pump();
        var events = new List<McpServersToolToggledEventArgs>();
        block.ToolToggled += (_, e) => events.Add(e);
        var row = block.RowOf(server)!;
        row.ToggleAllButton!.Content.ShouldBe("Enable all");
        Click(row.ToggleAllButton);
        server.Tools.ShouldAllBe(t => t.IsEnabled);
        events.Single().Tools.Select(t => t.Name).ShouldBe(["post_entry"]);
        events.Single().IsEnabled.ShouldBeTrue();
        row.ToolRows.ShouldAllBe(t => t.Toggle.IsChecked == true);
        block.LastAnnouncement.ShouldBe("All 4 tools of Ledgerline on");

        Click(row.ToggleAllButton);
        server.Tools.ShouldAllBe(t => !t.IsEnabled);
        events[1].Tools.Count.ShouldBe(4);
        row.StatusText.ShouldBe("Connected · 0 of 4 tools");
    }

    [AvaloniaFact]
    public void A_Tool_Changed_In_Code_Moves_Its_Switch_Without_An_Event()
    {
        var (block, _, _) = Show();
        var server = ById(block, "field-notes");
        block.ExpandedServer = server;
        Pump();
        var raised = 0;
        block.ToolToggled += (_, _) => raised++;
        server.Tools[0].IsEnabled = false;
        Pump();
        block.RowOf(server)!.ToolRows[0].Toggle.IsChecked.ShouldBe(false);
        raised.ShouldBe(0);
        server.Tools.Add(new McpTool("archive_note", "Moves a note to the archive.", changesData: true));
        Pump();
        block.RowOf(server)!.ToolRows.Count.ShouldBe(6);
        block.RowOf(server)!.StatusText.ShouldBe("Connected · 5 of 6 tools");
    }

    #endregion

    #region Turning servers off and on

    [AvaloniaFact]
    public void Turning_A_Server_Off_And_On_Reconnects_It()
    {
        var (block, _, gate) = Show();
        var server = ById(block, "field-notes");
        var row = block.RowOf(server)!;
        var toggles = new List<bool>();
        var statuses = new List<(McpServerStatus, McpServerStatus)>();
        block.ServerToggled += (_, e) => toggles.Add(e.IsEnabled);
        block.StatusChanged += (_, e) => statuses.Add((e.OldStatus, e.NewStatus));

        row.ServerSwitch.IsChecked = false;
        Pump();
        server.IsEnabled.ShouldBeFalse();
        server.Status.ShouldBe(McpServerStatus.Disabled);
        row.StatusText.ShouldBe("Off");
        block.ConnectedCount.ShouldBe(1);
        block.LastAnnouncement.ShouldBe("Field notes turned off");

        row.ServerSwitch.IsChecked = true;
        Pump();
        server.Status.ShouldBe(McpServerStatus.Connecting);
        row.StatusText.ShouldBe("Connecting…");
        block.LastAnnouncement.ShouldBe("Connecting to Field notes");
        gate.Count.ShouldBe(1);
        gate.ReleaseAll();
        server.Status.ShouldBe(McpServerStatus.Connected);
        server.Tools.Count.ShouldBe(5);
        block.LastAnnouncement.ShouldBe("Field notes connected, 5 tools");
        toggles.ShouldBe([false, true]);
        statuses.ShouldBe([
            (McpServerStatus.Connected, McpServerStatus.Disabled),
            (McpServerStatus.Disabled, McpServerStatus.Connecting),
            (McpServerStatus.Connecting, McpServerStatus.Connected)]);
    }

    [AvaloniaFact]
    public void Turning_Off_While_Connecting_Cancels_The_Connection()
    {
        CancellationToken seen = default;
        var (block, _, _) = Show(configure: b => b.ConnectAsync = (_, token) =>
        {
            seen = token;
            return new TaskCompletionSource<McpConnectResult>().Task;
        });
        var server = ById(block, "sketchpad");
        block.RowOf(server)!.ServerSwitch.IsChecked = true;
        Pump();
        server.Status.ShouldBe(McpServerStatus.Connecting);
        block.RowOf(server)!.ServerSwitch.IsChecked = false;
        Pump();
        seen.IsCancellationRequested.ShouldBeTrue();
        server.Status.ShouldBe(McpServerStatus.Disabled);
    }

    #endregion

    #region Connecting

    [AvaloniaFact]
    public void Retry_Reconnects_A_Failed_Server_And_Clears_The_Error()
    {
        var (block, _, gate) = Show();
        var server = ById(block, "orbit-calendar");
        block.ExpandedServer = server;
        Pump();
        var row = block.RowOf(server)!;
        row.ErrorBox!.IsVisible.ShouldBeTrue();
        row.ErrorMessageText.ShouldBe("The server answered 401: the access key was refused.");
        row.ReconnectButton!.IsVisible.ShouldBeFalse();
        row.LogToggle!.IsVisible.ShouldBeTrue();
        ((Control)row.LogText!.Parent!.Parent!).IsVisible.ShouldBeFalse();
        Click(row.LogToggle);
        row.IsLogOpen.ShouldBeTrue();
        row.LogToggle.Content.ShouldBe("Hide log");
        row.LogText.Text!.ShouldContain("401 Unauthorized");

        Click(row.RetryButton);
        server.Status.ShouldBe(McpServerStatus.Connecting);
        row.FetchingLine!.IsVisible.ShouldBeTrue();
        gate.ReleaseAll();
        server.Status.ShouldBe(McpServerStatus.Connected);
        server.ErrorMessage.ShouldBeNull();
        server.Tools.Select(t => t.Name).ShouldBe(["list_events", "find_free_time", "create_event"]);
        row.ErrorBox.IsVisible.ShouldBeFalse();
        row.FetchingLine.IsVisible.ShouldBeFalse();
        row.StatusText.ShouldBe("Connected · 3 tools");
    }

    [AvaloniaFact]
    public void Reconnecting_Keeps_The_Switches_Of_Tools_Listed_Again()
    {
        var (block, _, gate) = Show();
        var server = ById(block, "ledgerline");
        var post = server.Tools.Single(t => t.Name == "post_entry");
        post.IsEnabled.ShouldBeFalse();
        _ = block.Connect(server);
        gate.ReleaseAll();
        server.Tools.Single(t => t.Name == "post_entry").ShouldBeSameAs(post);
        post.IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Hook_That_Fails_Or_Throws_Shows_The_Error()
    {
        var calls = 0;
        var (block, _, _) = Show(configure: b => b.ConnectAsync = (server, _) =>
        {
            calls++;
            if (calls == 1)
            {
                return Task.FromResult(McpConnectResult.Failure("Handshake timed out.", "waited 10 s"));
            }
            throw new InvalidOperationException("Socket closed.");
        });
        var server = ById(block, "field-notes");
        _ = block.Connect(server);
        Pump();
        server.Status.ShouldBe(McpServerStatus.Error);
        server.ErrorMessage.ShouldBe("Handshake timed out.");
        server.ErrorLog.ShouldBe("waited 10 s");
        block.LastAnnouncement.ShouldBe("Field notes couldn't connect. Handshake timed out.");
        _ = block.Connect(server);
        Pump();
        server.ErrorMessage.ShouldBe("Socket closed.");
        server.ErrorLog.ShouldBeNull();
        block.RowOf(server)!.Dot.Status.ShouldBe(McpServerStatus.Error);
    }

    [AvaloniaFact]
    public void Simulation_Fails_For_Invalid_Hosts_And_Missing_Programs()
    {
        var http = new McpServer { Id = "x", Name = "X", Transport = McpTransport.Http, Url = "https://tools.offline.invalid/mcp" };
        var failed = McpServersSamples.SimulateConnect(http);
        failed.Succeeded.ShouldBeFalse();
        failed.ErrorMessage.ShouldBe("No server answered at tools.offline.invalid.");
        failed.ErrorLog!.ShouldContain("name resolution failed");
        var command = new McpServer { Id = "y", Name = "Y", Transport = McpTransport.Command, Command = "missing-tool" };
        McpServersSamples.SimulateConnect(command).Succeeded.ShouldBeFalse();
        var fine = new McpServer { Id = "z", Name = "Paper trail", Transport = McpTransport.Command, Command = "paper-trail" };
        var ok = McpServersSamples.SimulateConnect(fine);
        ok.Succeeded.ShouldBeTrue();
        ok.Tools.Select(t => t.Name).ShouldBe(["paper_trail_search", "paper_trail_read", "paper_trail_write"]);
        ok.Tools[2].ChangesData.ShouldBeTrue();
    }

    #endregion

    #region Adding

    [AvaloniaFact]
    public void The_Form_Checks_Every_Field_And_Focuses_The_First_Problem()
    {
        var (block, _, _) = Show();
        Click(block.AddButton);
        block.IsAddFormOpen.ShouldBeTrue();
        block.Classes.ShouldContain(":adding");
        var form = block.Form!;
        form.SubmitButton.Content.ShouldBe("Add and connect");
        Click(form.SubmitButton);
        form.NameField.ErrorMessage.ShouldBe("Give the server a name.");
        form.UrlField.ErrorMessage.ShouldBe("Enter the server's address.");
        form.NameField.IsFocused.ShouldBeTrue();

        form.NameField.Text = "field NOTES";
        form.UrlField.Text = "notes.example.dev";
        form.NameField.ErrorMessage.ShouldBe("A server named “field NOTES” is already in the list.");
        form.UrlField.ErrorMessage.ShouldBe("Use a full address that starts with http:// or https://.");

        form.Transport = McpTransport.Command;
        form.UrlField.IsVisible.ShouldBeFalse();
        form.CommandField.IsVisible.ShouldBeTrue();
        form.CommandField.ErrorMessage.ShouldBe("Enter the program to start.");
        form.VariablesField.Label.ShouldBe("Environment (optional)");
        form.VariablesField.Text = "OK=1\n\nnot a pair";
        form.VariablesField.ErrorMessage.ShouldBe("Line 3 is not KEY=value.");
        block.Servers!.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void A_Valid_Form_Adds_The_Server_Raises_The_Event_And_Connects_It()
    {
        var (block, _, gate) = Show();
        McpServer? added = null;
        block.ServerAdded += (_, e) => added = e.Server;
        block.OpenAddForm();
        Pump();
        var form = block.Form!;
        form.NameField.Text = "  Paper trail ";
        form.Transport = McpTransport.Command;
        form.CommandField.Text = "paper-trail";
        form.ArgumentsField.Text = "--vault ./docs";
        form.VariablesField.Text = "TRAIL_TOKEN=abc=def\n";
        Click(form.SubmitButton);

        added.ShouldNotBeNull();
        added.Name.ShouldBe("Paper trail");
        added.Id.ShouldBe("paper-trail");
        added.Command.ShouldBe("paper-trail");
        added.Arguments.ShouldBe("--vault ./docs");
        added.Variables!.Single().ShouldBe(new McpServerVariable("TRAIL_TOKEN", "abc=def"));
        added.Status.ShouldBe(McpServerStatus.Connecting);
        block.Servers![^1].ShouldBe(added);
        block.Rows[^1].Server.ShouldBe(added);
        block.IsAddFormOpen.ShouldBeFalse();
        form.NameField.Text.ShouldBe(string.Empty);

        gate.ReleaseAll();
        added.Status.ShouldBe(McpServerStatus.Connected);
        added.Tools.Count.ShouldBe(3);
        block.ConnectedCount.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Enter_Submits_And_Escape_Cancels_The_Form()
    {
        var (block, window, _) = Show();
        block.OpenAddForm();
        Pump();
        var form = block.Form!;
        form.NameField.Focus();
        form.NameField.Text = "Weather desk";
        form.UrlField.Text = "https://weather.example.dev/mcp";
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        block.Servers!.Count.ShouldBe(5);
        block.Servers[^1].Url.ShouldBe("https://weather.example.dev/mcp");

        block.OpenAddForm();
        Pump();
        form.NameField.Focus();
        form.NameField.Text = "Draft";
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        block.IsAddFormOpen.ShouldBeFalse();
        block.AddButton!.IsFocused.ShouldBeTrue();
        block.Servers.Count.ShouldBe(5);
    }

    [AvaloniaFact]
    public void Ids_Stay_Unique_And_Initials_Come_From_Two_Words()
    {
        var servers = McpServersSamples.Servers();
        McpServersDraftCheck.MakeId("Field notes", servers).ShouldBe("field-notes-2");
        McpServersDraftCheck.MakeId("  Écho / Desk!  ", servers).ShouldBe("cho-desk");
        McpServersDraftCheck.Initials("orbit calendar").ShouldBe("OC");
        McpServersDraftCheck.Initials("Sketchpad").ShouldBe("S");
        McpServersDraftCheck.IsHttpAddress("ftp://x.dev").ShouldBeFalse();
        McpServersDraftCheck.IsHttpAddress("http://localhost:7070/mcp").ShouldBeTrue();
    }

    #endregion

    #region Removing

    [AvaloniaFact]
    public void Remove_Asks_First_And_Keep_Returns()
    {
        var (block, _, _) = Show();
        var server = ById(block, "field-notes");
        block.ExpandedServer = server;
        Pump();
        var row = block.RowOf(server)!;
        Click(row.RemoveButton);
        row.IsConfirmingRemove.ShouldBeTrue();
        row.ConfirmBox!.IsVisible.ShouldBeTrue();
        Click(row.KeepButton);
        row.IsConfirmingRemove.ShouldBeFalse();
        block.Servers!.ShouldContain(server);

        McpServer? removed = null;
        block.ServerRemoved += (_, e) => removed = e.Server;
        Click(row.RemoveButton);
        Click(row.ConfirmRemoveButton);
        removed.ShouldBe(server);
        block.Servers!.ShouldNotContain(server);
        block.Rows.Count.ShouldBe(3);
        block.ExpandedServer.ShouldBeNull();
        block.LastAnnouncement.ShouldBe("Field notes removed");
        block.SummaryText!.Text.ShouldBe("1 of 3 connected · 3 tools on");
    }

    [AvaloniaFact]
    public void Removing_Every_Server_Shows_The_Empty_State()
    {
        var (block, _, _) = Show();
        foreach (var server in block.Servers!.ToArray())
        {
            block.RemoveServer(server).ShouldBeTrue();
        }
        Pump();
        block.Classes.ShouldContain(":empty");
        block.EmptyState!.IsVisible.ShouldBeTrue();
        block.SummaryText!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Read_Only_List_Offers_No_Add_Or_Remove()
    {
        var (block, _, _) = Show(configure: b => b.Servers = McpServersSamples.Servers().ToArray());
        block.AddButton!.IsVisible.ShouldBeFalse();
        block.ExpandedServer = block.Servers![0];
        Pump();
        block.Rows[0].RemoveButton!.IsVisible.ShouldBeFalse();
        block.RemoveServer(block.Servers[0]).ShouldBeFalse();
        block.AddServer(new McpServerDraft("A", McpTransport.Http, "https://a.dev", null, null, [])).ShouldBeNull();
    }

    #endregion

    #region Outside lists, keyboard, layout, motion

    [AvaloniaFact]
    public void The_Block_Follows_An_Outside_List()
    {
        var list = new ObservableCollection<McpServer>();
        var (block, _, _) = Show(configure: b => b.Servers = list);
        block.Rows.Count.ShouldBe(0);
        block.Classes.ShouldContain(":empty");
        var server = new McpServer { Id = "a", Name = "Atlas", Url = "https://atlas.example.dev/mcp", Tools = { new McpTool("look_up", "Looks things up.") } };
        list.Add(server);
        Pump();
        block.Rows.Single().Server.ShouldBe(server);
        block.Classes.ShouldNotContain(":empty");
        block.RowOf(server)!.StatusText.ShouldBe("Connected · 1 tool");
        server.Name = "Atlas two";
        Pump();
        AutomationProperties.GetName(block.RowOf(server)!.Header).ShouldBe("Atlas two");
        list.Clear();
        Pump();
        block.Rows.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Arrows_Home_And_End_Move_Between_Headers_And_Escape_Closes()
    {
        var (block, window, _) = Show();
        var rows = block.Rows;
        rows[0].FocusHeader();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rows[1].Header.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rows[3].Header.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rows[3].Header.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        rows[0].Header.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        block.ExpandedServer.ShouldBe(rows[0].Server);
        rows[0].ReconnectButton!.Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        block.ExpandedServer.ShouldBeNull();
        rows[0].Header.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Compact_Layout_Follows_The_Blocks_Own_Width()
    {
        var (block, window, _) = Show(width: 460);
        block.IsCompact.ShouldBeTrue();
        block.Classes.ShouldContain(":compact");
        window.Width = 900;
        Pump();
        block.IsCompact.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Live_Dot_Breathes_Only_While_Connected_Or_Connecting()
    {
        var (block, _, _) = Show(level: ArcMotionLevel.Full);
        block.RowOf(ById(block, "field-notes"))!.Dot.WantsBreathing.ShouldBeTrue();
        block.RowOf(ById(block, "orbit-calendar"))!.Dot.WantsBreathing.ShouldBeFalse();
        block.RowOf(ById(block, "sketchpad"))!.Dot.WantsBreathing.ShouldBeFalse();
        var (reduced, _, _) = Show(level: ArcMotionLevel.Reduced);
        reduced.RowOf(ById(reduced, "field-notes"))!.Dot.IsBreathing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (block, _, _) = Show();
        block.ExpandedServer = block.Servers![0];
        Pump();
        block.Labels = new McpServersLabels { Title = "Serveurs", AddServer = "Ajouter", Connected = "Connecté", Reconnect = "Reconnecter" };
        Pump();
        block.AddButton!.Content.ShouldBe("Ajouter");
        block.Rows[0].StatusText.ShouldBe("Connecté · 5 tools");
        block.Rows[0].ReconnectButton!.Content.ShouldBe("Reconnecter");
        block.Title = "Mes outils";
        TestHelpers.Descendant<TextBlock>(block, McpServers.TitlePart).Text.ShouldBe("Mes outils");
    }

    #endregion
}
