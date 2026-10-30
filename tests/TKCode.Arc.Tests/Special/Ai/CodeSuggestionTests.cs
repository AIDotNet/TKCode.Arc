using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The code suggestion: diff logic, the empty card, decisions and their events, keyboard, status, layout and the simulation.</summary>
public class CodeSuggestionTests
{
    private const string File = "a\nb\nc\nd\ne";

    private static (CodeSuggestion Card, Window Window) Open(Action<CodeSuggestion>? setup = null, double width = 720,
        Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        var card = new CodeSuggestion();
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (card, window);
    }

    /// <summary>An invented billing helper that fetches an invoice, and three hunks that add retries with backoff.</summary>
    private const string BillingCode =
        "import { db } from \"./db\";\n" +
        "import type { Invoice } from \"./types\";\n" +
        "\n" +
        "export async function fetchInvoice(id: string): Promise<Invoice> {\n" +
        "  const url = `${process.env.BILLING_API}/invoices/${id}`;\n" +
        "  const res = await fetch(url);\n" +
        "  if (!res.ok) {\n" +
        "    throw new Error(`Invoice ${id} failed: ${res.status}`);\n" +
        "  }\n" +
        "  const invoice = (await res.json()) as Invoice;\n" +
        "  await db.invoices.upsert(invoice);\n" +
        "  return invoice;\n" +
        "}";

    private static CodeSuggestionHunk[] BillingHunks() =>
    [
        new("limits", 2, 0,
            "",
            "const MAX_ATTEMPTS = 4;",
            "const BASE_DELAY_MS = 250;") { Summary = "Add retry limits" },
        new("backoff", 5, 4,
            "  let res = await fetch(url);",
            "  for (let attempt = 1; attempt < MAX_ATTEMPTS; attempt++) {",
            "    if (res.ok || (res.status !== 429 && res.status < 500)) break;",
            "    await sleep(BASE_DELAY_MS * 2 ** (attempt - 1));",
            "    res = await fetch(url);",
            "  }",
            "  if (!res.ok) {",
            "    throw new Error(`Invoice ${id} failed after ${MAX_ATTEMPTS} tries: ${res.status}`);",
            "  }") { Summary = "Retry 429 and 5xx with backoff" },
        new("sleep", 13, 0,
            "",
            "function sleep(ms: number) {",
            "  return new Promise((resolve) => setTimeout(resolve, ms));",
            "}") { Summary = "Add a sleep helper" },
    ];

    /// <summary>The billing helper as a simulation, with its file, instruction and an invented model on the card.</summary>
    private static void Simulated(CodeSuggestion card)
    {
        card.FileName = "fetch-invoice.ts";
        card.FilePath = "src/billing";
        card.Instruction = "Add retry with backoff";
        card.Model = new AgentModel("Model X", ArcIcons.Sparkles);
        card.Simulation = new CodeSuggestionSimulation { Code = BillingCode, Hunks = BillingHunks() };
    }

    private static void Given(CodeSuggestion card)
    {
        card.Code = File;
        card.FileName = "notes.txt";
        card.Hunks.Add(new CodeSuggestionHunk("one", 1, 1, "B") { Summary = "Capitalize b" });
        card.Hunks.Add(new CodeSuggestionHunk("two", 3, 0, "x", "y") { Summary = "Insert two lines" });
    }

    // Diff logic ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Normalize_Sorts_Clamps_And_Drops_Overlaps_After_The_First()
    {
        var hunks = new[]
        {
            new CodeSuggestionHunk("late", 3, 1, "D"),
            new CodeSuggestionHunk("early", 0, 2, "A"),
            new CodeSuggestionHunk("overlap", 1, 1, "Z"),
            new CodeSuggestionHunk("same-start", 3, 0, "Q"),
            new CodeSuggestionHunk("late", 4, 0, "dup id"),
            new CodeSuggestionHunk("tail", 99, 5, "end"),
        };
        var spans = CodeSuggestionDiff.Normalize(hunks, 5);
        spans.Select(s => s.Id).ShouldBe(["early", "late", "tail"]);
        spans[2].Start.ShouldBe(5);
        spans[2].Remove.ShouldBe(0);
    }

