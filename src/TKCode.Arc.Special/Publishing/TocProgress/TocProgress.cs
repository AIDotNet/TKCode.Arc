using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How a <see cref="TocProgress"/> presents itself.</summary>
public enum TocProgressDisplayMode
{
    /// <summary>The full list in wide viewports, the compact button in narrow ones (below <c>ArcViewport.CompactWidth</c>).</summary>
    Auto,

    /// <summary>Always the full list with its rail.</summary>
    List,

    /// <summary>Always one button with a progress ring and the current section; it opens the list in a popup.</summary>
    Compact
}

/// <summary>
/// A table of contents for a long article that follows the reader. The section being read is highlighted, the sections
/// above it read as done, and a rail along the list fills down to the reader's place inside the current section, with the
/// percentage of the article read beside the heading. Choosing a section scrolls the article to it.
/// </summary>
/// <remarks>
/// Point <see cref="Target"/> at the article's <c>ScrollViewer</c>. Sections come from <see cref="Entries"/>, or, when it is
/// empty, from every element inside the article with the attached <c>TocProgress.Heading</c> text (and optional
/// <c>TocProgress.Level</c>), in document order. A heading counts as reached when it passes the reading line,
/// <see cref="ActivationRatio"/> of the viewport below its top; at the very end the last section is active. In narrow
/// viewports (<see cref="DisplayMode"/> Auto) the list folds into a button with a progress ring that opens it in a popup.
/// </remarks>
[TemplatePart(ListHostPart, typeof(Decorator))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(PercentPart, typeof(TextBlock))]
[TemplatePart(CompactPart, typeof(AvaloniaButton))]
[TemplatePart(CompactRingPart, typeof(ArcRing))]
[TemplatePart(CompactTextPart, typeof(ArcContentSwap))]
[TemplatePart(PopupPart, typeof(ArcPopup))]
[TemplatePart(PopupHostPart, typeof(Decorator))]
[PseudoClasses(CompactPseudoClass, DonePseudoClass)]
public class TocProgress : TemplatedControl
{
    internal const string ListHostPart = "PART_ListHost";
    internal const string TitlePart = "PART_Title";
    internal const string PercentPart = "PART_Percent";
    internal const string CompactPart = "PART_Compact";
    internal const string CompactRingPart = "PART_CompactRing";
    internal const string CompactTextPart = "PART_CompactText";
    internal const string PopupPart = "PART_Popup";
    internal const string PopupHostPart = "PART_PopupHost";
    internal const string CompactPseudoClass = ":compact";
    internal const string DonePseudoClass = ":done";

    /// <summary>Marks an element inside the article as a section heading; the value is the words shown in the list.</summary>
    public static readonly AttachedProperty<string?> HeadingProperty =
        AvaloniaProperty.RegisterAttached<TocProgress, Control, string?>("Heading");

    /// <summary>The depth of a heading marked with <c>TocProgress.Heading</c>: 1 (the default), 2 or 3.</summary>
    public static readonly AttachedProperty<int> LevelProperty =
        AvaloniaProperty.RegisterAttached<TocProgress, Control, int>("Level", 1);

    /// <summary>The article's scroll viewer, whose position the list follows and which it scrolls when a section is chosen.</summary>
    public static readonly StyledProperty<ScrollViewer?> TargetProperty =
        AvaloniaProperty.Register<TocProgress, ScrollViewer?>(nameof(Target));

    /// <summary>Where the reading line sits, as a share of the viewport's height from its top (0 to 1).</summary>
    public static readonly StyledProperty<double> ActivationRatioProperty =
        AvaloniaProperty.Register<TocProgress, double>(nameof(ActivationRatio), 0.25, coerce: (_, v) => Math.Clamp(v, 0, 1));

    /// <summary>The room left above a heading when the list scrolls the article to it.</summary>
    public static readonly StyledProperty<double> ScrollMarginProperty =
        AvaloniaProperty.Register<TocProgress, double>(nameof(ScrollMargin), 16);

    /// <summary>Whether the percentage read shows beside the heading.</summary>
    public static readonly StyledProperty<bool> ShowsPercentProperty =
        AvaloniaProperty.Register<TocProgress, bool>(nameof(ShowsPercent), true);

