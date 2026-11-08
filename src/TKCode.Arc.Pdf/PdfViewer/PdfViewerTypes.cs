using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="PdfViewer"/> is with its document.</summary>
public enum PdfViewerStatus
{
    /// <summary>No document, and none on the way.</summary>
    Empty,

    /// <summary>A document is opening.</summary>
    Loading,

    /// <summary>A document is on show.</summary>
    Ready,

    /// <summary>The document needs a password.</summary>
    PasswordRequired,

    /// <summary>The document could not be opened (<see cref="PdfViewer.Error"/>).</summary>
    Failed
}

/// <summary>What the sidebar of a <see cref="PdfViewer"/> shows.</summary>
public enum PdfSidebarMode
{
    /// <summary>The pages as small pictures.</summary>
    Thumbnails,

    /// <summary>The table of contents.</summary>
    Outline
}

/// <summary>Arguments of <see cref="PdfViewer.DocumentOpened"/>.</summary>
public sealed class PdfDocumentOpenedEventArgs : RoutedEventArgs
{
    internal PdfDocumentOpenedEventArgs(RoutedEvent routedEvent, PdfDocument document)
        : base(routedEvent)
    {
        Document = document;
    }

    /// <summary>The document now on show.</summary>
    public PdfDocument Document { get; }
}

/// <summary>Arguments of <see cref="PdfViewer.DocumentFailed"/>.</summary>
public sealed class PdfDocumentFailedEventArgs : RoutedEventArgs
{
    internal PdfDocumentFailedEventArgs(RoutedEvent routedEvent, PdfException error)
        : base(routedEvent)
    {
        Error = error;
    }

    /// <summary>Why the document could not be opened.</summary>
    public PdfException Error { get; }
}

/// <summary>
/// Arguments of <see cref="PdfViewer.PasswordRequested"/>. Set <see cref="Password"/> to try it, or <see cref="Cancel"/> to give
/// up; leaving both alone shows the viewer's own form. To ask asynchronously, take a <see cref="GetDeferral"/> and complete
/// it once the answer is set.
/// </summary>
public sealed class PdfPasswordEventArgs : RoutedEventArgs
{
    private int _deferrals;
    private TaskCompletionSource? _completed;

    internal PdfPasswordEventArgs(RoutedEvent routedEvent, bool isRetry)
        : base(routedEvent)
    {
        IsRetry = isRetry;
    }

    /// <summary>True when a password was tried and was wrong.</summary>
    public bool IsRetry { get; }

    /// <summary>The password to try. The viewer clears it when it takes it.</summary>
    public string? Password { get; set; }

    /// <summary>Set to give up: the viewer goes to <see cref="PdfViewerStatus.Failed"/> with <see cref="PdfErrorKind.PasswordRequired"/>.</summary>
    public bool Cancel { get; set; }

    /// <summary>Makes the viewer wait for an answer until the returned deferral is completed.</summary>
    public PdfPasswordDeferral GetDeferral()
    {
        _deferrals++;
        return new PdfPasswordDeferral(this);
    }

    internal Task WhenAnswered => _deferrals == 0 ? Task.CompletedTask : (_completed ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    internal string? TakePassword()
    {
        var password = Password;
        Password = null;
        return password;
    }

    internal void CompleteDeferral()
    {
        if (--_deferrals == 0)
        {
            _completed?.TrySetResult();
        }
    }
}

/// <summary>Holds a <see cref="PdfViewer"/> waiting for a password; see <see cref="PdfPasswordEventArgs.GetDeferral"/>.</summary>
public sealed class PdfPasswordDeferral : IDisposable
{
    private PdfPasswordEventArgs? _owner;

    internal PdfPasswordDeferral(PdfPasswordEventArgs owner)
    {
        _owner = owner;
    }

    /// <summary>The answer is set; the viewer goes on.</summary>
    public void Complete()
    {
        _owner?.CompleteDeferral();
        _owner = null;
    }

    /// <summary>Same as <see cref="Complete"/>.</summary>
    public void Dispose() => Complete();
}