    [Fact]
    public void Insertion_Inside_Removed_Lines_Overlaps()
    {
        var a = new CodeSuggestionSpan(new CodeSuggestionHunk(), "a", 1, 3, []);
        var b = new CodeSuggestionSpan(new CodeSuggestionHunk(), "b", 2, 0, ["x"]);
        var c = new CodeSuggestionSpan(new CodeSuggestionHunk(), "c", 4, 0, ["x"]);
        CodeSuggestionDiff.Overlaps(a, b).ShouldBeTrue();
        CodeSuggestionDiff.Overlaps(a, c).ShouldBeFalse();
    }

    [Fact]
    public void Apply_Replaces_Inserts_And_Deletes()
    {
        var lines = CodeSuggestionDiff.SplitLines("a\r\nb\nc");
        lines.ShouldBe(["a", "b", "c"]);
        var spans = CodeSuggestionDiff.Normalize(
        [
            new CodeSuggestionHunk("r", 0, 1, "A", "A2"),
            new CodeSuggestionHunk("d", 1, 1),
            new CodeSuggestionHunk("i", 3, 0, "z"),
        ], lines.Length);
        CodeSuggestionDiff.Apply(lines, spans, _ => true).ShouldBe("A\nA2\nc\nz");
        CodeSuggestionDiff.Apply(lines, spans, s => s.Id == "d").ShouldBe("a\nc");
        CodeSuggestionDiff.Apply(lines, spans, _ => false).ShouldBe("a\nb\nc");
        CodeSuggestionDiff.Count(spans, _ => true).ShouldBe((3, 2));
    }

    [Theory]
    [InlineData("fetch-invoice.ts", "ts", "TS")]
    [InlineData("App.tsx", "tsx", "TS")]
    [InlineData("main.py", "py", "PY")]
    [InlineData("Program.cs", "cs", "C#")]
    [InlineData("Makefile", "", "")]
    public void Language_And_Monogram_Come_From_The_Extension(string file, string language, string monogram)
    {
        CodeSuggestionDiff.LanguageOf(file).ShouldBe(language);
        CodeSuggestionDiff.MonogramOf(file).ShouldBe(monogram);
    }

    [Fact]
    public void Default_Labels_Read_Naturally()
    {
        var labels = CodeSuggestionLabels.Default;
        labels.CardName("Add retry with backoff").ShouldBe("Code suggestion: Add retry with backoff");
        labels.ToReview(1).ShouldBe("1 change to review");
        labels.ToReview(3).ShouldBe("3 changes to review");
        labels.Applied(2, 3).ShouldBe("Applied 2 of 3 changes");
        labels.Applied(3, 3).ShouldBe("Applied all 3 changes");
        labels.RejectedAll(3).ShouldBe("Rejected all 3 changes");
        labels.AcceptHunk("Add retry limits").ShouldBe("Accept Add retry limits");
        labels.AcceptedAnnouncement("Add retry limits").ShouldBe("Accepted: Add retry limits.");
    }

    [Theory]
    [InlineData(CodeSuggestionRowKind.Removed, CodeSuggestionDecision.Pending, 1, 1)]
    [InlineData(CodeSuggestionRowKind.Removed, CodeSuggestionDecision.Accepted, 0, 1)]
    [InlineData(CodeSuggestionRowKind.Removed, CodeSuggestionDecision.Rejected, 1, 0)]
    [InlineData(CodeSuggestionRowKind.Added, CodeSuggestionDecision.Accepted, 1, 0)]
    [InlineData(CodeSuggestionRowKind.Added, CodeSuggestionDecision.Rejected, 0, 1)]
    [InlineData(CodeSuggestionRowKind.Context, CodeSuggestionDecision.Pending, 1, 0)]
    internal void Rows_Fold_And_Fade_By_Decision(CodeSuggestionRowKind kind, CodeSuggestionDecision decision, double open, double tint) =>
        CodeSuggestion.TargetOf(kind, decision).ShouldBe((open, tint));

