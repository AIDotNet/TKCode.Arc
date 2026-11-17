using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;
using static TKCode.Arc.Tests.Terminal.TerminalTestKit;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>Issue #142: box drawing, block elements, braille and Powerline glyphs drawn by the view.</summary>
public class TerminalBuiltInGlyphsTests
{
    private static readonly TerminalBuiltInGlyphs s_glyphs = TerminalBuiltInGlyphs.Instance;

    private static Rect Bounds(int cp, int w, int h, int t = 1) =>
        s_glyphs.Create(cp, new TerminalGlyphRequest(w, h, t, 1)).ShouldNotBeNull().Geometry.Bounds;

    [Theory]
    [InlineData(0x2500, true)]
    [InlineData(0x257F, true)]
    [InlineData(0x2588, true)]
    [InlineData(0x259F, true)]
    [InlineData(0x2800, true)]
    [InlineData(0x28FF, true)]
    [InlineData(0xE0B0, true)]
    [InlineData(0xE0BF, true)]
    [InlineData(0xE0D2, true)]
    [InlineData(0xE0D4, true)]
    [InlineData(0x24FF, false)]
    [InlineData(0x25A0, false)]
    [InlineData(0xE0C0, false)]
    [InlineData('a', false)]
    public void Handles_The_P1_Ranges(int cp, bool handled) => s_glyphs.Handles(cp).ShouldBe(handled);

    [AvaloniaFact]
    public void Every_Code_Point_In_The_Ranges_Has_A_Shape_Or_Is_Blank()
    {
        foreach (var cp in Enumerable.Range(0x2500, 0xA0).Concat(Enumerable.Range(0x2800, 0x100)).Concat(Enumerable.Range(0xE0B0, 16)).Append(0xE0D2).Append(0xE0D4))
        {
            var glyph = s_glyphs.Create(cp, new TerminalGlyphRequest(9, 19, 1, 1));
            if (cp == 0x2800)
            {
                glyph.ShouldBeNull();
                continue;
            }
            glyph.ShouldNotBeNull($"U+{cp:X4}");
            var bounds = glyph.Geometry.Bounds;
            bounds.Width.ShouldBeGreaterThan(0, $"U+{cp:X4}");
            bounds.Height.ShouldBeGreaterThan(0, $"U+{cp:X4}");
        }
    }

    [AvaloniaTheory]
    [InlineData(8, 16, 1)]
    [InlineData(9, 17, 1)]
    [InlineData(12, 25, 2)]
    [InlineData(15, 31, 3)]
    public void Lines_Reach_The_Cell_Edges_On_Whole_Pixels(int w, int h, int t)
    {
        // ┼ covers the whole cell, so neighbors meet; ─ and │ sit on the same centers at every size.
        Bounds(0x253C, w, h, t).ShouldBe(new Rect(0, 0, w, h));
        var horizontal = Bounds(0x2500, w, h, t);
        horizontal.ShouldBe(new Rect(0, (h - t) / 2, w, t));
        var vertical = Bounds(0x2502, w, h, t);
        vertical.ShouldBe(new Rect((w - t) / 2, 0, t, h));
        // A heavy line is twice as thick and stays centered on whole pixels.
        Bounds(0x2501, w, h, t).ShouldBe(new Rect(0, (h - 2 * t) / 2, w, 2 * t));
        // A corner meets its neighbors' lines at the same rows and columns.
        var corner = Bounds(0x250C, w, h, t);
        corner.Right.ShouldBe(w);
        corner.Bottom.ShouldBe(h);
        corner.Top.ShouldBe(horizontal.Top);
        corner.Left.ShouldBe(vertical.Left);
    }

    [AvaloniaTheory]
    [InlineData(8, 16)]
    [InlineData(9, 17)]
    [InlineData(10, 21)]
    public void Halves_Leave_No_Seam_And_Do_Not_Overlap(int w, int h)
    {
        Bounds(0x2588, w, h).ShouldBe(new Rect(0, 0, w, h));
        var upper = Bounds(0x2580, w, h);
        var lower = Bounds(0x2584, w, h);
        upper.Top.ShouldBe(0);
        lower.Bottom.ShouldBe(h);
        upper.Bottom.ShouldBe(lower.Top);
        Bounds(0x258C, w, h).Right.ShouldBe(Bounds(0x2590, w, h).Left);
        // Eighths are on whole pixels.
        var eighth = Bounds(0x2581, w, h);
        eighth.Top.ShouldBe(Math.Round(eighth.Top));
    }

