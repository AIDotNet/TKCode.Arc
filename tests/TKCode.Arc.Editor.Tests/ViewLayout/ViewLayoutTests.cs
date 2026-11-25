// Tests for ViewLayout (vscode's src/vs/editor/common/viewLayout/viewLayout.ts, commit b57d5c9e, MIT license).
// Upstream has no test of viewLayout.ts of its own (its LinesLayout has linesLayout.test.ts, see LinesLayoutTests);
// these check the behaviour its code states: content and scroll dimensions, scrollBeyondLastLine and the horizontal
// scrollbar, the content width with and without wrapping, whitespaces, configuration changes, scrolling and view state.

using Shouldly;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;
using Xunit;
using Layout = TKCode.Arc.Editor.ViewLayout.ViewLayout;

namespace TKCode.Arc.Editor.Tests;

public sealed class ViewLayoutTests
{
    private sealed class TestConfiguration(ViewLayoutOptions options) : IViewLayoutConfiguration
    {
        public ViewLayoutOptions Options { get; set; } = options;
    }

    /// <summary>Animation frames that run only when the test says so.</summary>
    private sealed class Frames
    {
        private readonly List<Action> _pending = new();

        public int Count => _pending.Count;

        public IDisposable Schedule(Action callback)
        {
            _pending.Add(callback);
            return new Cancel(() => _pending.Remove(callback));
        }

        public void RunAll()
        {
            while (_pending.Count > 0)
            {
                var callback = _pending[0];
                _pending.RemoveAt(0);
                callback();
            }
        }

        private sealed class Cancel(Action action) : IDisposable
        {
            public void Dispose() => action();
        }
    }

    private static ViewLayoutOptions Options(
        double contentWidth = 800,
        double height = 200,
        double lineHeight = 10,
        bool scrollBeyondLastLine = false) => new()
        {
            LayoutInfo = new ViewLayoutOptions.LayoutInfoOptions(contentWidth, height, 14),
            LineHeight = lineHeight,
            ScrollBeyondLastLine = scrollBeyondLastLine,
        };

    private static Layout Create(TestConfiguration configuration, int lineCount, Frames? frames = null)
    {
        frames ??= new Frames();
        return new Layout(configuration, lineCount, [], frames.Schedule);
    }

    [Fact]
    public void Defaults_Are_Upstreams()
    {
        var options = new ViewLayoutOptions();
        options.ScrollBeyondLastLine.ShouldBeTrue();
        options.ScrollBeyondLastColumn.ShouldBe(4);
        options.SmoothScrolling.ShouldBeFalse();
        options.Padding.ShouldBe(new ViewLayoutOptions.PaddingOptions(0, 0));
        options.Scrollbar.ShouldBe(new ViewLayoutOptions.ScrollbarOptions(ScrollbarVisibility.Auto, 12, false));
        options.WrappingInfo.IsViewportWrapping.ShouldBeFalse();
        options.Minimap.ShouldBe(new ViewLayoutOptions.MinimapOptions(true, "right"));
        options.LineHeight.ShouldBe(OperatingSystem.IsMacOS() ? 18 : 19);
    }

