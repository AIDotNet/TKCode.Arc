using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace TKCode.Arc.Tests.Pdf;

/// <summary>#175: a view model that keeps the document as a path, an address or bytes binds to <see cref="PdfViewer.Source"/>.</summary>
public class PdfViewerBindingTests
{
    private sealed class ViewModel : INotifyPropertyChanged
    {
        private object? _document;

        public object? Document
        {
            get => _document;
            set
            {
                _document = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Document)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>Binds Source to a view model holding <paramref name="first"/>, then changes it to <paramref name="second"/>.</summary>
    private static (PdfSource? First, PdfSource? Second) BindAndChange(FakePdfBackend backend, object? first, object? second)
    {
        var model = new ViewModel { Document = first };
        var viewer = new PdfViewer { Width = 900, Height = 600, DataContext = model };
        viewer.Bind(PdfViewer.SourceProperty, new Binding(nameof(ViewModel.Document)));
        var window = TestHelpers.Show(viewer);
        try
        {
            TestHelpers.PumpUntil(() => viewer.Status != PdfViewerStatus.Loading).ShouldBeTrue();
            var before = viewer.Source;
            model.Document = second;
            TestHelpers.PumpUntil(() => viewer.Status != PdfViewerStatus.Loading).ShouldBeTrue();
            return (before, viewer.Source);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void A_Bound_Path_Opens_And_Follows_The_View_Model()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var (first, second) = BindAndChange(backend, "docs/a.pdf", "docs/b.pdf");
        first!.FilePath.ShouldBe("docs/a.pdf");
        second!.FilePath.ShouldBe("docs/b.pdf");
        backend.Calls.Select(c => c.Source.FilePath).ShouldBe(["docs/a.pdf", "docs/b.pdf"]);
    }

    [AvaloniaFact]
    public void A_Bound_Uri_Opens()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var (_, second) = BindAndChange(backend, null, new Uri("https://example.com/report.pdf"));
        second!.Kind.ShouldBe(PdfSourceKind.Uri);
        backend.Calls.Single().Source.Uri!.AbsolutePath.ShouldBe("/report.pdf");
    }

    [AvaloniaFact]
    public void Bound_Bytes_Open()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var (first, second) = BindAndChange(backend, new byte[] { 1, 2 }, new byte[] { 3 });
        first!.Bytes.ToArray().ShouldBe(new byte[] { 1, 2 });
        second!.Bytes.ToArray().ShouldBe(new byte[] { 3 });
        backend.Calls.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_Bound_Empty_Path_Shows_Nothing()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var (first, second) = BindAndChange(backend, "a.pdf", "");
        first.ShouldNotBeNull();
        second.ShouldBeNull();
        backend.Calls.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Bound_PdfSource_Still_Opens()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var (_, second) = BindAndChange(backend, PdfSource.FromFile("a.pdf"), PdfSource.FromFile("b.pdf"));
        second!.FilePath.ShouldBe("b.pdf");
        backend.Calls.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_Bound_Path_Opens_Through_Pdf_Source_Too()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var model = new ViewModel { Document = "a.pdf" };
        var viewer = new PageViewer { Width = 800, Height = 600, DataContext = model };
        viewer.Bind(global::TKCode.Arc.Pdf.SourceProperty, new Binding(nameof(ViewModel.Document)));
        var window = TestHelpers.Show(viewer);
        try
        {
            TestHelpers.PumpUntil(() => viewer.PageCount > 0).ShouldBeTrue();
            model.Document = "b.pdf";
            TestHelpers.PumpUntil(() => backend.Documents.Count == 2 && viewer.PageSource == backend.Documents[1]).ShouldBeTrue();
            backend.Calls.Select(c => c.Source.FilePath).ShouldBe(["a.pdf", "b.pdf"]);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void The_Converter_Reads_Paths_Addresses_And_Bytes()
    {
        var converter = TypeDescriptor.GetConverter(typeof(PdfSource));
        converter.ShouldBeOfType<PdfSourceConverter>();
        converter.CanConvertFrom(typeof(string)).ShouldBeTrue();
        converter.CanConvertFrom(typeof(Uri)).ShouldBeTrue();
        converter.CanConvertFrom(typeof(byte[])).ShouldBeTrue();
        converter.CanConvertFrom(typeof(ReadOnlyMemory<byte>)).ShouldBeTrue();
        converter.CanConvertFrom(typeof(PdfDocument)).ShouldBeFalse();
        ((PdfSource)converter.ConvertFrom("avares://App/a.pdf")!).Kind.ShouldBe(PdfSourceKind.Uri);
        ((PdfSource)converter.ConvertFrom(new Uri("https://example.com/a.pdf"))!).Kind.ShouldBe(PdfSourceKind.Uri);
        ((PdfSource)converter.ConvertFrom(new ReadOnlyMemory<byte>([7]))!).Kind.ShouldBe(PdfSourceKind.Bytes);
        converter.ConvertFrom("  ").ShouldBeNull();
    }
}
