using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiSourceMark"/>: the three sizes, what each kind of mark draws (geometry, image, the first-letter fallback,
/// nothing), the fallback letter rules, and that the mark is decorative (no hit testing, focus or automation).
/// </summary>
public class AiSourceMarkTests
{
    [AvaloniaFact]
    public void Default_Size_Is_14_And_It_Measures_Its_Size_Square()
    {
        var mark = new AiSourceMark();
        mark.Size.ShouldBe(14);
        foreach (var size in new[] { 12.0, 14.0, 16.0 })
        {
            mark.Size = size;
            mark.Measure(Size.Infinity);
            mark.DesiredSize.ShouldBe(new Size(size, size));
        }
    }

    [AvaloniaFact]
    public void A_Geometry_Mark_Is_Drawn_As_Given()
    {
        var mark = new AiSourceMark { Source = AiSourcesSamples.Make(1, "Running two pipelines", "docs.pipewright.dev", AiSourcesSamples.Circle, "#2F6FEB") };
        mark.Kind.ShouldBe(AiSourceMarkKind.Geometry);
    }

    [AvaloniaFact]
    public void An_Image_Mark_Is_Drawn_As_Given()
    {
        using var bitmap = new WriteableBitmap(new PixelSize(32, 16), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        var mark = new AiSourceMark { Source = AiSourcesSamples.Make(1, "Runner images", "wiki.brightmill.net") };
        mark.Source!.Mark = bitmap;
        mark.Kind.ShouldBe(AiSourceMarkKind.Image);
    }

    [AvaloniaFact]
    public void Without_A_Mark_It_Falls_Back_To_The_First_Letter()
    {
        var source = AiSourcesSamples.Make(8, "Build provenance basics", "openbuildnotes.org");
        var mark = new AiSourceMark { Source = source };
        mark.Kind.ShouldBe(AiSourceMarkKind.Letter);
        AiSourceMark.FallbackLetter(source).ShouldBe("O");
        // Something that is neither a geometry nor an image is no mark either.
        source.Mark = "favicon.png";
        mark.Kind.ShouldBe(AiSourceMarkKind.Letter);
    }

    [AvaloniaFact]
    public void Nothing_Is_Drawn_Without_A_Source_Or_Any_Text()
    {
        new AiSourceMark().Kind.ShouldBe(AiSourceMarkKind.None);
        new AiSourceMark { Source = new AiSource { Title = "  " } }.Kind.ShouldBe(AiSourceMarkKind.None);
    }

    [AvaloniaTheory]
    [InlineData("docs.pipewright.dev", "Running two pipelines", "D")]
    [InlineData("www.zednotes.io", "Zed", "Z")]
    [InlineData("WWW.Example.org", "Example", "E")]
    [InlineData("  quarry.dev ", "Required checks", "Q")]
    [InlineData(null, "build provenance basics", "B")]
    [InlineData("", "lanternworks", "L")]
    [InlineData("ñandu.org", "Ñandu", "Ñ")]
    [InlineData("www.", "Fallback", "W")]
    public void The_Fallback_Letter_Is_The_Domain_Initial_Then_The_Title_Initial(string? domain, string title, string expected)
    {
        AiSourceMark.FallbackLetter(AiSourcesSamples.Make(1, title, domain)).ShouldBe(expected);
    }

    [AvaloniaFact]
    public void The_Fallback_Letter_Keeps_A_Whole_Grapheme()
    {
        // A flag is two code points; the chip shows it whole.
        AiSourceMark.FallbackLetter(new AiSource { Title = "\U0001F1EB\U0001F1F7 notes" }).ShouldBe("\U0001F1EB\U0001F1F7");
        AiSourceMark.FallbackLetter(null).ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Change_Of_Mark_On_A_Shown_Source_Changes_What_Is_Drawn()
    {
        var source = AiSourcesSamples.Make(2, "Caching dependencies", "eng.northgate.io");
        var mark = new AiSourceMark { Source = source, Size = 16 };
        var window = TestHelpers.Show(mark);
        mark.Kind.ShouldBe(AiSourceMarkKind.Letter);
        source.Mark = AiSourcesSamples.Square;
        source.MarkBrush = Brushes.OrangeRed;
        mark.Kind.ShouldBe(AiSourceMarkKind.Geometry);
        mark.Bounds.Size.ShouldBe(new Size(16, 16));
        window.Close();
    }

    [AvaloniaFact]
    public void It_Is_Decorative()
    {
        var mark = new AiSourceMark { Source = AiSourcesSamples.Make(1, "Running two pipelines", "docs.pipewright.dev") };
        var window = TestHelpers.Show(mark);
        mark.IsHitTestVisible.ShouldBeFalse();
        mark.Focusable.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(mark).ShouldBeOfType<NoneAutomationPeer>();
        window.Close();
    }
}
