using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;
using static TKCode.Arc.Tests.Terminal.TerminalTestKit;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>Issue #143: cursor blink, unfocused cursor, wide-cell cursor, cursor colors and the minimum contrast ratio.</summary>
public class TerminalViewCursorTests
{
    private static FrameSnapshot CursorFrame(CursorState cursor, params RowSnapshot[] rows) => Frame(DarkPalette, cursor, rows);

    private static void Focus(TerminalView view)
    {
        view.Focus();
        Paint();
        view.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Blinking_Cursor_Alternates_On_Timer_Ticks_And_Shows_Again_After_A_Key()
    {
        var now = TimeSpan.Zero;
        With(CursorFrame(new CursorState(0, 0, true, CursorStyle.Block, Blinking: true), Text("abc")), view =>
        {
            Focus(view);
            view.DrawnCursor.ShouldNotBeNull();
            view.IsBlinkTimerRunning.ShouldBeTrue();

            now = TimeSpan.FromMilliseconds(650);
            view.BlinkTick();
            Paint();
            view.DrawnCursor.ShouldBeNull();

            now = TimeSpan.FromMilliseconds(1250);
            view.BlinkTick();
            Paint();
            view.DrawnCursor.ShouldNotBeNull();

            now = TimeSpan.FromMilliseconds(1900);
            view.BlinkTick();
            Paint();
            view.DrawnCursor.ShouldBeNull();
            view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.A });
            view.InvalidateVisual();
            Paint();
            view.DrawnCursor.ShouldNotBeNull();
        }, view => view.Clock = () => now);
    }

    [AvaloniaFact]
    public void No_Timer_Runs_For_A_Steady_Cursor_Or_Without_Focus()
    {
        With(CursorFrame(new CursorState(0, 0, true, CursorStyle.Block), Text("abc")), view =>
        {
            Focus(view);
            view.IsBlinkTimerRunning.ShouldBeFalse();
        });
        With(CursorFrame(new CursorState(0, 0, true, CursorStyle.Block, Blinking: true), Text("abc")), view => view.IsBlinkTimerRunning.ShouldBeFalse());
        With(CursorFrame(new CursorState(0, 0, true, CursorStyle.Block, Blinking: true), Text("abc")), view =>
        {
            Focus(view);
            view.IsBlinkTimerRunning.ShouldBeFalse();
        }, view => view.CursorBlink = TerminalCursorBlink.Never);
    }

    [AvaloniaFact]
    public void Cursor_Blink_Always_Blinks_A_Steady_Cursor()
    {
        With(CursorFrame(new CursorState(0, 0, true, CursorStyle.Bar), Text("abc")), view =>
        {
            Focus(view);
            view.IsBlinkTimerRunning.ShouldBeTrue();
        }, view => view.CursorBlink = TerminalCursorBlink.Always);
    }

    [AvaloniaTheory]
    [InlineData(TerminalUnfocusedCursorStyle.Hollow, CursorStyle.BlockHollow)]
    [InlineData(TerminalUnfocusedCursorStyle.Same, CursorStyle.Bar)]
    public void An_Unfocused_View_Draws_Its_Unfocused_Cursor_Style(TerminalUnfocusedCursorStyle unfocused, CursorStyle drawn)
    {
        With(CursorFrame(new CursorState(1, 0, true, CursorStyle.Bar), Text("abc")), view =>
        {
            view.IsFocused.ShouldBeFalse();
            view.DrawnCursor.ShouldNotBeNull().Shape.ShouldBe(drawn);
        }, view => view.UnfocusedCursorStyle = unfocused);
    }

    [AvaloniaFact]
    public void Unfocused_Style_None_Hides_The_Cursor()
    {
        With(CursorFrame(new CursorState(1, 0, true, CursorStyle.Block), Text("abc")), view => view.DrawnCursor.ShouldBeNull(),
            view => view.UnfocusedCursorStyle = TerminalUnfocusedCursorStyle.None);
    }

    [AvaloniaTheory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void A_Cursor_On_A_Wide_Character_Covers_Both_Cells(int column, bool wideTail)
    {
        var row = Row([Cell('a'), Cell('中', CellWidth.Wide), Cell(0, CellWidth.SpacerTail), Cell('b')]);
        With(CursorFrame(new CursorState(column, 0, true, CursorStyle.Block, WideTail: wideTail), row), view =>
        {
            Focus(view);
            var bounds = view.DrawnCursor.ShouldNotBeNull().Bounds;
            bounds.X.ShouldBe(ColumnX(view, 1), 1e-9);
            bounds.Width.ShouldBe(2 * view.Metrics.CellWidth / view.Metrics.Scaling, 1e-9);
        });
    }

    [AvaloniaFact]
    public void A_Cursor_That_Would_Vanish_On_The_Cell_Background_Takes_The_Cell_Foreground()
    {
        // Cursor color equal to the default background: contrast 1 < 1.5.
        var palette = Palette(new TerminalRgb(200, 200, 200), new TerminalRgb(0, 0, 0), cursor: new TerminalRgb(0, 0, 0));
        With(Frame(palette, new CursorState(0, 0, true, CursorStyle.Block), Text("abc")), view =>
        {
            Focus(view);
            view.DrawnCursor.ShouldNotBeNull().Color.ShouldBe(Color.FromRgb(200, 200, 200));
        });
    }

    [Fact]
    public void Ensure_Contrast_Reaches_The_Ratio_And_Keeps_The_Hue()
    {
        var white = new TerminalRgb(255, 255, 255);
        var yellow = new TerminalRgb(205, 205, 0);
        TerminalColorMath.ContrastRatio(yellow, white).ShouldBeLessThan(4.5);
        var adjusted = TerminalColorMath.EnsureContrast(yellow, white, 4.5);
        TerminalColorMath.ContrastRatio(adjusted, white).ShouldBeGreaterThanOrEqualTo(4.5);
        adjusted.R.ShouldBe(adjusted.G);
        adjusted.B.ShouldBe((byte)0);
        TerminalColorMath.EnsureContrast(yellow, white, 1).ShouldBe(yellow);
        // On black the same color is lightened instead.
        var gray = new TerminalRgb(60, 60, 60);
        var lighter = TerminalColorMath.EnsureContrast(gray, new TerminalRgb(0, 0, 0), 4.5);
        lighter.R.ShouldBeGreaterThan(gray.R);
        TerminalColorMath.ContrastRatio(lighter, new TerminalRgb(0, 0, 0)).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [AvaloniaFact]
    public void Minimum_Contrast_Adjusts_Text_But_Not_Block_Glyphs_Or_Off_By_Default()
    {
        var yellow = ResolvedStyle.Default with { Foreground = TerminalColor.Rgb(205, 205, 0) };
        var same = ResolvedStyle.Default with { Foreground = TerminalColor.Rgb(255, 255, 255) };
        var row = Row([Cell('y', style: 1), Cell(0x2588, style: 2)], [ResolvedStyle.Default, yellow, same]);
        var frame = Frame(LightPalette, new CursorState(0, 0, false, CursorStyle.Block), row);
        With(frame, view =>
        {
            var text = view.GlyphRunsOf(row).ShouldHaveSingleItem().Brush.Color;
            TerminalColorMath.ContrastRatio(new TerminalRgb(text.R, text.G, text.B), new TerminalRgb(255, 255, 255)).ShouldBeGreaterThanOrEqualTo(4.5);
            view.SpritesOf(row).ShouldHaveSingleItem().Brush.Color.ShouldBe(Color.FromRgb(255, 255, 255));
        }, view => view.MinimumContrastRatio = 4.5);
        With(frame, view => view.GlyphRunsOf(row).ShouldHaveSingleItem().Brush.Color.ShouldBe(Color.FromRgb(205, 205, 0)));
    }

    [AvaloniaFact]
    public void Layers_Draw_In_Their_Places_With_The_View_Geometry_And_Colors()
    {
        var below = new RecordingLayer(TerminalLayerPlacement.BelowText);
        var above = new RecordingLayer(TerminalLayerPlacement.AboveText);
        var overlay = new RecordingLayer(TerminalLayerPlacement.Overlay);
        var red = ResolvedStyle.Default with { Foreground = TerminalColor.Rgb(255, 0, 0), Flags = StyleFlags.Inverse };
        var row = Row([Cell('a'), Cell('b', style: 1)], [ResolvedStyle.Default, red]);
        With(Frame(row), view =>
        {
            var calls = RecordingLayer.Order;
            calls.ShouldBe([TerminalLayerPlacement.BelowText, TerminalLayerPlacement.AboveText, TerminalLayerPlacement.Overlay]);
            below.Bounds.ShouldBe(view.CellBounds(1, 0));
            below.Colors.ShouldBe((Color.FromRgb(0, 0, 0), Color.FromRgb(255, 0, 0)));
            var center = view.CellBounds(1, 0).Center;
            view.CellAt(center).ShouldBe(new ViewportPoint(1, 0));
            view.CellAt(new Point(-5, -5)).ShouldBeNull();
        }, view =>
        {
            RecordingLayer.Order.Clear();
            view.Layers.Add(overlay);
            view.Layers.Add(above);
            view.Layers.Add(below);
        });
    }

    private sealed class RecordingLayer(TerminalLayerPlacement placement) : ITerminalRenderLayer
    {
        public static readonly List<TerminalLayerPlacement> Order = [];

        public TerminalLayerPlacement Placement { get; } = placement;

        public Rect Bounds { get; private set; }

        public (Color, Color) Colors { get; private set; }

        public void Render(DrawingContext drawingContext, TerminalRenderContext context)
        {
            if (Order.Count < 3)
            {
                Order.Add(Placement);
            }
            Bounds = context.CellBounds(1, 0);
            Colors = context.ColorsAt(1, 0);
        }
    }
}
