using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks;

public class SearchResultsTests
{
    private static List<SearchResultItem> Workspace() =>
    [
        new() { Id = "checklist", Title = "Release checklist", Kind = "Docs", Location = "Engineering / Releases", Meta = "Updated 2d ago", Snippet = "Freeze the main branch before every release." },
        new() { Id = "sync", Title = "Offline sync design", Kind = "Docs", Location = "Engineering / Architecture", Snippet = "Changes wait in a queue until the next release of the sync service." },
        new() { Id = "onboarding", Title = "Onboarding guide", Kind = "Docs", Location = "People ops", Snippet = "Pair with your buddy on a small release task." },
        new() { Id = "planning", Title = "Q3 planning notes", Kind = "Docs", Snippet = "The first public release of shared notebooks." },
        new() { Id = "fn-482", Title = "Sync stalls after sleep", Kind = "Issues", Location = "FN-482 · Mobile", Snippet = "The release candidate keeps the old socket open." },
        new() { Id = "fn-479", Title = "Release notes miss the offline fixes", Kind = "Issues", Location = "FN-479 · Docs" },
        new() { Id = "fn-455", Title = "Add a release toggle", Kind = "Issues", Location = "FN-455 · Platform", IsEnabled = false },
        new() { Id = "changelog", Title = "release-4.2-changelog.md", Kind = "Files", Location = "Files / Engineering" },
        new() { Id = "maya", Title = "Maya Lindqvist", Kind = "People", Location = "Release manager" },
        new() { Id = "loose", Title = "Release party photos" }
    ];

    private static SearchResults Create(string query = "release", IEnumerable<SearchResultItem>? items = null)
        => new() { Query = query, ItemsSource = items ?? Workspace(), Height = 640 };

