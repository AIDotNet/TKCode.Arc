using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>Source cards: the pure rules, the status line per state, the grid's cells, the stack, citations and highlight,
/// card presses, the preview, the keyboard model, labels, the empty cards and the simulation.</summary>
public class SourceCardsTests
{
    // Pure rules ---------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("https://www.webref.dev/api/request/text", "webref.dev")]
    [InlineData("https://docs.ledgerpay.dev/webhooks", "docs.ledgerpay.dev")]
    [InlineData("edgehost.app/docs", "edgehost.app")]
    [InlineData("", "")]
    public void Domain_Is_The_Host_Without_Www(string url, string expected) =>
        SourceCardsFormat.Domain(url).ShouldBe(expected);

    [Theory]
    [InlineData("developer.webref.dev", null, "W")]
    [InlineData("edgehost.app", null, "E")]
    [InlineData("", "hmac notes", "H")]
    [InlineData("", "", "?")]
    public void Monogram_Takes_The_Site_Name(string domain, string? title, string expected) =>
        SourceCardsFormat.Monogram(domain, title).ShouldBe(expected);

    [Theory]
    [InlineData(2100, "2.1s")]
    [InlineData(800, "0.8s")]
    [InlineData(65_000, "1m 5s")]
    public void Duration_Reads_Tenths_Then_Minutes(int ms, string expected) =>
        SourceCardsFormat.Duration(TimeSpan.FromMilliseconds(ms)).ShouldBe(expected);

    [Theory]
    [InlineData(700, 3, 4)]
    [InlineData(540, 3, 4)]
    [InlineData(539, 3, 2)]
    [InlineData(800, 5, 6)]
    [InlineData(300, 1, 2)]
    public void Columns_Are_Max_Plus_One_Or_Two_Below_540(double width, int max, int expected) =>
        SourceCardsFormat.Columns(width, max).ShouldBe(expected);

    [Fact]
    public void Plan_Reserves_Slots_While_Searching()
    {
        var plan = SourceCardsFormat.Plan(arrived: 2, reserved: 7, max: 3, expanded: false);
        plan.Select(c => c.Kind).ShouldBe([SourceCardsCellKind.Card, SourceCardsCellKind.Card, SourceCardsCellKind.Skeleton, SourceCardsCellKind.Skeleton]);
    }

    [Fact]
    public void Plan_Collapses_Past_Max_Into_The_Stack()
    {
        var plan = SourceCardsFormat.Plan(arrived: 5, reserved: 7, max: 3, expanded: false);
        plan.Count.ShouldBe(4);
        plan[3].ShouldBe(new SourceCardsCell(SourceCardsCellKind.Stack, 3));
    }

    [Fact]
    public void Plan_Expanded_Gives_Every_Slot_A_Cell()
    {
        var plan = SourceCardsFormat.Plan(arrived: 5, reserved: 7, max: 3, expanded: true);
        plan.Count.ShouldBe(7);
        plan.Count(c => c.Kind == SourceCardsCellKind.Card).ShouldBe(5);
        plan.Count(c => c.Kind == SourceCardsCellKind.Skeleton).ShouldBe(2);
    }

    [Fact]
    public void Plan_Without_Overflow_Has_No_Stack()
    {
        SourceCardsFormat.Plan(3, 0, 3, false).ShouldAllBe(c => c.Kind == SourceCardsCellKind.Card);
        SourceCardsFormat.Plan(0, 0, 3, false).ShouldBeEmpty();
        SourceCardsFormat.Rows(4, 2).ShouldBe(2);
        SourceCardsFormat.Rows(0, 4).ShouldBe(0);
    }

