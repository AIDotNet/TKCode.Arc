using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Arc's generous target for dropping one or more files: drag files from the system onto it, click (or Enter / Space)
/// to choose from the device, or paste a file while the pointer is over it. Files are checked against
/// <see cref="Accept"/>, <see cref="MaxFileSize"/> and <see cref="MaxFiles"/>; refusals shake the target. The list
/// shows each file with an upload state driven by <see cref="UploadHandler"/> or by the app, with Retry and Remove.
/// </summary>
[TemplatePart(ZoneRootPart, typeof(Panel))]
[TemplatePart(ZonePart, typeof(FileDropzoneZone))]
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[TemplatePart(InsideHostPart, typeof(Decorator))]
[TemplatePart(BelowHostPart, typeof(Decorator))]
[TemplatePart(StatusPart, typeof(EditorsLiveRegion))]
[TemplatePart(AlertPart, typeof(EditorsLiveRegion))]
[PseudoClasses(DragOverPseudoClass, HoveredPseudoClass, TriggerPressedPseudoClass, CompactPseudoClass, InsidePseudoClass, HasRowsPseudoClass, ErrorPseudoClass)]
public class FileDropzone : TemplatedControl
{
    public const string ZoneRootPart = "PART_ZoneRoot";
    public const string ZonePart = "PART_Zone";
    public const string TriggerPart = "PART_Trigger";
    public const string InsideHostPart = "PART_InsideHost";
    public const string BelowHostPart = "PART_BelowHost";
    public const string StatusPart = "PART_Status";
    public const string AlertPart = "PART_Alert";
    public const string DragOverPseudoClass = ":dragover";
    public const string HoveredPseudoClass = ":hovered";
    public const string TriggerPressedPseudoClass = ":trigger-pressed";
    public const string CompactPseudoClass = ":compact";
    public const string InsidePseudoClass = ":inside";
    public const string HasRowsPseudoClass = ":has-rows";
    public const string ErrorPseudoClass = ":error";

    /// <summary>Accepted types like an HTML file input: extensions (".pdf") and MIME types ("image/*", "application/pdf"), comma separated.</summary>
    public static readonly StyledProperty<string?> AcceptProperty = AvaloniaProperty.Register<FileDropzone, string?>(nameof(Accept));

    public static readonly StyledProperty<bool> AllowMultipleProperty = AvaloniaProperty.Register<FileDropzone, bool>(nameof(AllowMultiple), true);

    public static readonly StyledProperty<int> MaxFilesProperty = AvaloniaProperty.Register<FileDropzone, int>(nameof(MaxFiles), 5);

    public static readonly StyledProperty<long?> MaxFileSizeProperty = AvaloniaProperty.Register<FileDropzone, long?>(nameof(MaxFileSize));

    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<FileDropzone, string>(nameof(Label), "Add files");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<FileDropzone, string>(nameof(Description), "Drop files here or choose from your device");

    public static readonly StyledProperty<string?> NoteProperty = AvaloniaProperty.Register<FileDropzone, string?>(nameof(Note));

    public static readonly StyledProperty<string?> DropLabelProperty = AvaloniaProperty.Register<FileDropzone, string?>(nameof(DropLabel));

    public static readonly StyledProperty<FileDropzoneListPlacement> ListPlacementProperty =
        AvaloniaProperty.Register<FileDropzone, FileDropzoneListPlacement>(nameof(ListPlacement));

    public static readonly StyledProperty<int?> CompactAtProperty = AvaloniaProperty.Register<FileDropzone, int?>(nameof(CompactAt));

    /// <summary>Uploads each added file: report 0–100 through the progress, complete the task when it lands, throw to fail it (the message becomes the reason). Removing the row cancels the token.</summary>
    public static readonly StyledProperty<Func<FileDropzoneItem, IProgress<double>, CancellationToken, Task>?> UploadHandlerProperty =
        AvaloniaProperty.Register<FileDropzone, Func<FileDropzoneItem, IProgress<double>, CancellationToken, Task>?>(nameof(UploadHandler));

    public static readonly DirectProperty<FileDropzone, bool> IsDragOverProperty =
        AvaloniaProperty.RegisterDirect<FileDropzone, bool>(nameof(IsDragOver), o => o.IsDragOver);

    public static readonly DirectProperty<FileDropzone, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<FileDropzone, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    internal static readonly DirectProperty<FileDropzone, string> DisplayLabelProperty =
        AvaloniaProperty.RegisterDirect<FileDropzone, string>(nameof(DisplayLabel), o => o.DisplayLabel);

    internal static readonly DirectProperty<FileDropzone, string> NoteTextProperty =
        AvaloniaProperty.RegisterDirect<FileDropzone, string>(nameof(NoteText), o => o.NoteText);

