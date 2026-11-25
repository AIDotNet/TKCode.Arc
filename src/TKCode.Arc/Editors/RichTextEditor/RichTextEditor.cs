using TKCode.Arc.Primitives;
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
        AvaloniaProperty.Register<RichTextEditor, bool>(nameof(IsReadOnly),
            coerce: (o, v) => v || o.GetValue(Form.IsReadOnlyProperty));

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
        if (change.Property == Form.IsReadOnlyProperty)
        {
            // Form.IsReadOnly (inherited) makes the editor read-only with its form or section.
            CoerceValue(IsReadOnlyProperty);
        }
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
            _surface?.ShowCodeHeader = _engine.Docs;
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
}
