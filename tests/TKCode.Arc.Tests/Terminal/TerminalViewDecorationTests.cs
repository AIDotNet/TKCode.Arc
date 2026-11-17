using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Shouldly;
using TKCode.Terminal.Avalonia;
using TKCode.Terminal.Core;
using static TKCode.Arc.Tests.Terminal.TerminalTestKit;
using Kind = TKCode.Terminal.Avalonia.TerminalView.DecorationKind;

namespace TKCode.Arc.Tests.Terminal;

/// <summary>Issue #140: underline styles and colors, strikethrough and overline.</summary>
public class TerminalViewDecorationTests
{
    private static ResolvedStyle Underlined(UnderlineStyle underline, TerminalColor? color = null) =>
        ResolvedStyle.Default with { Underline = underline, UnderlineColor = color ?? TerminalColor.Default };

    private static ResolvedStyle Flags(StyleFlags flags) => ResolvedStyle.Default with { Flags = flags };

    [AvaloniaFact]
    public void Each_Style_Becomes_One_Span_Per_Contiguous_Run()
    {
        // Two cells of each underline style, then strikethrough and overline.
        ResolvedStyle[] styles =
        [
            ResolvedStyle.Default, Underlined(UnderlineStyle.Single), Underlined(UnderlineStyle.Double), Underlined(UnderlineStyle.Curly),
            Underlined(UnderlineStyle.Dotted), Underlined(UnderlineStyle.Dashed), Flags(StyleFlags.Strikethrough), Flags(StyleFlags.Overline),
        ];
        var cells = Enumerable.Range(0, 14).Select(i => Cell('a' + i, style: 1 + i / 2)).ToArray();
        var row = Row(cells, styles);
        With(Frame(row), view =>
        {
            var decorations = view.DecorationsOf(row);
            decorations.Select(d => (d.Kind, d.StartColumn, d.EndColumn)).ShouldBe(
            [
                (Kind.Underline, 0, 2), (Kind.DoubleUnderline, 2, 4), (Kind.CurlyUnderline, 4, 6), (Kind.DottedUnderline, 6, 8),
                (Kind.DashedUnderline, 8, 10), (Kind.Overline, 12, 14), (Kind.Strikethrough, 10, 12),
            ]);
        });
    }

    [AvaloniaFact]
    public void Underline_Color_Comes_From_Sgr_58_Otherwise_From_The_Foreground()
    {
        ResolvedStyle[] styles =
        [
            ResolvedStyle.Default,
            Underlined(UnderlineStyle.Curly, TerminalColor.Rgb(255, 0, 0)),
            Underlined(UnderlineStyle.Curly) with { Foreground = TerminalColor.Rgb(0, 255, 0) },
        ];
        var row = Row([Cell('a', style: 1), Cell('b', style: 1), Cell('c', style: 2)], styles);
        With(Frame(row), view =>
        {
            var decorations = view.DecorationsOf(row);
            decorations.Count.ShouldBe(2);
            decorations[0].Brush.Color.ShouldBe(Color.FromRgb(255, 0, 0));
            decorations[1].Brush.Color.ShouldBe(Color.FromRgb(0, 255, 0));
        });
    }

    [AvaloniaFact]
    public void A_Curly_Underline_Split_By_A_Color_Change_Continues_Across_The_Spans()
    {
        ResolvedStyle[] styles = [ResolvedStyle.Default, Underlined(UnderlineStyle.Curly, TerminalColor.Rgb(255, 0, 0)), Underlined(UnderlineStyle.Curly)];
        var row = Row([Cell('a', style: 1), Cell('b', style: 1), Cell('c', style: 1), Cell('d', style: 2), Cell('e', style: 2)], styles);
        With(Frame(row), view =>
        {
            var decorations = view.DecorationsOf(row);
            decorations.Count.ShouldBe(2);
            decorations[0].End.X.ShouldBe(decorations[1].Start.X, 1e-9);
            decorations[0].End.Y.ShouldBe(decorations[1].Start.Y, 1e-9);
            decorations[0].Stroke.ShouldBeGreaterThan(0);
        });
    }

