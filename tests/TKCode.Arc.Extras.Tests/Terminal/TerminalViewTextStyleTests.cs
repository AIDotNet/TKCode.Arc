using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;
using static TKCode.Arc.Tests.Terminal.TerminalTestKit;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>Issue #139: bold and italic faces, faint, bold-is-bright and text blink.</summary>
public class TerminalViewTextStyleTests
{
    private static ResolvedStyle Styled(StyleFlags flags, TerminalColor? fg = null) =>
        ResolvedStyle.Default with { Flags = flags, Foreground = fg ?? TerminalColor.Default };

    [AvaloniaFact]
    public void Regular_Bold_BoldItalic_And_Italic_Text_Use_Four_Faces()
    {
        // printf 'R\e[1mB\e[3mBI\e[22mI\e[0m'
        ResolvedStyle[] styles = [ResolvedStyle.Default, Styled(StyleFlags.Bold), Styled(StyleFlags.Bold | StyleFlags.Italic), Styled(StyleFlags.Italic)];
        var row = Row([Cell('R'), Cell('B', style: 1), Cell('B', style: 2), Cell('I', style: 2), Cell('I', style: 3)], styles);
        With(Frame(row), view =>
        {
            var runs = view.GlyphRunsOf(row);
            runs.Count.ShouldBe(4);
            var faces = runs.Select(r => (r.Run.GlyphTypeface.Weight, r.Run.GlyphTypeface.Style)).ToArray();
            faces.ShouldBe([(FontWeight.Normal, FontStyle.Normal), (FontWeight.Bold, FontStyle.Normal), (FontWeight.Bold, FontStyle.Italic), (FontWeight.Normal, FontStyle.Italic)]);
            runs[2].Run.GlyphInfos.Count.ShouldBe(2);
        });
    }

    [AvaloniaFact]
    public void A_Bold_Weight_Below_Semibold_Is_Not_Emboldened()
    {
        var row = Row([Cell('a'), Cell('b', style: 1)], [ResolvedStyle.Default, Styled(StyleFlags.Bold)]);
        With(Frame(row), view =>
        {
            // The font has no Medium: the bold cell takes the regular face itself, so both cells share one run.
            var face = view.GlyphRunsOf(row).ShouldHaveSingleItem().Run.GlyphTypeface;
            face.Weight.ShouldBe(FontWeight.Normal);
            face.FontSimulations.HasFlag(FontSimulations.Bold).ShouldBeFalse();
        }, view => view.BoldFontWeight = FontWeight.Medium);
        With(Frame(row), view => view.GlyphRunsOf(row)[1].Run.GlyphTypeface.Weight.ShouldBe(FontWeight.Bold));
    }

    [AvaloniaFact]
    public void Faint_Text_Fades_Towards_Its_Background_On_A_Light_Theme()
    {
        var row = Text("dim", Styled(StyleFlags.Faint));
        With(Frame(LightPalette, new CursorState(0, 0, false, CursorStyle.Block), row), view =>
        {
            var color = view.GlyphRunsOf(row).ShouldHaveSingleItem().Brush.Color;
            // Halfway from (30, 30, 30) to white: lighter than normal text, not darker.
            color.ShouldBe(Color.FromRgb(143, 143, 143));
        });
    }

    [AvaloniaFact]
    public void Faint_Inverse_Text_Mixes_Towards_The_Swapped_Background()
    {
        var row = Text("x", Styled(StyleFlags.Faint | StyleFlags.Inverse));
        With(Frame(LightPalette, new CursorState(0, 0, false, CursorStyle.Block), row), view =>
        {
            // Inverse: white text on the dark foreground; faint mixes the white halfway towards (30, 30, 30).
            view.GlyphRunsOf(row).ShouldHaveSingleItem().Brush.Color.ShouldBe(Color.FromRgb(143, 143, 143));
        });
    }

    [AvaloniaTheory]
    [InlineData(true, 100 + 9)]
    [InlineData(false, 10 + 1)]
    public void Bold_Is_Bright_Moves_Palette_Colors_0_To_7_Up_By_Eight(bool boldIsBright, int red)
    {
        var row = Text("r", Styled(StyleFlags.Bold, TerminalColor.Palette(1)));
        With(Frame(row), view => view.GlyphRunsOf(row).ShouldHaveSingleItem().Brush.Color.ShouldBe(Color.FromRgb((byte)red, 0, 0)),
            view => view.BoldIsBright = boldIsBright);
    }

    [AvaloniaFact]
    public void Bold_Is_Bright_Leaves_Rgb_Colors_And_Plain_Text_Alone()
    {
        ResolvedStyle[] styles = [ResolvedStyle.Default, Styled(StyleFlags.Bold, TerminalColor.Rgb(1, 2, 3)), Styled(StyleFlags.None, TerminalColor.Palette(1))];
        var row = Row([Cell('a', style: 1), Cell(' '), Cell('b', style: 2)], styles);
        With(Frame(row), view =>
        {
            var runs = view.GlyphRunsOf(row);
            runs[0].Brush.Color.ShouldBe(Color.FromRgb(1, 2, 3));
            runs[1].Brush.Color.ShouldBe(Color.FromRgb(11, 0, 0));
        }, view => view.BoldIsBright = true);
    }

    [AvaloniaFact]
    public void Blinking_Cells_Get_Their_Own_Runs_And_Hide_In_The_Off_Phase()
    {
        var row = Row([Cell('a'), Cell('b', style: 1), Cell('c')], [ResolvedStyle.Default, Styled(StyleFlags.Blink)]);
        var now = TimeSpan.Zero;
        With(Frame(row), view =>
        {
            view.AllGlyphRunsOf(row).Select(r => r.Blink).ShouldBe([false, true, false]);
            view.IsBlinkTimerRunning.ShouldBeTrue();

            // The off phase hides only the blinking run; the cache is not rebuilt for it.
            var builds = view.RowRunBuilds;
            now = TimeSpan.FromMilliseconds(600);
            view.BlinkTick();
            Paint();
            view.RowRunBuilds.ShouldBe(builds);
            view.IsBlinkTimerRunning.ShouldBeTrue();
        }, view => view.Clock = () => now);
    }

    [AvaloniaFact]
    public void No_Timer_Runs_When_Nothing_Blinks_Or_Text_Blink_Is_Off()
    {
        With(Frame(Text("steady")), view => view.IsBlinkTimerRunning.ShouldBeFalse());
        With(Frame(Text("blink", Styled(StyleFlags.Blink))), view => view.IsBlinkTimerRunning.ShouldBeFalse(), view => view.TextBlink = false);
    }

    [AvaloniaFact]
    public void Changing_A_Text_Option_Rebuilds_The_Rows()
    {
        var row = Text("abc", Styled(StyleFlags.Bold, TerminalColor.Palette(2)));
        With(Frame(row), view =>
        {
            var builds = view.RowRunBuilds;
            view.BoldIsBright = true;
            Paint();
            view.RowRunBuilds.ShouldBe(builds + 1);
            view.GlyphRunsOf(row).ShouldHaveSingleItem().Brush.Color.ShouldBe(Color.FromRgb(110, 0, 0));
        });
    }
}
