// Tests for the port of vscode's minimap.ts drawing (InnerMinimap.renderLines / renderDecorations and the IMinimapModel
// of Minimap, commit b57d5c9e, MIT license). Upstream tests the minimap's parts (minimapCharRenderer, minimapLayout)
// but not the drawing, which needs a canvas; these render through a real view model into the image buffer.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;
using TKCode.Arc.Editor.ViewParts;
using Xunit;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc.Editor.Tests;

public sealed class MinimapRenderTests
{
    private const double Height = 190;
    private const double LineHeight = 19;
    private static readonly RgbaColor Foreground = new(0, 0, 0);
    private static readonly RgbaColor Background = new(255, 255, 255);
    private static readonly RgbaColor SelectionColor = new(0, 0, 255);

    private static MinimapOptions CreateOptions(int viewLineCount, EditorMinimapOptions? minimap = null, MinimapColors? colors = null)
    {
        var minimapOptions = minimap ?? EditorMinimapOptions.Default;
        var layout = EditorLayoutInfoComputer.ComputeMinimapLayout(new MinimapLayoutInput
        {
            OuterWidth = 800,
            OuterHeight = Height,
            LineHeight = LineHeight,
            TypicalHalfwidthCharacterWidth = 10,
            PixelRatio = 1,
            ScrollBeyondLastLine = true,
            Minimap = minimapOptions,
            VerticalScrollbarWidth = 14,
            ViewLineCount = viewLineCount,
            RemainingWidth = 760,
        }, new ComputeOptionsMemory());
        var tracker = new MinimapTokensColorTracker([RgbaColor.Transparent, Foreground, Background]);
        return new MinimapOptions(
            new EditorLayoutInfo { Height = Height, Minimap = layout },
            minimapOptions,
            pixelRatio: 1,
            lineHeight: LineHeight,
            typicalHalfwidthCharacterWidth: 10,
            paddingTop: 0,
            paddingBottom: 0,
            scrollBeyondLastLine: true,
            tracker,
            colors ?? new MinimapColors { Selection = SelectionColor });
    }

    private static void WithMinimap(string[] lines, Action<EditorViewModel, TextModel, Minimap> callback, EditorMinimapOptions? minimap = null)
    {
        ViewModelTestUtils.WithTestViewModel(lines, new TestCodeEditorOptions { OuterHeight = Height }, (viewModel, model, _) =>
        {
            var view = new Minimap(viewModel, CreateOptions(lines.Length, minimap));
            viewModel.AddViewEventHandler(view);
            callback(viewModel, model, view);
            viewModel.RemoveViewEventHandler(view);
        });
    }