    [Fact]
    public void Default_Labels_Format_Counts_And_Names()
    {
        var labels = SourceCardsLabels.Default;
        labels.Progress(3, 7).ShouldBe("3 of 7");
        labels.Progress(3, null).ShouldBe("3 found");
        labels.Progress(0, null).ShouldBe(string.Empty);
        labels.Count(1).ShouldBe("1 source");
        labels.Count(7).ShouldBe("7 sources");
        labels.More(4).ShouldBe("+4 more");
        labels.CitationName(2, "Request: text() method", "webref.dev").ShouldBe("Source 2: Request: text() method, webref.dev");
        labels.CitationName(9, null, null).ShouldBe("Source 9");
        labels.CardName(1, "HMAC", "wikiref.org", true).ShouldBe("1: HMAC, wikiref.org, opens in your browser");
        labels.CardName(1, "HMAC", "wikiref.org", false).ShouldBe("1: HMAC, wikiref.org");
        labels.AnnounceSearching("pool size").ShouldBe("Searching for pool size");
        labels.AnnounceFailed("Timed out.").ShouldBe("Search failed. Timed out.");
    }

    [Fact]
    public void Count_Rolls_Up_When_The_Number_Grows()
    {
        SourceCardsCount.DirectionOf("3 of 7", "4 of 7").ShouldBe(1);
        SourceCardsCount.DirectionOf("+4 more", "+3 more").ShouldBe(-1);
        SourceCardsCount.DirectionOf("Searching", "7 sources").ShouldBe(1);
    }

    [Fact]
    public void Answer_Tokens_Mark_Citations()
    {
        SourceCardsFormat.CitationOf("[4];", out var trailing).ShouldBe(4);
        trailing.ShouldBe(";");
        SourceCardsFormat.CitationOf("request.text()", out _).ShouldBeNull();
    }

    // States -------------------------------------------------------------------------------------------------------------

    private static SourceCardsSource[] Seven() =>
    [
        new("Verify webhook signatures", "https://docs.ledgerpay.dev/webhooks") { Site = "Ledgerpay Docs", Snippet = "Compute an HMAC." },
        new("Request: text() method", "https://webref.dev/api/request/text") { Site = "WebRef" },
        new("Constant-time comparison", "https://cryptonotes.io/posts/compare"),
        new("Why does my signature never match?", "https://devanswers.net/q/1") { Site = "DevAnswers" },
        new("Raw body in edge functions", "https://edgehost.app/docs/raw-body") { Site = "Edgehost Docs" },
        new("HMAC", "https://wikiref.org/wiki/HMAC") { Site = "WikiRef" },
        new("Idle timeouts", "https://pgnotes.dev/idle")
    ];

    private static Window Show(SourceCards cards, double width = 700)
    {
        var window = TestHelpers.Show(cards);
        window.Width = width;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        cards.UpdateLayout();
        return window;
    }

