using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The page beside the assistant (spec 3.2, 3.3): a scrolling column at most <see cref="MaxColumnWidth"/> wide (680,
/// padding included), centered, with the title (display font 22 / 24.2, 500), the meta line (12 / 16.8, TextMuted, 6px
/// under it), 18px, then the blocks: headings (20px above), paragraphs and tables (4px above). Each block is a
/// <see cref="AiPanelBlockView"/>: the marker line shows on <see cref="MarkedBlockId"/>, <see cref="PlayChange"/> sweeps a
/// change into it, <see cref="ReplaceBlock"/> replaces it at once. The view follows the page: blocks added, removed or
/// replaced and text or cell changes show on the next dispatcher turn (so a host that changes the data and calls
/// <see cref="PlayChange"/> in the same turn keeps the animation, in either order).
/// <para>
/// The person drags to select text across the page's texts (headings, paragraphs, table cells); on release a non-empty
/// selection brings up the "Ask about selection" button under its end; pressing it raises
/// <see cref="SelectionRequested"/> with the <see cref="AiPanelSelection"/> (the block it starts in, that block's label, the
/// text) and clears the selection. A press elsewhere, an empty selection or a change to a selected block hides the button.
/// Every word comes from the owner (<see cref="AskButtonText"/>); blocks are named by their labels.
/// </para>
/// </summary>
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ArticlePart, typeof(AiPanelArticle))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass)]
internal sealed partial class AiPanelPageView : TemplatedControl
{
    public const string ScrollPart = "PART_Scroll";
    public const string ArticlePart = "PART_Article";
    public const string TitlePart = "PART_Title";
    public const string MetaPart = "PART_Meta";
    public const string CompactPseudoClass = ":compact";

    /// <summary>The gap above a heading block.</summary>
    public const double HeadingGap = 20;

    /// <summary>The gap above any other block.</summary>
    public const double BlockGap = 4;

    #region Properties

    /// <summary>The page shown.</summary>
    public static readonly StyledProperty<AiPanelPage?> PageProperty =
        AvaloniaProperty.Register<AiPanelPageView, AiPanelPage?>(nameof(Page));

    /// <summary>The id of the block that shows the marker line (the selection's block), or null.</summary>
    public static readonly StyledProperty<string?> MarkedBlockIdProperty =
        AvaloniaProperty.Register<AiPanelPageView, string?>(nameof(MarkedBlockId));

    /// <summary>The assistant's mark on the ask button; null draws the default spark.</summary>
    public static readonly StyledProperty<object?> AssistantMarkProperty =
        AvaloniaProperty.Register<AiPanelPageView, object?>(nameof(AssistantMark));

    /// <summary>The ask button's text ("Ask about selection").</summary>
    public static readonly StyledProperty<string?> AskButtonTextProperty =
        AvaloniaProperty.Register<AiPanelPageView, string?>(nameof(AskButtonText));

    /// <summary>The narrow layout (the assistant is a bottom sheet): padding 56 22 40 instead of 72 36 48.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<AiPanelPageView, bool>(nameof(IsCompact));

    /// <summary>The widest the article column grows, padding included.</summary>
    public static readonly StyledProperty<double> MaxColumnWidthProperty =
        AvaloniaProperty.Register<AiPanelPageView, double>(nameof(MaxColumnWidth), 680);

    /// <summary>The space inside the column (set by the theme: 72 36 48, compact 56 22 40).</summary>
    public static readonly StyledProperty<Thickness> ColumnPaddingProperty =
        AvaloniaProperty.Register<AiPanelPageView, Thickness>(nameof(ColumnPadding));

