using System.Collections.Concurrent;
using System.Text;
using Avalonia;
using Shouldly;

namespace TKCode.Arc.Tests.Pdf;

// The parts of the browser backend that need no browser (src/TKCode.Arc.Pdf.Browser/PdfJs, compiled into this project). What pdf.js
// itself does, and pixels, are only covered by running it in a browser (design/components/pdf/pdf.md section 14).
public class PdfJsConversionTests
{
    private const double Dip = 96.0 / 72.0;

    // What pdf.js gives for a page of 600 x 800 points: y flipped, and turned by /Rotate.
    private static readonly double[] s_upright = [1, 0, 0, -1, 0, 800];
    private static readonly double[] s_turned90 = [0, 1, 1, 0, 0, 0];
    private static readonly double[] s_turned180 = [-1, 0, 0, 1, 600, 0];
    private static readonly double[] s_turned270 = [0, -1, -1, 0, 800, 600];
    private static readonly double[] s_boxNotAtOrigin = [1, 0, 0, -1, -100, 800];

    private static double[] Item(string text, double x, double y, double width, double fontSize = 10, int flags = 0, double ascent = 0.8, double descent = -0.2) =>
        [text.Length, fontSize, 0, 0, fontSize, x, y, width, fontSize, ascent, descent, flags];

    private static PdfPageText Text(double[] view, params (string Text, double[] Numbers)[] items) =>
        PdfJsText.Build(string.Concat(items.Select(i => i.Text)), items.SelectMany(i => i.Numbers).ToArray(), view);

    private static void ShouldBeNear(Rect actual, Rect expected)
    {
        actual.X.ShouldBe(expected.X, 0.001);
        actual.Y.ShouldBe(expected.Y, 0.001);
        actual.Width.ShouldBe(expected.Width, 0.001);
        actual.Height.ShouldBe(expected.Height, 0.001);
    }

    #region Text

    [Fact]
    public void Boxes_Have_A_Top_Left_Origin_In_Page_DIPs_And_Use_The_Fonts_Ascent_And_Descent()
    {
        var text = Text(s_upright, ("II", Item("II", 100, 700, 20)));
        text.Text.ShouldBe("II");
        ShouldBeNear(text.CharBoxes[0], new Rect(100 * Dip, (800 - 708) * Dip, 10 * Dip, 10 * Dip));
        ShouldBeNear(text.CharBoxes[1], new Rect(110 * Dip, (800 - 708) * Dip, 10 * Dip, 10 * Dip));
    }

    [Fact]
    public void An_Item_Is_Split_By_Rough_Width_So_Wide_Characters_Get_More()
    {
        var text = Text(s_upright, ("A中", Item("A中", 0, 700, 100)));
        var (latin, chinese) = (text.CharBoxes[0], text.CharBoxes[1]);
        chinese.Width.ShouldBeGreaterThan(latin.Width);
        (latin.Width + chinese.Width).ShouldBe(100 * Dip, 0.001);
        chinese.X.ShouldBe(latin.Right, 0.001);
    }

