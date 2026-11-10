using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// An avatar field: a circle that shows the person's picture, with a button to pick one and a drop target for a file.
/// A new picture opens a framing step where it is moved and zoomed under a circle (drag, wheel, pinch, the slider or the
/// keyboard), then Save cuts a square of <see cref="OutputSize"/> pixels from it. With an <see cref="UploadHandler"/> the
/// field shows the new picture at once with a progress ring until the upload ends, offers Cancel while it runs and Retry
/// when it fails; without one, the picture is applied at once and <see cref="Cropped"/> hands it to the app.
/// </summary>
[TemplatePart(StagePart, typeof(AvatarUploadStage))]
[TemplatePart(RingPart, typeof(ArcRing))]
[TemplatePart(CropperPart, typeof(AvatarUploadCropper))]
[TemplatePart(PickPart, typeof(Button))]
[TemplatePart(RemovePart, typeof(Button))]
[TemplatePart(RetryPart, typeof(Button))]
[TemplatePart(CancelUploadPart, typeof(Button))]
[TemplatePart(SavePart, typeof(Button))]
[TemplatePart(CancelEditPart, typeof(Button))]
[TemplatePart(ZoomInPart, typeof(Button))]
[TemplatePart(ZoomOutPart, typeof(Button))]
[TemplatePart(ZoomPart, typeof(Slider))]
[PseudoClasses(EmptyPseudoClass, EditingPseudoClass, UploadingPseudoClass, DonePseudoClass, FailedPseudoClass, DragOverPseudoClass, ErrorPseudoClass)]
public class AvatarUpload : TemplatedControl
{
    public const string StagePart = "PART_Stage";
    public const string RingPart = "PART_Ring";
    public const string CropperPart = "PART_Cropper";
    public const string PickPart = "PART_Pick";
    public const string RemovePart = "PART_Remove";
    public const string RetryPart = "PART_Retry";
    public const string CancelUploadPart = "PART_CancelUpload";
    public const string SavePart = "PART_Save";
    public const string CancelEditPart = "PART_CancelEdit";
    public const string ZoomInPart = "PART_ZoomIn";
    public const string ZoomOutPart = "PART_ZoomOut";
    public const string ZoomPart = "PART_Zoom";
    public const string EmptyPseudoClass = ":empty";
    public const string EditingPseudoClass = ":editing";
    public const string UploadingPseudoClass = ":uploading";
    public const string DonePseudoClass = ":done";
    public const string FailedPseudoClass = ":failed";
    public const string DragOverPseudoClass = ":dragover";
    public const string ErrorPseudoClass = ":error";

    private static readonly string[] s_extensions = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"];

    /// <summary>Larger pictures are scaled down to this many pixels on their long side before framing.</summary>
    internal const int MaxWorkingSize = 2048;

    #region Properties

    /// <summary>The picture shown in the circle; null for none. Two-way: a saved picture is written back.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<AvatarUpload, IImage?>(nameof(Source), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The field's title beside the circle.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AvatarUpload, string?>(nameof(Label), "Profile photo");

    /// <summary>The line under the title: the accepted files and the size limit.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AvatarUpload, string?>(nameof(Description), "PNG, JPG or WebP, up to 10 MB");

    /// <summary>The diameter of the circle (96px by default).</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<AvatarUpload, double>(nameof(Size), 96);

    /// <summary>The side of the saved square picture in pixels (512 by default).</summary>
    public static readonly StyledProperty<int> OutputSizeProperty =
        AvaloniaProperty.Register<AvatarUpload, int>(nameof(OutputSize), 512);

    /// <summary>How far the framing step zooms in, relative to the zoom where the picture just covers the circle (4 by default).</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<AvatarUpload, double>(nameof(MaxZoom), 4);

    /// <summary>The largest file accepted, in bytes (10 MB by default); null for no limit.</summary>
    public static readonly StyledProperty<long?> MaxFileSizeProperty =
        AvaloniaProperty.Register<AvatarUpload, long?>(nameof(MaxFileSize), 10L * 1024 * 1024);

    /// <summary>
    /// Uploads a framed picture: report progress from 0 to 1 and honor the token (Cancel upload). Without a handler the
    /// picture is applied as soon as it is saved.
    /// </summary>
    public static readonly StyledProperty<Func<AvatarUploadRequest, IProgress<double>, CancellationToken, Task>?> UploadHandlerProperty =
        AvaloniaProperty.Register<AvatarUpload, Func<AvatarUploadRequest, IProgress<double>, CancellationToken, Task>?>(nameof(UploadHandler));