    [AvaloniaFact]
    public void Double_Lines_Have_Two_Rails_And_Rounded_Corners_Are_Stroked()
    {
        var w = 10;
        var h = 20;
        var doubleH = Bounds(0x2550, w, h);
        doubleH.Width.ShouldBe(w);
        doubleH.Height.ShouldBe(3); // rail, gap, rail
        var arc = s_glyphs.Create(0x256D, new TerminalGlyphRequest(w, h, 1, 1)).ShouldNotBeNull();
        arc.StrokeThickness.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Shades_Are_Solid_Fills_At_Partial_Opacity()
    {
        s_glyphs.Create(0x2591, new TerminalGlyphRequest(8, 16, 1, 1))!.Opacity.ShouldBe(0.25);
        s_glyphs.Create(0x2592, new TerminalGlyphRequest(8, 16, 1, 1))!.Opacity.ShouldBe(0.5);
        s_glyphs.Create(0x2593, new TerminalGlyphRequest(8, 16, 1, 1))!.Opacity.ShouldBe(0.75);
        Bounds(0x2592, 8, 16).ShouldBe(new Rect(0, 0, 8, 16));
    }

    [AvaloniaFact]
    public void Braille_Dots_Are_Inside_The_Cell()
    {
        var all = Bounds(0x28FF, 8, 16);
        all.Left.ShouldBeGreaterThanOrEqualTo(0);
        all.Right.ShouldBeLessThanOrEqualTo(8);
        all.Bottom.ShouldBeLessThanOrEqualTo(16);
        Bounds(0x2801, 8, 16).Right.ShouldBeLessThan(Bounds(0x2808, 8, 16).Left);
    }

    [AvaloniaFact]
    public void Powerline_Arrows_Fill_The_Cell_Height()
    {
        Bounds(0xE0B0, 8, 16).ShouldBe(new Rect(0, 0, 8, 16));
        Bounds(0xE0B2, 8, 16).ShouldBe(new Rect(0, 0, 8, 16));
    }

    [AvaloniaFact]
    public void The_View_Draws_These_Cells_As_Sprites_When_Custom_Glyphs_Are_On()
    {
        var row = Text("╭─┬─╮▀▄░⠿x");
        With(Frame(row), view =>
        {
            view.SpritesOf(row).Select(s => s.Column).ShouldBe([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]);
            view.GlyphRunsOf(row).ShouldHaveSingleItem().Run.Characters.ToString().ShouldBe("x");
        });
    }

    [AvaloniaFact]
    public void The_View_Takes_Them_From_The_Font_When_Custom_Glyphs_Are_Off()
    {
        var row = Text("─┼█");
        With(Frame(row), view =>
        {
            view.SpritesOf(row).ShouldBeEmpty();
            view.GlyphRunsOf(row).ShouldHaveSingleItem().Run.GlyphInfos.Count.ShouldBe(3);
        }, view => view.CustomGlyphs = false);
    }

    [AvaloniaFact]
    public void A_Shade_Is_Drawn_In_The_Foreground_At_Its_Opacity_And_A_Wide_Cell_Covers_Two_Cells()
    {
        var style = ResolvedStyle.Default with { Foreground = TerminalColor.Rgb(10, 20, 30) };
        var row = Row([Cell(0x2592, style: 1), Cell(0x2588, CellWidth.Wide, style: 1), Cell(0, CellWidth.SpacerTail, style: 1)], [ResolvedStyle.Default, style]);
        With(Frame(row), view =>
        {
            var sprites = view.SpritesOf(row);
            sprites.Count.ShouldBe(2);
            sprites[0].Brush.Color.ShouldBe(Color.FromRgb(10, 20, 30));
            sprites[0].Brush.Opacity.ShouldBe(0.5);
            sprites[1].Width.ShouldBe(2 * view.Metrics.CellWidth);
        });
    }

    [AvaloniaFact]
    public void A_Host_Provider_Is_Asked_First_And_Draws_Even_With_Custom_Glyphs_Off()
    {
        var provider = new DotProvider();
        var row = Text("─");
        With(Frame(row), view =>
        {
            var sprite = view.SpritesOf(row).ShouldHaveSingleItem();
            sprite.Column.ShouldBe(0);
            sprite.Glyph.Geometry.Bounds.ShouldBe(new Rect(0, 0, 2, 2));
            provider.Created.ShouldBe(1);
        }, view =>
        {
            view.CustomGlyphs = false;
            view.GlyphProviders.Add(provider);
        });
    }

    private sealed class DotProvider : ITerminalGlyphProvider
    {
        public int Created { get; private set; }

        public bool Handles(int codepoint) => codepoint == 0xF015;

        public TerminalGlyph? Create(int codepoint, TerminalGlyphRequest request)
        {
            Created++;
            return new TerminalGlyph(new RectangleGeometry(new Rect(0, 0, 2, 2)));
        }
    }
}
