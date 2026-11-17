using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ConnectorSetup: the sample card, the connect steps driven by a handler (redirect, authorize, sync with its count and
/// hairline, connected), failure with Retry, Cancel, the access details with the inline disconnect confirmation, search,
/// the scripted run, announcements, labels and the pure helpers.
/// </summary>
public class ConnectorSetupTests
{
    private static readonly Func<TimeSpan, CancellationToken, Task> Immediate = (_, token) =>
    {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    };

    private static (ConnectorSetup Card, Window Window) Open(Action<ConnectorSetup>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var card = new ConnectorSetup
        {
            Simulation = ConnectorSetupTestData.Simulation(), AutoPlay = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        card.Delay = Immediate;
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = 520;
        window.Height = 1200;
        Pump();
        return (card, window);
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

    private static void Click(Control? control)
    {
        control.ShouldNotBeNull();
        var button = control as Avalonia.Controls.Button ?? control.GetLogicalDescendantsOf<Avalonia.Controls.Button>().First();
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
    }

    private static ConnectorApp App(string id, string name) => new(id, name, name + " description", ConnectorMarkKind.Chat)
    {
        SyncCount = 1240,
        SyncNoun = "messages",
        Scopes = ["Read messages", "Post replies as Atlas"],
        Keywords = ["team"],
    };

    /// <summary>A connect handler whose steps the test releases one by one.</summary>
    private sealed class SteppedHandler
    {
        public readonly TaskCompletionSource Authorize = new();
        public readonly TaskCompletionSource Sync = new();
        public readonly TaskCompletionSource Finish = new();
        public CancellationToken Token;
        public Exception? Failure;
        public int Calls;

        public async Task<ConnectorConnectResult?> Run(ConnectorApp app, ConnectorConnectContext context)
        {
            Calls++;
            Token = context.CancellationToken;
            await Authorize.Task.WaitAsync(Token);
            context.ReportAuthorizing();
            await Sync.Task.WaitAsync(Token);
            if (Failure is not null)
            {
                throw Failure;
            }
            context.ReportSyncing(120, 1240);
            await Finish.Task.WaitAsync(Token);
            return new ConnectorConnectResult { Account = "maya@tailspin.dev", Synced = 1240 };
        }
    }

    [AvaloniaFact]
    public void Opens_On_The_Simulated_Apps()
    {
        var (card, window) = Open();
        try
        {
            card.ShowsSimulation.ShouldBeTrue();
            card.IsSimulated.ShouldBeTrue();
            card.Rows.Count.ShouldBe(8);
            card.GetPhase("code").ShouldBe(ConnectorPhase.Connected);
            card.GetPhase("drive").ShouldBe(ConnectorPhase.Connected);
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Idle);
            card.Find("chat")!.StatusText.ShouldBe("Channels, threads and direct messages");
            card.Find("code")!.StatusText.ShouldBe("tailspin-toys · 86 repositories synced");
            ((Avalonia.Controls.Button)card.Find("chat")!.ActionControl!).Content.ShouldBe("Connect");
            card.Find("code")!.ActionControl.ShouldBeOfType<ConnectorSetupConnectedButton>();
            AutomationProperties.GetName(card).ShouldBe("Connect your tools");
            TestHelpers.Descendant<TextBlock>(card).Text.ShouldBe("Connect your tools");
            card.FooterContent!.Content.ShouldBe("Atlas only sees what your account can see. Disconnect an app at any time.");
            card.SearchBox!.Placeholder.ShouldBe("Search apps");
            card.EffectiveAutoPlay.ShouldBeFalse();
            new ConnectorSetup { Simulation = ConnectorSetupTestData.Simulation() }.EffectiveAutoPlay.ShouldBeTrue();
            new ConnectorSetup().EffectiveAutoPlay.ShouldBeFalse();
            new ConnectorSetup
            {
                Simulation = ConnectorSetupTestData.Simulation(), ConnectHandler = (_, _) => Task.FromResult<ConnectorConnectResult?>(null)
            }.EffectiveAutoPlay.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Are_60px_In_Every_Phase()
    {
        var handler = new SteppedHandler();
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.ConnectHandler = handler.Run;
        });
        try
        {
            var row = card.Find("chat")!;
            row.Bounds.Height.ShouldBe(60);
            card.StartConnect("chat");
            Pump();
            row.Bounds.Height.ShouldBe(60);
            handler.Authorize.SetResult();
            Pump();
            row.Bounds.Height.ShouldBe(60);
            handler.Sync.SetResult();
            Pump();
            row.Bounds.Height.ShouldBe(60);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Connect_Runs_Through_The_Reported_Steps()
    {
        var handler = new SteppedHandler();
        ConnectorConnectedEventArgs? connected = null;
        IReadOnlyList<string>? ids = null;
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat"), App("mail", "Mail")];
            c.ConnectedIds = ["mail"];
            c.ConnectHandler = handler.Run;
        });
        try
        {
            card.AppConnected += (_, e) => connected = e;
            card.ConnectedIdsChanged += (_, e) => ids = e.Ids;
            var row = card.Find("chat")!;
            card.GetPhase("mail").ShouldBe(ConnectorPhase.Connected);

            Click(row.ActionControl);
            handler.Calls.ShouldBe(1);
            row.Phase.ShouldBe(ConnectorPhase.Redirecting);
            row.StatusText.ShouldBe("Opening Chat…");
            ((Avalonia.Controls.Button)row.ActionControl!).Content.ShouldBe("Cancel");
            row.Hairline!.Target.ShouldBe(0.12, 0.001);
            card.Announcer!.Announcement.ShouldBe("Connecting Chat.");

            handler.Authorize.SetResult();
            Pump();
            row.Phase.ShouldBe(ConnectorPhase.Authorizing);
            row.StatusText.ShouldBe("Waiting for you to approve in Chat…");
            row.Hairline.Target.ShouldBe(0.3, 0.001);

            handler.Sync.SetResult();
            Pump();
            row.Phase.ShouldBe(ConnectorPhase.Syncing);
            row.Synced.ShouldBe(120);
            row.StatusText.ShouldBe("Syncing 120 messages");
            row.Hairline.Target.ShouldBe(ConnectorSetupRow.SyncShare(120, 1240), 0.001);

            handler.Finish.SetResult();
            Pump();
            row.Phase.ShouldBe(ConnectorPhase.Connected);
            row.StatusText.ShouldBe("maya@tailspin.dev · 1,240 messages synced");
            row.ActionControl.ShouldBeOfType<ConnectorSetupConnectedButton>();
            connected.ShouldNotBeNull();
            connected.App.Id.ShouldBe("chat");
            connected.Result.Account.ShouldBe("maya@tailspin.dev");
            connected.Result.Synced.ShouldBe(1240);
            connected.Result.Scopes.ShouldBe(["Read messages", "Post replies as Atlas"]);
            ids.ShouldBe(["chat", "mail"]);
            card.ConnectedIds.ShouldBe(["chat", "mail"]);
            card.Announcer.Announcement.ShouldBe("Chat connected, maya@tailspin.dev · 1,240 messages synced.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failure_Shows_The_Reason_With_Retry()
    {
        var handler = new SteppedHandler { Failure = new InvalidOperationException("Needs admin approval") };
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("crm", "CRM")];
            c.ConnectHandler = handler.Run;
        });
        try
        {
            var row = card.Find("crm")!;
            card.StartConnect("crm");
            handler.Authorize.SetResult();
            handler.Sync.SetResult();
            Pump();
            row.Phase.ShouldBe(ConnectorPhase.Failed);
            row.Error.ShouldBe("Needs admin approval");
            row.StatusText.ShouldBe("Needs admin approval");
            ((Avalonia.Controls.Button)row.ActionControl!).Content.ShouldBe("Retry");
            AutomationProperties.GetName(row.ActionControl).ShouldBe("Retry connecting CRM");
            card.Announcer!.Announcement.ShouldBe("CRM failed: Needs admin approval");
            // An alert glyph pairs with the message: status is never color alone.
            TestHelpers.Descendant<ConnectorSetupRow>(card).GetVisualDescendantsGlyphs().ShouldContain(ArcIcons.CircleAlert);

            // Retry runs the handler again (its gates are already open, so it reaches the sync).
            handler.Failure = null;
            Click(row.ActionControl);
            handler.Calls.ShouldBe(2);
            row.Phase.ShouldBe(ConnectorPhase.Syncing);
            row.Error.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_Cancels_The_Token_And_Returns_To_Rest()
    {
        var handler = new SteppedHandler();
        var raised = false;
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.ConnectHandler = handler.Run;
        });
        try
        {
            card.AppConnected += (_, _) => raised = true;
            var row = card.Find("chat")!;
            card.StartConnect("chat");
            Pump();
            Click(row.ActionControl);
            handler.Token.IsCancellationRequested.ShouldBeTrue();
            row.Phase.ShouldBe(ConnectorPhase.Idle);
            row.StatusText.ShouldBe("Chat description");
            ((Avalonia.Controls.Button)row.ActionControl!).Content.ShouldBe("Connect");
            card.Announcer!.Announcement.ShouldBe("Cancelled connecting Chat.");
            handler.Authorize.TrySetResult();
            Pump();
            row.Phase.ShouldBe(ConnectorPhase.Idle);
            raised.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Card_Aborts_A_Running_Connect()
    {
        var handler = new SteppedHandler();
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.ConnectHandler = handler.Run;
        });
        card.StartConnect("chat");
        Pump();
        window.Content = null;
        Pump();
        handler.Token.IsCancellationRequested.ShouldBeTrue();
        card.GetPhase("chat").ShouldBe(ConnectorPhase.Idle);
        window.Close();
    }

    [AvaloniaFact]
    public void Connected_Opens_The_Scopes_And_Disconnect_Asks_First()
    {
        ConnectorApp? revoked = null;
        ConnectorApp? disconnected = null;
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.ConnectedIds = ["chat"];
            c.AssistantName = "Atlas";
            c.DisconnectHandler = (app, _) =>
            {
                revoked = app;
                return Task.CompletedTask;
            };
        });
        try
        {
            card.AppDisconnected += (_, e) => disconnected = e.App;
            var row = card.Find("chat")!;
            var connected = (ConnectorSetupConnectedButton)row.ActionControl!;
            AutomationProperties.GetName(connected).ShouldBe("Connected. Show access for Chat");
            Click(connected);
            row.IsExpanded.ShouldBeTrue();
            connected.IsExpanded.ShouldBeTrue();
            AutomationProperties.GetName(connected).ShouldBe("Connected. Hide access for Chat");
            row.Details!.IsMounted.ShouldBeTrue();
            row.ScopesHeading!.Text.ShouldBe("Atlas can");
            row.ScopesPanel!.Children.Count.ShouldBe(2);

            Click(row.ConfirmControl);
            row.IsConfirming.ShouldBeTrue();
            var question = row.ConfirmControl!.GetLogicalDescendantsOf<TextBlock>().First();
            question.Text.ShouldBe("Disconnect Chat? Atlas loses access.");

            // Escape backs out of the question.
            var cancel = row.ConfirmControl!.GetLogicalDescendantsOf<Avalonia.Controls.Button>().First(b => b.Classes.Contains("connector-cancel"));
            cancel.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            row.IsConfirming.ShouldBeFalse();
            revoked.ShouldBeNull();

            Click(row.ConfirmControl);
            var confirm = row.ConfirmControl!.GetLogicalDescendantsOf<Avalonia.Controls.Button>().First(b => b.Classes.Contains("connector-confirm"));
            Click(confirm);
            revoked!.Id.ShouldBe("chat");
            disconnected!.Id.ShouldBe("chat");
            row.Phase.ShouldBe(ConnectorPhase.Idle);
            row.IsExpanded.ShouldBeFalse();
            card.ConnectedIds.ShouldBeEmpty();
            card.Announcer!.Announcement.ShouldBe("Chat disconnected.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Keyboard_Disconnect_Moves_Focus_To_Cancel_Not_To_The_Destructive_Button()
    {
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.ConnectedIds = ["chat"];
        });
        try
        {
            card.SetAccessOpen("chat", true);
            Pump();
            card.IsAccessOpen("chat").ShouldBeTrue();
            var row = card.Find("chat")!;
            var disconnect = (Avalonia.Controls.Button)row.ConfirmControl!;
            disconnect.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            row.IsConfirming.ShouldBeTrue();
            var focused = TopLevel.GetTopLevel(card)!.FocusManager!.GetFocusedElement() as Avalonia.Controls.Button;
            focused.ShouldNotBeNull();
            focused.Classes.ShouldContain("connector-cancel");

            // Escape returns to the Disconnect button.
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            row.IsConfirming.ShouldBeFalse();
            (TopLevel.GetTopLevel(card)!.FocusManager!.GetFocusedElement() as Avalonia.Controls.Button)!.Classes.ShouldContain("connector-disconnect");

            card.SetAccessOpen("chat", false);
            card.IsAccessOpen("chat").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Filters_By_Name_Description_Account_And_Keywords()
    {
        var (card, window) = Open();
        try
        {
            string[] Visible() => card.Rows.Where(r => r.IsVisible).Select(r => r.App.Id).ToArray();
            card.SearchText = "mail";
            Visible().ShouldBe(["mail"]);
            card.SearchText = "tickets";
            Visible().ShouldBe(["issues"]);
            card.SearchText = "tailspin-toys";
            Visible().ShouldBe(["code"]);
            card.SearchText = "FILES";
            Visible().ShouldContain("drive");
            AutomationProperties.GetSizeOfSet(card.Rows.First(r => r.IsVisible)).ShouldBe(Visible().Length);
            card.SearchText = "zzz";
            Visible().ShouldBeEmpty();
            card.EmptyText!.IsVisible.ShouldBeTrue();
            card.EmptyText.Text.ShouldBe("No apps match “zzz”");

            // Typing in the field writes SearchText; Escape clears it.
            card.SearchBox!.Text = "cal";
            card.SearchText.ShouldBe("cal");
            Visible().ShouldBe(["calendar"]);
            card.SearchBox.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            card.SearchText.ShouldBe(string.Empty);
            Visible().Length.ShouldBe(8);
            card.EmptyText.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Connects_And_Says_Nothing_Was_Sent()
    {
        var (card, window) = Open();
        try
        {
            card.StartConnect("chat");
            Pump();
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Connected);
            card.Find("chat")!.StatusText.ShouldBe("tailspin.chat · 1,240 messages synced");
            card.Announcer!.Announcement.ShouldBe("Chat connected, tailspin.chat · 1,240 messages synced. Simulated, nothing was sent.");
            card.ConnectedIds.ShouldBe(["chat", "code", "drive"]);

            card.Disconnect("code");
            Pump();
            card.GetPhase("code").ShouldBe(ConnectorPhase.Idle);
            card.ConnectedIds.ShouldBe(["chat", "drive"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Scripted_Run_Connects_Chat_Then_Retries_The_CRM()
    {
        var (card, window) = Open();
        try
        {
            await card.PlayAsync(CancellationToken.None);
            Pump();
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Connected);
            card.GetPhase("crm").ShouldBe(ConnectorPhase.Connected);
            card.Announcer!.History.ShouldContain("CRM failed: Needs admin approval Simulated, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_No_Apps_And_No_Simulation_It_Lists_Nothing_And_Names_No_Assistant()
    {
        var (card, window) = Open(c => c.Simulation = null);
        try
        {
            card.ShowsSimulation.ShouldBeFalse();
            card.Rows.Count.ShouldBe(0);
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.FooterContent!.Content.ShouldBe("Your assistant only sees what your account can see. Disconnect an app at any time.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Own_Connectors_Win_Over_The_Simulation()
    {
        var (card, window) = Open(c => c.Connectors = [App("one", "One")]);
        try
        {
            card.ShowsSimulation.ShouldBeFalse();
            card.Rows.Select(r => r.App.Id).ShouldBe(["one"]);
            card.GetPhase("one").ShouldBe(ConnectorPhase.Idle);
            card.EffectiveAssistantName.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_Its_Run_And_Setting_It_To_Null_Stops_It()
    {
        var gate = new TaskCompletionSource();
        var card = new ConnectorSetup { Simulation = ConnectorSetupTestData.Simulation(), Delay = (_, token) => gate.Task.WaitAsync(token) };
        var window = TestHelpers.Show(card);
        try
        {
            window.Width = 520;
            window.Height = 1200;
            TestHelpers.PumpUntil(() => card.IsAutoPlaying, 3000).ShouldBeTrue();
            card.Simulation = null;
            Pump();
            card.IsAutoPlaying.ShouldBeFalse();
            card.Rows.Count.ShouldBe(0);
            gate.SetResult();
            Pump();
            card.IsAutoPlaying.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Scripted_Run_Stops_At_The_First_Press()
    {
        var gate = new TaskCompletionSource();
        var card = new ConnectorSetup { Simulation = ConnectorSetupTestData.Simulation(), Delay = (_, token) => gate.Task.WaitAsync(token) };
        var window = TestHelpers.Show(card);
        try
        {
            window.Width = 520;
            window.Height = 1200;
            TestHelpers.PumpUntil(() => card.IsAutoPlaying, 3000).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
            Pump();
            card.IsAutoPlaying.ShouldBeFalse();
            gate.SetResult();
            Pump();
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_App_Can_Set_Connected_Ids()
    {
        var (card, window) = Open(c => c.Connectors = [App("chat", "Chat"), App("mail", "Mail")]);
        try
        {
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Idle);
            card.ConnectedIds = ["chat"];
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Connected);
            card.ConnectedIds = [];
            card.GetPhase("chat").ShouldBe(ConnectorPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_And_Texts_Can_Be_Replaced()
    {
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.AssistantName = "Juno";
            c.Labels = new ConnectorSetupLabels { Connect = "Verbinden", Title = "Werkzeuge" };
            c.SearchPlaceholder = "Find";
            c.IsSearchable = false;
            c.IsFooterVisible = false;
        });
        try
        {
            ((Avalonia.Controls.Button)card.Find("chat")!.ActionControl!).Content.ShouldBe("Verbinden");
            AutomationProperties.GetName(card).ShouldBe("Werkzeuge");
            card.SearchBox!.Placeholder.ShouldBe("Find");
            card.SearchBox.IsVisible.ShouldBeFalse();
            card.FooterContent!.Content.ShouldBe("Juno only sees what your account can see. Disconnect an app at any time.");
            card.Title = "Mine";
            AutomationProperties.GetName(card).ShouldBe("Mine");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Sync_Share_Leaves_The_Handshake_Its_Part()
    {
        ConnectorSetupRow.SyncShare(0, 100).ShouldBe(0.4, 0.0001);
        ConnectorSetupRow.SyncShare(50, 100).ShouldBe(0.7, 0.0001);
        ConnectorSetupRow.SyncShare(200, 100).ShouldBe(1, 0.0001);
        var unknown = ConnectorSetupRow.SyncShare(400, null);
        unknown.ShouldBeGreaterThan(0.4);
        unknown.ShouldBeLessThan(1);
    }

    [AvaloniaFact]
    public void Marks_Use_Neutral_Glyphs_Or_A_Letter()
    {
        ConnectorMark.GlyphOf(ConnectorMarkKind.Mail).ShouldBe(ArcIcons.Mail);
        ConnectorMark.GlyphOf(ConnectorMarkKind.None).ShouldBeNull();
        var mark = new ConnectorMark { Monogram = "wingtip" };
        var window = TestHelpers.Show(mark);
        try
        {
            mark.EffectiveGlyph.ShouldBeNull();
            mark.MonogramText.ShouldBe("W");
            mark.Bounds.Width.ShouldBe(36);
            mark.Kind = ConnectorMarkKind.Code;
            mark.EffectiveGlyph.ShouldBe(ArcIcons.GitBranch);
            mark.Glyph = ArcIcons.Cloud;
            mark.EffectiveGlyph.ShouldBe(ArcIcons.Cloud);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Details_Spring_Open_With_Full_Motion()
    {
        var (card, window) = Open(c =>
        {
            c.Connectors = [App("chat", "Chat")];
            c.ConnectedIds = ["chat"];
        }, ArcMotionLevel.Full);
        try
        {
            var row = card.Find("chat")!;
            Click(row.ActionControl);
            row.Details!.IsMounted.ShouldBeTrue();
            TestHelpers.PumpUntil(() => !row.Details.IsMoving, 3000).ShouldBeTrue();
            row.Details.DisplayedHeight.ShouldBeGreaterThan(40);
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class ConnectorSetupTestExtensions
{
    /// <summary>Every glyph drawn under <paramref name="root"/>.</summary>
    public static IEnumerable<Avalonia.Media.Geometry?> GetVisualDescendantsGlyphs(this Visual root) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<ArcGlyph>().Select(g => g.Data);
}

/// <summary>
/// The invented fixture these tests play: a copy of the gallery's ConnectorSetup demo data with invented model names, so
/// the tests do not depend on the gallery's content.
/// </summary>
internal static class ConnectorSetupTestData
{
    /// <summary>The assistant named in the texts (an invented one).</summary>
    public const string AssistantName = "Atlas";

    /// <summary>The ids connected when the card first shows.</summary>
    public static IReadOnlyList<string> Connected { get; } = ["code", "drive"];

    /// <summary>The scripted run: connect Chat; then a CRM attempt that needs admin approval; then its retry.</summary>
    public static IList<ConnectorSetupSimulationStep> Steps { get; } =
    [
        new("chat", TimeSpan.FromMilliseconds(900)),
        new("crm", TimeSpan.FromMilliseconds(1200), "Needs admin approval"),
        new("crm", TimeSpan.FromMilliseconds(1800)),
    ];

    /// <summary>The eight apps.</summary>
    public static IReadOnlyList<ConnectorApp> Apps { get; } =
    [
        new("chat", "Chat", "Channels, threads and direct messages", ConnectorMarkKind.Chat)
        {
            Host = "chat.example.com",
            Account = "tailspin.chat",
            SyncCount = 1240,
            SyncNoun = "messages",
            Scopes = ["Read messages in channels you belong to", "Search message history", "Post replies as Atlas"],
            Keywords = ["messages", "team", "channels"],
        },
        new("code", "Code", "Repositories, pull requests and reviews", ConnectorMarkKind.Code)
        {
            Host = "code.example.com",
            Account = "tailspin-toys",
            SyncCount = 86,
            SyncNoun = "repositories",
            Scopes = ["Read code and pull requests", "Comment on pull requests", "Open draft pull requests"],
            Keywords = ["git", "repos", "pull requests"],
        },
        new("docs", "Docs", "Pages, wikis and meeting notes", ConnectorMarkKind.Docs)
        {
            Host = "docs.example.com",
            Account = "Tailspin wiki",
            SyncCount = 412,
            SyncNoun = "pages",
            Scopes = ["Read pages shared with you", "Create pages in your private space"],
            Keywords = ["wiki", "notes", "pages"],
        },
        new("drive", "Drive", "Files and folders shared with you", ConnectorMarkKind.Drive)
        {
            Host = "drive.example.com",
            Account = "maya@tailspin.example",
            SyncCount = 2480,
            SyncNoun = "files",
            Scopes = ["Read files you can open", "Search file contents"],
            Keywords = ["files", "storage", "folders"],
        },
        new("issues", "Issues", "Issues, projects and cycles", ConnectorMarkKind.Issues)
        {
            Host = "issues.example.com",
            Account = "Tailspin engineering",
            SyncCount = 930,
            SyncNoun = "issues",
            Scopes = ["Read issues and projects", "Create and update issues", "Comment as Atlas"],
            Keywords = ["tickets", "bugs", "tracker"],
        },
        new("calendar", "Calendar", "Events, invites and availability", ConnectorMarkKind.Calendar)
        {
            Host = "calendar.example.com",
            Account = "maya@tailspin.example",
            SyncCount = 318,
            SyncNoun = "events",
            Scopes = ["See your events and free time", "Draft invites for you to send"],
            Keywords = ["meetings", "schedule", "events"],
        },
        new("mail", "Mail", "Inbox, threads and attachments", ConnectorMarkKind.Mail)
        {
            Host = "mail.example.com",
            Account = "maya@tailspin.example",
            SyncCount = 5600,
            SyncNoun = "emails",
            Scopes = ["Read and search your email", "Draft replies for you to send"],
            Keywords = ["email", "inbox"],
        },
        new("crm", "CRM", "Accounts, contacts and deals", ConnectorMarkKind.Crm)
        {
            Host = "crm.example.com",
            Account = "Tailspin sales",
            SyncCount = 1875,
            SyncNoun = "records",
            Scopes = ["Read accounts, contacts and deals", "Log activity on records"],
            Keywords = ["sales", "customers", "deals"],
        },
    ];

    /// <summary>A new simulation of the apps (tests and code use it; the XAML demos spell it out).</summary>
    public static ConnectorSetupSimulation Simulation() => new()
    {
        AssistantName = AssistantName,
        Apps = Apps,
        Connected = Connected,
        Steps = Steps,
    };
}