    /// <summary>Every word the field shows or says (English by default).</summary>
    public static readonly StyledProperty<AvatarUploadLabels> LabelsProperty =
        AvaloniaProperty.Register<AvatarUpload, AvatarUploadLabels>(nameof(Labels), AvatarUploadLabels.Default);

    /// <summary>What the field is doing.</summary>
    public static readonly DirectProperty<AvatarUpload, AvatarUploadState> StateProperty =
        AvaloniaProperty.RegisterDirect<AvatarUpload, AvatarUploadState>(nameof(State), o => o.State);

    /// <summary>The upload's progress from 0 to 1.</summary>
    public static readonly DirectProperty<AvatarUpload, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<AvatarUpload, double>(nameof(Progress), o => o.Progress);

    /// <summary>The status line under the title (the description, the progress, or what went wrong).</summary>
    public static readonly DirectProperty<AvatarUpload, string?> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<AvatarUpload, string?>(nameof(StatusText), o => o.StatusText);

    /// <summary>The last problem: a refused file or a failed upload; null when there is none.</summary>
    public static readonly DirectProperty<AvatarUpload, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<AvatarUpload, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>True while a file is dragged over the field.</summary>
    public static readonly DirectProperty<AvatarUpload, bool> IsDragOverProperty =
        AvaloniaProperty.RegisterDirect<AvatarUpload, bool>(nameof(IsDragOver), o => o.IsDragOver);

    /// <summary>Raised when a framed picture is saved, before it uploads.</summary>
    public static readonly RoutedEvent<AvatarUploadCroppedEventArgs> CroppedEvent =
        RoutedEvent.Register<AvatarUpload, AvatarUploadCroppedEventArgs>(nameof(Cropped), RoutingStrategies.Bubble);

    /// <summary>Raised when a file is refused (type, size or unreadable).</summary>
    public static readonly RoutedEvent<AvatarUploadRejectedEventArgs> FileRejectedEvent =
        RoutedEvent.Register<AvatarUpload, AvatarUploadRejectedEventArgs>(nameof(FileRejected), RoutingStrategies.Bubble);

    /// <summary>Raised when the picture is removed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RemovedEvent =
        RoutedEvent.Register<AvatarUpload, RoutedEventArgs>(nameof(Removed), RoutingStrategies.Bubble);

    /// <summary>The picture shown in the circle; null for none. Two-way: a saved picture is written back.</summary>
    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>The field's title beside the circle.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The line under the title: the accepted files and the size limit.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The diameter of the circle (96px by default).</summary>
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>The side of the saved square picture in pixels (512 by default).</summary>
    public int OutputSize { get => GetValue(OutputSizeProperty); set => SetValue(OutputSizeProperty, value); }

    /// <summary>How far the framing step zooms in, relative to the zoom where the picture just covers the circle (4 by default).</summary>
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <summary>The largest file accepted, in bytes (10 MB by default); null for no limit.</summary>
    public long? MaxFileSize { get => GetValue(MaxFileSizeProperty); set => SetValue(MaxFileSizeProperty, value); }

    /// <summary>
    /// Uploads a framed picture: report progress from 0 to 1 and honor the token (Cancel upload). Without a handler the
    /// picture is applied as soon as it is saved.
    /// </summary>
    public Func<AvatarUploadRequest, IProgress<double>, CancellationToken, Task>? UploadHandler
    {
        get => GetValue(UploadHandlerProperty);
        set => SetValue(UploadHandlerProperty, value);
    }

    /// <summary>Every word the field shows or says (English by default).</summary>
    public AvatarUploadLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>What the field is doing.</summary>
    public AvatarUploadState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <summary>The upload's progress from 0 to 1.</summary>
    public double Progress { get => _progress; private set => SetAndRaise(ProgressProperty, ref _progress, value); }

