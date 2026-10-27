using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// <see cref="Integrations"/> on the invented demo data (nine apps, three categories, two connected): filters and counts,
/// search, empty states, the connect state machine (success, failure, retry, cancel), the drawer and disconnect, focus
/// after each step (our fixes of Arc's dropped focus and double tab stop), compact layout and announcements.
/// </summary>
public class IntegrationsTests
{
    private sealed class Rig
    {
        public Rig(double width = 900, Action<Integrations>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off)
        {
            Block = new Integrations { SimulatedConnectDuration = TimeSpan.Zero, DisconnectSettleDelay = TimeSpan.Zero };
            IntegrationsDemoData.Fill(Block);
            configure?.Invoke(Block);
            Window = TestHelpers.Show(Block, level);
            Window.Width = width;
            Window.Height = 900;
            Pump();
        }

        public Integrations Block { get; }

        public Window Window { get; }

        public IntegrationItem App(string key) => Block.Items.First(i => i.Key == key);

        public IntegrationRow Row(string key) => Block.RowFor(App(key)).ShouldNotBeNull();

        public IReadOnlyList<string> VisibleKeys => Block.VisibleItems.Select(i => i.Key).ToList();

        public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

        public void Pump()
        {
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Pump();
        }
    }

    [AvaloniaFact]
    public void Shows_every_app_with_counted_filters()
    {
        var rig = new Rig();
        rig.VisibleKeys.Count.ShouldBe(9);
        rig.Block.VisibleCount.ShouldBe(9);
        rig.Block.ConnectedCount.ShouldBe(2);
        var pills = rig.Block.FilterBar!.Pills;
        pills.Select(p => p.Key).ShouldBe(["all", "connected", "messaging", "files", "insights"]);
        pills.Select(p => p.Count).ShouldBe([9, 2, 3, 3, 3]);
        AutomationProperties.GetName(pills[3]).ShouldBe("Files, 3");
        pills[0].IsChecked.ShouldBe(true);
        pills.Count(p => p.IsTabStop).ShouldBe(1);
        rig.Block.TitleBlock!.Text.ShouldBe("Integrations");
        rig.Row("chirp").ShowsDivider.ShouldBeFalse();
        rig.Row("vaultline").ShowsDivider.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clicking_a_filter_narrows_the_list_and_announces()
    {
        var rig = new Rig();
        IntegrationFilterChangedEventArgs? args = null;
        rig.Block.FilterChanged += (_, e) => args = e;
        var files = rig.Block.FilterBar!.Pills[3];
        files.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        rig.Pump();
        rig.Block.SelectedFilter.ShouldBe("files");
        rig.VisibleKeys.ShouldBe(["vaultline", "boxyard", "coldstore"]);
        args.ShouldNotBeNull();
        args.OldFilter.ShouldBe("all");
        args.NewFilter.ShouldBe("files");
        args.VisibleCount.ShouldBe(3);
        rig.Block.Announcer!.Announcement.ShouldBe("3 Files apps shown");
        files.IsChecked.ShouldBe(true);
        files.IsTabStop.ShouldBeTrue();
        rig.Block.FilterBar.Pills[0].IsTabStop.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Arrow_keys_move_and_select_the_filter_and_wrap()
    {
        var rig = new Rig();
        var bar = rig.Block.FilterBar!;
        bar.FocusSelected(NavigationMethod.Tab).ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowRight);
        rig.Block.SelectedFilter.ShouldBe("connected");
        rig.Focused.ShouldBe(bar.Pills[1]);
        rig.VisibleKeys.ShouldBe(["chirp", "vaultline"]);
        rig.Key(PhysicalKey.End);
        rig.Block.SelectedFilter.ShouldBe("insights");
        rig.Key(PhysicalKey.ArrowRight);
        rig.Block.SelectedFilter.ShouldBe("all");
        rig.Key(PhysicalKey.ArrowLeft);
        rig.Block.SelectedFilter.ShouldBe("insights");
        rig.Key(PhysicalKey.Home);
        rig.Block.SelectedFilter.ShouldBe("all");
        rig.Focused.ShouldBe(bar.Pills[0]);
    }

    [AvaloniaFact]
    public void Search_matches_names_categories_and_descriptions()
    {
        var rig = new Rig();
        IntegrationSearchChangedEventArgs? args = null;
        rig.Block.SearchChanged += (_, e) => args = e;
        rig.Block.SearchBox!.Text = "Vault";
        rig.Pump();
        rig.Block.SearchText.ShouldBe("Vault");
        rig.VisibleKeys.ShouldBe(["vaultline"]);
        args!.VisibleCount.ShouldBe(1);
        rig.Block.Announcer!.Announcement.ShouldBe("1 match for “vault”");
        rig.Block.SearchText = "invoice";
        rig.Pump();
        rig.VisibleKeys.ShouldBe(["coldstore"]);
        rig.Block.SearchBox.Text.ShouldBe("invoice");
        rig.Block.SearchText = "insights";
        rig.Pump();
        rig.VisibleKeys.ShouldBe(["pulsegrid", "tallymark", "ledgerline"]);
        // Search and filter combine; the counts on the pills ignore the search.
        rig.Block.SelectedFilter = "connected";
        rig.Pump();
        rig.VisibleKeys.ShouldBeEmpty();
        rig.Block.FilterBar!.Pills.Select(p => p.Count).ShouldBe([9, 2, 3, 3, 3]);
    }

    [AvaloniaFact]
    public void Empty_search_offers_clear_search_and_returns_the_focus_to_the_field()
    {
        var rig = new Rig();
        rig.Block.SearchBox!.Text = "zebra";
        rig.Pump();
        var empty = rig.Block.EmptyView.ShouldNotBeNull();
        empty.Kind.ShouldBe(IntegrationEmptyKind.Search);
        empty.Title.ShouldBe("Nothing matches “zebra”");
        empty.Action.ShouldNotBeNull().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        rig.Pump();
        rig.Block.SearchText.ShouldBeEmpty();
        rig.VisibleKeys.Count.ShouldBe(9);
        rig.Block.EmptyView.ShouldBeNull();
        rig.Block.SearchBox.IsKeyboardFocusWithin.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Empty_connected_filter_offers_all_apps_and_focuses_the_all_filter()
    {
        var rig = new Rig();
        rig.Block.SelectedFilter = Integrations.ConnectedFilter;
        rig.Pump();
        rig.Block.DisconnectAsync(rig.App("chirp")).Result.ShouldBeTrue();
        rig.Block.DisconnectAsync(rig.App("vaultline")).Result.ShouldBeTrue();
        rig.Pump();
        rig.VisibleKeys.ShouldBeEmpty();
        var empty = rig.Block.EmptyView.ShouldNotBeNull();
        empty.Kind.ShouldBe(IntegrationEmptyKind.Connected);
        empty.Action!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        rig.Pump();
        rig.Block.SelectedFilter.ShouldBe(Integrations.AllFilter);
        rig.Focused.ShouldBe(rig.Block.FilterBar!.Pills[0]);
    }

    [AvaloniaFact]
    public void Idle_rows_have_one_tab_stop_per_action()
    {
        var rig = new Rig();
        static List<Control> TabStops(IntegrationRow row) => row.GetVisualDescendants().OfType<InputElement>()
            .Where(e => e.Focusable && e.IsTabStop && e.IsEffectivelyVisible && e.IsEffectivelyEnabled)
            .Cast<Control>().ToList();
        var idle = rig.Row("pulsegrid");
        TabStops(idle).ShouldBe(new Control[] { idle.NameButton!, idle.Pill! });
        var connected = rig.Row("chirp");
        TabStops(connected).ShouldBe(new Control[] { connected.NameButton!, connected.ConfigureButton! });
        AutomationProperties.GetName(idle.Pill!).ShouldBe("Connect Pulsegrid");
        AutomationProperties.GetName(connected.ConfigureButton!).ShouldBe("Settings for Chirp");
        AutomationProperties.GetHelpText(idle.NameButton!).ShouldBe(rig.App("pulsegrid").Description);
    }

    [AvaloniaFact]
    public void Connect_spins_then_connects_and_moves_the_focus_to_the_settings_button()
    {
        var gate = new TaskCompletionSource<IntegrationConnectResult>();
        var requests = new List<IntegrationConnectRequest>();
        var rig = new Rig(configure: b => b.ConnectAction = (request, _) =>
        {
            requests.Add(request);
            return gate.Task;
        });
        var statuses = new List<(IntegrationStatus, IntegrationStatus)>();
        rig.Block.StatusChanged += (_, e) => statuses.Add((e.OldStatus, e.NewStatus));
        var row = rig.Row("pulsegrid");
        row.Pill!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.App("pulsegrid").Status.ShouldBe(IntegrationStatus.Connecting);
        row.Pill.Status.ShouldBe(IntegrationStatus.Connecting);
        AutomationProperties.GetName(row.Pill).ShouldBe("Connecting Pulsegrid");
        rig.Block.Announcer!.Announcement.ShouldBe("Linking Pulsegrid");
        // Busy: a second press does nothing.
        rig.Key(PhysicalKey.Enter);
        requests.Count.ShouldBe(1);

        gate.SetResult(IntegrationConnectResult.Connected());
        rig.Pump();
        var app = rig.App("pulsegrid");
        app.Status.ShouldBe(IntegrationStatus.Connected);
        app.IsConnectedByCurrentUser.ShouldBeTrue();
        app.SyncFrequency.ShouldBe("realtime");
        rig.Block.ConnectedCount.ShouldBe(3);
        rig.Block.FilterBar!.Pills[1].Count.ShouldBe(3);
        row.ConfigureHost!.IsShown.ShouldBeTrue();
        row.Pill.Focusable.ShouldBeFalse();
        rig.Focused.ShouldBe(row.ConfigureButton);
        rig.Block.Announcer.Announcement.ShouldBe("Pulsegrid linked");
        statuses.ShouldBe([(IntegrationStatus.Idle, IntegrationStatus.Connecting), (IntegrationStatus.Connecting, IntegrationStatus.Connected)]);
        // Connecting does not reorder.
        rig.VisibleKeys[2].ShouldBe("pulsegrid");
    }

    [AvaloniaFact]
    public void A_refused_connect_shows_retry_and_the_retry_goes_through()
    {
        var rig = new Rig(configure: b => b.ConnectAction = (request, _) => Task.FromResult(request.Attempt == 1
            ? IntegrationConnectResult.Failed("Ledgerline did not grant access.")
            : IntegrationConnectResult.Connected("Ada Brook", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero))));
        var row = rig.Row("ledgerline");
        row.Pill!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        var app = rig.App("ledgerline");
        app.Status.ShouldBe(IntegrationStatus.Error);
        app.ErrorText.ShouldBe("Ledgerline did not grant access.");
        row.Pill.Status.ShouldBe(IntegrationStatus.Error);
        AutomationProperties.GetName(row.Pill).ShouldBe("Retry connecting Ledgerline");
        rig.Focused.ShouldBe(row.Pill);
        rig.Block.Announcer!.Announcement.ShouldBe("Ledgerline could not be linked");

        rig.Block.OpenConfiguration(app);
        rig.Pump();
        rig.Block.Sheet.MetaText.ShouldBe("Ledgerline did not grant access.");

        rig.Block.ConnectAsync(app).Result.ShouldBeTrue();
        rig.Pump();
        app.Status.ShouldBe(IntegrationStatus.Connected);
        app.ErrorText.ShouldBeNull();
        app.ConnectedBy.ShouldBe("Ada Brook");
        rig.Block.Sheet.MetaText.ShouldStartWith("Linked by Ada Brook");
    }

    [AvaloniaFact]
    public void A_throwing_connect_fails_with_the_formatted_error()
    {
        var rig = new Rig(configure: b =>
        {
            b.ConnectAction = (_, _) => throw new InvalidOperationException("boom");
            b.ErrorFormatter = e => $"Could not reach the service ({e.Message}).";
        });
        Exception? seen = null;
        rig.Block.StatusChanged += (_, e) => seen ??= e.Exception;
        rig.Block.ConnectAsync(rig.App("boxyard")).Result.ShouldBeFalse();
        rig.Pump();
        rig.App("boxyard").Status.ShouldBe(IntegrationStatus.Error);
        rig.App("boxyard").ErrorText.ShouldBe("Could not reach the service (boom).");
        seen.ShouldBeOfType<InvalidOperationException>();
    }

    [AvaloniaFact]
    public void Cancelling_a_connect_returns_to_idle_and_a_cancelled_request_never_starts()
    {
        var rig = new Rig(configure: b => b.ConnectAction = async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return IntegrationConnectResult.Connected();
        });
        var app = rig.App("tallymark");
        var task = rig.Block.ConnectAsync(app);
        app.Status.ShouldBe(IntegrationStatus.Connecting);
        rig.Block.CancelConnect(app);
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();
        task.Result.ShouldBeFalse();
        app.Status.ShouldBe(IntegrationStatus.Idle);

        rig.Block.ConnectRequested += (_, e) => e.Cancel = true;
        rig.Block.ConnectAsync(app).Result.ShouldBeFalse();
        app.Status.ShouldBe(IntegrationStatus.Idle);
    }

