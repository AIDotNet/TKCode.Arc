using System.Collections.ObjectModel;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks;

public class SearchResultsTests
{
    private static readonly DateTimeOffset s_day0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SearchResultItem Doc(string id, string title, string team, int day, string snippet, string? body = null)
    {
        var item = new SearchResultItem
        {
            Id = id,
            Title = title,
            Kind = "Docs",
            KindLabel = "Doc",
            Location = "Engineering / Handbook",
            Snippet = snippet,
            Body = body,
            Date = s_day0.AddDays(day),
            Url = "https://example.test/" + id
        };
        item.Attributes["team"] = team;
        item.Details.Add(new SearchResultDetail { Label = "Team", Value = team });
        item.Details.Add(new SearchResultDetail { Label = "Author", Value = "Nia Okonkwo", AvatarName = "Nia Okonkwo" });
        item.Details.Add(new SearchResultDetail { Label = "Last edited", Value = "Aug 30, 2026", Note = "3 weeks ago" });
        return item;
    }

    private static SearchResultItem Person(string id, string name, string team, string snippet)
    {
        var item = new SearchResultItem
        {
            Id = id,
            Title = name,
            Kind = "People",
            Subtitle = "Engineer, Lisbon",
            Snippet = snippet,
            Caption = id + "@example.test",
            AvatarName = name
        };
        item.Attributes["team"] = team;
        return item;
    }

    private static SearchResultItem File(string id, string title, string team, int day)
    {
        var item = new SearchResultItem { Id = id, Title = title, Kind = "Files", KindLabel = "File", Location = "Files / Design", Date = s_day0.AddDays(day), Icon = ArcIcons.File };
        item.Attributes["team"] = team;
        return item;
    }

    private static List<SearchResultItem> Workspace() =>
    [
        Doc("checklist", "Launch checklist", "Engineering", 10, "Freeze the main branch before every launch.",
            "Two days before a launch the owner freezes the branch.\n\nOne day before, support gets the change list."),
        Doc("rollback", "Rollback playbook", "Engineering", 200, "Flip the flag first, then redeploy."),
        Doc("pricing", "Pricing page copy", "Growth", 120, "Annual plans lead with the saving."),
        File("plan", "Launch plan.xlsx", "Operations", 60),
        File("kit", "press-kit.zip", "Growth", 250),
        Person("imani", "Imani Okoro", "Operations", "Runs the launch war room."),
        Person("soren", "Soren Lindgren", "Engineering", "Owns the launch pipeline."),
        Person("kenji", "Kenji Watanabe", "Growth", "Plans campaigns.")
    ];

    private static SearchResults Create(string query = "", IEnumerable<SearchResultItem>? items = null, bool filters = true)
    {
        var search = new SearchResults { Query = query, ItemsSource = items ?? Workspace(), Height = 640 };
        if (filters)
        {
            search.Filters.Add(new SearchValueFilter { Key = "kind", Title = "Type" });
            search.Filters.Add(new SearchValueFilter { Key = "team", Title = "Team" });
            search.Filters.Add(new SearchDateFilter { Key = "edited", Title = "Edited", AppliesTo = "Docs, Files" });
        }
        return search;
    }

    private static Window Show(SearchResults search, double width = 900, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new Border { Width = width, Child = search }, level);
        window.Width = width + 40;
        window.Height = 720;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static SearchValueFilter Filter(SearchResults search, string key) => search.Filters.OfType<SearchValueFilter>().Single(f => f.Key == key);

    private static string[] Ids(SearchResults search) => search.VisibleItems.Select(i => i.Id).ToArray();

