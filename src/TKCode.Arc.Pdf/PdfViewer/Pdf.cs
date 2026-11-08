using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Shows a PDF in a plain <see cref="PageViewer"/>, without the tools of <see cref="PdfViewer"/>: <c>arc:Pdf.Source="report.pdf"</c>.
/// The document opens while the viewer is in a tree and is closed when the source changes or the viewer leaves. There is no
/// password form and no failure cover; failures, a needed password included, are in <c>Pdf.Error</c>.
/// </summary>
public static class Pdf
{
    /// <summary>The PDF to show. Sets the viewer's <see cref="PageViewer.PageSource"/>.</summary>
    public static readonly AttachedProperty<PdfSource?> SourceProperty =
        AvaloniaProperty.RegisterAttached<PageViewer, PdfSource?>("Source", typeof(Pdf));

    /// <summary>Why the document of <c>Pdf.Source</c> could not be opened, a needed password included; null otherwise.</summary>
    public static readonly AttachedProperty<PdfException?> ErrorProperty =
        AvaloniaProperty.RegisterAttached<PageViewer, PdfException?>("Error", typeof(Pdf));

    private static readonly AttachedProperty<PdfAttachedOpener?> s_openerProperty =
        AvaloniaProperty.RegisterAttached<PageViewer, PdfAttachedOpener?>("Opener", typeof(Pdf));

    static Pdf()
    {
        SourceProperty.Changed.AddClassHandler<PageViewer>(static (viewer, change) =>
        {
            var opener = viewer.GetValue(s_openerProperty);
            if (opener is null && change.NewValue is not null)
            {
                opener = new PdfAttachedOpener(viewer);
                viewer.SetValue(s_openerProperty, opener);
            }
            opener?.Reopen();
        });
    }

    /// <summary>The PDF a viewer shows.</summary>
    public static PdfSource? GetSource(PageViewer viewer) => viewer.GetValue(SourceProperty);

    /// <summary>Sets the PDF a viewer shows.</summary>
    public static void SetSource(PageViewer viewer, PdfSource? value) => viewer.SetValue(SourceProperty, value);

    /// <summary>Why a viewer's document could not be opened; null otherwise.</summary>
    public static PdfException? GetError(PageViewer viewer) => viewer.GetValue(ErrorProperty);

    internal static void SetError(PageViewer viewer, PdfException? value) => viewer.SetValue(ErrorProperty, value);
}

/// <summary>Opens and closes the document of <see cref="Pdf.SourceProperty"/> for one viewer.</summary>
internal sealed class PdfAttachedOpener
{
    private readonly PageViewer _viewer;
    private CancellationTokenSource? _opening;
    private PdfDocument? _document;
    private int _session;
    private bool _leaving;
    private int _leavingSession;

    public PdfAttachedOpener(PageViewer viewer)
    {
        _viewer = viewer;
        viewer.AttachedToVisualTree += (_, _) => OnAttached();
        viewer.DetachedFromVisualTree += (_, _) => OnDetached();
    }

    /// <summary>Back before the leaving took effect (a parent moved the viewer in one step), it just goes on.</summary>
    private void OnAttached()
    {
        var moved = _leaving && _session == _leavingSession;
        _leaving = false;
        if (!moved)
        {
            Reopen();
        }
    }

    private void OnDetached()
    {
        _leaving = true;
        _leavingSession = _session;
        Dispatcher.UIThread.Post(FinishLeaving, DispatcherPriority.Background);
    }

    private void FinishLeaving()
    {
        if (_leaving && _session == _leavingSession)
        {
            _leaving = false;
            Close();
        }
    }

    public void Reopen()
    {
        Close();
        if (TopLevel.GetTopLevel(_viewer) is not null && Pdf.GetSource(_viewer) is { } source)
        {
            _ = OpenAsync(source);
        }
    }

    private void Close()
    {
        _session++;
        _opening?.Cancel();
        _opening?.Dispose();
        _opening = null;
        Pdf.SetError(_viewer, null);
        var document = _document;
        _document = null;
        if (document is not null)
        {
            _viewer.SetCurrentValue(PageViewer.PageSourceProperty, null);
            document.Dispose();
        }
    }

    private async Task OpenAsync(PdfSource source)
    {
        var session = _session;
        _opening = new CancellationTokenSource();
        var token = _opening.Token;
        try
        {
            var document = await ArcPdf.Backend.OpenAsync(source, null, token);
            if (session != _session)
            {
                document.Dispose();
                return;
            }
            _document = document;
            _viewer.SetCurrentValue(PageViewer.CurrentPageIndexProperty, 0);
            _viewer.SetCurrentValue(PageViewer.PageSourceProperty, document);
        }
        catch (Exception e)
        {
            if (session == _session)
            {
                Pdf.SetError(_viewer, e as PdfException ?? new PdfException(PdfErrorKind.Corrupt, null, e));
            }
        }
    }
}
