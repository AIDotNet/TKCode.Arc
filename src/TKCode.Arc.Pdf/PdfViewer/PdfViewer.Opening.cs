using Avalonia.Threading;

namespace TKCode.Arc;

public partial class PdfViewer
{
    /// <summary>How many wrong passwords in a row a <see cref="PasswordRequested"/> handler may give before the built-in form takes over (a handler that repeats itself must not spin forever).</summary>
    private const int MaxHandlerAttempts = 5;

    private CancellationTokenSource? _opening;
    private int _session;
    private PdfDocument? _ownedDocument;
    private TaskCompletionSource<string?>? _formAnswer;
    private bool _leaving;
    private int _leavingSession;

    /// <summary>Starts over from <see cref="Source"/> and <see cref="Document"/>: stops what is opening, closes what is shown.</summary>
    private void Reopen()
    {
        StopOpening();
        CloseDocument();
        if (Source is { } source)
        {
            if (VisualRoot is null)
            {
                SetEmpty();
                return;
            }
            _ = OpenAsync(source);
        }
        else if (Document is { } document)
        {
            Show(document, owned: false, null);
        }
        else
        {
            SetEmpty();
        }
    }

    private void StopOpening()
    {
        _session++;
        _formAnswer = null;
        _opening?.Cancel();
        _opening?.Dispose();
        _opening = null;
    }

    /// <summary>
    /// Opens a source, going through the password flow as often as needed. A superseded run notices by its session: whatever it
    /// still receives is thrown away, a document included (a backend may finish in spite of the cancellation). The password
    /// lives only in a local variable from the moment it is given until the backend has answered.
    /// </summary>
    private async Task OpenAsync(PdfSource source)
    {
        var session = _session;
        _opening = new CancellationTokenSource();
        var token = _opening.Token;
        SetState(PdfViewerStatus.Loading, null);
        string? password = null;
        var handlerAttempts = 0;
        while (true)
        {
            try
            {
                var document = await ArcPdf.Backend.OpenAsync(source, password, token);
                password = null;
                if (session != _session)
                {
                    document.Dispose();
                    return;
                }
                Show(document, owned: true, source);
                return;
            }
            catch (PdfException e) when (e.Kind == PdfErrorKind.PasswordRequired && session == _session)
            {
                var isRetry = password is not null;
                password = null;
                try
                {
                    password = await AskPasswordAsync(isRetry, handlerAttempts, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                if (session != _session)
                {
                    return;
                }
                if (password is null)
                {
                    Fail(new PdfException(PdfErrorKind.PasswordRequired));
                    return;
                }
                handlerAttempts = _formAnswer is null ? handlerAttempts + 1 : 0;
                _formAnswer = null;
            }
            catch (Exception e)
            {
                password = null;
                if (session != _session)
                {
                    return;
                }
                Fail(Classify(e, token));
                return;
            }
        }
    }

    /// <summary>The next password: from a handler, or from the built-in form. Null gives up.</summary>
    private async Task<string?> AskPasswordAsync(bool isRetry, int handlerAttempts, CancellationToken token)
    {
        SetState(PdfViewerStatus.PasswordRequired, null);
        var form = _stateLayer?.Form;
        if (form is null)
        {
            // A cover without the form while a handler may still answer.
            EnsureStateLayer().ShowLoading();
        }
        var args = new PdfPasswordEventArgs(PasswordRequestedEvent, isRetry);
        RaiseEvent(args);
        await args.WhenAnswered.WaitAsync(token);
        if (args.Cancel)
        {
            args.TakePassword();
            return null;
        }
        if (args.TakePassword() is { } answer && handlerAttempts < MaxHandlerAttempts)
        {
            return answer;
        }
        _formAnswer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        EnsureStateLayer().ShowPasswordForm(isRetry);
        return await _formAnswer.Task.WaitAsync(token);
    }

    /// <summary>The form's Open button or Enter.</summary>
    internal void OnFormSubmitted(string password) => _formAnswer?.TrySetResult(password);

    /// <summary>The form's Cancel button or Escape.</summary>
    internal void OnFormCancelled() => _formAnswer?.TrySetResult(null);

    private static PdfException Classify(Exception e, CancellationToken token) => e switch
    {
        PdfException pdf => pdf,
        // Not ours: the caller would have moved on to another session.
        OperationCanceledException when !token.IsCancellationRequested => new PdfException(PdfErrorKind.Aborted, null, e),
        FileNotFoundException or DirectoryNotFoundException => new PdfException(PdfErrorKind.NotFound, null, e),
        System.Net.Http.HttpRequestException => new PdfException(PdfErrorKind.Network, null, e),
        _ => new PdfException(PdfErrorKind.Corrupt, null, e)
    };

    private void Show(PdfDocument document, bool owned, PdfSource? source)
    {
        _ownedDocument = owned ? document : null;
        OpenedDocument = document;
        Title = document.Info.Title is { Length: > 0 } title ? title : source?.DisplayName;
        SetCurrentValue(CurrentPageIndexProperty, 0);
        SetCurrentValue(PageSourceProperty, document);
        SetState(PdfViewerStatus.Ready, null);
        _ = LoadOutlineAsync(document);
        if (Avalonia.Automation.Peers.ControlAutomationPeer.FromElement(this) is PdfViewerAutomationPeer)
        {
            LoadCurrentPageText();
        }
        RaiseEvent(new PdfDocumentOpenedEventArgs(DocumentOpenedEvent, document));
    }

    /// <summary>Takes the document off the screen first, so nothing asks a closed document for pages, then closes it if it is ours.</summary>
    private void CloseDocument()
    {
        if (OpenedDocument is null)
        {
            return;
        }
        var owned = _ownedDocument;
        _ownedDocument = null;
        OpenedDocument = null;
        Title = null;
        _currentPageText = null;
        SetOutline([]);
        SetCurrentValue(PageSourceProperty, null);
        owned?.Dispose();
    }

    private void Fail(PdfException error)
    {
        SetState(PdfViewerStatus.Failed, error);
        RaiseEvent(new PdfDocumentFailedEventArgs(DocumentFailedEvent, error));
    }

    private void SetEmpty() => SetState(PdfViewerStatus.Empty, null);

    private void SetState(PdfViewerStatus status, PdfException? error)
    {
        Error = error;
        Status = status;
        switch (status)
        {
            case PdfViewerStatus.Loading:
                EnsureStateLayer().ShowLoading();
                break;
            case PdfViewerStatus.Failed:
                EnsureStateLayer().ShowFailed(error!);
                break;
            case PdfViewerStatus.PasswordRequired:
                // The caller decides between a waiting cover and the form.
                break;
            default:
                _stateLayer?.Hide();
                break;
        }
    }

    private PdfStateLayer EnsureStateLayer()
    {
        if (_stateLayer is null)
        {
            _stateLayer = new PdfStateLayer(this);
            EnsureOverlay().Children.Add(_stateLayer);
        }
        return _stateLayer;
    }

    private async Task LoadOutlineAsync(PdfDocument document)
    {
        IReadOnlyList<PdfOutlineItem> outline;
        try
        {
            outline = await document.GetOutlineAsync(CancellationToken.None);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A broken table of contents is no reason to refuse a document that opened: it just has none.
            outline = [];
        }
        if (ReferenceEquals(document, OpenedDocument))
        {
            SetOutline(outline);
        }
    }

    private void SetOutline(IReadOnlyList<PdfOutlineItem> outline)
    {
        _outline = outline;
        _sidebar?.SetOutline(outline);
    }
}