    [AvaloniaFact]
    public void Blank_Cells_Are_Underlined_And_Invisible_Ones_Are_Not()
    {
        ResolvedStyle[] styles = [ResolvedStyle.Default, Underlined(UnderlineStyle.Single), Underlined(UnderlineStyle.Single) with { Flags = StyleFlags.Invisible }];
        var row = Row([Cell(' ', style: 1), Cell(0, style: 1), Cell('x', style: 2)], styles);
        With(Frame(row), view =>
        {
            var underline = view.DecorationsOf(row).ShouldHaveSingleItem();
            (underline.StartColumn, underline.EndColumn).ShouldBe((0, 2));
        });
    }

    [AvaloniaFact]
    public void Decorations_Stay_Inside_The_Cell()
    {
        var row = Row([Cell('a', style: 1), Cell('b', style: 2), Cell('c', style: 3)],
            [ResolvedStyle.Default, Underlined(UnderlineStyle.Single), Underlined(UnderlineStyle.Double), Underlined(UnderlineStyle.Curly)]);
        With(Frame(row), view =>
        {
            var m = view.Metrics;
            (m.UnderlinePosition + m.UnderlineThickness).ShouldBeLessThanOrEqualTo(m.CellHeight);
            (m.StrikethroughPosition + m.StrikethroughThickness).ShouldBeLessThanOrEqualTo(m.CellHeight);
            var cellHeight = m.CellHeight / m.Scaling;
            foreach (var d in view.DecorationsOf(row))
            {
                d.Bounds.Top.ShouldBeGreaterThanOrEqualTo(0);
                d.Bounds.Bottom.ShouldBeLessThanOrEqualTo(cellHeight + 1e-9);
            }
        });
    }

    [AvaloniaFact]
    public void Strikethrough_Is_Drawn_Above_The_Text_And_Overline_At_The_Top()
    {
        var row = Row([Cell('a', style: 1), Cell('b', style: 2)], [ResolvedStyle.Default, Flags(StyleFlags.Strikethrough), Flags(StyleFlags.Overline)]);
        With(Frame(row), view =>
        {
            var m = view.Metrics;
            var decorations = view.DecorationsOf(row);
            var overline = decorations.Single(d => d.Kind == Kind.Overline);
            overline.Bounds.Top.ShouldBe(0);
            var strike = decorations.Single(d => d.Kind == Kind.Strikethrough);
            strike.Bounds.Top.ShouldBe(m.StrikethroughPosition / m.Scaling, 1e-9);
            strike.Bounds.Top.ShouldBeLessThan(m.BaselineY / m.Scaling);
        });
    }

    [AvaloniaFact]
    public void Cell_Metrics_Keep_Their_Constructor_And_Default_The_New_Members()
    {
        // The positional constructor shipped in 1.x; the new members are init-only with fallbacks.
        var m = new CellMetrics(8, 16, 12, 1, 1.0, 13);
        m.UnderlinePosition.ShouldBe(13);
        m.StrikethroughThickness.ShouldBe(1);
        m.BoxThickness.ShouldBe(1);
        (m.StrikethroughPosition < m.BaselineY).ShouldBeTrue();
        var (w, h, _, _, _, _) = m;
        (w, h).ShouldBe((8, 16));
    }

    [AvaloniaFact]
    public void A_Hovered_Link_Is_Underlined_Without_Rebuilding_The_Row()
    {
        var row = Text("https://x");
        With(Frame(row), view =>
        {
            var builds = view.RowRunBuilds;
            view.HoveredLink = [new CellSpan(0, 0, 8)];
            view.LinkForeground = Colors.DeepSkyBlue;
            Paint();
            view.RowRunBuilds.ShouldBe(builds);
        });
    }
}