    [Fact]
    public void A_Mark_Takes_No_Width_And_A_Surrogate_Pair_Shares_One_Box()
    {
        var text = Text(s_upright, ("é\U0001F600", Item("é\U0001F600", 0, 700, 30)));
        text.Text.Length.ShouldBe(4);
        text.CharBoxes[1].Width.ShouldBe(0);
        text.CharBoxes[2].ShouldBe(text.CharBoxes[3]);
        text.CharBoxes[2].Width.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Right_To_Left_Items_Start_At_Their_Right_End()
    {
        var text = Text(s_upright, ("אב", Item("אב", 100, 700, 20, flags: PdfJsText.FlagRightToLeft)));
        text.CharBoxes[0].X.ShouldBeGreaterThan(text.CharBoxes[1].X);
        text.CharBoxes[1].X.ShouldBe(100 * Dip, 0.001);
        text.CharBoxes[0].Right.ShouldBe(120 * Dip, 0.001);
    }

    [Fact]
    public void Vertical_Text_Runs_Down_The_Page_With_The_Advance_In_The_Height()
    {
        // pdf.js: width is the font size and height the advance for a vertical font.
        double[] numbers = [2, 10, 0, 0, 10, 300, 700, 10, 40, 0.8, -0.2, PdfJsText.FlagVertical];
        var text = PdfJsText.Build("中文", numbers, s_upright);
        text.CharBoxes[0].Height.ShouldBe(20 * Dip, 0.001);
        text.CharBoxes[1].Y.ShouldBe(text.CharBoxes[0].Bottom, 0.001);
        text.CharBoxes[0].Y.ShouldBe(100 * Dip, 0.001);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Text_On_A_Turned_Page_Lands_Where_The_View_Says(int rotation)
    {
        var view = rotation switch { 90 => s_turned90, 180 => s_turned180, _ => s_turned270 };
        var box = Text(view, ("I", Item("I", 100, 700, 10))).CharBoxes[0];
        // The corners of the box in page space, through the view by hand.
        Point Through(double x, double y) => new((view[0] * x + view[2] * y + view[4]) * Dip, (view[1] * x + view[3] * y + view[5]) * Dip);
        var (a, b) = (Through(100, 698), Through(110, 708));
        ShouldBeNear(box, new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)));
    }

    [Fact]
    public void A_Line_End_Becomes_A_Break_With_An_Empty_Box_And_Empty_Items_Count()
    {
        var text = Text(s_upright,
            ("ab", Item("ab", 0, 700, 10, flags: PdfJsText.FlagEndOfLine)),
            ("", Item("", 0, 0, 0, flags: PdfJsText.FlagEndOfLine)),
            ("cd", Item("cd", 0, 680, 10)));
        text.Text.ShouldBe("ab\n\ncd");
        text.CharBoxes[2].ShouldBe(default);
        text.CharBoxes.Count.ShouldBe(text.Text.Length);
    }

    [Fact]
    public void Items_Elsewhere_Are_Kept_Apart_But_Neighbours_Are_Not()
    {
        Text(s_upright, ("ab", Item("ab", 0, 700, 10)), ("cd", Item("cd", 10, 700, 10))).Text.ShouldBe("abcd");
        // A column gutter on the same line, and a new line pdf.js did not mark.
        Text(s_upright, ("ab", Item("ab", 0, 700, 10)), ("cd", Item("cd", 200, 700, 10))).Text.ShouldBe("ab cd");
        Text(s_upright, ("ab", Item("ab", 0, 700, 10)), ("cd", Item("cd", 0, 680, 10))).Text.ShouldBe("ab\ncd");
        // White space there already needs no more.
        Text(s_upright, ("ab ", Item("ab ", 0, 700, 10)), ("cd", Item("cd", 200, 700, 10))).Text.ShouldBe("ab cd");
    }

    [Fact]
    public void Lone_Surrogates_And_Nulls_Do_Not_Reach_The_Rest_Of_The_Code()
    {
        var text = Text(s_upright, ("a\uD800b\0", Item("a\uD800b\0", 0, 700, 40)));
        text.Text.ShouldBe("a�b�");
        text.CharBoxes.Count.ShouldBe(4);
        Should.NotThrow(() => PdfTextNormalizer.Normalize(text.Text, false));
    }

    [Fact]
    public void Degenerate_Items_Give_Empty_Boxes_And_Wrong_Lengths_Do_Not_Throw()
    {
        var text = Text(s_upright, ("ab", Item("ab", 0, 700, 0)));
        text.CharBoxes[0].ShouldBe(default);
        // Numbers that claim more text than there is.
        PdfJsText.Build("ab", Item("abcdefgh", 0, 700, 10), s_upright).Text.ShouldBe("ab");
        PdfJsText.Build("", [], s_upright).HitTest(default).ShouldBe(-1);
    }

