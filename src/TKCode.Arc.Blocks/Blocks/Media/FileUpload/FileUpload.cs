// Ported from Arc's free file-upload block (MIT, Copyright (c) 2026 Elia Kuratli): file-upload.tsx.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A complete file selection flow: a dropzone that takes dropped files or opens the file picker, validation of type
/// (<see cref="Accept"/>) and size (<see cref="MaxSize"/>), one upload per accepted file through <see cref="Uploader"/>
/// with a progress bar and a counted percentage, and Retry and Remove on every row. Rejected files stay listed with their
/// error; removing a file cancels its upload. A polite status line says what was added, rejected, uploaded, failed or
/// removed.
/// </summary>
/// <remarks>
/// Without an uploader files are only listed and validated (an accepted file shows a check). Uploads run as files are
/// added, with no limit on how many run at once, and the list is not virtualized: queue large batches yourself.
/// </remarks>
[TemplatePart(DropzonePart, typeof(FileUploadDropzone))]
[TemplatePart(LabelPart, typeof(FileUploadSwap))]
[TemplatePart(StatusFramePart, typeof(FileUploadCollapse))]
[TemplatePart(StatusPart, typeof(FileUploadSwap))]
[TemplatePart(ListPart, typeof(Border))]
[TemplatePart(AnnouncerPart, typeof(FileUploadAnnouncer))]
[PseudoClasses(DraggingPseudoClass, ZoneHoverPseudoClass, ZonePressedPseudoClass)]
public class FileUpload : TemplatedControl
{
    public const string DropzonePart = "PART_Dropzone";
    public const string LabelPart = "PART_Label";
    public const string StatusFramePart = "PART_StatusFrame";
    public const string StatusPart = "PART_Status";
    public const string ListPart = "PART_List";
    public const string AnnouncerPart = "PART_Announcer";
    public const string DraggingPseudoClass = ":dragging";
    public const string ZoneHoverPseudoClass = ":zonehover";
    public const string ZonePressedPseudoClass = ":zonepressed";

    /// <summary>Accepted types, comma separated: extensions (".pdf"), MIME types ("application/zip") and wildcards ("image/*"). Rejected files stay listed with an error. Empty accepts everything.</summary>
    public static readonly StyledProperty<string?> AcceptProperty =
        AvaloniaProperty.Register<FileUpload, string?>(nameof(Accept));

    /// <summary>The largest accepted file in bytes; larger files are listed with an error. Null or 0 sets no limit.</summary>
    public static readonly StyledProperty<long?> MaxSizeProperty =
        AvaloniaProperty.Register<FileUpload, long?>(nameof(MaxSize));

    /// <summary>Whether several files can be added; when false a new file replaces the list (and cancels running uploads).</summary>
    public static readonly StyledProperty<bool> MultipleProperty =
        AvaloniaProperty.Register<FileUpload, bool>(nameof(Multiple), true);

    /// <summary>The dropzone's heading.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FileUpload, string>(nameof(Label), "Upload files");

    /// <summary>The dropzone's help text; it also describes the dropzone to screen readers.</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<FileUpload, string>(nameof(Description), "Drop files here or browse from your device.");

    /// <summary>The text at the dropzone's end.</summary>
    public static readonly StyledProperty<string> BrowseLabelProperty =
        AvaloniaProperty.Register<FileUpload, string>(nameof(BrowseLabel), "Browse");

