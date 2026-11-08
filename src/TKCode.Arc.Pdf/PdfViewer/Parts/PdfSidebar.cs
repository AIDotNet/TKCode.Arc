using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The sidebar of a <see cref="PdfViewer"/>: thumbnails and the table of contents. The tabs show only when the document has a
/// table of contents; without one the thumbnails are all there is.
/// </summary>
[TemplatePart(TabsPart, typeof(SegmentedControl))]
[TemplatePart(ThumbnailScrollPart, typeof(ScrollViewer))]
[TemplatePart(ThumbnailsPart, typeof(PdfThumbnailList))]
[TemplatePart(OutlinePart, typeof(TreeView))]
[PseudoClasses(FloatingPseudoClass)]
internal sealed class PdfSidebar : TemplatedControl
{
    public const string TabsPart = "PART_Tabs";
    public const string ThumbnailScrollPart = "PART_ThumbnailScroll";
    public const string ThumbnailsPart = "PART_Thumbnails";
    public const string OutlinePart = "PART_Outline";
    public const string FloatingPseudoClass = ":floating";

    private readonly PdfViewer _viewer;
    private SegmentedControl? _tabs;
    private ScrollViewer? _thumbnailScroll;
    private PdfThumbnailList? _thumbnails;
    private TreeView? _tree;
    private IReadOnlyList<PdfOutlineItem> _outline = [];
    private int _pageCount;
    private bool _syncing;

    public PdfSidebar(PdfViewer viewer)
    {
        _viewer = viewer;
    }

    public PdfThumbnailList? Thumbnails => _thumbnails;

    public TreeView? OutlineTree => _tree;

    public SegmentedControl? Tabs => _tabs;

    /// <summary>What shows now: the outline needs a table of contents.</summary>
    public PdfSidebarMode EffectiveMode => _outline.Count > 0 ? _viewer.SidebarMode : PdfSidebarMode.Thumbnails;

    public void SetPages(IPageSource? pages)
    {
        _pageCount = pages?.PageCount ?? 0;
        _thumbnails?.SetPages(_pageCount);
    }

    public void SetOutline(IReadOnlyList<PdfOutlineItem> outline)
    {
        _outline = outline;
        if (_tree is { } tree)
        {
            tree.ItemsSource = outline;
        }
        UpdateMode();
    }

    public void SetFloating(bool floating) => PseudoClasses.Set(FloatingPseudoClass, floating);

    public void UpdateMode()
    {
        var mode = EffectiveMode;
        if (_tabs is { } tabs)
        {
            tabs.IsVisible = _outline.Count > 0;
            _syncing = true;
            tabs.SelectedIndex = mode == PdfSidebarMode.Outline ? 1 : 0;
            _syncing = false;
        }
        if (_tree is { } tree)
        {
            tree.IsVisible = mode == PdfSidebarMode.Outline;
        }
        if (_thumbnailScroll is { } scroll)
        {
            scroll.IsVisible = mode == PdfSidebarMode.Thumbnails;
        }
        if (mode == PdfSidebarMode.Thumbnails)
        {
            OnCurrentPageChanged();
        }
    }

    public void OnCurrentPageChanged()
    {
        if (_thumbnails is not { } list)
        {
            return;
        }
        var current = _viewer.CurrentPageIndex;
        foreach (var thumbnail in list.Realized)
        {
            thumbnail.IsCurrent = thumbnail.PageIndex == current;
        }
        if (IsEffectivelyVisible && current < list.ItemCount && EffectiveMode == PdfSidebarMode.Thumbnails)
        {
            list.ScrollIntoView(current);
        }
    }

    public void OnThumbnailReady(int pageIndex, PixelSize size)
    {
        if (_thumbnails is not { } list)
        {
            return;
        }
        foreach (var thumbnail in list.Realized)
        {
            if (thumbnail.PageIndex == pageIndex && thumbnail.TargetSize == size)
            {
                thumbnail.OnBitmapReady();
            }
        }
    }

    /// <summary>Labels arrived: the thumbnails in view pick them up.</summary>
    public void OnPageLabelsChanged()
    {
        if (_thumbnails is { } list)
        {
            foreach (var thumbnail in list.Realized)
            {
                thumbnail.OnLabelChanged();
            }
        }
    }

    public void OnReopened()
    {
        if (_thumbnails is { } list)
        {
            foreach (var thumbnail in list.Realized)
            {
                thumbnail.OnReopened();
            }
        }
        OnCurrentPageChanged();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_tabs is not null)
        {
            _tabs.SelectionChanged -= OnTabChanged;
        }
        if (_tree is not null)
        {
            _tree.ItemInvoked -= OnOutlineInvoked;
        }
        _tabs = e.NameScope.Find<SegmentedControl>(TabsPart);
        _thumbnailScroll = e.NameScope.Find<ScrollViewer>(ThumbnailScrollPart);
        _thumbnails = e.NameScope.Find<PdfThumbnailList>(ThumbnailsPart);
        _tree = e.NameScope.Find<TreeView>(OutlinePart);
        if (_tabs is not null)
        {
            _tabs.SelectionChanged += OnTabChanged;
        }
        if (_tree is { } tree)
        {
            tree.ChildrenSelector = static item => ((PdfOutlineItem)item).Children;
            tree.HeaderSelector = static item => ((PdfOutlineItem)item).Title;
            tree.ItemsSource = _outline;
            tree.ItemInvoked += OnOutlineInvoked;
        }
        if (_thumbnails is { } thumbnails)
        {
            thumbnails.Viewer = _viewer;
            thumbnails.SetPages(_pageCount);
        }
        UpdateMode();
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _tabs is { } tabs)
        {
            _viewer.SetCurrentValue(PdfViewer.SidebarModeProperty, tabs.SelectedIndex == 1 ? PdfSidebarMode.Outline : PdfSidebarMode.Thumbnails);
        }
    }

    private void OnOutlineInvoked(object? sender, TreeViewItemEventArgs e)
    {
        if (e.Item is PdfOutlineItem { Destination: { } destination })
        {
            _viewer.JumpTo(destination);
        }
    }
}
