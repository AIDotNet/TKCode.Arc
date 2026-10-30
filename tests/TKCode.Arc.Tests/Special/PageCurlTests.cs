using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The page curl: fold geometry, the spread layout, turning by tap, key and drag, the tease and motion levels.</summary>
public class PageCurlTests
{
    private const double W = 280;
    private const double H = 400;

    private static double Area(Span<Point> polygon, int count)
    {
        var sum = 0.0;
        for (var i = 0; i < count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }

    // ---- Pure geometry -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(8, 5)]
    [InlineData(9, 5)]
    public void Spread_count_is_the_closed_cover_plus_one_per_two_pages(int pages, int spreads) =>
        PageCurlGeometry.SpreadCount(pages).ShouldBe(spreads);

    [Fact]
    public void Spreads_hold_the_cover_alone_then_pairs_and_an_even_book_ends_on_a_lone_back_cover()
    {
        PageCurlGeometry.LeftPage(0, 8).ShouldBe(0);
        PageCurlGeometry.RightPage(0, 8).ShouldBe(1);
        PageCurlGeometry.LeftPage(1, 8).ShouldBe(2);
        PageCurlGeometry.RightPage(1, 8).ShouldBe(3);
        PageCurlGeometry.LeftPage(4, 8).ShouldBe(8);
        PageCurlGeometry.RightPage(4, 8).ShouldBe(0);
        // An odd book ends on two pages.
        PageCurlGeometry.LeftPage(4, 9).ShouldBe(8);
        PageCurlGeometry.RightPage(4, 9).ShouldBe(9);
    }

    [Fact]
    public void A_lone_cover_slides_to_the_centre_and_so_does_a_lone_back_cover()
    {
        PageCurlGeometry.BookOffset(0, 8, W).ShouldBe(-W / 2);
        PageCurlGeometry.BookOffset(2, 8, W).ShouldBe(0);
        PageCurlGeometry.BookOffset(4, 8, W).ShouldBe(W / 2);
        PageCurlGeometry.BookOffset(4, 9, W).ShouldBe(0);
    }

    [Fact]
    public void The_fold_is_the_bisector_and_the_corner_lands_on_the_pointer()
    {
        var corner = new Point(2 * W, H);
        var pointer = new Point(2 * W - 120, H - 40);
        var fold = PageCurlGeometry.Fold(corner, pointer, W);
        fold.IsValid.ShouldBeTrue();
        fold.Side(fold.Mid).ShouldBe(0, 1e-9);
        fold.Side(corner).ShouldBeLessThan(0);
        fold.Side(pointer).ShouldBeGreaterThan(0);
        // Both ends are the same distance from the fold, and the mirror of one is the other.
        fold.Side(corner).ShouldBe(-fold.Side(pointer), 1e-9);
        var mirrored = PageCurlGeometry.Reflect(fold, corner);
        mirrored.X.ShouldBe(pointer.X, 1e-9);
        mirrored.Y.ShouldBe(pointer.Y, 1e-9);
    }

    [Fact]
    public void Reflection_twice_is_the_identity()
    {
        var fold = PageCurlGeometry.Fold(new Point(2 * W, 0), new Point(W + 60, 90), W);
        var p = new Point(417, 123);
        var back = PageCurlGeometry.Reflect(fold, PageCurlGeometry.Reflect(fold, p));
        back.X.ShouldBe(p.X, 1e-9);
        back.Y.ShouldBe(p.Y, 1e-9);
    }

    [Fact]
    public void A_corner_that_has_not_moved_makes_no_fold()
    {
        var corner = new Point(2 * W, H);
        PageCurlGeometry.Fold(corner, corner, W).IsValid.ShouldBeFalse();
        PageCurlGeometry.Fold(corner, new Point(corner.X - 0.1, corner.Y), W).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void The_corner_stays_within_a_page_width_of_the_spine()
    {
        var corner = new Point(2 * W, H);
        var far = PageCurlGeometry.ClampPointer(corner, new Point(-900, -500), W);
        var dx = far.X - W;
        var dy = far.Y - H;
        Math.Sqrt(dx * dx + dy * dy).ShouldBe(W, 1e-6);
        // A point already inside is left alone.
        PageCurlGeometry.ClampPointer(corner, new Point(W + 20, H - 30), W).ShouldBe(new Point(W + 20, H - 30));
    }

    [Theory]
    [InlineData(2 * W, H, 2 * W - 150, H - 60)]
    [InlineData(2 * W, 0, 2 * W - 60, 120)]
    [InlineData(2 * W, H, W + 40, H - 200)]
    [InlineData(0, H, 130, H - 50)]
    public void The_flap_and_the_rest_of_the_sheet_make_the_whole_sheet(double cx, double cy, double px, double py)
    {
        var dir = cx > W ? 1 : -1;
        var sheet = new Rect(dir > 0 ? W : 0, 0, W, H);
        var fold = PageCurlGeometry.Fold(new Point(cx, cy), new Point(px, py), W);
        Span<Point> flap = stackalloc Point[PageCurlGeometry.MaxVertices];
        Span<Point> keep = stackalloc Point[PageCurlGeometry.MaxVertices];
        var flapCount = PageCurlGeometry.ClipToSide(sheet, fold, true, flap);
        var keepCount = PageCurlGeometry.ClipToSide(sheet, fold, false, keep);
        flapCount.ShouldBeGreaterThanOrEqualTo(3);
        keepCount.ShouldBeGreaterThanOrEqualTo(3);
        (Area(flap, flapCount) + Area(keep, keepCount)).ShouldBe(W * H, 1e-6);
        // The grabbed corner is on the flap.
        var corner = new Point(cx, cy);
        flap[..flapCount].ToArray().ShouldContain(corner);
    }

    [Fact]
    public void A_fully_turned_page_is_all_flap_and_lies_where_it_rests()
    {
        var corner = new Point(2 * W, H);
        var fold = PageCurlGeometry.Fold(corner, PageCurlGeometry.FarCorner(corner, W), W);
        var sheet = new Rect(W, 0, W, H);
        Span<Point> buffer = stackalloc Point[PageCurlGeometry.MaxVertices];
        PageCurlGeometry.ClipToSide(sheet, fold, false, buffer).ShouldBe(0);
        PageCurlGeometry.ClipToSide(sheet, fold, true, buffer).ShouldBe(4);
        // The back page, resting on the left, maps onto itself.
        var matrix = PageCurlGeometry.FlapMatrix(fold, W, new Point(0, 0));
        var p = matrix.Transform(new Point(37, 211));
        p.X.ShouldBe(37, 1e-6);
        p.Y.ShouldBe(211, 1e-6);
    }

    [Fact]
    public void The_flap_matrix_agrees_with_the_point_map_and_keeps_the_page_unmirrored()
    {
        var fold = PageCurlGeometry.Fold(new Point(2 * W, H), new Point(2 * W - 170, H - 110), W);
        var matrix = PageCurlGeometry.FlapMatrix(fold, W, new Point(0, 0));
        var local = new Point(90, 140);
        var viaMatrix = matrix.Transform(local);
        var direct = PageCurlGeometry.Flap(fold, W, new Point(0, 0), local);
        viaMatrix.X.ShouldBe(direct.X, 1e-6);
        viaMatrix.Y.ShouldBe(direct.Y, 1e-6);
        // Two reflections are a rotation: the determinant is +1.
        (matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21).ShouldBe(1, 1e-9);
        // The inverse map from the sheet gives the page point back.
        var sheetPoint = PageCurlGeometry.Reflect(fold, direct);
        var back = PageCurlGeometry.BackLocal(sheetPoint, W, new Point(0, 0));
        back.X.ShouldBe(local.X, 1e-6);
        back.Y.ShouldBe(local.Y, 1e-6);
    }

    [Fact]
    public void Progress_runs_from_flat_to_across_the_spine_in_either_direction()
    {
        PageCurlGeometry.Progress(1, new Point(2 * W, H), new Point(2 * W, H), W).ShouldBe(0);
        PageCurlGeometry.Progress(1, new Point(2 * W, H), new Point(W, H), W).ShouldBe(0.5, 1e-9);
        PageCurlGeometry.Progress(1, new Point(2 * W, H), new Point(0, H), W).ShouldBe(1, 1e-9);
        PageCurlGeometry.Progress(-1, new Point(0, H), new Point(W, H), W).ShouldBe(0.5, 1e-9);
        // Pulling the wrong way is no progress.
        PageCurlGeometry.Progress(1, new Point(2 * W, H), new Point(2 * W + 30, H), W).ShouldBe(0);
    }

    [Fact]
    public void The_tap_arc_rises_in_the_middle_and_starts_and_ends_on_the_corners()
    {
        var corner = new Point(2 * W, H);
        PageCurlGeometry.ArcPoint(corner, true, 0, W, H).ShouldBe(corner);
        var end = PageCurlGeometry.ArcPoint(corner, true, 1, W, H);
        end.X.ShouldBe(0, 1e-9);
        end.Y.ShouldBe(H, 1e-9);
        PageCurlGeometry.ArcPoint(corner, true, 0.5, W, H).Y.ShouldBeLessThan(H - 50);
        PageCurlGeometry.ArcPoint(new Point(2 * W, 0), false, 0.5, W, H).Y.ShouldBeGreaterThan(50);
    }

    // ---- The control ---------------------------------------------------------------------------------------------

    private static Border Page(string name) => new() { Name = name, Width = 100, Height = 100 };

    private static PageCurl Book(int pages = 8, double width = 560)
    {
        var book = new PageCurl { Width = width, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        for (var i = 1; i <= pages; i++)
        {
            book.Pages.Add(Page("p" + i));
        }
        return book;
    }

    private static (Window Window, PageCurl Book, PageCurlSurface Surface) Show(ArcMotionLevel level = ArcMotionLevel.Off, int pages = 8)
    {
        var book = Book(pages);
        var window = TestHelpers.Show(book, level);
        window.Width = 700;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return (window, book, TestHelpers.Descendant<PageCurlSurface>(book));
    }

    private static Point InWindow(PageCurlSurface surface, Window window, double x, double y) =>
        surface.TranslatePoint(new Point(x, y), window)!.Value;

    private static int[] ShownPages(PageCurlSurface surface) =>
        surface.Sheets.Where(sheet => sheet.IsVisible).Select(sheet => sheet.PageNumber).OrderBy(n => n).ToArray();

    [AvaloniaFact]
    public void Starts_on_the_cover_alone_and_derives_page_size_from_the_width()
    {
        var (window, book, surface) = Show();
        book.Spread.ShouldBe(0);
        book.SpreadCount.ShouldBe(5);
        ShownPages(surface).ShouldBe([1]);
        surface.PageSize.Width.ShouldBe(280, 0.01);
        surface.PageSize.Height.ShouldBe(280 / 0.72, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void The_page_aspect_sets_the_page_height()
    {
        var book = Book();
        book.PageAspect = 0.9;
        var window = TestHelpers.Show(book);
        var surface = TestHelpers.Descendant<PageCurlSurface>(book);
        surface.PageSize.Height.ShouldBe(280 / 0.9, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Next_and_previous_move_one_spread_and_show_the_right_pages()
    {
        var (window, book, surface) = Show();
        book.NextSpread().ShouldBeTrue();
        book.Spread.ShouldBe(1);
        ShownPages(surface).ShouldBe([2, 3]);
        book.NextSpread();
        ShownPages(surface).ShouldBe([4, 5]);
        book.PreviousSpread().ShouldBeTrue();
        ShownPages(surface).ShouldBe([2, 3]);
        book.PreviousSpread();
        book.PreviousSpread().ShouldBeFalse();
        book.Spread.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void An_even_book_ends_on_a_lone_back_cover_and_an_odd_one_on_two_pages()
    {
        var (window, book, surface) = Show(pages: 8);
        book.Spread = 4;
        ShownPages(surface).ShouldBe([8]);
        book.NextSpread().ShouldBeFalse();
        window.Close();

        var (window2, odd, surface2) = Show(pages: 9);
        odd.Spread = 4;
        ShownPages(surface2).ShouldBe([8, 9]);
        window2.Close();
    }

    [AvaloniaFact]
    public void The_spread_is_clamped_to_the_book()
    {
        var (window, book, _) = Show();
        book.Spread = 99;
        book.Spread.ShouldBe(4);
        book.Spread = -3;
        book.Spread.ShouldBe(0);
        book.Pages.Clear();
        book.Spread = 2;
        book.Spread.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_and_page_keys_turn_and_the_event_reports_the_spread()
    {
        var (window, book, _) = Show();
        var seen = new List<int>();
        book.SpreadChanged += (_, e) => seen.Add(e.Spread);
        book.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
        book.Spread.ShouldBe(2);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        book.Spread.ShouldBe(0);
        seen.ShouldBe([1, 2, 1, 0]);
        window.Close();
    }

    [AvaloniaFact]
    public void Setting_the_property_does_not_raise_the_turn_event()
    {
        var (window, book, surface) = Show();
        var count = 0;
        book.SpreadChanged += (_, _) => count++;
        book.Spread = 2;
        count.ShouldBe(0);
        surface.ShownSpread.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void The_buttons_turn_and_disable_at_the_covers()
    {
        var (window, book, _) = Show();
        var previous = TestHelpers.Descendant<Button>(book, PageCurl.PreviousButtonPart);
        var next = TestHelpers.Descendant<Button>(book, PageCurl.NextButtonPart);
        previous.IsEnabled.ShouldBeFalse();
        next.IsEnabled.ShouldBeTrue();
        next.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        book.Spread.ShouldBe(1);
        previous.IsEnabled.ShouldBeTrue();
        book.Spread = 4;
        next.IsEnabled.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void The_status_names_the_visible_pages_and_the_book_is_a_group_named_by_its_label()
    {
        var (window, book, _) = Show();
        var status = TestHelpers.Descendant<TextBlock>(book, PageCurl.StatusPart);
        status.Text.ShouldBe("Page 1 of 8");
        book.Spread = 1;
        status.Text.ShouldBe("Page 2 and 3 of 8");
        book.Spread = 4;
        status.Text.ShouldBe("Page 8 of 8");
        book.Label = "Autumn lookbook";
        var peer = ControlAutomationPeer.CreatePeerForElement(book);
        peer.GetName().ShouldBe("Autumn lookbook");
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetLocalizedControlType().ShouldBe("book");
        book.Focusable.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Only_the_resting_pages_are_in_the_automation_tree()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Full);
        book.Spread = 1;
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        foreach (var sheet in surface.Sheets)
        {
            Avalonia.Automation.AutomationProperties.GetAccessibilityView(sheet).ShouldBe(Avalonia.Automation.AccessibilityView.Content);
        }
        book.NextSpread();
        surface.IsTurning.ShouldBeTrue();
        foreach (var sheet in surface.Sheets)
        {
            Avalonia.Automation.AutomationProperties.GetAccessibilityView(sheet).ShouldBe(Avalonia.Automation.AccessibilityView.Raw);
        }
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Pages_that_are_not_controls_are_built_with_the_template()
    {
        var book = new PageCurl
        {
            Width = 560,
            PageTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) => new TextBlock { Text = "page " + text })
        };
        book.Pages.Add("a");
        book.Pages.Add("b");
        book.Pages.Add("c");
        var window = TestHelpers.Show(book);
        var surface = TestHelpers.Descendant<PageCurlSurface>(book);
        book.Spread = 1;
        var items = surface.Sheets.Select(sheet => (string)((Avalonia.Controls.Presenters.ContentPresenter)sheet.Content!).Content!).OrderBy(x => x).ToArray();
        items.ShouldBe(["b", "c"]);
        window.Close();
    }

    [AvaloniaFact]
    public void Changing_the_pages_rebuilds_the_spread()
    {
        var (window, book, surface) = Show();
        book.Spread = 4;
        book.Pages.RemoveAt(book.Pages.Count - 1);
        book.Pages.RemoveAt(book.Pages.Count - 1);
        book.SpreadCount.ShouldBe(4);
        book.Spread.ShouldBe(3);
        ShownPages(surface).ShouldBe([6]);
        window.Close();
    }

    [AvaloniaFact]
    public void Tapping_the_right_page_goes_forward_and_the_left_page_goes_back()
    {
        var (window, book, surface) = Show();
        var right = InWindow(surface, window, 300, 150);
        window.MouseMove(right, RawInputModifiers.None);
        window.MouseDown(right, MouseButton.Left);
        window.MouseUp(right, MouseButton.Left);
        book.Spread.ShouldBe(1);
        var left = InWindow(surface, window, 100, 150);
        window.MouseDown(left, MouseButton.Left);
        window.MouseUp(left, MouseButton.Left);
        book.Spread.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_swaps_with_no_curl()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Reduced);
        book.NextSpread();
        surface.IsTurning.ShouldBeFalse();
        surface.TurnProgress.ShouldBe(0);
        book.Spread.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Dismissing_the_hint_is_for_good()
    {
        var (window, book, surface) = Show();
        surface.IsHintDismissed.ShouldBeFalse();
        book.NextSpread();
        surface.IsHintDismissed.ShouldBeTrue();
        book.PreviousSpread();
        surface.IsHintDismissed.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_null_hint_takes_no_room_under_the_book()
    {
        var withHint = Book();
        var without = Book();
        without.Hint = null;
        var a = TestHelpers.Show(withHint);
        var b = TestHelpers.Show(without);
        var sa = TestHelpers.Descendant<PageCurlSurface>(withHint);
        var sb = TestHelpers.Descendant<PageCurlSurface>(without);
        (sa.Bounds.Height - sb.Bounds.Height).ShouldBeGreaterThan(20);
        a.Close();
        b.Close();
    }

    // ---- Motion --------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void A_tap_turns_along_an_arc_and_commits_when_it_lands()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Full);
        book.NextSpread().ShouldBeTrue();
        surface.IsTurning.ShouldBeTrue();
        book.Spread.ShouldBe(0);
        // The sheet, its back and the page it uncovers are all in use while it turns.
        ShownPages(surface).ShouldBe([1, 2, 3]);
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        book.Spread.ShouldBe(1);
        ShownPages(surface).ShouldBe([2, 3]);
        window.Close();
    }

    [AvaloniaFact]
    public void Dragging_the_corner_folds_the_page_and_a_far_release_turns_it()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Full);
        var w = surface.PageSize.Width;
        var h = surface.PageSize.Height;
        // The cover sits on the right, centred: its right half of the book starts one page width in, shifted half a page left.
        var grab = InWindow(surface, window, 2 * w - w / 2 - 20, h - 20);
        window.MouseMove(grab, RawInputModifiers.None);
        window.MouseDown(grab, MouseButton.Left);
        var mid = new Point(grab.X - 120, grab.Y - 40);
        window.MouseMove(mid, RawInputModifiers.LeftMouseButton);
        window.MouseMove(new Point(mid.X - 10, mid.Y), RawInputModifiers.LeftMouseButton);
        surface.IsDragging.ShouldBeTrue();
        surface.TurnProgress.ShouldBeGreaterThan(0);
        ShownPages(surface).ShouldBe([1, 2, 3]);
        var far = new Point(grab.X - 2 * w + 30, grab.Y - 20);
        window.MouseMove(far, RawInputModifiers.LeftMouseButton);
        surface.TurnProgress.ShouldBeGreaterThan(0.5);
        window.MouseUp(far, MouseButton.Left);
        surface.IsDragging.ShouldBeFalse();
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        book.Spread.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void A_short_slow_drag_lays_the_page_back_down()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Full);
        var w = surface.PageSize.Width;
        var h = surface.PageSize.Height;
        var grab = InWindow(surface, window, 2 * w - w / 2 - 20, h - 20);
        window.MouseMove(grab, RawInputModifiers.None);
        window.MouseDown(grab, MouseButton.Left);
        var to = new Point(grab.X - 90, grab.Y - 30);
        window.MouseMove(new Point(grab.X - 20, grab.Y - 5), RawInputModifiers.LeftMouseButton);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        surface.IsDragging.ShouldBeTrue();
        surface.TurnProgress.ShouldBeLessThan(0.5);
        Thread.Sleep(120);
        window.MouseUp(to, MouseButton.Left);
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        book.Spread.ShouldBe(0);
        ShownPages(surface).ShouldBe([1]);
        window.Close();
    }

    [AvaloniaFact]
    public void The_book_slides_to_the_centre_while_the_cover_turns()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Full);
        var w = surface.PageSize.Width;
        // Closed: the cover is centred, half a page left of the open book's right page.
        var cover = surface.Sheets.Single();
        cover.Placement.OffsetX().ShouldBe(-w / 2 + w, 0.5);
        book.NextSpread();
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        // Open: the right page sits at one page width, no shift.
        surface.Sheets.Select(s => s.Placement.OffsetX()).OrderBy(x => x).ToArray().ShouldBe([0, w], 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void The_tease_lifts_the_cover_corner_and_lays_it_back()
    {
        var (window, _, surface) = Show(ArcMotionLevel.Full);
        // Coming into view already teased once.
        var before = surface.TeaseCount;
        surface.PlayTease(0.25);
        surface.TeaseCount.ShouldBe(before + 1);
        TestHelpers.PumpUntil(() => surface.PeekAmount > 0.2).ShouldBeTrue();
        surface.PeekDirection.ShouldBe(1);
        ShownPages(surface).ShouldBe([1, 2, 3]);
        TestHelpers.PumpUntil(() => surface.PeekAmount < 0.01 && ShownPages(surface).Length == 1, 5000).ShouldBeTrue();
        ShownPages(surface).ShouldBe([1]);
        window.Close();
    }

    [AvaloniaFact]
    public void The_tease_is_skipped_under_reduced_motion_and_when_off()
    {
        var (window, _, surface) = Show(ArcMotionLevel.Reduced);
        surface.PlayTease(0.25);
        surface.TeaseCount.ShouldBe(0);
        surface.PeekAmount.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Hovering_a_page_shows_a_chevron_for_the_direction_a_tap_turns()
    {
        var (window, book, surface) = Show(ArcMotionLevel.Full);
        book.Spread = 2;
        TestHelpers.PumpUntil(() => !surface.IsTurning).ShouldBeTrue();
        window.MouseMove(InWindow(surface, window, 420, 150), RawInputModifiers.None);
        TestHelpers.PumpUntil(() => surface.Chevron.Opacity > 0.9).ShouldBeTrue();
        surface.Chevron.Direction.ShouldBe(1);
        window.MouseMove(InWindow(surface, window, 120, 150), RawInputModifiers.None);
        surface.Chevron.Direction.ShouldBe(-1);
        window.Close();
    }

    [AvaloniaFact]
    public void Hovering_a_corner_lifts_it_a_little()
    {
        var (window, _, surface) = Show(ArcMotionLevel.Full);
        var w = surface.PageSize.Width;
        var h = surface.PageSize.Height;
        window.MouseMove(InWindow(surface, window, 2 * w - w / 2 - 15, h - 15), RawInputModifiers.None);
        TestHelpers.PumpUntil(() => surface.PeekAmount > 0.05).ShouldBeTrue();
        surface.PeekDirection.ShouldBe(1);
        window.MouseMove(InWindow(surface, window, 2 * w - w / 2 - 15, h / 2), RawInputModifiers.None);
        TestHelpers.PumpUntil(() => surface.PeekAmount < 0.01).ShouldBeTrue();
        window.Close();
    }
}

internal static class PageCurlMatrixExtensions
{
    public static double OffsetX(this Matrix matrix) => matrix.M31;
}
