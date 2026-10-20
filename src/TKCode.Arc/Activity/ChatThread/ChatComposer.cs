using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AtomUI.Controls;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>A file waiting in the <see cref="ChatComposer"/>'s tray.</summary>
public sealed class ChatComposerFile
{
    internal ChatComposerFile(string id, IStorageFile file, long? size)
    {
        Id = id;
        File = file;
        Name = file.Name;
        Size = size;
        IsImage = ChatComposer.IsImageName(file.Name);
    }

    /// <summary>Tray id ("file-3").</summary>
    public string Id { get; }

    /// <summary>The file.</summary>
    public IStorageFile File { get; }

    /// <summary>Its name.</summary>
    public string Name { get; }

    /// <summary>Its size in bytes, when the storage provider reports it.</summary>
    public long? Size { get; }

    /// <summary>True for pictures (png, jpg, gif, webp, bmp, avif, heic): shown as a 56px thumbnail and sent as an image.</summary>
    public bool IsImage { get; }
}

/// <summary>
/// The message box on its own (Arc <c>ChatComposer</c>): text that grows up to six lines on a spring, attachments by
/// button, paste or drop (up to ten at a time) in a tray above it, and a send button inside the box that turns to the
/// accent when there is something to send; its arrow flies off and a new one rises when you send. Enter sends,
/// Shift+Enter starts a new line (Enter during IME composition is left to the IME).
/// </summary>
[TemplatePart(TextBoxPart, typeof(AvaloniaTextBox))]
[TemplatePart(AttachPart, typeof(ChatThreadButton))]
[TemplatePart(SendPart, typeof(ChatThreadButton))]
[TemplatePart(TrayPart, typeof(StackPanel))]
[TemplatePart(TrayHostPart, typeof(PresenceHost))]
[PseudoClasses(DraggingPseudoClass, ReadyPseudoClass)]
public class ChatComposer : TemplatedControl
{
    public const string TextBoxPart = "PART_TextBox";
    public const string AttachPart = "PART_Attach";
    public const string SendPart = "PART_Send";
    public const string TrayPart = "PART_Tray";
    public const string TrayHostPart = "PART_TrayHost";

    /// <summary>Set while files are dragged over the composer (accent tint, dashed accent box).</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while there is something to send.</summary>
    public const string ReadyPseudoClass = ":ready";

    /// <summary>Most files taken by one add (Arc slices each add to 10).</summary>
    public const int MaxFilesPerAdd = 10;

