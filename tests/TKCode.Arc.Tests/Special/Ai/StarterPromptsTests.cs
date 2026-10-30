using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>Starter prompts: tabs and pages, shuffle, picking, the composer's send cycle, keyboard, labels and motion.</summary>
public class StarterPromptsTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    // ---- An invented fixture: 24 starters over four tabs (names, numbers and projects are made up) ----

    private static readonly IReadOnlyList<StarterPromptCategory> Categories =
    [
        new("write", "Write", ArcIcons.PenLine),
        new("code", "Code", ArcIcons.Code),
        new("analyze", "Analyze", ArcIcons.ChartBar),
        new("plan", "Plan", ArcIcons.ListTodo),
    ];

    private static readonly IReadOnlyList<StarterPrompt> Prompts =
    [
        P("launch-email", "write", "Draft the launch email", "For the 1,200 beta customers", StarterPromptApp.Mail,
          "Draft a launch email for the 1,200 customers in the beta list. Lead with the two features they asked for most, keep it under 150 words and end with one clear call to action."),
        P("tighten-post", "write", "Tighten this blog post", "Cut it to a 4 minute read", null,
          "Edit the blog post I paste next so it reads in about 4 minutes. Keep my voice, cut repetition, and list the three biggest changes you made."),
        P("reply-threads", "write", "Answer the open threads", "6 customer emails waiting", StarterPromptApp.Mail,
          "Draft replies to the 6 open customer threads in my inbox. Match each sender's tone, answer the question they asked first, and flag any that need a refund decision."),
        P("release-notes", "write", "Write the release notes", "From last week's merged changes", StarterPromptApp.Repository,
          "Write release notes from the changes merged last week. Group them into New, Improved and Fixed, use plain words, and leave out internal refactors."),
        P("team-update", "write", "Write the weekly update", "Wins, risks and asks", StarterPromptApp.Docs,
          "Turn this week's project notes into a team update with three sections: wins, risks and asks. Keep each item to one sentence."),
        P("job-post", "write", "Draft a job post", "Senior product designer", null,
          "Draft a job post for a senior product designer on a small, remote team. Describe the work in the first 90 days, the must-haves and what we offer, without buzzwords."),
        P("review-pr", "code", "Review my open pull request", "Find bugs before review", StarterPromptApp.Repository,
          "Review my open pull request. Point out bugs, missing tests and unclear names first, then smaller style notes, each with the file and line."),
        P("explain-code", "code", "Explain this function", "Line by line, in plain words", null,
          "Explain the function I paste next line by line in plain words, then describe one input that would make it fail."),
        P("write-tests", "code", "Write tests for the parser", "Cover the edge cases", null,
          "Write unit tests for the date parser I paste next. Cover empty input, time zones, leap days and invalid formats, and name each test after the behavior it checks."),
        P("fix-issue", "code", "Fix the top bug", "The oldest open crash report", StarterPromptApp.Issues,
          "Look at the oldest open crash report in the tracker, explain the likely cause, and propose a minimal fix with a test that reproduces it."),
        P("sql-query", "code", "Write a SQL query", "Weekly active users by plan", null,
          "Write a SQL query for weekly active users by plan for the last 12 weeks, from an events table with user_id, plan and created_at. Explain any assumptions."),
        P("refactor", "code", "Refactor a long component", "Split it without changing behavior", null,
          "Refactor the component I paste next into smaller pieces without changing what it renders. Show the new files and explain why each split helps."),
        P("churn", "analyze", "Find why customers churned", "From last quarter's exit survey", StarterPromptApp.Docs,
          "Read last quarter's exit survey answers and group the reasons customers left into five themes, with a count and one representative quote for each."),
        P("metrics", "analyze", "Summarize this week's metrics", "What moved and why", null,
          "Summarize this week's product metrics I paste next: what moved more than 5%, the likely reason, and one question worth asking the team."),
        P("compare-plans", "analyze", "Compare our pricing plans", "Against what customers use", null,
          "Compare our three pricing plans against the usage data I paste next. Which limits do customers hit first, and which plan looks underpriced?"),
        P("bug-trends", "analyze", "Spot trends in bug reports", "The last 30 days", StarterPromptApp.Issues,
          "Look at the bug reports filed in the last 30 days and tell me which areas of the product get the most reports, and whether any of them is getting worse."),
        P("survey", "analyze", "Read the survey results", "412 answers, sample data", null,
          "Analyze the 412 survey answers I paste next. Give me the three strongest findings, any surprising splits by plan, and what we should not conclude from it."),
        P("meeting-load", "analyze", "Audit my meeting load", "Where the week went", StarterPromptApp.Calendar,
          "Look at my calendar for the last two weeks and show how my time split between meetings, focus time and one-on-ones, and which recurring meetings I could drop."),
        P("plan-week", "plan", "Plan my week", "Around the Thursday deadline", StarterPromptApp.Calendar,
          "Plan my week around the Thursday deadline. Block focus time for the proposal, keep my recurring meetings, and leave Friday afternoon free."),
        P("roadmap", "plan", "Draft a Q1 roadmap", "Three themes, six bets", StarterPromptApp.Docs,
          "Draft a Q1 roadmap with three themes and two bets under each, with the problem, the expected outcome and a rough size for every bet."),
        P("launch-plan", "plan", "Plan the launch", "Two weeks, step by step", StarterPromptApp.Issues,
          "Plan a two week launch for the new reporting feature as a checklist of issues: owners, due dates, and what blocks what."),
        P("offsite", "plan", "Plan a team offsite", "Eight people, two days", null,
          "Plan a two day offsite for eight people. Suggest an agenda that mixes planning and downtime, and list what to book three weeks ahead."),
        P("onboarding", "plan", "Plan a new hire's first week", "An engineer starting Monday", StarterPromptApp.Calendar,
          "Plan the first week for an engineer starting Monday: meetings to set up, the codebase tour, a first small task and who to pair with each day."),
        P("migration", "plan", "Break down the migration", "Into small, safe steps", StarterPromptApp.Repository,
          "Break the database migration into small steps we can ship one at a time, each with a rollback plan and a check that it worked."),
    ];

    private static StarterPrompt P(string id, string category, string title, string description, StarterPromptApp? app, string prompt) =>
        new() { Id = id, Category = category, Title = title, Description = description, App = app, Prompt = prompt };

    /// <summary>Gives <paramref name="control"/> the fixture's tabs, starters and words.</summary>
    private static StarterPrompts Stocked(StarterPrompts control)
    {
        control.Greeting = "What are we working on, Maya?";
        control.Placeholder = "Ask anything, or pick a starter above";
        control.ModelName = "Model X";
        control.Categories.AddRange(Categories);
        control.Prompts.AddRange(Prompts);
        return control;
    }

    private static Window Open(StarterPrompts control, double width = 720, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return window;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static string?[] Ids(StarterPrompts control) => [.. control.VisiblePrompts.Select(p => p?.Id)];

    // ---- Pure logic ----

    [Fact]
    public void Deck_Hides_Empty_Tabs_And_Interleaves_All()
    {
        var deck = new StarterPromptsDeck(1);
        var prompts = new List<StarterPrompt>
        {
            new() { Id = "w1", Category = "write" }, new() { Id = "w2", Category = "write" },
            new() { Id = "c1", Category = "code" }, new() { Id = "x", Category = "other" },
        };
        deck.Reset(prompts, Categories);
        deck.Tabs.Select(t => t.Id).ShouldBe(["write", "code"]);
        deck.HasTab("analyze").ShouldBeFalse();
        deck.HasTab("all").ShouldBeTrue();
        deck.PoolOf("all").Select(p => p.Id).ShouldBe(["w1", "c1", "w2", "x"]);
        deck.Page("write", 4).Select(p => p.Id).ShouldBe(["w1", "w2"]);
        deck.CanShuffle("write", 4).ShouldBeFalse();
    }

    [Fact]
    public void Deck_Shuffle_Always_Brings_Something_New_And_Keeps_The_Pool()
    {
        var deck = new StarterPromptsDeck(7);
        deck.Reset(Prompts, Categories);
        foreach (var tab in new[] { "all", "write", "plan" })
        {
            var pool = deck.PoolOf(tab).ToHashSet();
            var page = deck.Page(tab, 4);
            for (var i = 0; i < 20; i++)
            {
                var next = deck.Shuffle(tab, 4);
                next.Count.ShouldBe(4);
                next.Distinct().Count().ShouldBe(4);
                next.All(pool.Contains).ShouldBeTrue();
                next.Except(page).ShouldNotBeEmpty();
                page = next;
            }
        }
    }

    [Fact]
    public void Fixture_Is_24_Starters_Over_Four_Categories_With_Unique_Ids()
    {
        Prompts.Count.ShouldBe(24);
        Prompts.Select(p => p.Id).Distinct().Count().ShouldBe(24);
        Prompts.GroupBy(p => p.Category).Select(g => g.Count()).ShouldAllBe(c => c == 6);
        Prompts.ShouldContain(p => p.App != null);
        Prompts.ShouldAllBe(p => p.Prompt.Length > p.Title.Length);
    }

    [Fact]
    public void Labels_Build_Card_Names_And_Announcements()
    {
        var labels = StarterPromptsLabels.Default;
        labels.CardName("Plan my week", "Calendar", "Plan").ShouldBe("Plan my week, uses Calendar, Plan");
        labels.CardName("Explain this", null, "Code").ShouldBe("Explain this, Code");
        labels.Shuffled(4).ShouldBe("Showing 4 new starters");
        labels.Picked("Plan my week").ShouldBe("“Plan my week” added to the composer");
    }

    // ---- The control ----

    [AvaloniaFact]
    public void Without_Starters_It_Shows_All_Alone_With_No_Cards_And_No_Invented_Words()
    {
        var control = new StarterPrompts(seed: 3);
        var window = Open(control);
        try
        {
            control.Greeting.ShouldBeNull();
            control.Description.ShouldBeNull();
            control.Placeholder.ShouldBeNull();
            control.ModelName.ShouldBeNull();
            control.TabIds.ShouldBe(["all"]);
            control.VisiblePrompts.ShouldAllBe(p => p == null);
            control.ShuffleButton!.IsEnabled.ShouldBeFalse();
            control.SendButton!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Starters_Show_With_All_And_Four_Tabs()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            control.TabIds.ShouldBe(["all", "write", "code", "analyze", "plan"]);
            control.SelectedCategory.ShouldBe("all");
            control.Cards.Count.ShouldBe(4);
            Ids(control).ShouldBe(["launch-email", "review-pr", "churn", "plan-week"]);
            control.Greeting.ShouldBe("What are we working on, Maya?");
            control.ModelName.ShouldBe("Model X");
            AutomationProperties.GetName(control.Cards[0]).ShouldBe("Draft the launch email, uses Mail, Write");
            AutomationProperties.GetName(control.Cards[1]).ShouldBe("Review my open pull request, uses Repository, Code");
            control.ShuffleButton!.IsEnabled.ShouldBeTrue();
            control.SendButton!.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(control.SendButton).ShouldBe("Send message");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_A_Tab_Filters_The_Cards_Both_Ways()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            control.TabList!.SelectedIndex = 2;
            Pump();
            control.SelectedCategory.ShouldBe("code");
            control.VisiblePrompts.ShouldAllBe(p => p!.Category == "code");
            control.SelectedCategory = "plan";
            Pump();
            control.TabList.SelectedIndex.ShouldBe(4);
            control.VisiblePrompts.ShouldAllBe(p => p!.Category == "plan");
            control.SelectedCategory = "missing";
            Pump();
            control.TabList.SelectedIndex.ShouldBe(0);
            control.VisiblePrompts.Select(p => p!.Category).Distinct().Count().ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Tabs()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            var first = (Control)control.TabList!.ContainerFromIndex(0)!;
            first.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            control.SelectedCategory.ShouldBe("write");
            Press(window, PhysicalKey.ArrowRight);
            control.SelectedCategory.ShouldBe("code");
            Press(window, PhysicalKey.ArrowLeft);
            control.SelectedCategory.ShouldBe("write");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shuffle_Swaps_The_Cards_And_Announces()
    {
        var control = Stocked(new StarterPrompts(seed: 5));
        var window = Open(control);
        try
        {
            var before = Ids(control);
            control.ShuffleButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            var after = Ids(control);
            after.ShouldNotBe(before);
            after.Distinct().Count().ShouldBe(4);
            control.Announcement.ShouldBe("Showing 4 new starters");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Card_Fills_The_Composer_Focuses_It_And_Raises_The_Event()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        StarterPrompt? picked = null;
        control.PromptSelected += (_, e) => picked = e.Prompt;
        var window = Open(control);
        try
        {
            var card = control.Cards[2];
            card.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Space);
            picked.ShouldNotBeNull();
            picked.Id.ShouldBe("churn");
            control.Text.ShouldBe(picked.Prompt);
            control.Input!.Text.ShouldBe(picked.Prompt);
            control.Input.IsFocused.ShouldBeTrue();
            control.Input.CaretIndex.ShouldBe(picked.Prompt.Length);
            control.SendButton!.IsEnabled.ShouldBeTrue();
            control.Announcement.ShouldBe("“Find why customers churned” added to the composer");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Sends_With_The_Picked_Prompt_And_Clears_On_Success()
    {
        var control = Stocked(new StarterPrompts(seed: 3) { SimulatedSendDuration = TimeSpan.Zero });
        var window = Open(control);
        try
        {
            StarterPromptsSubmitEventArgs? sent = null;
            var gate = new TaskCompletionSource();
            control.SubmitRequested += (_, e) =>
            {
                sent = e;
                e.Sending = gate.Task;
            };
            control.Pick(Prompts[0]);
            Press(window, PhysicalKey.Enter);
            sent.ShouldNotBeNull();
            sent.Message.ShouldBe(Prompts[0].Prompt);
            sent.Prompt.ShouldBe(Prompts[0]);
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Sending);
            control.Input!.IsReadOnly.ShouldBeTrue();
            control.SendButton!.IsSending.ShouldBeTrue();
            AutomationProperties.GetName(control.SendButton).ShouldBe("Stop sending");
            control.Announcement.ShouldBe("Sending message");
            gate.SetResult();
            TestHelpers.PumpUntil(() => control.SendStatus == StarterPromptsSendStatus.Sent).ShouldBeTrue();
            control.Text.ShouldBe(string.Empty);
            control.Announcement.ShouldBe("Message sent");
            control.SendButton.IsSending.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Enter_Adds_A_Line_Instead_Of_Sending()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            var sends = 0;
            control.SubmitRequested += (_, _) => sends++;
            control.Text = "First line";
            control.FocusComposer();
            control.Input!.CaretIndex = control.Input.Text!.Length;
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            sends.ShouldBe(0);
            control.Text!.ShouldContain("\n");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Stops_The_Send_And_Keeps_The_Text()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            CancellationToken token = default;
            control.SubmitRequested += (_, e) =>
            {
                token = e.CancellationToken;
                e.Sending = Task.Delay(Timeout.Infinite, e.CancellationToken);
            };
            control.Text = "Summarize the week";
            control.FocusComposer();
            Press(window, PhysicalKey.Enter);
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Sending);
            Press(window, PhysicalKey.Escape);
            token.IsCancellationRequested.ShouldBeTrue();
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Stopped);
            control.Text.ShouldBe("Summarize the week");
            control.Announcement.ShouldBe("Sending stopped");
            TestHelpers.PumpUntil(() => false, 50);
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Stopped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Send_Shows_The_Error_And_Retry_Sends_Again()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            var attempts = 0;
            control.SubmitRequested += (_, e) =>
            {
                attempts++;
                e.Sending = attempts == 1 ? Task.FromException(new InvalidOperationException("Couldn't reach the model.")) : Task.CompletedTask;
            };
            control.Text = "Hello";
            control.Submit();
            Pump();
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Failed);
            control.SendError.ShouldBe("Couldn't reach the model.");
            control.Text.ShouldBe("Hello");
            control.Announcement.ShouldBe("Couldn't send: Couldn't reach the model.");
            var retry = TestHelpers.Descendant<Button>(control.StatusSwap!);
            retry.Content.ShouldBe("Retry");
            retry.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            attempts.ShouldBe(2);
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Sent);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Handler_The_Send_Is_Simulated_And_Says_So()
    {
        var control = Stocked(new StarterPrompts(seed: 3) { SimulatedSendDuration = TimeSpan.Zero });
        var window = Open(control);
        try
        {
            control.Text = "Plan my week";
            control.Submit();
            Pump();
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Sent);
            control.Announcement.ShouldBe("Message sent. This send was simulated.");
            control.Text.ShouldBe(string.Empty);
            control.Text = "Next";
            control.SendStatus.ShouldBe(StarterPromptsSendStatus.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Blank_Text_Does_Not_Send()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control);
        try
        {
            var sends = 0;
            control.SubmitRequested += (_, _) => sends++;
            control.Text = "   ";
            control.Submit();
            sends.ShouldBe(0);
            control.SendButton!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Own_Prompts_And_Categories_Make_Their_Own_Tabs()
    {
        var control = new StarterPrompts(seed: 3) { Count = 2 };
        control.Categories.AddRange([new StarterPromptCategory("support", "Support"), new StarterPromptCategory("sales", "Sales"), new StarterPromptCategory("ops", "Ops")]);
        control.Prompts.AddRange(
        [
            new StarterPrompt { Id = "refund", Category = "support", Title = "Answer refund requests", Prompt = "Draft replies...", App = StarterPromptApp.Mail },
            new StarterPrompt { Id = "pipeline", Category = "sales", Title = "Review the pipeline", Prompt = "List the deals..." },
            new StarterPrompt { Id = "custom", Category = "sales", Title = "Custom app", Prompt = "...", App = new StarterPromptApp("Acme", null) },
        ]);
        var window = Open(control);
        try
        {
            control.TabIds.ShouldBe(["all", "support", "sales"]);
            control.Cards.Count.ShouldBe(2);
            Ids(control).ShouldBe(["refund", "pipeline"]);
            control.SelectedCategory = "support";
            Pump();
            Ids(control).ShouldBe(["refund", null]);
            control.Cards[1].Classes.Contains(":empty").ShouldBeTrue();
            control.Cards[1].Focusable.ShouldBeFalse();
            control.ShuffleButton!.IsEnabled.ShouldBeFalse();
            control.SelectedCategory = "sales";
            Pump();
            control.Cards[1].MarkLetter.ShouldBe("A");
            control.Cards[1].AppName.ShouldBe("Acme");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_Placeholders_That_Cannot_Be_Picked()
    {
        var control = Stocked(new StarterPrompts(seed: 3) { IsLoading = true });
        var window = Open(control);
        try
        {
            control.Cards.ShouldAllBe(c => c.Classes.Contains(":loading") && !c.Focusable);
            control.ShuffleButton!.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(control.Grid!).ShouldBe("Loading starters");
            var height = control.Grid!.Bounds.Height;
            control.IsLoading = false;
            Pump();
            control.Cards.ShouldAllBe(c => !c.Classes.Contains(":loading") && c.Focusable);
            control.Grid.Bounds.Height.ShouldBe(height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Two_Columns_From_560_One_Below_And_Shuffle_Turns_To_An_Icon()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control, 760);
        try
        {
            control.Grid!.Columns.ShouldBe(2);
            control.Classes.Contains(":compact").ShouldBeFalse();
            window.Width = 400;
            Pump();
            control.Grid.Columns.ShouldBe(1);
            control.Classes.Contains(":compact").ShouldBeTrue();
            control.Classes.Contains(":narrow").ShouldBeTrue();
            control.Bounds.Width.ShouldBeLessThanOrEqualTo(400);
            foreach (var card in control.Cards)
            {
                card.Bounds.Right.ShouldBeLessThanOrEqualTo(control.Grid.Bounds.Width + 0.5);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Width_Never_Follows_The_Content()
    {
        var control = Stocked(new StarterPrompts(seed: 3));
        var window = Open(control, 700);
        try
        {
            var width = control.Bounds.Width;
            control.Text = new string('x', 2000);
            control.Shuffle();
            control.SelectedCategory = "code";
            Pump();
            control.Bounds.Width.ShouldBe(width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hiding_The_Composer_Still_Fills_Text_On_A_Pick()
    {
        var control = Stocked(new StarterPrompts(seed: 3) { ShowsComposer = false });
        var window = Open(control);
        try
        {
            control.Input!.IsEffectivelyVisible.ShouldBeFalse();
            control.Pick(Prompts[3]);
            control.Text.ShouldBe(Prompts[3].Prompt);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Motion ----

    [AvaloniaFact]
    public void Shuffle_Crossfades_Each_Card_Staggered_35ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var control = Stocked(new StarterPrompts(seed: 5));
        var window = Open(control, 720, ArcMotionLevel.Full);
        try
        {
            var before = control.Cards.Select(c => c.Prompt).ToArray();
            control.Shuffle();
            Pump();
            // 60ms in: card 0 is half way out, card 3 (delay 105ms) has not started.
            Advance(clock, 60);
            control.Cards[0].ContentOpacity.ShouldBeLessThan(0.8);
            control.Cards[3].ContentOpacity.ShouldBe(1, 0.001);
            control.Cards[0].Prompt.ShouldBe(before[0]);
            // 120ms: card 0 has turned over and is rising in from 2px.
            Advance(clock, 60);
            control.Cards[0].Prompt.ShouldNotBe(before[0]);
            control.Cards[0].ContentOffset.ShouldBeGreaterThan(0);
            control.Cards[0].ContentOffset.ShouldBeLessThanOrEqualTo(2);
            // 105 + 100 + 160 = 365ms: every card is settled.
            Advance(clock, 300);
            control.Cards.ShouldAllBe(c => Math.Abs(c.ContentOpacity - 1) < 0.001 && Math.Abs(c.ContentOffset) < 0.001);
            control.Cards.Select(c => c.Prompt).ShouldBe(control.VisiblePrompts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Swaps_At_Once()
    {
        var control = Stocked(new StarterPrompts(seed: 5));
        var window = Open(control, 720, ArcMotionLevel.Reduced);
        try
        {
            control.Shuffle();
            Pump();
            control.Cards.ShouldAllBe(c => !c.IsSwapping && c.ContentOpacity == 1 && ReferenceEquals(c.Prompt, c.Target));
        }
        finally
        {
            window.Close();
        }
    }

    private static void Advance(ManualFrameClock clock, double milliseconds)
    {
        var frames = (int)Math.Round(milliseconds / Frame.TotalMilliseconds);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Pump();
        }
    }
}