    internal static readonly DirectProperty<FileDropzone, bool> IsIconShownProperty =
        AvaloniaProperty.RegisterDirect<FileDropzone, bool>(nameof(IsIconShown), o => o.IsIconShown);

    internal static readonly DirectProperty<FileDropzone, bool> IsNoteShownProperty =
        AvaloniaProperty.RegisterDirect<FileDropzone, bool>(nameof(IsNoteShown), o => o.IsNoteShown);

    /// <summary>Raised when the visitor's files change (added or removed; Arc's <c>onFilesChange</c>).</summary>
    public static readonly RoutedEvent<FileDropzoneFilesChangedEventArgs> FilesChangedEvent =
        RoutedEvent.Register<FileDropzone, FileDropzoneFilesChangedEventArgs>(nameof(FilesChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when files are refused (type not accepted, or more than <see cref="MaxFiles"/>).</summary>
    public static readonly RoutedEvent<FileDropzoneRejectedEventArgs> FilesRejectedEvent =
        RoutedEvent.Register<FileDropzone, FileDropzoneRejectedEventArgs>(nameof(FilesRejected), RoutingStrategies.Bubble);

    private readonly Dictionary<FileDropzoneItem, FileDropzoneRowHost> _rows = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<FileDropzoneItem, CancellationTokenSource> _uploads = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<FileDropzoneItem> _fresh = new(ReferenceEqualityComparer.Instance);
    private readonly StackPanel _list = new() { Orientation = Orientation.Vertical, Classes = { "file-dropzone-list" } };
    private readonly ScaleTransform _breathTransform = new();
    private readonly TranslateTransform _shakeTransform = new();
    private MotionValue<double>? _shake;
    private MotionValue<double>? _breath;
    private int _breathRun;
    private Panel? _zoneRoot;
    private FileDropzoneZone? _zone;
    private AvaloniaButton? _trigger;
    private Decorator? _insideHost;
    private Decorator? _belowHost;
    private EditorsLiveRegion? _status;
    private EditorsLiveRegion? _alert;
    private bool _isDragOver;
    private string? _errorMessage;
    private string _displayLabel = "Add files";
    private string _noteText = "Up to 5 files";
    private bool _isIconShown = true;
    private bool _isNoteShown = true;
    private int _batchStart;
    private bool _loaded;
    private bool _pasteHover;
    private TopLevel? _pasteRoot;

    public FileDropzone()
    {
        FocusModality.Watch();
        Items = new ObservableCollection<FileDropzoneItem>();
        Items.CollectionChanged += OnItemsChanged;
        _list.KeyDown += OnListKeyDown;
        AutomationProperties.SetName(_list, "Files");
        RefreshCopy();
    }

    /// <inheritdoc cref="AcceptProperty"/>
    public string? Accept { get => GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }

    /// <summary>Allow several files (default true); a single-file dropzone replaces its file.</summary>
    public bool AllowMultiple { get => GetValue(AllowMultipleProperty); set => SetValue(AllowMultipleProperty, value); }

    /// <summary>The most files the list holds (default 5).</summary>
    public int MaxFiles { get => GetValue(MaxFilesProperty); set => SetValue(MaxFilesProperty, value); }

    /// <summary>Files above this many bytes fail with a size reason and never upload.</summary>
    public long? MaxFileSize { get => GetValue(MaxFileSizeProperty); set => SetValue(MaxFileSizeProperty, value); }

    /// <summary>The target's label (default "Add files").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The line under the label (default "Drop files here or choose from your device").</summary>
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Replaces the small line under the description (default: the accepted types, or the file limit).</summary>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <summary>The label while files hover over the target (default "Drop to upload" / "Drop to add files").</summary>
    public string? DropLabel { get => GetValue(DropLabelProperty); set => SetValue(DropLabelProperty, value); }

    /// <summary>List the files under the target (default) or inside its edge.</summary>
    public FileDropzoneListPlacement ListPlacement { get => GetValue(ListPlacementProperty); set => SetValue(ListPlacementProperty, value); }

    /// <summary>Once the list holds this many files, the prompt folds to a slim bar; a file dragged over brings the icon back.</summary>
    public int? CompactAt { get => GetValue(CompactAtProperty); set => SetValue(CompactAtProperty, value); }

    /// <inheritdoc cref="UploadHandlerProperty"/>
    public Func<FileDropzoneItem, IProgress<double>, CancellationToken, Task>? UploadHandler
    {
        get => GetValue(UploadHandlerProperty);
        set => SetValue(UploadHandlerProperty, value);
    }

    /// <summary>The rows. Items added before the dropzone loads render in place without an entrance (Arc's <c>defaultItems</c>).</summary>
    public ObservableCollection<FileDropzoneItem> Items { get; }

    /// <summary>True while files are dragged over the target.</summary>
    public bool IsDragOver { get => _isDragOver; private set => SetAndRaise(IsDragOverProperty, ref _isDragOver, value); }

    /// <summary>The refusal shown under the target, or null.</summary>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    internal string DisplayLabel { get => _displayLabel; private set => SetAndRaise(DisplayLabelProperty, ref _displayLabel, value); }

    internal string NoteText { get => _noteText; private set => SetAndRaise(NoteTextProperty, ref _noteText, value); }

    internal bool IsIconShown { get => _isIconShown; private set => SetAndRaise(IsIconShownProperty, ref _isIconShown, value); }

    internal bool IsNoteShown { get => _isNoteShown; private set => SetAndRaise(IsNoteShownProperty, ref _isNoteShown, value); }

    internal StackPanel List => _list;

    internal EditorsLiveRegion? Status => _status;

    internal EditorsLiveRegion? Alert => _alert;

    internal FileDropzoneRow? RowOf(FileDropzoneItem item) => _rows.TryGetValue(item, out var host) ? host.Row : null;

    /// <summary>Raised when the visitor's files change.</summary>
    public event EventHandler<FileDropzoneFilesChangedEventArgs>? FilesChanged
    {
        add => AddHandler(FilesChangedEvent, value);
        remove => RemoveHandler(FilesChangedEvent, value);
    }

    /// <summary>Raised when files are refused.</summary>
    public event EventHandler<FileDropzoneRejectedEventArgs>? FilesRejected
    {
        add => AddHandler(FilesRejectedEvent, value);
        remove => RemoveHandler(FilesRejectedEvent, value);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Public API
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Opens the system file picker (filtered by <see cref="Accept"/>) and adds the chosen files.</summary>
    public async Task BrowseAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is not { CanOpen: true })
        {
            return;
        }
        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = AllowMultiple,
                Title = Label,
                FileTypeFilter = FileDropzoneTypes.PickerFilter(Accept)
            });
        }
        catch (Exception)
        {
            // A platform without a picker (or a refused one) adds nothing.
            return;
        }
        if (files.Count > 0)
        {
            await AddFilesAsync(files);
        }
    }

    /// <summary>Adds files as a drop would: checks type, duplicates, size and the file limit, then lists (and uploads) them.</summary>
    public async Task AddFilesAsync(IEnumerable<IStorageItem> files)
    {
        var incoming = new List<FileDropzoneItem>();
        foreach (var file in files.OfType<IStorageFile>())
        {
            long size = 0;
            DateTimeOffset? modified = null;
            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                size = (long)(properties.Size ?? 0);
                modified = properties.DateModified;
            }
            catch (Exception)
            {
                // Some providers cannot report properties; the file still goes in with size 0.
            }
            incoming.Add(new FileDropzoneItem(file, file.Name, size, modified));
        }
        AddItems(incoming);
    }

    /// <summary>Removes a row, cancelling its upload; focus moves to the next row's Remove, else the previous, else the target.</summary>
    public void Remove(FileDropzoneItem item)
    {
        var index = Items.IndexOf(item);
        if (index < 0)
        {
            return;
        }
        CancelUpload(item);
        var neighbor = index + 1 < Items.Count ? Items[index + 1] : index > 0 ? Items[index - 1] : null;
        var hadFocus = _rows.TryGetValue(item, out var host) && host.IsKeyboardFocusWithin;
        Items.Remove(item);
        SetError(null);
        Announce($"{item.Name} removed");
        if (item.File is not null)
        {
            RaiseFilesChanged();
        }
        if (hadFocus || neighbor is null)
        {
            // A Remove clicked with the mouse moves focus on without the keyboard ring.
            var method = FocusModality.For(this);
            Dispatcher.UIThread.Post(() =>
            {
                if (neighbor is not null && RowOf(neighbor)?.RemoveButton is { } remove)
                {
                    remove.Focus(method);
                }
                else if (hadFocus)
                {
                    _trigger?.Focus(method);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>Starts the upload again; focus moves to the row's Remove (Retry leaves with the failure).</summary>
    public void Retry(FileDropzoneItem item)
    {
        if (!Items.Contains(item))
        {
            return;
        }
        Announce($"Retrying {item.Name}");
        StartUpload(item);
        var method = FocusModality.For(this);
        Dispatcher.UIThread.Post(() => RowOf(item)?.RemoveButton?.Focus(method), DispatcherPriority.Loaded);
    }

    /// <summary>Reports upload progress for a row the app uploads itself (0–100; the bar never moves back).</summary>
    public void ReportProgress(FileDropzoneItem item, double percent)
    {
        if (item.Status != FileDropzoneStatus.Uploading)
        {
            item.Error = null;
            item.Progress = 0;
            item.Status = FileDropzoneStatus.Uploading;
        }
        item.Progress = Math.Max(item.Progress, Math.Clamp(percent, 0, 100));
    }

    /// <summary>Marks a row uploaded.</summary>
    public void CompleteUpload(FileDropzoneItem item)
    {
        item.Status = FileDropzoneStatus.Uploaded;
        item.Progress = 100;
        Announce($"{item.Name} uploaded");
    }

    /// <summary>Marks a row failed with <paramref name="reason"/> (default "Upload failed").</summary>
    public void FailUpload(FileDropzoneItem item, string? reason = null, bool retryable = true)
    {
        var message = string.IsNullOrWhiteSpace(reason) ? "Upload failed" : reason;
        item.Error = message;
        item.IsRetryable = retryable;
        item.Status = FileDropzoneStatus.Failed;
        Announce($"{item.Name} failed. {message}");
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Adding and uploading
     * -------------------------------------------------------------------------------------------------------- */

    internal void AddItems(IReadOnlyList<FileDropzoneItem> list)
    {
        var accept = FileDropzoneTypes.ParseAccept(Accept);
        var matching = list.Where(item => FileDropzoneTypes.Matches(item.Name, accept)).ToList();
        var multiple = AllowMultiple;
        var kept = multiple ? Items.ToList() : new List<FileDropzoneItem>();
        static bool Same(FileDropzoneItem a, FileDropzoneItem b) => a.Name == b.Name && a.Size == b.Size && a.Modified == b.Modified;
        var fresh = matching.Where((item, index) => matching.FindIndex(other => Same(other, item)) == index && !kept.Any(k => k.File is not null && Same(k, item))).ToList();
        var room = multiple ? Math.Max(0, MaxFiles - kept.Count) : 1;
        var added = fresh.Take(room).ToList();
        var maxSize = MaxFileSize;
        var upload = UploadHandler is not null;
        foreach (var item in added)
        {
            var tooLarge = maxSize is { } max && item.Size > max;
            item.IsRetryable = !tooLarge;
            item.Progress = 0;
            item.Error = tooLarge ? $"File is larger than {FileDropzoneItem.FormatSize(maxSize!.Value)}" : null;
            item.Status = tooLarge ? FileDropzoneStatus.Failed : upload ? FileDropzoneStatus.Uploading : FileDropzoneStatus.None;
        }
        var rejected = matching.Count != list.Count;
        var overflow = fresh.Count > added.Count;
        var message = rejected
            ? list.Count == 1 ? $"{list[0].Name} is not an accepted file type." : "Some files were not added because their type is not accepted."
            : overflow ? $"You can add up to {MaxFiles} {(MaxFiles == 1 ? "file" : "files")}." : null;
        SetError(message);
        if (message is not null)
        {
            ShakeZone();
            var refused = list.Except(added).Select(item => item.Name).ToList();
            RaiseEvent(new FileDropzoneRejectedEventArgs(FilesRejectedEvent, message, refused));
        }
        if (added.Count == 0)
        {
            return;
        }
        if (!multiple)
        {
            foreach (var item in Items.ToList())
            {
                CancelUpload(item);
            }
            Items.Clear();
        }
        _batchStart = Items.Count;
        foreach (var item in added)
        {
            _fresh.Add(item);
            Items.Add(item);
            _ = LoadPreviewAsync(item);
        }
        RaiseFilesChanged();
        var failed = added.Where(item => item.Status == FileDropzoneStatus.Failed).ToList();
        var count = $"{added.Count} {(added.Count == 1 ? "file" : "files")} added.";
        Announce(failed.Count > 0 ? $"{count} {string.Join(". ", failed.Select(item => $"{item.Name}: {item.Error}"))}." : count);
        foreach (var item in added.Where(item => item.Status == FileDropzoneStatus.Uploading))
        {
            StartUpload(item);
        }
    }

    private void RaiseFilesChanged()
    {
        var files = Items.Where(item => item.File is not null).Select(item => item.File!).ToList();
        RaiseEvent(new FileDropzoneFilesChangedEventArgs(FilesChangedEvent, files));
    }

    private async Task LoadPreviewAsync(FileDropzoneItem item)
    {
        if (item.File is not { } file || !FileDropzoneTypes.IsImage(item.Name) || item.Preview is not null)
        {
            return;
        }
        try
        {
            await using var stream = await file.OpenReadAsync();
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(buffer, 80));
            if (Items.Contains(item))
            {
                item.OwnsPreview = true;
                item.Preview = bitmap;
            }
            else
            {
                bitmap.Dispose();
            }
        }
        catch (Exception)
        {
            // Not decodable: the row keeps its type icon (Arc falls back the same way).
        }
    }

    private void StartUpload(FileDropzoneItem item)
    {
        if (UploadHandler is not { } handler)
        {
            return;
        }
        CancelUpload(item);
        var cts = new CancellationTokenSource();
        _uploads[item] = cts;
        item.Error = null;
        item.Progress = 0;
        item.Status = FileDropzoneStatus.Uploading;
        var progress = new FileDropzoneProgress(value =>
        {
            if (!cts.IsCancellationRequested && item.Status == FileDropzoneStatus.Uploading)
            {
                item.Progress = Math.Max(item.Progress, Math.Clamp(value, 0, 100));
            }
        });
        _ = RunUpload(item, handler, progress, cts);
    }

    private async Task RunUpload(FileDropzoneItem item, Func<FileDropzoneItem, IProgress<double>, CancellationToken, Task> handler,
                                 IProgress<double> progress, CancellationTokenSource cts)
    {
        try
        {
            await handler(item, progress, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }
            CompleteUpload(item);
        }
        catch (Exception exception)
        {
            if (cts.IsCancellationRequested)
            {
                return;
            }
            FailUpload(item, string.IsNullOrWhiteSpace(exception.Message) ? null : exception.Message);
        }
        finally
        {
            if (_uploads.TryGetValue(item, out var current) && ReferenceEquals(current, cts))
            {
                _uploads.Remove(item);
            }
        }
    }

    private void CancelUpload(FileDropzoneItem item)
    {
        if (_uploads.Remove(item, out var cts))
        {
            cts.Cancel();
        }
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Rows
     * -------------------------------------------------------------------------------------------------------- */

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (FileDropzoneItem item in e.NewItems!)
                {
                    AddRow(item);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (FileDropzoneItem item in e.OldItems!)
                {
                    RemoveRow(item);
                }
                break;
            default:
                foreach (var item in _rows.Keys.ToList())
                {
                    if (!Items.Contains(item))
                    {
                        RemoveRow(item);
                    }
                }
                foreach (var item in Items)
                {
                    if (!_rows.ContainsKey(item))
                    {
                        AddRow(item);
                    }
                }
                break;
        }
        // Keep the visual order of live rows in step with the collection.
        var index = 0;
        foreach (var item in Items)
        {
            if (_rows.TryGetValue(item, out var host))
            {
                var at = _list.Children.IndexOf(host);
                var target = Math.Min(index, _list.Children.Count - 1);
                if (at != target && at >= 0)
                {
                    _list.Children.Move(at, target);
                }
                index++;
            }
        }
        UpdateState();
    }

    private void AddRow(FileDropzoneItem item)
    {
        if (_rows.ContainsKey(item))
        {
            return;
        }
        var fresh = _fresh.Contains(item) && _loaded;
        var position = Items.IndexOf(item);
        var delay = TimeSpan.FromMilliseconds(Math.Min(Math.Max(0, position - _batchStart), 7) * ArcMotionPresets.StaggerItem.TotalMilliseconds * 1.6);
        var row = new FileDropzoneRow(item, fresh, UploadHandler is not null, delay);
        row.RemoveRequested += (_, _) => Remove(item);
        row.RetryRequested += (_, _) => Retry(item);
        var host = new FileDropzoneRowHost(row);
        _rows[item] = host;
        _list.Children.Add(host);
        if (fresh)
        {
            host.Enter(delay);
        }
    }

    private async void RemoveRow(FileDropzoneItem item)
    {
        if (!_rows.Remove(item, out var host))
        {
            return;
        }
        _fresh.Remove(item);
        CancelUpload(item);
        host.Row.Detach();
        await host.Leave();
        _list.Children.Remove(host);
        // The row keeps its thumbnail while it closes, then the bitmap is released.
        if (item.OwnsPreview && item.Preview is { } preview)
        {
            item.Preview = null;
            preview.Dispose();
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var hosts = _list.Children.OfType<FileDropzoneRowHost>().Where(h => !h.IsLeaving).ToList();
        var index = hosts.FindIndex(h => focused is not null && (ReferenceEquals(h, focused) || focused.GetVisualAncestors().Contains(h)));
        if (index < 0)
        {
            return;
        }
        if (e.Key is Key.Delete or Key.Back)
        {
            Remove(hosts[index].Row.Item);
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Down or Key.Up))
        {
            return;
        }
        var next = index + (e.Key == Key.Down ? 1 : -1);
        if (next < 0 || next >= hosts.Count)
        {
            return;
        }
        // Up and down keep the same action: Retry stays on Retry where the next row has one.
        var row = hosts[index].Row;
        var onRetry = row.RetryButton is { } retry && (ReferenceEquals(retry, focused) || focused!.GetVisualAncestors().Contains(retry));
        var target = hosts[next].Row;
        var button = onRetry && target.RetryButton is { IsEffectivelyVisible: true } targetRetry ? targetRetry : target.RemoveButton;
        button?.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Template and state
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_zoneRoot is not null)
        {
            _zoneRoot.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _zoneRoot.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _zoneRoot.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _zoneRoot.RemoveHandler(DragDrop.DropEvent, OnDrop);
        }
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.PointerEntered -= OnTriggerEntered;
            _trigger.PointerExited -= OnTriggerExited;
            _trigger.PropertyChanged -= OnTriggerPropertyChanged;
        }
        if (_insideHost is not null)
        {
            _insideHost.Child = null;
        }
        if (_belowHost is not null)
        {
            _belowHost.Child = null;
        }
        _zoneRoot = e.NameScope.Find<Panel>(ZoneRootPart);
        _zone = e.NameScope.Find<FileDropzoneZone>(ZonePart);
        _trigger = e.NameScope.Find<AvaloniaButton>(TriggerPart);
        _insideHost = e.NameScope.Find<Decorator>(InsideHostPart);
        _belowHost = e.NameScope.Find<Decorator>(BelowHostPart);
        _status = e.NameScope.Find<EditorsLiveRegion>(StatusPart);
        _alert = e.NameScope.Find<EditorsLiveRegion>(AlertPart);
        if (_zoneRoot is not null)
        {
            DragDrop.SetAllowDrop(_zoneRoot, true);
            _zoneRoot.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _zoneRoot.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            _zoneRoot.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _zoneRoot.AddHandler(DragDrop.DropEvent, OnDrop);
            _zoneRoot.RenderTransformOrigin = RelativePoint.Center;
            _zoneRoot.RenderTransform = new TransformGroup { Children = { _breathTransform, _shakeTransform } };
        }
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.PointerEntered += OnTriggerEntered;
            _trigger.PointerExited += OnTriggerExited;
            _trigger.PropertyChanged += OnTriggerPropertyChanged;
        }
        PlaceList();
        RefreshCopy();
        UpdateState();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _loaded = true;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _loaded = false;
        StopPasteWatch();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        foreach (var cts in _uploads.Values)
        {
            cts.Cancel();
        }
        _uploads.Clear();
        foreach (var item in Items)
        {
            if (item.OwnsPreview && item.Preview is { } preview)
            {
                item.Preview = null;
                preview.Dispose();
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ListPlacementProperty)
        {
            PlaceList();
            UpdateState();
        }
        else if (change.Property == CompactAtProperty)
        {
            UpdateState();
        }
        else if (change.Property == LabelProperty || change.Property == DropLabelProperty || change.Property == NoteProperty
                 || change.Property == AcceptProperty || change.Property == MaxFilesProperty || change.Property == AllowMultipleProperty
                 || change.Property == UploadHandlerProperty || change.Property == DescriptionProperty)
        {
            RefreshCopy();
        }
        if (change.Property == UploadHandlerProperty)
        {
            foreach (var host in _rows.Values)
            {
                host.Row.CanRetryUploads = UploadHandler is not null;
            }
        }
    }

    private void PlaceList()
    {
        if (_list.Parent is Decorator parent)
        {
            parent.Child = null;
        }
        var host = ListPlacement == FileDropzoneListPlacement.Inside ? _insideHost : _belowHost;
        if (host is not null)
        {
            host.Child = _list;
        }
    }

    private void RefreshCopy()
    {
        var dropCopy = DropLabel ?? (UploadHandler is not null ? "Drop to upload" : AllowMultiple ? "Drop to add files" : "Drop to add the file");
        DisplayLabel = IsDragOver ? dropCopy : Label;
        NoteText = Note ?? (Accept is { Length: > 0 } accept ? $"Accepted: {accept}" : $"Up to {MaxFiles} {(MaxFiles == 1 ? "file" : "files")}");
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, Label);
            AutomationProperties.SetHelpText(_trigger, IsNoteShown ? $"{Description} {NoteText}" : Description);
        }
    }

    private void UpdateState()
    {
        var hasRows = Items.Count > 0;
        var inside = ListPlacement == FileDropzoneListPlacement.Inside;
        var compact = CompactAt is { } at && Items.Count >= at;
        PseudoClasses.Set(HasRowsPseudoClass, hasRows);
        PseudoClasses.Set(InsidePseudoClass, inside);
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(":rows-inside", inside && hasRows);
        IsIconShown = !compact || IsDragOver;
        IsNoteShown = !compact;
        AutomationProperties.SetAccessibilityView(_list, hasRows ? AccessibilityView.Default : AccessibilityView.Raw);
        RefreshCopy();
    }

    private void SetError(string? message)
    {
        ErrorMessage = message;
        PseudoClasses.Set(ErrorPseudoClass, message is not null);
        if (message is not null)
        {
            _alert?.Announce(message);
        }
    }

    private void Announce(string message) => _status?.Announce(message);

    /// <summary>A short, decaying side-to-side shake: the refusal reads as "no" without moving anything else.</summary>
    private void ShakeZone()
    {
        if (_zoneRoot is null || !ArcMotion.IsFull(this))
        {
            return;
        }
        _shake ??= CreateShake();
        _shake.Jump(0);
        _ = _shake.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(420), new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1)));
    }

    private MotionValue<double> CreateShake()
    {
        double[] frames = { 0, -7, 6, -4, 3, -1.5, 0 };
        var shake = new MotionValue<double>(this, 1, MotionVectors.Double);
        shake.Changed += t =>
        {
            if (t <= 0 || t >= 1)
            {
                _shakeTransform.X = 0;
                return;
            }
            var position = t * (frames.Length - 1);
            var index = (int)position;
            _shakeTransform.X = frames[index] + (frames[Math.Min(index + 1, frames.Length - 1)] - frames[index]) * (position - index);
        };
        return shake;
    }

    /// <summary>
    /// While files hover, the target breathes (scale 1 → 1.012 → 1 every 2.2s, ease in-out each way). Code driven because
    /// the loop must stop the moment the drag ends and never run under reduced motion; it only runs while dragging.
    /// </summary>
    private async void Breathe()
    {
        var run = ++_breathRun;
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _breath ??= CreateBreath();
        var half = TimeSpan.FromMilliseconds(1100);
        while (run == _breathRun && IsDragOver)
        {
            if (!await _breath.AnimateTo(1.012, MotionSpec.Tween(half, ArcEasings.InOut)) || run != _breathRun)
            {
                break;
            }
            if (!await _breath.AnimateTo(1, MotionSpec.Tween(half, ArcEasings.InOut)))
            {
                break;
            }
        }
    }

    private void StopBreathing()
    {
        _breathRun++;
        if (_breath is not null && Math.Abs(_breath.Value - 1) > 0.0001)
        {
            _ = _breath.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
    }

    private MotionValue<double> CreateBreath()
    {
        var breath = new MotionValue<double>(this, 1, MotionVectors.Double);
        breath.Changed += value =>
        {
            _breathTransform.ScaleX = value;
            _breathTransform.ScaleY = value;
        };
        return breath;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Pointer, drag and drop, paste
     * -------------------------------------------------------------------------------------------------------- */

    private async void OnTriggerClick(object? sender, RoutedEventArgs e) => await BrowseAsync();

    private void OnTriggerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            PseudoClasses.Set(HoveredPseudoClass, true);
        }
    }

    private void OnTriggerExited(object? sender, PointerEventArgs e) => PseudoClasses.Set(HoveredPseudoClass, false);

    private void OnTriggerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AvaloniaButton.IsPressedProperty)
        {
            PseudoClasses.Set(TriggerPressedPseudoClass, _trigger?.IsPressed == true);
        }
    }

    private static bool CarriesFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    private void Track(DragEventArgs e, bool jump)
    {
        if (_zone is null)
        {
            return;
        }
        _zone.MoveGlow(e.GetPosition(_zone), jump || !ArcMotion.IsFull(this));
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!CarriesFiles(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        Track(e, jump: !IsDragOver);
        SetDragOver(true);
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!CarriesFiles(e))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        if (!IsDragOver)
        {
            Track(e, jump: true);
            SetDragOver(true);
        }
        else
        {
            Track(e, jump: false);
        }
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Moving between the target's own parts raises leave and enter; only leaving its bounds ends the drag.
        if (_zoneRoot is not null && new Rect(_zoneRoot.Bounds.Size).Contains(e.GetPosition(_zoneRoot)))
        {
            return;
        }
        SetDragOver(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragOver(false);
        if (!CarriesFiles(e))
        {
            return;
        }
        e.Handled = true;
        e.DragEffects = DragDropEffects.Copy;
        var files = e.DataTransfer.TryGetFiles();
        if (files is { Length: > 0 })
        {
            await AddFilesAsync(files);
        }
    }

    internal void SetDragOver(bool value)
    {
        if (IsDragOver == value)
        {
            return;
        }
        IsDragOver = value;
        PseudoClasses.Set(DragOverPseudoClass, value);
        UpdateState();
        if (value)
        {
            Breathe();
        }
        else
        {
            StopBreathing();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _pasteHover = true;
        StartPasteWatch();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pasteHover = false;
        if (!IsKeyboardFocusWithin)
        {
            StopPasteWatch();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        StartPasteWatch();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsKeyboardFocusWithin && !_pasteHover)
            {
                StopPasteWatch();
            }
        });
    }

    /// <summary>Paste works while the pointer is over the target or focus is inside it, so a screenshot on the clipboard is one keystroke away.</summary>
    private void StartPasteWatch()
    {
        if (_pasteRoot is not null)
        {
            return;
        }
        _pasteRoot = TopLevel.GetTopLevel(this);
        _pasteRoot?.AddHandler(KeyDownEvent, OnRootKeyDown, RoutingStrategies.Tunnel);
    }

    private void StopPasteWatch()
    {
        _pasteRoot?.RemoveHandler(KeyDownEvent, OnRootKeyDown);
        _pasteRoot = null;
    }

    private async void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        var paste = EditorsHotkeys.Matches(EditorsHotkeys.Keymap?.Paste, e) || (EditorsHotkeys.IsMod(e.KeyModifiers) && e.Key == Key.V);
        if (!paste || _pasteRoot is not { } root)
        {
            return;
        }
        if (root.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }
        try
        {
            if (root.Clipboard is { } clipboard && await clipboard.TryGetFilesAsync() is { Length: > 0 } files)
            {
                await AddFilesAsync(files);
            }
        }
        catch (Exception)
        {
            // Clipboards without file support simply have nothing to add.
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new EditorsGroupAutomationPeer(this, AutomationControlType.Group);
}

