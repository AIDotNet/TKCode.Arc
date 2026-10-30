using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// AiCommandPalette: the sample workspace, the word search with its Ask AI fallback, the keyboard model (arrows, Enter, Tab,
/// Escape), asking with a streaming responder (word pacing, sources, citations, actions, failure and retry, cancellation),
/// the fixed size, labels, the narrow footer, the walkthrough and the dialog.
/// </summary>
public class AiCommandPaletteTests
{
    private static (AiCommandPalette Palette, Window Window, AiCommandPaletteManualScheduler Timers) Open(
        Action<AiCommandPalette>? setup = null, double width = 640, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var timers = new AiCommandPaletteManualScheduler();
        var palette = new AiCommandPalette { Scheduler = timers, AutoPlay = false };
        setup?.Invoke(palette);
        var window = TestHelpers.Show(palette, level);
        window.Width = width;
        window.Height = 800;
        Pump();
        palette.FocusInput();
        Pump();
        return (palette, window, timers);
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

    private static void Advance(AiCommandPaletteManualScheduler timers, double seconds)
    {
        timers.Advance(TimeSpan.FromSeconds(seconds));
        Pump();
    }

    private static string[] Titles(AiCommandPaletteRowList? list) => list!.Rows.Select(r => r.Title ?? string.Empty).ToArray();

    private static string[] HintLabels(AiCommandPalette palette) =>
        ((StackPanel)palette.Hints!.Content!).Children.OfType<AiCommandPaletteHint>().Select(h => h.Label ?? string.Empty).ToArray();

    private static AiCommandPaletteItem[] TeamItems() =>
    [
        new("inbox", "Inbox", "Pages", ArcIcons.Inbox) { IsSuggested = true, Shortcut = "G I" },
        new("billing", "Billing", "Pages", ArcIcons.CreditCard, "Settings") { Keywords = "invoice payment" },
        new("invite", "Invite teammate", "Actions", ArcIcons.Mail) { IsSuggested = true },
        new("theme", "Switch theme", "Actions", ArcIcons.SunMoon),
    ];

    /// <summary>An invented workspace for the simulation: suggested pages and actions, three questions and two canned answers.</summary>
    private static AiCommandPaletteSimulation Simulation() => new()
    {
        Items =
        [
            new("inbox", "Inbox", "Pages", ArcIcons.Inbox, "12 unread") { IsSuggested = true, Shortcut = "G I" },
            new("issues", "My issues", "Pages", ArcIcons.CircleDot) { IsSuggested = true },
            new("billing", "Billing", "Pages", ArcIcons.CreditCard) { Keywords = "invoice payment" },
            new("invite", "Invite teammate", "Actions", ArcIcons.Mail) { IsSuggested = true },
        ],
        Questions = ["Why did the invoice for Globex fail?", "What shipped last week?", "Who owns search?"],
        SearchQuery = "inv",
        Answers =
        [
            new()
            {
                Keywords = "invoice globex fail",
                Sources =
                [
                    new AiCommandPaletteSource("card", "Payment history", "The card on file expired."),
                    new AiCommandPaletteSource("pr", "Pull request 12", "The retry hit a 502."),
                    new AiCommandPaletteSource("ticket", "Ticket 7", "Support wrote to the customer."),
                ],
                Text = "The card expired [1]. The retry hit a 502 during a deploy [2].\n\nSupport already wrote to the customer [3].",
                Actions =
                [
                    new("issue", "Create issue", "ENG", ArcIcons.CircleDot, "Created ENG-12"),
                    new("reply", "Draft reply", "Support", ArcIcons.Mail, "Draft saved"),
                ],
            },
            new()
            {
                Sources = [new AiCommandPaletteSource("search", "Workspace search")],
                Text = "Nothing in the workspace answers “{question}” [1].",
            },
        ],
    };

    /// <summary>A responder that streams the given pieces right away, sets two sources first and returns two actions.</summary>
    private static Func<AiCommandPaletteAskContext, Task<AiCommandPaletteAnswer?>> Streaming(string text, List<string>? asked = null,
        Func<CancellationToken, Task<string?>>? run = null) =>
        context =>
        {
            asked?.Add(context.Question);
            context.SetSources(
            [
                new AiCommandPaletteSource("a", "Doc A", "The first document"),
                new AiCommandPaletteSource("b", "Doc B"),
            ]);
            context.Append(text);
            return Task.FromResult<AiCommandPaletteAnswer?>(new AiCommandPaletteAnswer
            {
                Actions =
                [
                    new AiCommandPaletteAction("issue", "Create issue", "ENG", ArcIcons.CircleDot, run ?? (_ => Task.FromResult<string?>("Created ENG-1"))),
                    new AiCommandPaletteAction("open", "Open doc") { DoneLabel = "Opened" },
                ]
            });
        };

    [AvaloniaFact]
    public void With_No_Data_And_No_Simulation_It_Is_An_Empty_Palette()
    {
        var (palette, window, timers) = Open();
        Assert.False(palette.IsSample);
        Assert.Null(palette.ModelName);
        Assert.Empty(palette.EffectiveItems);
        Assert.Empty(palette.EffectiveQuestions);
        Assert.Empty(Titles(palette.SearchList));
        Press(window, PhysicalKey.Tab);
        Assert.Equal(AiCommandPalette.View.Questions, palette.CurrentView);
        Assert.Empty(palette.QuestionList!.Rows);
        // With no responder a question gets no answer, and nothing fails.
        palette.Query = "Anything?";
        Press(window, PhysicalKey.Enter);
        Advance(timers, 5);
        Assert.NotEqual(AiCommandPaletteAnswerState.Failed, palette.AnswerState);
        Assert.Empty(palette.Chips);
    }

    [AvaloniaFact]
    public void A_Simulation_Lists_Its_Suggested_Rows_With_The_First_One_Highlighted()
    {
        var (palette, _, _) = Open(p =>
        {
            p.Simulation = Simulation();
            p.ModelName = "Model X";
        });
        Assert.True(palette.IsSample);
        Assert.Equal(AiCommandPalette.View.Search, palette.CurrentView);
        var rows = Titles(palette.SearchList);
        Assert.Equal(["Inbox", "My issues", "Invite teammate"], rows);
        Assert.Equal(0, palette.SearchList!.ActiveIndex);
        Assert.True(palette.SearchList.Rows[0].IsActive);
        // The list's theme gives the shared highlight its fill and the row radius; it sits on the active row.
        Assert.NotNull(palette.SearchList.HighlightBrush);
        Assert.Equal(new CornerRadius(10), palette.SearchList.HighlightCornerRadius);
        Assert.Equal(palette.SearchList.Rows[0].Bounds, palette.SearchList.Highlight.CurrentRect);
        Assert.Equal(40, palette.SearchList.Rows[0].Bounds.Height);
        Assert.Equal("Model X", palette.ModelName);
        Assert.Equal(["Navigate", "Open", "Ask AI", "Close"], HintLabels(palette));
    }

    [AvaloniaFact]
    public void Typing_Filters_By_Word_Prefix_And_Ends_With_An_Ask_Ai_Row()
    {
        var (palette, _, _) = Open(p => p.Items.AddRange(TeamItems()));
        palette.Query = "inv";
        Pump();
        Assert.Equal(["Billing", "Invite teammate", "Ask AI about “inv”"], Titles(palette.SearchList));
        Assert.Equal(AiCommandPaletteRowKind.AskQuery, palette.SearchList!.Rows[^1].Kind);
        Assert.Equal(0, palette.SearchList.ActiveIndex);
        Assert.Equal(["Navigate", "Open", "Ask AI", "Clear"], HintLabels(palette));

        palette.Query = "zzz";
        Pump();
        Assert.Equal(["Ask AI about “zzz”"], Titles(palette.SearchList));
        Assert.Equal(0, palette.SearchList.ActiveIndex);
        Assert.Contains(palette.SearchList.GetVisualDescendants().OfType<AiCommandPaletteEmpty>(), e => e.Text!.Contains("zzz"));
    }

    [AvaloniaFact]
    public void Arrows_Wrap_And_Enter_Selects_The_Item_And_Runs_Its_Command()
    {
        var ran = new List<object?>();
        var items = TeamItems();
        items[2].Command = new RelayCommand(p => ran.Add(p));
        var (palette, window, _) = Open(p => p.Items.AddRange(items));
        AiCommandPaletteItem? selected = null;
        palette.ItemSelected += (_, e) => selected = e.Item;

        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal(1, palette.SearchList!.ActiveIndex);
        Press(window, PhysicalKey.ArrowDown);
        Assert.Equal(0, palette.SearchList.ActiveIndex);
        Press(window, PhysicalKey.ArrowDown);
        Assert.Equal("Invite teammate", palette.SearchList.ActiveRow!.Title);
        Press(window, PhysicalKey.Enter);
        Assert.Same(items[2], selected);
        Assert.Equal([items[2]], ran);
        Assert.True(palette.Input!.IsFocused);
    }

    [AvaloniaFact]
    public void Tab_On_An_Empty_Query_Opens_Suggested_Questions_And_Tab_Goes_Back()
    {
        var (palette, window, _) = Open(p =>
        {
            p.Items.AddRange(TeamItems());
            p.Simulation = Simulation();
        });
        var modes = new List<AiCommandPaletteMode>();
        palette.ModeChanged += (_, e) => modes.Add(e.Mode);

        Press(window, PhysicalKey.Tab);
        Assert.Equal(AiCommandPaletteMode.Ask, palette.Mode);
        Assert.Equal(AiCommandPalette.View.Questions, palette.CurrentView);
        Assert.Equal(3, palette.QuestionList!.Rows.Count);
        Assert.True(palette.AskModeButton!.IsChecked);
        Assert.False(palette.SearchModeButton!.IsChecked);
        Assert.Equal("Ask anything about your workspace", palette.Input!.PlaceholderText);
        Assert.True(palette.Input.IsFocused);

        Press(window, PhysicalKey.Tab);
        Assert.Equal(AiCommandPaletteMode.Search, palette.Mode);
        Assert.Equal(AiCommandPalette.View.Search, palette.CurrentView);
        Assert.Equal([AiCommandPaletteMode.Ask, AiCommandPaletteMode.Search], modes);
    }

    [AvaloniaFact]
    public void Tab_With_A_Query_Asks_The_Model_And_The_Answer_Streams_In_Batches_Of_Three_To_Seven_Words()
    {
        var asked = new List<string>();
        var text = "One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen [1] end.";
        var (palette, window, timers) = Open(p =>
        {
            p.Items.AddRange(TeamItems());
            p.Responder = Streaming(text, asked);
        });
        palette.Query = "what changed";
        Pump();
        Press(window, PhysicalKey.Tab);

        Assert.Equal(["what changed"], asked);
        Assert.Equal(AiCommandPaletteMode.Ask, palette.Mode);
        Assert.Equal(AiCommandPalette.View.Answer, palette.CurrentView);
        Assert.Equal(string.Empty, palette.Query);
        Assert.Equal("Asking: what changed", palette.Announcement);
        Assert.Equal(2, palette.Chips.Count);
        Assert.Empty(palette.AnswerText!.Children);

        var counts = new List<int>();
        var before = 0;
        while (palette.AnswerState != AiCommandPaletteAnswerState.Done)
        {
            Advance(timers, AiCommandPalette.RevealInterval.TotalSeconds);
            var now = palette.Session!.Shown.Count;
            if (now > before)
            {
                counts.Add(now - before);
            }
            before = now;
            Assert.True(counts.Count < 20);
        }
        Assert.All(counts.Take(counts.Count - 1), c => Assert.InRange(c, 3, 7));
        Assert.Equal(text.Split(' ').Length, palette.Session!.Shown.Count);
        Assert.Single(palette.Citations);
        Assert.Equal(1, palette.Citations[0].Number);
        Assert.Equal(["Create issue", "Open doc"], Titles(palette.ActionList));
        Assert.Equal(0, palette.ActionList!.ActiveIndex);
        Assert.StartsWith("One two three", palette.Announcement);
        Assert.DoesNotContain("[1]", palette.Announcement);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Reveals_The_Answer_In_Large_Steps_Without_Fades()
    {
        var (palette, _, timers) = Open(p => p.Responder = Streaming(string.Join(" ", Enumerable.Range(1, 60).Select(i => "w" + i))),
            level: ArcMotionLevel.Reduced);
        palette.Ask("q");
        Pump();
        Advance(timers, AiCommandPalette.ReducedRevealInterval.TotalSeconds);
        Assert.Equal(AiCommandPalette.ReducedRevealWords, palette.Session!.Shown.Count);
        Assert.All(palette.AnswerText!.Children, c => Assert.Equal(1, c.Opacity));
        Advance(timers, 1);
        Assert.Equal(60, palette.Session.Shown.Count);
        Assert.Equal(AiCommandPaletteAnswerState.Done, palette.AnswerState);
    }

    [AvaloniaFact]
    public void Enter_Runs_The_Highlighted_Action_And_Reports_Its_Result()
    {
        var gate = new TaskCompletionSource<string?>();
        var (palette, window, timers) = Open(p => p.Responder = Streaming("Short answer [2].", run: _ => gate.Task));
        palette.Ask("why");
        Advance(timers, 1);
        Assert.Equal(AiCommandPaletteAnswerState.Done, palette.AnswerState);
        AiCommandPaletteActionEventArgs? completed = null;
        palette.ActionCompleted += (_, e) => completed = e;

        Press(window, PhysicalKey.Enter);
        var row = palette.ActionList!.Rows[0];
        Assert.Equal(AiCommandPaletteActionState.Running, palette.Session!.StateOf(row));
        Assert.Equal("Create issue…", row.Title);

        gate.SetResult("Created ENG-1");
        Pump();
        Assert.Equal(AiCommandPaletteActionState.Done, palette.Session.StateOf(row));
        Assert.Equal("Created ENG-1", row.Title);
        Assert.Equal("Created ENG-1", completed!.Result);
        Assert.Equal("issue", completed.Action.Id);
        Assert.Equal("Created ENG-1", palette.Announcement);
        Assert.Contains(":done", row.Classes);

        // A finished action does not run again; the second one ends on its done label.
        Press(window, PhysicalKey.Enter);
        Assert.Equal(AiCommandPaletteActionState.Done, palette.Session.StateOf(row));
        Press(window, PhysicalKey.ArrowDown);
        Press(window, PhysicalKey.Enter);
        Assert.Equal("Opened", palette.ActionList.Rows[1].Title);
    }

    [AvaloniaFact]
    public void A_Failing_Action_Shows_The_Error_Label_And_Enter_Retries()
    {
        var calls = 0;
        var (palette, window, timers) = Open(p => p.Responder = Streaming("Answer.", run: _ =>
        {
            calls++;
            return calls == 1 ? Task.FromException<string?>(new InvalidOperationException("nope")) : Task.FromResult<string?>("Fine");
        }));
        palette.Ask("q");
        Advance(timers, 1);
        Press(window, PhysicalKey.Enter);
        var row = palette.ActionList!.Rows[0];
        Assert.Equal(AiCommandPaletteActionState.Failed, palette.Session!.StateOf(row));
        Assert.Equal("Failed. Press Enter to retry", row.Title);
        Assert.Contains("Retry", HintLabels(palette));
        Press(window, PhysicalKey.Enter);
        Assert.Equal(2, calls);
        Assert.Equal("Fine", row.Title);
    }

    [AvaloniaFact]
    public void A_Failing_Responder_Shows_The_Error_With_A_Retry_Row()
    {
        var calls = 0;
        var (palette, window, timers) = Open(p => p.Responder = _ =>
        {
            calls++;
            return Task.FromException<AiCommandPaletteAnswer?>(new InvalidOperationException("Model unavailable"));
        });
        palette.Ask("why");
        Pump();
        Assert.Equal(AiCommandPaletteAnswerState.Failed, palette.AnswerState);
        Assert.Equal(AiCommandPaletteRowKind.Retry, palette.ActionList!.Rows.Single().Kind);
        Assert.Contains("Model unavailable", palette.Announcement);
        Press(window, PhysicalKey.Enter);
        Assert.Equal(2, calls);
    }

    [AvaloniaFact]
    public void Escape_Goes_Back_To_Search_And_Cancels_Then_Clears_Then_Asks_To_Close()
    {
        CancellationToken token = default;
        var (palette, window, _) = Open(p =>
        {
            p.Items.AddRange(TeamItems());
            p.Responder = context =>
            {
                token = context.CancellationToken;
                return new TaskCompletionSource<AiCommandPaletteAnswer?>().Task;
            };
        });
        var closes = 0;
        palette.CloseRequested += (_, _) => closes++;
        palette.Query = "bill";
        Pump();
        Press(window, PhysicalKey.Tab);
        Assert.Equal(AiCommandPaletteAnswerState.Thinking, palette.AnswerState);

        Press(window, PhysicalKey.Escape);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(AiCommandPaletteMode.Search, palette.Mode);
        Assert.Equal("bill", palette.Query);
        Assert.Null(palette.Session);

        Press(window, PhysicalKey.Escape);
        Assert.Equal(string.Empty, palette.Query);
        Assert.Equal(0, closes);
        Press(window, PhysicalKey.Escape);
        Assert.Equal(1, closes);
    }

    [AvaloniaFact]
    public void Asking_Again_Cancels_The_Earlier_Answer()
    {
        var tokens = new List<CancellationToken>();
        var (palette, _, _) = Open(p => p.Responder = context =>
        {
            tokens.Add(context.CancellationToken);
            return new TaskCompletionSource<AiCommandPaletteAnswer?>().Task;
        });
        palette.Ask("first");
        palette.Ask("second");
        Pump();
        Assert.True(tokens[0].IsCancellationRequested);
        Assert.False(tokens[1].IsCancellationRequested);
    }

    [AvaloniaFact]
    public void Chips_And_Citations_Raise_SourceSelected_And_Light_Their_Partner()
    {
        var (palette, _, timers) = Open(p => p.Responder = Streaming("Text with a source [1] and another [2]."));
        palette.Ask("q");
        Advance(timers, 1);
        var events = new List<(string Id, bool FromCitation)>();
        palette.SourceSelected += (_, e) => events.Add((e.Item.Id, e.FromCitation));

        palette.Chips[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        palette.Citations[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal([("b", false), ("a", true)], events);

        palette.SetPartner(1, true);
        Assert.Contains(":partner", palette.Chips[0].Classes);
        Assert.Contains(":partner", palette.Citations[0].Classes);
        Assert.DoesNotContain(":partner", palette.Chips[1].Classes);
        palette.SetPartner(1, false);
        Assert.DoesNotContain(":partner", palette.Citations[0].Classes);

        Assert.Equal("Source 1: The first document", AutomationProperties.GetName(palette.Chips[0]));
        Assert.Equal("Source 2: Doc B", AutomationProperties.GetName(palette.Citations[1]));
    }

    [AvaloniaFact]
    public void The_Simulated_Model_Answers_A_Question_With_Sources_Actions_And_A_Simulated_Run()
    {
        var (palette, window, timers) = Open(p => p.Simulation = Simulation());
        Press(window, PhysicalKey.Tab);
        Press(window, PhysicalKey.Enter);
        Assert.Equal("Why did the invoice for Globex fail?", palette.Session!.Question);
        Assert.Equal(AiCommandPaletteAnswerState.Thinking, palette.AnswerState);
        Advance(timers, 0.8);
        Assert.Equal(3, palette.Chips.Count);
        Advance(timers, 30);
        Assert.Equal(AiCommandPaletteAnswerState.Done, palette.AnswerState);
        Assert.Equal(3, palette.Citations.Count);
        Assert.Equal("Create issue", palette.ActionList!.Rows[0].Title);

        Press(window, PhysicalKey.Enter);
        Advance(timers, 1.2);
        Assert.Equal("Created ENG-12", palette.ActionList.Rows[0].Title);
        Assert.Equal("Simulated, nothing was sent", palette.ActionList.Rows[0].Meta);
        Assert.Equal("Created ENG-12. Simulated, nothing was sent", palette.Announcement);
    }

    [AvaloniaFact]
    public void The_Simulated_Model_Falls_Back_To_The_Answer_Without_Keywords()
    {
        var (palette, window, timers) = Open(p => p.Simulation = Simulation());
        palette.Query = "What is the weather?";
        Press(window, PhysicalKey.Tab);
        Advance(timers, 30);
        Assert.Equal(AiCommandPaletteAnswerState.Done, palette.AnswerState);
        Assert.Single(palette.Chips);
        Assert.Contains(palette.Session!.Shown, w => w.Text.Contains("weather”", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void App_Items_And_Responder_Win_And_Removing_The_Simulation_Empties_The_Palette()
    {
        var (palette, _, _) = Open(p =>
        {
            p.Simulation = Simulation();
            p.Items.AddRange(TeamItems());
            p.Responder = Streaming("Own answer.");
        });
        Assert.False(palette.IsSample);
        Assert.Equal(["Inbox", "Invite teammate"], Titles(palette.SearchList));
        palette.Items.Clear();
        palette.Responder = null;
        Pump();
        Assert.True(palette.IsSample);
        Assert.Equal(["Inbox", "My issues", "Invite teammate"], Titles(palette.SearchList));
        palette.Simulation = null;
        Pump();
        Assert.False(palette.IsSample);
        Assert.Empty(Titles(palette.SearchList));
    }

    [AvaloniaFact]
    public void The_Palette_Keeps_Its_Size_In_Every_View()
    {
        var (palette, window, timers) = Open(p => p.Responder = Streaming(string.Join(" ", Enumerable.Repeat("word", 400))));
        var size = palette.Bounds.Size;
        // Input row, results, footer and the 1px frame above and below.
        Assert.Equal(56 + 392 + 40 + 2, size.Height, 1);
        palette.Query = "zzz";
        Pump();
        Assert.Equal(size, palette.Bounds.Size);
        Press(window, PhysicalKey.Tab);
        Advance(timers, 30);
        Assert.Equal(AiCommandPaletteAnswerState.Done, palette.AnswerState);
        Assert.Equal(size, palette.Bounds.Size);
        palette.ResultsHeight = 300;
        Pump();
        Assert.Equal(56 + 300 + 40 + 2, palette.Bounds.Height, 1);
    }

    [AvaloniaFact]
    public void It_Fills_Its_Container_Up_To_680()
    {
        var (palette, window, _) = Open(width: 900);
        Assert.Equal(680, palette.Bounds.Width, 1);
        window.Width = 360;
        Pump();
        Assert.Equal(360, palette.Bounds.Width, 1);
    }

    [AvaloniaFact]
    public void Below_540_The_Navigate_Hint_Leaves_The_Footer()
    {
        var (palette, _, _) = Open(p => p.Items.AddRange(TeamItems()), width: 420);
        Assert.Equal(["Open", "Ask AI", "Close"], HintLabels(palette));
    }

    [AvaloniaFact]
    public void Labels_Reword_The_Palette()
    {
        var (palette, window, _) = Open(p => p.Labels = new AiCommandPaletteLabels
        {
            SearchPlaceholder = "Suchen",
            AskMode = "KI fragen",
            AskAbout = q => "KI zu " + q,
            HintClose = "Schließen",
        });
        Assert.Equal("Suchen", palette.Input!.PlaceholderText);
        Assert.Equal("KI fragen", palette.AskModeButton!.Content);
        Assert.Contains("Schließen", HintLabels(palette));
        palette.Query = "x";
        Pump();
        Assert.Equal("KI zu x", palette.SearchList!.Rows[^1].Title);
        Assert.Equal("Command palette", AutomationProperties.GetName(palette));
    }

    [AvaloniaFact]
    public void Clicking_The_Mode_Toggle_Switches_Modes_And_Mode_Is_Two_Way()
    {
        var (palette, _, _) = Open();
        palette.AskModeButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
        Assert.Equal(AiCommandPaletteMode.Ask, palette.Mode);
        Assert.True(palette.AskModeButton.IsChecked);
        palette.Mode = AiCommandPaletteMode.Search;
        Pump();
        Assert.Equal(AiCommandPalette.View.Search, palette.CurrentView);
        Assert.True(palette.SearchModeButton!.IsChecked);
    }

    [AvaloniaFact]
    public void Hotkey_Focuses_The_Input()
    {
        var other = new TextBox();
        var timers = new AiCommandPaletteManualScheduler();
        var palette = new AiCommandPalette { Scheduler = timers, AutoPlay = false, IsHotkeyEnabled = true };
        var window = TestHelpers.Show(new StackPanel { Children = { other, palette } });
        Pump();
        other.Focus();
        Pump();
        Press(window, PhysicalKey.K, RawInputModifiers.Control);
        Assert.True(palette.Input!.IsFocused);
    }

    [AvaloniaFact]
    public void The_Walkthrough_Does_Not_Run_Without_Full_Motion_And_A_Real_Key_Ends_It()
    {
        var timers = new AiCommandPaletteManualScheduler();
        var palette = new AiCommandPalette { Scheduler = timers, Simulation = Simulation() };
        var window = TestHelpers.Show(palette, ArcMotionLevel.Off);
        Pump();
        Assert.False(palette.IsWalkthroughRunning);

        // Driven by hand: it types "inv" into the search.
        palette.StepWalkthrough(0);
        palette.StepWalkthrough(1.25);
        palette.StepWalkthrough(0.13);
        palette.StepWalkthrough(0.13);
        Pump();
        Assert.Equal("inv", palette.Query);
        Assert.False(palette.IsWalkthroughStopped);

        palette.FocusInput();
        Press(window, PhysicalKey.ArrowDown);
        Assert.True(palette.IsWalkthroughStopped);
        Assert.Equal(string.Empty, palette.Query);
    }

    [AvaloniaFact]
    public void The_Dialog_Opens_On_Ctrl_K_And_Closes_On_Escape_In_An_Empty_Search()
    {
        var timers = new AiCommandPaletteManualScheduler();
        var palette = new AiCommandPalette { Scheduler = timers, AutoPlay = false };
        var dialog = new AiCommandPaletteDialog { Palette = palette };
        var window = TestHelpers.Show(new Panel { Children = { dialog, new TextBox() } });
        Pump();
        Press(window, PhysicalKey.K, RawInputModifiers.Control);
        Assert.True(dialog.IsOpen);
        Assert.True(palette.IsElevated);
        Assert.True(palette.IsAttachedToVisualTreeSafe());
        palette.FocusInput();
        Pump();
        Press(window, PhysicalKey.Escape);
        Assert.False(dialog.IsOpen);
    }

    [AvaloniaFact]
    public void The_Dialog_Closes_When_An_Item_Is_Chosen()
    {
        var palette = new AiCommandPalette { Scheduler = new AiCommandPaletteManualScheduler(), AutoPlay = false };
        palette.Items.AddRange(TeamItems());
        var dialog = new AiCommandPaletteDialog { Palette = palette };
        var window = TestHelpers.Show(new Panel { Children = { dialog } });
        Pump();
        dialog.IsOpen = true;
        Pump();
        palette.FocusInput();
        Pump();
        Press(window, PhysicalKey.Enter);
        Assert.False(dialog.IsOpen);
    }

    // Pure logic.

    [AvaloniaFact]
    public void Search_Matches_Every_Query_Word_As_A_Word_Prefix_And_Puts_Leading_Labels_First()
    {
        var items = TeamItems();
        var groups = AiCommandPaletteSearch.Filter(items, "pay");
        Assert.Equal("Billing", groups.Single().Items.Single().Label);
        Assert.Empty(AiCommandPaletteSearch.Filter(items, "illing"));
        Assert.Equal(["Pages", "Actions"], AiCommandPaletteSearch.Filter(items, "").Select(g => g.Group));
        Assert.Equal(["Inbox", "Invite teammate"], AiCommandPaletteSearch.Filter(items, "").SelectMany(g => g.Items).Select(i => i.Label));
        Assert.Single(AiCommandPaletteSearch.Filter(items, "switch th").SelectMany(g => g.Items));
    }

    [AvaloniaFact]
    public void The_Word_Queue_Holds_Back_Half_Words_And_Marks_Paragraphs()
    {
        var queue = new AiCommandPaletteWordQueue();
        queue.Push("Hel");
        Assert.Equal(0, queue.Count);
        queue.Push("lo wor");
        Assert.Equal(["Hello"], queue.Take(10).Select(w => w.Text));
        queue.Push("ld.\n\nNext");
        queue.Push(" para");
        queue.Complete();
        var rest = queue.Take(10);
        Assert.Equal(["world.", "", "Next", "para"], rest.Select(w => w.Text));
        Assert.True(rest[1].IsBreak);
        Assert.True(queue.IsDrained);
    }

    [AvaloniaFact]
    public void Citations_Split_Only_Known_Source_Numbers()
    {
        Assert.Equal([("expired", 0), ("[1]", 1), (".", 0)], AiCommandPaletteCitations.Split("expired[1].", 2));
        Assert.Equal([("[3]", 0)], AiCommandPaletteCitations.Split("[3]", 2));
        Assert.Equal("It failed. Then", AiCommandPaletteCitations.Plain("It failed [1]. Then"));
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