    private static readonly string[] s_imageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".avif", ".heic", ".svg"];

    /// <summary>The text being written (two-way).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ChatComposer, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Placeholder and accessible name of the text box ("Message").</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ChatComposer, string?>(nameof(Placeholder), "Message");

    /// <summary>Shows the attach button and takes pasted and dropped files (default true).</summary>
    public static readonly StyledProperty<bool> IsAttachmentEnabledProperty =
        AvaloniaProperty.Register<ChatComposer, bool>(nameof(IsAttachmentEnabled), true);

    /// <summary>File types the attach button offers (Arc <c>accept</c>); null offers everything.</summary>
    public static readonly StyledProperty<IReadOnlyList<FilePickerFileType>?> AcceptedFileTypesProperty =
        AvaloniaProperty.Register<ChatComposer, IReadOnlyList<FilePickerFileType>?>(nameof(AcceptedFileTypes));

    /// <summary>Words the composer shows or announces (Arc <c>labels</c>).</summary>
    public static readonly StyledProperty<ChatComposerLabels> LabelsProperty =
        AvaloniaProperty.Register<ChatComposer, ChatComposerLabels>(nameof(Labels), new ChatComposerLabels());

    /// <summary>Whether there is something to send (text or files, and the composer is enabled).</summary>
    public static readonly DirectProperty<ChatComposer, bool> IsReadyProperty =
        AvaloniaProperty.RegisterDirect<ChatComposer, bool>(nameof(IsReady), o => o.IsReady);

    /// <summary>Whether files are being dragged over the composer.</summary>
    public static readonly DirectProperty<ChatComposer, bool> IsDragOverProperty =
        AvaloniaProperty.RegisterDirect<ChatComposer, bool>(nameof(IsDragOver), o => o.IsDragOver);

    /// <summary>Whether files wait in the tray.</summary>
    public static readonly DirectProperty<ChatComposer, bool> HasFilesProperty =
        AvaloniaProperty.RegisterDirect<ChatComposer, bool>(nameof(HasFiles), o => o.HasFiles);

    /// <summary>Raised with the draft when it is sent (Enter or the send button).</summary>
    public static readonly RoutedEvent<ChatSendEventArgs> SentEvent =
        RoutedEvent.Register<ChatComposer, ChatSendEventArgs>(nameof(Sent), RoutingStrategies.Bubble);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool IsAttachmentEnabled { get => GetValue(IsAttachmentEnabledProperty); set => SetValue(IsAttachmentEnabledProperty, value); }
    public IReadOnlyList<FilePickerFileType>? AcceptedFileTypes { get => GetValue(AcceptedFileTypesProperty); set => SetValue(AcceptedFileTypesProperty, value); }
    public ChatComposerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    private bool _isReady;

    /// <summary>Whether there is something to send.</summary>
    public bool IsReady { get => _isReady; private set => SetAndRaise(IsReadyProperty, ref _isReady, value); }

    private bool _isDragOver;

    /// <summary>Whether files are being dragged over the composer.</summary>
    public bool IsDragOver { get => _isDragOver; private set => SetAndRaise(IsDragOverProperty, ref _isDragOver, value); }

    private bool _hasFiles;

    /// <summary>Whether files wait in the tray.</summary>
    public bool HasFiles { get => _hasFiles; private set => SetAndRaise(HasFilesProperty, ref _hasFiles, value); }

    /// <summary>The draft was sent.</summary>
    public event EventHandler<ChatSendEventArgs>? Sent
    {
        add => AddHandler(SentEvent, value);
        remove => RemoveHandler(SentEvent, value);
    }

    private readonly List<ChatComposerFile> _files = new();
    private readonly Dictionary<ChatComposerFile, ChatComposerTile> _tiles = new();
    private AvaloniaTextBox? _textBox;
    private TextPresenter? _presenter;
    private ChatThreadButton? _attach;
    private ChatThreadButton? _send;
    private ChatComposerSendGlyph? _sendGlyph;
    private StackPanel? _tray;
    private PresenceHost? _trayHost;
    private int _counter;
    private bool _bypassPaste;

    public ChatComposer()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>The files waiting to send, in the order they were added.</summary>
    public IReadOnlyList<ChatComposerFile> Files => _files;

    internal AvaloniaTextBox? TextBox => _textBox;

    internal ChatThreadButton? SendButton => _send;

    internal ChatThreadButton? AttachButton => _attach;

    internal ChatComposerSendGlyph? SendGlyph => _sendGlyph;

    internal IReadOnlyDictionary<ChatComposerFile, ChatComposerTile> Tiles => _tiles;

    internal static bool IsImageName(string name) =>
        s_imageExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    /// <summary>Moves keyboard focus to the text box (Arc's composer handle <c>focus</c>).</summary>
    public void FocusInput() => _textBox?.Focus(NavigationMethod.Tab);

    /// <summary>Sends the draft when there is something to send; returns whether it did.</summary>
    public bool Send()
    {
        if (!IsReady)
        {
            return false;
        }
        var text = (Text ?? string.Empty).Trim();
        var files = _files.Select(f => f.File).ToArray();
        var attachments = _files.Select(f => new ChatAttachment(f.Id, f.Name, f.IsImage ? ChatAttachmentKind.Image : ChatAttachmentKind.File)
        {
            File = f.File,
            Size = f.Size
        }).ToArray();
        var draft = new ChatDraft(text, files, attachments);
        RaiseEvent(new ChatSendEventArgs(SentEvent, draft));
        foreach (var file in _files.ToArray())
        {
            RemoveTile(file, animate: false);
        }
        _files.Clear();
        SetCurrentValue(TextProperty, string.Empty);
        UpdateState();
        _sendGlyph?.Fly();
        FocusInput();
        return true;
    }

    /// <summary>Opens the system file picker (filtered by <see cref="AcceptedFileTypes"/>) and adds the chosen files.</summary>
    public async Task BrowseAsync()
    {
        if (!IsAttachmentEnabled || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }
        IReadOnlyList<IStorageFile> picked;
        try
        {
            picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = Labels.Attach,
                FileTypeFilter = AcceptedFileTypes
            });
        }
        catch (Exception)
        {
            return;
        }
        await AddFilesAsync(picked);
    }

    /// <summary>Adds files to the tray (folders are skipped; at most <see cref="MaxFilesPerAdd"/> per call).</summary>
    public async Task AddFilesAsync(IEnumerable<IStorageItem> items)
    {
        if (!IsAttachmentEnabled)
        {
            return;
        }
        foreach (var file in items.OfType<IStorageFile>().Take(MaxFilesPerAdd).ToArray())
        {
            long? size = null;
            try
            {
                var properties = await file.GetBasicPropertiesAsync();
                size = properties.Size is { } bytes ? (long)bytes : null;
            }
            catch (Exception)
            {
                // Providers that cannot report a size still attach the file.
            }
            var entry = new ChatComposerFile($"file-{++_counter}", file, size);
            _files.Add(entry);
            AddTile(entry);
        }
        UpdateState();
    }

    /// <summary>Takes a file back out of the tray.</summary>
    public void RemoveFile(ChatComposerFile file)
    {
        if (!_files.Remove(file))
        {
            return;
        }
        RemoveTile(file, animate: true);
        UpdateState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_textBox is not null)
        {
            _textBox.RemoveHandler(KeyDownEvent, OnTextKeyDown);
            _textBox.RemoveHandler(AvaloniaTextBox.PastingFromClipboardEvent, OnPasting);
            _textBox.TemplateApplied -= OnTextBoxTemplateApplied;
        }
        if (_attach is not null)
        {
            _attach.Click -= OnAttachClick;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        _textBox = e.NameScope.Find<AvaloniaTextBox>(TextBoxPart);
        _attach = e.NameScope.Find<ChatThreadButton>(AttachPart);
        _send = e.NameScope.Find<ChatThreadButton>(SendPart);
        _tray = e.NameScope.Find<StackPanel>(TrayPart);
        _trayHost = e.NameScope.Find<PresenceHost>(TrayHostPart);
        if (_textBox is not null)
        {
            _textBox.AddHandler(KeyDownEvent, OnTextKeyDown, RoutingStrategies.Tunnel);
            _textBox.AddHandler(AvaloniaTextBox.PastingFromClipboardEvent, OnPasting);
            _textBox.TemplateApplied += OnTextBoxTemplateApplied;
        }
        if (_attach is not null)
        {
            _attach.Content ??= new ArcGlyph { Data = ArcIcons.Paperclip, Size = 18, StrokeThickness = 1.75 };
            _attach.Click += OnAttachClick;
        }
        if (_send is not null)
        {
            _sendGlyph ??= new ChatComposerSendGlyph();
            _send.Content = _sendGlyph;
            _send.Click += OnSendClick;
        }
        if (_tray is not null)
        {
            foreach (var tile in _tiles.Values)
            {
                if (tile.Parent is Panel old)
                {
                    old.Children.Remove(tile);
                }
                _tray.Children.Add(tile);
            }
        }
        UpdateNames();
        UpdateState();
    }

    private void OnTextBoxTemplateApplied(object? sender, TemplateAppliedEventArgs e) =>
        _presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == IsEnabledProperty)
        {
            UpdateState();
        }
        else if (change.Property == LabelsProperty)
        {
            UpdateNames();
        }
        else if (change.Property == IsAttachmentEnabledProperty && !IsAttachmentEnabled)
        {
            SetDragOver(false);
        }
    }

    private void UpdateNames()
    {
        var labels = Labels;
        if (_attach is not null)
        {
            AutomationProperties.SetName(_attach, labels.Attach);
        }
        if (_send is not null)
        {
            AutomationProperties.SetName(_send, labels.Send);
        }
        if (_tray is not null)
        {
            AutomationProperties.SetName(_tray, labels.Attachments);
        }
        foreach (var (file, tile) in _tiles)
        {
            AutomationProperties.SetName(tile.RemoveButton, labels.RemoveFile(file.Name));
        }
    }

    private void UpdateState()
    {
        var ready = IsEffectivelyEnabled && (!string.IsNullOrWhiteSpace(Text) || _files.Count > 0);
        IsReady = ready;
        PseudoClasses.Set(ReadyPseudoClass, ready);
        if (_send is not null)
        {
            _send.IsReady = ready;
        }
        HasFiles = _files.Count > 0;
        if (_trayHost is not null)
        {
            _trayHost.IsPresent = HasFiles;
        }
    }

    private void OnTextKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }
        // Enter while the IME composes belongs to the IME.
        if (_presenter?.PreeditText is { Length: > 0 })
        {
            return;
        }
        e.Handled = true;
        Send();
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Send();
    }

    private void OnAttachClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = BrowseAsync();
    }

    /// <summary>Pasted files go to the tray instead of the text (Arc <c>onPaste</c>); text pastes as usual.</summary>
    private async void OnPasting(object? sender, RoutedEventArgs e)
    {
        if (_bypassPaste)
        {
            _bypassPaste = false;
            return;
        }
        if (!IsAttachmentEnabled || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        e.Handled = true;
        IStorageItem[]? files = null;
        try
        {
            files = await clipboard.TryGetFilesAsync();
        }
        catch (Exception)
        {
            files = null;
        }
        if (files is { Length: > 0 })
        {
            await AddFilesAsync(files);
            return;
        }
        _bypassPaste = true;
        _textBox?.Paste();
    }

    private static bool CarriesFiles(DragEventArgs e) => e.DataTransfer.Contains(DataFormat.File);

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!IsAttachmentEnabled || !CarriesFiles(e))
        {
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
        SetDragOver(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Moving between the composer's own parts raises leave and enter; only leaving its bounds ends the drag.
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            return;
        }
        SetDragOver(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragOver(false);
        if (!IsAttachmentEnabled || !CarriesFiles(e))
        {
            return;
        }
        e.Handled = true;
        e.DragEffects = DragDropEffects.Copy;
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files)
        {
            await AddFilesAsync(files);
        }
    }

    internal void SetDragOver(bool value)
    {
        IsDragOver = value;
        PseudoClasses.Set(DraggingPseudoClass, value);
    }

    private void AddTile(ChatComposerFile file)
    {
        var tile = new ChatComposerTile(file);
        tile.RemoveButton.Click += (_, e) =>
        {
            e.Handled = true;
            RemoveFile(file);
        };
        AutomationProperties.SetName(tile.RemoveButton, Labels.RemoveFile(file.Name));
        _tiles[file] = tile;
        _tray?.Children.Add(tile);
        tile.Enter();
    }

    private async void RemoveTile(ChatComposerFile file, bool animate)
    {
        if (!_tiles.Remove(file, out var tile))
        {
            return;
        }
        if (animate && _files.Count > 0)
        {
            await tile.LeaveAsync();
        }
        tile.Release();
        _tray?.Children.Remove(tile);
    }
}