/// <summary>Progress reports applied on the UI thread, whichever thread the upload reports from.</summary>
internal sealed class FileDropzoneProgress(Action<double> report) : IProgress<double>
{
    public void Report(double value)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            report(value);
        }
        else
        {
            Dispatcher.UIThread.Post(() => report(value));
        }
    }
}

/// <summary>Accept matching like an HTML file input (extensions and MIME patterns) and the picker filter built from it.</summary>
internal static class FileDropzoneTypes
{
    private static readonly Dictionary<string, string> s_mime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg", ["gif"] = "image/gif", ["webp"] = "image/webp",
        ["svg"] = "image/svg+xml", ["avif"] = "image/avif", ["heic"] = "image/heic", ["bmp"] = "image/bmp", ["ico"] = "image/x-icon",
        ["tif"] = "image/tiff", ["tiff"] = "image/tiff",
        ["mp4"] = "video/mp4", ["mov"] = "video/quicktime", ["webm"] = "video/webm", ["m4v"] = "video/x-m4v", ["avi"] = "video/x-msvideo",
        ["mkv"] = "video/x-matroska",
        ["mp3"] = "audio/mpeg", ["wav"] = "audio/wav", ["ogg"] = "audio/ogg", ["m4a"] = "audio/mp4", ["flac"] = "audio/flac", ["aac"] = "audio/aac",
        ["pdf"] = "application/pdf", ["zip"] = "application/zip", ["gz"] = "application/gzip", ["tar"] = "application/x-tar",
        ["rar"] = "application/vnd.rar", ["7z"] = "application/x-7z-compressed", ["json"] = "application/json", ["xml"] = "application/xml",
        ["csv"] = "text/csv", ["txt"] = "text/plain", ["md"] = "text/markdown", ["html"] = "text/html", ["css"] = "text/css",
        ["js"] = "text/javascript", ["rtf"] = "application/rtf", ["doc"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ["xls"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ["ppt"] = "application/vnd.ms-powerpoint",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public static string? MimeOf(string name)
    {
        var extension = Path.GetExtension(name).TrimStart('.');
        return s_mime.TryGetValue(extension, out var mime) ? mime : null;
    }

    public static bool IsImage(string name) => MimeOf(name) is { } mime && mime.StartsWith("image/", StringComparison.Ordinal) && mime != "image/svg+xml";

    public static List<string> ParseAccept(string? accept) =>
        (accept ?? string.Empty).Split(',').Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0).ToList();

    /// <summary>Arc's rule: ".ext" matches the name's end, "type/*" the MIME prefix, anything else the exact MIME type.</summary>
    public static bool Matches(string name, List<string> accepted)
    {
        if (accepted.Count == 0)
        {
            return true;
        }
        var lower = name.ToLowerInvariant();
        var mime = MimeOf(name) ?? string.Empty;
        foreach (var type in accepted)
        {
            if (type.StartsWith('.') ? lower.EndsWith(type, StringComparison.Ordinal)
                : type.EndsWith("/*", StringComparison.Ordinal) ? mime.StartsWith(type[..^1], StringComparison.Ordinal)
                : mime == type)
            {
                return true;
            }
        }
        return false;
    }

    public static IReadOnlyList<FilePickerFileType>? PickerFilter(string? accept)
    {
        var accepted = ParseAccept(accept);
        if (accepted.Count == 0)
        {
            return null;
        }
        var patterns = new List<string>();
        var mimes = new List<string>();
        foreach (var type in accepted)
        {
            if (type.StartsWith('.'))
            {
                patterns.Add("*" + type);
                continue;
            }
            mimes.Add(type);
            var prefix = type.EndsWith("/*", StringComparison.Ordinal) ? type[..^1] : null;
            foreach (var (extension, mime) in s_mime)
            {
                if (prefix is not null ? mime.StartsWith(prefix, StringComparison.Ordinal) : mime == type)
                {
                    patterns.Add("*." + extension);
                }
            }
        }
        return new[] { new FilePickerFileType("Accepted files") { Patterns = patterns.Distinct().ToList(), MimeTypes = mimes } };
    }
}
