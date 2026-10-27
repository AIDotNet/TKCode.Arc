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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
/// italic, strikethrough, inline code and links. The <see cref="RichTextEditorProfile.Docs"/> profile adds images and tables:
/// <see cref="ImageSourceRequested"/> lets the host decide where a picked, pasted or dropped picture is kept, and
/// <see cref="ImageLoading"/> lets it load the pictures itself.
/// </summary>
/// <remarks>
/// Keyboard: Mod+B, Mod+I, Mod+Shift+X and Mod+E toggle marks, Mod+K edits a link, Mod+Z / Mod+Shift+Z / Mod+Y undo and
/// redo, Tab and Shift+Tab indent list items, Shift+Enter breaks a line, "/" opens the block menu, Alt+F10 moves into
/// the toolbar. Mod is Command on macOS and Control elsewhere. In a table, Tab and Shift+Tab move between cells (Tab in the last
/// cell adds a row), Enter moves down the column and the context menu edits rows and columns and merges and splits cells.
/// </remarks>
[TemplatePart(SurfacePart, typeof(RichTextEditorSurface))]
[TemplatePart(LiveRegionPart, typeof(EditorsLiveRegion))]
public partial class RichTextEditor : TemplatedControl
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

    /// <summary>The feature set: <c>Arc</c> (the default) keeps Arc's blocks and marks; <c>Docs</c> adds underline, text and highlight colors, to-do lists, text alignment and a language label on code blocks.</summary>
    public static readonly StyledProperty<RichTextEditorProfile> ProfileProperty =
        AvaloniaProperty.Register<RichTextEditor, RichTextEditorProfile>(nameof(Profile));

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
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Images.HostLoader = LoadImageFromHost;
        Images.Changed += source => _surface?.OnImageChanged(source);
        UndoCommand = new RichTextEditorCommand(this, () => Undo(), () => CanUndo && !IsReadOnly);
        RedoCommand = new RichTextEditorCommand(this, () => Redo(), () => CanRedo && !IsReadOnly);
    }

    /// <inheritdoc cref="ProfileProperty"/>
    public RichTextEditorProfile Profile { get => GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }

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

    /// <summary>
    /// Raised when the editor holds a picture the user picked, pasted or dropped and the document must point at it. Set
    /// <see cref="RichTextImageSourceRequestedEventArgs.Source"/> or <see cref="RichTextImageSourceRequestedEventArgs.SourceTask"/> to keep the file
    /// where you want (upload it, copy it into your store); with no answer the picture is embedded as a <c>data:</c> address.
    /// </summary>
    public event EventHandler<RichTextImageSourceRequestedEventArgs>? ImageSourceRequested;

    /// <summary>
    /// Raised when the editor needs the pixels of an image's address. Set <see cref="RichTextImageLoadingEventArgs.Bitmap"/> or
    /// <see cref="RichTextImageLoadingEventArgs.StreamTask"/> to load it yourself; with no answer <c>data:</c>, http(s), <c>avares:</c> and file addresses are read by the editor.
    /// </summary>
    public event EventHandler<RichTextImageLoadingEventArgs>? ImageLoading;

    internal RichTextEditorEngine Engine => _engine;

    internal RichTextEditorImageCache Images { get; } = new();

    internal RichTextEditorSurface? Surface => _surface;

    internal RichTextEditorEngine EngineForHandle => _engine;

    internal RichTextEditorBlockHandle? BlockHandle => _blockHandle;

    internal bool IsSlashOpenForTests => IsSlashOpen;

    private RichTextEditorBlockHandle? _blockHandle;

    internal RichTextEditorToolbar? Toolbar => _toolbar;

    internal RichTextEditorSlashMenu? SlashMenu => _slashMenu;

    internal EditorsLiveRegion? LiveRegion => _liveRegion;

    /// <summary>True while the link field is open: the selection it edits stays highlighted.</summary>
    internal bool KeepsSelectionVisible => _toolbar is { IsLinkFace: true } && _savedLinkRange is not null || IsFindOpen;

    internal bool IsSlashOpen => _slash is { } slash && RichTextEditorSlashItem.Filter(slash.Query, _engine.Docs).Count > 0;

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
    public void SetHtml(string? html) => LoadContent(RichTextEditorHtml.ToDocument(html, _engine.Docs), record: true);

    /// <summary>Replaces the content with Markdown (one undo step). Unsupported constructs are converted as documented by <see cref="RichTextEditorMarkdown.Analyze(string?)"/>.</summary>
    public void SetMarkdown(string? markdown) => LoadContent(RichTextEditorMarkdown.ToDocument(markdown, _engine.Docs), record: true);

    /// <summary>
    /// Replaces the content with Markdown only when the editor can store all of it. When
    /// <see cref="RichTextEditorMarkdown.Analyze(string?)"/> reports a loss, nothing changes, the current content and the undo
    /// history stay as they were, and <paramref name="report"/> carries the issues (with line, column and reason) so
    /// the caller can keep the file, warn or open a source editor. A source that is only normalized (for example `*`
    /// bullets written back as `-`) is imported as one undo step, like <see cref="SetMarkdown"/>.
    /// </summary>
    /// <returns>True when the content was replaced; false when the import was refused.</returns>
    public bool TrySetMarkdown(string? markdown, out RichTextImportReport report)
    {
        report = RichTextEditorMarkdown.Analyze(markdown, _engine.Docs ? RichTextEditorProfile.Docs : RichTextEditorProfile.Arc);
        if (report.HasLosses)
        {
            return false;
        }
        SetMarkdown(markdown);
        return true;
    }

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

    /// <summary>Sets the text color of the selection (or of the next typed text at a caret); only the <see cref="RichTextEditorProfile.Docs"/> profile colors text.</summary>
    /// <param name="color">The palette color; <see cref="RichTextColor.Default"/> clears it.</param>
    public void SetTextColor(RichTextColor color)
    {
        if (!IsReadOnly && _engine.SetTextColor(color))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>Sets the highlight color of the selection (or of the next typed text at a caret); only the <see cref="RichTextEditorProfile.Docs"/> profile highlights text.</summary>
    /// <param name="color">The palette color; <see cref="RichTextColor.Default"/> clears it.</param>
    public void SetHighlight(RichTextColor color)
    {
        if (!IsReadOnly && _engine.SetHighlight(color))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>
    /// Sets the text size of the selection (or of the next typed text at a caret), relative to the size of each block's text; only the
    /// <see cref="RichTextEditorProfile.Docs"/> profile sizes text. Html writes it as <c>&lt;span data-size="large"&gt;</c>; Markdown has no
    /// sizes and keeps the text only.
    /// </summary>
    /// <param name="size">The size; <see cref="RichTextSize.Default"/> clears it.</param>
    public void SetTextSize(RichTextSize size)
    {
        if (!IsReadOnly && _engine.SetSize(size))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>
    /// Sets the line spacing of the selected blocks (code blocks and table cells keep their own); only the
    /// <see cref="RichTextEditorProfile.Docs"/> profile spaces lines. Html writes it as <c>data-spacing</c>; Markdown keeps the text only.
    /// </summary>
    public void SetLineSpacing(RichTextLineSpacing spacing)
    {
        if (!IsReadOnly && _engine.SetLineSpacing(spacing))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>Aligns the selected paragraphs, headings and quotes; only the <see cref="RichTextEditorProfile.Docs"/> profile aligns text.</summary>
    public void SetAlign(RichTextAlign align)
    {
        if (!IsReadOnly && _engine.SetAlign(align))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>Sets the color of the callout holding the caret; only the <see cref="RichTextEditorProfile.Docs"/> profile has callouts.</summary>
    /// <param name="color">The palette color; <see cref="RichTextColor.Default"/> is neutral gray.</param>
    public void SetCalloutColor(RichTextColor color)
    {
        if (!IsReadOnly && _engine.SetCallout(_engine.Selection.Focus.Block, color, null))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>Sets the icon of the callout holding the caret (an emoji or a short symbol); only the <see cref="RichTextEditorProfile.Docs"/> profile has callouts.</summary>
    /// <param name="icon">The icon text.</param>
    public void SetCalloutIcon(string icon)
    {
        if (!IsReadOnly && !string.IsNullOrWhiteSpace(icon) && _engine.SetCallout(_engine.Selection.Focus.Block, null, icon))
        {
            Announce("Formatted");
            FocusEditor();
        }
    }

    /// <summary>Sets the language label shown on the code block holding the caret; only the <see cref="RichTextEditorProfile.Docs"/> profile keeps it.</summary>
    /// <param name="language">The label, such as <c>csharp</c>; null or empty removes it.</param>
    public void SetCodeLanguage(string? language)
    {
        if (!IsReadOnly && _engine.SetCodeLanguage(language))
        {
            FocusEditor();
        }
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

    /// <summary>
    /// Inserts an image at the caret, as its own block; only the <see cref="RichTextEditorProfile.Docs"/> profile has images.
    /// <paramref name="source"/> is stored as given (a URL, a <c>data:</c> address or one of your own) and shown through <see cref="ImageLoading"/> or the built in loader.
    /// </summary>
    /// <param name="source">The image's address; scripts and non-image <c>data:</c> addresses are refused.</param>
    /// <param name="alt">The description for assistive technology and the Markdown alt text.</param>
    /// <returns>True when the image was inserted.</returns>
    public bool InsertImage(string source, string? alt = null)
    {
        if (IsReadOnly || _engine.InsertImage(source, alt) < 0)
        {
            return false;
        }
        CloseSlash();
        Announce("Image added");
        FocusEditor();
        return true;
    }

    /// <summary>
    /// Opens the system file picker for images and inserts what the user picks, asking <see cref="ImageSourceRequested"/> where each file should live.
    /// </summary>
    public async Task PickImageAsync()
    {
        if (IsReadOnly || !_engine.Docs || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Insert image",
            AllowMultiple = true,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });
        foreach (var file in files)
        {
            await InsertImageFileAsync(RichTextImageOrigin.Picker, file);
        }
    }

    /// <summary>Forgets the loaded pictures (of one address, or all) so they are loaded again, for example after your store changed them.</summary>
    public void ReloadImages(string? source = null)
    {
        Images.Reset(source);
        _surface?.InvalidateStyle();
    }

    /// <summary>Inserts a table of empty cells at the caret and moves into its first cell; only the <see cref="RichTextEditorProfile.Docs"/> profile has tables.</summary>
    /// <param name="rows">The number of rows, the first being the header (1 to 100).</param>
    /// <param name="columns">The number of columns (1 to 20).</param>
    /// <returns>True when the table was inserted.</returns>
    public bool InsertTable(int rows = 3, int columns = 3)
    {
        if (IsReadOnly || !_engine.InsertTable(rows, columns))
        {
            return false;
        }
        CloseSlash();
        Announce("Table added");
        FocusEditor();
        return true;
    }

    /// <summary>True when the caret is in a table cell (for your own toolbar).</summary>
    public bool IsInTable => _engine.IsInTable;

    /// <summary>Adds an empty row below (or above) the caret's row; above the header row is the same as below it.</summary>
    public bool AddTableRow(bool below = true) => EditTable(_engine.AddTableRow(below), "Row added");

    /// <summary>Adds an empty column after (or before) the caret's column.</summary>
    public bool AddTableColumn(bool after = true) => EditTable(_engine.AddTableColumn(after), "Column added");

    /// <summary>Deletes the caret's row; the only row takes the whole table with it.</summary>
    public bool DeleteTableRow() => !IsReadOnly && EditTable(_engine.DeleteTableRow(), "Row deleted");

    /// <summary>Deletes the caret's column; the only column takes the whole table with it.</summary>
    public bool DeleteTableColumn() => !IsReadOnly && EditTable(_engine.DeleteTableColumn(), "Column deleted");

    /// <summary>True when the selection reaches two or more cells of one table, so <see cref="MergeTableCells"/> can merge them.</summary>
    public bool CanMergeTableCells => !IsReadOnly && _engine.CanMergeCells;

    /// <summary>True when the caret is in a merged cell, so <see cref="SplitTableCell"/> can split it.</summary>
    public bool CanSplitTableCell => !IsReadOnly && _engine.CanSplitCell;

    /// <summary>
    /// Merges the cells the selection reaches (the rectangle between the cell it starts in and the one it ends in) into one. Their texts
    /// join as lines, and a merged cell that sticks out of the rectangle widens it. The Html output writes <c>colspan</c> and <c>rowspan</c>;
    /// the Markdown output has no merged cells and writes the covered cells empty.
    /// </summary>
    /// <returns>True when cells were merged.</returns>
    public bool MergeTableCells() => !IsReadOnly && EditTable(_engine.MergeCells(), "Cells merged");

    /// <summary>Splits the merged cell at the caret back into single cells; its text stays in the first one.</summary>
    /// <returns>True when a merged cell was split.</returns>
    public bool SplitTableCell() => !IsReadOnly && EditTable(_engine.SplitCell(), "Cell split");

    /// <summary>True when the table at the caret draws its first row as a header (tinted, medium weight); new tables start with one.</summary>
    public bool TableHasHeaderRow => _engine.HasHeaderRow;

    /// <summary>True when the table at the caret draws its first column as a header.</summary>
    public bool TableHasHeaderColumn => _engine.HasHeaderColumn;

    /// <summary>
    /// Turns the header row of the table at the caret on or off. The Html output writes header cells as <c>th</c>; Markdown tables always
    /// have a header row.
    /// </summary>
    /// <returns>True when the table changed.</returns>
    public bool SetTableHeaderRow(bool on) => !IsReadOnly && EditTable(_engine.SetTableHeader(column: false, on), on ? "Header row on" : "Header row off");

    /// <summary>Turns the header column of the table at the caret on or off.</summary>
    /// <returns>True when the table changed.</returns>
    public bool SetTableHeaderColumn(bool on) => !IsReadOnly && EditTable(_engine.SetTableHeader(column: true, on), on ? "Header column on" : "Header column off");

    /// <summary>The background shared by the cells the selection reaches, or <see cref="RichTextColor.Default"/> when they have none or several.</summary>
    public RichTextColor TableCellColor => _engine.CellTone;

    /// <summary>
    /// Fills the cells the selection reaches with a palette color (<see cref="RichTextColor.Default"/> clears it). The Html output writes
    /// <c>data-bg</c>; Markdown has no cell colors.
    /// </summary>
    /// <returns>True when a cell changed.</returns>
    public bool SetTableCellColor(RichTextColor color) => !IsReadOnly && EditTable(_engine.SetCellTone(color), "Cell color changed");

    /// <summary>
    /// Shares the width of the table at the caret evenly between its columns again (dragging a column border, or double-clicking it,
    /// sets widths too). The Html output writes widths as <c>col</c> elements.
    /// </summary>
    /// <returns>True when the widths changed.</returns>
    public bool DistributeTableColumns() => !IsReadOnly && IsInTable && EditTable(_engine.SetColumnWidths(_engine.Selection.Focus.Block, Array.Empty<int>()), "Columns distributed");

    /// <summary>
    /// Selects every cell of the caret's row. A selection from one cell to another picks the rectangle between them: deleting empties
    /// those cells, copying gives tab-separated text (and a table when pasted back), and pasting a table or tab-separated text fills cells
    /// from the first one, adding rows and columns as needed. The grips at the top and left of a table select a column or row on a click.
    /// </summary>
    public void SelectTableRow()
    {
        if (IsInTable)
        {
            var cell = _engine.Blocks[_engine.Selection.Focus.Block];
            _engine.SelectTableCells(_engine.Selection.Focus.Block, cell.Row, -1);
        }
    }

    /// <summary>Selects every cell of the caret's column (see <see cref="SelectTableRow"/>).</summary>
    public void SelectTableColumn()
    {
        if (IsInTable)
        {
            var cell = _engine.Blocks[_engine.Selection.Focus.Block];
            _engine.SelectTableCells(_engine.Selection.Focus.Block, -1, cell.Col);
        }
    }

    /// <summary>True when the caret's row can move down (or up) past its neighbour; a merged cell spanning rows through either holds it.</summary>
    public bool CanMoveTableRow(bool down = true) => !IsReadOnly && _engine.CanMoveTableRow(down ? 1 : -1);

    /// <summary>True when the caret's column can move right (or left) past its neighbour.</summary>
    public bool CanMoveTableColumn(bool right = true) => !IsReadOnly && _engine.CanMoveTableColumn(right ? 1 : -1);

    /// <summary>
    /// Moves the caret's row one place down (or up), and the caret with it. Dragging a row's grip (left in the table) moves it further.
    /// The first row stays the header row whatever lands there.
    /// </summary>
    /// <returns>True when the row moved.</returns>
    public bool MoveTableRow(bool down = true)
    {
        if (!CanMoveTableRow(down))
        {
            return false;
        }
        var row = _engine.Blocks[_engine.Selection.Focus.Block].Row;
        return EditTable(_engine.MoveTableRow(row, down ? row + 2 : row - 1), "Row moved");
    }

    /// <summary>Moves the caret's column one place right (or left) with its width; dragging a column's grip (top in the table) moves it further.</summary>
    /// <returns>True when the column moved.</returns>
    public bool MoveTableColumn(bool right = true)
    {
        if (!CanMoveTableColumn(right))
        {
            return false;
        }
        var col = _engine.Blocks[_engine.Selection.Focus.Block].Col;
        return EditTable(_engine.MoveTableColumn(col, right ? col + 2 : col - 1), "Column moved");
    }

    /// <summary>Deletes the whole table the caret is in.</summary>
    public bool DeleteTable() => !IsReadOnly && EditTable(_engine.DeleteTable(), "Table deleted");

    private bool EditTable(bool changed, string message)
    {
        if (!changed)
        {
            return false;
        }
        Announce(message);
        FocusEditor();
        return true;
    }

    private Task<Bitmap?>? LoadImageFromHost(string source)
    {
        if (ImageLoading is not { } handler)
        {
            return null;
        }
        var args = new RichTextImageLoadingEventArgs(source);
        handler(this, args);
        if (args.Bitmap is { } bitmap)
        {
            return Task.FromResult<Bitmap?>(bitmap);
        }
        return args.StreamTask is { } task ? DecodeAsync(task) : null;

        static async Task<Bitmap?> DecodeAsync(Task<Stream?> task)
        {
            await using var stream = await task;
            return stream is null ? null : new Bitmap(stream);
        }
    }

    /// <summary>Asks the host where a picture should live (<see cref="ImageSourceRequested"/>) and inserts the image there.</summary>
    private async Task InsertPictureAsync(RichTextImageOrigin origin, string? fileName, string contentType, Func<Task<Stream>> open)
    {
        if (IsReadOnly || !_engine.Docs)
        {
            return;
        }
        var args = new RichTextImageSourceRequestedEventArgs(origin, fileName, contentType, open);
        ImageSourceRequested?.Invoke(this, args);
        if (args.Cancel)
        {
            return;
        }
        string? source = args.Source;
        try
        {
            if (source is null && args.SourceTask is { } pending)
            {
                source = await pending;
            }
            else if (!args.IsAnswered)
            {
                source = "data:" + contentType + ";base64," + Convert.ToBase64String(await args.ReadAllBytesAsync());
            }
        }
        catch (Exception)
        {
            // An upload or a read that failed leaves the document as it was.
            source = null;
        }
        if (!string.IsNullOrWhiteSpace(source))
        {
            InsertImage(source, args.Alt);
        }
    }

    private Task InsertImageFileAsync(RichTextImageOrigin origin, IStorageFile file) =>
        InsertPictureAsync(origin, file.Name, ImageContentType(file.Name), () => file.OpenReadAsync());

    private static string? ImageContentTypeOrNull(string? name) => Path.GetExtension(name)?.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".avif" => "image/avif",
        _ => null
    };

    private static string ImageContentType(string? name) => ImageContentTypeOrNull(name) ?? "image/png";

    /* ----------------------------------------------------------------------------------------------------------
     * Template and lifecycle
     * -------------------------------------------------------------------------------------------------------- */

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_surface is not null)
        {
            _surface.Owner = null;
            _surface.ContextRequested -= OnSurfaceContextRequested;
        }
        _surface = e.NameScope.Find<RichTextEditorSurface>(SurfacePart);
        _liveRegion = e.NameScope.Find<EditorsLiveRegion>(LiveRegionPart);
        if (_surface is not null)
        {
            _surface.Owner = this;
            _surface.ContextRequested += OnSurfaceContextRequested;
            _surface.ShowCodeHeader = _engine.Docs;
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
                LoadContent(RichTextEditorHtml.ToDocument(Html, _engine.Docs), record: IsInitialized);
            }
        }
        else if (change.Property == MarkdownProperty)
        {
            if (!_writingValue && !string.Equals(Markdown, _lastMarkdown, StringComparison.Ordinal))
            {
                LoadContent(RichTextEditorMarkdown.ToDocument(Markdown, _engine.Docs), record: IsInitialized);
            }
        }
        else if (change.Property == ProfileProperty)
        {
            _engine.Docs = Profile == RichTextEditorProfile.Docs;
            if (_surface is not null)
            {
                _surface.ShowCodeHeader = _engine.Docs;
            }
            // Content assigned before the profile (XAML order) was read with the other profile's rules.
            if (!_writingValue && Html is not null)
            {
                LoadContent(RichTextEditorHtml.ToDocument(Html, _engine.Docs), record: false);
            }
            else if (!_writingValue && Markdown is not null)
            {
                LoadContent(RichTextEditorMarkdown.ToDocument(Markdown, _engine.Docs), record: false);
            }
            ScheduleChromeSync();
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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_engine.Docs || IsReadOnly || e.Pointer.Type != PointerType.Mouse || _surface is not { } surface)
        {
            return;
        }
        EnsureOverlay();
        _blockHandle?.Track(e.GetPosition(surface));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _blockHandle?.PointerLeftEditor();
    }

    private void OnEngineChanged(bool content)
    {
        _surface?.OnDocumentChanged(content);
        if (content)
        {
            _blockHandle?.Refresh();
        }
        if (content)
        {
            IsDocumentEmpty = RichTextEditorHtml.IsDocumentEmpty(_engine.Blocks);
            CanUndo = _engine.CanUndo;
            CanRedo = _engine.CanRedo;
            ((RichTextEditorCommand)UndoCommand).Refresh();
            ((RichTextEditorCommand)RedoCommand).Refresh();
            ScheduleFlush();
            RaiseEvent(new RichTextContentChangedEventArgs(ContentChangedEvent, new RichTextValue(_engine.Blocks)));
            OnContentChangedForFind();
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
            var results = RichTextEditorSlashItem.Filter(_slash!.Value.Query, _engine.Docs);
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

        if (mod && key is Key.F or Key.H && !alt)
        {
            OpenFind(replace: key == Key.H && !shift);
            e.Handled = true;
            return;
        }
        if (key == Key.F3 && IsFindOpen)
        {
            StepMatch(shift ? -1 : 1);
            e.Handled = true;
            return;
        }
        if (key == Key.Escape && IsFindOpen)
        {
            CloseFind();
            e.Handled = true;
            return;
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
        if (mod && shift && _engine.Docs && !IsReadOnly && key is Key.Up or Key.Down)
        {
            if (_engine.MoveUnitBy(_engine.Selection.Focus.Block, key == Key.Up ? -1 : 1))
            {
                Announce("Block moved");
            }
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
            // Arc has no underline: the browser shortcut is swallowed (the engine ignores it outside the Docs profile).
            _engine.ToggleMark(RichTextMark.Underline);
            e.Handled = true;
            return;
        }
        if (mod && !shift && _engine.Docs && key is Key.OemPeriod or Key.OemComma)
        {
            _engine.ToggleMark(key == Key.OemPeriod ? RichTextMark.Superscript : RichTextMark.Subscript);
            e.Handled = true;
            return;
        }
        if (mod && shift && _engine.Docs && key is Key.L or Key.E or Key.R)
        {
            _engine.SetAlign(key == Key.L ? RichTextAlign.Start : key == Key.E ? RichTextAlign.Center : RichTextAlign.End);
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
                    if (block.IsCell && !mod && !alt)
                    {
                        _engine.TabInTable(shift);
                        e.Handled = true;
                    }
                    else if (block.IsList && !mod && !alt)
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
            case Key.Enter when mod && _engine.Blocks[_engine.Selection.Focus.Block].IsToggle:
                ToggleFold();
                e.Handled = true;
                return;
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
                        target = Step(blocks, _engine.HiddenMask, selection.Focus, -1, word);
                    }
                    break;
                case Key.Right:
                    if (!shift && !selection.IsCollapsed && !word)
                    {
                        target = selection.End;
                    }
                    else
                    {
                        target = Step(blocks, _engine.HiddenMask, selection.Focus, 1, word);
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

    private static RichTextEditorPosition Step(ImmutableList<RichTextEditorBlock> blocks, bool[]? hidden, RichTextEditorPosition origin, int direction, bool word)
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
                if (blocks[i].IsTextual && hidden?[i] != true)
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
            if (blocks[i].IsTextual && hidden?[i] != true)
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
            if (_engine.Docs && await data.TryGetFilesAsync() is { Length: > 0 } files && files.OfType<IStorageFile>().Where(f => ImageContentTypeOrNull(f.Name) is not null).ToList() is { Count: > 0 } pictures)
            {
                foreach (var picture in pictures)
                {
                    await InsertImageFileAsync(RichTextImageOrigin.Paste, picture);
                }
                return;
            }
            var text = await data.TryGetTextAsync() ?? string.Empty;
            if (await data.TryGetValueAsync(s_markdownFormat) is { } markdown)
            {
                PasteContent(text, RichTextEditorMarkdown.ToDocument(markdown, _engine.Docs), html: null);
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
            if (_engine.Docs && text.Length == 0 && string.IsNullOrEmpty(html) && await data.TryGetBitmapAsync() is { } bitmap)
            {
                using (bitmap)
                {
                    var png = new MemoryStream();
                    bitmap.Save(png, PngBitmapEncoderOptions.Default);
                    var bytes = png.ToArray();
                    await InsertPictureAsync(RichTextImageOrigin.Paste, null, "image/png", () => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false)));
                }
                return;
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
        if (blocks is null && _engine.Docs && html is null && IsImageAddress(text) && _engine.Selection.IsCollapsed)
        {
            // A bare picture address pasted on its own becomes an image.
            InsertImage(text.Trim());
            return;
        }
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
                blocks = RichTextEditorHtml.ToBlocks(html, _engine.Docs);
            }
            else if (RichTextEditorMarkdown.LooksLikeMarkdown(text) || text.Contains("\n\n", StringComparison.Ordinal))
            {
                blocks = RichTextEditorMarkdown.ToDocument(text, _engine.Docs);
            }
        }
        if (blocks is { Count: 0 })
        {
            blocks = null;
        }
        _engine.Paste(text, blocks);
    }

    private static bool IsImageAddress(string text)
    {
        var value = text.Trim();
        if (value.Length == 0 || value.AsSpan().IndexOfAny(" \t\r\n") >= 0)
        {
            return false;
        }
        if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return RichTextEditorHtml.IsSafeImageUrl(value);
        }
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var path = value.Split('?', '#')[0];
        return ImageContentTypeOrNull(path) is not null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || !_engine.Docs || e.DataTransfer.TryGetFiles() is not { Length: > 0 } files || !files.OfType<IStorageFile>().Any(f => ImageContentTypeOrNull(f.Name) is not null))
        {
            return;
        }
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (IsReadOnly || !_engine.Docs || _surface is null || e.DataTransfer.TryGetFiles() is not { Length: > 0 } files)
        {
            return;
        }
        var pictures = files.OfType<IStorageFile>().Where(f => ImageContentTypeOrNull(f.Name) is not null).ToList();
        if (pictures.Count == 0)
        {
            return;
        }
        e.Handled = true;
        // The pictures go where they were dropped.
        _engine.SetSelection(RichTextEditorSelection.Caret(_surface.HitTest(e.GetPosition(_surface))));
        _ = InsertDroppedAsync(pictures);
    }

    private async Task InsertDroppedAsync(List<IStorageFile> pictures)
    {
        foreach (var picture in pictures)
        {
            await InsertImageFileAsync(RichTextImageOrigin.Drop, picture);
        }
    }

    private MenuSurface? _tableMenu;
    private Border? _tableAnchor;

    /// <summary>The context menu of a table cell: rows, columns and the table itself.</summary>
    private void OnSurfaceContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (IsReadOnly || !_engine.Docs || _surface is null || !e.TryGetPosition(_surface, out var point))
        {
            return;
        }
        var position = _surface.HitTest(point);
        if (!_engine.Blocks[position.Block].IsCell)
        {
            return;
        }
        e.Handled = true;
        EnsureOverlay();
        CloseSlash();
        if (!(_engine.Selection.Start.Block <= position.Block && _engine.Selection.End.Block >= position.Block))
        {
            _engine.SetSelection(RichTextEditorSelection.Caret(position));
        }
        if (_tableMenu is null)
        {
            _tableAnchor = new Border { Background = Brushes.Transparent, IsHitTestVisible = false, Width = 1, Height = 1 };
            _overlay!.Children.Add(_tableAnchor);
            _tableMenu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 4 };
            AutomationProperties.SetName(_tableMenu, "Table");
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert row above", Tag = "row-above" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert row below", Tag = "row-below" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert column left", Tag = "column-left" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Insert column right", Tag = "column-right" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move row up", Tag = "row-up", HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move row down", Tag = "row-down" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move column left", Tag = "column-move-left" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Move column right", Tag = "column-move-right" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Merge cells", Tag = "cells-merge", HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Split cell", Tag = "cell-split" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Header row", Tag = "header-row", ToggleType = MenuEntryToggleType.CheckBox, HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Header column", Tag = "header-column", ToggleType = MenuEntryToggleType.CheckBox });
            _tableMenu.Items.Add(new MenuEntry { Header = "Distribute columns", Tag = "columns-even" });
            foreach (var color in Enum.GetValues<RichTextColor>())
            {
                _tableMenu.Items.Add(new MenuEntry
                {
                    Header = "Color: " + (color == RichTextColor.Default ? "None" : color.ToString()), Tag = color, ToggleType = MenuEntryToggleType.Radio,
                    GroupName = "cell-color", HasSeparatorBefore = color == RichTextColor.Default
                });
            }
            _tableMenu.Items.Add(new MenuEntry { Header = "Delete row", Tag = "row-delete", HasSeparatorBefore = true });
            _tableMenu.Items.Add(new MenuEntry { Header = "Delete column", Tag = "column-delete" });
            _tableMenu.Items.Add(new MenuEntry { Header = "Delete table", Tag = "table-delete" });
            _tableMenu.ItemClicked += (_, args) =>
            {
                if (args.Entry.Tag is RichTextColor color)
                {
                    SetTableCellColor(color);
                    FocusEditor();
                    return;
                }
                switch (args.Entry.Tag as string)
                {
                    case "row-above":
                        AddTableRow(below: false);
                        break;
                    case "row-below":
                        AddTableRow();
                        break;
                    case "column-left":
                        AddTableColumn(after: false);
                        break;
                    case "column-right":
                        AddTableColumn();
                        break;
                    case "row-up":
                        MoveTableRow(down: false);
                        break;
                    case "row-down":
                        MoveTableRow();
                        break;
                    case "column-move-left":
                        MoveTableColumn(right: false);
                        break;
                    case "column-move-right":
                        MoveTableColumn();
                        break;
                    case "cells-merge":
                        MergeTableCells();
                        break;
                    case "cell-split":
                        SplitTableCell();
                        break;
                    case "header-row":
                        SetTableHeaderRow(!TableHasHeaderRow);
                        break;
                    case "header-column":
                        SetTableHeaderColumn(!TableHasHeaderColumn);
                        break;
                    case "columns-even":
                        DistributeTableColumns();
                        break;
                    case "row-delete":
                        DeleteTableRow();
                        break;
                    case "column-delete":
                        DeleteTableColumn();
                        break;
                    case "table-delete":
                        DeleteTable();
                        break;
                }
                FocusEditor();
            };
        }
        var cellColor = TableCellColor;
        foreach (var entry in _tableMenu.Items.OfType<MenuEntry>())
        {
            entry.IsChecked = entry.Tag switch
            {
                "header-row" => TableHasHeaderRow,
                "header-column" => TableHasHeaderColumn,
                RichTextColor color => color == cellColor,
                _ => false
            };
            entry.IsEnabled = (entry.Tag as string) switch
            {
                "cells-merge" => CanMergeTableCells,
                "row-up" => CanMoveTableRow(down: false),
                "row-down" => CanMoveTableRow(),
                "column-move-left" => CanMoveTableColumn(right: false),
                "column-move-right" => CanMoveTableColumn(),
                "cell-split" => CanSplitTableCell,
                _ => true
            };
        }
        var origin = _surface.TranslatePoint(point, this) ?? default;
        _tableAnchor!.RenderTransform = new TranslateTransform(origin.X, origin.Y);
        _tableMenu.Open(_tableAnchor);
    }

    /* ----------------------------------------------------------------------------------------------------------
     * Surface callbacks
     * -------------------------------------------------------------------------------------------------------- */

    /// <summary>A press on a to-do checkbox: flips the item (the caret stays where it was).</summary>
    /// <summary>A press on a toggle item's arrow: folds or unfolds it.</summary>
    internal void OnTogglePressed(int block)
    {
        if (IsReadOnly)
        {
            return;
        }
        var collapse = !_engine.Blocks[block].IsCollapsed;
        if (_engine.SetCollapsed(block, collapse))
        {
            Announce(collapse ? "Collapsed" : "Expanded");
        }
    }

    /// <summary>
    /// Folds the toggle item holding the caret, or unfolds it when it is folded (Ctrl+Enter, or a click on its arrow). A toggle item is a
    /// list item (the Docs profile's "Toggle list") whose nested items fold away; the folded state is stored, as <c>data-collapsed</c> in Html.
    /// </summary>
    /// <returns>True when the toggle changed.</returns>
    public bool ToggleFold()
    {
        var index = _engine.Selection.Focus.Block;
        if (IsReadOnly || !_engine.Blocks[index].IsToggle)
        {
            return false;
        }
        var collapse = !_engine.Blocks[index].IsCollapsed;
        if (!_engine.SetCollapsed(index, collapse))
        {
            return false;
        }
        Announce(collapse ? "Collapsed" : "Expanded");
        return true;
    }

    internal void OnTodoPressed(int block)
    {
        if (IsReadOnly)
        {
            return;
        }
        var item = _engine.Blocks[block];
        if (_engine.SetChecked(block, !item.IsChecked))
        {
            Announce(item.IsChecked ? "Unchecked" : "Checked");
        }
    }

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
        if (item.Type == RichTextBlockType.Image)
        {
            _ = PickImageAsync();
        }
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
        _toolbar.SetDocs(_engine.Docs);
        _toolbar.Underline.Click += (_, _) => _engine.ToggleMark(RichTextMark.Underline);
        _toolbar.Superscript.Click += (_, _) => _engine.ToggleMark(RichTextMark.Superscript);
        _toolbar.Subscript.Click += (_, _) => _engine.ToggleMark(RichTextMark.Subscript);
        _toolbar.Todo.Click += (_, _) => _engine.SetBlockType(RichTextBlockType.TodoList);
        _toolbar.Palette.Click += (_, _) => OpenColors();
        _toolbar.ColorBack.Click += (_, _) => _toolbar?.SetColorFace(false);
        _toolbar.TextSize.Click += (_, _) =>
        {
            if (!IsReadOnly)
            {
                _toolbar?.SetSizeFace(true, _engine.GetFormats().Size);
            }
        };
        _toolbar.SizeBack.Click += (_, _) => _toolbar?.SetSizeFace(false);
        _toolbar.SizeChosen += size =>
        {
            SetTextSize(size);
            _toolbar?.SetSizeFace(false);
        };
        _toolbar.SwatchChosen += (isHighlight, color) =>
        {
            if (isHighlight)
            {
                SetHighlight(color);
            }
            else
            {
                SetTextColor(color);
            }
            _toolbar?.SetColorFace(false);
        };
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
        _blockHandle = new RichTextEditorBlockHandle(this, _overlay);
        AdornerLayer.SetAdorner(this, _overlay);
    }

    /// <summary>The languages offered by the code block's language list, each a label and the id that is stored and highlighted. A "Custom…" row always lets the person type any other name.</summary>
    public static readonly StyledProperty<IReadOnlyList<RichTextCodeLanguage>?> CodeLanguagesProperty =
        AvaloniaProperty.Register<RichTextEditor, IReadOnlyList<RichTextCodeLanguage>?>(nameof(CodeLanguages));

    /// <inheritdoc cref="CodeLanguagesProperty"/>
    public IReadOnlyList<RichTextCodeLanguage>? CodeLanguages { get => GetValue(CodeLanguagesProperty); set => SetValue(CodeLanguagesProperty, value); }

    internal MenuSurface? LanguageMenu => _languageMenu;

    internal Avalonia.Controls.Primitives.Popup? CustomLanguagePopup => _customLanguagePopup;

    internal TextBox? CustomLanguageBox => _customLanguageBox;

    private const string CustomLanguageTag = "\u0001custom";

    private MenuSurface? _languageMenu;
    private Border? _languageAnchor;
    private Avalonia.Controls.Primitives.Popup? _customLanguagePopup;
    private TextBox? _customLanguageBox;

    /// <summary>A press on a code block's language label: opens the language list under it.</summary>
    internal void OnCodeLanguagePressed(int block, Rect labelRect)
    {
        if (IsReadOnly || !_engine.Docs || _surface is not { } surface)
        {
            return;
        }
        EnsureOverlay();
        CloseSlash();
        _engine.SetSelection(RichTextEditorSelection.Caret(block, Math.Min(_engine.Selection.Focus.Block == block ? _engine.Selection.Focus.Offset : 0, _engine.Blocks[block].Length)));
        var current = _engine.Blocks[block].Language;
        if (_languageMenu is null)
        {
            _languageAnchor = new Border { Background = Brushes.Transparent, IsHitTestVisible = false };
            _overlay!.Children.Add(_languageAnchor);
            _languageMenu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 4 };
            AutomationProperties.SetName(_languageMenu, "Code language");
            _languageMenu.ItemClicked += (_, args) =>
            {
                if (args.Entry.Tag is CustomLanguageMarker)
                {
                    Dispatcher.UIThread.Post(OpenCustomLanguage, DispatcherPriority.Loaded);
                    return;
                }
                _engine.SetCodeLanguage(args.Entry.Tag as string);
                FocusEditor();
            };
        }
        // Rebuilt on every open: the list can change, and a language set from code (or typed) shows as the checked row.
        _languageMenu.Items.Clear();
        var languages = CodeLanguages ?? RichTextCodeLanguage.Defaults;
        var known = languages.Any(l => string.Equals(l.Id, current, StringComparison.Ordinal));
        if (current is not null && !known)
        {
            _languageMenu.Items.Add(new MenuEntry { Header = current, Tag = current, ToggleType = MenuEntryToggleType.Radio, GroupName = "language", IsChecked = true });
        }
        foreach (var language in languages)
        {
            _languageMenu.Items.Add(new MenuEntry
            {
                Header = language.Label, Tag = language.Id, ToggleType = MenuEntryToggleType.Radio, GroupName = "language",
                IsChecked = string.Equals(language.Id, current, StringComparison.Ordinal),
                HasSeparatorBefore = language.Id is not null && ReferenceEquals(language, languages.FirstOrDefault(l => l.Id is not null))
            });
        }
        _languageMenu.Items.Add(new MenuEntry { Header = "Custom…", Tag = CustomLanguageMarker.Instance, HasSeparatorBefore = true });
        var origin = surface.TranslatePoint(labelRect.TopLeft, this) ?? default;
        _languageAnchor!.Width = labelRect.Width;
        _languageAnchor.Height = labelRect.Height;
        _languageAnchor.RenderTransform = new TranslateTransform(origin.X, origin.Y);
        _languageMenu.Open(_languageAnchor);
    }

    private sealed class CustomLanguageMarker
    {
        public static readonly CustomLanguageMarker Instance = new();
    }

    /// <summary>Opens a small field under the language label for a name the list does not have.</summary>
    internal void OpenCustomLanguage()
    {
        if (_languageAnchor is null)
        {
            return;
        }
        if (_customLanguagePopup is null)
        {
            _customLanguageBox = SelectInputBox.Create();
            _customLanguageBox.Width = 200;
            _customLanguageBox.Height = 32;
            _customLanguageBox.Padding = new Thickness(8, 0);
            _customLanguageBox.PlaceholderText = "Language, such as csharp";
            AutomationProperties.SetName(_customLanguageBox, "Custom language");
            _customLanguageBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ApplyCustomLanguage();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _customLanguagePopup!.IsOpen = false;
                    FocusEditor();
                    e.Handled = true;
                }
            };
            var frame = new Border { Padding = new Thickness(6), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Child = _customLanguageBox };
            ArcTokens.Bind(frame, Border.BackgroundProperty, DesignTokens.ArcDesignTokenKind.SurfaceRaised);
            ArcTokens.Bind(frame, Border.BorderBrushProperty, DesignTokens.ArcDesignTokenKind.Border);
            _customLanguagePopup = new Avalonia.Controls.Primitives.Popup
            {
                Child = frame, IsLightDismissEnabled = true, Placement = PlacementMode.BottomEdgeAlignedLeft, PlacementTarget = _languageAnchor
            };
            _overlay!.Children.Add(_customLanguagePopup);
        }
        _customLanguageBox!.Text = _engine.Blocks[_engine.Selection.Focus.Block].Language ?? string.Empty;
        _customLanguagePopup.IsOpen = true;
        Dispatcher.UIThread.Post(() =>
        {
            _customLanguageBox.Focus();
            _customLanguageBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private void ApplyCustomLanguage()
    {
        var name = _customLanguageBox?.Text?.Trim();
        if (_customLanguagePopup is not null)
        {
            _customLanguagePopup.IsOpen = false;
        }
        _engine.SetCodeLanguage(string.IsNullOrEmpty(name) ? null : name);
        FocusEditor();
    }

    private void OpenColors()
    {
        if (IsReadOnly || _surface is not { } surface || _toolbar is not { } toolbar)
        {
            return;
        }
        toolbar.SetPalette(surface.TextBrushOf, surface.FillBrushOf);
        toolbar.SetColorFace(true);
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
        var wantsBar = !IsReadOnly && !selection.IsCollapsed && !surface.IsDragging && !IsSlashOpen && (focused || linkFace) && !IsFindFocused
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
        PlaceFindBar();

        if (IsSlashOpen && focused && _slash is { } slash)
        {
            var results = RichTextEditorSlashItem.Filter(slash.Query, _engine.Docs);
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
        toolbar.SetDocs(_engine.Docs);
        toolbar.Underline.IsChecked = formats.IsUnderline;
        toolbar.Superscript.IsChecked = formats.IsSuperscript;
        toolbar.Subscript.IsChecked = formats.IsSubscript;
        toolbar.Todo.IsChecked = formats.BlockType == RichTextBlockType.TodoList;
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