/// <summary>The composer's text box: Avalonia's <see cref="AvaloniaTextBox"/> with a borderless template (the box is drawn by the composer).</summary>
internal sealed class ChatComposerTextBox : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(ChatComposerTextBox);
}

/// <summary>
/// The field's height (Arc <c>useAutoHeight</c>): the text's natural height up to <see cref="MaxFieldHeight"/> (six lines),
/// growing and shrinking on the smooth spring (Reduced and Off jump). The text box keeps its target height and is
/// clipped while the field catches up, so only this decorator relayouts during the spring.
/// </summary>
internal sealed class ChatComposerField : Decorator
{
    public static readonly StyledProperty<double> MaxFieldHeightProperty =
        AvaloniaProperty.Register<ChatComposerField, double>(nameof(MaxFieldHeight), 150);

    public double MaxFieldHeight { get => GetValue(MaxFieldHeightProperty); set => SetValue(MaxFieldHeightProperty, value); }

    private static readonly SpringSpec s_grow = SpringSpec.FromVisual(0.4, 0);
    private MotionValue<double>? _height;
    private double _target = double.NaN;

    public ChatComposerField()
    {
        ClipToBounds = true;
    }

    /// <summary>The height the field is heading to.</summary>
    public double TargetHeight => _target;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        var target = Math.Min(MaxFieldHeight, child.DesiredSize.Height);
        if (!target.Equals(_target))
        {
            var first = double.IsNaN(_target);
            _target = target;
            _height ??= CreateHeight(target);
            if (first || !IsLoaded || !ArcMotion.IsFull(this))
            {
                _height.Jump(target);
            }
            else
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(s_grow));
            }
        }
        child.Measure(new Size(availableSize.Width, target));
        return new Size(child.DesiredSize.Width, Math.Max(0, _height?.Value ?? target));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, double.IsNaN(_target) ? finalSize.Height : _target));
        return finalSize;
    }

    private MotionValue<double> CreateHeight(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }
}