    /// <summary>The full list, the compact button, or either one by viewport width.</summary>
    public static readonly StyledProperty<TocProgressDisplayMode> DisplayModeProperty =
        AvaloniaProperty.Register<TocProgress, TocProgressDisplayMode>(nameof(DisplayMode));

    /// <summary>The words the list shows and announces.</summary>
    public static readonly StyledProperty<TocProgressLabels> LabelsProperty =
        AvaloniaProperty.Register<TocProgress, TocProgressLabels>(nameof(Labels), new TocProgressLabels());

    /// <summary>The section being read, or -1 before the first heading (read-only).</summary>
    public static readonly DirectProperty<TocProgress, int> ActiveIndexProperty =
        AvaloniaProperty.RegisterDirect<TocProgress, int>(nameof(ActiveIndex), o => o.ActiveIndex);

    /// <summary>How much of the article has been scrolled through, 0 to 1 (read-only).</summary>
    public static readonly DirectProperty<TocProgress, double> ProgressProperty =
        AvaloniaProperty.RegisterDirect<TocProgress, double>(nameof(Progress), o => o.Progress);

    /// <summary>True while the compact button shows instead of the list (read-only).</summary>
    public static readonly DirectProperty<TocProgress, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<TocProgress, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>Raised when the reader moves into another section.</summary>
    public static readonly RoutedEvent<TocProgressActiveChangedEventArgs> ActiveChangedEvent =
        RoutedEvent.Register<TocProgress, TocProgressActiveChangedEventArgs>(nameof(ActiveChanged), RoutingStrategies.Bubble);

    private readonly List<TocProgressEntry> _effective = [];
    private double[] _tops = [];
    private TocProgressList? _list;
    private Decorator? _listHost;
    private Decorator? _popupHost;
    private TextBlock? _title;
    private TextBlock? _percent;
    private AvaloniaButton? _compact;
    private ArcRing? _compactRing;
    private ArcContentSwap? _compactText;
    private ArcPopup? _popup;
    private ScrollViewer? _watched;
    private IDisposable? _viewportWatch;
    private MotionValue<double>? _scroll;
    private int _navigatingTo = -1;
    private double _lastSetOffset = double.NaN;
    private int _activeIndex = -1;
    private double _progress;
    private bool _isCompact;
    private int _shownPercent = -1;
    private bool _refreshQueued;

    static TocProgress()
    {
        FocusableProperty.OverrideDefaultValue<TocProgress>(false);
        ClipToBoundsProperty.OverrideDefaultValue<TocProgress>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<TocProgress>(AutomationLandmarkType.Navigation);
        HeadingProperty.Changed.AddClassHandler<Control>((c, _) => NotifyHeadingChanged(c));
        LevelProperty.Changed.AddClassHandler<Control>((c, _) => NotifyHeadingChanged(c));
    }

    public TocProgress()
    {
        Entries.CollectionChanged += (_, _) => Refresh();
        AutomationProperties.SetName(this, Labels.Title);
    }

    /// <summary>The sections, in article order. Leave empty to collect the elements marked with <c>TocProgress.Heading</c>.</summary>
    [Content]
    public AvaloniaList<TocProgressEntry> Entries { get; } = [];

    /// <inheritdoc cref="TargetProperty"/>
    public ScrollViewer? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <inheritdoc cref="ActivationRatioProperty"/>
    public double ActivationRatio
    {
        get => GetValue(ActivationRatioProperty);
        set => SetValue(ActivationRatioProperty, value);
    }

    /// <inheritdoc cref="ScrollMarginProperty"/>
    public double ScrollMargin
    {
        get => GetValue(ScrollMarginProperty);
        set => SetValue(ScrollMarginProperty, value);
    }

    /// <inheritdoc cref="ShowsPercentProperty"/>
    public bool ShowsPercent
    {
        get => GetValue(ShowsPercentProperty);
        set => SetValue(ShowsPercentProperty, value);
    }

