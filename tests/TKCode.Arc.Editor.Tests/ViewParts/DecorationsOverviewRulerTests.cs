// Tests for the port of vscode's decorationsOverviewRuler.ts (commit b57d5c9e, MIT license). Upstream has no unit test
// for it (it draws on a canvas); these check the lanes, the decoration and cursor marks and when a frame is redrawn.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewParts;
using Xunit;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Tests;

public sealed class DecorationsOverviewRulerTests
{
    private static readonly RgbaColor Red = new(255, 0, 0);
    private static readonly RgbaColor Cursor = new(0, 0, 200);
    private static readonly RgbaColor Border = new(10, 10, 10);

    private static OverviewRulerSettings Settings(int lanes = 3, double height = 190, bool hideCursor = false, RgbaColor? border = null) =>
        new(new OverviewRulerPosition(14, height, 0, 0), lineHeight: 19, overviewRulerLanes: lanes, borderColor: border, hideCursor: hideCursor, cursorColor: Cursor);

    private static void WithRuler(int lineCount, Action<EditorViewModel, TextModel, DecorationsOverviewRuler> callback, OverviewRulerSettings? settings = null)
    {
        var lines = Enumerable.Range(1, lineCount).Select(i => $"line {i}").ToArray();
        ViewModelTestUtils.WithTestViewModel(lines, new TestCodeEditorOptions { OuterHeight = 190 }, (viewModel, model, _) =>
        {
            var ruler = new DecorationsOverviewRuler(viewModel, settings ?? Settings());
            ruler.ColorResolver = color => color == "theme.red" ? Red : RgbaColor.TryParseHex(color);
            viewModel.AddViewEventHandler(ruler);
            callback(viewModel, model, ruler);
            viewModel.RemoveViewEventHandler(ruler);
        });
    }

    private static ModelDeltaDecoration RulerDecoration(int startLine, int endLine, string color, OverviewRulerLane lane) =>
        new(new TextRange(startLine, 1, endLine, 1), new ModelDecorationOptions("test")
        {
            OverviewRuler = new ModelDecorationOverviewRulerOptions { Color = DecorationColor.FromTheme(color), Position = lane },
        });

    [Fact]
    public void Three_Lanes_Split_The_Canvas_After_A_One_Pixel_Offset()
    {
        var settings = Settings();
        settings.CanvasWidth.ShouldBe(14);
        settings.X.ShouldBe([0, 1, 5, 1, 10, 1, 5, 1]);
        settings.W.ShouldBe([0, 4, 5, 9, 4, 13, 9, 13]);
    }

    [Fact]
    public void Two_And_One_Lanes()
    {
        var two = Settings(lanes: 2);
        two.X.ShouldBe([0, 1, 1, 1, 7, 1, 1, 1]);
        two.W.ShouldBe([0, 6, 6, 6, 7, 13, 13, 13]);

        var one = Settings(lanes: 1);
        one.X.ShouldAllBe(x => x == 0 || x == 1);
        one.W.Skip(1).ShouldAllBe(w => w == 13);
    }

    [Fact]
    public void No_Lanes_Hides_The_Ruler()
    {
        var settings = Settings(lanes: 0);
        settings.CanvasWidth.ShouldBe(0);
        settings.CanvasHeight.ShouldBe(0);
        WithRuler(5, (_, _, ruler) => ruler.Render().IsHidden.ShouldBeTrue(), settings);
    }

    [Fact]
    public void The_Cursor_Is_A_Mark_Over_All_Lanes_At_70_Percent()
    {
        WithRuler(100, (_, _, ruler) =>
        {
            var frame = ruler.Render();
            var rect = frame.Rects.ShouldHaveSingleItem();
            rect.Color.ShouldBe(Cursor with { A = 179 });
            (rect.X, rect.Width).ShouldBe((1, 13));
            rect.Height.ShouldBe(2);
            rect.Y.ShouldBe(0);
        });
    }

    [Fact]
    public void A_Hidden_Cursor_Draws_Nothing()
    {
        WithRuler(10, (_, _, ruler) => ruler.Render().Rects.ShouldBeEmpty(), Settings(hideCursor: true));
    }

    [Fact]
    public void Decorations_Are_Drawn_In_Their_Lane_With_A_Minimum_Height()
    {
        WithRuler(100, (viewModel, model, ruler) =>
        {
            model.DeltaDecorations([], [RulerDecoration(50, 50, "theme.red", OverviewRulerLane.Right)]);
            var frame = ruler.Render();
            var mark = frame.Rects.First(r => r.Color == Red);
            (mark.X, mark.Width).ShouldBe((10, 4));
            mark.Height.ShouldBe(6);

            // the scroll height is 100 lines + a viewport less a line beyond the last line
            var scrollHeight = viewModel.ViewLayout.GetScrollHeight();
            var heightRatio = 190 / scrollHeight;
            var y1 = (int)(49 * 19 * heightRatio);
            var y2 = (int)(((49 * 19) + 19) * heightRatio);
            mark.Y.ShouldBe(((y1 + y2) / 2) - 3);
        });
    }