    /// <summary>Raised (bubbling) when the person presses "Ask about selection".</summary>
    public static readonly RoutedEvent<AiPanelSelectionEventArgs> SelectionRequestedEvent =
        RoutedEvent.Register<AiPanelPageView, AiPanelSelectionEventArgs>(nameof(SelectionRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PageProperty"/>
    public AiPanelPage? Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <inheritdoc cref="MarkedBlockIdProperty"/>
    public string? MarkedBlockId { get => GetValue(MarkedBlockIdProperty); set => SetValue(MarkedBlockIdProperty, value); }

    /// <inheritdoc cref="AssistantMarkProperty"/>
    public object? AssistantMark { get => GetValue(AssistantMarkProperty); set => SetValue(AssistantMarkProperty, value); }

    /// <inheritdoc cref="AskButtonTextProperty"/>
    public string? AskButtonText { get => GetValue(AskButtonTextProperty); set => SetValue(AskButtonTextProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="MaxColumnWidthProperty"/>
    public double MaxColumnWidth { get => GetValue(MaxColumnWidthProperty); set => SetValue(MaxColumnWidthProperty, value); }

    /// <inheritdoc cref="ColumnPaddingProperty"/>
    public Thickness ColumnPadding { get => GetValue(ColumnPaddingProperty); set => SetValue(ColumnPaddingProperty, value); }

    /// <inheritdoc cref="SelectionRequestedEvent"/>
    public event EventHandler<AiPanelSelectionEventArgs>? SelectionRequested
    {
        add => AddHandler(SelectionRequestedEvent, value);
        remove => RemoveHandler(SelectionRequestedEvent, value);
    }

    #endregion

    private readonly List<AiPanelBlockView> _views = [];
    private ScrollViewer? _scroll;
    private AiPanelArticle? _article;
    private TextBlock? _title;
    private TextBlock? _meta;
    private AiPanelPage? _observedPage;
    private bool _syncPending;

    static AiPanelPageView()
    {
        // Focusable for copy (Ctrl+C) after a press, but not a Tab stop: Arc's page body is not in the tab order.
        FocusableProperty.OverrideDefaultValue<AiPanelPageView>(true);
        IsTabStopProperty.OverrideDefaultValue<AiPanelPageView>(false);
        FocusAdornerProperty.OverrideDefaultValue<AiPanelPageView>(null);
    }

    public AiPanelPageView()
    {
        CreateAskButtons();
    }

    /// <summary>The block views in page order.</summary>
    internal IReadOnlyList<AiPanelBlockView> BlockViews => _views;

    /// <summary>The scroller (once the template is applied).</summary>
    internal ScrollViewer? Scroller => _scroll;

    /// <summary>The article column (once the template is applied).</summary>
    internal AiPanelArticle? Article => _article;

    /// <summary>The view of block <paramref name="id"/>, or null.</summary>
    internal AiPanelBlockView? ViewOf(string id) => _views.FirstOrDefault(v => v.Block.Id == id);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_article is not null)
        {
            foreach (var view in _views)
            {
                _article.Children.Remove(view);
            }
            foreach (var ask in _asks)
            {
                _article.Children.Remove(ask);
            }
        }
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _article = e.NameScope.Find<AiPanelArticle>(ArticlePart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _meta = e.NameScope.Find<TextBlock>(MetaPart);
        if (_article is not null)
        {
            foreach (var view in _views)
            {
                _article.Children.Add(view);
            }
            foreach (var ask in _asks)
            {
                _article.Children.Add(ask);
            }
        }
        RefreshHead();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty)
        {
            ObservePage();
            ClearSelection();
            RebuildBlocks();
            RefreshHead();
        }
        else if (change.Property == MarkedBlockIdProperty)
        {
            RefreshMarker();
        }
        else if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == AssistantMarkProperty || change.Property == AskButtonTextProperty)
        {
            RefreshAskButtons();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObservePage();
        // Anything that changed while the view was away shows now.
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unobserve();
    }

    /// <summary>
    /// Plays a change of block <paramref name="blockId"/> to the content of <paramref name="newBlock"/> (spec 5.6, 5.7): the
    /// tint sweeps left to right, holds and fades (1.4s); the old text fades out in 100ms while the new one fades in after
    /// 80ms over 180ms; the block's height springs to the new height. An undo plays the same sweep (spec 5.7:
    /// <paramref name="isUndo"/> does not change the look). Reduced motion fades the whole tint in 600ms and cross-fades in
    /// 120ms with the height at once; motion off replaces at once. The page data is the host's: update it as well (before or
    /// after this call, in the same turn). False when no block has that id.
    /// </summary>
    public bool PlayChange(string blockId, AiPanelBlock newBlock, bool isUndo)
    {
        _ = isUndo;
        var view = ViewOf(blockId);
        if (view is null)
        {
            return false;
        }
        InvalidatePieces();
        view.Play(newBlock, EffectiveLevel());
        return true;
    }

    /// <summary>Replaces block <paramref name="blockId"/>'s content with <paramref name="block"/>'s at once. False when no block has that id.</summary>
    public bool ReplaceBlock(string blockId, AiPanelBlock block)
    {
        var view = ViewOf(blockId);
        if (view is null)
        {
            return false;
        }
        InvalidatePieces();
        view.Show(block);
        return true;
    }

    /// <summary>Scrolls the page so block <paramref name="id"/> is in view. False when no block has that id.</summary>
    public bool ScrollToBlock(string id)
    {
        var view = ViewOf(id);
        if (view is null)
        {
            return false;
        }
        view.BringIntoView();
        return true;
    }

    private ArcMotionLevel EffectiveLevel() =>
        TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    private void ObservePage()
    {
        var page = TopLevel.GetTopLevel(this) is not null ? Page : null;
        if (ReferenceEquals(page, _observedPage))
        {
            return;
        }
        Unobserve();
        _observedPage = page;
        if (page is not null)
        {
            page.PropertyChanged += OnPagePropertyChanged;
            page.Blocks.CollectionChanged += OnBlocksChanged;
        }
    }

    private void Unobserve()
    {
        if (_observedPage is { } page)
        {
            page.PropertyChanged -= OnPagePropertyChanged;
            page.Blocks.CollectionChanged -= OnBlocksChanged;
        }
        _observedPage = null;
    }

    private void OnPagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => RefreshHead();

    private void OnBlocksChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleSync();

    private void OnBlockDataChanged(object? sender, EventArgs e) => ScheduleSync();

    private void ScheduleSync()
    {
        if (_syncPending)
        {
            return;
        }
        _syncPending = true;
        Dispatcher.UIThread.Post(Sync);
    }

    /// <summary>Brings the views in line with the page data at once (normally done on the next dispatcher turn).</summary>
    internal void Sync()
    {
        _syncPending = false;
        ReconcileBlocks();
        foreach (var view in _views)
        {
            if (view.NeedsSync)
            {
                InvalidatePieces();
            }
            view.Sync();
        }
        RefreshGaps();
        RefreshMarker();
    }

    private void RefreshHead()
    {
        var page = Page;
        if (_title is not null)
        {
            _title.Text = page?.Title;
            _title.IsVisible = !string.IsNullOrEmpty(page?.Title);
        }
        if (_meta is not null)
        {
            _meta.Text = page?.Meta;
            _meta.IsVisible = !string.IsNullOrEmpty(page?.Meta);
        }
    }

    private void RebuildBlocks()
    {
        InvalidatePieces();
        foreach (var view in _views)
        {
            Detach(view);
        }
        _views.Clear();
        ReconcileBlocks();
        RefreshGaps();
        RefreshMarker();
    }

    private void ReconcileBlocks()
    {
        var blocks = Page?.Blocks;
        var next = new List<AiPanelBlockView>(blocks?.Count ?? 0);
        var unused = new List<AiPanelBlockView>(_views);
        if (blocks is not null)
        {
            foreach (var block in blocks)
            {
                // The same object first; then a replacement with the same id keeps its view (and a playing change).
                var view = unused.FirstOrDefault(v => ReferenceEquals(v.Block, block))
                           ?? unused.FirstOrDefault(v => v.Block.Id == block.Id && v.Block.GetType() == block.GetType());
                if (view is null)
                {
                    view = new AiPanelBlockView(block);
                    view.DataChanged += OnBlockDataChanged;
                }
                else
                {
                    unused.Remove(view);
                    view.Rebind(block);
                }
                next.Add(view);
            }
        }
        var moved = !next.SequenceEqual(_views);
        if (moved)
        {
            InvalidatePieces();
        }
        foreach (var view in unused)
        {
            Detach(view);
        }
        if (moved && _article is not null)
        {
            foreach (var view in _views)
            {
                _article.Children.Remove(view);
            }
            // After the head (title, meta), before the floating buttons.
            var at = _article.Children.Count(c => c is not AiPanelBlockView and not AiPanelAskButton);
            foreach (var view in next)
            {
                _article.Children.Insert(at++, view);
            }
        }
        _views.Clear();
        _views.AddRange(next);
    }

    private void Detach(AiPanelBlockView view)
    {
        view.DataChanged -= OnBlockDataChanged;
        _article?.Children.Remove(view);
    }

    private void RefreshGaps()
    {
        for (var i = 0; i < _views.Count; i++)
        {
            var gap = _views[i].Block is AiPanelHeading ? HeadingGap : BlockGap;
            _views[i].Margin = new Thickness(0, gap, 0, 0);
        }
    }

    private void RefreshMarker()
    {
        var id = MarkedBlockId;
        foreach (var view in _views)
        {
            view.IsMarked = id is not null && view.Block.Id == id;
        }
    }
}