    /// <inheritdoc cref="DisplayModeProperty"/>
    public TocProgressDisplayMode DisplayMode
    {
        get => GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public TocProgressLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="ActiveIndexProperty"/>
    public int ActiveIndex
    {
        get => _activeIndex;
        private set => SetAndRaise(ActiveIndexProperty, ref _activeIndex, value);
    }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress
    {
        get => _progress;
        private set => SetAndRaise(ProgressProperty, ref _progress, value);
    }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => _isCompact;
        private set => SetAndRaise(IsCompactProperty, ref _isCompact, value);
    }

    /// <summary>The sections in use: <see cref="Entries"/>, or the headings found in the article.</summary>
    public IReadOnlyList<TocProgressEntry> Sections => _effective;

    /// <inheritdoc cref="ActiveChangedEvent"/>
    public event EventHandler<TocProgressActiveChangedEventArgs>? ActiveChanged
    {
        add => AddHandler(ActiveChangedEvent, value);
        remove => RemoveHandler(ActiveChangedEvent, value);
    }

    internal TocProgressList? List => _list;

    /// <summary>Reads the heading text marked on <paramref name="element"/>.</summary>
    public static string? GetHeading(Control element) => element.GetValue(HeadingProperty);

    /// <summary>Marks <paramref name="element"/> as a section heading with the words <paramref name="value"/>.</summary>
    public static void SetHeading(Control element, string? value) => element.SetValue(HeadingProperty, value);

    /// <summary>Reads the depth of the heading marked on <paramref name="element"/>.</summary>
    public static int GetLevel(Control element) => element.GetValue(LevelProperty);

    /// <summary>Sets the depth (1 to 3) of the heading marked on <paramref name="element"/>.</summary>
    public static void SetLevel(Control element, int value) => element.SetValue(LevelProperty, value);

    /// <summary>Collects the sections again (after the article's headings changed) and recomputes the reader's place.</summary>
    public void Refresh()
    {
        _effective.Clear();
        if (Entries.Count > 0)
        {
            foreach (var entry in Entries)
            {
                entry.Owner = this;
                _effective.Add(entry);
            }
        }
        else if (Target?.Content is Visual content)
        {
            Collect(content);
        }
        _tops = new double[_effective.Count];
        _list?.SetEntries(_effective);
        Track(animate: false);
    }

    /// <summary>
    /// Scrolls the article so section <paramref name="index"/> starts just under the top of the viewport (on a spring at
    /// the full motion level, at once otherwise). The section is highlighted for the whole trip.
    /// </summary>
    public void ScrollTo(int index) => ScrollTo(index, keyboard: false);

