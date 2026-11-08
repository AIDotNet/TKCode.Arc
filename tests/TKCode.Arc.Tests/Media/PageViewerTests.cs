using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Media;

/// <summary>The control: source, navigation, fit, zoom, keyboard, pointer, layers, toolbar and accessibility.</summary>
public class PageViewerTests
{
    /// <summary>A 400 × 300 window: the viewport is 400 × 252, the narrow canvas padding is 8, the page gap 12.</summary>
    private static (PageViewer Viewer, PageViewerPresenter Presenter, Window Window) Show(PageViewer viewer)
    {
        var window = TestHelpers.Show(viewer);
        Dispatcher.UIThread.RunJobs();
        return (viewer, TestHelpers.Descendant<PageViewerPresenter>(viewer), window);
    }

    private static PageViewer Viewer(int pages = 10, double width = 200, double height = 300) => new()
    {
        PageSource = new FakePageSource(pages, new Size(width, height)),
        FitMode = PageFitMode.None
    };

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void FocusViewport(PageViewerPresenter presenter)
    {
        presenter.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
    }

    private static Bitmap Picture(int width, int height) =>
        new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

    private static Point Over(PageViewerPresenter presenter, Window window, double x, double y) =>
        presenter.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Tick()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    #region Source

    [AvaloniaFact]
    public void Without_Pages_The_Toolbar_Hides_The_Page_And_Zoom_Controls()
    {
        var pages = new ObservableCollection<object>();
        var viewer = new PageViewer { Width = 800, Height = 500, ItemsSource = pages };
        var window = TestHelpers.Show(viewer);
        try
        {
            Control Tools() => viewer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_Tools");
            Control Toolbar() => viewer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == PageViewer.ToolbarPart);
            Tools().IsEffectivelyVisible.ShouldBeFalse();
            Toolbar().IsEffectivelyVisible.ShouldBeTrue();

            pages.Add(new WriteableBitmap(new PixelSize(10, 10), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul));
            Dispatcher.UIThread.RunJobs();
            Tools().IsEffectivelyVisible.ShouldBeTrue();

            pages.Clear();
            Dispatcher.UIThread.RunJobs();
            Tools().IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Defaults_Follow_The_Spec()
    {
        var viewer = new PageViewer();
        viewer.PageCount.ShouldBe(0);
        viewer.CurrentPageIndex.ShouldBe(0);
        viewer.Zoom.ShouldBe(1);
        viewer.MinZoom.ShouldBe(0.1);
        viewer.MaxZoom.ShouldBe(8);
        viewer.FitMode.ShouldBe(PageFitMode.Width);
        viewer.LayoutMode.ShouldBe(PageLayoutMode.Continuous);
        viewer.InteractionMode.ShouldBe(PageInteractionMode.Select);
        viewer.IsToolbarVisible.ShouldBeTrue();
        viewer.RenderCacheBytes.ShouldBe(192L * 1024 * 1024);
        viewer.IsLoading.ShouldBeFalse();
        viewer.Layers.ShouldBeEmpty();
        viewer.Classes.Contains(":empty").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Tokens_Reach_The_Template()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            var toolbar = TestHelpers.Descendant<PageViewerToolbar>(viewer);
            toolbar.Bounds.Height.ShouldBe(48);
            presenter.PageSpacing.ShouldBe(12);
            presenter.CanvasPadding.ShouldBe(24);
            presenter.CanvasPaddingNarrow.ShouldBe(8);
            presenter.Bounds.Size.ShouldBe(new Size(400, 252));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemsSource_Follows_Collection_Changes()
    {
        var pictures = new ObservableCollection<Bitmap> { Picture(100, 200), Picture(100, 200) };
        var (viewer, _, window) = Show(new PageViewer { ItemsSource = pictures });
        try
        {
            viewer.PageCount.ShouldBe(2);
            viewer.Classes.Contains(":empty").ShouldBeFalse();
            pictures.Add(Picture(100, 200));
            Dispatcher.UIThread.RunJobs();
            viewer.PageCount.ShouldBe(3);
            pictures.Clear();
            Dispatcher.UIThread.RunJobs();
            viewer.PageCount.ShouldBe(0);
            viewer.Classes.Contains(":empty").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_Pages_Clamps_The_Current_Page()
    {
        var pictures = new ObservableCollection<Bitmap>(Enumerable.Range(0, 5).Select(_ => Picture(100, 400)));
        var (viewer, _, window) = Show(new PageViewer { ItemsSource = pictures, FitMode = PageFitMode.None });
        try
        {
            viewer.GoToPage(4, animate: false);
            viewer.CurrentPageIndex.ShouldBe(4);
            pictures.RemoveAt(4);
            pictures.RemoveAt(3);
            Dispatcher.UIThread.RunJobs();
            viewer.PageCount.ShouldBe(3);
            viewer.CurrentPageIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PageSource_Wins_Over_ItemsSource()
    {
        var source = new FakePageSource(7, new Size(100, 100));
        var (viewer, _, window) = Show(new PageViewer { ItemsSource = new[] { Picture(10, 10) }, PageSource = source });
        try
        {
            viewer.PageCount.ShouldBe(7);
            viewer.PageSource = null;
            viewer.PageCount.ShouldBe(1);
            source.SubscriberCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Image_Source_Gives_The_Bitmap_Size_And_Does_Not_Hand_Over_The_Bitmap()
    {
        var picture = Picture(120, 80);
        var source = new ImagePageSource(new object[] { picture });
        source.PageCount.ShouldBe(1);
        source.GetPageSize(0).ShouldBe(picture.Size);
        var image = source.RenderAsync(new PageRenderRequest(0, new PixelSize(120, 80), null), default).AsTask().Result!;
        image.Bitmap.ShouldBeSameAs(picture);
        image.OwnsBitmap.ShouldBeFalse();
        source.RenderAsync(new PageRenderRequest(0, new PixelSize(120, 80), new PixelRect(0, 0, 4, 4)), default).AsTask().Result.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Image_Source_Treats_Other_Elements_As_Empty_Pages_And_Reads_An_Address_Later()
    {
        var picture = Picture(120, 80);
        var source = new ImagePageSource(new object[] { 42, picture, "https://example.com/a.png" });
        source.PageCount.ShouldBe(3);
        // Pages of unknown size take the first known size.
        source.GetPageSize(0).ShouldBe(picture.Size);
        source.GetPageSize(2).ShouldBe(picture.Size);
        source.RenderAsync(new PageRenderRequest(0, new PixelSize(120, 80), null), default).AsTask().Result.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Image_Source_Listens_To_Its_Collection_Only_While_It_Has_A_Subscriber()
    {
        var pictures = new ObservableCollection<Bitmap> { Picture(10, 10) };
        var source = new ImagePageSource(pictures);
        var raised = 0;
        EventHandler handler = (_, _) => raised++;
        source.PagesChanged += handler;
        pictures.Add(Picture(10, 10));
        raised.ShouldBe(1);
        source.PageCount.ShouldBe(2);
        source.PagesChanged -= handler;
        pictures.Add(Picture(10, 10));
        raised.ShouldBe(1);
        source.PagesChanged += handler;
        source.PageCount.ShouldBe(3);
    }

    [AvaloniaFact]
    public void The_Viewer_Lets_Go_Of_Its_Source_When_It_Leaves()
    {
        var source = new FakePageSource(5, new Size(100, 100));
        var weak = TestHelpers.ShowAndClose(() => new PageViewer { PageSource = source }, viewer =>
        {
            source.SubscriberCount.ShouldBe(1);
            viewer.ZoomIn();
        });
        source.SubscriberCount.ShouldBe(0);
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
    }

    #endregion

    #region Current page

    [AvaloniaFact]
    public void Setting_The_Current_Page_Scrolls_There_And_Scrolling_Updates_It()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            viewer.CurrentPageIndex = 3;
            // Pages are 300 tall with a 12 gap; the view stops the canvas padding above the page.
            presenter.Offset.Y.ShouldBe(3 * 312, 0.01);
            ((IScrollable)presenter).Offset = new Vector(0, 6 * 312 + 8);
            viewer.CurrentPageIndex.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Page_Index_Set_Before_The_First_Layout_Applies_Once_There_Is_One()
    {
        var viewer = Viewer();
        viewer.CurrentPageIndex = 4;
        var (_, presenter, window) = Show(viewer);
        try
        {
            viewer.CurrentPageIndex.ShouldBe(4);
            presenter.Offset.Y.ShouldBe(4 * 312, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Current_Page_Is_Clamped()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            viewer.GoToPage(99, animate: false);
            viewer.CurrentPageIndex.ShouldBe(9);
            viewer.CurrentPageIndex = -3;
            viewer.CurrentPageIndex.ShouldBe(0);
            viewer.NextPage();
            viewer.CurrentPageIndex.ShouldBe(1);
            viewer.PreviousPage();
            viewer.PreviousPage();
            viewer.CurrentPageIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Jumping_To_A_Small_Last_Page_Stays_On_It_Until_The_View_Moves()
    {
        // The last pages cannot scroll to the top, so the page at the middle is another one; the jump still counts.
        var (viewer, presenter, window) = Show(Viewer(pages: 20, width: 50, height: 50));
        try
        {
            viewer.GoToPage(19, animate: false);
            viewer.CurrentPageIndex.ShouldBe(19);
            ((IScrollable)presenter).Offset = presenter.Offset - new Vector(0, 200);
            viewer.CurrentPageIndex.ShouldBeLessThan(19);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Current_Page_Changed_Reports_Old_And_New()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            var seen = new List<(int Old, int New)>();
            viewer.CurrentPageChanged += (_, e) => seen.Add((e.OldIndex, e.NewIndex));
            viewer.GoToPage(2, animate: false);
            viewer.GoToPage(5, animate: false);
            seen.ShouldBe([(0, 2), (2, 5)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Animated_Jump_Keeps_The_Target_As_Current_While_It_Moves()
    {
        var viewer = Viewer();
        var window = TestHelpers.Show(viewer, ArcMotionLevel.Full);
        try
        {
            var presenter = TestHelpers.Descendant<PageViewerPresenter>(viewer);
            Dispatcher.UIThread.RunJobs();
            viewer.GoToPage(6);
            viewer.CurrentPageIndex.ShouldBe(6);
            presenter.IsScrollAnimating.ShouldBeTrue();
            presenter.Offset.Y.ShouldBeLessThan(6 * 312);
            ((IScrollable)presenter).Offset = new Vector(0, 2 * 312);
            presenter.IsScrollAnimating.ShouldBeFalse();
            viewer.CurrentPageIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Mode_Shows_One_Page_And_Pages_Swap()
    {
        var viewer = Viewer();
        viewer.LayoutMode = PageLayoutMode.Single;
        var (_, presenter, window) = Show(viewer);
        try
        {
            ((IScrollable)presenter).Extent.Height.ShouldBe(316);
            viewer.GoToPage(4);
            viewer.CurrentPageIndex.ShouldBe(4);
            presenter.Offset.Y.ShouldBe(0);
            ((IScrollable)presenter).Offset = new Vector(0, 20);
            viewer.CurrentPageIndex.ShouldBe(4);
            viewer.NextPage();
            viewer.CurrentPageIndex.ShouldBe(5);
            presenter.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scroll_Into_View_Moves_Only_When_Needed()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            viewer.ScrollIntoView(0, new Rect(10, 10, 20, 20), animate: false);
            presenter.Offset.Y.ShouldBe(0);
            viewer.ScrollIntoView(1, new Rect(10, 280, 20, 10), animate: false);
            // Page 1 starts at 320; the region ends at 610, the shortest move puts that at the viewport bottom.
            presenter.Offset.Y.ShouldBe(610 - 252, 0.01);
            viewer.ScrollIntoView(1, new Rect(10, 280, 20, 10), animate: false);
            presenter.Offset.Y.ShouldBe(610 - 252, 0.01);
            viewer.ScrollIntoView(0, new Rect(10, 10, 20, 20), animate: false);
            presenter.Offset.Y.ShouldBe(18, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Hit_Test_Works_In_Viewer_Coordinates()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            // The page starts 100 from the left and 8 under the 48 tall toolbar.
            viewer.PageHitTest(new Point(150, 48 + 8 + 20), out var page, out var point).ShouldBeTrue();
            page.ShouldBe(0);
            point.ShouldBe(new Point(50, 20));
            viewer.PageHitTest(new Point(50, 100), out _, out _).ShouldBeFalse();
            viewer.PageHitTest(new Point(150, 10), out _, out _).ShouldBeFalse();
            ((IScrollable)presenter).Offset = new Vector(0, 312);
            viewer.PageHitTest(new Point(150, 48 + 8 + 20), out page, out point).ShouldBeTrue();
            page.ShouldBe(1);
            point.ShouldBe(new Point(50, 20));
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Zoom and fit

    [AvaloniaFact]
    public void Fit_Modes_Follow_The_Viewport()
    {
        var viewer = new PageViewer { PageSource = new FakePageSource(3, new Size(200, 400)) };
        var (_, _, window) = Show(viewer);
        try
        {
            // Width: (400 - 2·8) / 200.
            viewer.Zoom.ShouldBe(384.0 / 200, 0.001);
            viewer.FitMode = PageFitMode.Height;
            viewer.Zoom.ShouldBe((252.0 - 16) / 400, 0.001);
            viewer.FitMode = PageFitMode.Page;
            viewer.Zoom.ShouldBe((252.0 - 16) / 400, 0.001);
            window.Width = 300;
            window.Height = 600;
            Dispatcher.UIThread.RunJobs();
            // 300 wide and 552 tall: 1.42 by width, 1.34 by height; the height limits.
            viewer.Zoom.ShouldBe((552.0 - 16) / 400, 0.001);
            viewer.FitMode = PageFitMode.Width;
            window.Width = 800;
            Dispatcher.UIThread.RunJobs();
            viewer.Zoom.ShouldBe((800.0 - 48) / 200, 0.001);
            viewer.FitMode.ShouldBe(PageFitMode.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_The_Zoom_By_Hand_Turns_Fit_Off()
    {
        var viewer = new PageViewer { PageSource = new FakePageSource(3, new Size(200, 400)) };
        var (_, _, window) = Show(viewer);
        try
        {
            viewer.FitMode.ShouldBe(PageFitMode.Width);
            viewer.Zoom = 1.5;
            viewer.FitMode.ShouldBe(PageFitMode.None);
            window.Width = 500;
            Dispatcher.UIThread.RunJobs();
            viewer.Zoom.ShouldBe(1.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Zoom_Is_Clamped_And_Follows_The_Limits()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            viewer.Zoom = 100;
            viewer.Zoom.ShouldBe(8);
            viewer.Zoom = 0.001;
            viewer.Zoom.ShouldBe(0.1);
            viewer.MinZoom = 0.5;
            viewer.Zoom.ShouldBe(0.5);
            viewer.Zoom = double.NaN;
            viewer.Zoom.ShouldBe(0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Zoom_Steps_Go_Through_The_List_And_Reset_Goes_To_One()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            viewer.ZoomIn();
            viewer.Zoom.ShouldBe(1.25);
            viewer.ZoomIn();
            viewer.Zoom.ShouldBe(1.5);
            viewer.ZoomOut();
            viewer.ZoomOut();
            viewer.ZoomOut();
            viewer.Zoom.ShouldBe(0.75);
            viewer.ResetZoom();
            viewer.Zoom.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reset_Zoom_At_One_Still_Turns_Fit_Off()
    {
        var viewer = new PageViewer { PageSource = new FakePageSource(3, new Size(384, 400)) };
        var (_, _, window) = Show(viewer);
        try
        {
            viewer.Zoom.ShouldBe(1);
            viewer.FitMode.ShouldBe(PageFitMode.Width);
            viewer.ResetZoom();
            viewer.FitMode.ShouldBe(PageFitMode.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Control_Wheel_Zooms_About_The_Pointer()
    {
        var (viewer, presenter, window) = Show(Viewer(height: 1000));
        try
        {
            viewer.Zoom = 2;
            ((IScrollable)presenter).Offset = new Vector(50, 600);
            Tick();
            var at = Over(presenter, window, 150, 120);
            viewer.PageHitTest(viewer.TranslatePoint(new Point(150, 120 + 48), viewer)!.Value, out var pageBefore, out var pointBefore).ShouldBeTrue();
            window.MouseMove(at);
            window.MouseWheel(at, new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            viewer.Zoom.ShouldBeGreaterThan(2);
            viewer.FitMode.ShouldBe(PageFitMode.None);
            viewer.PageHitTest(new Point(150, 120 + 48), out var pageAfter, out var pointAfter).ShouldBeTrue();
            pageAfter.ShouldBe(pageBefore);
            pointAfter.X.ShouldBe(pointBefore.X, 0.5);
            pointAfter.Y.ShouldBe(pointBefore.Y, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Plain_Wheel_Scrolls_And_Does_Not_Zoom()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            Tick();
            var at = Over(presenter, window, 150, 120);
            window.MouseMove(at);
            window.MouseWheel(at, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            viewer.Zoom.ShouldBe(1);
            presenter.Offset.Y.ShouldBe(50, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Zoom_Keeps_The_Viewport_Center_In_Place()
    {
        var (viewer, presenter, window) = Show(Viewer(height: 1000));
        try
        {
            ((IScrollable)presenter).Offset = new Vector(0, 300);
            var center = new Point(200, 48 + 126);
            viewer.PageHitTest(center, out var pageBefore, out var pointBefore).ShouldBeTrue();
            viewer.ZoomIn();
            viewer.PageHitTest(center, out var pageAfter, out var pointAfter).ShouldBeTrue();
            pageAfter.ShouldBe(pageBefore);
            pointAfter.Y.ShouldBe(pointBefore.Y, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Animated_Zoom_Holds_The_Target_In_The_Property_While_The_Screen_Follows()
    {
        var viewer = Viewer();
        var window = TestHelpers.Show(viewer, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            viewer.ZoomIn();
            viewer.Zoom.ShouldBe(1.25);
            viewer.Layout.Zoom.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Reduced_Motion_A_Zoom_Lands_At_Once()
    {
        var viewer = Viewer();
        var window = TestHelpers.Show(viewer, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            viewer.ZoomIn();
            viewer.Layout.Zoom.ShouldBe(1.25);
            viewer.GoToPage(3);
            TestHelpers.Descendant<PageViewerPresenter>(viewer).IsScrollAnimating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    [AvaloniaFact]
    public void In_A_Parent_Without_A_Height_Limit_The_Viewer_Follows_Its_Pages()
    {
        var viewer = Viewer(pages: 2, width: 100, height: 100);
        var (_, presenter, window) = Show(viewer);
        window.Content = null;
        window.Content = new ScrollViewer { Content = viewer };
        Dispatcher.UIThread.RunJobs();
        try
        {
            // Two pages, one gap, the narrow canvas on both ends. The outer ScrollViewer stretches a shorter child to its
            // viewport when arranging, so the presenter's own answer is its desired size.
            presenter.DesiredSize.Height.ShouldBe(228);
            viewer.Zoom = 2;
            Dispatcher.UIThread.RunJobs();
            presenter.Bounds.Height.ShouldBe(428);
        }
        finally
        {
            window.Close();
        }
    }

    #region Keyboard

    [AvaloniaFact]
    public void Arrows_Scroll_Forty_Units()
    {
        var (_, presenter, window) = Show(Viewer(width: 1000, height: 1000));
        try
        {
            FocusViewport(presenter);
            Press(window, PhysicalKey.ArrowDown);
            presenter.Offset.Y.ShouldBe(40);
            Press(window, PhysicalKey.ArrowRight);
            presenter.Offset.X.ShouldBe(40);
            Press(window, PhysicalKey.ArrowUp);
            presenter.Offset.Y.ShouldBe(0);
            Press(window, PhysicalKey.ArrowLeft);
            presenter.Offset.X.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Keys_Scroll_A_Screen_When_Continuous()
    {
        var (_, presenter, window) = Show(Viewer());
        try
        {
            FocusViewport(presenter);
            Press(window, PhysicalKey.PageDown);
            presenter.Offset.Y.ShouldBe(252);
            Press(window, PhysicalKey.PageUp);
            presenter.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Keys_Turn_Pages_In_Single()
    {
        var viewer = Viewer();
        viewer.LayoutMode = PageLayoutMode.Single;
        var (_, presenter, window) = Show(viewer);
        try
        {
            FocusViewport(presenter);
            Press(window, PhysicalKey.PageDown);
            viewer.CurrentPageIndex.ShouldBe(1);
            Press(window, PhysicalKey.PageUp);
            viewer.CurrentPageIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Home_And_End_Go_To_The_First_And_Last_Page()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            FocusViewport(presenter);
            Press(window, PhysicalKey.End);
            viewer.CurrentPageIndex.ShouldBe(9);
            Press(window, PhysicalKey.Home);
            viewer.CurrentPageIndex.ShouldBe(0);
            presenter.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(RawInputModifiers.Control)]
    [InlineData(RawInputModifiers.Meta)]
    public void Command_Keys_Zoom(RawInputModifiers command)
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            FocusViewport(presenter);
            Press(window, PhysicalKey.Equal, command);
            viewer.Zoom.ShouldBe(1.25);
            Press(window, PhysicalKey.Minus, command);
            Press(window, PhysicalKey.Minus, command);
            viewer.Zoom.ShouldBe(0.75);
            Press(window, PhysicalKey.Digit0, command);
            viewer.Zoom.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Zoom_Keys_Work_While_The_Toolbar_Has_Focus()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            TestHelpers.Descendant<AvaloniaButton>(viewer, PageViewerToolbar.ZoomInPart).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Equal, RawInputModifiers.Control);
            viewer.Zoom.ShouldBe(1.25);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class KeyTakingViewer : PageViewer
    {
        public List<Key> Keys { get; } = new();

        protected override Type StyleKeyOverride => typeof(PageViewer);

        protected override bool OnViewerKeyDown(KeyEventArgs e)
        {
            Keys.Add(e.Key);
            return e.Key == Key.Down;
        }
    }

    [AvaloniaFact]
    public void The_Key_Hook_Sees_Keys_First_And_Can_Take_Them()
    {
        var viewer = new KeyTakingViewer { PageSource = new FakePageSource(5, new Size(200, 300)), FitMode = PageFitMode.None };
        var (_, presenter, window) = Show(viewer);
        try
        {
            FocusViewport(presenter);
            Press(window, PhysicalKey.ArrowDown);
            viewer.Keys.ShouldContain(Key.Down);
            presenter.Offset.Y.ShouldBe(0);
            Press(window, PhysicalKey.PageDown);
            viewer.Keys.ShouldContain(Key.PageDown);
            presenter.Offset.Y.ShouldBe(252);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Pointer

    [AvaloniaFact]
    public void Pan_Mode_Drags_The_View()
    {
        var viewer = Viewer();
        viewer.InteractionMode = PageInteractionMode.Pan;
        var (_, presenter, window) = Show(viewer);
        try
        {
            viewer.Classes.Contains(":pan").ShouldBeTrue();
            Tick();
            var start = Over(presenter, window, 200, 200);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            viewer.Classes.Contains(":panning").ShouldBeTrue();
            window.MouseMove(Over(presenter, window, 200, 100), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            presenter.Offset.Y.ShouldBe(100);
            window.MouseUp(Over(presenter, window, 200, 100), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            viewer.Classes.Contains(":panning").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Select_Mode_Does_Not_Pan_With_The_Left_Button_But_Does_With_The_Middle()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            Tick();
            var start = Over(presenter, window, 200, 200);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(Over(presenter, window, 200, 100), RawInputModifiers.LeftMouseButton);
            window.MouseUp(Over(presenter, window, 200, 100), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            presenter.Offset.Y.ShouldBe(0);

            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Middle);
            window.MouseMove(Over(presenter, window, 200, 100), RawInputModifiers.MiddleMouseButton);
            Dispatcher.UIThread.RunJobs();
            presenter.Offset.Y.ShouldBe(100);
            window.MouseUp(Over(presenter, window, 200, 100), MouseButton.Middle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Holding_Space_Pans_With_The_Left_Button()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            Tick();
            FocusViewport(presenter);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            var start = Over(presenter, window, 200, 200);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(Over(presenter, window, 200, 150), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            presenter.Offset.Y.ShouldBe(50);
            window.MouseUp(Over(presenter, window, 200, 150), MouseButton.Left);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(Over(presenter, window, 200, 100), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            presenter.Offset.Y.ShouldBe(50);
            window.MouseUp(Over(presenter, window, 200, 100), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Layers

    private sealed class RecordingLayer : PageLayer
    {
        private readonly string _name;
        private readonly List<string> _log;

        public RecordingLayer(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        public bool HandlesPress { get; init; }

        public Cursor? CursorToShow { get; init; }

        public List<PageLayerContext> Rendered { get; } = new();

        public List<PageLayerPointerArgs> Moves { get; } = new();

        protected internal override void Render(DrawingContext context, in PageLayerContext page) => Rendered.Add(page);

        protected internal override void OnPointerPressed(PageLayerPointerArgs e)
        {
            _log.Add($"{_name} press {e.PageIndex} {e.ClickCount}");
            e.Handled = HandlesPress;
        }

        protected internal override void OnPointerMoved(PageLayerPointerArgs e)
        {
            _log.Add($"{_name} move");
            Moves.Add(e);
        }

        protected internal override void OnPointerReleased(PageLayerPointerArgs e) => _log.Add($"{_name} release");

        protected internal override Cursor? GetCursor(int pageIndex, Point pointInPage) => CursorToShow;
    }

    private static void RenderOnce(PageViewerPresenter presenter)
    {
        presenter.InvalidateVisual();
        Tick();
    }

    [AvaloniaFact]
    public void Layers_Render_Once_Per_Visible_Page_With_Their_Bounds_And_Scale()
    {
        var (viewer, presenter, window) = Show(Viewer(height: 150));
        try
        {
            var layer = new RecordingLayer("a", new List<string>());
            viewer.Layers.Add(layer);
            RenderOnce(presenter);
            layer.Rendered.Clear();
            RenderOnce(presenter);
            // 252 tall at the top: page 0 (8–158) and page 1 (170–320).
            layer.Rendered.Select(p => p.PageIndex).ShouldBe([0, 1]);
            layer.Rendered[1].PageBounds.ShouldBe(new Rect(100, 170, 200, 150));
            layer.Rendered[1].Scale.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layers_Are_Asked_Last_To_First_And_Handled_Stops_The_Press()
    {
        var log = new List<string>();
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            viewer.Layers.Add(new RecordingLayer("bottom", log));
            viewer.Layers.Add(new RecordingLayer("middle", log) { HandlesPress = true });
            viewer.Layers.Add(new RecordingLayer("top", log));
            Tick();
            var at = Over(presenter, window, 200, 100);
            window.MouseDown(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            log.ShouldBe(["top press 0 1", "middle press 0 1"]);
            window.MouseUp(at, MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_On_The_Canvas_Does_Not_Reach_The_Layers()
    {
        var log = new List<string>();
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            viewer.Layers.Add(new RecordingLayer("a", log));
            Tick();
            window.MouseDown(Over(presenter, window, 20, 100), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            log.ShouldBeEmpty();
            window.MouseUp(Over(presenter, window, 20, 100), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Layer_That_Took_The_Press_Keeps_The_Drag_Over_The_Canvas()
    {
        var log = new List<string>();
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            var layer = new RecordingLayer("a", log) { HandlesPress = true };
            viewer.Layers.Add(layer);
            Tick();
            window.MouseDown(Over(presenter, window, 200, 100), MouseButton.Left);
            window.MouseMove(Over(presenter, window, 20, 100), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            layer.Moves.Count.ShouldBe(1);
            layer.Moves[0].PointInPage.X.ShouldBe(-80);
            window.MouseUp(Over(presenter, window, 20, 100), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            log.ShouldContain("a release");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Top_Layer_With_A_Cursor_Sets_It()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            var cursor = new Cursor(StandardCursorType.Ibeam);
            viewer.Layers.Add(new RecordingLayer("bottom", new List<string>()) { CursorToShow = new Cursor(StandardCursorType.Hand) });
            viewer.Layers.Add(new RecordingLayer("top", new List<string>()) { CursorToShow = cursor });
            Tick();
            window.MouseMove(Over(presenter, window, 200, 100));
            Dispatcher.UIThread.RunJobs();
            presenter.Cursor.ShouldBeSameAs(cursor);
            window.MouseMove(Over(presenter, window, 20, 100));
            Dispatcher.UIThread.RunJobs();
            presenter.Cursor.ShouldNotBeSameAs(cursor);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Invalidating_A_Layer_Redraws_Until_It_Is_Removed()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            var layer = new RecordingLayer("a", new List<string>());
            viewer.Layers.Add(layer);
            Tick();
            layer.Invalidate();
            viewer.Layers.Remove(layer);
            layer.Invalidate();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Page_Without_A_Bitmap_Draws_Its_Placeholder_Without_Failing()
    {
        var source = new FakePageSource(3, new Size(200, 300)) { Hold = true };
        var (viewer, presenter, window) = Show(new PageViewer { PageSource = source, FitMode = PageFitMode.None });
        try
        {
            RenderOnce(presenter);
            viewer.Scheduler.GetBitmaps(0, out var baseEntry, out _);
            baseEntry.ShouldBeNull();
            source.Hold = false;
            source.Release();
            TestHelpers.PumpUntil(() => viewer.Scheduler.CachedCount > 0).ShouldBeTrue();
            RenderOnce(presenter);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Toolbar

    private static T Part<T>(PageViewer viewer, string name) where T : Control => TestHelpers.Descendant<T>(viewer, name);

    private static void Click(Window window, Control button)
    {
        Tick();
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Toolbar_Shows_And_Hides()
    {
        var (viewer, presenter, window) = Show(Viewer());
        try
        {
            viewer.IsToolbarVisible = false;
            Dispatcher.UIThread.RunJobs();
            presenter.Bounds.Height.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Slots_Show_Content()
    {
        var start = new TextBlock { Text = "S" };
        var end = new TextBlock { Text = "E" };
        var viewer = Viewer();
        viewer.ToolbarStart = start;
        viewer.ToolbarEnd = end;
        var (_, _, window) = Show(viewer);
        try
        {
            TopLevel.GetTopLevel(start).ShouldNotBeNull();
            TopLevel.GetTopLevel(end).ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Page_Field_Shows_The_Page_And_Enter_Jumps_And_Clamps()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            var box = Part<Input>(viewer, PageViewerToolbar.PageBoxPart);
            box.Text.ShouldBe("1");
            Part<TextBlock>(viewer, PageViewerToolbar.PageTotalPart).Text.ShouldBe("/ 10");
            viewer.GoToPage(2, animate: false);
            box.Text.ShouldBe("3");

            box.Focus();
            Dispatcher.UIThread.RunJobs();
            box.Text = "7";
            Press(window, PhysicalKey.Enter);
            viewer.CurrentPageIndex.ShouldBe(6);

            box.Text = "999";
            Press(window, PhysicalKey.Enter);
            viewer.CurrentPageIndex.ShouldBe(9);
            box.Text.ShouldBe("10");

            box.Text = "nope";
            Press(window, PhysicalKey.Enter);
            viewer.CurrentPageIndex.ShouldBe(9);
            box.Text.ShouldBe("10");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Buttons_Act_On_The_Viewer()
    {
        var viewer = Viewer();
        var window = new Window { Content = viewer, Width = 800, Height = 500 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.NextPart));
            viewer.CurrentPageIndex.ShouldBe(1);
            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.PreviousPart));
            viewer.CurrentPageIndex.ShouldBe(0);
            Part<AvaloniaButton>(viewer, PageViewerToolbar.PreviousPart).IsEnabled.ShouldBeFalse();

            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.ZoomInPart));
            viewer.Zoom.ShouldBe(1.25);
            Part<TextBlock>(viewer, PageViewerToolbar.ZoomLabelPart).Text.ShouldBe("125%");
            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.ZoomOutPart));
            viewer.Zoom.ShouldBe(1);

            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.LayoutPart));
            viewer.LayoutMode.ShouldBe(PageLayoutMode.Single);
            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.PanPart));
            viewer.InteractionMode.ShouldBe(PageInteractionMode.Pan);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Zoom_Menu_Offers_The_Steps_And_Fits()
    {
        var viewer = Viewer();
        var window = new Window { Content = viewer, Width = 800, Height = 500 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Click(window, Part<AvaloniaButton>(viewer, PageViewerToolbar.ZoomMenuPart));
            var toolbar = TestHelpers.Descendant<PageViewerToolbar>(viewer);
            var menu = toolbar.ZoomMenu.ShouldNotBeNull();
            menu.IsOpen.ShouldBeTrue();
            var headers = menu.Items.OfType<MenuEntry>().Select(e => e.Header as string).ToList();
            headers.ShouldBe(["10%", "25%", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "600%", "800%",
                "Actual size", "Fit width", "Fit height", "Fit page"]);
            menu.Items.OfType<MenuEntry>().Where(e => e.IsChecked).Select(e => e.Header as string).ShouldBe(["100%"]);
            menu.Close();

            menu.Items.OfType<MenuEntry>().First(e => e.Header as string == "Fit page").Activate(NavigationMethod.Unspecified);
            Dispatcher.UIThread.RunJobs();
            viewer.FitMode.ShouldBe(PageFitMode.Page);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Toolbar_Keeps_The_Page_Field_And_The_Zoom()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            Part<AvaloniaButton>(viewer, PageViewerToolbar.NextPart).IsVisible.ShouldBeFalse();
            Part<AvaloniaButton>(viewer, PageViewerToolbar.PanPart).IsVisible.ShouldBeFalse();
            Part<AvaloniaButton>(viewer, PageViewerToolbar.ZoomInPart).IsVisible.ShouldBeTrue();
            Part<Input>(viewer, PageViewerToolbar.PageBoxPart).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Toolbar_Buttons_Have_Names()
    {
        var viewer = Viewer();
        var window = new Window { Content = viewer, Width = 800, Height = 500 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            foreach (var part in new[] { PageViewerToolbar.PreviousPart, PageViewerToolbar.NextPart, PageViewerToolbar.ZoomOutPart,
                         PageViewerToolbar.ZoomMenuPart, PageViewerToolbar.ZoomInPart, PageViewerToolbar.LayoutPart, PageViewerToolbar.PanPart })
            {
                AutomationProperties.GetName(Part<AvaloniaButton>(viewer, part)).ShouldNotBeNullOrEmpty(part);
            }
            AutomationProperties.GetName(Part<AvaloniaButton>(viewer, PageViewerToolbar.LayoutPart)).ShouldBe("Single page");
            viewer.LayoutMode = PageLayoutMode.Single;
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(Part<AvaloniaButton>(viewer, PageViewerToolbar.LayoutPart)).ShouldBe("Continuous");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Comes_From_The_Language()
    {
        var languages = ArcLanguageManager.Current!;
        var viewer = Viewer();
        var (_, _, window) = Show(viewer);
        try
        {
            var peer = (PageViewerAutomationPeer)ControlAutomationPeer.CreatePeerForElement(viewer);
            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(Part<AvaloniaButton>(viewer, PageViewerToolbar.ZoomInPart)).ShouldBe("放大");
            peer.GetName().ShouldBe("第 1 页，共 10 页");
            languages.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            peer.GetName().ShouldBe("第 1 頁，共 10 頁");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    #endregion

    #region Accessibility

    [AvaloniaFact]
    public void The_Peer_Is_A_Document_Named_After_The_Page()
    {
        var (viewer, _, window) = Show(Viewer());
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(viewer);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Document);
            peer.GetName().ShouldBe("Page 1 of 10");
            var value = peer.GetProvider<IValueProvider>().ShouldNotBeNull();
            value.IsReadOnly.ShouldBeTrue();
            viewer.GoToPage(2, animate: false);
            peer.GetName().ShouldBe("Page 3 of 10");
            value.Value.ShouldBe("Page 3 of 10");
            AutomationProperties.SetName(viewer, "Contract");
            peer.GetName().ShouldBe("Contract");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Viewport_Takes_Focus_And_Shows_A_Ring_For_The_Keyboard()
    {
        var (_, presenter, window) = Show(Viewer());
        try
        {
            presenter.Focusable.ShouldBeTrue();
            FocusViewport(presenter);
            TestHelpers.IsFocusRingVisible(presenter).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Derived viewers

    private sealed class ExtendedViewer : PageViewer
    {
        public ExtendedViewer(object pane, object overlay)
        {
            LeadingPane = pane;
            ContentOverlay = overlay;
        }

        protected override Type StyleKeyOverride => typeof(PageViewer);

        public List<(int Page, PixelSize Size)> Ready { get; } = new();

        public Bitmap? Thumbnail(int page, PixelSize size) => GetPageBitmap(page, size);

        protected override void OnPageBitmapReady(int pageIndex, PixelSize targetSize) => Ready.Add((pageIndex, targetSize));
    }

    [AvaloniaFact]
    public void A_Derived_Viewer_Uses_The_Theme_And_Fills_The_Pane_And_The_Overlay()
    {
        var pane = new Border { Width = 100 };
        var overlay = new TextBlock { Text = "Overlay", IsHitTestVisible = false };
        var viewer = new ExtendedViewer(pane, overlay) { PageSource = new FakePageSource(3, new Size(100, 100)) };
        var (_, presenter, window) = Show(viewer);
        try
        {
            TopLevel.GetTopLevel(pane).ShouldNotBeNull();
            TopLevel.GetTopLevel(overlay).ShouldNotBeNull();
            presenter.Bounds.Width.ShouldBe(300);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Derived_Viewer_Shares_The_Render_Cache_For_Thumbnails()
    {
        var source = new FakePageSource(3, new Size(100, 100));
        var viewer = new ExtendedViewer(new Border(), new Border()) { PageSource = source };
        var (_, _, window) = Show(viewer);
        try
        {
            var size = new PixelSize(30, 30);
            viewer.Thumbnail(2, size).ShouldBeNull();
            TestHelpers.PumpUntil(() => viewer.Ready.Count > 0).ShouldBeTrue();
            viewer.Ready.ShouldContain((2, size));
            viewer.Thumbnail(2, size).ShouldNotBeNull();
            source.Requests.Count(r => r.TargetSize == size).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
