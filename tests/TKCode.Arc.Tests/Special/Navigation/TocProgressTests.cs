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
                                                                            Action<TocProgress>? setup = null, double lastSpacer = 360)
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
            stack.Children.Add(new Border { Height = i == titles.Length - 1 ? lastSpacer : 360 });
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

    /// <summary>Runs the clock frame by frame with a layout pass after each, as a window would.</summary>
    private static void Run(ManualFrameClock clock, TimeSpan duration)
    {
        for (var t = TimeSpan.Zero; t < duration; t += Frame)
        {
            clock.Advance(Frame, Frame);
            Pump(1);
        }
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
    public void Scroll_target_follows_the_layout()
    {
        TocProgressMath.OffsetFor(800, 16, 400, 2000).ShouldBe(784);
        TocProgressMath.OffsetFor(10, 16, 400, 2000).ShouldBe(0);
        TocProgressMath.OffsetFor(1900, 16, 400, 2000).ShouldBe(1600);
    }

    [Fact]
    public void Near_the_end_the_reading_line_eases_down_to_the_bottom_of_the_viewport()
    {
        // Viewport 400, extent 2000 (max offset 1600), line 100 down: the last 300px of scrolling ease the line by t².
        TocProgressMath.ReadingLine(0, 400, 2000, 100).ShouldBe(100);
        TocProgressMath.ReadingLine(1300, 400, 2000, 100).ShouldBe(1400);
        TocProgressMath.ReadingLine(1450, 400, 2000, 100).ShouldBe(1450 + 100 + 300 * 0.25, 1e-9);
        TocProgressMath.ReadingLine(1600, 400, 2000, 100).ShouldBe(2000);

        // So the last section fills completely at the bottom, even when its heading is close to the end.
        double[] tops = [0, 900, 1900];
        var p = TocProgressMath.ReadingPosition(TocProgressMath.LocateAt(tops, 1599, 400, 2000, 100));
        p.ShouldBeGreaterThan(2.9);
        TocProgressMath.ReadingPosition(TocProgressMath.LocateAt(tops, 1600, 400, 2000, 100)).ShouldBe(3);
    }

    [Fact]
    public void Minutes_left_count_down_with_the_reading_position_and_end_on_finished()
    {
        TocProgressMath.MinutesLeft(0, 10, 6).ShouldBe(6);
        TocProgressMath.MinutesLeft(1.65, 10, 6).ShouldBe(6);
        TocProgressMath.MinutesLeft(1.87, 10, 6).ShouldBe(5);
        TocProgressMath.MinutesLeft(9.99, 10, 6).ShouldBe(1);
        TocProgressMath.MinutesLeft(10, 10, 6).ShouldBe(0);
        TocProgressMath.CountWords("  two words\nand three  ").ShouldBe(4);
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
    public void Scrolling_moves_the_active_section_the_read_rows_and_the_marker()
    {
        var (toc, article, window) = Open();
        var changes = new List<(int Old, int New)>();
        toc.ActiveChanged += (_, e) => changes.Add((e.OldIndex, e.NewIndex));
        toc.ActiveIndex.ShouldBe(0);

        Scroll(article, 820);
        toc.ActiveIndex.ShouldBe(2);
        toc.Reading.ShouldBe(2 + (820 + 100 - 800) / 400.0, 1e-6);
        toc.List!.Rows[0].State.ShouldBe(TocProgressRowState.Read);
        toc.List.Rows[1].State.ShouldBe(TocProgressRowState.Read);
        toc.List.Rows[2].State.ShouldBe(TocProgressRowState.Active);
        toc.List.Rows[3].State.ShouldBe(TocProgressRowState.Upcoming);
        toc.Progress.ShouldBe(820 / (article.Extent.Height - article.Viewport.Height), 1e-6);
        changes.ShouldContain((0, 2));
        AutomationProperties.GetHelpText(toc.List.Rows[2]).ShouldBe("current section");
        AutomationProperties.GetHelpText(toc.List.Rows[0]).ShouldBe("read");

        // The marker rides the line between the current heading's point and the next one's.
        var marker = toc.List.MarkerPoint;
        marker.Y.ShouldBeGreaterThan(toc.List.HeadingPoint(2).Y);
        marker.Y.ShouldBeLessThan(toc.List.HeadingPoint(3).Y);

        Scroll(article, article.Extent.Height);
        toc.ActiveIndex.ShouldBe(4);
        toc.Progress.ShouldBe(1);
        toc.Classes.Contains(":done").ShouldBeTrue();
        toc.List.MarkerDistance.ShouldBe(toc.List.LineLength, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void The_line_bends_into_the_lane_of_each_level()
    {
        var (toc, _, window) = Open();
        var list = toc.List!;
        // Lanes are 12px apart (level 2 is the third heading), and the line starts on the first heading's point.
        list.HeadingPoint(1).X.ShouldBe(5, 0.01);
        list.HeadingPoint(2).X.ShouldBe(17, 0.01);
        list.HeadingPoint(3).X.ShouldBe(5, 0.01);
        list.HeadingPoint(0).Y.ShouldBe(list.Rows[0].Bounds.Height / 2, 0.01);
        // Rows of 13px text with 4px padding; text indented 22px plus 12px per level.
        list.Rows[0].Bounds.Height.ShouldBe(26.2, 1);
        list.Rows[0].TextLeft.ShouldBe(22);
        list.Rows[2].TextLeft.ShouldBe(34);
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

        // With a reading offset the heading lands on the reading line, so the marker rests on its point.
        toc.ReadingOffset = 40;
        toc.ScrollTo(1);
        Pump();
        article.Offset.Y.ShouldBe(400 - 40, 0.5);
        toc.Reading.ShouldBe(1, 1e-6);
        window.Close();
    }

    [AvaloniaFact]
    public void A_section_the_article_cannot_bring_to_the_line_stays_current_until_the_reader_scrolls()
    {
        var (toc, article, window) = Open(lastSpacer: 100);
        toc.ScrollTo(4);
        Pump();
        article.Offset.Y.ShouldBe(article.Extent.Height - article.Viewport.Height, 0.5);
        toc.ActiveIndex.ShouldBe(4);
        toc.Reading.ShouldBe(4, 1e-6);

        Scroll(article, article.Offset.Y - 40);
        toc.Reading.ShouldBeGreaterThan(4.05);
        window.Close();
    }

    [AvaloniaFact]
    public void The_reading_position_follows_the_scroll_on_a_spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (toc, article, window) = Open(ArcMotionLevel.Full);
        toc.Reading.ShouldBe(0.25, 1e-6);
        Scroll(article, 820);
        clock.Advance(Frame * 6, Frame);
        Pump();
        toc.Reading.ShouldBeGreaterThan(0.6);
        toc.Reading.ShouldBeLessThan(2.0);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        Pump();
        toc.Reading.ShouldBe(2.3, 0.01);
        toc.ActiveIndex.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void A_glide_traces_through_the_sections_and_the_reader_can_take_over()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (toc, article, window) = Open(ArcMotionLevel.Full);
        var seen = new List<int>();
        toc.ActiveChanged += (_, e) => seen.Add(e.NewIndex);
        toc.ScrollTo(4);
        Pump();
        Run(clock, Frame * 6);
        article.Offset.Y.ShouldBeGreaterThan(0);
        article.Offset.Y.ShouldBeLessThan(4 * 400 - 16);
        toc.ActiveIndex.ShouldBeLessThan(4);

        Run(clock, TimeSpan.FromSeconds(2));
        toc.ActiveIndex.ShouldBe(4);
        seen.ShouldContain(2);

        // A wheel or scroll bar move stops a glide.
        toc.ScrollTo(0);
        Pump();
        Run(clock, Frame * 4);
        Scroll(article, 900);
        Run(clock, TimeSpan.FromSeconds(1));
        article.Offset.Y.ShouldBe(900, 0.5);
        toc.ActiveIndex.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_walks_the_rows_and_Enter_jumps()
    {
        var (toc, article, window) = Open();
        var rows = toc.List!.Rows;
        // Every row is a link in the tab order.
        rows.ShouldAllBe(r => KeyboardNavigation.GetIsTabStop(r));
        rows[0].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        rows[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        rows[4].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        rows[3].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        article.Offset.Y.ShouldBe(3 * 400 - 16, 0.5);
        TestHelpers.IsFocusRingVisible(rows[3]).ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        rows[4].IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void The_time_left_counts_down_and_ends_on_finished()
    {
        var (toc, article, window) = Open(setup: t => t.ReadingTime = 6);
        var swap = TestHelpers.Descendant<ArcContentSwap>(toc, TocProgress.TimeLeftPart);
        swap.IsVisible.ShouldBeTrue();
        swap.Content.ShouldBe("6 min left");
        Scroll(article, 1000);
        // Reading position 2.75 of 5: 6 × (1 − 0.55) = 2.7, rounded up.
        swap.Content.ShouldBe("3 min left");
        Scroll(article, article.Extent.Height);
        swap.Content.ShouldBeAssignableTo<Control>();
        TestHelpers.Descendant<TextBlock>((Control)swap.Content!).Text.ShouldBe("Finished");

        toc.ShowsTimeLeft = false;
        Pump();
        swap.IsVisible.ShouldBeFalse();
        TestHelpers.Descendant<TextBlock>(toc, TocProgress.PercentPart).IsVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void The_reading_time_is_estimated_from_the_words_of_the_article()
    {
        var (toc, _, window) = Open();
        // Five one-word headings at 230 words a minute.
        toc.TotalMinutes.ShouldBe(5 / 230.0, 1e-9);
        TestHelpers.Descendant<ArcContentSwap>(toc, TocProgress.TimeLeftPart).Content.ShouldBe("1 min left");
        window.Close();
    }

    [AvaloniaFact]
    public void Rows_take_their_text_color_from_their_state()
    {
        var (toc, article, window) = Open();
        Scroll(article, 820);
        var rows = toc.List!.Rows;
        rows[2].TextColor.ShouldBe(ArcTokens.GetColor(rows[2], TKCode.Arc.DesignTokens.ArcDesignTokenKind.Foreground));
        rows[0].TextColor.ShouldBe(ArcTokens.GetColor(rows[0], TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextSecondary));
        rows[4].TextColor.ShouldBe(ArcTokens.GetColor(rows[4], TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted));
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
        var (toc, article, window) = Open(setup: t =>
        {
            t.Labels = new TocProgressLabels { Title = "Contents", Percent = p => $"{p} pct" };
            t.ShowsTimeLeft = false;
        });
        Scroll(article, (article.Extent.Height - article.Viewport.Height) / 2);
        TestHelpers.Descendant<TextBlock>(toc, TocProgress.TitlePart).Text.ShouldBe("Contents");
        TestHelpers.Descendant<TextBlock>(toc, TocProgress.PercentPart).Text.ShouldBe("50 pct");
        TestHelpers.Descendant<TextBlock>(toc, TocProgress.PercentPart).IsVisible.ShouldBeTrue();
        AutomationProperties.GetName(toc).ShouldBe("Contents");
        window.Close();
    }
}
