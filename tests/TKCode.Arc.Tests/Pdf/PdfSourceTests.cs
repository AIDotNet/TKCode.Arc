using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Pdf;

public class PdfSourceTests
{
    [Fact]
    public void Parse_Reads_Addresses()
    {
        var source = PdfSource.Parse("https://example.com/a%20b.pdf");
        source.Kind.ShouldBe(PdfSourceKind.Uri);
        source.Uri!.Host.ShouldBe("example.com");
        source.DisplayName.ShouldBe("a b.pdf");
        PdfSource.Parse("avares://App/Assets/x.pdf").Kind.ShouldBe(PdfSourceKind.Uri);
    }

    [Fact]
    public void Parse_Reads_Paths_And_File_Uris_As_Files()
    {
        var path = PdfSource.Parse("docs/report.pdf");
        path.Kind.ShouldBe(PdfSourceKind.File);
        path.FilePath.ShouldBe("docs/report.pdf");
        path.DisplayName.ShouldBe("report.pdf");
        var uri = new Uri(Path.Combine(Path.GetTempPath(), "x.pdf"));
        PdfSource.Parse(uri.ToString()).FilePath.ShouldBe(uri.LocalPath);
    }

    [Fact]
    public void Parse_Does_Not_Treat_Other_Schemes_As_Addresses()
    {
        var source = PdfSource.Parse("javascript:alert(1)");
        source.Kind.ShouldBe(PdfSourceKind.File);
        source.Uri.ShouldBeNull();
    }

    [Fact]
    public void FromUri_Refuses_Other_Schemes_And_Relative_Uris()
    {
        Should.Throw<ArgumentException>(() => PdfSource.FromUri(new Uri("ftp://example.com/a.pdf")));
        Should.Throw<ArgumentException>(() => PdfSource.FromUri(new Uri("a.pdf", UriKind.Relative)));
    }

    [Fact]
    public void Bytes_And_Streams_Keep_What_They_Were_Given()
    {
        byte[] data = [1, 2, 3];
        PdfSource.FromBytes(data).Bytes.ToArray().ShouldBe(data);
        using var stream = new MemoryStream();
        PdfSource.FromStream(() => stream).OpenStream!().ShouldBeSameAs(stream);
    }

    [Fact]
    public void WithDisplayName_Copies_The_Rest()
    {
        var source = PdfSource.FromFile("a.pdf").WithDisplayName("Report");
        source.DisplayName.ShouldBe("Report");
        source.FilePath.ShouldBe("a.pdf");
    }

    [AvaloniaFact]
    public void Xaml_Takes_A_String()
    {
        var viewer = AvaloniaRuntimeXamlLoader.Parse<PdfViewer>(
            "<arc:PdfViewer xmlns='https://github.com/avaloniaui' xmlns:arc='https://github.com/AIDotNet/TKCode.Arc' Source='https://example.com/x.pdf' />",
            typeof(PdfViewer).Assembly);
        viewer.Source!.Kind.ShouldBe(PdfSourceKind.Uri);
    }

    [AvaloniaFact]
    public void Xaml_Takes_The_Attached_Source_On_A_PageViewer()
    {
        var viewer = AvaloniaRuntimeXamlLoader.Parse<PageViewer>(
            "<arc:PageViewer xmlns='https://github.com/avaloniaui' xmlns:arc='https://github.com/AIDotNet/TKCode.Arc' arc:Pdf.Source='https://example.com/x.pdf' />",
            typeof(PdfViewer).Assembly);
        global::TKCode.Arc.Pdf.GetSource(viewer)!.Kind.ShouldBe(PdfSourceKind.Uri);
    }
}
