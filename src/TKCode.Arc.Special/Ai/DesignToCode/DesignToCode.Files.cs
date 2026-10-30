using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

/// <summary>Designs coming in: files dropped on the design side, files from the app's picker, and design file imports.</summary>
public partial class DesignToCode
{
    private CancellationTokenSource? _importCts;

    /// <summary>
    /// Reads <paramref name="file"/> as the design: a file of another type than <see cref="AcceptedFileTypes"/>, over
    /// <see cref="MaxFileSize"/> or that is not an image shows an error instead. Returns whether it became the source.
    /// </summary>
    public async Task<bool> LoadFileAsync(IStorageFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        StopAutoPlay();
        var labels = Labels;
        var name = file.Name;
        var error = CheckFile(name, null);
        long? size = null;
        if (error is null)
        {
            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                size = properties.Size is { } bytes ? (long)bytes : null;
            }
            catch (Exception)
            {
                // The size is only needed for the limit; reading the file below fails if it is really gone.
            }
            error = CheckFile(name, size);
        }
        if (error is not null)
        {
            Reject(error);
            return false;
        }
        Bitmap bitmap;
        try
        {
            await using var stream = await file.OpenReadAsync();
            bitmap = new Bitmap(stream);
        }
        catch (Exception)
        {
            Reject(labels.Unreadable);
            return false;
        }
        var previous = _ownedBitmap;
        _ownedBitmap = bitmap;
        SetCurrentValue(SourceProperty, new DesignToCodeSource(name, bitmap) { File = file, Size = size });
        if (previous is not null && !ReferenceEquals(previous, bitmap))
        {
            previous.Dispose();
        }
        Announce(labels.AnnounceSource(name));
        return true;
    }

    /// <summary>
    /// The error a file named <paramref name="name"/> of <paramref name="size"/> bytes gets, or null when it is accepted
    /// (the size is checked when known).
    /// </summary>
    internal string? CheckFile(string name, long? size)
    {
        var labels = Labels;
        if (!DesignToCodeFormat.Accepts(AcceptedFileTypes, name))
        {
            return labels.WrongType(DesignToCodeFormat.TypesText(AcceptedFileTypes));
        }
        if (size is { } bytes && bytes > MaxFileSize)
        {
            return labels.TooLarge(DesignToCodeFormat.SizeText(MaxFileSize));
        }
        return null;
    }

    /// <summary>Shows <paramref name="error"/> in the drop zone and the status; the design shown stays.</summary>
    internal void Reject(string error)
    {
        if (IsRunning)
        {
            Stop();
        }
        ErrorMessage = error;
        Announce(error);
        RefreshAll();
    }

    /// <summary>Import design file: the app's importer, or a simulated import of the simulation's design.</summary>
    internal async Task ImportDesignFileAsync()
    {
        StopAutoPlay();
        if (IsRunning)
        {
            Stop();
        }
        var labels = Labels;
        if (DesignFileImporter is not { } importer)
        {
            if (Simulation is { Source: { } sample } simulation)
            {
                var name = simulation.FrameName is { Length: > 0 } frame ? frame : sample.Name;
                SetCurrentValue(SourceProperty, new DesignToCodeSource(name, sample.Image, DesignToCodeSourceKind.DesignFile)
                {
                    Sketch = sample.Sketch,
                    SketchSize = sample.SketchSize,
                });
                Announce(labels.AnnounceSource(name) + " " + labels.SimulationNote);
            }
            return;
        }
        _importCts?.Cancel();
        _importCts?.Dispose();
        var cts = new CancellationTokenSource();
        _importCts = cts;
        try
        {
            var source = await importer(cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(_importCts, cts))
            {
                return;
            }
            if (source is not null)
            {
                SetCurrentValue(SourceProperty, source);
                Announce(labels.AnnounceSource(source.Name));
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Replaced by another import or the card went away.
        }
        catch (Exception exception)
        {
            Reject(labels.ImportFailed(exception.Message));
        }
    }

    private void InitializeDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnDrop, handledEventsToo: true);
    }

    /// <summary>Whether a drag at <paramref name="e"/> is over the design side (drop works anywhere on it).</summary>
    private bool OverDesign(DragEventArgs e) =>
        _designPane is not null && new Rect(_designPane.Bounds.Size).Contains(e.GetPosition(_designPane));

    private static bool CarriesFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!CarriesFiles(e) || !OverDesign(e))
        {
            e.DragEffects = DragDropEffects.None;
            SetDragging(false);
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
        SetDragging(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Moving between the card's own parts raises leave and enter; only leaving the design side ends the drag.
        if (OverDesign(e))
        {
            return;
        }
        SetDragging(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragging(false);
        if (!CarriesFiles(e) || !OverDesign(e))
        {
            return;
        }
        e.Handled = true;
        e.DragEffects = DragDropEffects.Copy;
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } items)
        {
            foreach (var item in items)
            {
                if (item is IStorageFile file)
                {
                    await LoadFileAsync(file);
                    return;
                }
            }
            Reject(Labels.Unreadable);
        }
    }

    /// <summary>Shows or hides the "Drop to use this design" cover.</summary>
    internal void SetDragging(bool dragging)
    {
        if (dragging == _dragging)
        {
            return;
        }
        _dragging = dragging;
        PseudoClasses.Set(DraggingPseudoClass, dragging);
        if (_dragCover is not null)
        {
            _dragCover.IsVisible = dragging;
        }
    }

    /// <summary>Whether the drag cover shows (tests).</summary>
    internal bool IsDragging => _dragging;
}
