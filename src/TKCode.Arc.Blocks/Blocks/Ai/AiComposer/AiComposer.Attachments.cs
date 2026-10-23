using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AiComposer
{
    private readonly Dictionary<AiAttachment, CancellationTokenSource> _uploads = new();
    private readonly HashSet<AiAttachment> _watchedAttachments = new();
    private readonly Dictionary<MenuEntry, AttachEntry> _attachEntries = new();
    private MenuSurface? _attachMenu;
    private bool _isDragging;
    // Attachments leaving with a sent message or a Clear: the collection handler must not raise events for them.
    private HashSet<AiAttachment>? _silentRemovals;

    /// <summary>What a row of the plus menu does.</summary>
    private sealed record AttachEntry(AiComposerActionKind Kind, AiComposerAction? Action);

    #region Public methods

    /// <summary>
    /// Adds picked, pasted or dropped files as attachments (folders are skipped). A file of a type
    /// <see cref="AcceptedFileTypes"/> does not take, or larger than <see cref="MaxFileSize"/>, is added in the error state
    /// with its reason; it can be removed and does not block sending. The others upload through <see cref="UploadHandler"/>
    /// (or are ready at once without one). Returns whether anything was added; nothing is while
    /// <see cref="IsAttachmentEnabled"/> is false.
    /// </summary>
    /// <param name="items">The files.</param>
    public async Task<bool> AddFilesAsync(IEnumerable<IStorageItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (!IsAttachmentEnabled)
        {
            return false;
        }
        var labels = Labels;
        IReadOnlyList<AiAttachment> added;
        try
        {
            added = await AiFileIntake.FromStorageAsync(items, MaxFileSize, AcceptedFileTypes, CancellationToken.None,
                labels.TooLarge, _ => labels.TypeNotAccepted);
        }
        catch (Exception)
        {
            return false;
        }
        if (added.Count == 0 || !IsAttachmentEnabled)
        {
            return false;
        }
        foreach (var attachment in added)
        {
            Attachments.Add(attachment);
            if (attachment.Status == AiAttachmentStatus.Error)
            {
                Announce(labels.AnnounceUploadFailed(attachment.Name, attachment.Error ?? labels.UploadFailed));
            }
            else
            {
                StartUpload(attachment);
            }
        }
        return true;
    }

    /// <summary>Opens the system file picker (filtered by <see cref="AcceptedFileTypes"/>) and adds the chosen files.</summary>
    public async Task BrowseAsync()
    {
        if (!IsAttachmentEnabled)
        {
            return;
        }
        var options = new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = Labels.PickerTitle,
            FileTypeFilter = PickerFilter(AcceptedFileTypes)
        };
        IReadOnlyList<IStorageFile> picked;
        try
        {
            if (PickFilesAsync is { } pick)
            {
                picked = await pick(options);
            }
            else if (TopLevel.GetTopLevel(this)?.StorageProvider is { CanOpen: true } storage)
            {
                picked = await storage.OpenFilePickerAsync(options);
            }
            else
            {
                return;
            }
        }
        catch (Exception)
        {
            return;
        }
        if (picked.Count > 0)
        {
            await AddFilesAsync(picked);
        }
    }

    #endregion

    #region Attachments collection

    private void OnAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Diff against what is watched, so Reset, Replace and Move need no special cases.
        var current = new HashSet<AiAttachment>(Attachments);
        foreach (var gone in _watchedAttachments.Where(a => !current.Contains(a)).ToList())
        {
            _watchedAttachments.Remove(gone);
            gone.PropertyChanged -= OnAttachmentPropertyChanged;
            CancelUpload(gone);
            AiFileIntake.Release(gone);
            if (_silentRemovals?.Contains(gone) != true)
            {
                RaiseEvent(new AiAttachmentEventArgs(AttachmentRemovedEvent, this, gone));
            }
        }
        foreach (var attachment in Attachments)
        {
            if (_watchedAttachments.Add(attachment))
            {
                attachment.PropertyChanged += OnAttachmentPropertyChanged;
                RaiseEvent(new AiAttachmentEventArgs(AttachmentAddedEvent, this, attachment));
            }
        }
        UpdateSendState();
    }

    private void OnAttachmentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != AiAttachment.StatusProperty || sender is not AiAttachment attachment)
        {
            return;
        }
        if (e.GetOldValue<AiAttachmentStatus>() == AiAttachmentStatus.Uploading)
        {
            var labels = Labels;
            switch (e.GetNewValue<AiAttachmentStatus>())
            {
                case AiAttachmentStatus.Done:
                case AiAttachmentStatus.Ready:
                    Announce(labels.AnnounceUploaded(attachment.Name));
                    break;
                case AiAttachmentStatus.Error:
                    Announce(labels.AnnounceUploadFailed(attachment.Name, attachment.Error ?? labels.UploadFailed));
                    break;
            }
        }
        UpdateSendState();
    }

    /// <summary>Uploads an attachment in place; removing it cancels the upload.</summary>
    private void StartUpload(AiAttachment attachment)
    {
        CancelUpload(attachment);
        var cancel = new CancellationTokenSource();
        _uploads[attachment] = cancel;
        _ = UploadAsync(attachment, UploadHandler, cancel);
    }

    private async Task UploadAsync(AiAttachment attachment, Func<AiAttachment, IProgress<double>, CancellationToken, Task>? handler,
        CancellationTokenSource cancel)
    {
        try
        {
            await AiFileIntake.RunUploadAsync(attachment, handler, UploadDoneHold, DelayAsync, () => Labels.UploadFailed, cancel.Token);
        }
        catch (Exception)
        {
            // The pipeline reports failures on the attachment; a faulty clock ends the hold early.
        }
        if (_uploads.TryGetValue(attachment, out var current) && ReferenceEquals(current, cancel))
        {
            _uploads.Remove(attachment);
            cancel.Dispose();
        }
    }

    private void CancelUpload(AiAttachment attachment)
    {
        if (_uploads.Remove(attachment, out var cancel))
        {
            cancel.Cancel();
            cancel.Dispose();
        }
    }

    /// <summary>A chip's remove or cancel button: the attachment leaves (a cancel is a removal), and is announced.</summary>
    private void OnChipRemoveRequested(object? sender, AiAttachmentRequestEventArgs e)
    {
        e.Handled = true;
        RemoveAttachment(e.Attachment);
    }

    /// <summary>
    /// Takes an attachment out (as its chip's button does): the upload stops, <see cref="AttachmentRemoved"/> is raised and
    /// announced. Focus inside the chips moves to the text area when no chip is left.
    /// </summary>
    internal bool RemoveAttachment(AiAttachment attachment)
    {
        var focusInTray = _tray?.IsKeyboardFocusWithin == true;
        if (!Attachments.Remove(attachment))
        {
            return false;
        }
        Announce(Labels.AnnounceRemoved(attachment.Name));
        if (focusInTray && Attachments.Count == 0)
        {
            FocusInput(FocusMethod);
        }
        return true;
    }

    /// <summary>
    /// Takes every attachment out. With <paramref name="raiseEvents"/> each raises <see cref="AttachmentRemoved"/>;
    /// <paramref name="keep"/> (a sent message's attachments) leave quietly either way.
    /// </summary>
    private void RemoveAllAttachments(bool raiseEvents, IReadOnlyCollection<AiAttachment>? keep = null)
    {
        if (Attachments.Count == 0)
        {
            return;
        }
        var focusInTray = _tray?.IsKeyboardFocusWithin == true;
        _silentRemovals = raiseEvents ? new HashSet<AiAttachment>(keep ?? Array.Empty<AiAttachment>()) : new HashSet<AiAttachment>(Attachments);
        try
        {
            Attachments.Clear();
        }
        finally
        {
            _silentRemovals = null;
        }
        if (focusInTray)
        {
            FocusInput(FocusMethod);
        }
    }

    /// <summary>The plus button and the drop target follow <see cref="IsAttachmentEnabled"/>.</summary>
    private void UpdateAttachmentParts()
    {
        if (_plus is not null)
        {
            _plus.IsVisible = IsAttachmentEnabled;
        }
        if (!IsAttachmentEnabled)
        {
            _attachMenu?.Close(returnFocus: false);
            SetDragging(false);
        }
    }

    /// <summary>The picker's filter for an HTML-style accept list (".pdf,image/*").</summary>
    internal static IReadOnlyList<FilePickerFileType>? PickerFilter(string? accept)
    {
        var accepted = AiFileIntake.ParseAccept(accept);
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
            foreach (var extension in KnownExtensions(type))
            {
                patterns.Add("*." + extension);
            }
        }
        return new[] { new FilePickerFileType(null) { Patterns = patterns.Distinct().ToList(), MimeTypes = mimes } };
    }

    /// <summary>The usual extensions of a MIME type or family, for pickers that filter by pattern only.</summary>
    private static IEnumerable<string> KnownExtensions(string mime) => mime switch
    {
        "image/*" => ["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "heic", "avif"],
        "text/*" => ["txt", "md", "csv", "html", "css"],
        "audio/*" => ["mp3", "wav", "ogg", "m4a", "flac"],
        "video/*" => ["mp4", "mov", "webm", "mkv"],
        "application/pdf" => ["pdf"],
        "application/json" => ["json"],
        "text/csv" => ["csv"],
        "text/plain" => ["txt"],
        "text/markdown" => ["md"],
        "image/png" => ["png"],
        "image/jpeg" => ["jpg", "jpeg"],
        _ => []
    };

    #endregion

    #region Paste

    /// <summary>
    /// A paste in the text area: files become attachments, a long text (<see cref="LongPasteCharacters"/> or
    /// <see cref="LongPasteLines"/>) becomes a "Pasted text" attachment and stays out of the text and its undo history,
    /// anything else is typed as usual.
    /// </summary>
    private async void OnEditorPasting(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        e.Handled = true;
        IStorageItem[]? files = null;
        string? text = null;
        if (IsAttachmentEnabled)
        {
            try
            {
                files = await clipboard.TryGetFilesAsync();
            }
            catch (Exception)
            {
                files = null;
            }
        }
        if (files is not { Length: > 0 })
        {
            try
            {
                text = await clipboard.TryGetTextAsync();
            }
            catch (Exception)
            {
                text = null;
            }
        }
        await PasteAsync(files, text);
    }

    /// <summary>What a paste does with the clipboard's files and text (tests call it directly).</summary>
    internal async Task PasteAsync(IReadOnlyList<IStorageItem>? files, string? text)
    {
        if (IsAttachmentEnabled && files is { Count: > 0 })
        {
            await AddFilesAsync(files);
            return;
        }
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (IsAttachmentEnabled && AiFileIntake.IsLongPaste(text, LongPasteCharacters, LongPasteLines))
        {
            var labels = Labels;
            Attachments.Add(AiFileIntake.FromPastedText(text, labels.PastedText, labels.PastedLines));
            Announce(labels.AnnouncePasted);
            return;
        }
        _editor?.InsertText(text);
    }

    #endregion

    #region Drag and drop

    private void InitializeDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnDrop, handledEventsToo: true);
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
        SetDragging(true);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Moving between the composer's own parts raises leave and enter; only leaving its bounds ends the drag.
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            return;
        }
        SetDragging(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragging(false);
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

    /// <summary>Shows or hides the "Drop to attach" cover.</summary>
    internal void SetDragging(bool dragging)
    {
        if (dragging == _isDragging)
        {
            return;
        }
        _isDragging = dragging;
        PseudoClasses.Set(DraggingPseudoClass, dragging);
        if (_dropHost is not null)
        {
            _dropHost.IsPresent = dragging;
        }
    }

    /// <summary>Whether the drag cover shows (tests).</summary>
    internal bool IsDragging => _isDragging;

    #endregion

    #region Plus menu

    /// <summary>The plus menu (null until it first opens; tests).</summary>
    internal MenuSurface? AttachMenu => _attachMenu;

    /// <summary>The plus button opens its menu; the chat's paperclip opens the file picker directly.</summary>
    private void OnPlusClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (IsInline)
        {
            _ = BrowseAsync();
            return;
        }
        if (_attachMenu is { IsOpen: true })
        {
            _attachMenu.Close(returnFocus: false);
            return;
        }
        OpenAttachMenu(_keyboardMode ? MenuOpenFocus.First : MenuOpenFocus.Surface);
    }

    private void OnPlusKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up && e.KeyModifiers == KeyModifiers.None && !IsInline && _attachMenu is not { IsOpen: true })
        {
            e.Handled = true;
            OpenAttachMenu(e.Key == Key.Down ? MenuOpenFocus.First : MenuOpenFocus.Last);
        }
    }

    /// <summary>
    /// Opens the plus menu above the button, aligned to its start: "Upload file", the <see cref="AttachActions"/>, then
    /// (after a separator) "Mention a file or doc" while there is anything to mention.
    /// </summary>
    internal void OpenAttachMenu(MenuOpenFocus focus)
    {
        if (_plus is not { } plus || !IsAttachmentEnabled || IsInline)
        {
            return;
        }
        var menu = EnsureAttachMenu();
        BuildAttachMenu(menu);
        menu.FocusReturnTarget = null;
        menu.Open(plus, focus);
    }

    private MenuSurface EnsureAttachMenu()
    {
        if (_attachMenu is not null)
        {
            return _attachMenu;
        }
        var menu = new MenuSurface
        {
            Variant = MenuSurfaceVariant.Dropdown,
            Side = MenuSide.Top,
            Alignment = MenuAlignment.Start,
            AnchorGap = MenuGap
        };
        menu.ItemClicked += OnAttachMenuItemClicked;
        menu.PropertyChanged += OnAttachMenuPropertyChanged;
        _attachMenu = menu;
        return menu;
    }

    private void BuildAttachMenu(MenuSurface menu)
    {
        var labels = Labels;
        menu.Items.Clear();
        _attachEntries.Clear();
        Add(new MenuEntry { Header = labels.UploadFile, Icon = ArcIcons.Paperclip }, new AttachEntry(AiComposerActionKind.Upload, null));
        foreach (var action in AttachActions)
        {
            if (action is null || string.IsNullOrEmpty(action.Label))
            {
                continue;
            }
            var entry = new MenuEntry { Header = action.Label, Icon = action.Icon, HasSeparatorBefore = action.IsSeparatorBefore };
            Add(entry, new AttachEntry(action.Kind, action));
        }
        if (ContextItems.Count > 0)
        {
            Add(new MenuEntry { Header = labels.MentionEntry, Icon = ArcIcons.AtSign, HasSeparatorBefore = true },
                new AttachEntry(AiComposerActionKind.Mention, null));
        }

        void Add(MenuEntry entry, AttachEntry what)
        {
            _attachEntries[entry] = what;
            menu.Items.Add(entry);
        }
    }

    /// <summary>
    /// A row was chosen: the focus goes back to the text area (not the plus button), then the row runs: the file picker,
    /// an "@" that opens the mention list, or the action's command with the composer as its parameter.
    /// </summary>
    private void OnAttachMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (_attachMenu is not { } menu || !_attachEntries.TryGetValue(e.Entry, out var what))
        {
            return;
        }
        menu.FocusReturnTarget = _editor;
        Post(() => RunAttachEntry(what));
    }

    private void RunAttachEntry(AttachEntry what)
    {
        switch (what.Kind)
        {
            case AiComposerActionKind.Upload:
                _ = BrowseAsync();
                break;
            case AiComposerActionKind.Mention:
                _editor?.OpenTrigger(MentionKind);
                break;
            default:
                if (what.Action?.Command is { } command && command.CanExecute(this))
                {
                    command.Execute(this);
                }
                break;
        }
    }

    private void OnAttachMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MenuSurface.IsOpenProperty)
        {
            return;
        }
        var open = e.GetNewValue<bool>();
        if (_plus is not null)
        {
            _plus.IsOpen = open;
        }
    }

    #endregion
}
