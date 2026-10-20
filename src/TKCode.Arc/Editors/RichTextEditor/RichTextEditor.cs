using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="RichTextEditor.ContentChanged"/>.</summary>
public sealed class RichTextContentChangedEventArgs : RoutedEventArgs
{
    internal RichTextContentChangedEventArgs(RoutedEvent routedEvent, RichTextValue value) : base(routedEvent)
    {
        Value = value;
    }

    /// <summary>The editor's content after the change (HTML, Markdown and text are computed on first read).</summary>
    public RichTextValue Value { get; }
}

/// <summary>
/// Arc's light rich text editor: Markdown shortcuts as you type, a floating selection toolbar that morphs into a link
/// field, a slash menu for blocks, its own undo history, and HTML plus Markdown output. Blocks are paragraphs,
/// three heading levels, nested bulleted and numbered lists, quotes, code blocks and dividers; inline marks are bold,
/// italic, strikethrough, inline code and links.
/// </summary>
/// <remarks>
/// Keyboard: Mod+B, Mod+I, Mod+Shift+X and Mod+E toggle marks, Mod+K edits a link, Mod+Z / Mod+Shift+Z / Mod+Y undo and
/// redo, Tab and Shift+Tab indent list items, Shift+Enter breaks a line, "/" opens the block menu, Alt+F10 moves into
/// the toolbar. Mod is Command on macOS and Control elsewhere.
/// </remarks>
[TemplatePart(SurfacePart, typeof(RichTextEditorSurface))]
[TemplatePart(LiveRegionPart, typeof(EditorsLiveRegion))]
public class RichTextEditor : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string LiveRegionPart = "PART_LiveRegion";

    /// <summary>The content as HTML (two-way). Assigning reloads the editor only when the value differs from what it last wrote.</summary>
    public static readonly StyledProperty<string?> HtmlProperty =
        AvaloniaProperty.Register<RichTextEditor, string?>(nameof(Html), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The content as Markdown (two-way): Arc's <c>defaultMarkdown</c>, kept in sync after every edit.</summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<RichTextEditor, string?>(nameof(Markdown), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<RichTextEditor, string>(nameof(Placeholder), "Start writing");

    public static readonly StyledProperty<string> BlockHintProperty =
        AvaloniaProperty.Register<RichTextEditor, string>(nameof(BlockHint), "Type / for blocks");

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<RichTextEditor, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<bool> IsAutoFocusEnabledProperty =
        AvaloniaProperty.Register<RichTextEditor, bool>(nameof(IsAutoFocusEnabled));

    public static readonly DirectProperty<RichTextEditor, bool> CanUndoProperty =
        AvaloniaProperty.RegisterDirect<RichTextEditor, bool>(nameof(CanUndo), o => o.CanUndo);

    public static readonly DirectProperty<RichTextEditor, bool> CanRedoProperty =
        AvaloniaProperty.RegisterDirect<RichTextEditor, bool>(nameof(CanRedo), o => o.CanRedo);

    public static readonly DirectProperty<RichTextEditor, bool> IsDocumentEmptyProperty =
        AvaloniaProperty.RegisterDirect<RichTextEditor, bool>(nameof(IsDocumentEmpty), o => o.IsDocumentEmpty);

    public static readonly DirectProperty<RichTextEditor, RichTextFormats> FormatsProperty =
        AvaloniaProperty.RegisterDirect<RichTextEditor, RichTextFormats>(nameof(Formats), o => o.Formats);

    /// <summary>Raised after every edit, undo, redo and load (Arc's <c>onChange</c>).</summary>
    public static readonly RoutedEvent<RichTextContentChangedEventArgs> ContentChangedEvent =
        RoutedEvent.Register<RichTextEditor, RichTextContentChangedEventArgs>(nameof(ContentChanged), RoutingStrategies.Bubble);

    private static readonly DataFormat<string> s_markdownFormat = DataFormat.CreateStringApplicationFormat("tkcode-arc-markdown");
    private static readonly DataFormat<RichTextEditorClipboardFragment> s_fragmentFormat =
        DataFormat.CreateInProcessFormat<RichTextEditorClipboardFragment>("tkcode-arc-rich-text");
    private static readonly string[] s_htmlFormats = { "public.html", "text/html", "HTML Format" };

    private readonly RichTextEditorEngine _engine = new();
    private RichTextEditorSurface? _surface;
    private EditorsLiveRegion? _liveRegion;
    private RichTextEditorOverlay? _overlay;
    private RichTextEditorToolbar? _toolbar;
    private RichTextEditorSlashMenu? _slashMenu;
    private bool _canUndo;
    private bool _canRedo;
    private bool _isDocumentEmpty = true;
    private RichTextFormats _formats = RichTextFormats.Default;
    private string? _lastHtml;
    private string? _lastMarkdown;
    private bool _writingValue;
    private bool _flushPending;
    private DispatcherTimer? _flushTimer;
    private bool _chromePending;
    private bool _revealPending;
    private (int Block, int Offset, string Query)? _slash;
    private int _slashActive;
    private RichTextEditorSelection? _savedLinkRange;
    private TopLevel? _linkWatchRoot;

    static RichTextEditor()
    {
        FocusableProperty.OverrideDefaultValue<RichTextEditor>(true);
        IsTabStopProperty.OverrideDefaultValue<RichTextEditor>(false);
        AutomationProperties.NameProperty.OverrideDefaultValue<RichTextEditor>("Editor");
    }

    public RichTextEditor()
    {
        _engine.Changed += OnEngineChanged;
        UndoCommand = new RichTextEditorCommand(this, () => Undo(), () => CanUndo && !IsReadOnly);
        RedoCommand = new RichTextEditorCommand(this, () => Redo(), () => CanRedo && !IsReadOnly);
    }

    /// <inheritdoc cref="HtmlProperty"/>
    public string? Html { get => GetValue(HtmlProperty); set => SetValue(HtmlProperty, value); }

    /// <inheritdoc cref="MarkdownProperty"/>
    public string? Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }

    /// <summary>Shown while the document is empty (default "Start writing").</summary>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>The quiet hint on an empty line holding the caret (default "Type / for blocks").</summary>
    public string BlockHint { get => GetValue(BlockHintProperty); set => SetValue(BlockHintProperty, value); }

    /// <summary>Turns off editing, the toolbar and the shortcuts; text can still be selected and copied.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>Focuses the editor when it loads and puts the caret at the end (Arc's <c>autoFocus</c>).</summary>
    public bool IsAutoFocusEnabled { get => GetValue(IsAutoFocusEnabledProperty); set => SetValue(IsAutoFocusEnabledProperty, value); }

    /// <summary>True when there is a step to undo (Arc's <c>onHistoryChange</c>).</summary>
    public bool CanUndo { get => _canUndo; private set => SetAndRaise(CanUndoProperty, ref _canUndo, value); }

    /// <summary>True when there is a step to redo.</summary>
    public bool CanRedo { get => _canRedo; private set => SetAndRaise(CanRedoProperty, ref _canRedo, value); }

    /// <summary>True when there is no text and no divider.</summary>
    public bool IsDocumentEmpty { get => _isDocumentEmpty; private set => SetAndRaise(IsDocumentEmptyProperty, ref _isDocumentEmpty, value); }

    /// <summary>The formatting at the selection, for your own toolbar's pressed states.</summary>
    public RichTextFormats Formats { get => _formats; private set => SetAndRaise(FormatsProperty, ref _formats, value); }

    /// <summary>Undoes one step (for your own toolbar).</summary>
    public ICommand UndoCommand { get; }

    /// <summary>Redoes one step (for your own toolbar).</summary>
    public ICommand RedoCommand { get; }

    /// <summary>Raised after every edit, undo, redo and load (Arc's <c>onChange</c>).</summary>
    public event EventHandler<RichTextContentChangedEventArgs>? ContentChanged
    {
        add => AddHandler(ContentChangedEvent, value);
        remove => RemoveHandler(ContentChangedEvent, value);
    }

    internal RichTextEditorEngine Engine => _engine;

    internal RichTextEditorSurface? Surface => _surface;

    internal RichTextEditorToolbar? Toolbar => _toolbar;

    internal RichTextEditorSlashMenu? SlashMenu => _slashMenu;

    internal EditorsLiveRegion? LiveRegion => _liveRegion;

    /// <summary>True while the link field is open: the selection it edits stays highlighted.</summary>
    internal bool KeepsSelectionVisible => _toolbar is { IsLinkFace: true } && _savedLinkRange is not null;

    internal bool IsSlashOpen => _slash is { } slash && RichTextEditorSlashItem.Filter(slash.Query).Count > 0;

    /* ----------------------------------------------------------------------------------------------------------
     * Public API (Arc's imperative handle)
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>Clean HTML of the current content.</summary>
    public string GetHtml() => RichTextEditorHtml.Write(_engine.Blocks);

    /// <summary>The current content as Markdown.</summary>
    public string GetMarkdown() => RichTextEditorMarkdown.Write(_engine.Blocks);

    /// <summary>Plain text, one line per block.</summary>
    public string GetText() => RichTextEditorHtml.PlainText(_engine.Blocks);

    /// <summary>Replaces the content with sanitized HTML (one undo step).</summary>
    public void SetHtml(string? html) => LoadContent(RichTextEditorHtml.ToDocument(html), record: true);

    /// <summary>Replaces the content with Markdown (one undo step).</summary>
    public void SetMarkdown(string? markdown) => LoadContent(RichTextEditorMarkdown.ToDocument(markdown), record: true);

    /// <summary>Empties the editor (one undo step).</summary>
    public void Clear() => LoadContent(ImmutableList.Create(RichTextEditorBlock.EmptyParagraph), record: true);

    /// <summary>Undoes one step.</summary>
    public void Undo()
    {
        CloseSlash();
        if (_engine.Undo())
        {
            Announce("Undone");
        }
    }

    /// <summary>Redoes one step.</summary>
    public void Redo()
    {
        CloseSlash();
        if (_engine.Redo())
        {
            Announce("Redone");
        }
    }

    /// <summary>Moves keyboard focus into the writing surface.</summary>
    public void FocusEditor() => _surface?.Focus();

    /// <summary>Selects everything.</summary>
    public void SelectAll() => _engine.SelectAll();

    /// <summary>Toggles a mark on the selection (or on the next typed text at a caret), as the toolbar does.</summary>
    public void ToggleMark(RichTextMark mark)
    {
        if (IsReadOnly)
        {
            return;
        }
        _engine.ToggleMark(mark);
        FocusEditor();
    }

    /// <summary>Turns the selected blocks into <paramref name="type"/>; blocks that already have it become paragraphs. <see cref="RichTextBlockType.Divider"/> inserts a rule.</summary>
    public void SetBlockType(RichTextBlockType type)
    {
        if (IsReadOnly)
        {
            return;
        }
        CloseSlash();
        _engine.SetBlockType(type);
        FocusEditor();
    }

    /// <summary>Links the selection (or the link at the caret) to <paramref name="url"/>, normalized; null or empty removes the link.</summary>
    public void SetLink(string? url)
    {
        if (IsReadOnly)
        {
            return;
        }
        var range = _engine.LinkRange(out _);
        if (range is null)
        {
            return;
        }
        var normalized = RichTextEditorMarkdown.NormalizeUrl(url);
        _engine.SetLink(range.Value, normalized);
        Announce(normalized.Length == 0 ? "Link removed" : "Link added");
        FocusEditor();
    }

    /// <summary>Opens the toolbar's link field for the selection or the link at the caret (Mod+K).</summary>
    public void EditLink() => OpenLink();

    /* ----------------------------------------------------------------------------------------------------------
     * Template and lifecycle
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_surface is not null)
        {
            _surface.Owner = null;
        }
        _surface = e.NameScope.Find<RichTextEditorSurface>(SurfacePart);
        _liveRegion = e.NameScope.Find<EditorsLiveRegion>(LiveRegionPart);
        if (_surface is not null)
        {
            _surface.Owner = this;
            _surface.OnDocumentChanged(true);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (IsAutoFocusEnabled && !IsReadOnly)
        {
            _engine.SetSelection(RichTextEditorSelection.Caret(RichTextEditorEngine.End(_engine.Blocks)));
            Dispatcher.UIThread.Post(() => _surface?.Focus(), DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopLinkWatch();
        FlushPending();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this))
        {
            // The editor itself is not a tab stop; focusing it focuses the writing surface (Arc's handle.focus()).
            _surface?.Focus(e.NavigationMethod);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_toolbar is not null)
        {
            // Narrow editors keep the inline marks; block types stay one slash away.
            var wide = e.NewSize.Width > 330;
            if (_toolbar.BlockTools.IsVisible != wide)
            {
                _toolbar.BlockTools.IsVisible = wide;
                _toolbar.Refit();
            }
        }
        ScheduleChromeSync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HtmlProperty)
        {
            if (!_writingValue && !string.Equals(Html, _lastHtml, StringComparison.Ordinal))
            {
                LoadContent(RichTextEditorHtml.ToDocument(Html), record: IsInitialized);
            }
        }
        else if (change.Property == MarkdownProperty)
        {
            if (!_writingValue && !string.Equals(Markdown, _lastMarkdown, StringComparison.Ordinal))
            {
                LoadContent(RichTextEditorMarkdown.ToDocument(Markdown), record: IsInitialized);
            }
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            CloseSlash();
            ((RichTextEditorCommand)UndoCommand).Refresh();
            ((RichTextEditorCommand)RedoCommand).Refresh();
            _surface?.OnDocumentChanged(false);
            ScheduleChromeSync();
        }
        else if (change.Property == PlaceholderProperty || change.Property == BlockHintProperty)
        {
            _surface?.InvalidateVisual();
        }
    }

    private void LoadContent(ImmutableList<RichTextEditorBlock> blocks, bool record)
    {
        CloseSlash();
        _engine.Load(blocks, record);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Change propagation
     * -------------------------------------------------------------------------------------------------------- */

    private void OnEngineChanged(bool content)
    {
        _surface?.OnDocumentChanged(content);
        if (content)
        {
            IsDocumentEmpty = RichTextEditorHtml.IsDocumentEmpty(_engine.Blocks);
            CanUndo = _engine.CanUndo;
            CanRedo = _engine.CanRedo;
            ((RichTextEditorCommand)UndoCommand).Refresh();
            ((RichTextEditorCommand)RedoCommand).Refresh();
            ScheduleFlush();
            RaiseEvent(new RichTextContentChangedEventArgs(ContentChangedEvent, new RichTextValue(_engine.Blocks)));
        }
        Formats = _engine.GetFormats();
        SyncSlash();
        ScheduleChromeSync();
        RevealCaret();
    }

    /// <summary>Documents up to this many blocks write Html and Markdown after every edit; longer ones after a pause in typing.</summary>
    private const int ImmediateFlushBlocks = 400;

    private void ScheduleFlush()
    {
        if (_engine.Blocks.Count > ImmediateFlushBlocks)
        {
            // Serializing a long document on every keystroke would cost more than the edit itself.
            _flushTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) =>
            {
                _flushTimer!.Stop();
                FlushValue();
            });
            _flushTimer.Stop();
            _flushTimer.Start();
            return;
        }
        if (_flushPending)
        {
            return;
        }
        _flushPending = true;
        Dispatcher.UIThread.Post(FlushValue, DispatcherPriority.Normal);
    }

    /// <summary>Writes a pending Html and Markdown update now (focus leaving, detaching).</summary>
    private void FlushPending()
    {
        if (_flushTimer is { IsEnabled: true })
        {
            _flushTimer.Stop();
            FlushValue();
        }
    }

    /// <summary>Writes Html and Markdown once per batch of edits, so typing stays cheap in long documents.</summary>
    private void FlushValue()
    {
        _flushPending = false;
        _flushTimer?.Stop();
        var blocks = _engine.Blocks;
        _lastHtml = RichTextEditorHtml.Write(blocks);
        _lastMarkdown = RichTextEditorMarkdown.Write(blocks);
        _writingValue = true;
        try
        {
            SetCurrentValue(HtmlProperty, _lastHtml);
            SetCurrentValue(MarkdownProperty, _lastMarkdown);
        }
        finally
        {
            _writingValue = false;
        }
    }

    internal void Announce(string message) => _liveRegion?.Announce(message);

    internal void RevealCaret()
    {
        if (_revealPending || _surface is null)
        {
            return;
        }
        _revealPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _revealPending = false;
            if (_surface is { IsKeyboardFocusWithin: true } surface)
            {
                var caret = surface.CaretRect(_engine.Selection.Focus);
                surface.BringIntoView(caret.Inflate(new Thickness(0, 4)));
            }
        }, DispatcherPriority.Loaded);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Text input and keyboard
     * -------------------------------------------------------------------------------------------------------- */

    internal void HandleTextInput(string text, bool composition)
    {
        if (IsReadOnly)
        {
            return;
        }
        _surface!.GoalX = null;
        // Line breaks and tabs only come from their keys (Enter, Shift+Enter, Tab), never as typed text.
        text = text.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", string.Empty);
        if (text.Length == 0)
        {
            return;
        }
        var result = _engine.InsertText(text, composition);
        if (composition)
        {
            _engine.MarkBoundary();
        }
        if (result.Converted)
        {
            Announce("Formatted");
        }
        if (result.SlashTyped)
        {
            var caret = _engine.Selection.Focus;
            _slash = (caret.Block, caret.Offset - 1, string.Empty);
            _slashActive = 0;
            ScheduleChromeSync();
        }
    }

    internal void OnCompositionStarted()
    {
        if (!_engine.Selection.IsCollapsed)
        {
            // Composition replaces the selection like typing does.
            _engine.DeleteBackward();
        }
    }

    internal void HandleKeyDown(KeyEventArgs e)
    {
        var surface = _surface!;
        var keymap = EditorsHotkeys.Keymap;
        var mod = EditorsHotkeys.IsMod(e.KeyModifiers);
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var key = e.Key;

        if (!IsReadOnly && IsSlashOpen)
        {
            var results = RichTextEditorSlashItem.Filter(_slash!.Value.Query);
            var index = Math.Min(_slashActive, results.Count - 1);
            switch (key)
            {
                case Key.Down:
                case Key.Up:
                    _slashActive = (index + (key == Key.Down ? 1 : -1) + results.Count) % results.Count;
                    Announce(results[_slashActive].Label);
                    ScheduleChromeSync();
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Tab:
                    ChooseSlash(results[index]);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    CloseSlash();
                    ScheduleChromeSync();
                    e.Handled = true;
                    return;
            }
        }

        // Clipboard and selection work in read-only mode too.
        if (EditorsHotkeys.Matches(keymap?.Copy, e) || (mod && !shift && key == Key.C))
        {
            _ = CopyAsync();
            e.Handled = true;
            return;
        }
        if (EditorsHotkeys.Matches(keymap?.SelectAll, e) || (mod && !shift && key == Key.A))
        {
            CloseSlash();
            _engine.SelectAll();
            e.Handled = true;
            return;
        }
        if (HandleNavigation(e, surface))
        {
            e.Handled = true;
            return;
        }
        if (IsReadOnly)
        {
            return;
        }
        if (EditorsHotkeys.Matches(keymap?.Cut, e) || (mod && !shift && key == Key.X))
        {
            _ = CutAsync();
            e.Handled = true;
            return;
        }
        if (EditorsHotkeys.Matches(keymap?.Paste, e) || (mod && !shift && key == Key.V))
        {
            _ = PasteAsync();
            e.Handled = true;
            return;
        }
        if (mod && key == Key.Z)
        {
            if (shift)
            {
                Redo();
            }
            else
            {
                Undo();
            }
            e.Handled = true;
            return;
        }
        if (mod && key == Key.Y)
        {
            Redo();
            e.Handled = true;
            return;
        }
        if (mod && !shift && key == Key.B)
        {
            _engine.ToggleMark(RichTextMark.Bold);
            e.Handled = true;
            return;
        }
        if (mod && !shift && key == Key.I)
        {
            _engine.ToggleMark(RichTextMark.Italic);
            e.Handled = true;
            return;
        }
        if (mod && shift && key == Key.X)
        {
            _engine.ToggleMark(RichTextMark.Strikethrough);
            e.Handled = true;
            return;
        }
        if (mod && key == Key.E)
        {
            _engine.ToggleMark(RichTextMark.Code);
            e.Handled = true;
            return;
        }
        if (mod && key == Key.K)
        {
            OpenLink();
            e.Handled = true;
            return;
        }
        if (mod && key == Key.U)
        {
            // Arc has no underline: the browser shortcut is swallowed.
            e.Handled = true;
            return;
        }
        if (alt && key == Key.F10)
        {
            if (_toolbar is { IsShown: true } toolbar && !toolbar.IsLinkFace)
            {
                toolbar.Bold.Focus(NavigationMethod.Tab);
                e.Handled = true;
            }
            return;
        }
        switch (key)
        {
            case Key.Tab:
                {
                    var block = _engine.Blocks[_engine.Selection.Anchor.Block];
                    if (block.IsList && !mod && !alt)
                    {
                        if (shift)
                        {
                            _engine.OutdentListItem();
                        }
                        else
                        {
                            _engine.IndentListItem();
                        }
                        e.Handled = true;
                    }
                    else if (block.IsCode && !shift && !mod && !alt)
                    {
                        _engine.InsertCodeIndent();
                        e.Handled = true;
                    }
                    return;
                }
            case Key.Enter:
                surface.GoalX = null;
                if (shift)
                {
                    _engine.InsertLineBreak();
                }
                else
                {
                    _engine.InsertParagraph();
                }
                e.Handled = true;
                return;
            case Key.Back when (e.KeyModifiers & KeyModifiers.Meta) != 0 && _engine.Selection.IsCollapsed:
                {
                    // macOS: Command+Backspace deletes to the start of the visual line.
                    var caret = _engine.Selection.Focus;
                    var (lineStart, _) = surface.LineBounds(caret);
                    if (lineStart < caret.Offset)
                    {
                        _engine.SetSelection(new RichTextEditorSelection(caret, caret with { Offset = lineStart }));
                    }
                    _engine.DeleteBackward();
                    e.Handled = true;
                    return;
                }
            case Key.Back:
                surface.GoalX = null;
                _engine.DeleteBackward(word: (e.KeyModifiers & EditorsHotkeys.WordModifiers) != 0 && EditorsHotkeys.WordModifiers != KeyModifiers.None);
                e.Handled = true;
                return;
            case Key.Delete:
                surface.GoalX = null;
                _engine.DeleteForward(word: (e.KeyModifiers & EditorsHotkeys.WordModifiers) != 0 && EditorsHotkeys.WordModifiers != KeyModifiers.None);
                e.Handled = true;
                return;
            case Key.Escape:
                if (_toolbar is { IsLinkFace: true })
                {
                    CloseLink(focusEditor: true);
                    e.Handled = true;
                }
                return;
        }
    }

    /// <summary>Caret movement and selection extension, by grapheme, word, visual line, line edge, page and document.</summary>
    private bool HandleNavigation(KeyEventArgs e, RichTextEditorSurface surface)
    {
        var keymap = EditorsHotkeys.Keymap;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var selection = _engine.Selection;
        var blocks = _engine.Blocks;
        RichTextEditorPosition? target = null;
        var keepGoal = false;

        if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfDocument, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfDocumentWithSelection, e)
            || (e.Key == Key.Home && (e.KeyModifiers & KeyModifiers.Control) != 0))
        {
            target = RichTextEditorEngine.NearestTextual(blocks, 0, forward: true);
        }
        else if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfDocument, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfDocumentWithSelection, e)
                 || (e.Key == Key.End && (e.KeyModifiers & KeyModifiers.Control) != 0))
        {
            target = RichTextEditorEngine.End(blocks);
        }
        else if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfLine, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheStartOfLineWithSelection, e) || e.Key == Key.Home)
        {
            var (start, _) = surface.LineBounds(selection.Focus);
            target = selection.Focus with { Offset = start };
        }
        else if (EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfLine, e) || EditorsHotkeys.Matches(keymap?.MoveCursorToTheEndOfLineWithSelection, e) || e.Key == Key.End)
        {
            var (_, end) = surface.LineBounds(selection.Focus);
            target = selection.Focus with { Offset = end };
        }
        else
        {
            var word = EditorsHotkeys.WordModifiers != KeyModifiers.None && (e.KeyModifiers & EditorsHotkeys.WordModifiers) == EditorsHotkeys.WordModifiers;
            switch (e.Key)
            {
                case Key.Left:
                    if (!shift && !selection.IsCollapsed && !word)
                    {
                        target = selection.Start;
                    }
                    else
                    {
                        target = Step(blocks, selection.Focus, -1, word);
                    }
                    break;
                case Key.Right:
                    if (!shift && !selection.IsCollapsed && !word)
                    {
                        target = selection.End;
                    }
                    else
                    {
                        target = Step(blocks, selection.Focus, 1, word);
                    }
                    break;
                case Key.Up:
                case Key.Down:
                    {
                        if (IsSlashOpen)
                        {
                            return false;
                        }
                        var direction = e.Key == Key.Up ? -1 : 1;
                        var origin = !shift && !selection.IsCollapsed ? (direction < 0 ? selection.Start : selection.End) : selection.Focus;
                        var goal = surface.GoalX ?? surface.LineRect(origin).X;
                        surface.GoalX = goal;
                        keepGoal = true;
                        target = surface.MoveVertical(origin, goal, direction);
                        break;
                    }
                case Key.PageUp:
                case Key.PageDown:
                    {
                        var direction = e.Key == Key.PageUp ? -1 : 1;
                        var caret = surface.LineRect(selection.Focus);
                        var page = Math.Max(caret.Height, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 600) * 0.85);
                        var goal = surface.GoalX ?? caret.X;
                        surface.GoalX = goal;
                        keepGoal = true;
                        target = surface.HitTest(new Point(goal, caret.Center.Y + direction * page));
                        break;
                    }
                default:
                    return false;
            }
        }
        if (target is not { } position)
        {
            return false;
        }
        if (!keepGoal)
        {
            surface.GoalX = null;
        }
        _engine.SetSelection(shift ? selection with { Focus = position } : RichTextEditorSelection.Caret(position));
        return true;
    }

    private static RichTextEditorPosition Step(ImmutableList<RichTextEditorBlock> blocks, RichTextEditorPosition origin, int direction, bool word)
    {
        var block = blocks[origin.Block];
        if (direction < 0)
        {
            if (origin.Offset > 0)
            {
                return origin with { Offset = word ? RichTextEditorText.PreviousWord(block.Text, origin.Offset) : RichTextEditorText.PreviousCaret(block.Text, origin.Offset) };
            }
            for (var i = origin.Block - 1; i >= 0; i--)
            {
                if (blocks[i].IsTextual)
                {
                    return new RichTextEditorPosition(i, blocks[i].Length);
                }
            }
            return origin;
        }
        if (origin.Offset < block.Length)
        {
            return origin with { Offset = word ? RichTextEditorText.NextWord(block.Text, origin.Offset) : RichTextEditorText.NextCaret(block.Text, origin.Offset) };
        }
        for (var i = origin.Block + 1; i < blocks.Count; i++)
        {
            if (blocks[i].IsTextual)
            {
                return new RichTextEditorPosition(i, 0);
            }
        }
        return origin;
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Clipboard
     * -------------------------------------------------------------------------------------------------------- */

    private async Task CopyAsync()
    {
        if (_engine.Selection.IsCollapsed || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        var fragment = _engine.GetFragment();
        var item = new DataTransferItem();
        item.Set(DataFormat.Text, _engine.GetSelectedText());
        item.Set(s_markdownFormat, RichTextEditorMarkdown.Write(fragment));
        item.Set(s_fragmentFormat, new RichTextEditorClipboardFragment(fragment));
        var data = new DataTransfer();
        data.Add(item);
        try
        {
            await clipboard.SetDataAsync(data);
        }
        catch (Exception)
        {
            // The platform refused the clipboard; nothing to do.
        }
    }

    private async Task CutAsync()
    {
        if (_engine.Selection.IsCollapsed)
        {
            return;
        }
        await CopyAsync();
        _engine.DeleteSelectionAsCommand();
    }

    private async Task PasteAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        try
        {
            var own = await clipboard.TryGetInProcessDataAsync();
            if (own is not null && await own.TryGetValueAsync(s_fragmentFormat) is { } fragment)
            {
                PasteContent(await own.TryGetTextAsync() ?? string.Empty, fragment.Blocks, html: null);
                return;
            }
            using var data = await clipboard.TryGetDataAsync();
            if (data is null)
            {
                return;
            }
            var text = await data.TryGetTextAsync() ?? string.Empty;
            if (await data.TryGetValueAsync(s_markdownFormat) is { } markdown)
            {
                PasteContent(text, RichTextEditorMarkdown.ToDocument(markdown), html: null);
                return;
            }
            string? html = null;
            foreach (var format in data.Formats)
            {
                if (Array.IndexOf(s_htmlFormats, format.Identifier) >= 0)
                {
                    html = await data.TryGetValueAsync(DataFormat.CreateStringPlatformFormat(format.Identifier));
                    if (!string.IsNullOrEmpty(html))
                    {
                        break;
                    }
                }
            }
            PasteContent(text, null, html);
        }
        catch (Exception)
        {
            // Clipboard formats a platform cannot convert are skipped.
        }
    }

    /// <summary>Arc's paste rules: HTML is sanitized, Markdown-looking text is converted, other text goes in as text.</summary>
    internal void PasteContent(string text, IReadOnlyList<RichTextEditorBlock>? blocks, string? html)
    {
        if (IsReadOnly)
        {
            return;
        }
        CloseSlash();
        if (blocks is null)
        {
            if (!string.IsNullOrEmpty(html))
            {
                var fragmentStart = html.IndexOf("<!--StartFragment-->", StringComparison.OrdinalIgnoreCase);
                var fragmentEnd = html.IndexOf("<!--EndFragment-->", StringComparison.OrdinalIgnoreCase);
                if (fragmentStart >= 0 && fragmentEnd > fragmentStart)
                {
                    html = html[(fragmentStart + 20)..fragmentEnd];
                }
                else if (html.IndexOf('<') is var first and > 0)
                {
                    html = html[first..];
                }
                blocks = RichTextEditorHtml.ToBlocks(html);
            }
            else if (RichTextEditorMarkdown.LooksLikeMarkdown(text) || text.Contains("\n\n", StringComparison.Ordinal))
            {
                blocks = RichTextEditorMarkdown.ToDocument(text);
            }
        }
        if (blocks is { Count: 0 })
        {
            blocks = null;
        }
        _engine.Paste(text, blocks);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Surface callbacks
     * -------------------------------------------------------------------------------------------------------- */

    internal void OnSurfacePointerDown()
    {
        if (_toolbar is { IsLinkFace: true })
        {
            // Arc: a press outside the toolbar drops the link field without applying it.
            _savedLinkRange = null;
            _toolbar.SetLinkFace(false, animate: false);
            _toolbar.Dismiss();
            StopLinkWatch();
        }
        CloseSlash();
    }

    internal void OnDragEnded() => ScheduleChromeSync();

    internal void OnSurfaceFocusChanged(bool focused)
    {
        if (!focused)
        {
            CloseSlashIfFocusLeft();
            FlushPending();
        }
        ScheduleChromeSync();
    }

    private void CloseSlashIfFocusLeft()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsFocusWithinEditor())
            {
                CloseSlash();
                ScheduleChromeSync();
            }
        }, DispatcherPriority.Loaded);
    }

    private bool IsFocusWithinEditor() =>
        _surface is { IsKeyboardFocusWithin: true } || _overlay is { IsKeyboardFocusWithin: true };

    /* ----------------------------------------------------------------------------------------------------------
     * Slash menu
     * -------------------------------------------------------------------------------------------------------- */

    private void SyncSlash()
    {
        if (_slash is not { } slash)
        {
            return;
        }
        var selection = _engine.Selection;
        var blocks = _engine.Blocks;
        var valid = selection.IsCollapsed && slash.Block < blocks.Count && selection.Focus.Block == slash.Block
                    && selection.Focus.Offset > slash.Offset && slash.Offset < blocks[slash.Block].Length
                    && blocks[slash.Block].Text[slash.Offset] == '/';
        var query = valid ? blocks[slash.Block].Text[(slash.Offset + 1)..selection.Focus.Offset] : string.Empty;
        if (!valid || query.Length > 24 || (query.Length > 0 && char.IsWhiteSpace(query[0])) || HasDoubleWhitespace(query))
        {
            CloseSlash();
        }
        else if (query != slash.Query)
        {
            _slash = slash with { Query = query };
        }
    }

    private static bool HasDoubleWhitespace(string query)
    {
        for (var i = 1; i < query.Length; i++)
        {
            if (char.IsWhiteSpace(query[i]) && char.IsWhiteSpace(query[i - 1]))
            {
                return true;
            }
        }
        return false;
    }

    private void CloseSlash()
    {
        if (_slash is null)
        {
            return;
        }
        _slash = null;
        _slashMenu?.Dismiss();
    }

    private void ChooseSlash(RichTextEditorSlashItem item)
    {
        if (_slash is not { } slash)
        {
            return;
        }
        var selection = _engine.Selection;
        var caret = selection.Focus.Block == slash.Block ? selection.Focus.Offset : slash.Offset + 1 + slash.Query.Length;
        _slash = null;
        _slashMenu?.Dismiss();
        _engine.ApplySlashChoice(slash.Block, slash.Offset, caret, item.Type);
        Announce($"{item.Label} added");
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Toolbar and links
     * -------------------------------------------------------------------------------------------------------- */

    private void EnsureOverlay()
    {
        if (_overlay is not null)
        {
            return;
        }
        _toolbar = new RichTextEditorToolbar();
        _slashMenu = new RichTextEditorSlashMenu();
        _overlay = new RichTextEditorOverlay { Children = { _toolbar, _slashMenu } };
        AdornerLayer.SetIsClipEnabled(_overlay, true);
        _toolbar.BlockTools.IsVisible = Bounds.Width > 330;
        _toolbar.Bold.Click += (_, _) => _engine.ToggleMark(RichTextMark.Bold);
        _toolbar.Italic.Click += (_, _) => _engine.ToggleMark(RichTextMark.Italic);
        _toolbar.Strike.Click += (_, _) => _engine.ToggleMark(RichTextMark.Strikethrough);
        _toolbar.Code.Click += (_, _) => _engine.ToggleMark(RichTextMark.Code);
        _toolbar.Link.Click += (_, _) => OpenLink();
        _toolbar.Heading1.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.Heading1);
        _toolbar.Heading2.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.Heading2);
        _toolbar.Quote.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.Quote);
        _toolbar.Back.Click += (_, _) => CloseLink(focusEditor: true);
        _toolbar.Unlink.Click += (_, _) => ApplyLink(remove: true);
        _toolbar.Apply.Click += (_, _) => ApplyLink(remove: false);
        _toolbar.LinkBox.KeyDown += OnLinkBoxKeyDown;
        _toolbar.LinkBox.TextChanged += (_, _) => UpdateApplyReady();
        _toolbar.KeyDown += OnToolbarKeyDown;
        _slashMenu.Hovered += index =>
        {
            if (index >= 0 && index != _slashActive)
            {
                _slashActive = index;
                ScheduleChromeSync();
            }
        };
        _slashMenu.Chosen += ChooseSlash;
        AdornerLayer.SetAdorner(this, _overlay);
    }

    private void UpdateApplyReady()
    {
        if (_toolbar is { } toolbar)
        {
            toolbar.Apply.Classes.Set("ready", !string.IsNullOrWhiteSpace(toolbar.LinkBox.Text));
        }
    }

    private void OnToolbarKeyDown(object? sender, KeyEventArgs e)
    {
        if (_toolbar is not { } toolbar || toolbar.IsLinkFace)
        {
            return;
        }
        var buttons = toolbar.FormatButtons.ToList();
        var at = buttons.FindIndex(b => b.IsKeyboardFocusWithin);
        if (e.Key is Key.Right or Key.Left)
        {
            var next = (at + (e.Key == Key.Right ? 1 : -1) + buttons.Count) % buttons.Count;
            buttons[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _surface?.Focus();
            e.Handled = true;
        }
    }

    private void OnLinkBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyLink(remove: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseLink(focusEditor: true);
            e.Handled = true;
        }
    }

    private void OpenLink()
    {
        if (IsReadOnly)
        {
            return;
        }
        var range = _engine.LinkRange(out var href);
        if (range is null)
        {
            return;
        }
        EnsureOverlay();
        CloseSlash();
        _engine.SetSelection(range.Value);
        _savedLinkRange = range;
        var toolbar = _toolbar!;
        toolbar.Unlink.IsVisible = href is not null;
        toolbar.LinkBox.Text = href ?? string.Empty;
        UpdateApplyReady();
        // Open the bar first if the selection had none yet (a link edited from a caret).
        SyncChromeNow();
        toolbar.SetLinkFace(true);
        StartLinkWatch();
        Dispatcher.UIThread.Post(() =>
        {
            toolbar.LinkBox.Focus();
            toolbar.LinkBox.CaretIndex = toolbar.LinkBox.Text?.Length ?? 0;
        }, DispatcherPriority.Loaded);
    }

    private void CloseLink(bool focusEditor)
    {
        var range = _savedLinkRange;
        _savedLinkRange = null;
        StopLinkWatch();
        _toolbar?.SetLinkFace(false);
        if (focusEditor)
        {
            _surface?.Focus();
            if (range is { } saved)
            {
                _engine.SetSelection(saved);
            }
        }
        ScheduleChromeSync();
    }

    private void ApplyLink(bool remove)
    {
        var range = _savedLinkRange;
        if (range is null || _toolbar is not { } toolbar)
        {
            CloseLink(focusEditor: true);
            return;
        }
        var url = remove ? string.Empty : RichTextEditorMarkdown.NormalizeUrl(toolbar.LinkBox.Text);
        _savedLinkRange = null;
        StopLinkWatch();
        _surface?.Focus();
        _engine.SetSelection(range.Value);
        _engine.SetLink(range.Value, url);
        toolbar.SetLinkFace(false, animate: false);
        toolbar.Dismiss();
        Announce(url.Length == 0 ? "Link removed" : "Link added");
    }

    /// <summary>While the link field is open, a press anywhere outside the toolbar closes it (Arc's document pointerdown).</summary>
    private void StartLinkWatch()
    {
        StopLinkWatch();
        _linkWatchRoot = TopLevel.GetTopLevel(this);
        _linkWatchRoot?.AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void StopLinkWatch()
    {
        _linkWatchRoot?.RemoveHandler(PointerPressedEvent, OnAnyPointerPressed);
        _linkWatchRoot = null;
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_toolbar is not { IsLinkFace: true } toolbar)
        {
            StopLinkWatch();
            return;
        }
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, toolbar))
            {
                return;
            }
        }
        _savedLinkRange = null;
        StopLinkWatch();
        toolbar.SetLinkFace(false, animate: false);
        toolbar.Dismiss();
    }

    private void ScheduleChromeSync()
    {
        if (_chromePending)
        {
            return;
        }
        _chromePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _chromePending = false;
            SyncChromeNow();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Arc's selection driven chrome: the slash menu at the slash, the toolbar above (or below) the selection.</summary>
    private void SyncChromeNow()
    {
        var surface = _surface;
        if (surface is null || !IsAttachedToVisualTree())
        {
            return;
        }
        var selection = _engine.Selection;
        var focused = IsFocusWithinEditor();
        var linkFace = _toolbar is { IsLinkFace: true };
        var wantsBar = !IsReadOnly && !selection.IsCollapsed && !surface.IsDragging && !IsSlashOpen && (focused || linkFace)
                       && !_engine.Blocks[selection.Start.Block].IsCode;
        if (!wantsBar && !IsSlashOpen && _overlay is null)
        {
            return;
        }
        EnsureOverlay();
        var toolbar = _toolbar!;
        var menu = _slashMenu!;
        UpdateToolbarState(toolbar);
        var offset = surface.TranslatePoint(default, this) ?? default;
        var band = ClipBand();

        if (IsSlashOpen && focused && _slash is { } slash)
        {
            var results = RichTextEditorSlashItem.Filter(slash.Query);
            _slashActive = Math.Min(_slashActive, results.Count - 1);
            var start = surface.CaretRect(new RichTextEditorPosition(slash.Block, slash.Offset)).Translate(offset);
            var top = start.Top;
            var bottom = start.Bottom;
            var above = band.Bottom - bottom < 330 && top - band.Top > band.Bottom - bottom;
            var room = above ? top - band.Top - 12 : band.Bottom - bottom - 12;
            var x = Math.Max(0, Math.Min(start.Left - 8, Bounds.Width - 244));
            var y = above ? top - 6 : bottom + 6;
            menu.Present(new Point(x, y), above, Math.Max(150, double.IsFinite(room) ? room : 320), Bounds.Width - 8, results, _slashActive);
        }
        else
        {
            menu.Dismiss();
        }

        if (linkFace)
        {
            return;
        }
        if (!wantsBar)
        {
            toolbar.Dismiss();
            return;
        }
        var bounds = surface.SelectionBounds(selection.Start, selection.End);
        var caretLine = surface.LineRect(selection.Start);
        var first = (bounds?.First ?? caretLine).Translate(offset);
        var whole = (bounds?.Whole ?? caretLine).Translate(offset);
        var below = first.Top - band.Top < 60;
        toolbar.Present(whole.Center.X, below ? whole.Bottom + 8 : first.Top - 8, below, Bounds.Width);
    }

    private void UpdateToolbarState(RichTextEditorToolbar toolbar)
    {
        var formats = Formats;
        toolbar.Bold.IsChecked = formats.IsBold;
        toolbar.Italic.IsChecked = formats.IsItalic;
        toolbar.Strike.IsChecked = formats.IsStrikethrough;
        toolbar.Code.IsChecked = formats.IsCode;
        toolbar.Link.IsChecked = formats.Link is not null;
        var linkLabel = formats.Link is null ? "Link" : "Edit link";
        AutomationProperties.SetName(toolbar.Link, linkLabel);
        Tooltip.SetTip(toolbar.Link, linkLabel + " (⌘K)");
        toolbar.Heading1.IsChecked = formats.BlockType == RichTextBlockType.Heading1;
        toolbar.Heading2.IsChecked = formats.BlockType == RichTextBlockType.Heading2;
        toolbar.Quote.IsChecked = formats.BlockType == RichTextBlockType.Quote;
        ArcMotion.SetLevel(toolbar, ArcMotion.GetLevel(this));
        ArcMotion.SetLevel(_slashMenu!, ArcMotion.GetLevel(this));
    }

    /// <summary>The visible band of the nearest clipping ancestors, in editor coordinates, so floating layers flip before they get cut off.</summary>
    private Rect ClipBand()
    {
        var top = double.NegativeInfinity;
        var bottom = double.PositiveInfinity;
        for (var visual = this.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (!visual.ClipToBounds && visual is not TopLevel)
            {
                continue;
            }
            var transform = visual.TransformToVisual(this);
            if (transform is null)
            {
                continue;
            }
            var rect = new Rect(visual.Bounds.Size).TransformToAABB(transform.Value);
            top = Math.Max(top, rect.Top);
            bottom = Math.Min(bottom, rect.Bottom);
        }
        return new Rect(0, double.IsFinite(top) ? top : -10000, Bounds.Width, double.IsFinite(top) && double.IsFinite(bottom) ? Math.Max(0, bottom - top) : 20000);
    }

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;
}

/// <summary>The in-process clipboard payload: the copied blocks, pasted back without loss.</summary>
internal sealed class RichTextEditorClipboardFragment(IReadOnlyList<RichTextEditorBlock> blocks)
{
    public IReadOnlyList<RichTextEditorBlock> Blocks { get; } = blocks;
}

/// <summary>A command bound to the editor's history state.</summary>
internal sealed class RichTextEditorCommand(RichTextEditor owner, Action execute, Func<bool> canExecute) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute();

    public void Execute(object? parameter)
    {
        if (canExecute())
        {
            execute();
            owner.FocusEditor();
        }
    }

    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