    [Fact]
    public void Scroll_Dimensions_Without_ScrollBeyondLastLine()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);

        layout.GetContentWidth().ShouldBe(0);
        layout.GetScrollWidth().ShouldBe(800);
        layout.GetContentHeight().ShouldBe(1000);
        layout.GetScrollHeight().ShouldBe(1000);
        var viewport = layout.GetCurrentViewport();
        (viewport.Top, viewport.Left, viewport.Width, viewport.Height).ShouldBe((0d, 0d, 800d, 200d));
    }

    [Fact]
    public void Short_Content_Scrolls_Over_The_Viewport_Height()
    {
        using var layout = Create(new TestConfiguration(Options()), 5);

        layout.GetContentHeight().ShouldBe(50);
        layout.GetScrollHeight().ShouldBe(200);
    }

    [Fact]
    public void ScrollBeyondLastLine_Adds_The_Viewport_Height_Less_One_Line_And_The_Bottom_Padding()
    {
        using var layout = Create(new TestConfiguration(Options(scrollBeyondLastLine: true)), 100);
        layout.GetContentHeight().ShouldBe(1000 + (200 - 10));

        var padded = Options(scrollBeyondLastLine: true) with { Padding = new ViewLayoutOptions.PaddingOptions(5, 7) };
        using var paddedLayout = Create(new TestConfiguration(padded), 100);
        paddedLayout.GetContentHeight().ShouldBe(5 + 1000 + 7 + (200 - 10 - 7));

        // Never negative: a viewport smaller than a line adds nothing.
        using var tiny = Create(new TestConfiguration(Options(height: 5, scrollBeyondLastLine: true)), 3);
        tiny.GetContentHeight().ShouldBe(30);
    }

    [Fact]
    public void Content_Width_Without_Wrapping_Adds_ScrollBeyondLastColumn_And_The_Vertical_Scrollbar()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);

        layout.SetMaxLineWidth(500);
        layout.GetContentWidth().ShouldBe(500 + 4 * 10 + 14);
        layout.GetScrollWidth().ShouldBe(800);

        layout.SetOverlayWidgetsMinWidth(900);
        layout.GetContentWidth().ShouldBe(900);
        layout.GetScrollWidth().ShouldBe(900);

        layout.SetOverlayWidgetsMinWidth(0);
        layout.ChangeWhitespace(accessor => accessor.InsertWhitespace(3, 0, 20, 1200)).ShouldBeTrue();
        layout.SetMaxLineWidth(500);
        layout.GetContentWidth().ShouldBe(1200);
    }

    [Fact]
    public void Horizontal_Scrollbar_Height_Counts_Only_Without_ScrollBeyondLastLine()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);
        layout.SetMaxLineWidth(1000);
        layout.GetContentWidth().ShouldBe(1054);
        layout.GetContentHeight().ShouldBe(1000 + 12);

        layout.SetMaxLineWidth(100);
        layout.GetContentHeight().ShouldBe(1000);

        var hidden = Options() with
        {
            Scrollbar = new ViewLayoutOptions.ScrollbarOptions(ScrollbarVisibility.Hidden, 12, false),
        };
        using var hiddenLayout = Create(new TestConfiguration(hidden), 100);
        hiddenLayout.SetMaxLineWidth(1000);
        hiddenLayout.GetContentHeight().ShouldBe(1000);

        var ignored = Options() with
        {
            Scrollbar = new ViewLayoutOptions.ScrollbarOptions(ScrollbarVisibility.Auto, 12, true),
        };
        using var ignoredLayout = Create(new TestConfiguration(ignored), 100);
        ignoredLayout.SetMaxLineWidth(1000);
        ignoredLayout.GetContentHeight().ShouldBe(1000);

        using var beyond = Create(new TestConfiguration(Options(scrollBeyondLastLine: true)), 100);
        beyond.SetMaxLineWidth(1000);
        beyond.GetContentHeight().ShouldBe(1000 + 190);
    }

    [Fact]
    public void Content_Width_With_Viewport_Wrapping()
    {
        var wrapping = Options() with { WrappingInfo = new ViewLayoutOptions.WrappingInfoOptions(true) };
        var configuration = new TestConfiguration(wrapping);
        using var layout = Create(configuration, 100);

        layout.SetMaxLineWidth(805);
        layout.GetContentWidth().ShouldBe(805);

        // Wider than the viewport plus a character: the minimap on the right needs the scrollbar's width.
        layout.SetMaxLineWidth(900);
        layout.GetContentWidth().ShouldBe(914);

        configuration.Options = wrapping with { Minimap = new ViewLayoutOptions.MinimapOptions(false, "right") };
        layout.SetMaxLineWidth(900);
        layout.GetContentWidth().ShouldBe(900);

        configuration.Options = wrapping with { Minimap = new ViewLayoutOptions.MinimapOptions(true, "left") };
        layout.SetMaxLineWidth(900);
        layout.GetContentWidth().ShouldBe(900);
    }

    [Fact]
    public void Whitespaces_Change_The_Height_And_Raise_DidContentSizeChange()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);
        var sizes = new List<ContentSizeChangedEvent>();
        layout.DidContentSizeChange += sizes.Add;

        string? id = null;
        layout.ChangeWhitespace(accessor => id = accessor.InsertWhitespace(5, 0, 50, 0)).ShouldBeTrue();
        layout.GetContentHeight().ShouldBe(1050);
        layout.GetWhitespaces().ShouldHaveSingleItem().AfterLineNumber.ShouldBe(5);
        layout.GetVerticalOffsetForLineNumber(6).ShouldBe(100);
        // With the view zones: the top of the whitespace above the line.
        layout.GetVerticalOffsetForLineNumber(6, includeViewZones: true).ShouldBe(50);
        layout.GetLineNumberAtVerticalOffset(105).ShouldBe(6);
        layout.GetWhitespaceAtVerticalOffset(60)!.Id.ShouldBe(id);

        var e = sizes.ShouldHaveSingleItem();
        e.ContentHeightChanged.ShouldBeTrue();
        e.ContentWidthChanged.ShouldBeFalse();
        e.ContentHeight.ShouldBe(1050);
        e.IsNoOp().ShouldBeFalse();

        // A callback that changes nothing reports no change.
        layout.ChangeWhitespace(_ => { }).ShouldBeFalse();
        sizes.Count.ShouldBe(1);

        layout.ChangeWhitespace(accessor => accessor.RemoveWhitespace(id!)).ShouldBeTrue();
        layout.GetContentHeight().ShouldBe(1000);
        sizes.Count.ShouldBe(2);
    }

    [Fact]
    public void Special_Line_Heights_Change_The_Height()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);

        layout.ChangeSpecialLineHeights(accessor => accessor.InsertOrChangeCustomLineHeight("dec", 2, 3, 30)).ShouldBeTrue();
        layout.GetLineHeightForLineNumber(2).ShouldBe(30);
        layout.GetContentHeight().ShouldBe(1040);
    }

    [Fact]
    public void Lines_Inserted_And_Deleted_Update_The_Height_On_OnHeightMaybeChanged()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);

        layout.OnLinesInserted(10, 19);
        layout.GetContentHeight().ShouldBe(1000);
        layout.OnHeightMaybeChanged();
        layout.GetContentHeight().ShouldBe(1100);

        layout.OnLinesDeleted(1, 50);
        layout.OnHeightMaybeChanged();
        layout.GetContentHeight().ShouldBe(600);

        layout.OnFlushed(3, []);
        layout.OnHeightMaybeChanged();
        layout.GetContentHeight().ShouldBe(30);
    }

    [Fact]
    public void Configuration_Changes_Update_Only_What_Changed()
    {
        var configuration = new TestConfiguration(Options());
        using var layout = Create(configuration, 100);

        // lineHeight: the lines layout takes the new height, and the height is updated.
        configuration.Options = configuration.Options with { LineHeight = 20 };
        layout.OnConfigurationChanged(new ConfigurationChangedEvent([EditorOptionNames.LineHeight]));
        layout.GetContentHeight().ShouldBe(2000);

        // padding
        configuration.Options = configuration.Options with { Padding = new ViewLayoutOptions.PaddingOptions(4, 6) };
        layout.OnConfigurationChanged(new ConfigurationChangedEvent([EditorOptionNames.Padding]));
        layout.GetContentHeight().ShouldBe(2010);
        layout.IsInTopPadding(3).ShouldBeTrue();
        layout.IsInBottomPadding(2009).ShouldBeTrue();

        // layoutInfo: the viewport size.
        configuration.Options = configuration.Options with { LayoutInfo = new ViewLayoutOptions.LayoutInfoOptions(600, 300, 14) };
        layout.OnConfigurationChanged(new ConfigurationChangedEvent([EditorOptionNames.LayoutInfo]));
        var viewport = layout.GetCurrentViewport();
        (viewport.Width, viewport.Height).ShouldBe((600d, 300d));

        // An option the layout does not know about only updates the height (scrollBeyondLastLine here).
        configuration.Options = configuration.Options with { ScrollBeyondLastLine = true };
        layout.OnConfigurationChanged(new ConfigurationChangedEvent([EditorOptionNames.ScrollBeyondLastLine]));
        layout.GetContentHeight().ShouldBe(2010 + (300 - 20 - 6));

        // A change of lineHeight not announced is not taken by the lines layout.
        configuration.Options = configuration.Options with { LineHeight = 10 };
        layout.OnConfigurationChanged(new ConfigurationChangedEvent([EditorOptionNames.ScrollBeyondLastLine]));
        layout.GetVerticalOffsetForLineNumber(2).ShouldBe(24);
    }

    [Fact]
    public void SetScrollPosition_Immediate_Validates_And_Raises_DidScroll()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);
        var scrolls = new List<ScrollEvent>();
        layout.DidScroll += scrolls.Add;

        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 300.7), ScrollType.Immediate);
        layout.GetCurrentScrollTop().ShouldBe(300);
        var e = scrolls.ShouldHaveSingleItem();
        e.ScrollTopChanged.ShouldBeTrue();
        e.OldScrollTop.ShouldBe(0);

        // Clamped to the scroll height less the viewport height.
        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 5000), ScrollType.Immediate);
        layout.GetCurrentScrollTop().ShouldBe(800);

        layout.ValidateScrollPosition(new NewScrollPosition(-10, 900)).ScrollTop.ShouldBe(800);
        layout.ValidateScrollPosition(new NewScrollPosition(-10, 900)).ScrollLeft.ShouldBe(0);

        layout.DeltaScrollNow(0, -100);
        layout.GetCurrentScrollTop().ShouldBe(700);
        layout.GetCurrentViewport().Top.ShouldBe(700);
    }

    [Fact]
    public void Smooth_Scrolling_Runs_On_The_Hosts_Frames()
    {
        var frames = new Frames();
        var configuration = new TestConfiguration(Options() with { SmoothScrolling = true });
        using var layout = Create(configuration, 100, frames);

        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 400), ScrollType.Smooth);
        layout.HasPendingScrollAnimation().ShouldBeTrue();
        layout.GetFutureViewport().Top.ShouldBe(400);
        frames.Count.ShouldBe(1);

        // Immediate cancels the animation.
        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 100), ScrollType.Immediate);
        layout.HasPendingScrollAnimation().ShouldBeFalse();
        frames.Count.ShouldBe(0);
        layout.GetCurrentScrollTop().ShouldBe(100);

        // Turning smooth scrolling off makes Smooth immediate.
        configuration.Options = configuration.Options with { SmoothScrolling = false };
        layout.OnConfigurationChanged(new ConfigurationChangedEvent([EditorOptionNames.SmoothScrolling]));
        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 200), ScrollType.Smooth);
        layout.HasPendingScrollAnimation().ShouldBeFalse();
        layout.GetCurrentScrollTop().ShouldBe(200);
    }

    [Fact]
    public void Without_SmoothScrolling_Smooth_Is_Immediate()
    {
        var frames = new Frames();
        using var layout = Create(new TestConfiguration(Options()), 100, frames);

        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 400), ScrollType.Smooth);
        layout.HasPendingScrollAnimation().ShouldBeFalse();
        frames.Count.ShouldBe(0);
        layout.GetCurrentScrollTop().ShouldBe(400);
    }

    [Fact]
    public void Viewport_Data_Follows_The_Scroll_Position()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);
        layout.ChangeWhitespace(accessor => accessor.InsertWhitespace(25, 0, 30, 0));
        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 200), ScrollType.Immediate);

        var data = layout.GetLinesViewportData();
        data.StartLineNumber.ShouldBe(21);
        // Line 38 starts at 400, where the viewport ends.
        data.EndLineNumber.ShouldBe(37);
        data.CompletelyVisibleStartLineNumber.ShouldBe(21);

        var whitespaces = layout.GetWhitespaceViewportData();
        whitespaces.ShouldHaveSingleItem().VerticalOffset.ShouldBe(250);

        // AtScrollTop clamps the scroll top to the scroll range.
        var clamped = layout.GetLinesViewportDataAtScrollTop(5000);
        clamped.EndLineNumber.ShouldBe(100);
        layout.GetLinesViewportDataAtScrollTop(-50).StartLineNumber.ShouldBe(1);
    }

    [Fact]
    public void SaveState_Leaves_Out_The_Whitespace_Above_The_First_Line()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);
        layout.ChangeWhitespace(accessor => accessor.InsertWhitespace(5, 0, 50, 0));
        layout.SetScrollPosition(new NewScrollPosition(ScrollLeft: 0, ScrollTop: 300), ScrollType.Immediate);

        var state = layout.SaveState();
        state.ScrollTop.ShouldBe(300);
        state.ScrollTopWithoutViewZones.ShouldBe(250);
        state.ScrollLeft.ShouldBe(0);
    }

    [Fact]
    public void Scroll_Dimensions_Are_Truncated_To_Integers()
    {
        using var layout = Create(new TestConfiguration(Options(contentWidth: 800.9, height: 200.6)), 100);
        var viewport = layout.GetCurrentViewport();
        (viewport.Width, viewport.Height).ShouldBe((800d, 200d));

        layout.SetMaxLineWidth(1000.75);
        layout.GetContentWidth().ShouldBe(1054);
    }

    [Fact]
    public void Viewport_Truncates_Like_Javascript()
    {
        var viewport = new Viewport(10.9, -3.5, 100.2, double.NaN);
        (viewport.Top, viewport.Left, viewport.Width, viewport.Height).ShouldBe((10d, -3d, 100d, 0d));
    }

    [Fact]
    public void DidScroll_And_DidContentSizeChange_Can_Be_Removed()
    {
        using var layout = Create(new TestConfiguration(Options()), 100);
        var count = 0;
        Action<ScrollEvent> onScroll = _ => count++;
        Action<ContentSizeChangedEvent> onSize = _ => count++;
        layout.DidScroll += onScroll;
        layout.DidContentSizeChange += onSize;
        layout.DidScroll -= onScroll;
        layout.DidContentSizeChange -= onSize;

        layout.ChangeWhitespace(accessor => accessor.InsertWhitespace(1, 0, 10, 0));
        layout.SetScrollPosition(new NewScrollPosition(ScrollTop: 10), ScrollType.Immediate);

        count.ShouldBe(0);
    }
}