    private static void Settle()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Defaults_And_Template_Parts()
    {
        var search = new SearchResults();
        search.Query.ShouldBe(string.Empty);
        search.SortOrder.ShouldBe(SearchSortOrder.BestMatch);
        search.PageSize.ShouldBe(15);
        search.MaxRecent.ShouldBe(5);
        search.IsSuggestionsEnabled.ShouldBeTrue();
        search.IsDetailEnabled.ShouldBeTrue();
        search.IsFilteringEnabled.ShouldBeTrue();
        search.IsLoading.ShouldBeFalse();
        search.Elapsed.ShouldBeNull();
        search.ShortcutText.ShouldBe("/");
        search.FiltersTitle.ShouldBe("Filters");
        search.ClearAllText.ShouldBe("Clear all");
        search.ResultsFormat.ShouldBe("{0} results");
        search.ElapsedFormat.ShouldBe("in {0} seconds");
        search.BestMatchText.ShouldBe("Best match");
        search.NewestText.ShouldBe("Newest");
        search.TitleSortText.ShouldBe("A to Z");
        search.RecentLabel.ShouldBe("Recent");
        search.SuggestedLabel.ShouldBe("Suggested");
        search.OpenFormat.ShouldBe("Open {0}");
        search.CopyLinkText.ShouldBe("Copy link");
        search.Items.ShouldBeEmpty();
        search.Filters.ShouldBeEmpty();
        search.Recent.ShouldBeEmpty();
        search.ActiveItem.ShouldBeNull();
        search.OpenedItem.ShouldBeNull();
        AutomationProperties.GetLandmarkType(search).ShouldBe(AutomationLandmarkType.Search);

        var window = Show(search);
        try
        {
            search.SearchFieldControl.ShouldNotBeNull();
            search.SearchFieldControl!.ShortcutText.ShouldBe("/");
            search.FiltersView.ShouldNotBeNull();
            search.ChipBar.ShouldNotBeNull();
            search.SortControl.ShouldNotBeNull();
            search.ListHostControl.ShouldNotBeNull();
            search.CursorView.ShouldNotBeNull();
            search.EmptyStateControl.ShouldNotBeNull();
            search.SuggestionsPopup.ShouldNotBeNull();
            // No items and no filters: the empty state, no filters column.
            search.Classes.ShouldContain(SearchResults.EmptyPseudoClass);
            search.Classes.ShouldContain(SearchResults.NoFiltersPseudoClass);
            search.CountTextBlock!.Text.ShouldBe("No results");
            search.CountDigits!.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(search).ShouldBe("Search");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Results_Show_People_And_Documents_In_One_List()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            // Rank: titles that start with the word first, then the rest in source order.
            Ids(search).ShouldBe(["checklist", "plan", "imani", "soren"]);
            search.ResultCount.ShouldBe(4);

            var doc = search.Rows[0];
            doc.Classes.ShouldNotContain(SearchResultRow.AvatarPseudoClass);
            doc.Classes.ShouldContain(SearchResultRow.HasSubtitlePseudoClass);
            doc.SubtitleText!.PlainText.ShouldBe("Doc in Engineering / Handbook");
            doc.TitleText!.Ranges.ShouldBe([new SearchMatchRange(0, 6)]);
            doc.LeadContent.ShouldBeOfType<ArcGlyph>();
            AutomationProperties.GetName(doc).ShouldBe("Launch checklist, Doc, Doc in Engineering / Handbook");

            var person = search.Rows[2];
            person.Classes.ShouldContain(SearchResultRow.AvatarPseudoClass);
            person.LeadContent.ShouldBeOfType<Avatar>().DisplayName.ShouldBe("Imani Okoro");
            person.SubtitleText!.PlainText.ShouldBe("Engineer, Lisbon");
            person.CaptionText!.PlainText.ShouldBe("imani@example.test");
            person.Classes.ShouldContain(SearchResultRow.HasCaptionPseudoClass);
            var snippet = person.SnippetText!;
            snippet.Ranges.Select(r => snippet.PlainText.Substring(r.Start, r.Length)).ShouldBe(["launch"]);

            // A row without a snippet or caption hides those lines.
            var file = search.Rows[1];
            file.Classes.ShouldNotContain(SearchResultRow.HasSnippetPseudoClass);
            file.Classes.ShouldNotContain(SearchResultRow.HasCaptionPseudoClass);

            // The first result carries the keyboard cursor, the one Enter opens.
            search.CursorIndex.ShouldBe(0);
            search.ActiveItem!.Id.ShouldBe("checklist");
            search.CursorView!.IsShown.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Body_Shows_As_The_Snippet_When_Only_It_Has_The_Words()
    {
        var items = new List<SearchResultItem>
        {
            new() { Id = "a", Title = "Runbook", Snippet = "Short summary.", Body = "Switch the feature flag off.\n\nThen watch the error rate." }
        };
        var search = Create("watch", items, filters: false);
        var window = Show(search);
        try
        {
            var snippet = search.Rows.Single().SnippetText!;
            snippet.PlainText.ShouldContain("watch the error rate");
            snippet.Ranges.Select(r => snippet.PlainText.Substring(r.Start, r.Length)).ShouldBe(["watch"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stats_Roll_The_Count_And_Show_The_Time()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            search.CountDigits!.IsVisible.ShouldBeTrue();
            search.CountDigits.Value.ShouldBe(4);
            search.CountDigits.Suffix.ShouldBe(" results");
            search.CountDigits.Prefix.ShouldBe(string.Empty);
            search.ElapsedDigits!.IsVisible.ShouldBeFalse();
            search.StatsText.ShouldBe("4 results");

            search.Elapsed = TimeSpan.FromSeconds(0.12);
            Dispatcher.UIThread.RunJobs();
            search.ElapsedDigits.IsVisible.ShouldBeTrue();
            search.ElapsedDigits.Value.ShouldBe(0.12, 0.001);
            search.ElapsedDigits.Decimals.ShouldBe(2);
            search.ElapsedDigits.Prefix.ShouldBe("in ");
            search.ElapsedDigits.Suffix.ShouldBe(" seconds");
            search.StatsText.ShouldBe("4 results in 0.12 seconds");

            search.Query = "launch checklist";
            Dispatcher.UIThread.RunJobs();
            search.CountDigits.Value.ShouldBe(1);
            search.CountDigits.Suffix.ShouldBe(" result");
            search.StatsText.ShouldBe("1 result in 0.12 seconds");

            search.Query = "zzz";
            Dispatcher.UIThread.RunJobs();
            search.CountDigits.IsVisible.ShouldBeFalse();
            search.ElapsedDigits.IsVisible.ShouldBeFalse();
            search.CountTextBlock!.IsVisible.ShouldBeTrue();
            search.CountTextBlock.Text.ShouldBe("No results");
            search.StatsText.ShouldBe("No results");

            search.ResultsFormat = "{0} hits";
            search.Query = "launch";
            Dispatcher.UIThread.RunJobs();
            search.CountDigits.Suffix.ShouldBe(" hits");
            var status = TestHelpers.Descendant<TextBlock>(search, SearchResults.StatusPart);
            AutomationProperties.GetLiveSetting(status).ShouldBe(AutomationLiveSetting.Polite);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Filters_Show_Counts_And_Ticking_Narrows_The_Results()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var groups = search.FiltersView!.Groups.ToList();
            groups.Select(g => g.Title).ShouldBe(["Type", "Team"]);
            var type = groups[0].Rows;
            type.Select(r => (r.Label, r.CountText)).ShouldBe([("Docs", "1"), ("Files", "1"), ("People", "2")]);
            var team = groups[1].Rows;
            team.Select(r => (r.Label, r.CountText)).ShouldBe([("Engineering", "2"), ("Growth", "0"), ("Operations", "2")]);
            // A value with no results keeps its place but cannot be ticked.
            team[1].Classes.ShouldContain(SearchResultsFilterRow.DimmedPseudoClass);
            team[1].Check!.IsEnabled.ShouldBeFalse();
            team[0].Check!.IsEnabled.ShouldBeTrue();
            search.HasActiveFilters.ShouldBeFalse();
            search.ChipsHost!.IsExpanded.ShouldBeFalse();

            // Ticking the checkbox ticks the value and narrows the results.
            type[2].Check!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Filter(search, "kind").Values[2].IsSelected.ShouldBeTrue();
            Ids(search).ShouldBe(["imani", "soren"]);
            search.ResultCount.ShouldBe(2);
            search.HasActiveFilters.ShouldBeTrue();
            search.ActiveFilterCount.ShouldBe(1);
            search.Classes.ShouldContain(SearchResults.FilteredPseudoClass);
            // The other counts follow the tick; the filter's own counts do not.
            type.Select(r => r.CountText).ShouldBe(["1", "1", "2"]);
            team.Select(r => r.CountText).ShouldBe(["1", "0", "1"]);
            search.FiltersView.HasActiveFilters.ShouldBeTrue();

            // The bar lists the filter as a chip that can be removed.
            search.ChipsHost.IsExpanded.ShouldBeTrue();
            var chip = search.ChipBar!.Chips.Single();
            (chip.Group, chip.Text).ShouldBe(("Type", "People"));
            AutomationProperties.GetName(chip).ShouldBe("Type People");
            TestHelpers.Descendant<Avalonia.Controls.Button>(chip, SearchResultsChip.RemovePart).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Filter(search, "kind").Values[2].IsSelected.ShouldBeFalse();
            search.ResultCount.ShouldBe(4);
            search.ChipBar.Chips.ShouldBeEmpty();
            search.ChipsHost.IsExpanded.ShouldBeFalse();

            // Several filters AND; "Clear all" (in the column and in the bar) resets them.
            type[2].Check!.IsChecked = true;
            team[0].Check!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Ids(search).ShouldBe(["soren"]);
            search.ChipBar!.Chips.Select(c => c.Text).ShouldBe(["People", "Engineering"]);
            TestHelpers.Descendant<Avalonia.Controls.Button>(search.FiltersView, SearchResultsFilters.ClearPart).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.HasActiveFilters.ShouldBeFalse();
            search.ResultCount.ShouldBe(4);

            type[0].Check!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<Avalonia.Controls.Button>(search.ChipBar!, SearchResultsChipBar.ClearPart).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.HasActiveFilters.ShouldBeFalse();
            Filter(search, "kind").Values.Any(v => v.IsSelected).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Counts_Follow_The_Query_As_You_Type()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            var type = search.FiltersView!.Groups.First().Rows;
            type.Select(r => r.CountText).ShouldBe(["3", "2", "3"]);
            search.Query = "launch";
            Dispatcher.UIThread.RunJobs();
            type.Select(r => r.CountText).ShouldBe(["1", "1", "2"]);
            search.Query = "launch freeze";
            Dispatcher.UIThread.RunJobs();
            type.Select(r => r.CountText).ShouldBe(["1", "0", "0"]);
            Filter(search, "kind").Values.Select(v => v.Count).ShouldBe([1, 0, 0]);
            search.Query = string.Empty;
            Dispatcher.UIThread.RunJobs();
            type.Select(r => r.CountText).ShouldBe(["3", "2", "3"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Date_Range_Narrows_Documents_And_Files_And_Shows_Its_Ends()
    {
        using var _ = English();
        var search = Create();
        var window = Show(search);
        try
        {
            var range = search.FiltersView!.DateRanges.Single();
            var slider = range.SliderControl!;
            slider.IsRange.ShouldBeTrue();
            // The span runs over the days of the documents and files.
            (slider.Maximum - slider.Minimum).ShouldBe(240);
            slider.LowerValue.ShouldBe(slider.Minimum);
            slider.UpperValue.ShouldBe(slider.Maximum);
            range.RangeText.ShouldBe("Jan 2026 – Sep 2026");
            range.Caption.ShouldBe("Applies to docs and files");
            range.Title.ShouldBe("Edited");

            // Moving the lower thumb sets the filter (the people have no date and stay).
            slider.LowerValue = slider.Minimum + 100;
            slider.RaiseEvent(new SliderRangeChangedEventArgs(Slider.RangeChangedEvent, slider.LowerValue, slider.UpperValue));
            Dispatcher.UIThread.RunJobs();
            var date = search.Filters.OfType<SearchDateFilter>().Single();
            date.Start.ShouldNotBeNull();
            date.End.ShouldBeNull();
            Ids(search).ShouldBe(["rollback", "pricing", "kit", "imani", "soren", "kenji"]);
            search.HasActiveFilters.ShouldBeTrue();
            var chip = search.ChipBar!.Chips.Single();
            (chip.Group, chip.Text).ShouldBe(("Edited", "Apr 2026 – Sep 2026"));
            range.RangeText.ShouldBe("Apr 2026 – Sep 2026");

            // Setting the filter from code moves the thumbs; the chip removes it.
            date.Start = null;
            date.End = s_day0.AddDays(70);
            Dispatcher.UIThread.RunJobs();
            slider.UpperValue.ShouldBe(slider.Minimum + 60, 1);
            slider.LowerValue.ShouldBe(slider.Minimum);
            Ids(search).ShouldBe(["checklist", "plan", "imani", "soren", "kenji"]);
            TestHelpers.Descendant<Avalonia.Controls.Button>(search.ChipBar, SearchResultsChip.RemovePart).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            date.IsActive.ShouldBeFalse();
            search.ResultCount.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sort_Segments_Follow_The_Sort_Order_Both_Ways()
    {
        var search = Create();
        var window = Show(search);
        try
        {
            var sort = search.SortControl!;
            sort.ItemCount.ShouldBe(3);
            sort.SelectedIndex.ShouldBe(0);
            Ids(search).First().ShouldBe("checklist");

            search.SortOrder = SearchSortOrder.Newest;
            Dispatcher.UIThread.RunJobs();
            sort.SelectedIndex.ShouldBe(1);
            Ids(search).Take(5).ShouldBe(["kit", "rollback", "pricing", "plan", "checklist"]);

            // Choosing a segment sets the property and raises the change.
            var reasons = new List<SearchChangeReason>();
            search.SearchChanged += (_, e) => reasons.Add(e.Reason);
            sort.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            search.SortOrder.ShouldBe(SearchSortOrder.Title);
            Ids(search).Take(3).ShouldBe(["imani", "kenji", "checklist"]);
            reasons.ShouldBe([SearchChangeReason.Sort]);

            // Sorting is not a new search: the cursor goes back to the first result of the new order.
            search.ActiveItem!.Id.ShouldBe("imani");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Field_Clears_Shows_Its_Keycap_And_Focuses_On_The_Shortcut()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var field = search.SearchFieldControl!;
            var keycap = TestHelpers.Descendant<Border>(field, "PART_Keycap");
            var clear = TestHelpers.Descendant<FieldIconButton>(field, SearchResultsField.ClearButtonPart);
            field.Text.ShouldBe("launch");
            field.Classes.ShouldContain(SearchResultsField.FilledPseudoClass);
            keycap.IsVisible.ShouldBeTrue();

            // The key hint disappears while the field has focus.
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            keycap.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(clear).ShouldBe("Clear search");

            clear.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe(string.Empty);
            field.Text.ShouldBeNullOrEmpty();
            field.IsFocused.ShouldBeTrue();
            search.ResultCount.ShouldBe(8);
            field.Classes.ShouldNotContain(SearchResultsField.FilledPseudoClass);

            // "/" typed in the results moves focus to the field with its text selected.
            search.Query = "kit";
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowDown);
            search.ListHostControl!.IsFocused.ShouldBeTrue();
            window.KeyTextInput("/");
            Dispatcher.UIThread.RunJobs();
            field.IsFocused.ShouldBeTrue();
            field.Text.ShouldBe("kit");
            (field.SelectionEnd - field.SelectionStart).ShouldBe(3);

            // Typing refines the query without losing the shortcut's meaning inside the field.
            window.KeyTextInput("/");
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("/");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Suggestions_First_And_Then_Clears_The_Query()
    {
        var search = Create("launch");
        search.Recent.AddRange(["launch", "rollback"]);
        var window = Show(search);
        try
        {
            search.FocusSearch();
            Dispatcher.UIThread.RunJobs();
            search.AreSuggestionsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            search.AreSuggestionsOpen.ShouldBeFalse();
            search.Query.ShouldBe("launch");
            Press(window, PhysicalKey.Escape);
            search.Query.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focusing_The_Field_Opens_The_Suggestions_With_Recent_And_Suggested_Rows()
    {
        var search = Create("launch");
        search.Recent.AddRange(["launch", "rollback", "pricing copy"]);
        search.Suggestions = new List<string> { "release notes", "Rollback", "press kit" };
        var window = Show(search);
        try
        {
            search.AreSuggestionsOpen.ShouldBeFalse();
            search.FocusSearch();
            Dispatcher.UIThread.RunJobs();
            search.AreSuggestionsOpen.ShouldBeTrue();
            var list = search.SuggestionList!;
            list.Rows.Select(r => (r.Text, r.IsRecent)).ShouldBe(
                [("launch", true), ("rollback", true), ("pricing copy", true), ("release notes", false), ("press kit", false)]);
            // The entry equal to the query is the active one (it shows the return glyph); suggestions equal to a recent one are not repeated.
            list.ActiveIndex.ShouldBe(0);
            list.Rows[0].IsActive.ShouldBeTrue();
            list.Rows[0].Classes.ShouldContain(SearchResultsSuggestionRow.ActivePseudoClass);
            list.Rows[3].Classes.ShouldNotContain(SearchResultsSuggestionRow.RecentPseudoClass);
            list.Rows[0].Classes.ShouldContain(SearchResultsSuggestionRow.RecentPseudoClass);
            list.CursorView!.IsShown.ShouldBeTrue();
            // The popup is as wide as the field.
            list.GetVisualAncestors().OfType<Border>().First().Width.ShouldBe(search.SearchFieldControl!.Bounds.Width, 1);
            // Focus stays in the field.
            search.SearchFieldControl.IsFocused.ShouldBeTrue();

            // Arrow keys walk the rows (without wrapping); the first up from the top row clears the highlight.
            Press(window, PhysicalKey.ArrowDown);
            list.ActiveText.ShouldBe("rollback");
            for (var i = 0; i < 6; i++)
            {
                Press(window, PhysicalKey.ArrowDown);
            }
            list.ActiveText.ShouldBe("press kit");
            Press(window, PhysicalKey.ArrowUp);
            list.ActiveText.ShouldBe("release notes");
            for (var i = 0; i < 4; i++)
            {
                Press(window, PhysicalKey.ArrowUp);
            }
            list.ActiveIndex.ShouldBe(-1);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            list.ActiveText.ShouldBe("pricing copy");
            // Enter runs the highlighted entry.
            var submitted = new List<string>();
            search.QuerySubmitted += (_, e) => submitted.Add(e.Query);
            Press(window, PhysicalKey.Enter);
            search.Query.ShouldBe("pricing copy");
            search.AreSuggestionsOpen.ShouldBeFalse();
            submitted.ShouldBe(["pricing copy"]);
            // The chosen query moves to the top of the recent searches.
            search.Recent.ToList().ShouldBe(["pricing copy", "launch", "rollback"]);
            search.ResultCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Filters_The_Suggestions_And_Enter_Submits_The_Text()
    {
        var search = Create();
        search.Recent.AddRange(["rollback", "launch"]);
        search.Suggestions = new List<string> { "release notes", "roadmap" };
        var window = Show(search);
        try
        {
            search.FocusSearch();
            Dispatcher.UIThread.RunJobs();
            search.SuggestionList!.Rows.Count.ShouldBe(4);
            search.SuggestionList.ActiveIndex.ShouldBe(-1);

            window.KeyTextInput("ro");
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("ro");
            search.SuggestionList.Rows.Select(r => r.Text).ShouldBe(["rollback", "roadmap"]);

            // Nothing contains the text: the popup closes by itself.
            window.KeyTextInput("zz");
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            search.AreSuggestionsOpen.ShouldBeFalse();

            // Enter without a highlighted row submits what was typed.
            Press(window, PhysicalKey.Backspace);
            Press(window, PhysicalKey.Backspace);
            Dispatcher.UIThread.RunJobs();
            search.AreSuggestionsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Enter);
            search.AreSuggestionsOpen.ShouldBeFalse();
            search.Recent.ToList().ShouldBe(["ro", "rollback", "launch"]);

            // Disabling the popup keeps it closed.
            search.IsSuggestionsEnabled = false;
            search.FocusSearch();
            Press(window, PhysicalKey.Escape);
            search.AreSuggestionsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Recent_Searches_Are_Recorded_Without_Repeats_And_Capped()
    {
        // Nothing to open: Enter only submits the text.
        var search = Create(items: new List<SearchResultItem>());
        search.MaxRecent = 3;
        var window = Show(search);
        try
        {
            foreach (var query in new[] { "one", "two", "three", "TWO", "four" })
            {
                search.Query = query;
                search.FocusSearch();
                Press(window, PhysicalKey.Enter);
            }
            // Newest first, a repeat (ignoring case) moves up, and only MaxRecent are kept.
            search.Recent.ToList().ShouldBe(["four", "TWO", "three"]);
            search.MaxRecent = 2;
            search.Recent.ToList().ShouldBe(["four", "TWO"]);
            search.Query = "  ";
            search.FocusSearch();
            Press(window, PhysicalKey.Enter);
            search.Recent.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_On_A_Suggestion_Fills_The_Query()
    {
        var search = Create();
        search.Recent.AddRange(["launch"]);
        var window = Show(search);
        try
        {
            search.FocusSearch();
            Dispatcher.UIThread.RunJobs();
            Settle();
            var row = search.SuggestionList!.Rows.Single();
            var point = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("launch");
            search.AreSuggestionsOpen.ShouldBeFalse();
            search.SearchFieldControl!.IsFocused.ShouldBeTrue();
            search.Recent.ToList().ShouldBe(["launch"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Move_The_Cursor_Through_Results_And_Back_To_The_Field()
    {
        var search = Create("launch", filters: false);
        var window = Show(search);
        try
        {
            var field = search.SearchFieldControl!;
            var list = search.ListHostControl!;
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            // No suggestions to walk: Down leaves the field for the list, on the first result.
            Press(window, PhysicalKey.ArrowDown);
            list.IsFocused.ShouldBeTrue();
            search.CursorIndex.ShouldBe(0);
            search.Rows[0].IsActive.ShouldBeTrue();
            search.CursorView!.IsRingVisible.ShouldBeTrue();
            search.CursorView.TargetRect.Height.ShouldBe(search.Rows[0].Bounds.Height, 0.5);

            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem!.Id.ShouldBe("plan");
            Press(window, PhysicalKey.End);
            search.ActiveItem!.Id.ShouldBe("soren");
            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem!.Id.ShouldBe("soren");
            Press(window, PhysicalKey.Home);
            search.ActiveItem!.Id.ShouldBe("checklist");
            Press(window, PhysicalKey.PageDown);
            search.CursorIndex.ShouldBeGreaterThan(0);
            Press(window, PhysicalKey.PageUp);
            search.CursorIndex.ShouldBe(0);

            // Up past the first result returns to the field and clears the cursor.
            Press(window, PhysicalKey.ArrowUp);
            field.IsFocused.ShouldBeTrue();
            search.CursorView.IsRingVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Opens_The_Cursor_Result_Raises_The_Event_And_Runs_The_Command()
    {
        var search = Create("launch");
        var invoked = new List<SearchResultItem>();
        var command = new RecordingCommand();
        search.InvokeCommand = command;
        search.ItemInvoked += (_, e) => invoked.Add(e.Item);
        var window = Show(search);
        try
        {
            search.SearchFieldControl!.Focus();
            Dispatcher.UIThread.RunJobs();
            // The suggestions are for choosing a query: the first Enter closes them and records the query...
            search.AreSuggestionsOpen.ShouldBeFalse();
            Press(window, PhysicalKey.Enter);
            invoked.Count.ShouldBe(1);
            // ...Enter opens the result under the cursor, the first one when nothing moved it.
            invoked.Single().Id.ShouldBe("checklist");
            command.Parameters.Single().ShouldBe(invoked[0]);
            search.OpenedItem.ShouldBe(invoked[0]);
            search.Recent.ToList().ShouldBe(["launch"]);

            // Close the detail and open another one from the list.
            Press(window, PhysicalKey.Escape);
            search.OpenedItem.ShouldBeNull();
            // Focus handed back to the field is not a request for suggestions.
            search.AreSuggestionsOpen.ShouldBeFalse();
            search.ListHostControl!.IsFocused.ShouldBeFalse();
            search.FocusSearch();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            invoked.Last().Id.ShouldBe("plan");
            search.OpenedItem!.Id.ShouldBe("plan");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Results_Are_Skipped_And_Cannot_Be_Opened()
    {
        var items = Workspace();
        items.First(i => i.Id == "plan").IsEnabled = false;
        var search = Create("launch", items, filters: false);
        var opened = new List<SearchResultItem>();
        search.ItemInvoked += (_, e) => opened.Add(e.Item);
        var window = Show(search);
        try
        {
            search.SearchFieldControl!.Focus();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            search.ActiveItem!.Id.ShouldBe("imani");
            search.Rows[1].IsEnabled.ShouldBeFalse();

            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var disabled = search.Rows[1];
            var point = disabled.TranslatePoint(new Point(20, 10), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            opened.ShouldBeEmpty();
            search.ActiveItem!.Id.ShouldBe("imani");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Opens_The_Detail_And_Hovering_Moves_The_Cursor()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            Settle();
            var row = search.Rows[2];
            var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            search.ActiveItem!.Id.ShouldBe("imani");
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            search.OpenedItem!.Id.ShouldBe("imani");
            search.DetailPresence!.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Detail_Shows_The_Result_With_Its_Matches_Marked_And_Closes_On_Escape()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var item = search.VisibleItems[0];
            search.SearchFieldControl!.Focus();
            search.ShowDetail(item);
            Dispatcher.UIThread.RunJobs();
            Settle();
            search.OpenedItem.ShouldBe(item);
            var presence = search.DetailPresence!;
            presence.IsOpen.ShouldBeTrue();
            presence.Entrance.ShouldBe(ModalEntrance.Rise);
            var surface = search.DetailSurface!;
            surface.IsSheet.ShouldBeFalse();
            surface.Title.ShouldBe("Doc");
            surface.MaxWidth.ShouldBe(560);
            surface.HorizontalAlignment.ShouldBe(Avalonia.Layout.HorizontalAlignment.Center);

            var body = search.DetailBody!;
            body.Heading!.PlainText.ShouldBe("Launch checklist");
            body.Heading.Ranges.ShouldBe([new SearchMatchRange(0, 6)]);
            // Paragraphs split on blank lines, with the words marked.
            var paragraphs = body.ParagraphPanel!.Children.OfType<SearchResultsMarkedText>().ToList();
            paragraphs.Count.ShouldBe(2);
            paragraphs[0].Ranges.Select(r => paragraphs[0].PlainText.Substring(r.Start, r.Length)).ShouldBe(["launch"]);
            // The first row counts the matches (title and paragraphs); the item's own details follow.
            var rows = body.TablePanel!.Children.OfType<SearchResultsDetailRow>().ToList();
            rows.Select(r => (r.Label, r.Value)).ShouldBe([("Matches", "2"), ("Team", "Engineering"), ("Author", "Nia Okonkwo"), ("Last edited", "Aug 30, 2026")]);
            rows[2].AvatarContent.ShouldBeOfType<Avatar>().DisplayName.ShouldBe("Nia Okonkwo");
            rows[3].Note.ShouldBe("3 weeks ago");

            // Buttons: "Open doc" and "Copy link" raise the action event.
            search.OpenButton!.Content.ShouldBe("Open doc");
            var actions = new List<SearchResultAction>();
            search.DetailActionInvoked += (_, e) =>
            {
                e.Item.ShouldBe(item);
                actions.Add(e.Action);
            };
            search.OpenButton.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            search.CopyLinkButton!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            actions.ShouldBe([SearchResultAction.Open, SearchResultAction.CopyLink]);

            // Escape closes the detail (the modal's own key), the item is no longer open.
            Press(window, PhysicalKey.Escape);
            search.OpenedItem.ShouldBeNull();
            presence.IsOpen.ShouldBeFalse();
            // Focus returns to where it was.
            search.SearchFieldControl!.IsFocused.ShouldBeTrue();

            // The close button and code close it too.
            search.OpenedItem = item;
            Dispatcher.UIThread.RunJobs();
            presence.IsOpen.ShouldBeTrue();
            surface.CloseButton!.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.OpenedItem.ShouldBeNull();
            presence.IsOpen.ShouldBeFalse();
            search.ShowDetail(item);
            search.CloseDetail();
            presence.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Detail_Can_Be_Turned_Off_Handled_Or_Replaced_By_A_Template()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var invoked = 0;
            search.ItemInvoked += (_, e) =>
            {
                invoked++;
                if (invoked == 2)
                {
                    e.Handled = true;
                }
            };
            search.IsDetailEnabled = false;
            search.SearchFieldControl!.Focus();
            Press(window, PhysicalKey.Enter);
            invoked.ShouldBe(1);
            search.OpenedItem.ShouldBeNull();

            // Handling the event keeps the built-in detail closed too.
            search.IsDetailEnabled = true;
            Press(window, PhysicalKey.Enter);
            invoked.ShouldBe(2);
            search.OpenedItem.ShouldBeNull();

            // A template replaces the built-in content; its data is the item.
            search.DetailTemplate = new FuncDataTemplate<SearchResultItem>((item, _) => new TextBlock { Text = "Custom: " + item?.Title }, true);
            Press(window, PhysicalKey.Enter);
            search.OpenedItem.ShouldNotBeNull();
            search.DetailSurface!.Content.ShouldBe(search.OpenedItem);
            search.DetailSurface.ContentTemplate.ShouldBe(search.DetailTemplate);
            search.DetailSurface.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Custom: Launch checklist").ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            search.OpenedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Width_Moves_The_Filters_Behind_A_Button_And_The_Detail_Into_A_Sheet()
    {
        var search = Create("launch");
        var window = Show(search, width: 420);
        try
        {
            search.Classes.ShouldContain(SearchResults.NarrowPseudoClass);
            var column = TestHelpers.Descendant<Border>(search, SearchResults.FiltersColumnPart);
            column.IsVisible.ShouldBeFalse();
            var button = search.FiltersButton!;
            button.IsVisible.ShouldBeTrue();
            search.SheetFiltersView.ShouldBeNull();

            // The button opens the filters in a bottom sheet; ticking there narrows the results.
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Settle();
            var presence = search.FiltersPresence!;
            presence.IsOpen.ShouldBeTrue();
            presence.Entrance.ShouldBe(ModalEntrance.Slide);
            presence.Side.ShouldBe(ModalSide.Bottom);
            var sheet = search.SheetFiltersView!;
            sheet.IsTitleVisible.ShouldBeFalse();
            var rows = sheet.Groups.First().Rows;
            rows.Select(r => r.Label).ShouldBe(["Docs", "Files", "People"]);
            rows[2].Check!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Ids(search).ShouldBe(["imani", "soren"]);
            // The button shows how many filters are on.
            search.ActiveFilterCount.ShouldBe(1);
            search.Classes.ShouldContain(SearchResults.FilteredPseudoClass);
            // "Clear all" lives in the sheet too.
            sheet.HasActiveFilters.ShouldBeTrue();
            TestHelpers.Descendant<Avalonia.Controls.Button>(sheet, SearchResultsFilters.ClearPart).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.HasActiveFilters.ShouldBeFalse();
            Press(window, PhysicalKey.Escape);
            presence.IsOpen.ShouldBeFalse();

            // A result's detail is a bottom sheet with a grab handle.
            search.ShowDetail(search.VisibleItems[0]);
            Dispatcher.UIThread.RunJobs();
            search.DetailPresence!.Entrance.ShouldBe(ModalEntrance.Slide);
            search.DetailSurface!.IsSheet.ShouldBeTrue();
            search.DetailSurface.Classes.ShouldContain(SearchResultsSurface.SheetPseudoClass);
            search.DetailSurface.VerticalAlignment.ShouldBe(Avalonia.Layout.VerticalAlignment.Bottom);
            search.CloseDetail();
            Dispatcher.UIThread.RunJobs();

            // Widening the control brings the column back and closes the sheet.
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            presence.IsOpen.ShouldBeTrue();
            ((Border)search.Parent!).Width = 900;
            Dispatcher.UIThread.RunJobs();
            search.Classes.ShouldNotContain(SearchResults.NarrowPseudoClass);
            presence.IsOpen.ShouldBeFalse();
            column.IsVisible.ShouldBeTrue();
            button.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Loading_Shows_Skeleton_Rows_Built_On_Demand()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var loading = TestHelpers.Descendant<PresenceHost>(search, SearchResults.LoadingPresencePart);
            // A hidden skeleton would keep animating and invalidating layout, so it does not exist until needed.
            loading.Child.ShouldBeNull();
            search.IsLoading = true;
            Dispatcher.UIThread.RunJobs();
            search.Classes.ShouldContain(SearchResults.LoadingPseudoClass);
            loading.Child.ShouldBeOfType<StackPanel>().Children.OfType<Skeleton>().Count().ShouldBe(3);
            loading.IsPresent.ShouldBeTrue();
            TestHelpers.Descendant<PresenceHost>(search, SearchResults.ResultsPresencePart).IsPresent.ShouldBeFalse();
            search.StatsText.ShouldBe("Searching…");
            search.CursorView!.IsShown.ShouldBeFalse();
            search.HintsControl!.IsVisible.ShouldBeFalse();

            search.IsLoading = false;
            Dispatcher.UIThread.RunJobs();
            loading.IsPresent.ShouldBeFalse();
            Ids(search).ShouldBe(["checklist", "plan", "imani", "soren"]);
            search.StatsText.ShouldBe("4 results");
            search.CursorView.IsShown.ShouldBeTrue();
            search.HintsControl.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Results_Offers_Suggestions_And_Clearing_The_Filters()
    {
        var search = Create("zzz");
        search.Suggestions = new List<string> { "launch", "rollback" };
        var window = Show(search);
        try
        {
            search.Classes.ShouldContain(SearchResults.EmptyPseudoClass);
            search.EmptyStateControl!.Title.ShouldBe("No results for “zzz”");
            search.EmptyStateControl.Description.ShouldBe("Check the spelling or try fewer words.");
            search.ClearFiltersButton!.IsVisible.ShouldBeFalse();
            search.SuggestionButtons.Select(b => b.Content).ShouldBe(["launch", "rollback"]);
            search.HintsControl!.IsVisible.ShouldBeFalse();

            // A suggestion fills the query and records it.
            search.SuggestionButtons[0].RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.Query.ShouldBe("launch");
            search.Recent.ToList().ShouldBe(["launch"]);
            search.ResultCount.ShouldBe(4);

            // Filters that hide everything: say so and offer to clear them.
            Filter(search, "kind").Values[0].IsSelected = true;
            Filter(search, "team").Values[1].IsSelected = true;
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(0);
            search.EmptyStateControl.Description.ShouldBe("The filters leave nothing. Remove one to see more.");
            search.ClearFiltersButton.IsVisible.ShouldBeTrue();
            search.ClearFiltersButton.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.HasActiveFilters.ShouldBeFalse();
            search.ResultCount.ShouldBe(4);
            search.Classes.ShouldNotContain(SearchResults.EmptyPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Server_Side_Mode_Shows_Items_As_Given_And_Reports_Changes()
    {
        var search = Create("anything");
        search.IsFilteringEnabled = false;
        var type = Filter(search, "kind");
        var window = Show(search);
        try
        {
            // The server's order and the server's items: nothing is matched, filtered or sorted here.
            search.ResultCount.ShouldBe(8);
            search.VisibleItems.Select(i => i.Id).Take(3).ShouldBe(["checklist", "rollback", "pricing"]);
            search.SortOrder = SearchSortOrder.Title;
            Dispatcher.UIThread.RunJobs();
            search.VisibleItems.Select(i => i.Id).Take(3).ShouldBe(["checklist", "rollback", "pricing"]);
            // The words are still marked.
            search.Query = "launch";
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(8);
            search.Rows[0].TitleText!.Ranges.ShouldBe([new SearchMatchRange(0, 6)]);

            // The counts are the app's: the control reads them and never writes them.
            type.Values[0].Count = 120;
            type.Values[1].Count = 0;
            type.Values[2].Count = 7;
            Dispatcher.UIThread.RunJobs();
            var rows = search.FiltersView!.Groups.First().Rows;
            rows.Select(r => r.CountText).ShouldBe(["120", "0", "7"]);
            search.Query = "launch c";
            Dispatcher.UIThread.RunJobs();
            type.Values.Select(v => v.Count).ShouldBe([120, 0, 7]);
            rows[1].Classes.ShouldContain(SearchResultsFilterRow.DimmedPseudoClass);

            // Every change that needs a new request is reported with its reason.
            var reasons = new List<SearchChangeReason>();
            search.SearchChanged += (_, e) => reasons.Add(e.Reason);
            search.Query = "plan";
            type.Values[0].IsSelected = true;
            search.SortOrder = SearchSortOrder.Newest;
            search.ClearFilters();
            reasons.ShouldBe([SearchChangeReason.Query, SearchChangeReason.Filters, SearchChangeReason.Sort, SearchChangeReason.Filters]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Long_Results_Page_In_And_Keep_Their_Rows()
    {
        // Tall rows (a snippet each), so the first page is longer than the viewport and nothing loads by itself.
        var items = Enumerable.Range(1, 40)
                              .Select(i => new SearchResultItem
                              {
                                  Id = "i" + i,
                                  Title = $"Launch note {i}",
                                  Kind = "Docs",
                                  Subtitle = "Doc",
                                  Snippet = "A passage of the note that runs long enough to need two lines when the list is shown at this width, with a launch in it.",
                                  Caption = "note@example.test"
                              })
                              .ToList();
        var search = Create("launch", items, filters: false);
        search.PageSize = 15;
        var window = Show(search);
        try
        {
            search.ResultCount.ShouldBe(40);
            search.Rows.Count.ShouldBe(15);
            var more = search.MoreButton!;
            more.IsVisible.ShouldBeTrue();
            more.Content.ShouldBe("Show more");
            var first = search.Rows.ToList();

            more.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.Rows.Count.ShouldBe(30);
            // The rows that were listed are the same controls: nothing is rebuilt.
            search.Rows.Take(15).ShouldBe(first);
            // The cursor lands on the first new result.
            search.ActiveItem!.Id.ShouldBe("i16");
            more.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            search.Rows.Count.ShouldBe(40);
            more.IsVisible.ShouldBeFalse();

            // A new query starts from the first page again.
            search.Query = "note";
            Dispatcher.UIThread.RunJobs();
            search.Rows.Count.ShouldBe(15);
            search.Rows.Take(15).ShouldBe(first);
            search.Rows[0].Item!.Id.ShouldBe("i1");

            // The keyboard asks for the next page too.
            search.SearchFieldControl!.Focus();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.End);
            search.CursorIndex.ShouldBe(14);
            Press(window, PhysicalKey.ArrowDown);
            search.Rows.Count.ShouldBe(30);
            search.CursorIndex.ShouldBe(15);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Hug_Their_Content_With_No_Reserved_Lines()
    {
        var items = new List<SearchResultItem>
        {
            new() { Id = "full", Title = "Launch full", Kind = "Docs", Subtitle = "Doc in Handbook", Snippet = "A short passage with a launch in it.", Caption = "full@example.test" },
            new() { Id = "snippet", Title = "Launch snippet", Kind = "Docs", Subtitle = "Doc in Handbook", Snippet = "A short passage with a launch in it." },
            new() { Id = "sub", Title = "Launch subtitle", Kind = "Docs", Subtitle = "Doc in Handbook" },
            new() { Id = "bare", Title = "Launch bare" },
            new() { Id = "two", Title = "Launch two lines", Kind = "Docs", Subtitle = "Doc in Handbook", Snippet = string.Join(' ', Enumerable.Repeat("A long passage with a launch in it that wraps onto a second line when the list is narrow.", 3)) },
            new() { Id = "body", Title = "Launch body", Kind = "Docs", Subtitle = "Doc in Handbook", Snippet = "Nothing here.", Body = "Intro paragraph.\n\nThe launch is the second paragraph.\n\nOutro paragraph." },
            new() { Id = "person", Title = "Launch Person", AvatarName = "Launch Person" }
        };
        var search = Create("launch", items, filters: false);
        var window = Show(search, width: 640);
        try
        {
            Settle();
            double Height(string id) => search.Rows.Single(r => r.Item!.Id == id).Bounds.Height;
            var rows = search.Rows.ToDictionary(r => r.Item!.Id);
            // Lines that are not there take no room: the caption, snippet and subtitle are hidden, not empty.
            rows["snippet"].CaptionText!.IsVisible.ShouldBeFalse();
            rows["sub"].SnippetText!.IsVisible.ShouldBeFalse();
            rows["bare"].SubtitleText!.IsVisible.ShouldBeFalse();
            rows["full"].CaptionText!.IsVisible.ShouldBeTrue();

            // Each row is its inset (14 + 12) plus the lines it shows: title 22, subtitle 2 + 17, snippet 6 + 20 per line, caption 10 + 17.
            const double inset = 26;
            Height("bare").ShouldBe(inset + 22, 1);
            Height("sub").ShouldBe(inset + 22 + 19, 1);
            Height("snippet").ShouldBe(inset + 22 + 19 + 26, 1);
            Height("full").ShouldBe(inset + 22 + 19 + 26 + 27, 1);
            // A second snippet line adds exactly one line; a body paragraph is one flowing line, not the paragraphs with their breaks.
            (Height("two") - Height("snippet")).ShouldBe(20, 1);
            Height("body").ShouldBe(Height("snippet"), 1);
            rows["body"].SnippetText!.PlainText.ShouldBe("The launch is the second paragraph.");
            // An avatar row is as tall as its avatar (36) plus the inset when its text is shorter.
            Height("person").ShouldBe(inset + 36, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Geometry_Icon_Is_A_Small_Muted_Glyph_On_The_Title_Line()
    {
        var items = new List<SearchResultItem>
        {
            new() { Id = "doc", Title = "Launch doc", Kind = "Docs", Subtitle = "Doc in Handbook", Snippet = "A passage.", Icon = ArcIcons.FileText },
            new() { Id = "default", Title = "Launch default", Kind = "Docs" },
            new() { Id = "control", Title = "Launch control", Kind = "Docs", Icon = new Border { Width = 30, Height = 30, Name = "Custom" } }
        };
        var search = Create("launch", items, filters: false);
        var window = Show(search, width: 640);
        try
        {
            Settle();
            foreach (var id in new[] { "doc", "default" })
            {
                var row = search.Rows.Single(r => r.Item!.Id == id);
                var glyph = row.LeadContent.ShouldBeOfType<ArcGlyph>();
                glyph.Size.ShouldBe(16);
                glyph.StrokeThickness.ShouldBe(1.75);
                glyph.Bounds.Size.ShouldBe(new Size(16, 16));
                // Centered in the 36px lead column, on the middle of the title's first line (22 high).
                var lead = TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(row, "PART_Lead");
                lead.Bounds.Width.ShouldBe(36);
                var glyphCenter = glyph.TranslatePoint(new Point(8, 8), row)!.Value;
                var title = row.TitleText!;
                var titleTop = title.TranslatePoint(default, row)!.Value.Y;
                glyphCenter.Y.ShouldBe(titleTop + 11, 1);
                glyphCenter.X.ShouldBe(lead.TranslatePoint(new Point(18, 0), row)!.Value.X, 1);
                // Muted, not the full-contrast foreground (the row under the cursor turns up its contrast).
                var expected = row.IsActive ? DesignTokens.ArcDesignTokenKind.Foreground : DesignTokens.ArcDesignTokenKind.TextSecondary;
                TestHelpers.ColorOf(glyph.Foreground).ShouldBe(ArcTokens.GetColor(search, expected));
            }
            // A control icon is left alone.
            var custom = search.Rows.Single(r => r.Item!.Id == "control");
            var border = custom.LeadContent.ShouldBeOfType<Border>();
            border.Name.ShouldBe("Custom");
            border.Bounds.Size.ShouldBe(new Size(30, 30));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Key_Hints_Sit_Under_The_List_And_Never_Over_A_Row()
    {
        // Fewer rows than a page, so scrolling to the end does not load more.
        var items = Enumerable.Range(1, 12)
                              .Select(i => new SearchResultItem { Id = "i" + i, Title = $"Launch note {i}", Kind = "Docs", Subtitle = "Doc", Snippet = "A passage that runs long enough to take two lines when the list is shown at this width, with a launch in it." })
                              .ToList();
        var search = Create("launch", items, filters: false);
        var window = Show(search);
        try
        {
            Settle();
            var layout = TestHelpers.Descendant<SearchResultsPaneLayout>(search);
            var scroll = TestHelpers.Descendant<ScrollViewer>(search, SearchResults.ResultsScrollPart);
            var hints = search.HintsControl!;
            hints.IsVisible.ShouldBeTrue();
            // A long list fills the pane; the hints are their own row below the scrolling area, a gap clear of it.
            var scrollBottom = scroll.TranslatePoint(new Point(0, scroll.Bounds.Height), layout)!.Value.Y;
            var hintsTop = hints.TranslatePoint(default, layout)!.Value.Y;
            hintsTop.ShouldBeGreaterThanOrEqualTo(scrollBottom + 14 - 0.5);
            (hintsTop + hints.Bounds.Height).ShouldBeLessThanOrEqualTo(layout.Bounds.Height + 0.5);
            // The last row can scroll fully clear of the hints: the list ends with room under its last row.
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            Settle();
            var last = search.Rows[^1];
            var lastBottom = last.TranslatePoint(new Point(0, last.Bounds.Height), layout)!.Value.Y;
            lastBottom.ShouldBeLessThanOrEqualTo(hintsTop - 14 + 0.5);

            // A short list leaves the hints right under its rows, not at the bottom of the pane.
            search.Query = "launch note 12";
            Settle();
            search.Rows.Count.ShouldBe(1);
            var shortHintsTop = hints.TranslatePoint(default, layout)!.Value.Y;
            var row = search.Rows[0];
            var rowBottom = row.TranslatePoint(new Point(0, row.Bounds.Height), layout)!.Value.Y;
            (shortHintsTop - rowBottom).ShouldBeLessThan(60);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Suggestions_Popup_Matches_Its_Geometry()
    {
        var search = Create("launch");
        search.Recent.AddRange(["launch", "rollback"]);
        search.Suggestions = new List<string> { "release notes" };
        var window = Show(search);
        try
        {
            search.FocusSearch();
            Dispatcher.UIThread.RunJobs();
            Settle();
            var field = search.SearchFieldControl!;
            var list = search.SuggestionList!;
            var surface = list.GetVisualAncestors().OfType<Border>().First();
            // As wide as the field, a few pixels under it, 16 radius, 6 inset.
            surface.Bounds.Width.ShouldBe(field.Bounds.Width, 1);
            surface.CornerRadius.ShouldBe(new CornerRadius(16));
            surface.Padding.ShouldBe(new Thickness(6));
            var fieldBottom = field.TranslatePoint(new Point(0, field.Bounds.Height), window)!.Value.Y;
            var surfaceTop = surface.TranslatePoint(default, window)!.Value.Y;
            (surfaceTop - fieldBottom).ShouldBe(6, 1);
            surface.TranslatePoint(default, window)!.Value.X.ShouldBe(field.TranslatePoint(default, window)!.Value.X, 1);

            // Group labels are small and muted; rows are 40 high with 16px icons, text at regular weight.
            var labels = list.GetVisualDescendants().OfType<SearchResultsSuggestionLabel>().ToList();
            labels.Select(l => l.Text).ShouldBe(["Recent", "Suggested"]);
            foreach (var label in labels)
            {
                var text = TestHelpers.Descendant<TextBlock>(label);
                text.FontSize.ShouldBe(12);
                TestHelpers.ColorOf(text.Foreground).ShouldBe(ArcTokens.GetColor(search, DesignTokens.ArcDesignTokenKind.TextMuted));
            }
            foreach (var row in list.Rows)
            {
                row.Bounds.Height.ShouldBe(40);
                var glyphs = row.GetVisualDescendants().OfType<ArcGlyph>().Where(g => g.IsVisible).ToList();
                glyphs.First().Size.ShouldBe(16);
                TestHelpers.Descendant<TextBlock>(row).FontWeight.ShouldBe(Avalonia.Media.FontWeight.Normal);
            }
            // The active row has the muted block behind it and the return glyph at its right edge.
            list.Rows[0].IsActive.ShouldBeTrue();
            var enter = TestHelpers.Descendant<ArcGlyph>(list.Rows[0], "PART_Enter");
            enter.IsVisible.ShouldBeTrue();
            enter.TranslatePoint(new Point(enter.Bounds.Width, 0), list.Rows[0])!.Value.X.ShouldBe(list.Rows[0].Bounds.Width - 12, 1.5);
            list.Rows[1].GetVisualDescendants().OfType<ArcGlyph>().First(g => g.Name == "PART_Enter").IsVisible.ShouldBeFalse();
            list.CursorView!.TargetRect.Height.ShouldBe(40, 0.5);
            list.CursorView.CornerRadius.ShouldBe(new CornerRadius(12));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Search_Starts_At_The_Top_Of_The_List()
    {
        var items = Enumerable.Range(1, 30)
                              .Select(i => new SearchResultItem { Id = "i" + i, Title = $"Launch note {i}", Kind = "Docs", Subtitle = "Doc", Snippet = "A passage that runs long enough to take two lines when the list is shown at this width, with a launch in it." })
                              .ToList();
        var search = Create("launch", items, filters: false);
        var window = Show(search);
        try
        {
            var scroll = TestHelpers.Descendant<ScrollViewer>(search, SearchResults.ResultsScrollPart);
            scroll.Offset = new Vector(0, 300);
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.Y.ShouldBeGreaterThan(0);
            search.Query = "launch note";
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.Y.ShouldBe(0);

            // So does a filter or a new sort order (they are new searches); a longer page does not move it.
            scroll.Offset = new Vector(0, 300);
            Dispatcher.UIThread.RunJobs();
            search.SortOrder = SearchSortOrder.Title;
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Control_Out_Of_The_Window_Lets_Go_Of_Its_Filters()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var host = (Border)search.Parent!;
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            // The filters may live longer than the control: it no longer listens to them while it is gone.
            Filter(search, "kind").Values[2].IsSelected = true;
            search.ResultCount.ShouldBe(4);

            host.Child = search;
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(2);
            Filter(search, "kind").Values[2].IsSelected = false;
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Filter_Objects_Report_Their_State_And_Clear()
    {
        var type = new SearchValueFilter { Key = "kind", Values = { new SearchFilterValue("Docs"), new SearchFilterValue("Files", "Documents") } };
        type.IsActive.ShouldBeFalse();
        type.Values[1].DisplayLabel.ShouldBe("Documents");
        type.Values[0].DisplayLabel.ShouldBe("Docs");
        type.Values[0].IsSelected = true;
        type.IsActive.ShouldBeTrue();
        type.SelectedValues.Select(v => v.Value).ShouldBe(["Docs"]);
        type.Clear();
        type.IsActive.ShouldBeFalse();

        var date = new SearchDateFilter { Key = "edited", AppliesTo = "Docs, files" };
        date.IsActive.ShouldBeFalse();
        date.Start = s_day0;
        date.IsActive.ShouldBeTrue();
        date.Clear();
        date.Start.ShouldBeNull();
        date.End.ShouldBeNull();
        date.AppliesToKinds().ShouldBe(["Docs", "files"]);
        new SearchDateFilter().AppliesToKinds().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Collection_And_Definition_Changes_Update_The_Results()
    {
        var source = new ObservableCollection<SearchResultItem>(Workspace().Take(3));
        var search = Create(items: source);
        var window = Show(search);
        try
        {
            search.ResultCount.ShouldBe(3);
            source.Add(File("late", "Launch late.zip", "Growth", 30));
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(4);
            // The filter lists the kinds it finds, new ones included.
            Filter(search, "kind").Values.Select(v => v.Value).ShouldBe(["Docs", "Files"]);
            source.Clear();
            Dispatcher.UIThread.RunJobs();
            search.ResultCount.ShouldBe(0);
            search.Classes.ShouldContain(SearchResults.EmptyPseudoClass);

            // Inline items and a changed attribute work the same way (Refresh runs the search again).
            var inline = new SearchResults { Query = "plan" };
            inline.Filters.Add(new SearchValueFilter { Key = "team", Title = "Team" });
            inline.Items.Add(new SearchResultItem { Title = "Planning notes" });
            inline.ResultCount.ShouldBe(1);
            inline.Items.Add(new SearchResultItem { Title = "Plan B" });
            inline.ResultCount.ShouldBe(2);
            inline.Items.RemoveAt(0);
            inline.ResultCount.ShouldBe(1);
            var plan = inline.Items.Single();
            plan.Attributes["team"] = "Design";
            inline.Refresh();
            ((SearchValueFilter)inline.Filters[0]).Values.Select(v => v.Value).ShouldBe(["Design"]);

            // A filter added later takes part at once.
            search.ItemsSource = Workspace();
            Dispatcher.UIThread.RunJobs();
            search.Filters.Add(new SearchValueFilter { Key = "extra", Title = "Extra", Values = { new SearchFilterValue("none") } });
            Dispatcher.UIThread.RunJobs();
            search.FiltersView!.Groups.Count().ShouldBe(3);
            search.Filters.Clear();
            Dispatcher.UIThread.RunJobs();
            search.Classes.ShouldContain(SearchResults.NoFiltersPseudoClass);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Query_That_Grows_Searches_The_Previous_Matches_Only()
    {
        var search = Create("l");
        var window = Show(search);
        try
        {
            search.Engine.LastCandidateCount.ShouldBe(8);
            var matches = search.Engine.QueryHits.Count;
            search.Query = "la";
            Dispatcher.UIThread.RunJobs();
            search.Engine.LastCandidateCount.ShouldBe(matches);
            // Ticking a filter does not search the text again.
            Filter(search, "kind").Values[0].IsSelected = true;
            Dispatcher.UIThread.RunJobs();
            search.Engine.LastCandidateCount.ShouldBe(matches);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Expose_Listbox_Semantics_And_Filters_Are_Named_With_Their_Counts()
    {
        var search = Create("launch");
        var window = Show(search);
        try
        {
            var list = ControlAutomationPeer.CreatePeerForElement(search.ListHostControl!);
            list.GetAutomationControlType().ShouldBe(AutomationControlType.List);
            list.GetName().ShouldBe("4 results");
            var peer = ControlAutomationPeer.CreatePeerForElement(search.Rows[0]);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ListItem);
            ((Avalonia.Automation.Provider.ISelectionItemProvider)peer).IsSelected.ShouldBeTrue();
            ((Avalonia.Automation.Provider.ISelectionItemProvider)ControlAutomationPeer.CreatePeerForElement(search.Rows[1])).IsSelected.ShouldBeFalse();

            var row = search.FiltersView!.Groups.First().Rows[0];
            AutomationProperties.GetName(row.Check!).ShouldBe("Docs, 1");
            AutomationProperties.GetName(search.FiltersView.Groups.First()).ShouldBe("Type");
            var slider = search.FiltersView.DateRanges.Single().SliderControl!;
            slider.LowerThumbName.ShouldBe("Edited start");
            slider.UpperThumbName.ShouldBe("Edited end");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Reveals_New_Results_And_Runs_Every_Transition_Without_Throwing()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var search = Create("launch");
        search.Recent.AddRange(["launch", "rollback"]);
        var window = Show(search, level: ArcMotionLevel.Full);
        try
        {
            Run(clock, 100);
            // Narrowing only removes results: nothing fades in.
            search.Query = "launch checklist";
            Dispatcher.UIThread.RunJobs();
            search.Rows.Single().Opacity.ShouldBe(1);

            // Results that were not on screen fade in and rise.
            search.Query = "soren";
            Dispatcher.UIThread.RunJobs();
            var fresh = search.Rows.Single();
            Run(clock, 16);
            fresh.Opacity.ShouldBeLessThan(1);
            Run(clock, 600);
            fresh.Opacity.ShouldBe(1, 0.001);
            ((Avalonia.Media.TranslateTransform)fresh.RenderTransform!).Y.ShouldBe(0, 0.001);

            // Filters, the bar, the popup, the detail and loading.
            search.Query = "launch";
            Filter(search, "kind").Values[2].IsSelected = true;
            Run(clock, 700);
            search.ChipsHost!.IsExpanded.ShouldBeTrue();
            search.SortOrder = SearchSortOrder.Newest;
            Run(clock, 300);
            search.FocusSearch();
            Run(clock, 400);
            search.AreSuggestionsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.Escape);
            Run(clock, 300);
            search.ShowDetail(search.VisibleItems[0]);
            Run(clock, 700);
            search.CloseDetail();
            Run(clock, 700);
            search.IsLoading = true;
            Run(clock, 200);
            search.IsLoading = false;
            Run(clock, 900);
            search.ResultCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Dates are written in the current culture; the expectations are English.</summary>
    private static IDisposable English()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        return new Restore(() => System.Globalization.CultureInfo.CurrentCulture = before);
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
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