    // The empty card and the simulation ----------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Card_Without_Data_Or_A_Simulation_Shows_An_Empty_File()
    {
        var (card, window) = Open(level: Motion.ArcMotionLevel.Full);
        try
        {
            card.UsesSample.ShouldBeFalse();
            card.EffectiveCode.ShouldBeEmpty();
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Ready);
            card.HunkStates.Count.ShouldBe(0);
            card.PendingCount.ShouldBe(0);
            card.RunCount.ShouldBe(0);
            card.HasPendingBeat.ShouldBeFalse();
            card.Instruction.ShouldBeEmpty();
            card.FileName.ShouldBeEmpty();
            card.Model.ShouldBeNull();
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.ModelPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Shows_The_Finished_Simulation_Without_Full_Motion()
    {
        var (card, window) = Open(Simulated);
        try
        {
            card.UsesSample.ShouldBeTrue();
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Ready);
            card.PendingCount.ShouldBe(3);
            card.HunkStates.Count.ShouldBe(3);
            card.StatusText.ShouldBe("3 changes to review");
            card.Actions!.Kind.ShouldBe(CodeSuggestionActionsKind.Review);
            AutomationProperties.GetName(card).ShouldBe("Code suggestion: Add retry with backoff");
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.ModelPart).Text.ShouldBe("Model X");
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.AddedCountPart).Text.ShouldBe("+16");
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.RemovedCountPart).Text.ShouldBe("−4");
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.FolderPart).IsVisible.ShouldBeTrue();
            TestHelpers.Descendant<CodeSuggestionFileMark>(card, CodeSuggestion.MonogramPart).Text.ShouldBe("TS");
            card.View!.Rows.Count.ShouldBe(13 + 16);
            card.HasPendingBeat.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bars_Name_Their_Hunk_And_Rows_Carry_The_Hidden_Words()
    {
        var (card, window) = Open(Simulated);
        try
        {
            var bar = card.HunkStates[1].Bar;
            AutomationProperties.GetName(bar.AcceptButton!).ShouldBe("Accept Retry 429 and 5xx with backoff");
            AutomationProperties.GetName(bar.RejectButton!).ShouldBe("Reject Retry 429 and 5xx with backoff");
            var text = card.AccessibleCode();
            text.ShouldContain("Removed:   const res = await fetch(url);");
            text.ShouldContain("Added: const MAX_ATTEMPTS = 4;");
            AutomationProperties.GetName(card.CodeArea!).ShouldBe("fetch-invoice.ts");
        }
        finally
        {
            window.Close();
        }
    }

    // Decisions -----------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Accepting_Raises_The_Decision_With_The_File_As_It_Stands()
    {
        var (card, window) = Open(Given);
        try
        {
            card.UsesSample.ShouldBeFalse();
            CodeSuggestionDecisionEventArgs? args = null;
            card.DecisionMade += (_, e) => args = e;
            card.Accept("one").ShouldBeTrue();
            args!.Hunk.Id.ShouldBe("one");
            args.Decision.ShouldBe(CodeSuggestionDecision.Accepted);
            args.Code.ShouldBe("a\nB\nc\nd\ne");
            card.GetDecision("one").ShouldBe(CodeSuggestionDecision.Accepted);
            card.Accept("one").ShouldBeFalse();
            card.PendingCount.ShouldBe(1);
            card.StatusText.ShouldBe("1 change to review");
            card.Announcer!.Announcement.ShouldBe("Accepted: Capitalize b. 1 change to review");

            var removed = card.View!.Rows.Single(r => r.Kind == CodeSuggestionRowKind.Removed);
            removed.OpenTarget.ShouldBe(0);
            removed.Open.Value.ShouldBe(0);
            var added = card.View.Rows.First(r => r.Kind == CodeSuggestionRowKind.Added);
            added.TintTarget.ShouldBe(0);
            added.Number.ShouldBe("2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resolving_Every_Hunk_Raises_Resolved_Once()
    {
        var (card, window) = Open(Given);
        try
        {
            var resolved = new List<CodeSuggestionResolvedEventArgs>();
            card.Resolved += (_, e) => resolved.Add(e);
            card.Reject("two").ShouldBeTrue();
            resolved.ShouldBeEmpty();
            card.Accept("one").ShouldBeTrue();
            resolved.Count.ShouldBe(1);
            resolved[0].Code.ShouldBe("a\nB\nc\nd\ne");
            resolved[0].Accepted.ShouldBe(["one"]);
            resolved[0].Rejected.ShouldBe(["two"]);
            card.StatusText.ShouldBe("Applied 1 of 2 changes");
            card.Actions.ShouldBeNull();
            card.AllowsRegenerate = true;
            card.Actions!.Kind.ShouldBe(CodeSuggestionActionsKind.Regenerate);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Accept_All_Raises_One_Decision_Per_Hunk()
    {
        var (card, window) = Open(Given);
        try
        {
            var decisions = new List<CodeSuggestionDecisionEventArgs>();
            card.DecisionMade += (_, e) => decisions.Add(e);
            card.AcceptAll().ShouldBe(2);
            decisions.Select(d => d.Hunk.Id).ShouldBe(["one", "two"]);
            decisions[0].Code.ShouldBe("a\nB\nc\nd\ne");
            decisions[1].Code.ShouldBe("a\nB\nc\nx\ny\nd\ne");
            card.GetCode().ShouldBe("a\nB\nc\nx\ny\nd\ne");
            card.StatusText.ShouldBe("Applied all 2 changes");
            card.AcceptAll().ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Command_Enter_Accepts_All_And_Command_Backspace_Rejects_All_Inside_The_Card()
    {
        var (card, window) = Open(Given);
        try
        {
            card.CodeArea!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.Control);
            card.GetDecision("one").ShouldBe(CodeSuggestionDecision.Rejected);
            card.GetDecision("two").ShouldBe(CodeSuggestionDecision.Rejected);
            card.StatusText.ShouldBe("Rejected all 2 changes");
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.AddedCountPart).Text.ShouldBe("+0");

            card.Status = CodeSuggestionStatus.Thinking;
            card.GetDecision("one").ShouldBe(CodeSuggestionDecision.Pending);
            card.Status = CodeSuggestionStatus.Ready;
            card.CodeArea.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Meta);
            card.GetDecision("one").ShouldBe(CodeSuggestionDecision.Accepted);
            card.GetDecision("two").ShouldBe(CodeSuggestionDecision.Accepted);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shortcuts_Can_Be_Turned_Off()
    {
        var (card, window) = Open(c =>
        {
            Given(c);
            c.AreShortcutsEnabled = false;
        });
        try
        {
            card.CodeArea!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
            card.PendingCount.ShouldBe(2);
            card.Actions!.ShowsHint.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_Accept_With_The_Keyboard_Moves_Focus_To_The_Next_Pending_Accept()
    {
        var (card, window) = Open(Given);
        try
        {
            var first = card.HunkStates[0].Bar;
            first.AcceptButton!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            card.GetDecision("one").ShouldBe(CodeSuggestionDecision.Accepted);
            card.HunkStates[1].Bar.AcceptButton!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            card.GetDecision("two").ShouldBe(CodeSuggestionDecision.Accepted);
            card.CodeArea!.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Scroll_Long_Lines_In_The_Code_Area()
    {
        var (card, window) = Open(c =>
        {
            c.Code = "short\n" + new string('x', 400);
            c.Hunks.Add(new CodeSuggestionHunk("h", 0, 1, "SHORT"));
        });
        try
        {
            var scroll = TestHelpers.Descendant<ScrollViewer>(card, CodeSuggestion.ScrollPart);
            scroll.Extent.Width.ShouldBeGreaterThan(scroll.Viewport.Width);
            card.CodeArea!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.X.ShouldBe(40);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    // Status --------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Thinking_Shows_The_Plain_File_And_Stop()
    {
        var (card, window) = Open(c =>
        {
            Given(c);
            c.AllowsStop = true;
            c.Status = CodeSuggestionStatus.Thinking;
        });
        try
        {
            card.HunkStates.Count.ShouldBe(0);
            card.View!.Rows.Count.ShouldBe(5);
            card.View.Rows.ShouldAllBe(r => r.Kind == CodeSuggestionRowKind.Context);
            card.StatusText.ShouldBe("Reading notes.txt");
            card.Actions!.Kind.ShouldBe(CodeSuggestionActionsKind.Stop);
            var stops = 0;
            card.StopRequested += (_, _) => stops++;
            card.Actions.Button(CodeSuggestionActions.StopPart)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            stops.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streaming_Shows_Hunks_Without_Bars()
    {
        var (card, window) = Open(c =>
        {
            Given(c);
            c.Status = CodeSuggestionStatus.Streaming;
        });
        try
        {
            card.HunkStates.Count.ShouldBe(2);
            card.HunkStates.ShouldAllBe(s => s.BarTarget == 0);
            card.StatusText.ShouldBe("Writing changes");
            card.Accept("one").ShouldBeFalse();
            card.Hunks[1].AppendLine("z");
            card.View!.Rows.Count(r => r.Kind == CodeSuggestionRowKind.Added).ShouldBe(4);
            card.Status = CodeSuggestionStatus.Ready;
            card.HunkStates.ShouldAllBe(s => s.BarTarget == 1);
            card.Announcer!.Announcement.ShouldBe("2 changes to review");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Shows_The_Message_And_Retry()
    {
        var (card, window) = Open(c =>
        {
            Given(c);
            c.AllowsRegenerate = true;
            c.Status = CodeSuggestionStatus.Error;
        });
        try
        {
            card.HunkStates.Count.ShouldBe(0);
            card.StatusText.ShouldBe("The model stopped responding. Nothing was changed.");
            card.Actions!.Kind.ShouldBe(CodeSuggestionActionsKind.Retry);
            card.Classes.Contains(CodeSuggestion.ErrorPseudoClass).ShouldBeTrue();
            var regenerates = 0;
            card.RegenerateRequested += (_, _) => regenerates++;
            card.Actions.Button(CodeSuggestionActions.RetryPart)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            regenerates.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Model_Null_Hides_The_Model_Line()
    {
        var (card, window) = Open(c =>
        {
            Given(c);
            c.Model = null;
            c.ShowsFileMark = false;
        });
        try
        {
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.ModelPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<CodeSuggestionFileMark>(card, CodeSuggestion.MonogramPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.FolderPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // Layout --------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Narrow_Card_Hides_Summaries_And_Stacks_The_Footer()
    {
        var (card, window) = Open(Simulated, width: 400);
        try
        {
            card.IsNarrow.ShouldBeTrue();
            card.IsCompact.ShouldBeTrue();
            card.HunkStates.ShouldAllBe(s => s.Bar.IsCompact);
            card.Actions!.IsStacked.ShouldBeTrue();
            card.View!.FontSize.ShouldBe(12);
            TestHelpers.Descendant<TextBlock>(card, CodeSuggestion.FolderPart).IsVisible.ShouldBeFalse();
            card.Bounds.Width.ShouldBeLessThanOrEqualTo(400);

            window.Width = 900;
            Dispatcher.UIThread.RunJobs();
            card.Bounds.Width.ShouldBe(720);
            card.IsNarrow.ShouldBeFalse();
            card.View.FontSize.ShouldBe(13);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Max_Code_Height_Scrolls_The_Code()
    {
        var (card, window) = Open(c =>
        {
            Simulated(c);
            c.MaxCodeHeight = 120;
        });
        try
        {
            var scroll = TestHelpers.Descendant<ScrollViewer>(card, CodeSuggestion.ScrollPart);
            scroll.Bounds.Height.ShouldBeLessThanOrEqualTo(120);
            scroll.Extent.Height.ShouldBeGreaterThan(120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Code_Is_Highlighted_With_The_Library_Highlighter()
    {
        var (card, window) = Open(Simulated);
        try
        {
            var view = card.View!;
            await view.Original.WhenHighlighted;
            await view.Proposed.WhenHighlighted;
            Dispatcher.UIThread.RunJobs();
            var tokens = view.Original.TokensFor(0, "import { db } from \"./db\";");
            tokens.ShouldNotBeNull();
            tokens.Value.ShouldContain(t => t.Foreground != 0);
            view.Proposed.TokensFor(3, "const MAX_ATTEMPTS = 4;").ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    // Simulation ----------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Sample_Plays_Streams_And_Reviews_Itself_At_Full_Motion()
    {
        var clock = new AgentStatusTestClock();
        var (card, window) = Open(c => Simulated(c, clock), level: Motion.ArcMotionLevel.Full);
        try
        {
            TestHelpers.PumpUntil(() => card.RunCount == 1, 2000).ShouldBeTrue();
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Thinking);
            card.StatusText.ShouldBe("Reading fetch-invoice.ts");
            card.Actions!.Kind.ShouldBe(CodeSuggestionActionsKind.Stop);

            Step(clock, CodeSuggestion.SampleThinking);
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Streaming);
            card.HunkStates.Count.ShouldBe(1);
            Step(clock, CodeSuggestion.SampleHunkInterval);
            card.View!.Rows.Count(r => r.Kind == CodeSuggestionRowKind.Added).ShouldBe(1);

            // Three hunks and 16 lines stream in about 1.45s.
            Step(clock, TimeSpan.FromMilliseconds(1300));
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Ready);
            card.PendingCount.ShouldBe(3);

            // Run 1 reviews one hunk at a time: accept, reject, accept.
            Step(clock, CodeSuggestion.SampleFirstDecision);
            card.GetDecision("limits").ShouldBe(CodeSuggestionDecision.Accepted);
            Step(clock, CodeSuggestion.SampleDecisionInterval);
            card.GetDecision("backoff").ShouldBe(CodeSuggestionDecision.Rejected);
            Step(clock, CodeSuggestion.SampleDecisionInterval);
            card.GetDecision("sleep").ShouldBe(CodeSuggestionDecision.Accepted);
            card.StatusText.ShouldBe("Applied 2 of 3 changes");

            Step(clock, CodeSuggestion.SampleReplay);
            card.RunCount.ShouldBe(2);
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Thinking);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Touching_The_Card_Hands_The_Review_To_The_Person_And_Stop_Keeps_Finished_Hunks()
    {
        var clock = new AgentStatusTestClock();
        var (card, window) = Open(c => Simulated(c, clock), level: Motion.ArcMotionLevel.Full);
        try
        {
            TestHelpers.PumpUntil(() => card.RunCount == 1, 2000).ShouldBeTrue();
            Step(clock, CodeSuggestion.SampleThinking);
            // Hunk 1 (3 lines) streams, hunk 2 has started.
            Step(clock, CodeSuggestion.SampleHunkInterval + 4 * CodeSuggestion.SampleLineInterval + CodeSuggestion.SampleHunkInterval
                + CodeSuggestion.SampleLineInterval);
            card.HunkStates.Count.ShouldBe(2);

            card.CodeArea!.Focus(NavigationMethod.Tab);
            card.PersonInControl.ShouldBeTrue();
            card.Actions!.Button(CodeSuggestionActions.StopPart)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Ready);
            card.HunkStates.Count.ShouldBe(1);
            card.HunkStates[0].Id.ShouldBe("limits");

            Step(clock, TimeSpan.FromSeconds(10));
            card.GetDecision("limits").ShouldBe(CodeSuggestionDecision.Pending);
            card.HasPendingBeat.ShouldBeFalse();

            card.Accept("limits");
            card.Actions!.Kind.ShouldBe(CodeSuggestionActionsKind.Regenerate);
            card.Announcer!.Announcement.ShouldBe("Applied 1 change. Simulated, nothing was written to disk.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Giving_Code_Wins_Over_The_Simulation()
    {
        var clock = new AgentStatusTestClock();
        var (card, window) = Open(c => Simulated(c, clock), level: Motion.ArcMotionLevel.Full);
        try
        {
            TestHelpers.PumpUntil(() => card.RunCount == 1, 2000).ShouldBeTrue();
            Given(card);
            card.UsesSample.ShouldBeFalse();
            card.HasPendingBeat.ShouldBeFalse();
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Ready);
            card.HunkStates.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Simulated(CodeSuggestion card, AgentStatusTestClock clock)
    {
        card.Clock = clock;
        Simulated(card);
    }

    [AvaloniaFact]
    public void Removing_The_Simulation_Stops_It_And_Restart_Plays_It_Again()
    {
        var clock = new AgentStatusTestClock();
        var (card, window) = Open(c => Simulated(c, clock), level: Motion.ArcMotionLevel.Full);
        try
        {
            TestHelpers.PumpUntil(() => card.RunCount == 1, 2000).ShouldBeTrue();
            Step(clock, CodeSuggestion.SampleThinking);
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Streaming);

            // Restart starts a new run from the plain file.
            card.Simulation!.Restart();
            card.RunCount.ShouldBe(2);
            card.EffectiveStatus.ShouldBe(CodeSuggestionStatus.Thinking);
            card.HunkStates.Count.ShouldBe(0);

            var simulation = card.Simulation;
            card.Simulation = null;
            card.UsesSample.ShouldBeFalse();
            card.HasPendingBeat.ShouldBeFalse();
            card.EffectiveCode.ShouldBeEmpty();
            card.HunkStates.Count.ShouldBe(0);
            Step(clock, TimeSpan.FromSeconds(3));
            card.RunCount.ShouldBe(2);
            simulation.Restart();
            card.RunCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Step(AgentStatusTestClock clock, TimeSpan span)
    {
        // Lines are 38ms apart: advance in small steps so each beat runs on the UI thread before the next falls due.
        var step = TimeSpan.FromMilliseconds(10);
        for (var t = TimeSpan.Zero; t < span; t += step)
        {
            clock.Advance(step);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
