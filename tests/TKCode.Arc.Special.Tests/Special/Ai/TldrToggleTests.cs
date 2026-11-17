using System.Threading.Channels;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>The TL;DR toggle: lengths, chunking, the simulation (an invented fixture) and the empty state, streaming, the cache, key points and jumps, Stop, failures, autoplay and widths.</summary>
public class TldrToggleTests
{
    /// <summary>A summarizer the test drives: each request gets a channel the test writes events into.</summary>
    private sealed class Script
    {
        private Channel<TldrStreamEvent> _channel = Channel.CreateUnbounded<TldrStreamEvent>();

        public List<TldrRequest> Requests { get; } = [];

        public TldrSummarizer Summarizer => request =>
        {
            Requests.Add(request);
            _channel = Channel.CreateUnbounded<TldrStreamEvent>();
            return _channel.Reader.ReadAllAsync(request.CancellationToken);
        };

        public void Send(params TldrStreamEvent[] events)
        {
            foreach (var item in events)
            {
                _channel.Writer.TryWrite(item);
            }
        }

        public void Complete(Exception? error = null) => _channel.Writer.TryComplete(error);
    }

    /// <summary>A card on the fixture post as your own article, with the test's summarizer (none simulates lead sentences).</summary>
    private static TldrToggle Card(Script? script = null) => new()
    {
        Article = TldrToggleFixture.Article,
        Model = TldrToggleFixture.Model,
        Summarizer = script?.Summarizer,
        AutoPlay = false,
    };

    private static string ModelName => TldrToggleFixture.Model.Name;

    [Theory]
    [InlineData(1, 35, 3)]
    [InlineData(2, 60, 4)]
    [InlineData(3, 85, 5)]
    [InlineData(4, 100, 6)]
    [InlineData(5, 130, 7)]
    public void Lengths_Map_To_Target_Words_And_Point_Counts(int length, int words, int points)
    {
        var request = new TldrRequest(TldrMode.Summary, length, TldrToggleFixture.Article, CancellationToken.None);
        request.TargetWords.ShouldBe(words);
        request.PointCount.ShouldBe(points);
        TldrToggleLabels.Default.LengthValue(TldrMode.Summary, length).ShouldBe("~" + words + " words");
        TldrToggleLabels.Default.LengthValue(TldrMode.KeyPoints, length).ShouldBe(points + " points");
    }

    [Fact]
    public void Requests_Clamp_The_Length()
    {
        new TldrRequest(TldrMode.Summary, 9, TldrToggleFixture.Article, CancellationToken.None).Length.ShouldBe(5);
        new TldrRequest(TldrMode.Summary, 0, TldrToggleFixture.Article, CancellationToken.None).Length.ShouldBe(1);
    }

    [Fact]
    public void Chunks_Hold_Three_To_Eight_Words_And_Rebuild_The_Text()
    {
        var text = string.Join(' ', Enumerable.Range(0, 200).Select(i => "w" + i));
        var chunks = TldrStreams.Chunk(text, new Random(3));
        string.Concat(chunks).ShouldBe(text);
        foreach (var chunk in chunks.Take(chunks.Count - 1))
        {
            TldrStreams.WordCount(chunk).ShouldBeInRange(TldrStreams.MinChunkWords, TldrStreams.MaxChunkWords);
        }
    }

    [Fact]
    public void Fixture_Summaries_Aim_For_Their_Lengths()
    {
        for (var length = 1; length <= 5; length++)
        {
            var target = TldrToggle.TargetWordsFor(length);
            var words = TldrStreams.WordCount(TldrToggleFixture.Summaries[length - 1]);
            words.ShouldBeInRange((int)(target * 0.85), (int)(target * 1.15));
        }
    }

