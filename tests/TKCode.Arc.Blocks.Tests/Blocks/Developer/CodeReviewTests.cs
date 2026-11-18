using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using ArcButton = TKCode.Arc.Button;

namespace TKCode.Arc.Tests.Blocks.Developer;

/// <summary>
/// <see cref="CodeReview"/>: the diff rules (numbers, hunk headers, applying suggestions), then the card on its sample
/// review — tabs and progress, comment boxes and pending threads, replies, resolving, applying, viewed marks, approving
/// and withdrawing, the keyboard model and the compact layout.
/// </summary>
public class CodeReviewTests
{
    // ---- Diff rules ----

    private static List<CodeReviewDiffLine> Sample() =>
    [
        CodeReviewDiffLine.Hunk("h1", 10, 12, "function run("),
        CodeReviewDiffLine.Context("a", "one"),
        CodeReviewDiffLine.Deleted("b", "two"),
        CodeReviewDiffLine.Added("c", "deux"),
        CodeReviewDiffLine.Added("d", "trois"),
        CodeReviewDiffLine.Context("e", "four")
    ];

    [Fact]
    public void Rows_Number_Both_Sides_And_Write_The_Hunk_Header()
    {
        var rows = CodeReviewDiff.Rows(Sample());
        rows[0].HunkHeader.ShouldBe("@@ −10,3 +12,4 @@");
        rows[1].OldNumber.ShouldBe(10);
        rows[1].NewNumber.ShouldBe(12);
        rows[2].OldNumber.ShouldBe(11);
        rows[2].NewNumber.ShouldBe(0);
        rows[2].Number.ShouldBe(11);
        rows[3].NewNumber.ShouldBe(13);
        rows[4].NewNumber.ShouldBe(14);
        rows[5].OldNumber.ShouldBe(12);
        rows[5].NewNumber.ShouldBe(15);
    }

    [Fact]
    public void A_New_File_Hunk_Starts_The_Old_Side_At_Zero()
    {
        var rows = CodeReviewDiff.Rows([CodeReviewDiffLine.Hunk("h", 0, 1), CodeReviewDiffLine.Added("a", "x"), CodeReviewDiffLine.Added("b", "y")]);
        rows[0].HunkHeader.ShouldBe("@@ −0,0 +1,2 @@");
        CodeReviewDiff.Stat(Sample()).ShouldBe((2, 1));
    }

    [Fact]
    public void Applying_Swaps_Added_Lines_In_Place()
    {
        var lines = CodeReviewDiff.Apply(Sample(), new CodeReviewSuggestion { LineIds = ["c", "d"], NewLines = ["deux!"] }, "s", out var ids)!;
        lines.Select(l => l.Id).ShouldBe(["h1", "a", "b", "s-1", "e"]);
        lines[3].Kind.ShouldBe(CodeReviewLineKind.Added);
        lines[3].Text.ShouldBe("deux!");
        ids.ShouldBe(["s-1"]);
    }

    [Fact]
    public void Applying_To_An_Unchanged_Line_Removes_It_And_Adds_The_New_Text_After()
    {
        var lines = CodeReviewDiff.Apply(Sample(), new CodeReviewSuggestion { LineIds = ["e"], NewLines = ["four", "five"] }, "s", out _)!;
        lines.Select(l => (l.Id, l.Kind)).TakeLast(3).ShouldBe([("e", CodeReviewLineKind.Deleted), ("s-1", CodeReviewLineKind.Added), ("s-2", CodeReviewLineKind.Added)]);
        CodeReviewDiff.Rows(lines)[0].HunkHeader.ShouldBe("@@ −10,3 +12,5 @@");
    }

    [Fact]
    public void Applying_Refuses_Removed_Or_Missing_Lines()
    {
        CodeReviewDiff.Apply(Sample(), new CodeReviewSuggestion { LineIds = ["b"], NewLines = ["x"] }, "s", out _).ShouldBeNull();
        CodeReviewDiff.Apply(Sample(), new CodeReviewSuggestion { LineIds = ["zz"], NewLines = ["x"] }, "s", out _).ShouldBeNull();
        CodeReviewDiff.OldText(Sample(), new CodeReviewSuggestion { LineIds = ["c", "d"] }).ShouldBe(["deux", "trois"]);
    }