/// <summary>The dashed accent outline of the box while files are dragged over the composer.</summary>
internal sealed class ChatComposerDashFrame : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ChatComposerDashFrame, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ChatComposerDashFrame, CornerRadius>(nameof(CornerRadius));

    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    static ChatComposerDashFrame()
    {
        AffectsRender<ChatComposerDashFrame>(StrokeProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ChatComposerDashFrame>(false);
    }

    public override void Render(DrawingContext context)
    {
        if (Stroke is not { } stroke)
        {
            return;
        }
        var pen = new Pen(stroke, 1, new DashStyle([4, 3], 0));
        var rect = new Rect(Bounds.Size).Deflate(0.5);
        context.DrawRectangle(null, pen, new RoundedRect(rect, CornerRadius.TopLeft - 0.5));
    }
}

/// <summary>
/// The send button's arrow (P2/P3): on send the arrow flies up and out (18px, fast) while a new one rises from 14px
/// below on the snappy spring. Reduced cross-fades; Off swaps at once.
/// </summary>
internal sealed class ChatComposerSendGlyph : Panel
{
    private readonly ArcGlyph _current;
    private readonly ArcGlyph _leaving;
    private readonly TranslateTransform _currentShift = new();
    private readonly TranslateTransform _leavingShift = new();
    private MotionValue<double>? _rise;
    private MotionValue<double>? _fly;