    private static Window Show(SearchResults search, double width = 820, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new Border { Width = width, Child = search }, level);
        window.Width = width + 40;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_And_Template_Parts()
    {
        var search = new SearchResults();
        search.Query.ShouldBe(string.Empty);
        search.PlaceholderText.ShouldBe("Search");
        search.Label.ShouldBe("Search");
        search.AllLabel.ShouldBe("All");
        search.OtherLabel.ShouldBe("Other");
        search.SelectedKind.ShouldBeNull();
        search.IsFilteringEnabled.ShouldBeTrue();
        search.IsLoading.ShouldBeFalse();
        search.GroupPreviewCount.ShouldBe(3);
        search.PageSize.ShouldBe(20);
        search.ResultsFormat.ShouldBe("{0} results");
        search.EmptyTitleFormat.ShouldBe("No results for “{0}”");
        search.ActiveItem.ShouldBeNull();
        search.ResultCount.ShouldBe(0);
        AutomationProperties.GetLandmarkType(search).ShouldBe(AutomationLandmarkType.Search);

        var window = Show(search);
        try
        {
            search.SearchFieldControl.ShouldNotBeNull();
            search.SearchFieldControl!.Size.ShouldBe(ControlSize.Large);
            search.SearchFieldControl.Placeholder.ShouldBe("Search");
            search.ListHostControl.ShouldNotBeNull();
            search.CursorView.ShouldNotBeNull();
            search.EmptyStateControl.ShouldNotBeNull();
            TestHelpers.Descendant<GlidingIndicator>(search, SearchResults.FacetSelectionPart).Spring.ShouldBe(ArcSpring.Morph);
            // No items: only the All facet, the empty state and "No results".
            search.Facets.Single().Label.ShouldBe("All");
            search.StatsText.ShouldBe("No results");
            search.Classes.ShouldContain(SearchResults.EmptyPseudoClass);
            AutomationProperties.GetName(search).ShouldBe("Search");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void All_Groups_Results_By_Kind_With_Counts_And_See_All()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            // Facets keep the order kinds first appear in; items without a kind are "Other".
            search.Facets.Select(f => (f.Label, f.Count)).ShouldBe([("All", 10), ("Docs", 4), ("Issues", 3), ("Files", 1), ("People", 1), ("Other", 1)]);
            search.ResultCount.ShouldBe(10);
            search.StatsText.ShouldBe("10 results");
            // Three per group, ranked inside the group: title starts first, then the rest in source order.
            search.VisibleItems.Select(i => i.Id).ShouldBe(["checklist", "sync", "onboarding", "fn-479", "fn-455", "fn-482", "changelog", "maya", "loose"]);
            var headers = search.ListChildren.OfType<SearchResultGroupHeader>().ToList();
            headers.Select(h => (h.Label, h.CountText)).ShouldBe([("Docs", "4"), ("Issues", "3"), ("Files", "1"), ("People", "1"), ("Other", "1")]);
            // Only Docs has more than three.
            var more = search.MoreButtons.Single();
            more.IsVisible.ShouldBeTrue();
            ((more.Content as StackPanel)!.Children[0] as TextBlock)!.Text.ShouldBe("See all 4 Docs");

            more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.SelectedKind.ShouldBe("Docs");
            search.VisibleItems.Select(i => i.Id).ShouldBe(["checklist", "sync", "onboarding", "planning"]);
            search.ListChildren.OfType<SearchResultGroupHeader>().ShouldBeEmpty();
            search.ResultCount.ShouldBe(4);
            search.StatsText.ShouldBe("4 results");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Titles_And_Snippets_Mark_Every_Term()
    {
        var search = Create("release queue");
        var window = Show(search);
        try
        {
            var row = search.Rows.Single();
            row.Item!.Id.ShouldBe("sync");
            var snippet = row.SnippetText!;
            snippet.PlainText.ShouldBe("Changes wait in a queue until the next release of the sync service.");
            snippet.Ranges.Select(r => snippet.PlainText.Substring(r.Start, r.Length)).ShouldBe(["queue", "release"]);
            snippet.Inlines!.Count.ShouldBe(5);
            row.TitleText!.Ranges.ShouldBeEmpty();
            AutomationProperties.GetName(row).ShouldBe("Offline sync design, Docs, Engineering / Architecture");

            search.Query = "sync";
            Dispatcher.UIThread.RunJobs();
            var title = search.Rows.First(r => r.Item!.Id == "sync").TitleText!;
            title.Ranges.ShouldBe([new SearchMatchRange(8, 4)]);
            title.MarkRects.ShouldNotBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Facets_Stay_In_Place_Dim_When_Empty_And_Page_With_Show_More()
    {
        var items = Enumerable.Range(1, 7).Select(i => new SearchResultItem { Id = $"i{i}", Title = $"Release note {i}", Kind = "Notes" })
                              .Append(new SearchResultItem { Id = "p", Title = "Payroll", Kind = "Docs" })
                              .ToList();
        var search = Create(items: items);
        search.PageSize = 3;
        search.SelectedKind = "Notes";
        var window = Show(search);
        try
        {
            search.Facets.Select(f => (f.Label, f.Count)).ShouldBe([("All", 7), ("Notes", 7), ("Docs", 0)]);
            var facets = search.FacetControls;
            facets.Select(f => f.IsDimmed).ShouldBe([false, false, true]);
            facets.Select(f => f.IsSelected).ShouldBe([false, true, false]);
            facets.Select(f => f.IsTabStop).ShouldBe([false, true, false]);
            search.VisibleItems.Select(i => i.Id).ShouldBe(["i1", "i2", "i3"]);
            var more = search.MoreButtons.Single();
            ((more.Content as StackPanel)!.Children[0] as TextBlock)!.Text.ShouldBe("Show more");

            more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.VisibleItems.Count.ShouldBe(6);
            // The cursor lands on the first new result.
            search.ActiveItem!.Id.ShouldBe("i4");
            more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.VisibleItems.Count.ShouldBe(7);
            search.MoreButtons.ShouldBeEmpty();

            // A new query starts from the first page again.
            search.Query = "note";
            Dispatcher.UIThread.RunJobs();
            search.VisibleItems.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collection_Changes_Update_The_Results()
    {
        var source = new ObservableCollection<SearchResultItem>(Workspace().Take(2));
        var search = Create(items: source);
        var window = Show(search);
        try
        {
            search.ResultCount.ShouldBe(2);
            source.Add(new SearchResultItem { Id = "new", Title = "Release radio", Kind = "Channels" });
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(3);
            search.Facets.Last().Label.ShouldBe("Channels");
            source.Clear();
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(0);
            search.Classes.ShouldContain(SearchResults.EmptyPseudoClass);

            // Inline items work the same way.
            var inline = new SearchResults { Query = "plan" };
            inline.Items.Add(new SearchResultItem { Title = "Planning notes" });
            inline.ResultCount.ShouldBe(1);
            inline.Items.Add(new SearchResultItem { Title = "Plan B" });
            inline.ResultCount.ShouldBe(2);
            inline.Items.RemoveAt(0);
            inline.ResultCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Move_The_Cursor_Through_Results_And_Back_To_The_Field()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            var field = search.SearchFieldControl!;
            var list = search.ListHostControl!;
            field.Focus();
            Dispatcher.UIThread.RunJobs();

            Press(window, PhysicalKey.ArrowDown);
            list.IsFocused.ShouldBeTrue();
            search.CursorIndex.ShouldBe(0);
            search.ActiveItem!.Id.ShouldBe("checklist");
            search.Rows[0].IsActive.ShouldBeTrue();
            search.CursorView!.IsShown.ShouldBeTrue();
            search.CursorView.IsRingVisible.ShouldBeTrue();
            search.CursorView.TargetRect.Height.ShouldBe(search.Rows[0].Bounds.Height, 0.5);

            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem!.Id.ShouldBe("sync");
            Press(window, PhysicalKey.End);
            search.ActiveItem!.Id.ShouldBe("loose");
            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem!.Id.ShouldBe("loose");
            Press(window, PhysicalKey.Home);
            search.ActiveItem!.Id.ShouldBe("checklist");
            Press(window, PhysicalKey.PageDown);
            search.CursorIndex.ShouldBeGreaterThan(0);

            // The disabled "Add a release toggle" is skipped.
            Press(window, PhysicalKey.Home);
            for (var i = 0; i < 4; i++)
            {
                Press(window, PhysicalKey.ArrowDown);
            }
            search.ActiveItem!.Id.ShouldBe("fn-482");
            Press(window, PhysicalKey.ArrowUp);
            search.ActiveItem!.Id.ShouldBe("fn-479");

            // Up past the first result returns to the field and clears the cursor.
            Press(window, PhysicalKey.Home);
            Press(window, PhysicalKey.ArrowUp);
            field.IsFocused.ShouldBeTrue();
            search.ActiveItem.ShouldBeNull();
            search.CursorView.IsShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Opens_The_Cursor_Result_Or_The_First_And_Runs_The_Command()
    {
        var search = Create();
        var invoked = new List<SearchResultItem>();
        var command = new RecordingCommand();
        search.InvokeCommand = command;
        search.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(search);
        try
        {
            search.SearchFieldControl!.Focus();
            Dispatcher.UIThread.RunJobs();
            // No cursor yet: Enter in the field opens the first result.
            Press(window, PhysicalKey.Enter);
            invoked.Single().Id.ShouldBe("checklist");
            command.Parameters.Single().ShouldBe(invoked[0]);

            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            invoked.Last().Id.ShouldBe("sync");
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            invoked.Last().Id.ShouldBe("sync");
            invoked.Count.ShouldBe(3);

            // Escape in the list returns to the field; Escape there clears the query.
            Press(window, PhysicalKey.Escape);
            search.SearchFieldControl.IsFocused.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            search.Query.ShouldBe(string.Empty);
            search.ResultCount.ShouldBe(10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_In_The_Field_Updates_The_Query_And_Clears_The_Cursor()
    {
        var search = Create(string.Empty);
        var window = Show(search);
        try
        {
            // An empty query lists everything.
            search.ResultCount.ShouldBe(10);
            search.SearchFieldControl!.Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("offline");
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("offline");
            search.VisibleItems.Select(i => i.Id).ShouldBe(["sync", "fn-479"]);

            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem.ShouldNotBeNull();
            // Typing in the list keeps refining the query in the field.
            window.KeyTextInput(" fixes");
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("offline fixes");
            search.SearchFieldControl.IsFocused.ShouldBeTrue();
            search.VisibleItems.Single().Id.ShouldBe("fn-479");
            search.ActiveItem.ShouldBeNull();

            // Setting Query from code updates the field.
            search.Query = "maya";
            Dispatcher.UIThread.RunJobs();
            search.SearchFieldControl.Text.ShouldBe("maya");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Facets_Follow_The_Arrow_Keys_Like_Tabs()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            var facets = search.FacetControls;
            facets[0].Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowRight);
            search.SelectedKind.ShouldBe("Docs");
            facets[1].IsFocused.ShouldBeTrue();
            facets[1].IsSelected.ShouldBeTrue();
            Press(window, PhysicalKey.End);
            search.SelectedKind.ShouldBe("Other");
            search.VisibleItems.Single().Id.ShouldBe("loose");
            Press(window, PhysicalKey.ArrowRight);
            search.SelectedKind.ShouldBeNull();
            Press(window, PhysicalKey.ArrowLeft);
            search.SelectedKind.ShouldBe("Other");
            Press(window, PhysicalKey.Home);
            search.SelectedKind.ShouldBeNull();
            // ArrowDown leaves the facets for the results.
            Press(window, PhysicalKey.ArrowDown);
            search.ListHostControl!.IsFocused.ShouldBeTrue();
            search.ActiveItem!.Id.ShouldBe("checklist");

            var peer = ControlAutomationPeer.CreatePeerForElement(facets[1]);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
            AutomationProperties.GetName(facets[1]).ShouldBe("Docs, 4");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_F_And_Slash_Focus_The_Field()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            search.FacetControls[0].Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.F, RawInputModifiers.Control);
            search.SearchFieldControl!.IsFocused.ShouldBeTrue();
            search.SearchFieldControl.SelectedText.ShouldBe("release");

            search.ListHostControl!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            window.KeyTextInput("/");
            Dispatcher.UIThread.RunJobs();
            search.SearchFieldControl.IsFocused.ShouldBeTrue();
            search.Query.ShouldBe("release");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nothing_Found_Shows_The_Empty_State_With_Suggestions()
    {
        var search = Create("zebra");
        search.Suggestions = ["sync", "release"];
        var window = Show(search);
        try
        {
            search.ResultCount.ShouldBe(0);
            search.StatsText.ShouldBe("No results");
            search.VisibleItems.ShouldBeEmpty();
            search.Facets.Skip(1).ShouldAllBe(f => f.Count == 0);
            search.FacetControls.Skip(1).ShouldAllBe(f => f.IsDimmed);
            var empty = search.EmptyStateControl!;
            empty.Title.ShouldBe("No results for “zebra”");
            empty.Description.ShouldBe("Check the spelling or try fewer words.");
            search.SearchAllButton!.IsVisible.ShouldBeFalse();
            search.SuggestionButtons.Select(b => b.Content).ShouldBe(["sync", "release"]);
            TestHelpers.Descendant<PresenceHost>(search, SearchResults.EmptyPresencePart).IsPresent.ShouldBeTrue();
            TestHelpers.Descendant<PresenceHost>(search, SearchResults.ResultsPresencePart).IsPresent.ShouldBeFalse();

            search.SuggestionButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("sync");
            search.SearchFieldControl!.Text.ShouldBe("sync");
            search.ResultCount.ShouldBe(2);
            search.Classes.ShouldNotContain(SearchResults.EmptyPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Facet_Points_To_The_Others_And_Searches_All()
    {
        var search = Create("offline");
        search.SelectedKind = "People";
        var window = Show(search);
        try
        {
            search.ResultCount.ShouldBe(0);
            var empty = search.EmptyStateControl!;
            empty.Title.ShouldBe("No results for “offline”");
            // Docs and Issues each hold one; the first one wins the tie.
            empty.Description.ShouldBe("1 result in Docs.");
            search.SearchAllButton!.IsVisible.ShouldBeTrue();
            search.SearchAllButton.Content.ShouldBe("Search all");
            search.SearchAllButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.SelectedKind.ShouldBeNull();
            search.ResultCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Server_Side_Results_Keep_Their_Order_And_Are_Still_Highlighted()
    {
        List<SearchResultItem> fromServer =
        [
            new() { Id = "b", Title = "Billing export", Kind = "Docs" },
            new() { Id = "e", Title = "Export your data", Kind = "Docs" },
            new() { Id = "x", Title = "Unrelated but returned", Kind = "Docs" }
        ];
        var search = Create("export", fromServer);
        search.IsFilteringEnabled = false;
        var window = Show(search);
        try
        {
            search.ResultCount.ShouldBe(3);
            search.VisibleItems.Select(i => i.Id).ShouldBe(["b", "e", "x"]);
            search.Rows[0].TitleText!.Ranges.ShouldBe([new SearchMatchRange(8, 6)]);
            search.Rows[2].TitleText!.Ranges.ShouldBeEmpty();

            search.IsFilteringEnabled = true;
            Dispatcher.UIThread.RunJobs();
            search.VisibleItems.Select(i => i.Id).ShouldBe(["e", "b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_Skeletons_And_Keeps_The_Height()
    {
        var search = Create();
        search.Height = double.NaN;
        var window = Show(search);
        try
        {
            var area = TestHelpers.Descendant<Panel>(search, SearchResults.ResultsAreaPart);
            var listHeight = search.ListHostControl!.Bounds.Height;
            listHeight.ShouldBeGreaterThan(0);
            search.SearchFieldControl!.Focus();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem.ShouldNotBeNull();

            search.IsLoading = true;
            Dispatcher.UIThread.RunJobs();
            search.Classes.ShouldContain(SearchResults.LoadingPseudoClass);
            search.StatsText.ShouldBe("Searching…");
            area.MinHeight.ShouldBe(listHeight, 0.5);
            search.ActiveItem.ShouldBeNull();
            search.SearchFieldControl.IsFocused.ShouldBeTrue();
            TestHelpers.Descendant<PresenceHost>(search, SearchResults.LoadingPresencePart).IsPresent.ShouldBeTrue();
            TestHelpers.Descendant<PresenceHost>(search, SearchResults.ResultsPresencePart).IsPresent.ShouldBeFalse();
            search.GetVisualDescendants().OfType<Skeleton>().Count().ShouldBe(4);
            // Arrow keys wait for the results.
            Press(window, PhysicalKey.ArrowDown);
            search.ListHostControl.IsFocused.ShouldBeFalse();

            search.ItemsSource = Workspace().Take(3).ToList();
            search.IsLoading = false;
            Dispatcher.UIThread.RunJobs();
            area.MinHeight.ShouldBe(0);
            search.StatsText.ShouldBe("3 results");
            TestHelpers.Descendant<PresenceHost>(search, SearchResults.ResultsPresencePart).IsPresent.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Width_Folds_Meta_Into_The_Location_Line()
    {
        var search = Create("checklist");
        var window = Show(search, width: 400);
        try
        {
            search.Classes.ShouldContain(SearchResults.NarrowPseudoClass);
            var row = search.Rows.Single();
            row.Classes.ShouldContain(SearchResultRow.CompactPseudoClass);
            row.LocationText.ShouldBe("Engineering / Releases · Updated 2d ago");
            TestHelpers.Descendant<TextBlock>(row, "PART_Meta").IsVisible.ShouldBeFalse();

            ((Border)search.Parent!).Width = 820;
            Dispatcher.UIThread.RunJobs();
            search.Classes.ShouldNotContain(SearchResults.NarrowPseudoClass);
            row.LocationText.ShouldBe("Engineering / Releases");
            TestHelpers.Descendant<TextBlock>(row, "PART_Meta").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Moves_The_Cursor_And_A_Click_Opens_The_Result()
    {
        var search = Create();
        SearchResultItem? opened = null;
        search.ItemInvoked += (_, e) => opened = e.Item;
        var window = Show(search);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var row = search.Rows[1];
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            search.ActiveItem!.Id.ShouldBe("sync");
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            opened!.Id.ShouldBe("sync");

            // Disabled results neither take the cursor nor open.
            opened = null;
            var disabled = search.Rows.First(r => r.Item!.Id == "fn-455");
            disabled.IsEnabled.ShouldBeFalse();
            var point = disabled.TranslatePoint(new Point(20, 10), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            opened.ShouldBeNull();
            search.ActiveItem!.Id.ShouldBe("sync");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void List_And_Rows_Expose_Listbox_Semantics()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            var list = ControlAutomationPeer.CreatePeerForElement(search.ListHostControl!);
            list.GetAutomationControlType().ShouldBe(AutomationControlType.List);
            list.GetName().ShouldBe("10 results");
            var row = search.Rows[0];
            var peer = ControlAutomationPeer.CreatePeerForElement(row);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ListItem);
            ((Avalonia.Automation.Provider.ISelectionItemProvider)peer).IsSelected.ShouldBeFalse();
            search.SearchFieldControl!.Focus();
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            ((Avalonia.Automation.Provider.ISelectionItemProvider)peer).IsSelected.ShouldBeTrue();
            var status = TestHelpers.Descendant<TextBlock>(search, SearchResults.StatusPart);
            AutomationProperties.GetLiveSetting(status).ShouldBe(AutomationLiveSetting.Polite);
            status.Text.ShouldBe("10 results");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Staggers_New_Results_And_Glides_Without_Throwing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var search = Create();
        var window = Show(search, level: ArcMotionLevel.Full);
        try
        {
            Run(clock, 100);
            search.Query = "sync";
            Dispatcher.UIThread.RunJobs();
            var fresh = search.Rows.First(r => r.Item!.Id == "sync");
            var kept = search.Rows.First(r => r.Item!.Id == "fn-482");
            // "Offline sync design" was shown before ("release" matched its snippet), so nothing moves...
            kept.Opacity.ShouldBe(1);
            fresh.Opacity.ShouldBe(1);

            search.Query = "maya";
            Dispatcher.UIThread.RunJobs();
            var maya = search.Rows.Single();
            Run(clock, 16);
            // ...while a result that was not on screen fades in and rises.
            maya.Opacity.ShouldBeLessThan(1);
            Run(clock, 600);
            maya.Opacity.ShouldBe(1, 0.001);
            ((Avalonia.Media.TranslateTransform)maya.RenderTransform!).Y.ShouldBe(0, 0.001);

            search.SearchFieldControl!.Focus();
            Dispatcher.UIThread.RunJobs();
            search.Query = "release";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 100);
            Press(window, PhysicalKey.ArrowDown);
            Run(clock, 16);
            Press(window, PhysicalKey.ArrowDown);
            Run(clock, 32);
            Press(window, PhysicalKey.ArrowDown);
            search.SelectedKind = "Issues";
            Dispatcher.UIThread.RunJobs();
            Run(clock, 900);
            search.IsLoading = true;
            Run(clock, 200);
            search.IsLoading = false;
            Run(clock, 900);
            search.ResultCount.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Run(ManualFrameClock clock, int milliseconds)
    {
        for (var elapsed = 0; elapsed < milliseconds; elapsed += 16)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Parameters { get; } = new();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }
}