    // whether any pixel of the rows [y1, y2) differs from the background
    private static bool HasInk(MinimapImageData image, int y1, int y2)
    {
        for (var y = y1; y < y2; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = ((y * image.Width) + x) * 4;
                if (image.Data[offset] != Background.R || image.Data[offset + 1] != Background.G || image.Data[offset + 2] != Background.B)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public void The_Default_Layout_Is_Proportional_Text_At_Scale_One()
    {
        var options = CreateOptions(10);
        options.RenderMinimap.ShouldBe(RenderMinimap.Text);
        options.MinimapLineHeight.ShouldBe(2);
        options.MinimapCharWidth.ShouldBe(1);
        options.FontScale.ShouldBe(1);
        // maxColumn 120 at one pixel a column
        options.MinimapWidth.ShouldBe(Math.Min(120, Math.Floor((760 - 14 - 2) * 1.0 / 11) + 8));
        options.MinimapLeft.ShouldBe(800 - options.MinimapWidth - 14);
    }

    [Fact]
    public void Lines_With_Text_Are_Drawn_And_Empty_Lines_Are_Not()
    {
        WithMinimap(["hello world", "", "    indented"], (_, _, minimap) =>
        {
            var frame = minimap.Render().ShouldNotBeNull();
            var image = frame.ImageData.ShouldNotBeNull();
            (frame.DirtyY1, frame.DirtyY2).ShouldBe((0, image.Height));
            HasInk(image, 0, 2).ShouldBeTrue();
            HasInk(image, 2, 4).ShouldBeFalse();
            HasInk(image, 4, 6).ShouldBeTrue();
            HasInk(image, 6, image.Height).ShouldBeFalse();

            // the characters start after the minimap gutter (8px); the indented line after 4 more columns
            for (var x = 0; x < EditorLayoutInfoComputer.MinimapGutterWidth + 4; x++)
            {
                for (var y = 4; y < 6; y++)
                {
                    image.Data[((y * image.Width) + x) * 4].ShouldBe(Background.R);
                }
            }
        });
    }

    [Fact]
    public void An_Edit_Repaints_Only_The_Changed_Line()
    {
        var lines = Enumerable.Range(1, 10).Select(i => $"line {i}").ToArray();
        WithMinimap(lines, (_, model, minimap) =>
        {
            minimap.Render();
            model.ApplyEdits([new SingleEditOperation(new TextRange(5, 1, 5, 1), "xxxxxxxxxx")]);
            var frame = minimap.Render().ShouldNotBeNull();
            // rows of lines 1-4 and 6-10 are copied from the last frame
            (frame.DirtyY1, frame.DirtyY2).ShouldBe((8, 10));
        });
    }

    [Fact]
    public void Nothing_Changed_Copies_Nothing()
    {
        WithMinimap(["a", "b"], (_, _, minimap) =>
        {
            minimap.Render();
            minimap.ShouldRender().ShouldBeFalse();
            var frame = minimap.Render().ShouldNotBeNull();
            (frame.DirtyY1, frame.DirtyY2).ShouldBe((0, 0));
        });
    }

    [Fact]
    public void Blocks_Mode_Draws_Three_Pixel_Lines()
    {
        WithMinimap(["abc", "", "def"], (_, _, minimap) =>
        {
            minimap.Options.RenderMinimap.ShouldBe(RenderMinimap.Blocks);
            minimap.Options.MinimapLineHeight.ShouldBe(3);
            var image = minimap.Render()!.ImageData!;
            HasInk(image, 0, 3).ShouldBeTrue();
            HasInk(image, 3, 6).ShouldBeFalse();
            HasInk(image, 6, 9).ShouldBeTrue();
        }, EditorMinimapOptions.Default with { RenderCharacters = false });
    }

    [Fact]
    public void The_Selection_Is_A_Line_Highlight_And_A_Range()
    {
        WithMinimap(["hello world", "second", "third"], (viewModel, _, minimap) =>
        {
            viewModel.SetSelections("test", [new TextSelection(1, 1, 1, 6)]);
            var frame = minimap.Render()!;
            frame.DecorationsChanged.ShouldBeTrue();
            // one line: the selected characters only (upstream's line highlight spans from the start line's top to the
            // end line's top, which is empty for one line); columns 1-6 are five pixels after the gutter
            var range = frame.Decorations.ShouldHaveSingleItem();
            range.Color.ShouldBe(SelectionColor);
            (range.X, range.Y, range.Width, range.Height).ShouldBe((8.0, 0.0, 5.0, 2.0));

            viewModel.SetSelections("test", [new TextSelection(1, 1, 3, 2)]);
            var rects = minimap.Render()!.Decorations;
            rects.Count.ShouldBe(4);
            // the line highlight at half the selection color's alpha, over the whole width after the gutter
            rects[0].Color.ShouldBe(SelectionColor with { A = 128 });
            (rects[0].X, rects[0].Y, rects[0].Height).ShouldBe((8.0, 0.0, 4.0));
            rects.Skip(1).Select(r => r.Y).ShouldBe([0.0, 2.0, 4.0]);
            rects[3].Width.ShouldBe(1.0);
        });
    }

    [Fact]
    public void Minimap_Decorations_Are_Drawn_Inline_Or_In_The_Gutter()
    {
        WithMinimap(["hello world", "second", "third"], (_, model, minimap) =>
        {
            minimap.DecorationColorResolver = o => o.Color.ThemeColorId == "minimap.errorHighlight" ? new RgbaColor(255, 0, 0) : new RgbaColor(0, 255, 0);
            model.DeltaDecorations([],
            [
                new ModelDeltaDecoration(new TextRange(2, 1, 2, 3), new ModelDecorationOptions("test")
                {
                    Minimap = new ModelDecorationMinimapOptions { Color = DecorationColor.FromTheme("minimap.errorHighlight"), Position = MinimapPosition.Inline },
                }),
                new ModelDeltaDecoration(new TextRange(3, 1, 3, 1), new ModelDecorationOptions("test")
                {
                    Minimap = new ModelDecorationMinimapOptions { Color = DecorationColor.FromValue("#00ff00"), Position = MinimapPosition.Gutter },
                }),
            ]);
            var rects = minimap.Render()!.Decorations;
            rects.ShouldContain(r => r.Color == new RgbaColor(255, 0, 0, 128) && r.Y == 2 && r.X == 8);
            rects.ShouldContain(r => r.Color == new RgbaColor(255, 0, 0) && r.Y == 2 && r.Width == 2);
            rects.ShouldContain(r => r.Color == new RgbaColor(0, 255, 0) && r.Y == 4 && r.X == 2 && r.Width == 2);
        });
    }

    [Fact]
    public void The_Slider_Follows_The_Scroll_Position()
    {
        var lines = Enumerable.Range(1, 500).Select(i => $"line {i}").ToArray();
        WithMinimap(lines, (viewModel, _, minimap) =>
        {
            var top = minimap.Render()!.Layout;
            top.SliderNeeded.ShouldBeTrue();
            top.SliderTop.ShouldBe(0);
            // ten lines of the viewport at two pixels a line
            top.SliderHeight.ShouldBe(20);

            viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: 100 * LineHeight), ScrollType.Immediate);
            var scrolled = minimap.Render()!.Layout;
            scrolled.SliderTop.ShouldBeGreaterThan(0);
            scrolled.StartLineNumber.ShouldBeGreaterThan(1);
        });
    }