    public ChatComposerSendGlyph()
    {
        _current = new ArcGlyph { Data = ArcIcons.ArrowUp, Size = 16, StrokeThickness = 2.25, RenderTransform = _currentShift };
        _leaving = new ArcGlyph { Data = ArcIcons.ArrowUp, Size = 16, StrokeThickness = 2.25, RenderTransform = _leavingShift, Opacity = 0 };
        Children.Add(_leaving);
        Children.Add(_current);
        ClipToBounds = false;
        IsHitTestVisible = false;
    }

    /// <summary>Number of sends played (tests).</summary>
    public int Flights { get; private set; }

    /// <summary>Vertical offset of the rising arrow (0 at rest).</summary>
    public double RiseOffset => _currentShift.Y;

    public void Fly()
    {
        Flights++;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            return;
        }
        _rise ??= Create(v =>
        {
            _currentShift.Y = level == ArcMotionLevel.Full ? 14 * (1 - v) : 0;
            _current.Opacity = Math.Clamp(v, 0, 1);
        });
        _fly ??= Create(v =>
        {
            _leavingShift.Y = ArcMotion.IsFull(this) ? -18 * v : 0;
            _leaving.Opacity = Math.Clamp(1 - v, 0, 1);
        });
        _fly.Jump(0);
        _ = _fly.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        _rise.Jump(0);
        _ = _rise.AnimateTo(1, level == ArcMotionLevel.Full ? MotionSpec.Spring(ArcSpring.Snappy) : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
    }

    private MotionValue<double> Create(Action<double> apply)
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += apply;
        return value;
    }
}