    internal void ScrollTo(int index, bool keyboard)
    {
        if (Target is not { } target || index < 0 || index >= _effective.Count || TopOf(index) is not { } top)
        {
            return;
        }
        var offset = TocProgressMath.OffsetFor(top, ScrollMargin, target.Viewport.Height, target.Extent.Height);
        _navigatingTo = index;
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
            if (keyboard)
            {
                _compact?.Focus(NavigationMethod.Tab);
            }
        }
        var scroll = ScrollValue(target);
        if (ArcMotion.IsFull(this) && Math.Abs(target.Offset.Y - offset) > 1)
        {
            _ = FinishScroll(scroll.AnimateTo(offset, MotionSpec.Spring(ArcSpring.Smooth)), index);
        }
        else
        {
            scroll.Jump(offset);
            _navigatingTo = -1;
        }
        Track(animate: true);
    }

    private async Task FinishScroll(Task<bool> motion, int index)
    {
        await motion;
        if (_navigatingTo == index)
        {
            _navigatingTo = -1;
            Track(animate: true);
        }
    }

    private MotionValue<double> ScrollValue(ScrollViewer target)
    {
        if (_scroll is null)
        {
            _scroll = new MotionValue<double>(this, target.Offset.Y, MotionVectors.Double);
            _scroll.Changed += v =>
            {
                if (Target is { } t)
                {
                    _lastSetOffset = v;
                    t.Offset = new Vector(t.Offset.X, v);
                }
            };
        }
        else if (!_scroll.IsAnimating)
        {
            _scroll.Jump(target.Offset.Y);
        }
        return _scroll;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_compact is not null)
        {
            _compact.Click -= OnCompactClick;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        if (_list is not null)
        {
            Detach(_list);
        }
        _listHost = e.NameScope.Find<Decorator>(ListHostPart);
        _popupHost = e.NameScope.Find<Decorator>(PopupHostPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _percent = e.NameScope.Find<TextBlock>(PercentPart);
        _compact = e.NameScope.Find<AvaloniaButton>(CompactPart);
        _compactRing = e.NameScope.Find<ArcRing>(CompactRingPart);
        _compactText = e.NameScope.Find<ArcContentSwap>(CompactTextPart);
        _popup = e.NameScope.Find<ArcPopup>(PopupPart);
        _list ??= new TocProgressList { Owner = this };
        if (_compact is not null)
        {
            _compact.Click += OnCompactClick;
        }
        if (_popup is not null)
        {
            _popup.PlacementTarget = _compact;
            _popup.Closed += OnPopupClosed;
        }
        _shownPercent = -1;
        PlaceList();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Target);
        _viewportWatch?.Dispose();
        _viewportWatch = ArcViewport.Observe(this, _ => UpdateCompact());
        UpdateCompact();
        QueueRefresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(null);
        _viewportWatch?.Dispose();
        _viewportWatch = null;
        _scroll?.Stop();
        _navigatingTo = -1;
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty)
        {
            if (VisualRoot is not null)
            {
                Watch(Target);
            }
            Refresh();
        }
        else if (change.Property == ActivationRatioProperty)
        {
            Track(animate: true);
        }
        else if (change.Property == DisplayModeProperty)
        {
            UpdateCompact();
        }
        else if (change.Property == LabelsProperty)
        {
            AutomationProperties.SetName(this, Labels.Title);
            _shownPercent = -1;
            UpdateTexts(force: true);
        }
        else if (change.Property == ShowsPercentProperty)
        {
            UpdateTexts(force: true);
        }
    }

    internal void OnEntryChanged(TocProgressEntry entry) => QueueRefresh();

    private static void NotifyHeadingChanged(Control element)
    {
        // A heading changed inside some article: the lists that collect headings look again on the next pass.
        for (var visual = element.GetVisualParent(); visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ScrollViewer viewer && s_watchers.TryGetValue(viewer, out var owner) && owner.TryGetTarget(out var toc))
            {
                toc.QueueRefresh();
                return;
            }
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScrollViewer, WeakReference<TocProgress>> s_watchers = new();

    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            Refresh();
        }, DispatcherPriority.Loaded);
    }

    private void Collect(Visual root)
    {
        foreach (var visual in root.GetVisualDescendants())
        {
            if (visual is Control control && GetHeading(control) is { Length: > 0 } heading)
            {
                _effective.Add(new TocProgressEntry { Title = heading, Level = GetLevel(control), Target = control });
            }
        }
    }

    private void Watch(ScrollViewer? viewer)
    {
        if (_watched == viewer)
        {
            return;
        }
        if (_watched is not null)
        {
            _watched.ScrollChanged -= OnScrollChanged;
            _watched.SizeChanged -= OnViewerSizeChanged;
            s_watchers.Remove(_watched);
        }
        _watched = viewer;
        _scroll?.Stop();
        _scroll = null;
        if (_watched is not null)
        {
            _watched.ScrollChanged += OnScrollChanged;
            _watched.SizeChanged += OnViewerSizeChanged;
            s_watchers.AddOrUpdate(_watched, new WeakReference<TocProgress>(this));
        }
    }

    private void OnViewerSizeChanged(object? sender, SizeChangedEventArgs e) => Track(animate: false);

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (Target is { } target && _scroll is { IsAnimating: true } scroll && Math.Abs(target.Offset.Y - _lastSetOffset) > 1)
        {
            // The reader took over (wheel, scroll bar, keys): stop gliding and follow them.
            scroll.Stop();
            _navigatingTo = -1;
        }
        if (e.ExtentDelta.Y != 0 && _effective.Count > 0 && Entries.Count == 0)
        {
            QueueRefresh();
        }
        Track(animate: true);
    }

    private double? TopOf(int index)
    {
        if (Target?.Content is not Visual content || _effective[index].Target is not { } heading)
        {
            return null;
        }
        return heading.TranslatePoint(default, content) is { } point ? point.Y : null;
    }

    /// <summary>Works out the reader's place and shows it.</summary>
    private void Track(bool animate)
    {
        TocProgressPosition position;
        if (Target is { } target && _effective.Count > 0)
        {
            for (var i = 0; i < _effective.Count; i++)
            {
                // A heading that is not laid out (yet) shares the previous one's place, so the order holds.
                _tops[i] = TopOf(i) ?? (i > 0 ? _tops[i - 1] : 0);
            }
            position = TocProgressMath.Locate(_tops, target.Offset.Y, target.Viewport.Height, target.Extent.Height,
                ActivationRatio);
        }
        else
        {
            position = new TocProgressPosition(-1, 0, 0);
        }
        if (_navigatingTo >= 0)
        {
            position = position with { ActiveIndex = _navigatingTo, SectionProgress = 0 };
        }
        var old = ActiveIndex;
        ActiveIndex = position.ActiveIndex;
        Progress = position.Progress;
        PseudoClasses.Set(DonePseudoClass, position.Progress >= 0.999);
        _list?.SetPosition(position.ActiveIndex, position.SectionProgress, animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off);
        if (_compactRing is not null)
        {
            _compactRing.Value = position.Progress;
        }
        UpdateTexts(force: old != position.ActiveIndex);
        if (old != position.ActiveIndex)
        {
            var entry = position.ActiveIndex >= 0 && position.ActiveIndex < _effective.Count ? _effective[position.ActiveIndex] : null;
            RaiseEvent(new TocProgressActiveChangedEventArgs(ActiveChangedEvent, old, position.ActiveIndex, entry));
        }
    }

    private void UpdateTexts(bool force)
    {
        var percent = (int)Math.Round(Progress * 100);
        if (!force && percent == _shownPercent)
        {
            return;
        }
        _shownPercent = percent;
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_percent is not null)
        {
            _percent.Text = labels.Percent(percent);
            _percent.IsVisible = ShowsPercent && _effective.Count > 0;
        }
        var section = ActiveIndex >= 0 && ActiveIndex < _effective.Count ? _effective[ActiveIndex].Title : null;
        if (_compactText is not null && force)
        {
            var text = section ?? labels.CompactIdle;
            if (_compactText.Content as string != text)
            {
                _compactText.Content = text;
            }
        }
        if (_compact is not null)
        {
            AutomationProperties.SetName(_compact, labels.CompactName(section, percent));
        }
    }

    private void UpdateCompact()
    {
        var compact = DisplayMode switch
        {
            TocProgressDisplayMode.Compact => true,
            TocProgressDisplayMode.List => false,
            _ => VisualRoot is not null && ArcViewport.IsCompact(this)
        };
        if (compact == IsCompact && _list?.Parent is not null)
        {
            return;
        }
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (!compact && _popup is not null)
        {
            _popup.IsOpen = false;
        }
        PlaceList();
    }

    /// <summary>The list lives inline in list mode and inside the popup's surface in compact mode.</summary>
    private void PlaceList()
    {
        if (_list is null)
        {
            return;
        }
        var host = IsCompact ? _popupHost : _listHost;
        if (host is null || host.Child == _list)
        {
            return;
        }
        Detach(_list);
        host.Child = _list;
    }

    private void Detach(TocProgressList list)
    {
        if (_listHost?.Child == list)
        {
            _listHost.Child = null;
        }
        if (_popupHost?.Child == list)
        {
            _popupHost.Child = null;
        }
    }

    private void OnCompactClick(object? sender, RoutedEventArgs e)
    {
        if (_popup is null)
        {
            return;
        }
        _popup.IsOpen = !_popup.IsOpen;
        if (_popup.IsOpen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_list is not null && _list.Rows.Count > 0)
                {
                    _list.FocusRow(ActiveIndex >= 0 ? ActiveIndex : 0);
                }
            }, DispatcherPriority.Input);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_list is not null && _compact is not null && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
            && _list.IsVisualAncestorOf(focused))
        {
            _compact.Focus(NavigationMethod.Tab);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && _popup is { IsOpen: true })
        {
            _compact?.Focus(NavigationMethod.Tab);
            _popup.IsOpen = false;
            e.Handled = true;
        }
    }
}
