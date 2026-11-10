using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Publishing;

/// <summary>
/// The table of contents progress: the reading-position rules, collecting headings, following the scroll (active row,
/// read rows, rail fill, percentage, events), jumping to a section, the keyboard model, entries by hand and compact mode.
/// </summary>
public class TocProgressTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (TocProgress Toc, ScrollViewer Article, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off,
                                                                            Action<TocProgress>? setup = null)
    {
        var stack = new StackPanel { Spacing = 0 };
        string[] titles = ["Intro", "Setup", "Options", "Usage", "Notes"];
        for (var i = 0; i < titles.Length; i++)
        {
            var heading = new TextBlock { Text = titles[i], Height = 40 };
            TocProgress.SetHeading(heading, titles[i]);
            if (i == 2)
            {
                TocProgress.SetLevel(heading, 2);
            }
            stack.Children.Add(heading);
            stack.Children.Add(new Border { Height = 360 });
        }
        var article = new ScrollViewer { Content = stack, Height = 400 };
        var toc = new TocProgress { Target = article, Width = 220, DisplayMode = TocProgressDisplayMode.List };
        setup?.Invoke(toc);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(article);
        Grid.SetColumn(toc, 1);
        grid.Children.Add(toc);
        var window = TestHelpers.Show(grid, level);
        window.Width = 900;
        window.Height = 600;
        Pump();
        return (toc, article, window);
    }

    private static void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Scroll(ScrollViewer article, double y)
    {
        article.Offset = new Vector(0, y);
        Pump();
    }

    // ---- Pure rules ----

    [Fact]
    public void Locate_finds_the_last_heading_above_the_reading_line()
    {
        double[] tops = [0, 400, 800, 1200];
        // Viewport 400, extent 2000: the line is 100 below the top.
        var start = TocProgressMath.Locate(tops, 0, 400, 2000, 0.25);
        start.ActiveIndex.ShouldBe(0);
        start.SectionProgress.ShouldBe(0.25, 1e-9);
        start.Progress.ShouldBe(0);

        var mid = TocProgressMath.Locate(tops, 350, 400, 2000, 0.25);
        mid.ActiveIndex.ShouldBe(1);
        mid.SectionProgress.ShouldBe(50.0 / 400, 1e-9);
        mid.Progress.ShouldBe(350.0 / 1600, 1e-9);

        // The last section runs to the end of the article.
        var last = TocProgressMath.Locate(tops, 1150, 400, 2000, 0.25);
        last.ActiveIndex.ShouldBe(3);
        last.SectionProgress.ShouldBe(50.0 / 800, 1e-9);
    }

    [Fact]
    public void Locate_is_before_the_first_heading_until_it_is_reached_and_complete_at_the_end()
    {
        double[] tops = [300, 700];
        TocProgressMath.Locate(tops, 0, 400, 2000, 0.25).ActiveIndex.ShouldBe(-1);
        var end = TocProgressMath.Locate(tops, 1600, 400, 2000, 0.25);
        end.ShouldBe(new TocProgressPosition(1, 1, 1));
        // Nothing to scroll: everything is read.
        TocProgressMath.Locate(tops, 0, 400, 300, 0.25).Progress.ShouldBe(1);
        TocProgressMath.Locate([], 0, 400, 2000, 0.25).ActiveIndex.ShouldBe(-1);
    }

    [Fact]
    public void Scroll_target_and_fill_end_follow_the_layout()
    {
        TocProgressMath.OffsetFor(800, 16, 400, 2000).ShouldBe(784);
        TocProgressMath.OffsetFor(10, 16, 400, 2000).ShouldBe(0);
        TocProgressMath.OffsetFor(1900, 16, 400, 2000).ShouldBe(1600);
        double[] rows = [0, 30, 60];
        double[] heights = [30, 30, 50];
        TocProgressMath.FillEnd(rows, heights, 2, 0.5).ShouldBe(85);
        TocProgressMath.FillEnd(rows, heights, -1, 0.5).ShouldBe(0);
    }

    // ---- Following the article ----

    [AvaloniaFact]
    public void Collects_the_marked_headings_in_order_with_their_levels()
    {
        var (toc, _, window) = Open();
        toc.Sections.Select(s => s.Title).ShouldBe(["Intro", "Setup", "Options", "Usage", "Notes"]);
        toc.Sections[2].Level.ShouldBe(2);
        toc.List!.Rows.Count.ShouldBe(5);
        toc.List.Rows[2].TextLeft.ShouldBeGreaterThan(toc.List.Rows[1].TextLeft);
        window.Close();
    }

    [AvaloniaFact]
    public void Scrolling_moves_the_active_section_the_read_rows_and_the_percentage()
    {
        var (toc, article, window) = Open();
        var changes = new List<(int Old, int New)>();
        toc.ActiveChanged += (_, e) => changes.Add((e.OldIndex, e.NewIndex));
        toc.ActiveIndex.ShouldBe(0);

        Scroll(article, 820);
        toc.ActiveIndex.ShouldBe(2);
        toc.List!.Rows[0].State.ShouldBe(TocProgressRowState.Read);
        toc.List.Rows[1].State.ShouldBe(TocProgressRowState.Read);
        toc.List.Rows[2].State.ShouldBe(TocProgressRowState.Active);
        toc.List.Rows[3].State.ShouldBe(TocProgressRowState.Upcoming);
        toc.Progress.ShouldBe(820 / (article.Extent.Height - article.Viewport.Height), 1e-6);
        changes.ShouldContain((0, 2));
        AutomationProperties.GetHelpText(toc.List.Rows[2]).ShouldBe("current section");
        AutomationProperties.GetHelpText(toc.List.Rows[0]).ShouldBe("read");

        // The fill ends inside the active row.
        var row = toc.List.Rows[2];
        toc.List.FillEnd.ShouldBeGreaterThanOrEqualTo(row.Bounds.Top - 0.01);
        toc.List.FillEnd.ShouldBeLessThanOrEqualTo(row.Bounds.Bottom + 0.01);

        Scroll(article, article.Extent.Height);
        toc.ActiveIndex.ShouldBe(4);
        toc.Progress.ShouldBe(1);
        toc.Classes.Contains(":done").ShouldBeTrue();
        toc.List.FillEnd.ShouldBe(toc.List.Rows[4].Bounds.Bottom, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Choosing_a_section_scrolls_the_article_to_it()
    {
        var (toc, article, window) = Open();
        toc.ScrollTo(3);
        Pump();
        article.Offset.Y.ShouldBe(3 * 400 - 16, 0.5);
        toc.ActiveIndex.ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void A_glide_highlights_the_chosen_section_and_the_reader_can_take_over()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (toc, article, window) = Open(ArcMotionLevel.Full);
        toc.ScrollTo(4);
        Pump();
        clock.Advance(Frame * 6, Frame);
        Pump();
        article.Offset.Y.ShouldBeGreaterThan(0);
        article.Offset.Y.ShouldBeLessThan(4 * 400 - 16);
        toc.ActiveIndex.ShouldBe(4);

        // A wheel or scroll bar move stops the glide.
        Scroll(article, 100);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        Pump();
        article.Offset.Y.ShouldBe(100, 0.5);
        toc.ActiveIndex.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_walks_the_rows_and_Enter_jumps()
    {
        var (toc, article, window) = Open();
        var rows = toc.List!.Rows;
        KeyboardNavigation.GetIsTabStop(rows[0]).ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(rows[1]).ShouldBeFalse();
        rows[0].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rows[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rows[4].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        rows[3].IsFocused.ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(rows[3]).ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        article.Offset.Y.ShouldBe(3 * 400 - 16, 0.5);
        TestHelpers.IsFocusRingVisible(rows[3]).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Entries_by_hand_replace_the_marked_headings()
    {
        var (toc, article, window) = Open();
        var stack = (StackPanel)article.Content!;
        toc.Entries.Add(new TocProgressEntry { Title = "Start", Target = (Control)stack.Children[0] });
        toc.Entries.Add(new TocProgressEntry { Title = "End", Target = (Control)stack.Children[8], Level = 9 });
        Pump();
        toc.Sections.Count.ShouldBe(2);
        toc.Sections[1].Level.ShouldBe(3);
        Scroll(article, 1700);
        toc.ActiveIndex.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Compact_mode_shows_the_ring_button_and_moves_the_list_into_the_popup()
    {
        var (toc, article, window) = Open(setup: t => t.DisplayMode = TocProgressDisplayMode.Compact);
        toc.IsCompact.ShouldBeTrue();
        toc.Classes.Contains(":compact").ShouldBeTrue();
        var popup = TestHelpers.Descendant<ArcPopup>(toc);
        popup.IsOpen.ShouldBeFalse();
        Scroll(article, 420);
        var button = TestHelpers.Descendant<Avalonia.Controls.Button>(toc, TocProgress.CompactPart);
        AutomationProperties.GetName(button).ShouldBe($"Contents, Setup, {(int)Math.Round(toc.Progress * 100)}% read");
        TestHelpers.Descendant<ArcRing>(toc).Value.ShouldBe(toc.Progress, 1e-9);

        button.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        popup.IsOpen.ShouldBeTrue();
        toc.List!.Rows[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        popup.IsOpen.ShouldBeFalse();

        toc.DisplayMode = TocProgressDisplayMode.List;
        Pump();
        toc.IsCompact.ShouldBeFalse();
        toc.List.IsVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Percentage_and_title_come_from_the_labels()
    {
        var (toc, article, window) = Open(setup: t => t.Labels = new TocProgressLabels { Title = "Contents", Percent = p => $"{p} pct" });
        Scroll(article, (article.Extent.Height - article.Viewport.Height) / 2);
        TestHelpers.Descendant<TextBlock>(toc, TocProgress.TitlePart).Text.ShouldBe("Contents");
        TestHelpers.Descendant<TextBlock>(toc, TocProgress.PercentPart).Text.ShouldBe("50 pct");
        AutomationProperties.GetName(toc).ShouldBe("Contents");
        window.Close();
    }
}
