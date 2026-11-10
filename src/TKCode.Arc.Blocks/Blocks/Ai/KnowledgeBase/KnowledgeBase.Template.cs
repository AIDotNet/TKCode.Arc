using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class KnowledgeBase
{
    private Border? _card;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private TextBlock? _totals;
    private TextBlock? _indexingText;
    private Button? _add;
    private TextBlock? _addText;
    private Border? _dropZone;
    private TextBlock? _dropText;
    private ArcContentSwap? _dropNote;
    private TextBlock? _listHeading;
    private TextBlock? _listCount;
    private ItemsControl? _list;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyText;
    private TextBlock? _searchHeading;
    private TextBlock? _searchHint;
    private TextBox? _search;
    private ArcContentSwap? _searchStatus;
    private StackPanel? _passagesPanel;
    private AiAnnouncer? _announcer;

    private readonly List<KnowledgeBasePassageView> _passageViews = new();
    private readonly HashSet<KnowledgeDocument> _watched = new();
    private INotifyCollectionChanged? _observedSource;
    private bool _attached;
    private bool _wide;
    private bool _compact;
    private bool _dragging;
    private bool _samplesChecked;
    private string? _dropNoteText;
    private KnowledgeDocument? _located;

    /// <summary>The list the card works on: <see cref="DocumentsSource"/> when set, otherwise <see cref="Documents"/>.</summary>
    internal IList<KnowledgeDocument> Items => DocumentsSource ?? Documents;

    /// <summary>Raised when something every row reads changed (compact layout, read-only, the located document, labels).</summary>
    internal event EventHandler? RowsInvalidated;

    internal bool IsCompactLayout => _compact;

    internal bool IsWideLayout => _wide;

    internal KnowledgeDocument? LocatedDocument => _located;

    /// <summary>The document whose row asks "Delete …?" (at most one at a time).</summary>
    internal KnowledgeDocument? ConfirmingDocument => _confirming;

    private KnowledgeDocument? _confirming;

    /// <summary>
    /// Opens the delete question for <paramref name="document"/>, or closes it (null) when it belongs to
    /// <paramref name="from"/>; every row redraws, so a question opened on one row closes on another.
    /// </summary>
    internal void SetConfirmingDocument(KnowledgeDocument? document, KnowledgeDocument from)
    {
        if (document is null && !ReferenceEquals(_confirming, from))
        {
            return;
        }
        if (ReferenceEquals(_confirming, document))
        {
            return;
        }
        _confirming = document;
        RowsInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Focuses Cancel or Delete in the row of <paramref name="document"/>, once the list has laid the row out.</summary>
    internal void FocusAfterConfirm(KnowledgeDocument document, bool open)
    {
        // The question changes the row's height, and the list may then hand the document to another row: focus now, and
        // again once the list has laid out, in whichever row shows the document by then.
        RowOf(document)?.FocusConfirmTarget(open);
        Dispatcher.UIThread.Post(() =>
        {
            var index = Items.IndexOf(document);
            if (index < 0)
            {
                return;
            }
            _list?.ScrollIntoView(index);
            RowOf(document)?.FocusConfirmTarget(open);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>The culture rows format sizes and dates with.</summary>
    internal System.Globalization.CultureInfo FormatCulture => Culture;

    internal string? LastAnnouncement => _announcer?.Announcement ?? _pendingAnnouncement;

    internal Button? AddButton => _add;

    internal TextBox? SearchBox => _search;

    internal ItemsControl? List => _list;

    internal TextBlock? TotalsText => _totals;

    internal string? DropNoteText => _dropNoteText;

    internal string? SearchStatusText => _searchStatus?.Content as string;

    internal IReadOnlyList<KnowledgeBasePassageView> PassageViews => _passageViews;

    private string? _pendingAnnouncement;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        T? Find<T>(string name) where T : class => e.NameScope.Find<T>(name);
        _card = Find<Border>(CardPart);
        _title = Find<TextBlock>(TitlePart);
        _subtitle = Find<TextBlock>(SubtitlePart);
        _totals = Find<TextBlock>(TotalsPart);
        _indexingText = Find<TextBlock>(IndexingTextPart);
        _add = Find<Button>(AddPart);
        _addText = Find<TextBlock>(AddTextPart);
        _dropZone = Find<Border>(DropZonePart);
        _dropText = Find<TextBlock>(DropTextPart);
        _dropNote = Find<ArcContentSwap>(DropNotePart);
        _listHeading = Find<TextBlock>(ListHeadingPart);
        _listCount = Find<TextBlock>(ListCountPart);
        _list = Find<ItemsControl>(ListPart);
        _emptyTitle = Find<TextBlock>(EmptyTitlePart);
        _emptyText = Find<TextBlock>(EmptyTextPart);
        _searchHeading = Find<TextBlock>(SearchHeadingPart);
        _searchHint = Find<TextBlock>(SearchHintPart);
        _search = Find<TextBox>(SearchPart);
        _searchStatus = Find<ArcContentSwap>(SearchStatusPart);
        _passagesPanel = Find<StackPanel>(PassagesPart);
        _announcer = Find<AiAnnouncer>(AnnouncerPart);
        _passageViews.Clear();

        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_card is not null)
        {
            DragDrop.SetAllowDrop(_card, true);
            _card.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _card.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            _card.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _card.AddHandler(DragDrop.DropEvent, OnDrop);
        }
        if (_list is not null)
        {
            _list.ItemTemplate = new FuncDataTemplate<KnowledgeDocument>((_, _) => new KnowledgeBaseDocumentRow(this), supportsRecycling: true);
            _list.ItemsSource = Items;
        }
        if (_search is not null)
        {
            _search.Text = SearchText;
            _search.TextChanged += OnSearchTextChanged;
            _search.KeyDown += OnSearchKeyDown;
        }
        if (_announcer is not null && _pendingAnnouncement is not null)
        {
            _announcer.Announce(_pendingAnnouncement);
        }
        ApplyLabels();
        UpdateTotals();
        RenderSearchStatus();
        RenderPassages();
        UpdatePseudoClasses();
    }

    private void Unhook()
    {
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        if (_card is not null)
        {
            _card.RemoveHandler(DragDrop.DragEnterEvent, OnDragEnter);
            _card.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _card.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _card.RemoveHandler(DragDrop.DropEvent, OnDrop);
        }
        if (_list is not null)
        {
            _list.ItemsSource = null;
        }
        if (_search is not null)
        {
            _search.TextChanged -= OnSearchTextChanged;
            _search.KeyDown -= OnSearchKeyDown;
        }
        foreach (var view in _passageViews)
        {
            view.Click -= OnPassageClick;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        EnsureSamples();
        ObserveSource();
        Rewatch();
        EnqueueWaiting();
        Pump();
        if (!string.IsNullOrWhiteSpace(SearchText) && _passages.Count == 0)
        {
            StartSearch(immediate: true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        StopAllRuns();
        CancelSearch();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var wide = width >= WideWidth;
        var compact = width < CompactWidth;
        if (wide == _wide && compact == _compact)
        {
            return;
        }
        var compactChanged = compact != _compact;
        _wide = wide;
        _compact = compact;
        UpdatePseudoClasses();
        if (compactChanged)
        {
            RowsInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(WidePseudoClass, _wide);
        PseudoClasses.Set(CompactPseudoClass, _compact);
        PseudoClasses.Set(DraggingPseudoClass, _dragging);
        PseudoClasses.Set(EmptyPseudoClass, Items.Count == 0);
        PseudoClasses.Set(IndexingPseudoClass, PendingCount > 0);
        PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
        PseudoClasses.Set(SearchActivePseudoClass, !string.IsNullOrWhiteSpace(SearchText));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == DocumentsSourceProperty)
        {
            OnItemsReplaced();
        }
        else if (property == SearchTextProperty)
        {
            var text = change.GetNewValue<string?>();
            if (_search is not null && _search.Text != text)
            {
                _search.Text = text;
            }
            UpdatePseudoClasses();
            StartSearch(immediate: false);
        }
        else if (property == LabelsProperty || property == AssistantNameProperty || property == TitleProperty ||
                 property == CultureNameProperty || property == MaxFileSizeProperty)
        {
            ApplyLabels();
            UpdateTotals();
            RenderSearchStatus();
            RenderPassages();
            RowsInvalidated?.Invoke(this, EventArgs.Empty);
        }
        else if (property == IsReadOnlyProperty)
        {
            UpdatePseudoClasses();
            RowsInvalidated?.Invoke(this, EventArgs.Empty);
        }
        else if (property == MaxPassagesProperty)
        {
            StartSearch(immediate: true);
        }
        else if (property == MaxConcurrentIndexingProperty)
        {
            Pump();
        }
        else if (property == WideWidthProperty || property == CompactWidthProperty)
        {
            UpdateLayoutMode(Bounds.Width);
        }
    }

    #region Words

    private void ApplyLabels()
    {
        var labels = Labels;
        SetText(_title, Title ?? labels.Title);
        SetText(_subtitle, labels.Subtitle(AssistantName));
        SetText(_addText, labels.AddFiles);
        if (_add is not null)
        {
            AutomationProperties.SetName(_add, labels.AddFiles);
        }
        SetText(_dropText, _dragging ? labels.DropActive : labels.DropHint);
        ShowDropNote(_dropNoteText);
        SetText(_listHeading, labels.DocumentsHeading);
        SetText(_emptyTitle, labels.EmptyTitle);
        SetText(_emptyText, labels.EmptyText);
        SetText(_searchHeading, labels.SearchHeading);
        SetText(_searchHint, labels.SearchHint);
        if (_search is FieldTextBox field)
        {
            field.Placeholder = labels.SearchPlaceholder;
        }
        if (_search is not null)
        {
            AutomationProperties.SetName(_search, labels.SearchName);
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, labels.DocumentsHeading);
        }
        if (_passagesPanel is not null)
        {
            AutomationProperties.SetName(_passagesPanel, labels.SearchHeading);
        }
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null && block.Text != text)
        {
            block.Text = text;
        }
    }

    /// <summary>Shows a note in the drop zone in place of the size limit; null brings the limit back.</summary>
    private void ShowDropNote(string? note)
    {
        _dropNoteText = note;
        if (_dropNote is not null)
        {
            var text = note ?? Labels.DropNote(KnowledgeBaseText.FormatSize(MaxFileSize, Culture));
            if (!Equals(_dropNote.Content, text))
            {
                _dropNote.Content = text;
            }
        }
    }

    internal void Announce(string text)
    {
        _pendingAnnouncement = text;
        _announcer?.Announce(text);
    }

    #endregion

    #region Documents

    private void EnsureSamples()
    {
        if (_samplesChecked)
        {
            return;
        }
        _samplesChecked = true;
        if (IsSampleData && DocumentsSource is null && Documents.Count == 0)
        {
            Documents.AddRange(KnowledgeBaseSamples.Documents());
        }
    }

    private void OnItemsReplaced()
    {
        ObserveSource();
        if (_list is not null)
        {
            _list.ItemsSource = Items;
        }
        Rewatch();
        EnqueueWaiting();
        Pump();
        AfterDocumentsChanged();
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
            _observedSource = null;
        }
        if (DocumentsSource is INotifyCollectionChanged source)
        {
            _observedSource = source;
            source.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnInlineDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DocumentsSource is null)
        {
            OnItemsChanged();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnItemsChanged();

    private void OnItemsChanged()
    {
        Rewatch();
        if (!_batching)
        {
            EnqueueWaiting();
            Pump();
        }
        AfterDocumentsChanged();
    }

    /// <summary>Follows every document in the list (for the totals) and lets go of the ones that left, stopping their runs.</summary>
    private void Rewatch()
    {
        var current = new HashSet<KnowledgeDocument>(Items);
        foreach (var gone in _watched.Where(d => !current.Contains(d)).ToArray())
        {
            gone.PropertyChanged -= OnDocumentPropertyChanged;
            _watched.Remove(gone);
            Forget(gone);
            if (ReferenceEquals(gone, _located))
            {
                _located = null;
            }
            if (ReferenceEquals(gone, _confirming))
            {
                _confirming = null;
            }
        }
        foreach (var document in current)
        {
            if (_watched.Add(document))
            {
                document.PropertyChanged += OnDocumentPropertyChanged;
            }
        }
    }

    private void OnDocumentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KnowledgeDocument.StatusProperty || e.Property == KnowledgeDocument.ChunkCountProperty ||
            e.Property == KnowledgeDocument.SizeBytesProperty)
        {
            UpdateTotals();
        }
        if (e.Property == KnowledgeDocument.ChunksProperty && sender is KnowledgeDocument { Status: KnowledgeDocumentStatus.Ready })
        {
            StartSearch(immediate: true);
        }
    }

    private void AfterDocumentsChanged()
    {
        UpdateTotals();
        UpdatePseudoClasses();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            StartSearch(immediate: true);
        }
    }

    private void UpdateTotals()
    {
        var chunks = 0;
        long bytes = 0;
        var pending = 0;
        foreach (var document in Items)
        {
            bytes += Math.Max(0, document.SizeBytes);
            if (document.Status == KnowledgeDocumentStatus.Ready)
            {
                chunks += document.ChunkCount;
            }
            else if (document.Status != KnowledgeDocumentStatus.Failed)
            {
                pending++;
            }
        }
        TotalChunks = chunks;
        TotalBytes = bytes;
        PendingCount = pending;
        var labels = Labels;
        var culture = Culture;
        SetText(_totals, labels.Totals(Items.Count, chunks, KnowledgeBaseText.FormatSize(bytes, culture)));
        SetText(_indexingText, pending > 0 ? labels.IndexingCount(pending) : null);
        SetText(_listCount, Items.Count.ToString("N0", culture));
        if (_totals is not null && _indexingText is not null)
        {
            AutomationProperties.SetName(_totals, pending > 0 ? $"{_totals.Text}, {_indexingText.Text}" : _totals.Text);
        }
        UpdatePseudoClasses();
    }

    #endregion

    #region Adding files

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = BrowseAsync();
    }

    private bool AcceptsDrag(DragEventArgs e) => !IsReadOnly && IsEffectivelyEnabled && e.DataTransfer.Contains(DataFormat.File);

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var accepts = AcceptsDrag(e);
        e.DragEffects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        SetDragging(accepts);
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = AcceptsDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Moving between the card's own parts raises leave and enter; only leaving its bounds ends the drag.
        if (_card is not null && new Rect(_card.Bounds.Size).Contains(e.GetPosition(_card)))
        {
            return;
        }
        SetDragging(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetDragging(false);
        e.Handled = true;
        if (!AcceptsDrag(e))
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

    private void SetDragging(bool value)
    {
        if (_dragging == value)
        {
            return;
        }
        _dragging = value;
        SetText(_dropText, value ? Labels.DropActive : Labels.DropHint);
        UpdatePseudoClasses();
    }

    #endregion

    #region Rows and keyboard

    /// <summary>The row that shows <paramref name="document"/>, when it is realized.</summary>
    internal KnowledgeBaseDocumentRow? RowOf(KnowledgeDocument document)
    {
        if (_list is null)
        {
            return null;
        }
        var index = Items.IndexOf(document);
        return index < 0 ? null : RowAt(index);
    }

    private KnowledgeBaseDocumentRow? RowAt(int index)
    {
        if (_list?.ContainerFromIndex(index) is not { } container)
        {
            return null;
        }
        return container as KnowledgeBaseDocumentRow ?? container.GetVisualDescendants().OfType<KnowledgeBaseDocumentRow>().FirstOrDefault();
    }

    /// <summary>Scrolls the row at <paramref name="index"/> into view and gives the same kind of button in it the focus.</summary>
    internal void FocusRow(int index, KnowledgeBaseRowAction action, NavigationMethod method)
    {
        if (_list is null || index < 0 || index >= Items.Count)
        {
            return;
        }
        _list.ScrollIntoView(index);
        if (RowAt(index) is { } row && row.FocusAction(action, method))
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (index < Items.Count && RowAt(index) is { } later)
            {
                later.FocusAction(action, method);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Up and Down move from a row's button to the same button in the previous or next row, Home and End to the first and
    /// last row (called by the row for a key pressed inside it). Returns whether the key was used.
    /// </summary>
    internal bool MoveFromRow(KnowledgeBaseDocumentRow row, Visual source, Key key)
    {
        if (row.Document is not { } document || row.IsConfirming)
        {
            return false;
        }
        var index = Items.IndexOf(document);
        var target = key switch
        {
            Key.Down => index + 1,
            Key.Up => index - 1,
            Key.Home => 0,
            Key.End => Items.Count - 1,
            _ => -1
        };
        if (key is not (Key.Up or Key.Down or Key.Home or Key.End) || index < 0)
        {
            return false;
        }
        if (target >= 0 && target < Items.Count && target != index)
        {
            FocusRow(target, row.FocusedAction(source), NavigationMethod.Directional);
        }
        // At the ends the key is still used, so focus does not jump out of the list.
        return true;
    }

    #endregion

    #region Passages

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(_search?.Text))
        {
            e.Handled = true;
            SetCurrentValue(SearchTextProperty, null);
        }
        else if (e.Key == Key.Down && _passageViews.FirstOrDefault(v => v.IsVisible) is { } first)
        {
            e.Handled = true;
            first.Focus(NavigationMethod.Directional);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            StartSearch(immediate: true);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_search is not null && _search.Text != SearchText)
        {
            SetCurrentValue(SearchTextProperty, _search.Text);
        }
    }

    private void RenderPassages()
    {
        if (_passagesPanel is null)
        {
            return;
        }
        var passages = _passages;
        var terms = KnowledgeBaseText.Terms(SearchText);
        while (_passageViews.Count < passages.Count)
        {
            var view = new KnowledgeBasePassageView();
            view.Click += OnPassageClick;
            view.AddHandler(KeyDownEvent, OnPassageKeyDown, RoutingStrategies.Bubble);
            _passageViews.Add(view);
            _passagesPanel.Children.Add(view);
        }
        var culture = Culture;
        for (var i = 0; i < _passageViews.Count; i++)
        {
            var view = _passageViews[i];
            if (i < passages.Count)
            {
                view.Show(passages[i], i + 1, terms, Labels, culture, PreviewLength);
                view.IsVisible = true;
            }
            else
            {
                view.IsVisible = false;
            }
        }
    }

    private void OnPassageKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not KnowledgeBasePassageView view || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var visible = _passageViews.Where(v => v.IsVisible).ToList();
        var index = visible.IndexOf(view);
        if (e.Key == Key.Down && index + 1 < visible.Count)
        {
            e.Handled = true;
            visible[index + 1].Focus(NavigationMethod.Directional);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (index > 0)
            {
                visible[index - 1].Focus(NavigationMethod.Directional);
            }
            else
            {
                _search?.Focus(NavigationMethod.Directional);
            }
        }
    }

    private void OnPassageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is KnowledgeBasePassageView { Passage: { } passage })
        {
            e.Handled = true;
            InvokePassage(passage);
        }
    }

    /// <summary>Scrolls the list to the passage's document, marks its row and raises <see cref="PassageInvoked"/>.</summary>
    internal void InvokePassage(KnowledgePassage passage)
    {
        var index = Items.IndexOf(passage.Document);
        _located = index >= 0 ? passage.Document : null;
        if (index >= 0)
        {
            _list?.ScrollIntoView(index);
        }
        RowsInvalidated?.Invoke(this, EventArgs.Empty);
        RaiseEvent(new KnowledgeBasePassageEventArgs(PassageInvokedEvent, this, passage));
    }

    #endregion
}

/// <summary>Which button of a row has the focus, so Up and Down keep the column.</summary>
internal enum KnowledgeBaseRowAction
{
    Reindex,
    Delete,
    Retry
}
