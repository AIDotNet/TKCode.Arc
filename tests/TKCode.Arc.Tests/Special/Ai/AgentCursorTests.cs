using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// AgentCursor: the simulated run on an invented fixture (read, rewrite with a drag selection and streaming, insert, fix with a flag), review with
/// Accept and Undo and their events, Stop, Ask when it does not start on its own, a run fetched from the app (success,
/// failure, cancel), an empty run, the empty document, the phases, the keyboard model, the compact breakpoint, labels and
/// the pure text logic. The story clock is driven by hand.
/// </summary>
public class AgentCursorTests
{
    /// <summary>The fixture agent's name.</summary>
    private const string AgentName = "Claude Opus 5.5";

    /// <summary>A card playing the fixture session (pass <paramref name="simulate"/> false for a card with no data).</summary>
    private static (AgentCursor Card, Window Window) Open(Action<AgentCursor>? setup = null, double width = 640,
        ArcMotionLevel level = ArcMotionLevel.Off, bool simulate = true)
    {
        var card = new AgentCursor { ManualClock = true, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (simulate)
        {
            card.Simulation = AgentCursorFixture.Simulation();
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (card, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Moves the story clock in 50ms frames with layout between them.</summary>
    private static void Step(AgentCursor card, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += 0.05)
        {
            card.Advance(0.05);
            Pump();
        }
    }

    /// <summary>Steps until <paramref name="condition"/> holds (at most <paramref name="limit"/> story seconds).</summary>
    private static bool StepUntil(AgentCursor card, Func<bool> condition, double limit = 30)
    {
        for (var t = 0.0; t < limit; t += 0.05)
        {
            if (condition())
            {
                return true;
            }
            card.Advance(0.05);
            Pump();
        }
        return condition();
    }

    private static AgentCursorLiveBlock Block(AgentCursor card, string id) => card.LiveBlocks.First(b => b.Id == id);

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static List<AgentCursorBlock> Brief() =>
    [
        new("t", AgentCursorBlockKind.Title, "Weekly notes"),
        new("p1", AgentCursorBlockKind.Paragraph, "The team shipped the new onboarding flow and fixed two crashes on start."),
        new("p2", AgentCursorBlockKind.Paragraph, "Next week we plan to recieve feedback from five customers."),
    ];

    [AvaloniaFact]
    public void Plays_The_Simulation_Into_Review()
    {
        var (card, window) = Open();
        try
        {
            card.PlayingSimulation.ShouldNotBeNull();
            card.Phase.ShouldBe(AgentCursorPhase.Thinking);
            card.StatusText.ShouldBe(AgentName + " is reading");
            card.StatusShimmers.ShouldBeTrue();
            card.LiveBlocks.Count.ShouldBe(6);

            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            card.PendingChanges.ShouldBe(3);
            card.StatusText.ShouldBe("3 edits ready");
            card.StatusShimmers.ShouldBeFalse();
            card.IsReviewBarShown.ShouldBeTrue();
            card.ReviewBar!.IsVisible.ShouldBeTrue();
            card.ReviewText!.Text.ShouldBe("3 changes by " + AgentName);
            card.ActionButton!.IsVisible.ShouldBeFalse();

            var expected = AgentCursorText.Apply(AgentCursorFixture.Blocks, AgentCursorFixture.Edits);
            AgentCursorText.SameContent(card.CurrentDocument(), expected).ShouldBeTrue();
            card.LiveBlocks.Select(b => b.Id).ShouldBe(["title", "summary-heading", "summary", "goals-heading", "goals", "rollout-heading", "rollout"]);
            Block(card, "rollout-heading").Kind.ShouldBe(AgentCursorBlockKind.Heading);
            Block(card, "rollout").Text.ShouldEndWith("help center article.");
            Block(card, "summary").View.TintRanges.Count.ShouldBe(1);
            Block(card, "rollout").View.TintRanges.ShouldBe([(Block(card, "rollout").Text.IndexOf("article", StringComparison.Ordinal), 7)]);
            Block(card, "goals").View.TintRanges.ShouldBeEmpty();

            // Below full motion the sample does not loop: it waits in review.
            Step(card, 8);
            card.Phase.ShouldBe(AgentCursorPhase.Review);
            card.IsPlaying.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rewrite_Selects_By_Dragging_Then_Replaces_The_Text()
    {
        var (card, window) = Open();
        try
        {
            var summary = Block(card, "summary");
            var original = summary.Text;
            StepUntil(card, () => summary.View.SelectionRange.Length > 0).ShouldBeTrue();
            card.Phase.ShouldBe(AgentCursorPhase.Editing);
            card.StatusText.ShouldBe("Tightening the summary");
            summary.View.SelectionRange.Start.ShouldBe(0);
            summary.View.SelectionRange.Length.ShouldBeLessThanOrEqualTo(original.Length);
            card.AgentFlight!.IsShown.ShouldBeTrue();

            StepUntil(card, () => summary.Text != original).ShouldBeTrue();
            summary.View.SelectionRange.Length.ShouldBe(0);
            summary.View.TintRanges.Count.ShouldBe(1);
            summary.View.Text.ShouldStartWith("Shared inbox lets a team");
            card.PendingChanges.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fix_Flags_The_Word_Before_Replacing_It()
    {
        var (card, window) = Open();
        try
        {
            var rollout = Block(card, "rollout");
            var at = rollout.Text.IndexOf("artcle", StringComparison.Ordinal);
            StepUntil(card, () => rollout.View.FlagRange.Length > 0).ShouldBeTrue();
            rollout.View.FlagRange.ShouldBe((at, 6));
            card.StatusText.ShouldBe("Fixing a typo");
            StepUntil(card, () => rollout.Text.Contains("article", StringComparison.Ordinal)).ShouldBeTrue();
            rollout.View.FlagRange.Length.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Accept_Raises_The_Edited_Document()
    {
        var (card, window) = Open();
        try
        {
            IReadOnlyList<AgentCursorBlock>? accepted = null;
            card.Accepted += (_, e) => accepted = e.Blocks;
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            card.Accept();
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Accepted);
            card.PendingChanges.ShouldBe(0);
            card.StatusText.ShouldBe("Changes accepted");
            card.IsReviewBarShown.ShouldBeFalse();
            card.ReviewBar!.IsVisible.ShouldBeFalse();
            accepted.ShouldNotBeNull();
            AgentCursorText.SameContent(accepted, card.CurrentDocument()).ShouldBeTrue();
            card.LiveBlocks.All(b => b.View.TintRanges.Count == 0).ShouldBeTrue();
            card.ActionButton!.IsVisible.ShouldBeTrue();
            card.ActionButton.Content.ShouldBe("Run again");

            // Run again starts the sample over from its original text.
            card.Ask();
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Thinking);
            AgentCursorText.SameContent(card.CurrentDocument(), AgentCursorFixture.Blocks).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_Restores_The_Document()
    {
        var (card, window) = Open();
        try
        {
            IReadOnlyList<AgentCursorBlock>? restored = null;
            card.Undone += (_, e) => restored = e.Blocks;
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            card.Undo();
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Reverted);
            card.StatusText.ShouldBe("Changes undone");
            restored.ShouldNotBeNull();
            AgentCursorText.SameContent(restored, AgentCursorFixture.Blocks).ShouldBeTrue();
            AgentCursorText.SameContent(card.CurrentDocument(), AgentCursorFixture.Blocks).ShouldBeTrue();
            card.LiveBlocks.All(b => b.View.TintRanges.Count == 0).ShouldBeTrue();
            // The inserted heading closes and leaves the document.
            TestHelpers.PumpUntil(() => card.GetVisualDescendants().OfType<AgentCursorTextView>().All(v => v.Text != "Rollout")).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Puts_Back_The_Edit_In_Progress()
    {
        var (card, window) = Open();
        try
        {
            var summary = Block(card, "summary");
            var original = summary.Text;
            StepUntil(card, () => summary.View.SelectionRange.Length > 0).ShouldBeTrue();
            card.ActionButton!.Content.ShouldBe("Stop");
            card.Stop();
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Stopped);
            card.StatusText.ShouldBe("Stopped");
            summary.Text.ShouldBe(original);
            summary.View.Text.ShouldBe(original);
            summary.View.SelectionRange.Length.ShouldBe(0);
            card.PendingChanges.ShouldBe(0);
            card.IsReviewBarShown.ShouldBeFalse();
            card.ActionButton.Content.ShouldBe("Run again");
            Step(card, 3);
            card.Phase.ShouldBe(AgentCursorPhase.Stopped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_After_An_Edit_Keeps_It_For_Review()
    {
        var (card, window) = Open();
        try
        {
            StepUntil(card, () => card.PendingChanges == 1).ShouldBeTrue();
            card.Stop();
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Stopped);
            card.IsReviewBarShown.ShouldBeTrue();
            card.ReviewText!.Text.ShouldBe("1 change by " + AgentName);
            card.Accept();
            card.Phase.ShouldBe(AgentCursorPhase.Accepted);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_AutoPlay_The_Header_Offers_Ask()
    {
        var (card, window) = Open(c => c.AutoPlay = false);
        try
        {
            card.Phase.ShouldBe(AgentCursorPhase.Idle);
            card.StatusText.ShouldBe(AgentName + " can edit this doc");
            card.ActionButton!.IsVisible.ShouldBeTrue();
            card.ActionButton.Content.ShouldBe("Ask " + AgentName);
            card.ActionButton.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            card.Phase.ShouldBe(AgentCursorPhase.Thinking);
            card.ActionButton.Content.ShouldBe("Stop");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tab_Reaches_Undo_Then_Accept_And_Enter_Accepts()
    {
        var (card, window) = Open();
        try
        {
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            Press(window, PhysicalKey.Tab);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(card.UndoButton);
            Press(window, PhysicalKey.Tab);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(card.AcceptButton);
            Press(window, PhysicalKey.Enter);
            card.Phase.ShouldBe(AgentCursorPhase.Accepted);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plays_The_Apps_Edits_On_The_Apps_Document()
    {
        var (card, window) = Open(c =>
        {
            c.Blocks = Brief();
            c.Meta = "Owner · Today";
            c.Edits =
            [
                AgentCursorEdit.Fix("p2", "recieve", "collect"),
                AgentCursorEdit.Insert("p1", AgentCursorBlockKind.Paragraph, "Crash reports dropped by half."),
                AgentCursorEdit.Fix("missing", "x", "y"),
            ];
        });
        try
        {
            card.PlayingSimulation.ShouldBeNull();
            card.EffectiveSimulated.ShouldBeFalse();
            card.GetVisualDescendants().OfType<AgentCursorTextView>().ShouldContain(v => v.Text == "Owner · Today");
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            card.PendingChanges.ShouldBe(2);
            card.StatusText.ShouldBe("2 edits ready");
            Block(card, "p2").Text.ShouldBe("Next week we plan to collect feedback from five customers.");
            card.LiveBlocks.Select(b => b.Id).ShouldBe(["t", "p1", "inserted-1", "p2"]);
            card.LiveBlocks[2].Text.ShouldBe("Crash reports dropped by half.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Document_With_No_Edits_Ends_Empty()
    {
        var (card, window) = Open(c => c.Blocks = Brief());
        try
        {
            StepUntil(card, () => card.Phase == AgentCursorPhase.Empty).ShouldBeTrue();
            card.StatusText.ShouldBe("No changes needed");
            card.PendingChanges.ShouldBe(0);
            card.ActionButton!.Content.ShouldBe("Run again");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Run_Fetches_The_Edits()
    {
        var source = new TaskCompletionSource<IReadOnlyList<AgentCursorEdit>>();
        CancellationToken token = default;
        var (card, window) = Open(c =>
        {
            c.Blocks = Brief();
            c.Edits = [AgentCursorEdit.Rewrite("p1", "ignored")];
            c.Run = t =>
            {
                token = t;
                return source.Task;
            };
        });
        try
        {
            Step(card, 5);
            card.Phase.ShouldBe(AgentCursorPhase.Thinking);
            source.SetResult([AgentCursorEdit.Rewrite("p1", "The team shipped onboarding.")]);
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            Block(card, "p1").Text.ShouldBe("The team shipped onboarding.");
            token.IsCancellationRequested.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Run_Shows_Retry()
    {
        var (card, window) = Open(c =>
        {
            c.Blocks = Brief();
            c.Run = _ => Task.FromException<IReadOnlyList<AgentCursorEdit>>(new InvalidOperationException("Agent unavailable"));
        });
        try
        {
            StepUntil(card, () => card.Phase == AgentCursorPhase.Error).ShouldBeTrue();
            card.StatusText.ShouldBe("Couldn't get the edits");
            card.ActionButton!.Content.ShouldBe("Retry");
            card.Announcer!.History.ShouldContain("Agent unavailable");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Cancels_The_Run()
    {
        CancellationToken token = default;
        var (card, window) = Open(c =>
        {
            c.Blocks = Brief();
            c.Run = t =>
            {
                token = t;
                return new TaskCompletionSource<IReadOnlyList<AgentCursorEdit>>().Task;
            };
        });
        try
        {
            Step(card, 3);
            token.CanBeCanceled.ShouldBeTrue();
            card.Stop();
            token.IsCancellationRequested.ShouldBeTrue();
            card.Phase.ShouldBe(AgentCursorPhase.Stopped);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Phases_Are_Raised_In_Order_And_Announced()
    {
        var phases = new List<(AgentCursorPhase Old, AgentCursorPhase New)>();
        var (card, window) = Open(c => c.PhaseChanged += (_, e) => phases.Add((e.OldPhase, e.Phase)));
        try
        {
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            phases.ShouldBe([
                (AgentCursorPhase.Idle, AgentCursorPhase.Thinking),
                (AgentCursorPhase.Thinking, AgentCursorPhase.Editing),
                (AgentCursorPhase.Editing, AgentCursorPhase.Review),
            ]);
            var history = card.Announcer!.History;
            history.ShouldContain(AgentName + " is reading. Simulated, nothing was saved");
            history.ShouldContain("Adding a Rollout heading. Simulated, nothing was saved");
            history.ShouldContain("3 edits ready. Simulated, nothing was saved");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Drop_The_Agent_From_The_Review_Bar()
    {
        var (card, window) = Open(width: 400);
        try
        {
            card.IsCompact.ShouldBeTrue();
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            card.ReviewText!.Text.ShouldBe("3 changes");
            window.Width = 640;
            Pump();
            card.IsCompact.ShouldBeFalse();
            card.ReviewText.Text.ShouldBe("3 changes by " + AgentName);
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(680);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Agent_And_Collaborator_Are_Used()
    {
        var (card, window) = Open(c =>
        {
            c.AutoPlay = false;
            c.Agent = new AgentModel("Gemini 3.5 Flash-Lite");
            c.Collaborator = new AgentCursorCollaborator("Jon Park");
            c.Labels = new AgentCursorLabels { Ask = name => "Ask " + name + " to edit", Idle = _ => "Ready" };
        });
        try
        {
            card.ActionButton!.Content.ShouldBe("Ask Gemini 3.5 Flash-Lite to edit");
            card.StatusText.ShouldBe("Ready");
            card.TeammateFlight!.Pointer.Label.ShouldBe("Jon");
            card.TeammateFlight.Pointer.Initials.ShouldBe("JP");
            card.AgentFlight!.Pointer.Label.ShouldBe("Gemini 3.5 Flash-Lite");
            card.IsCollaboratorVisible = false;
            card.TeammateShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Passing_Back_The_Accepted_Document_Keeps_It()
    {
        var (card, window) = Open(c =>
        {
            c.Blocks = Brief();
            c.Edits = [AgentCursorEdit.Fix("p2", "recieve", "collect")];
        });
        try
        {
            card.Accepted += (_, e) => card.Blocks = e.Blocks.ToList();
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            var before = card.LiveBlocks.ToList();
            card.Accept();
            Pump();
            card.LiveBlocks.ShouldBe(before);
            Block(card, "p2").Text.ShouldContain("collect");

            // A different document replaces it when no run uses it.
            card.Blocks = [new AgentCursorBlock("x", AgentCursorBlockKind.Paragraph, "Fresh text.")];
            Pump();
            card.LiveBlocks.Select(b => b.Text).ShouldBe(["Fresh text."]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Speed_Shortens_The_Run()
    {
        double TimeToReview(double speed)
        {
            var (card, window) = Open(c => c.Speed = speed);
            try
            {
                var t = 0.0;
                while (card.Phase != AgentCursorPhase.Review && t < 60)
                {
                    card.Advance(0.05);
                    Pump();
                    t += 0.05;
                }
                return t;
            }
            finally
            {
                window.Close();
            }
        }
        var normal = TimeToReview(1);
        var fast = TimeToReview(2);
        fast.ShouldBeLessThan(normal * 0.7);
    }

    [AvaloniaFact]
    public void Full_Motion_Loops_The_Sample_After_The_Teammate_Accepts()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var accepted = 0;
            card.Accepted += (_, _) => accepted++;
            card.EffectiveLoop.ShouldBeTrue();
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review, 60).ShouldBeTrue();
            StepUntil(card, () => card.Phase == AgentCursorPhase.Accepted, 10).ShouldBeTrue();
            accepted.ShouldBe(1);
            card.StatusText.ShouldBe("Maya accepted the changes");
            StepUntil(card, () => card.Phase == AgentCursorPhase.Thinking, 10).ShouldBeTrue();
            AgentCursorText.SameContent(card.CurrentDocument(), AgentCursorFixture.Blocks).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Logic()
    {
        AgentCursorText.FindWord("an art artcle artcle", "artcle").ShouldBe(7);
        AgentCursorText.FindWord("cart art", "art").ShouldBe(5);
        AgentCursorText.FindWord("smart", "art").ShouldBe(2);
        AgentCursorText.FindWord("abc", "zz").ShouldBe(-1);

        var text = "one two three four five six seven eight nine ten eleven twelve thirteen";
        var ends = AgentCursorText.ChunkEnds(text);
        ends[^1].ShouldBe(text.Length);
        var start = 0;
        for (var i = 0; i < ends.Count; i++)
        {
            var words = text[start..ends[i]].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (i < ends.Count - 1)
            {
                words.ShouldBeInRange(3, 6);
            }
            start = ends[i];
        }

        AgentCursorText.FirstName("Maya Ortiz").ShouldBe("Maya");
        AgentCursorText.Initials("Maya Ortiz").ShouldBe("MO");
        AgentCursorText.Initials("Scribe").ShouldBe("S");
        AgentCursorText.FlightSeconds(0).ShouldBe(0.28, 1e-9);
        AgentCursorText.FlightSeconds(5000).ShouldBe(0.6, 1e-9);
        AgentCursorText.SameContent(Brief(), Brief()).ShouldBeTrue();
        AgentCursorText.SameContent(Brief(), Brief().Take(2).ToList()).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void No_Data_Shows_An_Empty_Document_And_Does_Not_Run()
    {
        var (card, window) = Open(simulate: false);
        try
        {
            card.PlayingSimulation.ShouldBeNull();
            card.EffectiveSimulated.ShouldBeFalse();
            card.Phase.ShouldBe(AgentCursorPhase.Idle);
            card.LiveBlocks.Count.ShouldBe(0);
            card.AgentName.ShouldBe("Agent");
            card.TeammateShown.ShouldBeFalse();
            card.EffectiveLoop.ShouldBeFalse();
            Step(card, 2);
            card.Phase.ShouldBe(AgentCursorPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Set_Later_Plays_Its_Session()
    {
        var (card, window) = Open(simulate: false);
        try
        {
            card.Simulation = AgentCursorFixture.Simulation();
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Thinking);
            card.LiveBlocks.Count.ShouldBe(6);
            card.AgentName.ShouldBe(AgentName);
            card.TeammateShown.ShouldBeTrue();
            StepUntil(card, () => card.Phase == AgentCursorPhase.Review).ShouldBeTrue();
            card.PendingChanges.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Blocks_Win_Over_The_Simulation()
    {
        var (card, window) = Open(c =>
        {
            c.AutoPlay = false;
            c.Blocks = Brief();
        });
        try
        {
            card.PlayingSimulation.ShouldBeNull();
            card.LiveBlocks.Select(b => b.Id).ShouldBe(["t", "p1", "p2"]);
            card.AgentName.ShouldBe("Agent");
            card.TeammateShown.ShouldBeFalse();
            card.EffectiveEdits.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_The_Run_And_Shows_The_Empty_Document()
    {
        var (card, window) = Open();
        try
        {
            StepUntil(card, () => card.Phase == AgentCursorPhase.Editing).ShouldBeTrue();
            card.Simulation = null;
            Pump();
            card.Phase.ShouldBe(AgentCursorPhase.Idle);
            card.PendingChanges.ShouldBe(0);
            card.LiveBlocks.Count.ShouldBe(0);
            card.IsReviewBarShown.ShouldBeFalse();
            Step(card, 5);
            card.Phase.ShouldBe(AgentCursorPhase.Idle);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Invented test data for the AgentCursor tests (the product and people are made up).</summary>
file static class AgentCursorFixture
{
    public const string Meta = "Maya Ortiz · Target date Nov 14";

    /// <summary>The agent: a real model's name, a neutral mark and a fixed warm color for its cursor, selection and tint.</summary>
    public static AgentModel Agent { get; } =
        new("Claude Opus 5.5", ArcIcons.Sparkles, new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.FromRgb(0xE0, 0x7A, 0x45)));

    /// <summary>The teammate who reads along.</summary>
    public static AgentCursorCollaborator Collaborator { get; } = new("Maya Ortiz");

    /// <summary>The launch brief.</summary>
    public static IReadOnlyList<AgentCursorBlock> Blocks { get; } =
    [
        new("title", AgentCursorBlockKind.Title, "Launch brief: Shared inbox"),
        new("summary-heading", AgentCursorBlockKind.Heading, "Summary"),
        new("summary", AgentCursorBlockKind.Paragraph,
            "Shared inbox is a new place where the whole team is able to answer customer email together. In this release we are adding assignment, internal notes and a status for every thread, so that nothing gets lost when somebody happens to be out of the office."),
        new("goals-heading", AgentCursorBlockKind.Heading, "Goals"),
        new("goals", AgentCursorBlockKind.Paragraph,
            "Cut the time to a first reply for teams of five to twenty people, and make it obvious who owns each conversation."),
        new("rollout", AgentCursorBlockKind.Paragraph,
            "Beta workspaces get it on Nov 3 and everyone on Nov 14, with a short in-app tour and a help center artcle."),
    ];

    /// <summary>The agent's three edits.</summary>
    public static IReadOnlyList<AgentCursorEdit> Edits { get; } =
    [
        AgentCursorEdit.Rewrite("summary",
            "Shared inbox lets a team answer customer email together. This release adds assignment, internal notes and a status on every thread, so nothing slips when someone is away.",
            "Tightening the summary"),
        AgentCursorEdit.Insert("goals", AgentCursorBlockKind.Heading, "Rollout", "rollout-heading", "Adding a Rollout heading"),
        AgentCursorEdit.Fix("rollout", "artcle", "article", "Fixing a typo"),
    ];

    /// <summary>A new simulation of the session (each card gets its own).</summary>
    public static AgentCursorSimulation Simulation() => new()
    {
        Blocks = Blocks,
        Edits = Edits,
        Agent = Agent,
        Collaborator = Collaborator,
        Meta = Meta,
    };
}