    [Fact]
    public void Close_Decorations_Of_One_Color_And_Lane_Merge()
    {
        WithRuler(100, (_, model, ruler) =>
        {
            model.DeltaDecorations([],
            [
                RulerDecoration(10, 10, "#00ff00", OverviewRulerLane.Left),
                RulerDecoration(11, 11, "#00ff00", OverviewRulerLane.Left),
                RulerDecoration(80, 80, "#00ff00", OverviewRulerLane.Left),
            ]);
            var green = ruler.Render().Rects.Where(r => r.Color == new RgbaColor(0, 255, 0)).ToList();
            green.Count.ShouldBe(2);
            green.ShouldAllBe(r => r.X == 1 && r.Width == 4);
        });
    }

    [Fact]
    public void A_Color_The_Resolver_Does_Not_Know_Draws_Nothing()
    {
        WithRuler(20, (_, model, ruler) =>
        {
            model.DeltaDecorations([], [RulerDecoration(5, 5, "unknown.color", OverviewRulerLane.Center)]);
            ruler.Render().Rects.Count.ShouldBe(1); // the cursor only
        });
    }

    [Fact]
    public void An_Unchanged_Frame_Is_Not_Drawn_Again()
    {
        WithRuler(100, (viewModel, model, ruler) =>
        {
            var first = ruler.Render();
            ruler.Render().ShouldBeSameAs(first);

            // a cursor move on the same line: maybe needed, but nothing changed
            viewModel.SetSelections("test", [new TextSelection(1, 3, 1, 3)]);
            ruler.Render().ShouldBeSameAs(first);

            // another line: drawn again
            viewModel.SetSelections("test", [new TextSelection(60, 1, 60, 1)]);
            var moved = ruler.Render();
            moved.ShouldNotBeSameAs(first);
            moved.Rects.ShouldHaveSingleItem().Y.ShouldBeGreaterThan(0);

            // a decoration: drawn again
            model.DeltaDecorations([], [RulerDecoration(5, 5, "theme.red", OverviewRulerLane.Full)]);
            ruler.Render().ShouldNotBeSameAs(moved);
        });
    }

    [Fact]
    public void Several_Cursors_Use_The_Primary_And_Secondary_Colors()
    {
        var primary = new RgbaColor(1, 2, 3);
        var secondary = new RgbaColor(4, 5, 6);
        var settings = new OverviewRulerSettings(new OverviewRulerPosition(14, 190, 0, 0), 19, cursorColor: Cursor, cursorColorPrimary: primary, cursorColorSecondary: secondary);
        WithRuler(100, (viewModel, _, ruler) =>
        {
            viewModel.SetSelections("test", [new TextSelection(80, 1, 80, 1), new TextSelection(10, 1, 10, 1)]);
            var rects = ruler.Render().Rects;
            rects.Count.ShouldBe(2);
            // sorted by position: line 10 (secondary) first
            rects[0].Color.ShouldBe(secondary with { A = 179 });
            rects[1].Color.ShouldBe(primary with { A = 179 });
        }, settings);
    }

    [Fact]
    public void The_Border_Is_The_Left_And_Top_Edges()
    {
        WithRuler(10, (_, _, ruler) =>
        {
            var rects = ruler.Render().Rects.Where(r => r.Color == Border).ToList();
            rects.ShouldBe([new OverviewRulerRect(Border, 0, 0, 1, 190), new OverviewRulerRect(Border, 1, 0, 13, 1)]);
        }, Settings(border: Border));
    }

    [Fact]
    public void New_Settings_Redraw_Only_When_They_Differ()
    {
        WithRuler(10, (_, _, ruler) =>
        {
            var first = ruler.Render();
            ruler.SetSettings(Settings()).ShouldBeFalse();
            ruler.Render().ShouldBeSameAs(first);
            ruler.SetSettings(Settings(height: 300)).ShouldBeTrue();
            var taller = ruler.Render();
            taller.ShouldNotBeSameAs(first);
            taller.Settings.CanvasHeight.ShouldBe(300);
        });
    }

    [Fact]
    public void Pixel_Ratio_Scales_The_Canvas_And_The_Marks()
    {
        var settings = new OverviewRulerSettings(new OverviewRulerPosition(14, 190, 0, 0), 19, pixelRatio: 2, cursorColor: Cursor);
        settings.CanvasWidth.ShouldBe(28);
        settings.CanvasHeight.ShouldBe(380);
        WithRuler(10, (_, _, ruler) => ruler.Render().Rects.ShouldHaveSingleItem().Height.ShouldBe(4), settings);
    }
}
