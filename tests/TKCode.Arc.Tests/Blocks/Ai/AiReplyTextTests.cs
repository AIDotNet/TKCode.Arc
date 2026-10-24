using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiReplyText"/>: whole-word pacing on the frame clock, completion and stop, finished text, the fade levels,
/// citations held back while words fade, sources, hover, the preview card, pointer and keyboard invocation.
/// </summary>
public class AiReplyTextTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private const string Prose =
        "Compare the stage timings before and after the jump. A cache that stopped restoring usually shows up as a slow " +
        "restore step and a long compile, not as a slow test stage. Pin the image next. ";

    private static (AiReplyText Reply, Window Window) Show(ArcMotionLevel level = ArcMotionLevel.Off, Control? before = null)
    {
        var reply = new AiReplyText();
        Control content = reply;
        if (before is not null)
        {
            content = new StackPanel { Children = { before, reply } };
        }
        var window = TestHelpers.Show(content, level);
        window.Width = 640;
        window.Height = 600;
        Settle(window);
        return (reply, window);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs <paramref name="count"/> 60 Hz frames, letting the dispatcher run between them as an app would.</summary>
    private static void Frames(ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task Until(Window window, Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(10);
            Settle(window);
        }
        condition().ShouldBeTrue();
    }

    private static int Words(string text) => text.Split((char[])[' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Keyed(string text)
    {
        var scanner = new AiCitationText.Scanner { EmitsKeys = true };
        return scanner.Push(text) + scanner.Flush();
    }

    private static AiSource Source(int index, string title, string domain, string? snippet = null)
        => new() { Index = index, Title = title, Domain = domain, Snippet = snippet };

    [AvaloniaFact]
    public void Push_Shows_Whole_Words_Three_At_A_Time_On_The_34ms_Clock()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show();
        try
        {
            reply.Push(Prose);
            reply.IsRevealing.ShouldBeTrue();
            reply.Classes.Contains(AiReplyText.RevealingPseudoClass).ShouldBeTrue();

            // The first step does not wait; after that one step every third 60 Hz frame (34 ms rounds up to 50 ms).
            Frames(clock, 1);
            reply.AppendedPieces.Count.ShouldBe(1);
            Frames(clock, 2);
            reply.AppendedPieces.Count.ShouldBe(1);
            Frames(clock, 1);
            reply.AppendedPieces.Count.ShouldBe(2);
            Frames(clock, 27);
            reply.AppendedPieces.Count.ShouldBe(11);

            foreach (var piece in reply.AppendedPieces)
            {
                Words(piece).ShouldBe(3);
                // Whole words: every piece ends after a word's trailing space.
                piece.ShouldEndWith(" ");
            }
            string.Concat(reply.AppendedPieces).ShouldBe(string.Join(" ", Prose.Split(' ').Take(33)) + " ");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Words_Per_Step_And_Reveal_Interval_Set_The_Cadence_Of_The_Next_Reply()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show();
        try
        {
            reply.WordsPerStep = 1;
            reply.RevealInterval = TimeSpan.FromMilliseconds(100);
            // Eight words: under a second of reading at 10 words a second, so no catching up.
            reply.Push("One two three four five six seven eight ");
            Frames(clock, 1);
            // 100 ms is six frames and a hair: the next word comes on the seventh.
            Frames(clock, 7);
            reply.AppendedPieces.Count.ShouldBe(2);
            reply.AppendedPieces.ShouldAllBe(p => Words(p) == 1);

            // A reply keeps its cadence; the next one takes the new values.
            reply.WordsPerStep = 3;
            Frames(clock, 7);
            reply.AppendedPieces.Count.ShouldBe(3);
            Words(reply.AppendedPieces.Last()).ShouldBe(1);
            reply.Reset();
            reply.Push(Prose);
            Frames(clock, 1);
            Words(reply.AppendedPieces.Single()).ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Complete_Shows_The_Backlog_Within_The_Drain_Then_Raises_RevealCompleted_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show();
        var completed = 0;
        reply.RevealCompleted += (_, _) => completed++;
        try
        {
            var text = string.Concat(Enumerable.Repeat(Prose, 6)) + "Last word";
            reply.Push(text);
            Frames(clock, 3);
            reply.Complete();
            reply.IsRevealing.ShouldBeTrue();
            // The rest drains within 600 ms, then the finished state follows one step after the last batch.
            int lastPiece = 0, finished = 0, pieces = reply.AppendedPieces.Count;
            for (var frame = 1; frame <= 120 && finished == 0; frame++)
            {
                Frames(clock, 1);
                if (reply.AppendedPieces.Count != pieces)
                {
                    pieces = reply.AppendedPieces.Count;
                    lastPiece = frame;
                }
                if (completed > 0)
                {
                    finished = frame;
                }
            }
            string.Concat(reply.AppendedPieces).ShouldBe(text);
            (lastPiece * Frame.TotalMilliseconds).ShouldBeLessThanOrEqualTo(600 + 2 * Frame.TotalMilliseconds);
            (finished - lastPiece).ShouldBeInRange(2, 4);
            completed.ShouldBe(1);
            reply.IsRevealing.ShouldBeFalse();
            Frames(clock, 30);
            completed.ShouldBe(1);

            // Pushing after completion is ignored.
            reply.Push("more");
            Frames(clock, 5);
            string.Concat(reply.AppendedPieces).ShouldBe(text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Completing_An_Empty_Reply_Finishes_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show();
        var completed = 0;
        reply.RevealCompleted += (_, _) => completed++;
        try
        {
            reply.Complete();
            Frames(clock, 2);
            completed.ShouldBe(1);
            reply.AppendedPieces.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Keeps_What_Is_Shown_Drops_The_Rest_And_Raises_Nothing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show();
        var completed = 0;
        reply.RevealCompleted += (_, _) => completed++;
        try
        {
            reply.Push(Prose + Prose);
            Frames(clock, 10);
            var shown = string.Concat(reply.AppendedPieces);
            shown.Length.ShouldBeGreaterThan(0);
            reply.Stop();
            reply.IsRevealing.ShouldBeFalse();
            Frames(clock, 60);
            string.Concat(reply.AppendedPieces).ShouldBe(shown);
            completed.ShouldBe(0);
            reply.Push("ignored ");
            reply.Complete();
            Frames(clock, 10);
            string.Concat(reply.AppendedPieces).ShouldBe(shown);
            completed.ShouldBe(0);

            // The stream ends: the Markdown is no longer streaming.
            Settle(window);
            reply.Body!.IsStreaming.ShouldBeFalse();

            // Reset starts a new reply.
            reply.Reset();
            reply.AppendedPieces.ShouldBeEmpty();
            reply.Body.Stream.ShouldBeNull();
            reply.Body.Text.ShouldBeNull();
            reply.Push("Fresh start here ");
            Frames(clock, 1);
            reply.AppendedPieces.ShouldBe(["Fresh start here "]);
            reply.IsRevealing.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SetText_Shows_A_Finished_Reply_At_Once_Without_Pacing()
    {
        var (reply, window) = Show(ArcMotionLevel.Full);
        try
        {
            reply.Push("Streaming ");
            reply.SetText("Done [1] and [2].");
            reply.IsRevealing.ShouldBeFalse();
            reply.AppendedPieces.ShouldBeEmpty();
            reply.Body!.Stream.ShouldBeNull();
            reply.Body.Text.ShouldBe(Keyed("Done [1] and [2]."));
            reply.ShownCitations.ShouldBe([(1, 0), (2, 1)]);
            // Nothing fades: the pills are fully there.
            reply.RevealOf(0).ShouldBe(1);
            reply.Push("ignored");
            reply.Body.Text.ShouldBe(Keyed("Done [1] and [2]."));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Batches_Fade_150ms_At_Full_Motion_And_Not_At_Reduced_Or_Off()
    {
        foreach (var (level, fade) in new[] { (ArcMotionLevel.Full, 150.0), (ArcMotionLevel.Reduced, 0.0), (ArcMotionLevel.Off, 0.0) })
        {
            var clock = new ManualFrameClock();
            using var _ = clock.Install();
            var (reply, window) = Show(level);
            try
            {
                reply.Push(Prose);
                Frames(clock, 7);
                // The cadence is the same at every level.
                reply.AppendedPieces.Count.ShouldBe(3, $"{level}");
                reply.Body!.Stream!.State.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(fade), $"{level}");
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task A_Citation_Waits_Until_Nothing_Fades_Then_Ends_Its_Own_Batch_And_Fades_In()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show(ArcMotionLevel.Full);
        try
        {
            reply.Push("Check the key [1]. Then pin the image ");
            Frames(clock, 1);
            reply.AppendedPieces.ShouldBe(["Check the key "]);
            // The words are still fading (real time): the pill is held, and so is the pacer.
            Frames(clock, 3);
            reply.AppendedPieces.Count.ShouldBe(1);
            await Task.Delay(220);
            Frames(clock, 1);
            reply.AppendedPieces.ShouldBe(["Check the key ", AiCitationText.Tag(1, 0)]);
            reply.RevealOf(0).ShouldBeLessThan(1);
            Frames(clock, 1);
            reply.AppendedPieces.Count.ShouldBe(3);
            reply.AppendedPieces[2].ShouldBe(". Then pin ");
            Frames(clock, 30);
            string.Concat(reply.AppendedPieces).ShouldBe(Keyed("Check the key [1]. Then pin the image "));
            // The pill's own fade (150 ms, linear) is done.
            reply.RevealOf(0).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shown_Text_Is_Never_Appended_Twice_Whatever_The_Chunking()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show();
        try
        {
            const string text = "1. Check the cache key.[1]\n2. Pin the SDK image.\n3. Split the UI tests.[3]\n\n```yaml\nkey: [2]\n```\n\nIt fell to zero [2][1].";
            var random = new Random(7);
            var at = 0;
            while (at < text.Length)
            {
                var size = Math.Min(text.Length - at, random.Next(1, 9));
                reply.Push(text.Substring(at, size));
                at += size;
                Frames(clock, random.Next(0, 3));
            }
            reply.Complete();
            Frames(clock, 90);
            reply.IsRevealing.ShouldBeFalse();
            string.Concat(reply.AppendedPieces).ShouldBe(Keyed(text));
            reply.ShownCitations.ShouldBe([(1, 0), (3, 1), (2, 2), (1, 3)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sources_Map_By_Index_And_A_Missing_Source_Leaves_A_Plain_Number()
    {
        var (reply, window) = Show();
        try
        {
            var sources = new AvaloniaList<AiSource> { Source(1, "Caching dependencies in pipelines", "docs.example.org") };
            reply.CitationName = (n, title, domain) => $"Source {n}: {title}, {domain}";
            reply.Sources = sources;
            reply.SetText("Key [1], image [3].");
            reply.FindSource(1).ShouldBeSameAs(sources[0]);
            reply.FindSource(3).ShouldBeNull();
            reply.CitationTargets.Select(t => t.IsVisible).ShouldBe([true, false]);
            Avalonia.Automation.AutomationProperties.GetName(reply.CitationTargets[0]).ShouldBe("Source 1: Caching dependencies in pipelines, docs.example.org");

            // A source that arrives later turns the number into a link.
            sources.Add(Source(3, "Build performance checklist", "handbook.example.com"));
            reply.CitationTargets.Select(t => t.IsVisible).ShouldBe([true, true]);
            Avalonia.Automation.AutomationProperties.GetName(reply.CitationTargets[1]).ShouldBe("Source 3: Build performance checklist, handbook.example.com");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Hovering_A_Pill_Inverts_It_And_Opens_The_Preview_Above_It()
    {
        var (reply, window) = Show();
        try
        {
            reply.Sources = new AvaloniaList<AiSource>
            {
                Source(1, "Why our cache hit rate fell to zero", "blog.example.dev", "The key included the lock file's timestamp.")
            };
            // Room above the text for the card.
            reply.Margin = new Thickness(0, 260, 0, 0);
            reply.SetText("The cache key changed [1] last week, and [2] is unknown.");
            await Until(window, () => reply.GetCitationRect(0) is not null && reply.GetCitationRect(1) is not null);
            var rect = reply.GetCitationRect(0)!.Value;
            rect.Height.ShouldBe(AiCitationRun.PillHeight);
            rect.Width.ShouldBeGreaterThanOrEqualTo(AiCitationRun.PillHeight);

            var center = reply.TranslatePoint(rect.Center, window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Settle(window);
            reply.HoveredKey.ShouldBe(0);
            reply.InversionOf(0).ShouldBe(1);
            reply.IsCardOpen.ShouldBeFalse();

            reply.FlushCardTimers();
            Settle(window);
            reply.IsCardOpen.ShouldBeTrue();
            reply.CardKey.ShouldBe(0);
            var card = reply.Card!;
            card.Title.ShouldBe("Why our cache hit rate fell to zero");
            card.Domain.ShouldBe("blog.example.dev");
            card.Number.ShouldBe("1");
            card.Snippet.ShouldBe("The key included the lock file's timestamp.");
            card.Bounds.Width.ShouldBe(288);
            // Above the pill, 8 away, centred on it.
            var cardRect = new Rect(card.TranslatePoint(default, window)!.Value, card.Bounds.Size);
            var pill = new Rect(center - new Point(rect.Width / 2, rect.Height / 2), rect.Size);
            cardRect.Bottom.ShouldBe(pill.Top - 8, 1);
            cardRect.Center.X.ShouldBe(pill.Center.X, 1);

            // A citation without a source does nothing.
            var plain = reply.TranslatePoint(reply.GetCitationRect(1)!.Value.Center, window)!.Value;
            window.MouseMove(plain, RawInputModifiers.None);
            Settle(window);
            reply.HoveredKey.ShouldBe(1);
            reply.InversionOf(1).ShouldBe(0);
            reply.InversionOf(0).ShouldBe(1);
            reply.FlushCardTimers();
            Settle(window);
            reply.IsCardOpen.ShouldBeFalse();
            reply.InversionOf(0).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_Pill_At_The_End_Of_The_Text_Is_Found_And_Hovered_Like_Any_Other()
    {
        var (reply, window) = Show();
        try
        {
            reply.Sources = new AvaloniaList<AiSource> { Source(4, "Runner disk usage", "status.example.net") };
            reply.SetText("The runner ran out of disk. [4]");
            await Until(window, () => reply.GetCitationRect(0) is not null);
            var rect = reply.GetCitationRect(0)!.Value;
            rect.Width.ShouldBeGreaterThanOrEqualTo(AiCitationRun.PillHeight);
            window.MouseMove(reply.TranslatePoint(new Point(rect.Right - 2, rect.Center.Y), window)!.Value, RawInputModifiers.None);
            Settle(window);
            reply.HoveredKey.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task At_Full_Motion_The_Pill_Turns_Over_160ms_And_Back()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (reply, window) = Show(ArcMotionLevel.Full);
        try
        {
            reply.Sources = new AvaloniaList<AiSource> { Source(1, "Caching dependencies in pipelines", "docs.example.org") };
            reply.SetText("Check the cache key [1] first.");
            await Until(window, () => reply.GetCitationRect(0) is not null);
            var center = reply.TranslatePoint(reply.GetCitationRect(0)!.Value.Center, window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Frames(clock, 4);
            reply.InversionOf(0).ShouldBeInRange(0.2, 0.95);
            Frames(clock, 8);
            reply.InversionOf(0).ShouldBe(1);
            window.MouseMove(new Point(2, 2), RawInputModifiers.None);
            Frames(clock, 12);
            reply.InversionOf(0).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task A_New_Reply_Closes_The_Preview_And_Drops_The_Stops()
    {
        var (reply, window) = Show();
        try
        {
            reply.Sources = new AvaloniaList<AiSource> { Source(1, "Caching dependencies in pipelines", "docs.example.org") };
            reply.SetText("Check the cache key [1] first.");
            await Until(window, () => reply.GetCitationRect(0) is not null);
            reply.OpenCard(0, 1);
            Settle(window);
            reply.IsCardOpen.ShouldBeTrue();
            reply.CitationTargets.Count.ShouldBe(1);

            reply.Reset();
            Settle(window);
            reply.IsCardOpen.ShouldBeFalse();
            reply.CardPopup!.IsOpen.ShouldBeFalse();
            reply.CitationTargets.ShouldBeEmpty();
            reply.ShownCitations.ShouldBeEmpty();
            reply.Body!.Text.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Pressing_A_Pill_Raises_CitationInvoked_With_Its_Source()
    {
        var (reply, window) = Show();
        try
        {
            var source = Source(2, "Build performance checklist", "handbook.example.com");
            reply.Sources = new AvaloniaList<AiSource> { source };
            reply.SetText("Split the job [2] and [4].");
            await Until(window, () => reply.GetCitationRect(1) is not null);
            var invoked = new List<(AiSource, int)>();
            reply.CitationInvoked += (_, e) => invoked.Add((e.Item, e.Number));

            // Left edge, right edge (StreamMark itself only reports the left half), then the plain number.
            var linked = reply.GetCitationRect(0)!.Value;
            var plain = reply.GetCitationRect(1)!.Value;
            foreach (var at in new[] { linked.Center.WithX(linked.X + 2), linked.Center.WithX(linked.Right - 2), plain.Center })
            {
                var point = reply.TranslatePoint(at, window)!.Value;
                window.MouseMove(point, RawInputModifiers.None);
                window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
                Settle(window);
            }
            invoked.ShouldBe([(source, 2), (source, 2)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Tab_Reaches_Linked_Citations_In_Order_Opens_The_Preview_And_Enter_Invokes()
    {
        var before = new Button { Content = "Before" };
        var (reply, window) = Show(before: before);
        try
        {
            reply.Sources = new AvaloniaList<AiSource>
            {
                Source(1, "Caching dependencies in pipelines", "docs.example.org"),
                Source(2, "Why our cache hit rate fell to zero", "blog.example.dev")
            };
            reply.SetText("First [1], unknown [9], second [2].");
            await Until(window, () => reply.GetCitationRect(2) is not null);
            var invoked = new List<int>();
            reply.CitationInvoked += (_, e) => invoked.Add(e.Number);

            before.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Settle(window);
            var focused = TopLevel.GetTopLevel(reply)!.FocusManager!.GetFocusedElement();
            focused.ShouldBeSameAs(reply.CitationTargets[0]);
            reply.FlushCardTimers();
            Settle(window);
            reply.IsCardOpen.ShouldBeTrue();
            reply.CardKey.ShouldBe(0);
            reply.Card!.Title.ShouldBe("Caching dependencies in pipelines");
            Avalonia.Automation.AutomationProperties.GetHelpText(reply.CitationTargets[0]).ShouldBe("Caching dependencies in pipelines");

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldBe([1]);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle(window);
            reply.IsCardOpen.ShouldBeFalse();
            TopLevel.GetTopLevel(reply)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(reply.CitationTargets[0]);

            // The plain number [9] is skipped.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Settle(window);
            TopLevel.GetTopLevel(reply)!.FocusManager!.GetFocusedElement().ShouldBeSameAs(reply.CitationTargets[2]);
            reply.FlushCardTimers();
            reply.CardKey.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            invoked.ShouldBe([1, 2]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Copying_The_Text_Gives_Citations_As_Numbers_In_Brackets()
    {
        var (reply, window) = Show();
        try
        {
            reply.Sources = new AvaloniaList<AiSource> { Source(1, "A", "a.example") };
            reply.SetText("Check the cache key [1].");
            await Until(window, () => reply.Body!.IsDocumentReady);
            reply.Body!.SelectAll();
            reply.Body.GetSelectedText().ShouldBe("Check the cache key [1].");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Reply_Text_Is_Not_A_Tab_Stop()
    {
        var (reply, window) = Show();
        try
        {
            var view = reply.Body!.GetVisualDescendants().OfType<StreamMark.Avalonia.MarkdownView>().Single();
            KeyboardNavigation.GetIsTabStop(view).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Reply_Is_Collected_While_Its_Sources_Live_On()
    {
        // A conversation keeps a reply's sources after its views are gone (another conversation opened).
        var sources = new AvaloniaList<AiSource> { Source(1, "Caching dependencies in pipelines", "docs.example.org") };
        var weak = TestHelpers.ShowAndClose(() => new AiReplyText { Sources = sources }, reply => reply.SetText("Key [1]."));
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        sources.Add(Source(2, "Build performance checklist", "handbook.example.com"));
    }

    [AvaloniaFact]
    public void A_Reply_Taken_Out_And_Put_Back_Shows_The_Sources_That_Changed_Meanwhile()
    {
        var sources = new AvaloniaList<AiSource> { Source(1, "Caching dependencies in pipelines", "docs.example.org") };
        var reply = new AiReplyText { Sources = sources, CitationName = (n, title, domain) => $"Source {n}: {title}, {domain}" };
        var host = new Border { Child = reply };
        var window = TestHelpers.Show(host);
        try
        {
            reply.SetText("Key [1], image [3].");
            reply.CitationTargets.Select(t => t.IsVisible).ShouldBe([true, false]);

            host.Child = null;
            sources.Add(Source(3, "Build performance checklist", "handbook.example.com"));
            host.Child = reply;
            Settle(window);
            reply.CitationTargets.Select(t => t.IsVisible).ShouldBe([true, true]);
            Avalonia.Automation.AutomationProperties.GetName(reply.CitationTargets[1]).ShouldBe("Source 3: Build performance checklist, handbook.example.com");

            // And it follows the list again.
            sources.RemoveAt(0);
            reply.CitationTargets.Select(t => t.IsVisible).ShouldBe([false, true]);
        }
        finally
        {
            window.Close();
        }
    }
}