    [AvaloniaFact]
    public void The_drawer_shows_who_connected_the_app_and_the_permissions()
    {
        var rig = new Rig();
        IntegrationItem? opened = null;
        rig.Block.ConfigurationOpened += (_, e) => opened = e.Item;
        var chirp = rig.App("chirp");
        rig.Row("chirp").NameButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        rig.Pump();
        opened.ShouldBe(chirp);
        rig.Block.OpenItem.ShouldBe(chirp);
        rig.Block.DrawerPanel!.Title.ShouldBe("Chirp");
        var sheet = rig.Block.Sheet;
        sheet.MetaText.ShouldBe("Linked by Lena Ortiz · Mar 12");
        sheet.HasPerson.ShouldBeTrue();
        sheet.LeadText.ShouldBe("Chirp can");
        sheet.Settings!.IsExpanded.ShouldBeTrue();
        sheet.Frequency!.SelectedIndex.ShouldBe(0);
        sheet.GetVisualDescendants().OfType<IntegrationScopeRow>().Select(r => r.Text)
            .ShouldBe(chirp.Scopes.Select(s => s.Text));

        rig.Block.OpenConfiguration(rig.App("pulsegrid"));
        rig.Pump();
        sheet.MetaText.ShouldBe("Not linked yet");
        sheet.LeadText.ShouldBe("Pulsegrid will be able to");
        sheet.Settings.IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Connecting_in_the_drawer_opens_the_settings_and_focuses_the_frequency()
    {
        var rig = new Rig();
        var app = rig.App("huddle");
        rig.Block.OpenConfiguration(app);
        rig.Pump();
        var sheet = rig.Block.Sheet;
        sheet.Pill!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Pump();
        app.Status.ShouldBe(IntegrationStatus.Connected);
        sheet.MetaText.ShouldBe("Linked by you just now");
        sheet.HasPerson.ShouldBeFalse();
        sheet.LeadText.ShouldBe("Huddle Video can");
        sheet.Settings!.IsExpanded.ShouldBeTrue();
        sheet.Frequency!.IsKeyboardFocusWithin.ShouldBeTrue();
        rig.Row("huddle").Pill!.Status.ShouldBe(IntegrationStatus.Connected);
    }

    [AvaloniaFact]
    public void Changing_the_frequency_saves_it_and_announces()
    {
        var rig = new Rig();
        var app = rig.App("vaultline");
        IntegrationSyncFrequencyChangedEventArgs? args = null;
        rig.Block.SyncFrequencyChanged += (_, e) => args = e;
        rig.Block.OpenConfiguration(app);
        rig.Pump();
        var select = rig.Block.Sheet.Frequency!;
        select.SelectedIndex.ShouldBe(1);
        select.SelectedIndex = 3;
        rig.Pump();
        app.SyncFrequency.ShouldBe("nightly");
        args!.OldKey.ShouldBe("30m");
        args.NewKey.ShouldBe("nightly");
        select.Description.ShouldBe("Runs every night at 03:00 local time.");
        rig.Block.Announcer!.Announcement.ShouldBe("Vaultline now syncs nightly");
    }

    [AvaloniaFact]
    public void Holding_to_disconnect_returns_to_idle_and_focuses_the_drawer_pill()
    {
        var rig = new Rig();
        var app = rig.App("chirp");
        var commands = new List<object?>();
        rig.Block.DisconnectCommand = new RelayCommand(p => commands.Add(p));
        rig.Block.OpenConfiguration(app);
        rig.Pump();
        var sheet = rig.Block.Sheet;
        sheet.Hold!.Focus(NavigationMethod.Tab);
        sheet.Hold.RaiseEvent(new RoutedEventArgs(HoldToConfirm.ConfirmedEvent));
        rig.Pump();
        app.Status.ShouldBe(IntegrationStatus.Idle);
        app.ConnectedBy.ShouldBeNull();
        commands.ShouldBe([app]);
        rig.Block.ConnectedCount.ShouldBe(1);
        sheet.MetaText.ShouldBe("Not linked yet");
        sheet.Settings!.IsExpanded.ShouldBeFalse();
        rig.Focused.ShouldBe(sheet.Pill);
        rig.Block.Announcer!.Announcement.ShouldBe("Chirp unlinked");
        rig.Row("chirp").ConfigureHost!.IsShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_failed_disconnect_keeps_the_app_connected()
    {
        var rig = new Rig(configure: b => b.DisconnectAction = (_, _) => throw new InvalidOperationException("offline"));
        var app = rig.App("vaultline");
        rig.Block.DisconnectAsync(app).Result.ShouldBeFalse();
        app.Status.ShouldBe(IntegrationStatus.Connected);
        rig.Block.Announcer!.Announcement.ShouldBe("Vaultline is still linked");
    }

    [AvaloniaFact]
    public void Closing_the_drawer_after_its_row_left_moves_the_focus_to_the_heading()
    {
        var rig = new Rig();
        rig.Block.SelectedFilter = Integrations.ConnectedFilter;
        rig.Pump();
        var row = rig.Row("chirp");
        row.NameButton!.Focus(NavigationMethod.Tab);
        row.NameButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        rig.Pump();
        rig.Block.Sheet.Hold!.RaiseEvent(new RoutedEventArgs(HoldToConfirm.ConfirmedEvent));
        rig.Pump();
        rig.VisibleKeys.ShouldBe(["vaultline"]);
        rig.Block.CloseConfiguration();
        TestHelpers.PumpUntil(() => rig.Block.OpenItem is null).ShouldBeTrue();
        rig.Pump();
        rig.Focused.ShouldBe(rig.Block.TitleBlock);
    }

    [AvaloniaFact]
    public void A_focused_row_that_leaves_hands_the_focus_to_the_next_row()
    {
        var rig = new Rig();
        rig.Block.SelectedFilter = Integrations.ConnectedFilter;
        rig.Pump();
        rig.Row("chirp").ConfigureButton!.Focus(NavigationMethod.Tab);
        rig.App("chirp").Status = IntegrationStatus.Idle;
        rig.Pump();
        rig.VisibleKeys.ShouldBe(["vaultline"]);
        rig.Focused.ShouldBe(rig.Row("vaultline").NameButton);
    }

    [AvaloniaFact]
    public void Compact_width_stacks_rows_and_hides_the_settings_button()
    {
        var rig = new Rig(width: 400);
        rig.Block.IsCompact.ShouldBeTrue();
        rig.Block.Classes.Contains(":narrow").ShouldBeTrue();
        rig.Block.FilterBar!.IsCompact.ShouldBeTrue();
        var row = rig.Row("chirp");
        row.IsCompact.ShouldBeTrue();
        row.ConfigureHost!.IsVisible.ShouldBeFalse();
        // The name takes the focus after a connect when the settings button is hidden.
        var gate = new TaskCompletionSource<IntegrationConnectResult>();
        rig.Block.ConnectAction = (_, _) => gate.Task;
        var idle = rig.Row("pulsegrid");
        idle.Pill!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        gate.SetResult(IntegrationConnectResult.Connected());
        rig.Pump();
        rig.Focused.ShouldBe(idle.NameButton);
    }

    [AvaloniaFact]
    public void Items_source_wins_and_is_observed()
    {
        var source = new AvaloniaList<IntegrationItem>(IntegrationsDemoData.Apps().Take(3));
        var rig = new Rig(configure: b => b.ItemsSource = source);
        rig.Block.VisibleItems.Count.ShouldBe(3);
        source.Add(new IntegrationItem { Key = "new", Name = "Newcomer", Category = "files" });
        rig.Pump();
        rig.Block.VisibleItems.Select(i => i.Key).ShouldBe(["chirp", "vaultline", "pulsegrid", "new"]);
        rig.Block.FilterBar!.Pills[0].Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public void Without_the_connected_filter_only_categories_remain()
    {
        var rig = new Rig(configure: b => b.ShowConnectedFilter = false);
        rig.Block.FilterBar!.Pills.Select(p => p.Key).ShouldBe(["all", "messaging", "files", "insights"]);
        rig.Block.SelectedFilter = Integrations.ConnectedFilter;
        rig.Pump();
        rig.VisibleKeys.Count.ShouldBe(9);
    }

    [AvaloniaFact]
    public void The_simulated_connect_waits_then_connects()
    {
        var rig = new Rig();
        var app = rig.App("coldstore");
        rig.Block.SimulatedConnectDuration = TimeSpan.FromMilliseconds(30);
        var task = rig.Block.ConnectAsync(app);
        app.Status.ShouldBe(IntegrationStatus.Connecting);
        TestHelpers.PumpUntil(() => task.IsCompleted).ShouldBeTrue();
        task.Result.ShouldBeTrue();
        app.Status.ShouldBe(IntegrationStatus.Connected);
    }

    [AvaloniaFact]
    public void Full_motion_glides_the_filter_highlight_and_reveals_settings()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var rig = new Rig(level: ArcMotionLevel.Full);
        var bar = rig.Block.FilterBar!;
        var start = bar.Highlight!.CurrentRect;
        start.Width.ShouldBeGreaterThan(0);
        bar.Pills[4].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        rig.Pump();
        clock.Advance(TimeSpan.FromMilliseconds(50));
        rig.Pump();
        var mid = bar.Highlight.CurrentRect;
        mid.X.ShouldBeGreaterThan(start.X);
        mid.X.ShouldBeLessThan(bar.Pills[4].Bounds.X);
        for (var i = 0; i < 90; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            rig.Window.UpdateLayout();
        }
        rig.Pump();
        bar.Highlight.CurrentRect.X.ShouldBe(bar.Pills[4].Bounds.X, 0.5);
    }

    private sealed class RelayCommand(Action<object?> execute) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