    [AvaloniaFact]
    public void Searching_Reserves_Slots_And_Counts_Up()
    {
        var sources = new AvaloniaList<SourceCardsSource>(Seven().Take(2));
        var cards = new SourceCards { Sources = sources, Status = SourceCardsStatus.Searching, Total = 5, Query = "pool size" };
        var window = Show(cards);
        try
        {
            cards.DisplayedWord.ShouldBe("Searching");
            cards.CountView!.Text.ShouldBe("2 of 5");
            cards.DisplayedMeta.ShouldBe("pool size");
            cards.Classes.ShouldContain(SourceCards.SearchingPseudoClass);
            cards.CellPlan.Count.ShouldBe(4);
            cards.CellHosts[2].View.ShouldBeOfType<SourceCardsSkeleton>();
            AutomationProperties.GetItemStatus(cards.GridPanel!).ShouldBe("Loading sources");

            sources.Add(Seven()[2]);
            cards.CountView.Text.ShouldBe("3 of 5");
            cards.CellHosts[2].View.ShouldBeOfType<SourceCardsCard>();
            cards.CellHosts[3].View.ShouldBeOfType<SourceCardsSkeleton>();
            cards.Announcer!.History.ShouldContain("Searching for pool size");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Done_Shows_The_Count_The_Time_And_The_Stack()
    {
        var cards = new SourceCards { Sources = Seven(), Duration = TimeSpan.FromMilliseconds(2100) };
        var window = Show(cards);
        try
        {
            cards.DisplayedWord.ShouldBeEmpty();
            cards.CountView!.Text.ShouldBe("7 sources");
            cards.DisplayedMeta.ShouldBe("· 2.1s");
            cards.CellPlan.Count.ShouldBe(4);
            var stack = cards.CellHosts[3].View.ShouldBeOfType<SourceCardsStack>();
            stack.MoreText.ShouldBe("+4 more");
            stack.MarkCount.ShouldBe(SourceCardsStack.MaxMarks);
            AutomationProperties.GetName(stack).ShouldStartWith("4 more sources: DevAnswers, Edgehost Docs, WikiRef");
            cards.ToggleButton!.IsVisible.ShouldBeTrue();
            cards.ToggleButton.Content.ShouldBe("Show all");
            cards.GridPanel!.Columns.ShouldBe(4);
            AutomationProperties.GetName(cards.GridPanel).ShouldBe("Sources");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_Shows_Its_Source_And_Says_What_It_Does()
    {
        var cards = new SourceCards { Sources = Seven() };
        var window = Show(cards);
        try
        {
            var card = cards.CardOf(2)!;
            card.NumberText.ShouldBe("2");
            card.Domain.ShouldBe("webref.dev");
            card.Letter.ShouldBe("W");
            card.Title.ShouldBe("Request: text() method");
            AutomationProperties.GetName(card).ShouldBe("2: Request: text() method, webref.dev, opens in your browser");
            cards.OpensLinks = false;
            AutomationProperties.GetName(card).ShouldBe("2: Request: text() method, webref.dev");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Width_Follows_The_Container_Not_The_Content()
    {
        var cards = new SourceCards { Sources = Seven().Take(1).ToArray() };
        var window = Show(cards, width: 640);
        try
        {
            cards.Bounds.Width.ShouldBe(640, 0.5);
            cards.Sources = Seven();
            cards.UpdateLayout();
            cards.Bounds.Width.ShouldBe(640, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Container_Has_Two_Columns_With_The_Stack_Last()
    {
        var cards = new SourceCards { Sources = Seven() };
        var window = Show(cards, width: 420);
        try
        {
            cards.GridPanel!.Columns.ShouldBe(2);
            cards.CellPlan[^1].Kind.ShouldBe(SourceCardsCellKind.Stack);
            var stack = cards.StackTile!;
            var card = cards.CardOf(3)!;
            // Two rows of two: the third card starts the second row, the stack ends it.
            card.TranslatePoint(default, cards.GridPanel)!.Value.X.ShouldBe(0, 0.5);
            stack.TranslatePoint(default, cards.GridPanel)!.Value.Y.ShouldBe(card.TranslatePoint(default, cards.GridPanel)!.Value.Y, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Keeps_What_Arrived_Drops_The_Slots_And_Offers_Retry()
    {
        var cards = new SourceCards
        {
            Sources = Seven().Take(2).ToArray(),
            Status = SourceCardsStatus.Searching,
            Total = 6,
            AllowsRetry = true
        };
        var retried = 0;
        cards.RetryRequested += (_, _) => retried++;
        var window = Show(cards);
        try
        {
            cards.CellPlan.Count.ShouldBe(4);
            cards.ErrorText = "The search provider timed out after 8s.";
            cards.Status = SourceCardsStatus.Error;
            cards.CellPlan.Count.ShouldBe(2);
            cards.DisplayedWord.ShouldBe("Search failed");
            cards.DisplayedMeta.ShouldBe("The search provider timed out after 8s.");
            cards.CountView!.Text.ShouldBeEmpty();
            cards.Classes.ShouldContain(SourceCards.ErrorPseudoClass);
            cards.RetryButton!.IsVisible.ShouldBeTrue();
            cards.RetryButton.Content.ShouldBe("Retry");
            cards.Announcer!.History.ShouldContain("Search failed. The search provider timed out after 8s.");

            cards.RetryButton.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            retried.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nothing_Found_Says_So_And_Offers_Search_Again()
    {
        var cards = new SourceCards { Sources = [], Status = SourceCardsStatus.Searching, Query = "lunar billing" };
        var window = Show(cards);
        try
        {
            cards.Status = SourceCardsStatus.Done;
            cards.IsEmptyResult.ShouldBeTrue();
            cards.DisplayedWord.ShouldBe("No sources found");
            cards.DisplayedMeta.ShouldBe("lunar billing");
            cards.CellPlan.ShouldBeEmpty();
            cards.RetryButton!.IsVisible.ShouldBeFalse();
            cards.AllowsRetry = true;
            cards.RetryButton.IsVisible.ShouldBeTrue();
            cards.RetryButton.Content.ShouldBe("Search again");
            cards.Announcer!.History.ShouldContain("No sources found");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hidden_Status_Keeps_The_Toggle()
    {
        var cards = new SourceCards { Sources = Seven(), IsStatusVisible = false };
        var window = Show(cards);
        try
        {
            TestHelpers.Descendant<Grid>(cards, SourceCards.StatusLeftPart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Grid>(cards, SourceCards.StatusRowPart).IsVisible.ShouldBeTrue();
            cards.Sources = Seven().Take(2).ToArray();
            TestHelpers.Descendant<Grid>(cards, SourceCards.StatusRowPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // Stack and keyboard ---------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Pressing_The_Stack_Opens_It_And_Show_Less_Closes_It()
    {
        var cards = new SourceCards { Sources = Seven() };
        var changes = new List<bool>();
        cards.ExpandedChanged += (_, e) => changes.Add(e.IsExpanded);
        var window = Show(cards);
        try
        {
            cards.StackTile!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            cards.IsExpanded.ShouldBeTrue();
            cards.CellPlan.Count.ShouldBe(7);
            cards.CellPlan.ShouldAllBe(c => c.Kind == SourceCardsCellKind.Card);
            cards.CellHosts[3].View.ShouldBe(cards.CardOf(4));
            cards.ToggleButton!.Content.ShouldBe("Show less");
            cards.ToggleButton.IsExpanded.ShouldBeTrue();
            cards.Classes.ShouldContain(SourceCards.ExpandedPseudoClass);

            cards.ToggleButton.Focus();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            cards.IsExpanded.ShouldBeFalse();
            cards.CellHosts[3].View.ShouldBe(cards.StackTile);
            changes.ShouldBe([true, false]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Card_Raises_SourceInvoked()
    {
        var cards = new SourceCards { Sources = Seven(), OpensLinks = false };
        SourceCardsSourceEventArgs? invoked = null;
        cards.SourceInvoked += (_, e) => invoked = e;
        var window = Show(cards);
        try
        {
            cards.CardOf(2)!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            invoked.ShouldNotBeNull();
            invoked.Number.ShouldBe(2);
            invoked.Item.Title.ShouldBe("Request: text() method");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focused_Card_Shows_Its_Preview_And_Escape_Closes_It()
    {
        var cards = new SourceCards { Sources = Seven() };
        var window = Show(cards);
        try
        {
            var card = cards.CardOf(1)!;
            card.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            cards.IsPreviewOpen.ShouldBeTrue();
            cards.PreviewedCard.ShouldBe(card);
            cards.PreviewView!.Title.ShouldBe("Verify webhook signatures");
            cards.PreviewView.Site.ShouldBe("Ledgerpay Docs");
            cards.PreviewView.Snippet.ShouldBe("Compute an HMAC.");
            AutomationProperties.GetHelpText(card).ShouldBe("Compute an HMAC.");

            // An open preview moves to the next card at once.
            cards.ShowPreview(cards.CardOf(3)!);
            cards.PreviewedCard.ShouldBe(cards.CardOf(3));
            AutomationProperties.GetHelpText(card).ShouldBeNull();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            cards.IsPreviewOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Preview_Aligns_To_The_Right_Edge_When_The_Left_Does_Not_Fit()
    {
        var cards = new SourceCards { Sources = Seven() };
        // 600px: the third card starts at 304px, and a 300px preview would reach past the right edge.
        var window = Show(cards, width: 600);
        try
        {
            cards.ShowPreview(cards.CardOf(1)!);
            TestHelpers.Descendant<Primitives.ArcPopup>(cards, SourceCards.PopupPart).RequestedPlacement.ShouldBe(PlacementMode.TopEdgeAlignedLeft);
            cards.ShowPreview(cards.CardOf(3)!);
            TestHelpers.Descendant<Primitives.ArcPopup>(cards, SourceCards.PopupPart).RequestedPlacement.ShouldBe(PlacementMode.TopEdgeAlignedRight);
        }
        finally
        {
            window.Close();
        }
    }

    // Citations ----------------------------------------------------------------------------------------------------------

    private static (SourceCards Cards, SourceCitation Two, SourceCitation Six) WithAnswer()
    {
        var two = new SourceCitation { Number = 2 };
        var six = new SourceCitation { Number = 6 };
        var text = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        text.Inlines!.Add(new Run("Read the raw body first "));
        text.Inlines.Add(new InlineUIContainer(two));
        text.Inlines.Add(new Run(" and compare in constant time "));
        text.Inlines.Add(new InlineUIContainer(six));
        return (new SourceCards { Sources = Seven(), Content = text }, two, six);
    }

    [AvaloniaFact]
    public void Citation_Names_Its_Source()
    {
        var (cards, two, _) = WithAnswer();
        var window = Show(cards);
        try
        {
            cards.Citations.Count.ShouldBe(2);
            two.Owner.ShouldBe(cards);
            AutomationProperties.GetName(two).ShouldBe("Source 2: Request: text() method, webref.dev");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_A_Citation_Highlights_Its_Card_For_A_While()
    {
        var (cards, two, six) = WithAnswer();
        var highlights = new List<int?>();
        cards.HighlightChanged += (_, e) => highlights.Add(e.Number);
        var window = Show(cards);
        try
        {
            two.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            cards.Highlighted.ShouldBe(2);
            cards.CardOf(2)!.IsHighlighted.ShouldBeTrue();
            cards.CardOf(2)!.Classes.ShouldContain(SourceCardsCard.HighlightedPseudoClass);
            cards.CardOf(1)!.IsHighlighted.ShouldBeFalse();
            two.IsHighlighted.ShouldBeTrue();
            six.IsHighlighted.ShouldBeFalse();
            highlights.ShouldBe([2]);

            cards.EndHighlight(2);
            cards.Highlighted.ShouldBeNull();
            two.IsHighlighted.ShouldBeFalse();
            highlights.ShouldBe([2, null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Citation_Into_The_Stack_Opens_It()
    {
        var (cards, _, six) = WithAnswer();
        var expanded = 0;
        cards.ExpandedChanged += (_, _) => expanded++;
        var window = Show(cards);
        try
        {
            cards.IsExpanded.ShouldBeFalse();
            six.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            cards.IsExpanded.ShouldBeTrue();
            expanded.ShouldBe(1);
            cards.Highlighted.ShouldBe(6);
            cards.CardOf(6)!.IsHighlighted.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Highlight_Set_By_The_App_Outlines_The_Stack_While_Closed()
    {
        var cards = new SourceCards { Sources = Seven() };
        var window = Show(cards);
        try
        {
            cards.Highlighted = 5;
            cards.StackTile!.IsHighlighted.ShouldBeTrue();
            cards.IsExpanded.ShouldBeFalse();
            cards.HighlightSource(5);
            cards.IsExpanded.ShouldBeTrue();
            cards.CardOf(5)!.IsHighlighted.ShouldBeTrue();
            cards.StackTile.IsHighlighted.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Citation_Outside_The_Cards_Is_A_Plain_Button()
    {
        var citation = new SourceCitation { Number = 3 };
        var clicks = 0;
        citation.Click += (_, _) => clicks++;
        var window = TestHelpers.Show(citation);
        try
        {
            citation.Owner.ShouldBeNull();
            citation.NumberText.ShouldBe("3");
            AutomationProperties.GetName(citation).ShouldBe("Source 3");
            citation.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            clicks.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    // Labels ---------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var cards = new SourceCards
        {
            Sources = Seven(),
            Status = SourceCardsStatus.Searching,
            Total = 9,
            Labels = new SourceCardsLabels
            {
                Searching = "Recherche",
                Progress = (n, total) => $"{n} sur {total}",
                ShowAll = "Tout voir",
                More = n => $"+{n} autres",
                List = "Sources citées"
            }
        };
        var window = Show(cards);
        try
        {
            cards.DisplayedWord.ShouldBe("Recherche");
            cards.CountView!.Text.ShouldBe("7 sur 9");
            cards.ToggleButton!.Content.ShouldBe("Tout voir");
            cards.StackTile!.MoreText.ShouldBe("+4 autres");
            AutomationProperties.GetName(cards.GridPanel!).ShouldBe("Sources citées");
        }
        finally
        {
            window.Close();
        }
    }

    // Empty and simulation ---------------------------------------------------------------------------------------------

    private static SourceCardsSimulation Simulated(SourceCardsScenario scenario = SourceCardsScenario.Live) => new()
    {
        Scenario = scenario,
        Sources = Seven().Take(6).ToArray(),
        Query = "webhook signature verification",
        EmptyQuery = "webhook retries on the moon",
        Answer = "Read the raw body before parsing it [1] [2]. Compare the HMAC in constant time [3] [6]. "
            + "Most mismatches come from a framework parsing the JSON first [4]; turn that off for the route [5].",
        Duration = TimeSpan.FromMilliseconds(2100),
        ErrorText = "The search provider timed out.",
    };

    [AvaloniaFact]
    public void Without_Sources_Or_A_Simulation_The_Cards_Say_Nothing_Was_Found()
    {
        var cards = new SourceCards();
        var window = Show(cards);
        try
        {
            cards.IsSimulating.ShouldBeFalse();
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Done);
            cards.IsEmptyResult.ShouldBeTrue();
            cards.DisplayedSources.Count.ShouldBe(0);
            cards.CellPlan.Count.ShouldBe(0);
            cards.RetryButton!.IsVisible.ShouldBeFalse();
            cards.Announcer!.History.ShouldNotContain(SourceCardsLabels.Default.AnnounceSimulated);
            cards.StepSimulation(5).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Simulation_Stops_It()
    {
        var simulation = Simulated();
        var cards = new SourceCards { Simulation = simulation };
        var window = Show(cards);
        try
        {
            cards.StepSimulation(SourceCardsSimulation.AnswerAt + 1);
            cards.DisplayedSources.Count.ShouldBe(6);
            cards.Simulation = null;
            cards.IsSimulating.ShouldBeFalse();
            cards.DisplayedSources.Count.ShouldBe(0);
            cards.IsEmptyResult.ShouldBeTrue();
            TestHelpers.Descendant<ContentPresenter>(cards, SourceCards.SampleAnswerPart).IsVisible.ShouldBeFalse();
            var time = simulation.Time;
            cards.StepSimulation(1).ShouldBeFalse();
            simulation.Time.ShouldBe(time);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Sources_The_Simulation_Searches_Then_Answers_With_Citations()
    {
        var cards = new SourceCards { Simulation = Simulated() };
        var window = Show(cards);
        try
        {
            cards.IsSimulating.ShouldBeTrue();
            cards.Announcer!.History.ShouldContain(SourceCardsLabels.Default.AnnounceSimulated);
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Searching);
            cards.CountView!.Text.ShouldBe("0 of 6");
            cards.CellPlan.Count.ShouldBe(4);
            cards.EffectiveOpensLinks.ShouldBeFalse();

            cards.StepSimulation(SourceCardsSimulation.FirstArrival + SourceCardsSimulation.ArrivalStep);
            cards.DisplayedSources.Count.ShouldBe(2);
            cards.CountView.Text.ShouldBe("2 of 6");

            cards.StepSimulation(SourceCardsSimulation.AnswerAt + 2);
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Done);
            cards.CountView.Text.ShouldBe("6 sources");
            cards.DisplayedMeta.ShouldBe("· 2.1s");
            cards.SimulatedAnswerText.ShouldNotBeNull();
            cards.StepSimulation(SourceCardsSimulation.FirstTourAt - cards.Simulation!.Time);
            cards.Citations.Count.ShouldBeGreaterThan(3);
            cards.Highlighted.ShouldBe(2);

            cards.StepSimulation(SourceCardsSimulation.SecondTourAt - cards.Simulation!.Time);
            cards.Highlighted.ShouldBe(5);
            cards.IsExpanded.ShouldBeTrue();

            cards.StepSimulation(SourceCardsSimulation.CollapseAt - cards.Simulation!.Time);
            cards.IsExpanded.ShouldBeFalse();

            // It loops until someone interacts.
            cards.StepSimulation(SourceCardsSimulation.LoopAt - cards.Simulation!.Time);
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Searching);
            cards.DisplayedSources.Count.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulation_Stops_Looping_After_An_Interaction()
    {
        var cards = new SourceCards { Simulation = Simulated() };
        var window = Show(cards);
        try
        {
            cards.Simulation!.Interacted = true;
            cards.StepSimulation(SourceCardsSimulation.LoopAt + 0.1).ShouldBeFalse();
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Done);
            cards.DisplayedSources.Count.ShouldBe(6);
            cards.Highlighted.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_And_Empty_Scenarios_End_With_Retry()
    {
        var cards = new SourceCards { Simulation = Simulated(SourceCardsScenario.Error) };
        var window = Show(cards);
        try
        {
            cards.StepSimulation(3);
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Error);
            cards.DisplayedSources.Count.ShouldBe(2);
            cards.RetryButton!.IsVisible.ShouldBeTrue();

            cards.Simulation!.Scenario = SourceCardsScenario.Empty;
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Searching);
            cards.StepSimulation(2);
            cards.IsEmptyResult.ShouldBeTrue();
            cards.RetryButton.Content.ShouldBe("Search again");

            // Search again plays the story from the top.
            cards.RetryButton.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Searching);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Given_Sources_Win_Over_The_Simulation()
    {
        var cards = new SourceCards { Simulation = Simulated() };
        var window = Show(cards);
        try
        {
            cards.StepSimulation(SourceCardsSimulation.AnswerAt + 1);
            cards.Sources = Seven().Take(3).ToArray();
            cards.IsSimulating.ShouldBeFalse();
            cards.DisplayedSources.Count.ShouldBe(3);
            cards.EffectiveStatus.ShouldBe(SourceCardsStatus.Done);
            cards.StepSimulation(1).ShouldBeFalse();
            TestHelpers.Descendant<ContentPresenter>(cards, SourceCards.SampleAnswerPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // Motion ---------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Cells_Trade_Views_At_Once_With_Motion_Off()
    {
        var sources = new AvaloniaList<SourceCardsSource>();
        var cards = new SourceCards { Sources = sources, Status = SourceCardsStatus.Searching, Total = 3 };
        var window = Show(cards);
        try
        {
            sources.Add(Seven()[0]);
            cards.CellHosts[0].View.ShouldBeOfType<SourceCardsCard>();
            cards.CellHosts[0].LeavingCount.ShouldBe(0);
            cards.CellHosts[0].View!.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Skeletons_Are_Hidden_From_Assistive_Technology()
    {
        var skeleton = new SourceCardsSkeleton();
        AutomationProperties.GetAccessibilityView(skeleton).ShouldBe(AccessibilityView.Raw);
        skeleton.Strength.ShouldBe(1);
    }
}