    [Fact]
    public void A_Click_Maps_To_The_Line_Under_It()
    {
        var lines = Enumerable.Range(1, 50).Select(i => $"line {i}").ToArray();
        WithMinimap(lines, (_, _, minimap) =>
        {
            minimap.GetLineNumberAtY(5).ShouldBeNull();
            minimap.Render();
            minimap.GetLineNumberAtY(0).ShouldBe(1);
            minimap.GetLineNumberAtY(5).ShouldBe(3);
            minimap.GetLineNumberAtY(10_000).ShouldBe(50);
        });
    }

    [Fact]
    public void Dragging_The_Slider_Scrolls_The_Editor()
    {
        var lines = Enumerable.Range(1, 500).Select(i => $"line {i}").ToArray();
        WithMinimap(lines, (viewModel, _, minimap) =>
        {
            var layout = minimap.Render()!.Layout;
            minimap.SetScrollTop(layout.GetDesiredScrollTopFromDelta(50));
            viewModel.ViewLayout.GetCurrentScrollTop().ShouldBeGreaterThan(0);
            minimap.GetEditorScrollTop().ShouldBe(viewModel.ViewLayout.GetCurrentScrollTop());
        });
    }

    [Fact]
    public void New_Options_That_Draw_The_Same_Are_Ignored()
    {
        WithMinimap(["a"], (_, _, minimap) =>
        {
            minimap.SetOptions(CreateOptions(1)).ShouldBeFalse();
            minimap.SetOptions(CreateOptions(1, EditorMinimapOptions.Default with { Scale = 2 })).ShouldBeTrue();
            minimap.Options.FontScale.ShouldBe(2);
            minimap.Render()!.ImageData.ShouldNotBeNull();
        });
    }
}