/// <summary>
/// A file in the tray (Arc <c>.pending</c>): a 56px picture thumbnail or a 168px file card with its name and size, and a
/// 20px remove button on its corner. Pops in from 0.9 out of a subtle blur on the pop spring; leaves the same way.
/// </summary>
internal sealed class ChatComposerTile : Panel
{
    private static readonly SpringSpec s_pop = SpringSpec.FromVisual(0.3, 0.22);
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _presence;
    private readonly AsyncImage? _image;

    public ChatComposerTile(ChatComposerFile file)
    {
        File = file;
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
        ClipToBounds = false;
        Control body;
        if (file.IsImage)
        {
            _image = new AsyncImage { Stretch = Stretch.UniformToFill, Source = new StorageFileImageSource(file.File) };
            body = new ChatComposerTileFrame { Child = _image, IsImage = true };
        }
        else
        {
            var name = new TextBlock { Text = file.Name, TextTrimming = TextTrimming.CharacterEllipsis };
            var size = new TextBlock { Text = file.Size is { } bytes ? ActivityTime.FormatBytes(bytes) : string.Empty };
            var icon = new ArcGlyph { Data = ArcIcons.FileText, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(name, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
            ArcTokens.Bind(name, TextBlock.FontWeightProperty, DesignTokens.ArcDesignTokenKind.WeightMedium);
            ArcTokens.Bind(name, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
            ArcTokens.Bind(size, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
            ArcTokens.Bind(size, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnSpacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRowSpan(icon, 2);
            Grid.SetColumn(name, 1);
            Grid.SetColumn(size, 1);
            Grid.SetRow(size, 1);
            grid.Children.Add(icon);
            grid.Children.Add(name);
            grid.Children.Add(size);
            body = new ChatComposerTileFrame { Child = grid };
        }
        Children.Add(body);
        RemoveButton = ChatThreadButton.WithGlyph(ChatThreadButtonRole.Remove, ArcIcons.X, 12, 2.25, $"Remove {file.Name}");
        RemoveButton.HorizontalAlignment = HorizontalAlignment.Right;
        RemoveButton.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(RemoveButton);
    }

    public ChatComposerFile File { get; }

    public ChatThreadButton RemoveButton { get; }

    public void Enter()
    {
        _presence ??= CreatePresence();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded && Parent is not Control { IsLoaded: true } || level == ArcMotionLevel.Off)
        {
            _presence.Jump(1);
            return;
        }
        _presence.Jump(0);
        _ = _presence.AnimateTo(1, level == ArcMotionLevel.Full ? MotionSpec.Spring(s_pop) : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
    }

    public async Task LeaveAsync()
    {
        _presence ??= CreatePresence();
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            return;
        }
        IsHitTestVisible = false;
        await _presence.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Standard));
    }

    /// <summary>Drops the thumbnail so its decoded picture can be released.</summary>
    public void Release()
    {
        if (_image is not null)
        {
            _image.Source = null;
        }
    }

    private MotionValue<double> CreatePresence()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            var full = ArcMotion.IsFull(this);
            var scale = full ? 0.9 + 0.1 * v : 1;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
            var fade = Math.Clamp(v, 0, 1);
            Opacity = fade;
            Effect = full ? ArcBlur.Get(2 * (1 - fade)) : null;
        };
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }
        if (Children.Count > 0)
        {
            size = Children[0].DesiredSize;
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count > 0)
        {
            Children[0].Arrange(new Rect(finalSize));
        }
        if (Children.Count > 1)
        {
            var remove = Children[1];
            var size = remove.DesiredSize;
            remove.Arrange(new Rect(new Point(finalSize.Width - size.Width + 6, -6), size));
        }
        return finalSize;
    }
}

/// <summary>A tray tile's frame: a 56px rounded picture, or a 168×56 bordered file card.</summary>
internal sealed class ChatComposerTileFrame : Border
{
    public static readonly StyledProperty<bool> IsImageProperty =
        AvaloniaProperty.Register<ChatComposerTileFrame, bool>(nameof(IsImage));

    public bool IsImage { get => GetValue(IsImageProperty); set => SetValue(IsImageProperty, value); }

    protected override Type StyleKeyOverride => typeof(ChatComposerTileFrame);
}
