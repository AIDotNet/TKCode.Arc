using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class KnowledgeBase
{
    // Documents waiting for a slot, in arrival order, and the runs under way with their cancellation.
    private readonly List<KnowledgeDocument> _queue = new();
    private readonly Dictionary<KnowledgeDocument, Run> _runs = new();
    private int _idCounter;

    private sealed class Run(CancellationTokenSource cancellation, bool isReindex)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public bool IsReindex { get; } = isReindex;
        public bool IsOver { get; set; }
    }

    /// <summary>The documents waiting for a slot, first in line first (tests).</summary>
    internal IReadOnlyList<KnowledgeDocument> Queue => _queue;

    /// <summary>The documents being indexed now (tests).</summary>
    internal IReadOnlyCollection<KnowledgeDocument> Running => _runs.Keys;

    #region Public API

    /// <summary>
    /// Adds a document at the top of the list and raises <see cref="DocumentAdded"/>. A document that is not
    /// <see cref="KnowledgeDocumentStatus.Ready"/> or <see cref="KnowledgeDocumentStatus.Failed"/> joins the queue.
    /// </summary>
    public void AddDocument(KnowledgeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrEmpty(document.Id))
        {
            document.Id = NextId();
        }
        if (document.Kind == default && document.Name.Length > 0)
        {
            document.Kind = KnowledgeBaseText.KindOf(document.Name);
        }
        Items.Insert(0, document);
        RaiseEvent(new KnowledgeBaseDocumentEventArgs(DocumentAddedEvent, this, document));
    }

    /// <summary>Opens the platform's file picker (filtered by <see cref="AcceptedExtensions"/>) and adds the chosen files.</summary>
    public async Task BrowseAsync()
    {
        if (IsReadOnly || !IsEffectivelyEnabled || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }
        IReadOnlyList<IStorageFile> files;
        try
        {
            var options = new FilePickerOpenOptions { AllowMultiple = true, Title = Labels.AddFiles };
            if (AcceptedExtensions is { Count: > 0 } accepted)
            {
                options.FileTypeFilter =
                [
                    new FilePickerFileType(Labels.DocumentsHeading) { Patterns = accepted.Select(x => "*" + x).ToArray() }
                ];
            }
            files = await storage.OpenFilePickerAsync(options);
        }
        catch (Exception)
        {
            // A platform without a picker, or a refused one, adds nothing.
            return;
        }
        if (files.Count > 0)
        {
            await AddFilesAsync(files);
        }
        _add?.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Adds picked or dropped files: skips folders, types outside <see cref="AcceptedExtensions"/> and files above
    /// <see cref="MaxFileSize"/> (with a note in the drop zone), reads the text of plain-text files for the built-in
    /// pipeline, and queues the rest at the top of the list.
    /// </summary>
    /// <returns>The documents that were added.</returns>
    public async Task<IReadOnlyList<KnowledgeDocument>> AddFilesAsync(IEnumerable<IStorageItem> files)
    {
        var added = new List<KnowledgeDocument>();
        var skipped = new List<string>();
        foreach (var file in files.OfType<IStorageFile>())
        {
            long size = 0;
            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                size = (long)(properties.Size ?? 0);
            }
            catch (Exception)
            {
                // A file whose size cannot be read is still added; the size shows as 0 B.
            }
            if (!Accepts(file.Name, size))
            {
                skipped.Add(file.Name);
                continue;
            }
            string? text = null;
            if (IndexHandler is null && KnowledgeBaseText.IsPlainText(file.Name))
            {
                text = await ReadTextAsync(file);
            }
            var document = new KnowledgeDocument
            {
                Id = NextId(),
                Name = file.Name,
                Kind = KnowledgeBaseText.KindOf(file.Name),
                SizeBytes = size,
                Text = text,
                Status = KnowledgeDocumentStatus.Queued,
                SourceFile = file
            };
            added.Add(document);
        }
        FinishAdding(added, skipped);
        return added;
    }

    /// <summary>
    /// Adds files described by name, size and text (for files that do not come from a picker or a drop, and for tests);
    /// the same checks and queue as <see cref="AddFilesAsync"/>.
    /// </summary>
    /// <returns>The documents that were added.</returns>
    public IReadOnlyList<KnowledgeDocument> AddFiles(IEnumerable<(string Name, long SizeBytes, string? Text)> files)
    {
        var added = new List<KnowledgeDocument>();
        var skipped = new List<string>();
        foreach (var (name, size, text) in files)
        {
            if (!Accepts(name, size))
            {
                skipped.Add(name);
                continue;
            }
            added.Add(new KnowledgeDocument
            {
                Id = NextId(),
                Name = name,
                Kind = KnowledgeBaseText.KindOf(name),
                SizeBytes = size,
                Text = text,
                Status = KnowledgeDocumentStatus.Queued
            });
        }
        FinishAdding(added, skipped);
        return added;
    }

    /// <summary>Indexes a document again (a ready one is refreshed, a failed one retried); a run under way restarts.</summary>
    public void Reindex(KnowledgeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Items.Contains(document))
        {
            return;
        }
        var wasIndexed = document.Status is KnowledgeDocumentStatus.Ready or KnowledgeDocumentStatus.Failed || document.ChunkCount > 0;
        Forget(document);
        document.Error = null;
        document.Progress = 0;
        document.Status = KnowledgeDocumentStatus.Queued;
        if (wasIndexed)
        {
            _reindexing.Add(document);
        }
        _queue.Add(document);
        Announce(Labels.ReindexAnnouncement(document.Name));
        UpdateTotals();
        Pump();
    }

    /// <summary>Re-indexes every document that is ready or failed.</summary>
    public void ReindexAll()
    {
        foreach (var document in Items.Where(d => d.Status is KnowledgeDocumentStatus.Ready or KnowledgeDocumentStatus.Failed).ToArray())
        {
            Reindex(document);
        }
    }

    /// <summary>
    /// Deletes a document without asking: stops its run, awaits <see cref="DeleteHandler"/>, removes it from the list and
    /// raises <see cref="DocumentDeleted"/>. Returns false (and announces the message) when the handler threw.
    /// </summary>
    public async Task<bool> RemoveAsync(KnowledgeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Items.Contains(document))
        {
            return false;
        }
        Forget(document);
        if (DeleteHandler is { } handler)
        {
            try
            {
                await handler(document, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Announce(Labels.FailedAnnouncement(document.Name, exception.Message));
                return false;
            }
        }
        Items.Remove(document);
        RaiseEvent(new KnowledgeBaseDocumentEventArgs(DocumentDeletedEvent, this, document));
        Announce(Labels.DeletedAnnouncement(document.Name));
        return true;
    }

    #endregion

    private readonly HashSet<KnowledgeDocument> _reindexing = new();

    // While a batch of files is inserted, the list changes do not start runs (the batch is queued in order at the end).
    private bool _batching;

    private string NextId()
    {
        string id;
        do
        {
            id = "doc-" + (++_idCounter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        while (Items.Any(d => d.Id == id));
        return id;
    }

    private bool Accepts(string name, long size)
    {
        if (size > MaxFileSize)
        {
            return false;
        }
        if (AcceptedExtensions is not { Count: > 0 } accepted)
        {
            return true;
        }
        var extension = KnowledgeBaseText.Extension(name);
        return accepted.Any(a => string.Equals(a, extension, StringComparison.OrdinalIgnoreCase));
    }

    private void FinishAdding(List<KnowledgeDocument> added, List<string> skipped)
    {
        // Inserted one by one at the top, so the first file of the batch ends up first; the batch joins the queue in
        // the order it was picked, after the announcement, so a run that ends at once is announced after it.
        _batching = true;
        try
        {
            for (var i = added.Count - 1; i >= 0; i--)
            {
                AddDocument(added[i]);
            }
        }
        finally
        {
            _batching = false;
        }
        ShowDropNote(skipped.Count > 0 ? Labels.SkippedFiles(skipped.Count, skipped[0]) : null);
        if (added.Count > 0)
        {
            Announce(Labels.AddedAnnouncement(added.Count));
        }
        else if (skipped.Count > 0)
        {
            Announce(Labels.SkippedFiles(skipped.Count, skipped[0]));
        }
        foreach (var document in added)
        {
            if (document.Status == KnowledgeDocumentStatus.Queued && !_queue.Contains(document) && !_runs.ContainsKey(document))
            {
                _queue.Add(document);
            }
        }
        Pump();
    }

    private static async Task<string?> ReadTextAsync(IStorageFile file)
    {
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }

    #region Queue

    /// <summary>Puts every queued document that is not yet in line at the end of the line, in list order from the bottom (oldest first).</summary>
    private void EnqueueWaiting()
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var document = Items[i];
            if (document.Status == KnowledgeDocumentStatus.Queued && !_queue.Contains(document) && !_runs.ContainsKey(document))
            {
                _queue.Add(document);
            }
        }
    }

    /// <summary>Starts runs while a slot is free and someone waits. Nothing runs while the card is off screen.</summary>
    private void Pump()
    {
        if (!_attached)
        {
            return;
        }
        while (_runs.Count < MaxConcurrentIndexing && _queue.Count > 0)
        {
            var document = _queue[0];
            _queue.RemoveAt(0);
            if (!Items.Contains(document))
            {
                continue;
            }
            _ = RunAsync(document, _reindexing.Remove(document));
        }
        UpdateTotals();
    }

    /// <summary>Stops a document's run and takes it out of the line (deleted, re-indexed or gone from the list).</summary>
    private void Forget(KnowledgeDocument document)
    {
        _queue.Remove(document);
        _reindexing.Remove(document);
        if (_runs.Remove(document, out var run))
        {
            run.IsOver = true;
            run.Cancellation.Cancel();
            run.Cancellation.Dispose();
        }
    }

    /// <summary>Leaving the visual tree stops every run; the documents go back to the queue and resume on return.</summary>
    private void StopAllRuns()
    {
        foreach (var (document, run) in _runs.ToArray())
        {
            run.IsOver = true;
            run.Cancellation.Cancel();
            run.Cancellation.Dispose();
            _runs.Remove(document);
            if (run.IsReindex)
            {
                _reindexing.Add(document);
            }
            document.Progress = 0;
            document.Status = KnowledgeDocumentStatus.Queued;
            _queue.Insert(0, document);
        }
    }

    private async Task RunAsync(KnowledgeDocument document, bool isReindex)
    {
        var cancellation = new CancellationTokenSource();
        var run = new Run(cancellation, isReindex);
        _runs[document] = run;
        document.Error = null;
        document.Progress = 0;
        document.Status = document.SourceFile is not null || IndexHandler is not null
            ? KnowledgeDocumentStatus.Uploading
            : KnowledgeDocumentStatus.Parsing;
        var progress = new KnowledgeBaseProgressSink(document, run);
        IReadOnlyList<KnowledgeChunk> chunks;
        try
        {
            chunks = IndexHandler is { } handler
                ? await handler(document, progress, cancellation.Token)
                : await SimulateAsync(document, progress, cancellation.Token);
        }
        catch (OperationCanceledException) when (run.IsOver)
        {
            return;
        }
        catch (Exception exception)
        {
            if (run.IsOver)
            {
                return;
            }
            Finish(document, run);
            var message = exception.Message;
            document.Error = string.IsNullOrWhiteSpace(message) ? Labels.StatusName(KnowledgeDocumentStatus.Failed) : message;
            document.Status = KnowledgeDocumentStatus.Failed;
            RaiseEvent(new KnowledgeBaseIndexFailedEventArgs(IndexingFailedEvent, this, document,
                exception is KnowledgeBaseNoTextException ? null : exception, document.Error));
            Announce(Labels.FailedAnnouncement(document.Name, document.Error));
            Pump();
            return;
        }
        if (run.IsOver)
        {
            return;
        }
        Finish(document, run);
        var list = chunks ?? Array.Empty<KnowledgeChunk>();
        document.Chunks = list;
        document.ChunkCount = list.Count;
        document.Progress = 1;
        document.UpdatedAt = Clock();
        document.Status = KnowledgeDocumentStatus.Ready;
        RaiseEvent(new KnowledgeBaseIndexedEventArgs(IndexingCompletedEvent, this, document, list.Count, isReindex));
        Announce(Labels.ReadyAnnouncement(document.Name, list.Count));
        Pump();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            StartSearch(immediate: true);
        }
    }

    private void Finish(KnowledgeDocument document, Run run)
    {
        run.IsOver = true;
        if (_runs.TryGetValue(document, out var current) && ReferenceEquals(current, run))
        {
            _runs.Remove(document);
        }
        run.Cancellation.Dispose();
    }

    /// <summary>
    /// The built-in pipeline: an upload of four steps for a file, parsing (the text is split into chunks), and embedding
    /// in one step per chunk (three to eight steps), each step <see cref="PipelineStep"/> long. A document without text
    /// fails with <see cref="KnowledgeBaseLabels.NoText"/>.
    /// </summary>
    private async Task<IReadOnlyList<KnowledgeChunk>> SimulateAsync(KnowledgeDocument document, IProgress<KnowledgeIndexProgress> progress,
        CancellationToken cancellation)
    {
        var step = PipelineStep;
        if (document.SourceFile is not null)
        {
            for (var i = 1; i <= 4; i++)
            {
                await Delay(step, cancellation);
                progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Uploading, 0.3 * i / 4));
            }
        }
        progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Parsing, 0.3));
        await Delay(step, cancellation);
        if (string.IsNullOrWhiteSpace(document.Text))
        {
            throw new KnowledgeBaseNoTextException(Labels.NoText(Labels.KindName(document.Kind)));
        }
        var chunks = KnowledgeBaseText.Chunk(document.Id, document.Text);
        progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.4));
        var steps = Math.Clamp(chunks.Count, 3, 8);
        for (var i = 1; i <= steps; i++)
        {
            await Delay(step, cancellation);
            progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.4 + 0.6 * i / steps));
        }
        return chunks;
    }

    /// <summary>
    /// Writes a run's reports to its document on the UI thread, at once when called there (so a pipeline that reports
    /// synchronously is seen synchronously), and ignores reports after the run ended.
    /// </summary>
    private sealed class KnowledgeBaseProgressSink(KnowledgeDocument document, Run run) : IProgress<KnowledgeIndexProgress>
    {
        public void Report(KnowledgeIndexProgress value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply(value);
            }
            else
            {
                Dispatcher.UIThread.Post(() => Apply(value));
            }
        }

        private void Apply(KnowledgeIndexProgress value)
        {
            if (run.IsOver)
            {
                return;
            }
            if (value.Status is KnowledgeDocumentStatus.Uploading or KnowledgeDocumentStatus.Parsing or KnowledgeDocumentStatus.Embedding)
            {
                document.Status = value.Status;
            }
            document.Progress = value.Fraction;
        }
    }

    #endregion
}

/// <summary>Thrown by the built-in pipeline for a document without text; its message is shown in the row.</summary>
internal sealed class KnowledgeBaseNoTextException(string message) : Exception(message);