    /// <summary>The file list, newest last. Two-way: the control replaces it when files are added or removed.</summary>
    public static readonly StyledProperty<IReadOnlyList<FileUploadItem>> FilesProperty =
        AvaloniaProperty.Register<FileUpload, IReadOnlyList<FileUploadItem>>(nameof(Files), Array.Empty<FileUploadItem>(),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Uploads one accepted file: report 0 to 100 through the progress, return when done, throw to mark the file failed.
    /// The token is cancelled when the file is removed or replaced, or the control leaves the tree. Null only lists files.
    /// </summary>
    public static readonly StyledProperty<Func<FileUploadItem, IProgress<double>, CancellationToken, Task>?> UploaderProperty =
        AvaloniaProperty.Register<FileUpload, Func<FileUploadItem, IProgress<double>, CancellationToken, Task>?>(nameof(Uploader));

    /// <summary>True while files are dragged over the dropzone.</summary>
    public static readonly DirectProperty<FileUpload, bool> IsDraggingProperty =
        AvaloniaProperty.RegisterDirect<FileUpload, bool>(nameof(IsDragging), o => o.IsDragging);

    /// <summary>The last status message ("2 files added.", "report.pdf uploaded."); empty until the first one.</summary>
    public static readonly DirectProperty<FileUpload, string> StatusProperty =
        AvaloniaProperty.RegisterDirect<FileUpload, string>(nameof(Status), o => o.Status);

    /// <summary>Raised after files were added or removed, with the new list.</summary>
    public static readonly RoutedEvent<FileUploadFilesChangedEventArgs> FilesChangedEvent =
        RoutedEvent.Register<FileUpload, FileUploadFilesChangedEventArgs>(nameof(FilesChanged), RoutingStrategies.Bubble);

    private readonly Dictionary<FileUploadItem, (FileUploadCollapse Host, FileUploadRow Row)> _rows = new();
    private readonly StackPanel _listPanel = new();
    private FileUploadDropzone? _dropzone;
    private FileUploadSwap? _label;
    private FileUploadCollapse? _statusFrame;
    private FileUploadSwap? _status;
    private Border? _list;
    private FileUploadAnnouncer? _announcer;
    private MotionValue<double>? _listInset;
    private bool _isDragging;
    private string _statusText = string.Empty;
    private bool _zonePressed;

    static FileUpload()
    {
        BlockFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<FileUpload>(false);
    }

    /// <inheritdoc cref="AcceptProperty"/>
    public string? Accept { get => GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }

    /// <inheritdoc cref="MaxSizeProperty"/>
    public long? MaxSize { get => GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }

    /// <inheritdoc cref="MultipleProperty"/>
    public bool Multiple { get => GetValue(MultipleProperty); set => SetValue(MultipleProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="BrowseLabelProperty"/>
    public string BrowseLabel { get => GetValue(BrowseLabelProperty); set => SetValue(BrowseLabelProperty, value); }

    /// <inheritdoc cref="FilesProperty"/>
    public IReadOnlyList<FileUploadItem> Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }

    /// <inheritdoc cref="UploaderProperty"/>
    public Func<FileUploadItem, IProgress<double>, CancellationToken, Task>? Uploader
    {
        get => GetValue(UploaderProperty);
        set => SetValue(UploaderProperty, value);
    }

    /// <inheritdoc cref="IsDraggingProperty"/>
    public bool IsDragging { get => _isDragging; private set => SetAndRaise(IsDraggingProperty, ref _isDragging, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public string Status { get => _statusText; private set => SetAndRaise(StatusProperty, ref _statusText, value); }

    /// <inheritdoc cref="FilesChangedEvent"/>
    public event EventHandler<FileUploadFilesChangedEventArgs>? FilesChanged
    {
        add => AddHandler(FilesChangedEvent, value);
        remove => RemoveHandler(FilesChangedEvent, value);
    }

    internal FileUploadAnnouncer? Announcer => _announcer;

    internal FileUploadDropzone? Dropzone => _dropzone;

    internal FileUploadRow? RowOf(FileUploadItem item) => _rows.TryGetValue(item, out var entry) ? entry.Row : null;

    internal FileUploadCollapse? RowHostOf(FileUploadItem item) => _rows.TryGetValue(item, out var entry) ? entry.Host : null;

    /* ----------------------------------------------------------------------------------------------------------
     * Public API
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Opens the platform's file picker (filtered by <see cref="Accept"/>) and adds the chosen files.</summary>
    public async Task BrowseAsync()
    {
        if (!IsEffectivelyEnabled || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }
        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = Multiple,
                Title = Label,
                FileTypeFilter = FileUploadTypes.PickerFilter(Accept)
            });
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
    }

    /// <summary>Adds picked or dropped files: reads their sizes, then validates, lists and uploads them like <see cref="AddFiles"/>.</summary>
    public async Task AddFilesAsync(IEnumerable<IStorageItem> files)
    {
        var incoming = new List<FileUploadItem>();
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
            incoming.Add(new FileUploadItem(file, file.Name, size, modified));
        }
        if (incoming.Count > 0)
        {
            AddFiles(incoming);
        }
    }

    /// <summary>
    /// Validates the items against <see cref="Accept"/> and <see cref="MaxSize"/> (setting <see cref="FileUploadItem.Error"/>),
    /// adds them (replacing the list when <see cref="Multiple"/> is false), announces the result and uploads the accepted ones.
    /// </summary>
    public void AddFiles(IEnumerable<FileUploadItem> items)
    {
        var incoming = Multiple ? items.ToList() : items.Take(1).ToList();
        var accepted = FileUploadTypes.ParseAccept(Accept);
        var maxSize = MaxSize ?? 0;
        foreach (var item in incoming)
        {
            item.Error = !FileUploadTypes.Matches(item.Name, accepted)
                ? "This file type is not accepted."
                : maxSize > 0 && item.Size > maxSize
                    ? $"File is larger than {FileUploadTypes.FormatSize(maxSize)}."
                    : null;
        }
        if (!Multiple)
        {
            foreach (var item in Files)
            {
                CancelUpload(item);
            }
        }
        Update(Multiple ? [.. Files, .. incoming] : incoming);
        var invalid = incoming.Count(item => item.Error is not null);
        var valid = incoming.Count - invalid;
        var added = $"{valid} file{(valid == 1 ? "" : "s")} added.";
        var attention = $"{invalid} file{(invalid == 1 ? " needs" : "s need")} attention.";
        SetStatus(invalid == 0 ? added : valid > 0 ? $"{added} {attention}" : attention);
        foreach (var item in incoming.Where(item => item.Error is null))
        {
            StartUpload(item);
        }
    }

    /// <summary>Uploads the item again from an empty bar (the row's Retry button).</summary>
    public void Retry(FileUploadItem item) => StartUpload(item);

    /// <summary>Removes the item and cancels its upload; focus moves to the next row's Remove, else the previous one's, else the dropzone.</summary>
    public void Remove(FileUploadItem item)
    {
        var files = Files;
        var index = IndexOf(files, item);
        if (index < 0)
        {
            return;
        }
        CancelUpload(item);
        var neighbor = index + 1 < files.Count ? files[index + 1] : index > 0 ? files[index - 1] : null;
        Update(files.Where(file => !ReferenceEquals(file, item)).ToList());
        SetStatus($"{item.Name} removed.");
        // Remove clicked with the mouse moves focus on without the keyboard ring.
        var method = BlockFocusModality.For(this);
        Dispatcher.UIThread.Post(() =>
        {
            Control? target = neighbor is not null ? RowOf(neighbor)?.RemoveButton : _dropzone;
            target?.Focus(method);
        });
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Uploads
     * -------------------------------------------------------------------------------------------------------- */

    private void StartUpload(FileUploadItem item)
    {
        if (Uploader is not { } uploader)
        {
            return;
        }
        item.Upload?.Cancel();
        var controller = new CancellationTokenSource();
        item.Upload = controller;
        item.Progress = 0;
        item.Status = FileUploadStatus.Uploading;
        var progress = new UploadProgress(item, controller.Token);
        _ = Run();

        async Task Run()
        {
            Exception? failure = null;
            try
            {
                await uploader(item, progress, controller.Token);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            if (ReferenceEquals(item.Upload, controller))
            {
                item.Upload = null;
            }
            if (controller.IsCancellationRequested)
            {
                controller.Dispose();
                return;
            }
            controller.Dispose();
            if (failure is null)
            {
                item.Progress = 100;
                item.Status = FileUploadStatus.Done;
                SetStatus($"{item.Name} uploaded.");
            }
            else
            {
                item.Status = FileUploadStatus.Failed;
                SetStatus($"{item.Name} could not be uploaded.");
            }
        }
    }

    private static void CancelUpload(FileUploadItem item)
    {
        if (item.Upload is { } upload)
        {
            item.Upload = null;
            upload.Cancel();
        }
    }

    /// <summary>Progress reports from any thread, applied on the UI thread, clamped to 0..100 and never going back.</summary>
    private sealed class UploadProgress(FileUploadItem item, CancellationToken token) : IProgress<double>
    {
        public void Report(double value)
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

        private void Apply(double value)
        {
            if (token.IsCancellationRequested || item.Status != FileUploadStatus.Uploading || double.IsNaN(value))
            {
                return;
            }
            item.Progress = Math.Min(Math.Max(value, item.Progress), 100);
        }
    }

    /* ----------------------------------------------------------------------------------------------------------
     * List and status
     * -------------------------------------------------------------------------------------------------------- */

    private void Update(IReadOnlyList<FileUploadItem> next)
    {
        SetCurrentValue(FilesProperty, next);
        RaiseEvent(new FileUploadFilesChangedEventArgs(FilesChangedEvent, next));
    }

    private static int IndexOf(IReadOnlyList<FileUploadItem> files, FileUploadItem item)
    {
        for (var i = 0; i < files.Count; i++)
        {
            if (ReferenceEquals(files[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    private void SetStatus(string text)
    {
        Status = text;
        _announcer?.Announce(text);
        if (_status is not null && !string.IsNullOrEmpty(text))
        {
            // Arc keys the swap by the text: a new message rises in, the same message again stays put.
            if (_status.Content is not TextBlock { Text: var shown } || shown != text)
            {
                _status.Content = new TextBlock { Text = text, LineHeight = 16.8, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            }
        }
        if (_statusFrame is not null)
        {
            _statusFrame.IsOpen = !string.IsNullOrEmpty(Status);
        }
    }

    private void SyncRows()
    {
        var files = Files ?? Array.Empty<FileUploadItem>();
        var present = new HashSet<FileUploadItem>(files);
        foreach (var (item, entry) in _rows.ToArray())
        {
            if (present.Contains(item) || !entry.Host.IsOpen)
            {
                continue;
            }
            // Rows close their own height, so the list closes the gap when a file is removed.
            var host = entry.Host;
            host.IsHitTestVisible = false;
            host.Closed += OnClosed;
            host.IsOpen = false;

            void OnClosed(object? sender, EventArgs e)
            {
                host.Closed -= OnClosed;
                if (_rows.TryGetValue(item, out var current) && ReferenceEquals(current.Host, host) && !host.IsOpen)
                {
                    _rows.Remove(item);
                    current.Row.Detach();
                    _listPanel.Children.Remove(host);
                }
            }
        }
        var animate = IsLoaded;
        var order = 0;
        foreach (var item in files)
        {
            if (!_rows.TryGetValue(item, out var entry) || !entry.Host.IsOpen)
            {
                if (entry.Host is not null)
                {
                    entry.Row.Detach();
                    _listPanel.Children.Remove(entry.Host);
                }
                var row = new FileUploadRow(this, item);
                var host = new FileUploadCollapse { Child = row };
                entry = (host, row);
                _rows[item] = entry;
                _listPanel.Children.Insert(Math.Min(PanelIndexFor(order), _listPanel.Children.Count), host);
                if (animate)
                {
                    host.Enter(deferUntilLoaded: true);
                }
            }
            order++;
        }
        UpdateListInset();
        if (_list is not null)
        {
            AutomationProperties.SetAccessibilityView(_list, files.Count == 0 ? AccessibilityView.Raw : AccessibilityView.Content);
        }
    }

    private int PanelIndexFor(int order)
    {
        // Rows that are closing keep their place; a new row goes after the open row before it in the file list.
        var open = 0;
        for (var i = 0; i < _listPanel.Children.Count; i++)
        {
            if (_listPanel.Children[i] is FileUploadCollapse { IsOpen: true })
            {
                if (open == order)
                {
                    return i;
                }
                open++;
            }
        }
        return _listPanel.Children.Count;
    }

    private void UpdateListInset()
    {
        if (_list is null)
        {
            return;
        }
        var target = Files.Count > 0 ? 6 : 0;
        _listInset ??= CreateListInset();
        if (!IsLoaded)
        {
            _listInset.Jump(target);
            _list.Padding = new Thickness(0, target, 0, 0);
            return;
        }
        _ = _listInset.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private MotionValue<double> CreateListInset()
    {
        var value = new MotionValue<double>(this, _list?.Padding.Top ?? 0, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_list is not null)
            {
                _list.Padding = new Thickness(0, Math.Max(0, v), 0, 0);
            }
        };
        return value;
    }

    private void UpdateLabel()
    {
        if (_label is null)
        {
            return;
        }
        // While a file hovers over the target the label says what releasing it will do.
        var text = IsDragging ? $"Drop to add {(Multiple ? "files" : "a file")}" : Label;
        if (_label.Content is TextBlock { Text: var shown } && shown == text)
        {
            return;
        }
        _label.Content = new TextBlock { Text = text, LineHeight = 19.6 };
    }

    private void UpdateDropzoneAutomation()
    {
        if (_dropzone is null)
        {
            return;
        }
        AutomationProperties.SetName(_dropzone, Label);
        AutomationProperties.SetHelpText(_dropzone, Description);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Template and input
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_dropzone is not null)
        {
            _dropzone.KeyDown -= OnZoneKeyDown;
            _dropzone.PointerPressed -= OnZonePointerPressed;
            _dropzone.PointerReleased -= OnZonePointerReleased;
            _dropzone.PointerCaptureLost -= OnZoneCaptureLost;
            _dropzone.PointerEntered -= OnZonePointerEntered;
            _dropzone.PointerExited -= OnZonePointerExited;
            _dropzone.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _dropzone.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _dropzone.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _dropzone.RemoveHandler(DragDrop.DropEvent, OnDrop);
            _dropzone.Invoked = null;
        }
        if (_list is not null)
        {
            _list.Child = null;
        }
        _dropzone = e.NameScope.Find<FileUploadDropzone>(DropzonePart);
        _label = e.NameScope.Find<FileUploadSwap>(LabelPart);
        _statusFrame = e.NameScope.Find<FileUploadCollapse>(StatusFramePart);
        _status = e.NameScope.Find<FileUploadSwap>(StatusPart);
        _list = e.NameScope.Find<Border>(ListPart);
        _announcer = e.NameScope.Find<FileUploadAnnouncer>(AnnouncerPart);
        if (_dropzone is not null)
        {
            _dropzone.KeyDown += OnZoneKeyDown;
            _dropzone.PointerPressed += OnZonePointerPressed;
            _dropzone.PointerReleased += OnZonePointerReleased;
            _dropzone.PointerCaptureLost += OnZoneCaptureLost;
            _dropzone.PointerEntered += OnZonePointerEntered;
            _dropzone.PointerExited += OnZonePointerExited;
            DragDrop.SetAllowDrop(_dropzone, true);
            _dropzone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _dropzone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            _dropzone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _dropzone.AddHandler(DragDrop.DropEvent, OnDrop);
            _dropzone.Invoked = () => _ = BrowseAsync();
            UpdateDropzoneAutomation();
        }
        if (_list is not null)
        {
            _list.Child = _listPanel;
            AutomationProperties.SetName(_list, "Selected files");
        }
        _label?.SetValue(FileUploadSwap.ContentProperty, null);
        UpdateLabel();
        if (_status is not null && !string.IsNullOrEmpty(Status))
        {
            _status.Content = new TextBlock { Text = Status, LineHeight = 16.8, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        }
        if (_statusFrame is not null)
        {
            _statusFrame.IsOpen = !string.IsNullOrEmpty(Status);
        }
        _listInset = null;
        SyncRows();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FilesProperty)
        {
            SyncRows();
        }
        else if (change.Property == LabelProperty || change.Property == MultipleProperty)
        {
            UpdateLabel();
            UpdateDropzoneAutomation();
        }
        else if (change.Property == DescriptionProperty)
        {
            UpdateDropzoneAutomation();
        }
        else if (change.Property == IsDraggingProperty)
        {
            PseudoClasses.Set(DraggingPseudoClass, IsDragging);
            UpdateLabel();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            IsDragging = false;
            SetZonePressed(false);
            PseudoClasses.Set(ZoneHoverPseudoClass, false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var item in Files)
        {
            CancelUpload(item);
        }
        IsDragging = false;
    }

    private void OnZoneKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsEffectivelyEnabled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            _ = BrowseAsync();
        }
    }

    private void OnZonePointerEntered(object? sender, PointerEventArgs e)
    {
        // Arc's hover lives under (hover: hover) and (pointer: fine): touch never hovers.
        PseudoClasses.Set(ZoneHoverPseudoClass, IsEffectivelyEnabled && e.Pointer.Type != PointerType.Touch);
    }

    private void OnZonePointerExited(object? sender, PointerEventArgs e) => PseudoClasses.Set(ZoneHoverPseudoClass, false);

    private void OnZonePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || !e.GetCurrentPoint(_dropzone).Properties.IsLeftButtonPressed)
        {
            return;
        }
        SetZonePressed(true);
        e.Pointer.Capture(_dropzone);
        e.Handled = true;
    }

    private void OnZonePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_zonePressed)
        {
            return;
        }
        SetZonePressed(false);
        e.Pointer.Capture(null);
        e.Handled = true;
        if (_dropzone is not null && new Rect(_dropzone.Bounds.Size).Contains(e.GetPosition(_dropzone)))
        {
            _ = BrowseAsync();
        }
    }

    private void OnZoneCaptureLost(object? sender, PointerCaptureLostEventArgs e) => SetZonePressed(false);

    private void SetZonePressed(bool value)
    {
        _zonePressed = value;
        PseudoClasses.Set(ZonePressedPseudoClass, value);
    }

    private bool Accepts(DragEventArgs e) => IsEffectivelyEnabled && e.DataTransfer.Contains(DataFormat.File);

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.DragEffects = Accepts(e) ? DragDropEffects.Copy : DragDropEffects.None;
        if (IsEffectivelyEnabled)
        {
            IsDragging = true;
        }
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = Accepts(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Moving between the dropzone's own parts raises leave and enter; only leaving its bounds ends the drag (Arc counts depth).
        if (_dropzone is not null && new Rect(_dropzone.Bounds.Size).Contains(e.GetPosition(_dropzone)))
        {
            return;
        }
        IsDragging = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        IsDragging = false;
        e.Handled = true;
        if (!Accepts(e))
        {
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        var files = e.DataTransfer.TryGetFiles();
        if (files is { Length: > 0 })
        {
            await AddFilesAsync(files);
        }
    }
}