    [Fact]
    public void A_Font_That_Says_Nothing_Gets_An_Ordinary_Height()
    {
        var box = Text(s_upright, ("I", Item("I", 0, 700, 5, ascent: 0, descent: 0))).CharBoxes[0];
        box.Height.ShouldBe(10 * Dip, 0.001);
    }

    #endregion

    #region Coordinates

    [Fact]
    public void Points_And_Rectangles_Follow_The_View()
    {
        PdfJsGeometry.Map(s_upright, 100, 700).ShouldBe(new Point(100 * Dip, 100 * Dip));
        PdfJsGeometry.Map(s_boxNotAtOrigin, 100, 800).ShouldBe(new Point(0, 0));
        ShouldBeNear(PdfJsGeometry.MapRect(s_upright, 10, 700, 110, 780), new Rect(10 * Dip, 20 * Dip, 100 * Dip, 80 * Dip));
        ShouldBeNear(PdfJsGeometry.MapRect(s_turned90, 10, 700, 110, 780), new Rect(700 * Dip, 10 * Dip, 80 * Dip, 100 * Dip));
    }

    [Fact]
    public void What_A_Destination_Leaves_Out_Is_The_Pages_Edge_Whichever_Way_It_Is_Turned()
    {
        // Only a y: on an upright page that is a height; on a page turned by 90 degrees it runs across.
        PdfJsGeometry.MapDestination(s_upright, double.NaN, 700).ShouldBe(new Point(0, 100 * Dip));
        PdfJsGeometry.MapDestination(s_upright, 50, double.NaN).ShouldBe(new Point(50 * Dip, 0));
        PdfJsGeometry.MapDestination(s_turned90, double.NaN, 700).ShouldBe(new Point(700 * Dip, 0));
        PdfJsGeometry.MapDestination(s_turned90, 50, double.NaN).ShouldBe(new Point(0, 50 * Dip));
    }

    private static double[] Place(double page, double x = double.NaN, double y = double.NaN, double[]? view = null) =>
        [page, x, y, .. view ?? [double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN]];

    [Fact]
    public void Destinations_Stay_In_The_Document()
    {
        PdfJsGeometry.Destination(Place(2), 3).ShouldBe(new PdfDestination(2));
        PdfJsGeometry.Destination(Place(2, 0, 700, s_upright), 3).ShouldBe(new PdfDestination(2, new Point(0, 100 * Dip)));
        PdfJsGeometry.Destination(Place(3), 3).ShouldBeNull();
        PdfJsGeometry.Destination(Place(-1), 3).ShouldBeNull();
        PdfJsGeometry.Destination(Place(double.NaN), 3).ShouldBeNull();
        // A point without a view cannot be converted: the top of the page.
        PdfJsGeometry.Destination(Place(1, 5, 5), 3).ShouldBe(new PdfDestination(1));
    }

    [Fact]
    public void An_Unreadable_Page_Takes_Its_Neighbours_Size_And_Changes_Are_Reported()
    {
        var sizes = new Size[3];
        PdfJsGeometry.FillPageSizes(sizes, 0, [72, 144, double.NaN, double.NaN, 36, 72], new Size(1, 1)).ShouldBeTrue();
        sizes[0].ShouldBe(new Size(96, 192));
        sizes[1].ShouldBe(new Size(96, 192));
        sizes[2].ShouldBe(new Size(48, 96));
        PdfJsGeometry.FillPageSizes(sizes, 2, [36, 72], new Size(1, 1)).ShouldBeFalse();
        // The first page unreadable; more numbers than pages.
        var first = new Size[1];
        PdfJsGeometry.FillPageSizes(first, 0, [double.NaN, double.NaN, 72, 72], new Size(816, 1056));
        first[0].ShouldBe(new Size(816, 1056));
    }