    [Fact]
    public void Simulated_Points_Take_The_Most_Important_In_Article_Order()
    {
        var simulation = TldrToggleFixture.Simulation();
        var points = simulation.Points(new TldrRequest(TldrMode.KeyPoints, 2, TldrToggleFixture.Article, CancellationToken.None));
        points.Select(p => p.Source).ShouldBe(["measure", "bootstrap", "cache", "results"]);
        TldrToggleFixture.Article.Paragraphs.Select(p => p.Id).ShouldContain(points[0].Source!);
        simulation.Summary(new TldrRequest(TldrMode.Summary, 3, TldrToggleFixture.Article, CancellationToken.None))
            .ShouldBe(TldrToggleFixture.Summaries[2]);
    }

    [Fact]
    public void A_Simulation_Without_Written_Text_Falls_Back_To_Lead_Sentences()
    {
        var article = new TldrArticle { Title = "Notes", Paragraphs = [new("a", "First lead. Second one."), new("b", "Another lead! More.")] };
        var simulation = new TldrToggleSimulation { Article = article, Summaries = ["Only the shortest."] };
        simulation.Summary(new TldrRequest(TldrMode.Summary, 1, article, CancellationToken.None)).ShouldBe("Only the shortest.");
        simulation.Summary(new TldrRequest(TldrMode.Summary, 2, article, CancellationToken.None)).ShouldBe("First lead. Another lead! Second one. More.");
        simulation.Points(new TldrRequest(TldrMode.KeyPoints, 1, article, CancellationToken.None)).Select(p => p.Source).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Other_Articles_Are_Simulated_From_Lead_Sentences()
    {
        var article = new TldrArticle
        {
            Title = "Notes",
            Paragraphs = [new("a", "First lead. Second one."), new("b", "Another lead! More."), new("c", "")],
        };
        var summary = TldrStreams.SimulatedSummary(new TldrRequest(TldrMode.Summary, 1, article, CancellationToken.None));
        summary.ShouldBe("First lead. Another lead! Second one. More.");
        var points = TldrStreams.SimulatedPoints(new TldrRequest(TldrMode.KeyPoints, 1, article, CancellationToken.None));
        points.Select(p => (p.Source, p.Text)).ShouldBe([("a", "First lead."), ("b", "Another lead!")]);
    }

    [Fact]
    public void Default_Labels_Count_And_Name()
    {
        var labels = TldrToggleLabels.Default;
        labels.Words(58).ShouldBe("58 words");
        labels.Points(4).ShouldBe("4 key points");
        labels.SummaryReady(58).ShouldBe("Summary ready, 58 words");
        labels.PointsReady(4).ShouldBe("4 key points ready");
        labels.JumpTo(3, "Partial bootstrapping loads").ShouldBe("Jump to paragraph 3: Partial bootstrapping loads");
        labels.ReadingTime(2).ShouldBe("2 min read");
    }

    [AvaloniaFact]
    public void A_Simulation_Shows_Its_Article_And_Model()
    {
        var card = new TldrToggle { AutoPlay = false, Simulation = TldrToggleFixture.Simulation() };
        var window = TestHelpers.Show(card);
        try
        {
            card.Mode.ShouldBe(TldrMode.Full);
            card.ParagraphViews.Count.ShouldBe(7);
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.TitlePart).Text.ShouldBe(TldrToggleFixture.Article.Title);
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.MetaPart).Text.ShouldBe("Northwind Engineering · Oct 21, 2026 · 2 min read");
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.ModelNamePart).Text.ShouldBe(ModelName);
            card.StatusView!.Text.ShouldBe("Full article");
            card.ActionsView!.Kind.ShouldBe(TldrToggleActionsKind.None);
            card.LengthSlider!.IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(card).ShouldBe(TldrToggleFixture.Article.Title);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summary_Streams_Then_Finishes_And_Is_Kept()
    {
        var script = new Script();
        var card = Card(script);
        var window = TestHelpers.Show(card);
        try
        {
            TldrCompletedEventArgs? completed = null;
            card.Completed += (_, e) => completed = e;
            card.Mode = TldrMode.Summary;
            card.Phase.ShouldBe(TldrTogglePhase.Thinking);
            card.LastAnnouncement.ShouldBe("Summarizing with " + ModelName);
            card.Classes.Contains(TldrToggle.BusyPseudoClass).ShouldBeTrue();
            card.ActionsView!.Kind.ShouldBe(TldrToggleActionsKind.Running);
            script.Requests.Single().TargetWords.ShouldBe(60);

            script.Send(TldrStreamEvent.OfText("The team cut"), TldrStreamEvent.OfText(" cold starts in half."));
            TestHelpers.PumpUntil(() => card.SummaryText == "The team cut cold starts in half.").ShouldBeTrue();
            card.Phase.ShouldBe(TldrTogglePhase.Writing);
            card.StatusView!.Kind.ShouldBe(TldrToggleStatusKind.Writing);

            script.Complete();
            TestHelpers.PumpUntil(() => card.Phase == TldrTogglePhase.Done).ShouldBeTrue();
            completed.ShouldNotBeNull();
            completed.Mode.ShouldBe(TldrMode.Summary);
            completed.Length.ShouldBe(2);
            completed.Words.ShouldBe(7);
            completed.Points.ShouldBeNull();
            card.LastAnnouncement.ShouldBe("Summary ready, 7 words");
            card.StatusView!.Text.ShouldBe("7 words");
            card.ActionsView!.Kind.ShouldBe(TldrToggleActionsKind.Finished);
            card.ActionsView.CopyText.ShouldBe("The team cut cold starts in half.");
            card.Classes.Contains(TldrToggle.BusyPseudoClass).ShouldBeFalse();

            // Switching away and back shows the kept result without asking again.
            card.Mode = TldrMode.Full;
            card.Mode = TldrMode.Summary;
            script.Requests.Count.ShouldBe(1);
            card.SummaryText.ShouldBe("The team cut cold starts in half.");
            card.Phase.ShouldBe(TldrTogglePhase.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Committed_Length_Aborts_The_Run_And_Asks_Again()
    {
        var script = new Script();
        var card = Card(script);
        var window = TestHelpers.Show(card);
        try
        {
            var lengths = new List<int>();
            card.LengthChanged += (_, e) => lengths.Add(e.Length);
            card.Mode = TldrMode.Summary;
            card.LengthSlider!.IsEnabled.ShouldBeTrue();
            card.LengthSlider.ValueFormatter!(2).ShouldBe("~60 words");
            var first = script.Requests.Single();
            card.LengthSlider.RaiseEvent(new SliderValueCommittedEventArgs(Slider.ValueCommittedEvent, 4, 0, 0, false));
            card.Length.ShouldBe(4);
            lengths.ShouldBe([4]);
            first.CancellationToken.IsCancellationRequested.ShouldBeTrue();
            script.Requests.Count.ShouldBe(2);
            script.Requests[1].TargetWords.ShouldBe(100);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Key_Points_Tick_As_They_Are_Written_And_Link_When_Done()
    {
        var script = new Script();
        var card = Card(script);
        card.Mode = TldrMode.KeyPoints;
        var window = TestHelpers.Show(card);
        try
        {
            script.Requests.Single().PointCount.ShouldBe(4);
            card.LengthSlider!.ValueFormatter!(2).ShouldBe("4 points");
            script.Send(TldrStreamEvent.OfPoint("bootstrap"), TldrStreamEvent.OfText("Partial bootstrapping loads the shell first."),
                TldrStreamEvent.OfPoint("results"), TldrStreamEvent.OfText("Starts take 0.9s."));
            TestHelpers.PumpUntil(() => card.PointRows.Count == 2 && card.PointRows[1].Text.Length > 0).ShouldBeTrue();
            card.PointRows[0].IsDone.ShouldBeTrue();
            card.PointRows[1].IsDone.ShouldBeFalse();
            card.PointRows[0].IsLinked.ShouldBeFalse();

            TldrCompletedEventArgs? completed = null;
            card.Completed += (_, e) => completed = e;
            script.Complete();
            TestHelpers.PumpUntil(() => card.Phase == TldrTogglePhase.Done).ShouldBeTrue();
            card.PointRows.ShouldAllBe(r => r.IsDone && r.IsLinked);
            card.PointRows[0].Number.ShouldBe(3);
            card.PointRows[0].LinkName.ShouldBe("Jump to paragraph 3: Partial bootstrapping loads the shell first.");
            completed!.Points!.Select(p => p.Source).ShouldBe(["bootstrap", "results"]);
            card.LastAnnouncement.ShouldBe("2 key points ready");
            card.StatusView!.Text.ShouldBe("2 key points");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Jump_Link_Shows_Focuses_And_Highlights_Its_Paragraph()
    {
        var script = new Script();
        var card = Card(script);
        card.Mode = TldrMode.KeyPoints;
        var window = TestHelpers.Show(card);
        try
        {
            script.Send(TldrStreamEvent.OfPoint("cache"), TldrStreamEvent.OfText("Drawn from disk first."));
            script.Complete();
            TestHelpers.PumpUntil(() => card.Phase == TldrTogglePhase.Done).ShouldBeTrue();
            string? jumped = null;
            var modes = new List<TldrMode>();
            card.Jumping += (_, e) => jumped = e.ParagraphId;
            card.ModeChanged += (_, e) => modes.Add(e.Mode);

            var link = card.PointRows[0].Link!;
            link.IsEnabled.ShouldBeTrue();
            link.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            jumped.ShouldBe("cache");
            modes.ShouldBe([TldrMode.Full]);
            card.Mode.ShouldBe(TldrMode.Full);
            var paragraph = card.ParagraphViews.Single(p => p.ParagraphId == "cache");
            TestHelpers.PumpUntil(() => paragraph.IsFocused && paragraph.HighlightOpacity > 0).ShouldBeTrue();
            paragraph.Decorations.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Stops_And_Keeps_What_Arrived()
    {
        var script = new Script();
        var card = Card(script);
        var window = TestHelpers.Show(card);
        try
        {
            var completed = false;
            card.Completed += (_, _) => completed = true;
            card.Mode = TldrMode.Summary;
            script.Send(TldrStreamEvent.OfText("Half a"));
            TestHelpers.PumpUntil(() => card.SummaryText == "Half a").ShouldBeTrue();
            card.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            card.Phase.ShouldBe(TldrTogglePhase.Stopped);
            script.Requests.Single().CancellationToken.IsCancellationRequested.ShouldBeTrue();
            card.SummaryText.ShouldBe("Half a");
            card.LastAnnouncement.ShouldBe("Stopped");
            card.StatusView!.Kind.ShouldBe(TldrToggleStatusKind.Stopped);
            card.ActionsView!.Kind.ShouldBe(TldrToggleActionsKind.Finished);
            card.ActionsView.CopyText.ShouldBe("Half a");
            Dispatcher.UIThread.RunJobs();
            completed.ShouldBeFalse();
            card.CachedCount.ShouldBe(0);

            // Regenerate asks again.
            card.Regenerate();
            script.Requests.Count.ShouldBe(2);
            card.Phase.ShouldBe(TldrTogglePhase.Thinking);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Before_Any_Text_Says_So()
    {
        var script = new Script();
        var card = Card(script);
        var window = TestHelpers.Show(card);
        try
        {
            card.Mode = TldrMode.Summary;
            card.Stop();
            card.Phase.ShouldBe(TldrTogglePhase.Stopped);
            card.Body!.Content.ShouldBeOfType<TldrToggleNote>().Text.ShouldBe("Stopped before any text arrived.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failure_Shows_An_Alert_With_Try_Again()
    {
        var script = new Script();
        var card = Card(script);
        var window = TestHelpers.Show(card);
        try
        {
            card.Mode = TldrMode.Summary;
            script.Complete(new InvalidOperationException("The quota ran out."));
            TestHelpers.PumpUntil(() => card.Phase == TldrTogglePhase.Failed).ShouldBeTrue();
            var alert = card.Body!.Content.ShouldBeOfType<TldrToggleAlert>();
            alert.Message.ShouldBe("The quota ran out.");
            card.StatusView!.Text.ShouldBe("Failed");
            Dispatcher.UIThread.RunJobs();
            alert.Retry!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            script.Requests.Count.ShouldBe(2);
            card.Phase.ShouldBe(TldrTogglePhase.Thinking);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Simulation_Says_So_And_Streams_The_Written_Summary()
    {
        var card = new TldrToggle { Simulation = TldrToggleFixture.Simulation() };
        var window = TestHelpers.Show(card);
        try
        {
            card.EffectiveAutoPlay.ShouldBeTrue();
            var modes = new List<TldrMode>();
            card.ModeChanged += (_, e) => modes.Add(e.Mode);
            card.PlayNow();
            modes.ShouldBe([TldrMode.Summary]);
            card.LastAnnouncement.ShouldBe("Summarizing with " + ModelName + ". Simulated, nothing was sent");
            TestHelpers.PumpUntil(() => card.Phase == TldrTogglePhase.Done, 15000).ShouldBeTrue();
            card.SummaryText.ShouldBe(TldrToggleFixture.Summaries[1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Stays_Away_Once_The_Reader_Has_Touched_The_Card()
    {
        var card = new TldrToggle { Simulation = TldrToggleFixture.Simulation() };
        var window = TestHelpers.Show(card);
        try
        {
            card.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab });
            card.IsTouched.ShouldBeTrue();
            card.PlayNow();
            card.Mode.ShouldBe(TldrMode.Full);
            new TldrToggle { Article = TldrToggleFixture.Article, Summarizer = new Script().Summarizer }.EffectiveAutoPlay.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Stack_The_Slider_And_Tighten_The_Padding()
    {
        var card = Card();
        var window = TestHelpers.Show(card);
        try
        {
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            card.Classes.Contains(TldrToggle.NarrowPseudoClass).ShouldBeTrue();
            card.Classes.Contains(TldrToggle.CompactPseudoClass).ShouldBeTrue();
            TestHelpers.Descendant<TldrToggleControlsPanel>(card).IsStacked.ShouldBeTrue();
            card.Padding.Left.ShouldBe(16);

            window.Width = 900;
            Dispatcher.UIThread.RunJobs();
            card.Bounds.Width.ShouldBe(680);
            card.Classes.Contains(TldrToggle.NarrowPseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<TldrToggleControlsPanel>(card).IsStacked.ShouldBeFalse();
            card.Padding.Left.ShouldBe(24);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streaming_Never_Changes_The_Card_Width()
    {
        var script = new Script();
        var card = Card(script);
        var window = TestHelpers.Show(card);
        try
        {
            var width = card.Bounds.Width;
            card.Mode = TldrMode.Summary;
            script.Send(TldrStreamEvent.OfText("Short."));
            TestHelpers.PumpUntil(() => card.SummaryText == "Short.").ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            card.Bounds.Width.ShouldBe(width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Article_Without_Paragraphs_Shows_A_Quiet_Empty_State()
    {
        var card = Card();
        card.Article = new TldrArticle { Title = "Draft" };
        var window = TestHelpers.Show(card);
        try
        {
            card.Body!.Content.ShouldBeOfType<TldrToggleNote>().Text.ShouldBe(TldrToggleLabels.Default.Empty);
            card.ModeSwitch!.IsEnabled.ShouldBeFalse();
            card.Mode = TldrMode.Summary;
            card.RequestCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Body_Height_Follows_Its_Content()
    {
        var card = Card(new Script());
        var window = TestHelpers.Show(card);
        try
        {
            var box = card.BodyBox!;
            var full = box.Bounds.Height;
            full.ShouldBeGreaterThan(100);
            card.Mode = TldrMode.Summary;
            Dispatcher.UIThread.RunJobs();
            box.Bounds.Height.ShouldBeLessThan(full);
            box.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Article_And_No_Simulation_Shows_The_Quiet_Empty_State()
    {
        var card = new TldrToggle();
        var window = TestHelpers.Show(card);
        try
        {
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.Body!.Content.ShouldBeOfType<TldrToggleNote>().Text.ShouldBe(TldrToggleLabels.Default.Empty);
            card.ModeSwitch!.IsEnabled.ShouldBeFalse();
            card.LengthSlider!.IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.ModelNamePart).IsVisible.ShouldBeFalse();
            card.PlayNow();
            card.Mode.ShouldBe(TldrMode.Full);
            card.Mode = TldrMode.Summary;
            card.RequestCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Article_And_Summarizer_Win_Over_The_Simulation()
    {
        var script = new Script();
        var card = new TldrToggle
        {
            Simulation = TldrToggleFixture.Simulation(),
            Article = new TldrArticle { Title = "Draft", Paragraphs = [new("a", "Only paragraph.")] },
            Summarizer = script.Summarizer,
            AutoPlay = null,
        };
        var window = TestHelpers.Show(card);
        try
        {
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.EffectiveSimulated.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.TitlePart).Text.ShouldBe("Draft");
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.ModelNamePart).IsVisible.ShouldBeFalse();
            card.Mode = TldrMode.Summary;
            script.Requests.Single().Article.Title.ShouldBe("Draft");
            card.LastAnnouncement.ShouldBe("Summarizing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Shows_The_Empty_State()
    {
        var card = new TldrToggle { Simulation = TldrToggleFixture.Simulation() };
        var window = TestHelpers.Show(card);
        try
        {
            card.PlayNow();
            card.Phase.ShouldBe(TldrTogglePhase.Thinking);
            card.Simulation = null;
            card.Phase.ShouldBe(TldrTogglePhase.Idle);
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.Body!.Content.ShouldBeOfType<TldrToggleNote>().Text.ShouldBe(TldrToggleLabels.Default.Empty);
            card.ParagraphViews.Count.ShouldBe(0);
            TestHelpers.Descendant<TextBlock>(card, TldrToggle.ModelNamePart).IsVisible.ShouldBeFalse();
            // Nothing late lands after the stop.
            TestHelpers.PumpUntil(() => card.Phase == TldrTogglePhase.Done, 1500).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Invented test data for the TldrToggle tests (people, companies and numbers are made up).</summary>
file static class TldrToggleFixture
{
    public static TldrArticle Article { get; } = new()
    {
        Title = "How we made our desktop app start four times faster",
        SourceName = "Northwind Engineering",
        AuthorName = "Priya Raman",
        Date = "Oct 21, 2026",
        Paragraphs =
        [
            new("intro",
                "Last spring our desktop app took 4.2 seconds to show its first workspace on a typical laptop. People opened it in the " +
                "morning, switched to email while it loaded and sometimes never came back. We set ourselves one goal for the quarter: " +
                "show something useful in under a second, on the same hardware, without dropping a single feature."),
            new("measure",
                "We started by measuring instead of guessing. A trace of a thousand cold starts showed that almost half the time went to " +
                "loading modules nobody needed yet: the editor, the calendar, the settings pages and three charting libraries. Another " +
                "quarter went to waiting on the network before drawing anything at all, even when the data on disk was only minutes old."),
            new("bootstrap",
                "Partial bootstrapping loads only what the first screen needs. The shell, the sidebar and the list view now ship in one " +
                "small bundle, and everything else is fetched in the background after the first paint, in the order people usually open " +
                "it. Opening the editor before it has arrived shows a short skeleton instead of blocking the whole window."),
            new("cache",
                "Next we drew from the local cache first. The app now renders the last known state of the workspace straight from disk " +
                "and marks anything older than a few minutes as stale. Fresh data streams in behind it, and rows update in place so " +
                "nothing jumps under the pointer."),
            new("sync",
                "Sync was the hardest part. The old client asked the server for everything since the last session and waited for the " +
                "full answer. The new one asks for a compact list of changed ids first, applies them in small batches and only then " +
                "fetches the full records for what is on screen."),
            new("results",
                "The results held up outside the lab. Median cold start dropped from 4.2 seconds to 0.9, and the slowest tenth of " +
                "machines went from 11 seconds to 2.6. Support tickets about a slow launch fell by two thirds in the first month, and " +
                "daily use rose on the older hardware we had worried about most."),
            new("next",
                "We are not done. Search still builds its index on the main thread, and large workspaces with thousands of projects take " +
                "longer to sync than we would like. Both are next on our list, and we will share what we learn."),
        ],
    };

    private const string Lead =
        "The desktop app took 4.2 seconds to show a workspace, so the team set out to show something useful in under a second " +
        "without dropping features. Traces showed half the time went to modules nobody needed yet and a quarter to waiting on the " +
        "network. Partial bootstrapping now loads only the first screen while the rest arrives in the background, and the app renders " +
        "the last known state from disk as fresh data streams in. A leaner sync applies changed ids first.";

    private const string Outcome =
        " The median cold start fell to 0.9 seconds, and support tickets about slow launches dropped by two thirds.";

    /// <summary>The summaries written for lengths 1 to 5 (about 35, 60, 85, 100 and 130 words).</summary>
    public static IReadOnlyList<string> Summaries { get; } =
    [
        "The team cut the app's median cold start from 4.2 seconds to 0.9 by loading only what the first screen needs, drawing the " +
        "last known state from disk and syncing changed ids before full records.",
        "Cold starts took 4.2 seconds, and traces showed most of that went to loading modules nobody needed yet and to waiting on the " +
        "network. Partial bootstrapping now ships only the shell, sidebar and list view first, the app draws its cached state straight " +
        "from disk, and sync fetches changed ids before full records. The median start is now 0.9 seconds.",
        Lead,
        Lead + Outcome,
        Lead + Outcome + " The slowest tenth of machines went from 11 seconds to 2.6, and daily use rose on the older hardware the " +
        "team had worried about most. Search indexing and very large workspaces are next.",
    ];

    /// <summary>Seven key points, most important first; a list of N takes the first N and shows them in article order.</summary>
    public static IReadOnlyList<TldrKeyPoint> KeyPoints { get; } =
    [
        new("bootstrap", "Partial bootstrapping loads only the shell, sidebar and list view first; everything else arrives after the first paint."),
        new("results", "Median cold start fell from 4.2 seconds to 0.9, and the slowest machines went from 11 seconds to 2.6."),
        new("cache", "The app draws the last known workspace from disk and updates stale rows in place."),
        new("measure", "Traces showed half of startup went to modules nobody needed yet and a quarter to the network."),
        new("sync", "Sync fetches a list of changed ids first, then full records only for what is on screen."),
        new("intro", "The goal was something useful on screen in under a second, with no features dropped."),
        new("next", "Search indexing on the main thread and very large workspaces are next."),
    ];

    /// <summary>The model credited in the footer (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new("GPT-6 Luna", ArcIcons.Sparkles);

    /// <summary>A new simulation of the post (each card gets its own).</summary>
    public static TldrToggleSimulation Simulation() => new()
    {
        Article = Article,
        Model = Model,
        Summaries = Summaries,
        KeyPoints = KeyPoints,
    };
}