    // ---- The card ----

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 760, Action<CodeReview>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off)
        {
            Review = new CodeReview { TimingScale = 0 };
            configure?.Invoke(Review);
            Host = new Border { Width = width, Child = Review, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = 1000;
            Window.Height = 1600;
            Pump();
        }

        public CodeReview Review { get; }

        public Border Host { get; }

        public Window Window { get; }

        public void Pump()
        {
            for (var i = 0; i < 4; i++)
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public CodeReviewDiffView Diff => Review.CurrentPanel!.Diff;

        public CodeReviewLineRow Row(int number) => Diff.Rows.First(r => !r.IsHunk && r.Row.Number == number);

        public ArcButton Button(string label) =>
            Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(Review).OfType<ArcButton>()
                .First(b => b.Content as string == label && b.IsEffectivelyVisible);

        public void Click(string label)
        {
            Button(label).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
        }

        public CodeReviewThread Thread(string id) => Review.CurrentPanel!.Diff.ThreadViews.Select(v => v.Thread).First(t => t?.Id == id)!;

        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public void The_Sample_Opens_On_The_First_File_With_An_Open_Conversation()
    {
        using var rig = new Rig();
        rig.Review.SelectedFileId.ShouldBe("limiter");
        rig.Review.TabStrip!.Tabs.Count.ShouldBe(5);
        rig.Review.TabStrip.Tabs[0].OpenCount.ShouldBe(1);
        AutomationProperties.GetName(rig.Review.TabStrip.Tabs[0]).ShouldBe("src/mail/limiter.ts, 1 open conversation");
        rig.Review.Progress!.Count.ShouldBe(0);
        AutomationProperties.GetName(rig.Review).ShouldBe("Review of pull request 3172");
        rig.Diff.Rows.Count(r => !r.IsHunk).ShouldBe(11);
        AutomationProperties.GetName(rig.Row(9)).ShouldBe("Comment on added line 9");
        // The resolved thread is folded, the open one shows its suggestion.
        rig.Diff.ThreadViews.Count().ShouldBe(2);
        rig.Button("Apply suggestion").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Choosing_A_Tab_Shows_Its_File_And_Reports_It()
    {
        using var rig = new Rig();
        var changes = new List<CodeReviewSelectionChangedEventArgs>();
        rig.Review.SelectionChanged += (_, e) => changes.Add(e);
        rig.Review.TabStrip!.Tabs[1].PerformClick();
        rig.Pump();
        rig.Review.SelectedFileId.ShouldBe("send");
        rig.Review.CurrentPanel!.File.Id.ShouldBe("send");
        changes.Single().OldFile!.Id.ShouldBe("limiter");
        rig.Review.SelectedFileId = "docs";
        rig.Pump();
        rig.Review.CurrentPanel!.File.Id.ShouldBe("docs");
        rig.Diff.Rows.First(r => r.Row.Kind == CodeReviewLineKind.Deleted).Row.OldNumber.ShouldBe(29);
    }

    [AvaloniaFact]
    public void A_Line_Number_Opens_A_Comment_Box_And_Posting_Adds_A_Pending_Thread()
    {
        using var rig = new Rig();
        var added = new List<CodeReviewThreadEventArgs>();
        rig.Review.CommentAdded += (_, e) => added.Add(e);
        var row = rig.Row(2);
        row.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Pump();
        row.IsActive.ShouldBeTrue();
        var box = rig.Diff.ComposerAt(row.Row.Line.Id)!;
        box.Input!.Text = "Could this come from the plan?";
        rig.Pump();
        rig.Click("Comment");
        var thread = added.Single().Thread;
        thread.IsPending.ShouldBeTrue();
        thread.Label.ShouldBe("Line 2");
        thread.AnchorLineId.ShouldBe(row.Row.Line.Id);
        thread.Comments.Single().Author.Name.ShouldBe("Mia Laurent");
        row.IsActive.ShouldBeFalse();
        rig.Review.TabStrip!.Tabs[0].OpenCount.ShouldBe(2);
        rig.Review.Announcer!.Announcement.ShouldBe("Comment added on line 2. It is pending until you approve.");
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Box_And_Returns_To_The_Line_Number()
    {
        using var rig = new Rig();
        var row = rig.Row(3);
        rig.Diff.ToggleComposer(row);
        rig.Pump();
        var box = rig.Diff.ComposerAt(row.Row.Line.Id)!;
        box.Input!.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        rig.Pump();
        rig.Diff.ComposerAt(row.Row.Line.Id).ShouldBeNull();
        row.IsFocused.ShouldBeTrue();
        row.IsActive.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Ctrl_Enter_Posts_A_Suggested_Change()
    {
        using var rig = new Rig();
        CodeReviewThread? thread = null;
        rig.Review.CommentAdded += (_, e) => thread = e.Thread;
        var row = rig.Row(1);
        rig.Diff.ToggleComposer(row);
        rig.Pump();
        var toggle = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(rig.Review).OfType<CodeReviewToggle>()
            .First(t => AutomationProperties.GetName(t) == "Suggest a change");
        toggle.PerformClick();
        rig.Pump();
        var box = rig.Diff.ComposerAt(row.Row.Line.Id)!;
        box.Input!.Text = "Use a minute constant.";
        box.Input.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        rig.Pump();
        thread.ShouldNotBeNull();
        thread.Suggestion.ShouldNotBeNull();
        thread.Suggestion!.NewLines.ShouldBe(["export const WINDOW_MS = 60_000;"]);
    }

    [AvaloniaFact]
    public void Applying_A_Suggestion_Rewrites_The_Diff_And_Resolves_The_Thread()
    {
        using var rig = new Rig();
        var applied = new List<CodeReviewSuggestionAppliedEventArgs>();
        var resolved = new List<CodeReviewThreadEventArgs>();
        rig.Review.SuggestionApplied += (_, e) => applied.Add(e);
        rig.Review.ResolvedChanged += (_, e) => resolved.Add(e);
        rig.Click("Apply suggestion");
        var e = applied.Single();
        e.Thread.IsSuggestionApplied.ShouldBeTrue();
        e.Thread.IsResolved.ShouldBeTrue();
        resolved.Single().IsResolved.ShouldBeTrue();
        var line9 = e.Lines.Where(l => l.Kind != CodeReviewLineKind.Hunk).ElementAt(8);
        line9.Text.ShouldContain("Math.min(");
        line9.Kind.ShouldBe(CodeReviewLineKind.Added);
        e.Thread.AnchorLineId.ShouldBe(line9.Id);
        rig.Row(9).Row.Line.Text.ShouldContain("Math.min(");
        rig.Diff.Rows.Count(r => !r.IsHunk).ShouldBe(11);
        rig.Review.TabStrip!.Tabs[0].OpenCount.ShouldBe(0);
        rig.Review.Announcer!.Announcement.ShouldBe("Conversation resolved");
    }

    [AvaloniaFact]
    public void Resolve_Folds_The_Thread_And_Reopen_Opens_It_Again()
    {
        using var rig = new Rig();
        var changes = new List<bool>();
        rig.Review.ResolvedChanged += (_, e) => changes.Add(e.IsResolved);
        rig.Click("Resolve");
        var thread = rig.Thread("refill-cap");
        thread.IsResolved.ShouldBeTrue();
        rig.Review.TabStrip!.Tabs[0].OpenCount.ShouldBe(0);
        // Unfold the summary, then reopen.
        var folded = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(rig.Review).OfType<CodeReviewPressable>()
            .Where(p => p.Height == 36).ToList();
        folded.Count.ShouldBe(2);
        folded[1].PerformClick();
        rig.Pump();
        rig.Click("Reopen");
        thread.IsResolved.ShouldBeFalse();
        changes.ShouldBe([true, false]);
        rig.Review.Announcer!.Announcement.ShouldBe("Conversation reopened");
    }

    [AvaloniaFact]
    public void A_Reply_Is_Added_To_The_Thread()
    {
        using var rig = new Rig();
        var replies = new List<CodeReviewReplyEventArgs>();
        rig.Review.ReplyPosted += (_, e) => replies.Add(e);
        rig.Click("Reply");
        var view = rig.Diff.ThreadViews.First(v => v.Thread?.Id == "refill-cap");
        view.Input!.Text = "Capped at one window.";
        rig.Click("Reply");
        replies.Single().Comment.Text.ShouldBe("Capped at one window.");
        rig.Thread("refill-cap").Comments.Count.ShouldBe(2);
        view.Input.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Marking_Files_Viewed_Fills_The_Progress()
    {
        using var rig = new Rig();
        var events = new List<CodeReviewViewedChangedEventArgs>();
        rig.Review.ViewedChanged += (_, e) => events.Add(e);
        rig.Review.CurrentPanel!.ViewedToggle.PerformClick();
        rig.Pump();
        rig.Review.ViewedCount.ShouldBe(1);
        rig.Review.Progress!.FillScale.ShouldBe(0.2, 0.001);
        events.Single().IsViewed.ShouldBeTrue();
        AutomationProperties.GetName(rig.Review.TabStrip!.Tabs[0]).ShouldStartWith("src/mail/limiter.ts, viewed");
        rig.Review.SetFileViewed("send", true);
        rig.Pump();
        rig.Review.ViewedCount.ShouldBe(2);
        rig.Review.Announcer!.Announcement.ShouldBe("src/mail/send.ts marked viewed");
    }

    [AvaloniaFact]
    public async Task Approve_Sends_The_Pending_Comments_And_Undo_Withdraws()
    {
        using var rig = new Rig();
        CodeReviewSubmission? sent = null;
        rig.Review.SubmitReviewAction = (s, _) =>
        {
            sent = s;
            return Task.CompletedTask;
        };
        rig.Diff.ToggleComposer(rig.Row(2));
        rig.Pump();
        rig.Diff.ComposerAt(rig.Row(2).Row.Line.Id)!.Input!.Text = "One thing.";
        rig.Click("Comment");
        await rig.Review.Approve();
        rig.Pump();
        rig.Review.IsApproved.ShouldBeTrue();
        sent!.Comments.Count.ShouldBe(1);
        sent.Comments[0].IsPending.ShouldBeFalse();
        rig.Review.StatusBand!.IsExpanded.ShouldBeTrue();
        rig.Review.Announcer!.Announcement.ShouldBe("You approved these changes with 1 comment");
        rig.Review.Classes.Contains(":approved").ShouldBeTrue();
        rig.Review.Withdraw();
        rig.Pump();
        rig.Review.IsApproved.ShouldBeFalse();
        rig.Review.StatusBand.IsExpanded.ShouldBeFalse();
        rig.Review.ApproveButton!.Content.ShouldBe("Approve");
        rig.Review.Announcer.Announcement.ShouldBe("Approval withdrawn");
    }

    [AvaloniaFact]
    public async Task A_Failed_Approval_Offers_Try_Again()
    {
        using var rig = new Rig(configure: r => r.SubmitReviewAction = (_, _) => Task.FromException(new InvalidOperationException("offline")));
        await rig.Review.Approve();
        rig.Pump();
        rig.Review.IsApproved.ShouldBeFalse();
        rig.Review.ApproveButton!.Content.ShouldBe("Try again");
        rig.Review.ApproveButton.IsLoading.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Tabs_And_Lines()
    {
        using var rig = new Rig();
        var tab = rig.Review.TabStrip!.Tabs[0];
        tab.IsTabStop.ShouldBeTrue();
        rig.Review.TabStrip.Tabs[1].IsTabStop.ShouldBeFalse();
        tab.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        rig.Pump();
        rig.Review.SelectedFileId.ShouldBe("send");
        rig.Window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rig.Pump();
        rig.Review.SelectedFileId.ShouldBe("docs");

        rig.Review.SelectedFileId = "limiter";
        rig.Pump();
        var first = rig.Row(1);
        first.IsTabStop.ShouldBeTrue();
        first.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rig.Pump();
        rig.Row(2).IsFocused.ShouldBeTrue();
        rig.Row(2).IsTabStop.ShouldBeTrue();
        rig.Window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rig.Pump();
        rig.Row(11).IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Below_560_The_Card_Is_Compact()
    {
        using var rig = new Rig(width: 358);
        rig.Review.Classes.Contains(":compact").ShouldBeTrue();
        rig.Row(1).IsCompact.ShouldBeTrue();
        rig.Row(1).GutterWidth.ShouldBe(42.5);
        rig.Host.Width = 760;
        rig.Pump();
        rig.Review.Classes.Contains(":compact").ShouldBeFalse();
        rig.Row(1).GutterWidth.ShouldBe(83);
    }

    [AvaloniaFact]
    public void Given_Data_Replaces_The_Sample()
    {
        var file = new CodeReviewFile
        {
            Id = "f",
            Path = "lib/a.cs",
            Language = "csharp",
            Lines = [CodeReviewDiffLine.Hunk("h", 1, 1), CodeReviewDiffLine.Context("1", "int a;"), CodeReviewDiffLine.Added("2", "int b;")]
        };
        var threads = new List<CodeReviewThread>();
        using var rig = new Rig(configure: r =>
        {
            r.Pull = new CodeReviewPullRequest { Title = "Tidy", Number = 7, Author = new CodeReviewPerson("Ana Ruiz"), HeadBranch = "ana/tidy", CommitCount = 1 };
            r.Files = [file];
            r.Threads = threads;
            r.Viewer = new CodeReviewPerson("Kai Berg");
        });
        rig.Review.SelectedFileId.ShouldBe("f");
        rig.Diff.ToggleComposer(rig.Row(2));
        rig.Pump();
        rig.Diff.ComposerAt("2")!.Input!.Text = "Why b?";
        rig.Click("Comment");
        threads.Single().Comments[0].Author.Name.ShouldBe("Kai Berg");
        AutomationProperties.GetName(rig.Review).ShouldBe("Review of pull request 7");
    }

    [AvaloniaFact]
    public void Motion_Grows_A_New_Band_From_Zero()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var rig = new Rig(level: ArcMotionLevel.Full);
        var row = rig.Row(2);
        rig.Diff.ToggleComposer(row);
        rig.Pump();
        var band = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(rig.Review).OfType<CodeReviewBand>()
            .First(b => b.Child is CodeReviewThreadView { IsComposer: true });
        band.CurrentHeight.ShouldBeLessThan(1);
    }
}