    [Fact]
    public void A_Render_Is_Clamped_To_The_Page_And_Refused_When_There_Is_Nothing_Or_Too_Much()
    {
        var page = new PixelSize(100, 200);
        PdfJsGeometry.RenderArea(new PageRenderRequest(0, page, null), long.MaxValue).ShouldBe(new PixelRect(0, 0, 100, 200));
        PdfJsGeometry.RenderArea(new PageRenderRequest(0, page, new PixelRect(90, 190, 50, 50)), long.MaxValue).ShouldBe(new PixelRect(90, 190, 10, 10));
        PdfJsGeometry.RenderArea(new PageRenderRequest(0, page, new PixelRect(200, 0, 10, 10)), long.MaxValue).ShouldBeNull();
        PdfJsGeometry.RenderArea(new PageRenderRequest(0, new PixelSize(0, 10), null), long.MaxValue).ShouldBeNull();
        PdfJsGeometry.RenderArea(new PageRenderRequest(0, page, null), 100 * 200 * 4).ShouldNotBeNull();
        PdfJsGeometry.RenderArea(new PageRenderRequest(0, page, null), 100 * 200 * 4 - 1).ShouldBeNull();
    }

    [Fact]
    public void Page_Labels_Fit_The_Document_Whatever_The_File_Says()
    {
        PdfJsGeometry.PageLabels(["i", "", "1"], 3).ShouldBe(["i", null, "1"]);
        PdfJsGeometry.PageLabels(["i"], 2).ShouldBe(["i", null]);
        PdfJsGeometry.PageLabels(["i", "ii", "iii"], 1).ShouldBe(["i"]);
    }

    #endregion

    #region Links

    [Fact]
    public void Links_Are_External_Or_Internal_And_Others_Are_Dropped()
    {
        double[] rect = [100, 700, 200, 720];
        double[] values =
        [
            .. rect, .. Place(-1),
            .. rect, .. Place(1, 0, 700, s_upright),
            .. rect, .. Place(9),
            .. rect, .. Place(-1),
        ];
        var links = PdfJsLinks.Build(["https://example.com/a", "", "", "not a uri"], values, s_upright, 2);
        links.Count.ShouldBe(2);
        links[0].Uri.ShouldBe(new Uri("https://example.com/a"));
        links[0].Destination.ShouldBeNull();
        ShouldBeNear(links[0].Bounds, new Rect(100 * Dip, 80 * Dip, 100 * Dip, 20 * Dip));
        links[1].Uri.ShouldBeNull();
        links[1].Destination.ShouldBe(new PdfDestination(1, new Point(0, 100 * Dip)));
        // Fewer numbers than links.
        PdfJsLinks.Build(["https://example.com/"], [1, 2], s_upright, 1).ShouldBeEmpty();
    }

    #endregion

    #region Outline

    private static double[] Entry(int depth, double page = 0) => [depth, .. Place(page)];

    [Fact]
    public void The_Outline_Tree_Comes_Back_From_Reading_Order()
    {
        string[] titles = ["A", "A1", "A1a", "A2", "B"];
        double[] values = [.. Entry(0), .. Entry(1), .. Entry(2), .. Entry(1), .. Entry(0, -1)];
        var outline = PdfJsOutline.Build(titles, values, 1);
        outline.Select(i => i.Title).ShouldBe(["A", "B"]);
        outline[0].Children.Select(i => i.Title).ShouldBe(["A1", "A2"]);
        outline[0].Children[0].Children.Single().Title.ShouldBe("A1a");
        outline[0].Destination.ShouldBe(new PdfDestination(0));
        // It only groups its children, like an entry whose target does not exist.
        outline[1].Destination.ShouldBeNull();
    }

    [Fact]
    public void The_Outline_Stops_At_The_Same_Limits_As_The_PDFium_Backend()
    {
        PdfJsOutline.MaxNodes.ShouldBe(10_000);
        PdfJsOutline.MaxDepth.ShouldBe(64);

        var deep = Enumerable.Range(0, 100).ToArray();
        var outline = PdfJsOutline.Build(deep.Select(i => $"Level {i}").ToArray(), deep.SelectMany(i => Entry(i)).ToArray(), 1);
        var levels = 0;
        for (var level = outline; level.Count > 0; level = level[0].Children)
        {
            levels++;
        }
        levels.ShouldBe(64);

        var wide = Enumerable.Range(0, 10_050).ToArray();
        var flat = PdfJsOutline.Build(wide.Select(i => $"Entry {i}").ToArray(), wide.SelectMany(_ => Entry(0)).ToArray(), 1);
        flat.Count.ShouldBe(10_000);
        flat[^1].Title.ShouldBe("Entry 9999");
    }