    /// <summary>The status line under the title (the description, the progress, or what went wrong).</summary>
    public string? StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }

    /// <summary>The last problem: a refused file or a failed upload; null when there is none.</summary>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    /// <summary>True while a file is dragged over the field.</summary>
    public bool IsDragOver { get => _isDragOver; private set => SetAndRaise(IsDragOverProperty, ref _isDragOver, value); }

    /// <summary>Raised when a framed picture is saved, before it uploads.</summary>
    public event EventHandler<AvatarUploadCroppedEventArgs>? Cropped
    {
        add => AddHandler(CroppedEvent, value);
        remove => RemoveHandler(CroppedEvent, value);
    }

    /// <summary>Raised when a file is refused (type, size or unreadable).</summary>
    public event EventHandler<AvatarUploadRejectedEventArgs>? FileRejected
    {
        add => AddHandler(FileRejectedEvent, value);
        remove => RemoveHandler(FileRejectedEvent, value);
    }

    /// <summary>Raised when the picture is removed.</summary>
    public event EventHandler<RoutedEventArgs>? Removed
    {
        add => AddHandler(RemovedEvent, value);
        remove => RemoveHandler(RemovedEvent, value);
    }

    #endregion

    private AvatarUploadState _state;
    private double _progress;
    private string? _statusText;
    private string? _errorMessage;
    private bool _isDragOver;
    private AvatarUploadStage? _stage;
    private ArcRing? _ring;
    private AvatarUploadCropper? _cropper;
    private Button? _pick;
    private Button? _remove;
    private Button? _retry;
    private Button? _cancelUpload;
    private Button? _save;
    private Button? _cancelEdit;
    private Button? _zoomIn;
    private Button? _zoomOut;
    private Slider? _zoom;
    private Bitmap? _working;
    private string? _workingName;
    private AvatarUploadRequest? _pendingUpload;
    private IImage? _beforeUpload;
    private CancellationTokenSource? _uploadCancel;
    private int _uploadGeneration;
    private bool _settingSource;

    static AvatarUpload()
    {
        FocusableProperty.OverrideDefaultValue<AvatarUpload>(false);
        SpecialFocusModality.Watch();
    }

    public AvatarUpload()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        UpdateState();
    }

    internal AvatarUploadCropper? Cropper => _cropper;

    internal Bitmap? WorkingImage => _working;

    #region Public actions

    /// <summary>Opens the system file picker for one image and frames it.</summary>
    public async Task BrowseAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is not { CanOpen: true } || State is AvatarUploadState.Uploading)
        {
            return;
        }
        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = Label,
                FileTypeFilter = [new FilePickerFileType(Labels.Picture)
                {
                    Patterns = s_extensions.Select(e => "*" + e).ToArray(),
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/gif", "image/bmp"],
                    AppleUniformTypeIdentifiers = ["public.image"]
                }]
            });
        }
        catch (Exception)
        {
            // A platform without a picker (or a refused one) changes nothing.
            return;
        }
        if (files.Count > 0)
        {
            await LoadAsync(files[0]);
        }
    }

    /// <summary>Checks <paramref name="file"/> (type and size), reads it and opens the framing step.</summary>
    public async Task LoadAsync(IStorageFile file)
    {
        var name = file.Name;
        if (!IsAcceptedName(name))
        {
            Reject(name, AvatarUploadRejection.Type);
            return;
        }
        try
        {
            var properties = await file.GetBasicPropertiesAsync();
            if (MaxFileSize is { } limit && properties.Size is { } size && (long)size > limit)
            {
                Reject(name, AvatarUploadRejection.Size);
                return;
            }
            await using var stream = await file.OpenReadAsync();
            await LoadAsync(stream, name);
        }
        catch (Exception)
        {
            Reject(name, AvatarUploadRejection.Unreadable);
        }
    }

    /// <summary>Reads an image from <paramref name="stream"/> (off the UI thread) and opens the framing step.</summary>
    public async Task LoadAsync(Stream stream, string? fileName = null)
    {
        var name = fileName ?? Labels.Picture;
        if (MaxFileSize is { } limit && stream.CanSeek && stream.Length > limit)
        {
            Reject(name, AvatarUploadRejection.Size);
            return;
        }
        Bitmap? bitmap;
        try
        {
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;
            bitmap = await Task.Run(() => Decode(buffer));
        }
        catch (Exception)
        {
            bitmap = null;
        }
        if (bitmap is null)
        {
            Reject(name, AvatarUploadRejection.Unreadable);
            return;
        }
        Edit(bitmap, name);
    }

    /// <summary>Opens the framing step for a picture the app already has.</summary>
    public void Edit(Bitmap image, string? fileName = null)
    {
        if (State is AvatarUploadState.Uploading)
        {
            CancelUpload();
        }
        _working = image;
        _workingName = fileName;
        ErrorMessage = null;
        _cropper?.SetImage(image);
        if (_cropper is not null)
        {
            _cropper.MaxZoom = Math.Max(1, MaxZoom);
        }
        State = AvatarUploadState.Editing;
        UpdateState();
        Dispatcher.UIThread.Post(() =>
        {
            if (State == AvatarUploadState.Editing)
            {
                _cropper?.Focus(SpecialFocusModality.For(this));
            }
        }, DispatcherPriority.Input);
    }

    /// <summary>Cuts the framed square and applies it (or starts the upload when there is an <see cref="UploadHandler"/>).</summary>
    public Task SaveAsync()
    {
        if (State != AvatarUploadState.Editing || _working is null || _cropper is null)
        {
            return Task.CompletedTask;
        }
        var source = _cropper.CropRect();
        var image = Crop(_working, source, Math.Max(16, OutputSize));
        var request = new AvatarUploadRequest(image, source, _workingName);
        _working = null;
        _cropper.SetImage(null);
        RaiseEvent(new AvatarUploadCroppedEventArgs(CroppedEvent, request));
        FocusPick();
        if (UploadHandler is null)
        {
            SetSource(image);
            State = AvatarUploadState.Done;
            ErrorMessage = null;
            UpdateState();
            Announce(Labels.Uploaded);
            return Task.CompletedTask;
        }
        _beforeUpload = Source;
        return UploadAsync(request);
    }

    /// <summary>Leaves the framing step without changing the picture.</summary>
    public void CancelEdit()
    {
        if (State != AvatarUploadState.Editing)
        {
            return;
        }
        _working = null;
        _cropper?.SetImage(null);
        State = Source is null ? AvatarUploadState.Empty : AvatarUploadState.Done;
        UpdateState();
        FocusPick();
    }

    /// <summary>Removes the picture.</summary>
    public void Remove()
    {
        if (State is AvatarUploadState.Uploading)
        {
            CancelUpload();
        }
        if (Source is null && State != AvatarUploadState.Failed)
        {
            return;
        }
        _pendingUpload = null;
        _beforeUpload = null;
        SetSource(null);
        State = AvatarUploadState.Empty;
        ErrorMessage = null;
        UpdateState();
        Announce(Labels.Removed);
        RaiseEvent(new RoutedEventArgs(RemovedEvent));
        FocusPick();
    }

    /// <summary>Tries a failed upload again.</summary>
    public Task RetryAsync()
    {
        if (State != AvatarUploadState.Failed || _pendingUpload is null || UploadHandler is null)
        {
            return Task.CompletedTask;
        }
        return UploadAsync(_pendingUpload);
    }

    /// <summary>Stops an upload, or gives up a failed one, and goes back to the picture before it.</summary>
    public void CancelUpload()
    {
        if (State is not (AvatarUploadState.Uploading or AvatarUploadState.Failed))
        {
            return;
        }
        ++_uploadGeneration;
        _uploadCancel?.Cancel();
        _uploadCancel = null;
        _pendingUpload = null;
        SetSource(_beforeUpload);
        _beforeUpload = null;
        ErrorMessage = null;
        Progress = 0;
        State = Source is null ? AvatarUploadState.Empty : AvatarUploadState.Done;
        UpdateState();
        FocusPick();
    }

    /// <summary>
    /// Shows upload progress driven by the app (for apps that upload in <see cref="Cropped"/> rather than through an
    /// <see cref="UploadHandler"/>): the field enters the uploading state.
    /// </summary>
    public void ReportProgress(double progress)
    {
        if (State is AvatarUploadState.Editing)
        {
            return;
        }
        if (State != AvatarUploadState.Uploading)
        {
            _beforeUpload ??= Source;
            State = AvatarUploadState.Uploading;
        }
        Progress = Math.Clamp(progress, 0, 1);
        UpdateState();
    }

    /// <summary>Ends an app-driven upload successfully.</summary>
    public void CompleteUpload()
    {
        if (State is not (AvatarUploadState.Uploading or AvatarUploadState.Failed))
        {
            return;
        }
        Progress = 1;
        _pendingUpload = null;
        _beforeUpload = null;
        State = Source is null ? AvatarUploadState.Empty : AvatarUploadState.Done;
        ErrorMessage = null;
        UpdateState();
        Announce(Labels.Uploaded);
    }

    /// <summary>Ends an app-driven upload with an error; <paramref name="reason"/> is shown under the title.</summary>
    public void FailUpload(string? reason = null)
    {
        if (State is not AvatarUploadState.Uploading)
        {
            return;
        }
        ErrorMessage = string.IsNullOrEmpty(reason) ? Labels.UploadFailed : reason;
        State = AvatarUploadState.Failed;
        UpdateState();
        Announce(ErrorMessage);
    }

    #endregion

    #region Upload

    private async Task UploadAsync(AvatarUploadRequest request)
    {
        var handler = UploadHandler;
        if (handler is null)
        {
            return;
        }
        _pendingUpload = request;
        var generation = ++_uploadGeneration;
        _uploadCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _uploadCancel = cancel;
        SetSource(request.Image);
        ErrorMessage = null;
        Progress = 0;
        State = AvatarUploadState.Uploading;
        UpdateState();
        var progress = new UiProgress(this, generation);
        try
        {
            await handler(request, progress, cancel.Token);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (generation != _uploadGeneration)
            {
                return;
            }
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? Labels.UploadFailed : ex.Message;
            State = AvatarUploadState.Failed;
            UpdateState();
            Announce(ErrorMessage);
            return;
        }
        if (generation != _uploadGeneration)
        {
            return;
        }
        _uploadCancel = null;
        Progress = 1;
        _pendingUpload = null;
        _beforeUpload = null;
        State = AvatarUploadState.Done;
        UpdateState();
        Announce(Labels.Uploaded);
    }

    private sealed class UiProgress : IProgress<double>
    {
        private readonly AvatarUpload _owner;
        private readonly int _generation;

        public UiProgress(AvatarUpload owner, int generation)
        {
            _owner = owner;
            _generation = generation;
        }

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
            if (_generation == _owner._uploadGeneration && _owner.State == AvatarUploadState.Uploading)
            {
                _owner.Progress = Math.Clamp(value, 0, 1);
                _owner.UpdateState();
            }
        }
    }

    #endregion

    #region Image helpers

    internal static bool IsAcceptedName(string name)
    {
        var extension = Path.GetExtension(name);
        return s_extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static Bitmap? Decode(Stream stream)
    {
        var bitmap = new Bitmap(stream);
        var px = bitmap.PixelSize;
        var longSide = Math.Max(px.Width, px.Height);
        if (longSide <= MaxWorkingSize)
        {
            return bitmap;
        }
        var scale = (double)MaxWorkingSize / longSide;
        var scaled = bitmap.CreateScaledBitmap(new PixelSize(Math.Max(1, (int)(px.Width * scale)), Math.Max(1, (int)(px.Height * scale))));
        bitmap.Dispose();
        return scaled;
    }

    /// <summary>Draws the <paramref name="source"/> square of <paramref name="image"/> into a new square bitmap.</summary>
    internal static Bitmap Crop(Bitmap image, PixelRect source, int size)
    {
        var target = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            // Both rectangles in pixels: the source rect of DrawImage is in the bitmap's pixels.
            context.DrawImage(image, new Rect(source.X, source.Y, source.Width, source.Height), new Rect(0, 0, size, size));
        }
        return target;
    }

    private void Reject(string name, AvatarUploadRejection reason)
    {
        var labels = Labels;
        ErrorMessage = reason switch
        {
            AvatarUploadRejection.Type => labels.WrongType(name),
            AvatarUploadRejection.Size => labels.TooLarge(name, FormatSize(MaxFileSize ?? 0)),
            _ => labels.Unreadable(name)
        };
        UpdateState();
        Announce(ErrorMessage);
        RaiseEvent(new AvatarUploadRejectedEventArgs(FileRejectedEvent, name, reason));
    }

    internal static string FormatSize(long bytes)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (bytes >= 1024 * 1024)
        {
            var mb = bytes / (1024.0 * 1024);
            return (mb % 1 == 0 ? mb.ToString("0", inv) : mb.ToString("0.#", inv)) + " MB";
        }
        if (bytes >= 1024)
        {
            return (bytes / 1024.0).ToString("0", inv) + " KB";
        }
        return bytes.ToString(inv) + " B";
    }

    private void SetSource(IImage? image)
    {
        _settingSource = true;
        try
        {
            SetCurrentValue(SourceProperty, image);
        }
        finally
        {
            _settingSource = false;
        }
    }

    #endregion

    #region Template and state

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _stage = e.NameScope.Find<AvatarUploadStage>(StagePart);
        _ring = e.NameScope.Find<ArcRing>(RingPart);
        _cropper = e.NameScope.Find<AvatarUploadCropper>(CropperPart);
        _pick = e.NameScope.Find<Button>(PickPart);
        _remove = e.NameScope.Find<Button>(RemovePart);
        _retry = e.NameScope.Find<Button>(RetryPart);
        _cancelUpload = e.NameScope.Find<Button>(CancelUploadPart);
        _save = e.NameScope.Find<Button>(SavePart);
        _cancelEdit = e.NameScope.Find<Button>(CancelEditPart);
        _zoomIn = e.NameScope.Find<Button>(ZoomInPart);
        _zoomOut = e.NameScope.Find<Button>(ZoomOutPart);
        _zoom = e.NameScope.Find<Slider>(ZoomPart);
        Hook();
        if (_cropper is not null)
        {
            _cropper.MaxZoom = Math.Max(1, MaxZoom);
            _cropper.SetImage(_working);
        }
        UpdateState();
    }

    private void Hook()
    {
        if (_stage is not null)
        {
            _stage.Tapped += OnStageTapped;
        }
        if (_pick is not null)
        {
            _pick.Click += OnPickClick;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        if (_retry is not null)
        {
            _retry.Click += OnRetryClick;
        }
        if (_cancelUpload is not null)
        {
            _cancelUpload.Click += OnCancelUploadClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        if (_cancelEdit is not null)
        {
            _cancelEdit.Click += OnCancelEditClick;
        }
        if (_zoomIn is not null)
        {
            _zoomIn.Click += OnZoomInClick;
        }
        if (_zoomOut is not null)
        {
            _zoomOut.Click += OnZoomOutClick;
        }
        if (_cropper is not null)
        {
            _cropper.Committed += OnCropperCommitted;
            _cropper.PropertyChanged += OnCropperChanged;
        }
        if (_zoom is not null)
        {
            _zoom.PropertyChanged += OnZoomSliderChanged;
        }
    }

    private void Unhook()
    {
        if (_stage is not null)
        {
            _stage.Tapped -= OnStageTapped;
        }
        if (_pick is not null)
        {
            _pick.Click -= OnPickClick;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        if (_retry is not null)
        {
            _retry.Click -= OnRetryClick;
        }
        if (_cancelUpload is not null)
        {
            _cancelUpload.Click -= OnCancelUploadClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
        if (_cancelEdit is not null)
        {
            _cancelEdit.Click -= OnCancelEditClick;
        }
        if (_zoomIn is not null)
        {
            _zoomIn.Click -= OnZoomInClick;
        }
        if (_zoomOut is not null)
        {
            _zoomOut.Click -= OnZoomOutClick;
        }
        if (_cropper is not null)
        {
            _cropper.Committed -= OnCropperCommitted;
            _cropper.PropertyChanged -= OnCropperChanged;
        }
        if (_zoom is not null)
        {
            _zoom.PropertyChanged -= OnZoomSliderChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            if (!_settingSource && State is AvatarUploadState.Empty or AvatarUploadState.Done)
            {
                State = Source is null ? AvatarUploadState.Empty : AvatarUploadState.Done;
            }
            UpdateState();
        }
        else if (change.Property == LabelsProperty || change.Property == DescriptionProperty || change.Property == LabelProperty)
        {
            UpdateState();
        }
        else if (change.Property == MaxZoomProperty && _cropper is not null)
        {
            _cropper.MaxZoom = Math.Max(1, MaxZoom);
        }
    }

    private void UpdateState()
    {
        var state = State;
        PseudoClasses.Set(EmptyPseudoClass, state == AvatarUploadState.Empty);
        PseudoClasses.Set(EditingPseudoClass, state == AvatarUploadState.Editing);
        PseudoClasses.Set(UploadingPseudoClass, state == AvatarUploadState.Uploading);
        PseudoClasses.Set(DonePseudoClass, state == AvatarUploadState.Done);
        PseudoClasses.Set(FailedPseudoClass, state == AvatarUploadState.Failed);
        PseudoClasses.Set(DragOverPseudoClass, IsDragOver);
        var labels = Labels;
        StatusText = state switch
        {
            AvatarUploadState.Uploading => labels.Uploading((int)Math.Round(Progress * 100)),
            AvatarUploadState.Failed => ErrorMessage ?? labels.UploadFailed,
            _ when IsDragOver => labels.DropHint,
            _ => ErrorMessage ?? Description
        };
        if (_ring is not null)
        {
            _ring.Value = state == AvatarUploadState.Uploading ? Progress : 0;
        }
        if (_stage is not null)
        {
            AutomationProperties.SetName(_stage, labels.Picture);
        }
        if (_pick is not null)
        {
            _pick.Content = Source is null ? labels.Upload : labels.Change;
        }
        SetContent(_remove, labels.Remove);
        SetContent(_retry, labels.Retry);
        SetContent(_cancelUpload, state == AvatarUploadState.Failed ? labels.Cancel : labels.CancelUpload);
        PseudoClasses.Set(ErrorPseudoClass, state == AvatarUploadState.Failed || (ErrorMessage is not null && !IsDragOver && state != AvatarUploadState.Uploading));
        SetContent(_save, labels.Save);
        SetContent(_cancelEdit, labels.Cancel);
        if (_zoomIn is not null)
        {
            AutomationProperties.SetName(_zoomIn, labels.ZoomIn);
        }
        if (_zoomOut is not null)
        {
            AutomationProperties.SetName(_zoomOut, labels.ZoomOut);
        }
        if (_zoom is not null)
        {
            AutomationProperties.SetName(_zoom, labels.Zoom);
            _zoom.Maximum = Math.Max(1, MaxZoom);
        }
        if (_cropper is not null)
        {
            AutomationProperties.SetName(_cropper, labels.CropArea);
            AutomationProperties.SetHelpText(_cropper, labels.CropHelp);
        }
    }

    private static void SetContent(Button? button, string text)
    {
        if (button is not null && !Equals(button.Content, text))
        {
            button.Content = text;
        }
    }

    private void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        AutomationProperties.SetHelpText(this, text);
    }

    private void FocusPick()
    {
        if (!IsKeyboardFocusWithin)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            var target = State switch
            {
                AvatarUploadState.Uploading => _cancelUpload,
                AvatarUploadState.Failed => _retry,
                _ => _pick
            };
            target?.Focus(SpecialFocusModality.For(this));
        }, DispatcherPriority.Input);
    }

    #endregion

    #region Input

    private void OnStageTapped(object? sender, TappedEventArgs e)
    {
        if (State is AvatarUploadState.Empty or AvatarUploadState.Done)
        {
            _ = BrowseAsync();
        }
    }

    private void OnPickClick(object? sender, RoutedEventArgs e) => _ = BrowseAsync();

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => Remove();

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _ = RetryAsync();

    private void OnCancelUploadClick(object? sender, RoutedEventArgs e) => CancelUpload();

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveAsync();

    private void OnCancelEditClick(object? sender, RoutedEventArgs e) => CancelEdit();

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => _cropper?.StepZoom(1);

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => _cropper?.StepZoom(-1);

    private void OnZoomSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty && _cropper is not null && _zoom is not null &&
            Math.Abs(_zoom.Value - _cropper.RelativeZoom) > 1e-6)
        {
            _cropper.RelativeZoom = _zoom.Value;
        }
    }

    private void OnCropperChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AvatarUploadCropper.RelativeZoomProperty && _zoom is not null && _cropper is not null &&
            Math.Abs(_zoom.Value - _cropper.RelativeZoom) > 1e-6)
        {
            _zoom.SetCurrentValue(RangeBase.ValueProperty, _cropper.RelativeZoom);
        }
    }

    private void OnCropperCommitted(bool save)
    {
        if (save)
        {
            _ = SaveAsync();
        }
        else
        {
            CancelEdit();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && State == AvatarUploadState.Editing)
        {
            e.Handled = true;
            CancelEdit();
        }
    }

    private static bool CarriesFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    private bool AcceptsDrop => State is not (AvatarUploadState.Uploading or AvatarUploadState.Editing) && IsEffectivelyEnabled;

    private void OnDragEnter(object? sender, DragEventArgs e) => OnDragOver(sender, e);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!CarriesFiles(e) || !AcceptsDrop)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
        SetDragOver(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => SetDragOver(false);

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragOver(false);
        if (!CarriesFiles(e) || !AcceptsDrop)
        {
            return;
        }
        e.Handled = true;
        e.DragEffects = DragDropEffects.Copy;
        if (e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault() is { } file)
        {
            await LoadAsync(file);
        }
    }

    internal void SetDragOver(bool value)
    {
        if (IsDragOver == value)
        {
            return;
        }
        IsDragOver = value;
        UpdateState();
    }

    #endregion
}