    [Fact]
    public void A_Depth_Skipped_By_A_Hostile_File_Joins_The_Nearest_Level_And_Bad_Input_Is_Harmless()
    {
        var outline = PdfJsOutline.Build(["A", "deep"], [.. Entry(0), .. Entry(30)], 1);
        outline.Single().Children.Single().Title.ShouldBe("deep");
        PdfJsOutline.Build(["A"], [.. Entry(-1)], 1).ShouldBeEmpty();
        PdfJsOutline.Build(["A", "B"], [.. Entry(0)], 1).Count.ShouldBe(1);
        PdfJsOutline.Build([], [], 1).ShouldBeEmpty();
    }

    #endregion

    #region Errors

    [Theory]
    [InlineData("PasswordException", 0, false, null, true, PdfErrorKind.PasswordRequired)]
    [InlineData("InvalidPDFException", 0, false, "Invalid PDF structure.", false, PdfErrorKind.Corrupt)]
    [InlineData("ResponseException", 404, true, null, true, PdfErrorKind.NotFound)]
    [InlineData("ResponseException", 410, false, null, true, PdfErrorKind.NotFound)]
    [InlineData("ResponseException", 0, true, null, true, PdfErrorKind.NotFound)]
    [InlineData("ResponseException", 500, false, null, true, PdfErrorKind.Network)]
    [InlineData("ResponseException", 403, false, null, true, PdfErrorKind.Network)]
    [InlineData("AbortException", 0, false, null, true, PdfErrorKind.Aborted)]
    [InlineData("UnknownErrorException", 0, false, "Failed to fetch TypeError: Failed to fetch", true, PdfErrorKind.Network)]
    [InlineData("UnknownErrorException", 0, false, "NetworkError when attempting to fetch resource.", true, PdfErrorKind.Network)]
    [InlineData("UnknownErrorException", 0, false, "Load failed", true, PdfErrorKind.Network)]
    [InlineData("UnknownErrorException", 0, false, "Failed to fetch", false, PdfErrorKind.Corrupt)]
    [InlineData("UnknownErrorException", 0, false, "Unknown encryption method", false, PdfErrorKind.Unsupported)]
    [InlineData("UnknownErrorException", 0, false, "Something else", true, PdfErrorKind.Corrupt)]
    [InlineData("TypeError", 0, false, null, false, PdfErrorKind.Corrupt)]
    public void Errors_Of_PdfJs_Map_To_Kinds(string name, int status, bool missing, string? detail, bool fromAddress, PdfErrorKind expected) =>
        PdfJsErrors.Map(name, status, missing, detail, fromAddress).ShouldBe(expected);

    #endregion

    #region NFKC without string.Normalize

    [Fact]
    public void Search_Asks_The_Hook_For_NFKC_When_There_Is_One_And_Skips_It_For_Ideographs()
    {
        // string.Normalize has no NFKC in WebAssembly, which took search down with it before the browser backend set this hook.
        var asked = new ConcurrentBag<string>();
        PdfTextNormalizer.CompatibilityForm = text =>
        {
            asked.Add(text);
            return text.Normalize(NormalizationForm.FormKC);
        };
        try
        {
            PdfTextNormalizer.Normalize("ﬁn", matchCase: true).Text.ShouldBe("fin");
            asked.ShouldContain("ﬁ");
            PdfTextNormalizer.Normalize("灯塔한", matchCase: false).Text.ShouldBe("灯塔한");
            asked.ShouldNotContain("灯");
            asked.ShouldNotContain("한");
        }
        finally
        {
            PdfTextNormalizer.CompatibilityForm = null;
        }
    }

    #endregion
}
