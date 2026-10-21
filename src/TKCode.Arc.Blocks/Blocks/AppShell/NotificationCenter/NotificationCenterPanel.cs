// Ported from Arc's free notification-center block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The floating panel of a <see cref="NotificationCenter"/>: the header (heading, unread chip, summary, close), the
/// All / Unread switch with "Mark all read", the list of rows or the empty state, and the "N read · Clear read"
/// footer. It shows the owner's state: <see cref="Sync"/> reconciles the rows by id the way AnimatePresence does
/// (leaving rows stay in place until their exit ends) and moves every animated part into its new state.
/// </summary>
[TemplatePart(CountPart, typeof(ArcContentSwap))]
[TemplatePart(SummaryPart, typeof(ArcContentSwap))]
[TemplatePart(ClosePart, typeof(NotificationCenterButton))]
[TemplatePart(ViewSwitchPart, typeof(Panel))]
[TemplatePart(ViewHighlightPart, typeof(GlidingIndicator))]
[TemplatePart(AllViewPart, typeof(NotificationCenterButton))]
[TemplatePart(UnreadViewPart, typeof(NotificationCenterButton))]
[TemplatePart(MarkAllRevealPart, typeof(NotificationCenterReveal))]
[TemplatePart(MarkAllPart, typeof(NotificationCenterButton))]
[TemplatePart(ListPart, typeof(StackPanel))]
[TemplatePart(EmptyRevealPart, typeof(NotificationCenterReveal))]
[TemplatePart(EmptyTitlePart, typeof(ArcContentSwap))]
[TemplatePart(EmptyDescriptionPart, typeof(ArcContentSwap))]
[TemplatePart(ViewAllPart, typeof(NotificationCenterButton))]
[TemplatePart(FooterRevealPart, typeof(NotificationCenterReveal))]
[TemplatePart(ReadCountPart, typeof(ArcContentSwap))]
[TemplatePart(ClearReadPart, typeof(NotificationCenterButton))]
[PseudoClasses(NarrowPseudoClass)]
internal sealed class NotificationCenterPanel : TemplatedControl
{
    public const string CountPart = "PART_Count";
    public const string SummaryPart = "PART_Summary";
    public const string ClosePart = "PART_Close";
    public const string ViewSwitchPart = "PART_ViewSwitch";
    public const string ViewHighlightPart = "PART_ViewHighlight";
    public const string AllViewPart = "PART_AllView";
    public const string UnreadViewPart = "PART_UnreadView";
    public const string MarkAllRevealPart = "PART_MarkAllReveal";
    public const string MarkAllPart = "PART_MarkAll";
    public const string ListPart = "PART_List";
    public const string EmptyRevealPart = "PART_EmptyReveal";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyDescriptionPart = "PART_EmptyDescription";
    public const string ViewAllPart = "PART_ViewAll";
    public const string FooterRevealPart = "PART_FooterReveal";
    public const string ReadCountPart = "PART_ReadCount";
    public const string ClearReadPart = "PART_ClearRead";

    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Arc's stagger.item: rows and bulk exits are 35ms apart.</summary>
    private static readonly TimeSpan s_stagger = TimeSpan.FromMilliseconds(35);

    /// <summary>Bulk exits cascade down the list, capped so the sweep stays under a quarter second.</summary>
    internal static TimeSpan Cascade(int index) => TimeSpan.FromMilliseconds(Math.Min(index * s_stagger.TotalMilliseconds, 200));

    /// <summary>Rows settle in 35ms apart, up to the eighth row.</summary>
    internal static TimeSpan Stagger(int index) => TimeSpan.FromMilliseconds(Math.Min(index, 7) * s_stagger.TotalMilliseconds);

    public static readonly StyledProperty<string?> HeadingProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(Heading));

    public static readonly StyledProperty<string?> AllTextProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(AllText));

    public static readonly StyledProperty<string?> UnreadTextProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(UnreadText));

    public static readonly StyledProperty<string?> MarkAllTextProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(MarkAllText));

    public static readonly StyledProperty<string?> ViewAllTextProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(ViewAllText));

    public static readonly StyledProperty<string?> ReadPrefixProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(ReadPrefix));

    public static readonly StyledProperty<string?> ReadSuffixProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(ReadSuffix));

    public static readonly StyledProperty<string?> ClearReadTextProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, string?>(nameof(ClearReadText));

    public static readonly StyledProperty<bool> IsViewAllVisibleProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, bool>(nameof(IsViewAllVisible));

    /// <summary>Widest the panel gets (424); it also stays <see cref="ViewportInset"/> px narrower than the window.</summary>
    public static readonly StyledProperty<double> MaxPanelWidthProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, double>(nameof(MaxPanelWidth), 424);

    /// <summary>Tallest the panel gets (590); it also stays <see cref="ViewportInset"/> px shorter than the window.</summary>
    public static readonly StyledProperty<double> MaxPanelHeightProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, double>(nameof(MaxPanelHeight), 590);

    public static readonly StyledProperty<double> ViewportInsetProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, double>(nameof(ViewportInset), 24);

    /// <summary>Window width at or below which the narrow layout applies (Arc: max-width 380px).</summary>
    public static readonly StyledProperty<double> NarrowBreakpointProperty =
        AvaloniaProperty.Register<NotificationCenterPanel, double>(nameof(NarrowBreakpoint), 380);

    private readonly NotificationCenter _owner;
    private readonly Dictionary<object, RowEntry> _rows = new();
    private readonly List<Control> _order = new();
    private ArcContentSwap? _count;
    private ArcContentSwap? _summary;
    private NotificationCenterButton? _close;
    private Panel? _viewSwitch;
    private GlidingIndicator? _viewHighlight;
    private NotificationCenterButton? _allView;
    private NotificationCenterButton? _unreadView;
    private NotificationCenterReveal? _markAllReveal;
    private NotificationCenterButton? _markAll;
    private StackPanel? _list;
    private NotificationCenterReveal? _emptyReveal;
    private ArcContentSwap? _emptyTitle;
    private ArcContentSwap? _emptyDescription;
    private NotificationCenterButton? _viewAll;
    private NotificationCenterReveal? _footerReveal;
    private ArcContentSwap? _readCount;
    private NotificationCenterButton? _clearRead;
    private int _shownUnread = -1;
    private int _shownRead = -1;
    private NotificationCenterView? _shownView;
    private bool _narrow;

    public NotificationCenterPanel(NotificationCenter owner)
    {
        _owner = owner;
    }

    static NotificationCenterPanel()
    {
        FocusableProperty.OverrideDefaultValue<NotificationCenterPanel>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<NotificationCenterPanel>(AutomationControlType.Pane);
    }

    public string? Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string? AllText { get => GetValue(AllTextProperty); set => SetValue(AllTextProperty, value); }
    public string? UnreadText { get => GetValue(UnreadTextProperty); set => SetValue(UnreadTextProperty, value); }
    public string? MarkAllText { get => GetValue(MarkAllTextProperty); set => SetValue(MarkAllTextProperty, value); }
    public string? ViewAllText { get => GetValue(ViewAllTextProperty); set => SetValue(ViewAllTextProperty, value); }
    public string? ReadPrefix { get => GetValue(ReadPrefixProperty); set => SetValue(ReadPrefixProperty, value); }
    public string? ReadSuffix { get => GetValue(ReadSuffixProperty); set => SetValue(ReadSuffixProperty, value); }
    public string? ClearReadText { get => GetValue(ClearReadTextProperty); set => SetValue(ClearReadTextProperty, value); }
    public bool IsViewAllVisible { get => GetValue(IsViewAllVisibleProperty); set => SetValue(IsViewAllVisibleProperty, value); }
    public double MaxPanelWidth { get => GetValue(MaxPanelWidthProperty); set => SetValue(MaxPanelWidthProperty, value); }
    public double MaxPanelHeight { get => GetValue(MaxPanelHeightProperty); set => SetValue(MaxPanelHeightProperty, value); }
    public double ViewportInset { get => GetValue(ViewportInsetProperty); set => SetValue(ViewportInsetProperty, value); }
    public double NarrowBreakpoint { get => GetValue(NarrowBreakpointProperty); set => SetValue(NarrowBreakpointProperty, value); }

    internal NotificationCenterButton? CloseButton => _close;
    internal NotificationCenterButton? AllViewButton => _allView;
    internal NotificationCenterButton? UnreadViewButton => _unreadView;
    internal NotificationCenterButton? MarkAllButton => _markAll;
    internal NotificationCenterReveal? MarkAllReveal => _markAllReveal;
    internal NotificationCenterReveal? EmptyReveal => _emptyReveal;
    internal NotificationCenterReveal? FooterReveal => _footerReveal;
    internal NotificationCenterButton? ViewAllButton => _viewAll;
    internal NotificationCenterButton? ClearReadButton => _clearRead;
    internal GlidingIndicator? ViewHighlight => _viewHighlight;
    internal StackPanel? List => _list;
    internal ArcContentSwap? CountSwap => _count;
    internal ArcContentSwap? SummarySwap => _summary;
    internal ArcContentSwap? ReadCountSwap => _readCount;
    internal ArcContentSwap? EmptyTitleSwap => _emptyTitle;

    /// <summary>The rows that are shown (not leaving), in list order.</summary>
    internal IEnumerable<NotificationCenterRow> ShownRows =>
        _list?.Children.OfType<NotificationCenterReveal>().Where(h => h.IsShown).Select(h => h.Child).OfType<NotificationCenterRow>() ?? [];

    internal bool IsNarrow => _narrow;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _count = e.NameScope.Find<ArcContentSwap>(CountPart);
        _summary = e.NameScope.Find<ArcContentSwap>(SummaryPart);
        _close = e.NameScope.Find<NotificationCenterButton>(ClosePart);
        _viewSwitch = e.NameScope.Find<Panel>(ViewSwitchPart);
        _viewHighlight = e.NameScope.Find<GlidingIndicator>(ViewHighlightPart);
        _allView = e.NameScope.Find<NotificationCenterButton>(AllViewPart);
        _unreadView = e.NameScope.Find<NotificationCenterButton>(UnreadViewPart);
        _markAllReveal = e.NameScope.Find<NotificationCenterReveal>(MarkAllRevealPart);
        _markAll = e.NameScope.Find<NotificationCenterButton>(MarkAllPart);
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _emptyReveal = e.NameScope.Find<NotificationCenterReveal>(EmptyRevealPart);
        _emptyTitle = e.NameScope.Find<ArcContentSwap>(EmptyTitlePart);
        _emptyDescription = e.NameScope.Find<ArcContentSwap>(EmptyDescriptionPart);
        _viewAll = e.NameScope.Find<NotificationCenterButton>(ViewAllPart);
        _footerReveal = e.NameScope.Find<NotificationCenterReveal>(FooterRevealPart);
        _readCount = e.NameScope.Find<ArcContentSwap>(ReadCountPart);
        _clearRead = e.NameScope.Find<NotificationCenterButton>(ClearReadPart);
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_allView is not null)
        {
            _allView.Click += OnViewClick;
            _allView.SizeChanged += OnViewButtonSizeChanged;
        }
        if (_unreadView is not null)
        {
            _unreadView.Click += OnViewClick;
            _unreadView.SizeChanged += OnViewButtonSizeChanged;
        }
        if (_markAll is not null)
        {
            _markAll.Click += OnMarkAllClick;
        }
        if (_viewAll is not null)
        {
            _viewAll.Click += OnViewAllClick;
        }
        if (_clearRead is not null)
        {
            _clearRead.Click += OnClearReadClick;
        }
        if (_list is not null)
        {
            AutomationProperties.SetControlTypeOverride(_list, AutomationControlType.List);
            _list.Children.Clear();
        }
        _rows.Clear();
        _shownUnread = -1;
        _shownRead = -1;
        _shownView = null;
        Sync(opening: true);
    }

    private void Detach()
    {
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_allView is not null)
        {
            _allView.Click -= OnViewClick;
            _allView.SizeChanged -= OnViewButtonSizeChanged;
        }
        if (_unreadView is not null)
        {
            _unreadView.Click -= OnViewClick;
            _unreadView.SizeChanged -= OnViewButtonSizeChanged;
        }
        if (_markAll is not null)
        {
            _markAll.Click -= OnMarkAllClick;
        }
        if (_viewAll is not null)
        {
            _viewAll.Click -= OnViewAllClick;
        }
        if (_clearRead is not null)
        {
            _clearRead.Click -= OnClearReadClick;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.CloseFromPanel(_close?.IsKeyboardActivation == true);
    }

    private void OnViewClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.SelectView(ReferenceEquals(sender, _unreadView) ? NotificationCenterView.Unread : NotificationCenterView.All);
    }

    private void OnMarkAllClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.MarkAllRead(fromPanel: true, _markAll?.IsKeyboardActivation == true);
    }

    private void OnViewAllClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.SelectView(NotificationCenterView.All);
    }

    private void OnClearReadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.ClearRead(fromPanel: true, _clearRead?.IsKeyboardActivation == true);
    }

    private void OnViewButtonSizeChanged(object? sender, SizeChangedEventArgs e) => MoveHighlight(animate: false);

    /// <summary>
    /// Shows the owner's current state. <paramref name="opening"/> (the panel mounting) lands every part without
    /// motion except the rows' staggered rise, like Arc's <c>initial={false}</c> presences.
    /// </summary>
    public void Sync(bool opening)
    {
        if (_list is null)
        {
            return;
        }
        var owner = _owner;
        var labels = owner.Labels;
        var animate = !opening;
        var unread = owner.UnreadCount;
        var read = owner.ReadCount;
        var view = owner.View;

        Heading = owner.Label;
        AutomationProperties.SetName(this, owner.Label);
        AllText = labels.All;
        UnreadText = labels.Unread;
        MarkAllText = labels.MarkAllRead;
        ViewAllText = labels.ViewAll;
        ClearReadText = labels.ClearRead;
        var format = labels.ReadCount;
        var slot = format.IndexOf("{0}", StringComparison.Ordinal);
        ReadPrefix = slot < 0 ? string.Empty : format[..slot];
        ReadSuffix = slot < 0 ? " " + format : format[(slot + 3)..];

        // Counts roll like an odometer: a higher number rises from below, a lower one drops from above.
        Roll(_count, unread, ref _shownUnread, animate);
        Swap(_summary, labels.Summary(unread), animate);
        if (read > 0 || _shownRead < 0)
        {
            Roll(_readCount, read, ref _shownRead, animate);
        }

        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
        }
        if (_viewSwitch is not null)
        {
            AutomationProperties.SetName(_viewSwitch, labels.ViewSwitch);
        }
        if (_allView is not null)
        {
            _allView.IsOn = view == NotificationCenterView.All;
        }
        if (_unreadView is not null)
        {
            _unreadView.IsOn = view == NotificationCenterView.Unread;
        }
        if (_markAll is not null)
        {
            AutomationProperties.SetName(_markAll, labels.MarkAllRead);
        }
        if (_shownView != view)
        {
            var glide = animate && _shownView is not null;
            _shownView = view;
            Dispatcher.UIThread.Post(() => MoveHighlight(glide), DispatcherPriority.Loaded);
        }
        AutomationProperties.SetName(_list, view == NotificationCenterView.All ? labels.AllList : labels.UnreadList);

        SetPresence(_markAllReveal, unread > 0, opening);
        SetPresence(_footerReveal, read > 0, opening);

        var visible = owner.VisibleItems;
        SyncRows(visible, opening, owner.IsBulk);

        Swap(_emptyTitle, view == NotificationCenterView.Unread ? labels.UnreadEmptyTitle : labels.EmptyTitle, animate);
        Swap(_emptyDescription, view == NotificationCenterView.Unread ? labels.UnreadEmptyDescription : labels.EmptyDescription, animate);
        IsViewAllVisible = view == NotificationCenterView.Unread && owner.ItemCount > 0;
        if (_emptyReveal is not null)
        {
            var empty = visible.Count == 0;
            if (opening)
            {
                _emptyReveal.Set(empty);
            }
            else if (empty)
            {
                // The empty state opens on the spring the rows close on; after a bulk action it waits for the cascade
                // to get going, so the panel never grows before it shrinks.
                _emptyReveal.Show(animate: true, heightDelay: owner.IsBulk ? Cascade(3) : TimeSpan.Zero,
                    fadeDelay: Motion.ArcMotionPresets.Fast, rise: 6, riseDelay: Motion.ArcMotionPresets.Fast);
            }
            else
            {
                _emptyReveal.Hide(animate: true);
            }
        }
    }

    private static void SetPresence(NotificationCenterReveal? reveal, bool shown, bool opening)
    {
        if (reveal is null)
        {
            return;
        }
        if (opening)
        {
            reveal.Set(shown);
        }
        else if (shown)
        {
            reveal.Show(animate: true);
        }
        else
        {
            reveal.Hide(animate: true);
        }
    }

    private static void Roll(ArcContentSwap? swap, int value, ref int shown, bool animate)
    {
        if (swap is null || value == shown)
        {
            return;
        }
        swap.Direction = value > shown ? 1 : -1;
        var previous = shown;
        shown = value;
        Swap(swap, value.ToString(System.Globalization.CultureInfo.CurrentCulture), animate && previous >= 0);
    }

    private static void Swap(ArcContentSwap? swap, string text, bool animate)
    {
        if (swap is null || Equals(swap.Content, text))
        {
            return;
        }
        if (animate)
        {
            swap.Content = text;
            return;
        }
        var mode = swap.Mode;
        swap.Mode = ContentSwapMode.None;
        swap.Content = text;
        swap.Mode = mode;
    }

    #region Rows

    private sealed class RowEntry
    {
        public required object Key { get; init; }
        public required NotificationCenterReveal Host { get; init; }
        public required NotificationCenterRow Row { get; init; }
        public int Index { get; set; }
    }

    /// <summary>Rows are matched by <see cref="NotificationItem.Id"/>; items without one (or a repeated one) by reference.</summary>
    private static object KeyOf(NotificationItem item, HashSet<object> used)
    {
        if (!string.IsNullOrEmpty(item.Id) && !used.Contains(item.Id))
        {
            return item.Id;
        }
        return item;
    }

    private void SyncRows(IReadOnlyList<NotificationItem> visible, bool opening, bool bulk)
    {
        var list = _list!;
        var used = new HashSet<object>();
        var shown = new List<Control>(visible.Count);
        for (var i = 0; i < visible.Count; i++)
        {
            var item = visible[i];
            var key = KeyOf(item, used);
            used.Add(key);
            var expanded = Equals(_owner.ExpandedKey, key);
            if (_rows.TryGetValue(key, out var entry))
            {
                // A row present before keeps its element (no rise); a leaving one turns back.
                entry.Row.Update(item, expanded, !opening, bulk ? Cascade(i) : TimeSpan.Zero);
                if (opening)
                {
                    // The panel mounts its rows again each time it opens: they settle in with the stagger.
                    entry.Host.Set(true);
                    entry.Row.PlayRise(Stagger(i));
                }
                else if (!entry.Host.IsShown)
                {
                    entry.Host.Show(animate: true, fadeIn: false);
                }
            }
            else
            {
                entry = CreateRow(key, item);
                _rows[key] = entry;
                // Mounted in place (the list order is fixed below), then brought into its first state without motion.
                list.Children.Insert(Math.Min(i, list.Children.Count), entry.Host);
                entry.Row.SetNarrow(_narrow);
                entry.Row.ApplyTemplate();
                entry.Row.Update(item, expanded, animate: false, TimeSpan.Zero);
                if (opening)
                {
                    entry.Host.Set(true);
                }
                else
                {
                    entry.Host.Show(animate: true, fadeIn: false);
                }
                entry.Row.PlayRise(Stagger(i));
            }
            entry.Index = i;
            shown.Add(entry.Host);
        }

        foreach (var entry in _rows.Values.ToList())
        {
            if (!used.Contains(entry.Key) && entry.Host.IsShown)
            {
                // Rows close their own height on the way out, so the list and the panel close the gap together.
                entry.Host.Hide(!opening, bulk ? Cascade(entry.Index) : TimeSpan.Zero);
            }
        }

        // AnimatePresence order: the shown rows in their new order, leaving rows kept where they were.
        _order.Clear();
        _order.AddRange(shown);
        var children = list.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is NotificationCenterReveal { IsShown: false } leaving && leaving.IsLeaving)
            {
                _order.Insert(Math.Min(i, _order.Count), leaving);
            }
        }
        for (var i = 0; i < _order.Count; i++)
        {
            var control = _order[i];
            var at = children.IndexOf(control);
            if (at == i)
            {
                continue;
            }
            if (at >= 0)
            {
                children.Move(at, i);
            }
            else
            {
                children.Insert(i, control);
            }
        }
        while (children.Count > _order.Count)
        {
            RemoveHost(children[^1]);
        }
    }

    private RowEntry CreateRow(object key, NotificationItem item)
    {
        var row = new NotificationCenterRow(_owner, item);
        var host = new NotificationCenterReveal { Child = row, ClipToBounds = true };
        host.Set(false);
        var entry = new RowEntry { Key = key, Host = host, Row = row };
        host.Hidden += (_, _) => OnRowHidden(entry);
        return entry;
    }

    private void OnRowHidden(RowEntry entry)
    {
        if (entry.Host.IsShown)
        {
            return;
        }
        if (_rows.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry))
        {
            _rows.Remove(entry.Key);
        }
        RemoveHost(entry.Host);
    }

    private void RemoveHost(Control host)
    {
        _list?.Children.Remove(host);
        if (host is NotificationCenterReveal { Child: NotificationCenterRow row })
        {
            foreach (var pair in _rows.Where(p => ReferenceEquals(p.Value.Row, row)).ToList())
            {
                _rows.Remove(pair.Key);
            }
        }
    }

    /// <summary>The shown row of <paramref name="item"/>, if any.</summary>
    internal NotificationCenterRow? RowOf(NotificationItem item) =>
        _rows.Values.FirstOrDefault(r => ReferenceEquals(r.Row.Item, item) && r.Host.IsShown)?.Row;

    #endregion

    #region Highlight, layout and focus

    /// <summary>The All / Unread highlight glides on the morph spring (Arc's shared layoutId).</summary>
    private void MoveHighlight(bool animate)
    {
        if (_viewHighlight is null || _viewSwitch is null)
        {
            return;
        }
        var button = _owner.View == NotificationCenterView.Unread ? _unreadView : _allView;
        if (button is null || button.Bounds.Width <= 0 || button.TranslatePoint(default, _viewSwitch) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, button.Bounds.Size);
        if (!animate || !_viewHighlight.HasTarget)
        {
            _viewHighlight.Hide(animate: false);
            _viewHighlight.MoveTo(rect, animate: false);
            return;
        }
        _viewHighlight.MoveTo(rect, animate: true, Motion.ArcSpring.Morph);
    }

    /// <summary>
    /// Fits the panel to the window: min(424, width − 24) wide, min(590, height − 24) tall, narrow at ≤ 380. Given the
    /// bell's rectangle, the height is also capped at the larger free space above or below it (12px from the window
    /// edge), so in a short window the panel flips above the bell instead of sliding over it.
    /// </summary>
    public void FitTo(Size client, Rect? anchor = null, double gap = 0)
    {
        Width = Math.Max(0, Math.Min(MaxPanelWidth, client.Width - ViewportInset));
        var height = Math.Min(MaxPanelHeight, client.Height - ViewportInset);
        if (anchor is { } bell)
        {
            var edge = ViewportInset / 2;
            var below = client.Height - bell.Bottom - gap - edge;
            var above = bell.Top - gap - edge;
            height = Math.Min(height, Math.Max(below, above));
        }
        MaxHeight = Math.Max(0, height);
        var narrow = client.Width <= NarrowBreakpoint;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            foreach (var entry in _rows.Values)
            {
                entry.Row.SetNarrow(narrow);
            }
        }
    }

    /// <summary>Whether keyboard focus is inside the panel.</summary>
    public bool IsFocusWithin =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused &&
        (ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused));

    /// <summary>Focuses the close button, the first stop in the panel (Radix focuses the first tabbable element).</summary>
    public void FocusFirst(NavigationMethod method) => _close?.Focus(method);

    /// <summary>Focuses the button of <paramref name="view"/>.</summary>
    public void FocusView(NotificationCenterView view, NavigationMethod method) =>
        (view == NotificationCenterView.Unread ? _unreadView : _allView)?.Focus(method);

    /// <summary>Focuses the title button of <paramref name="item"/>'s row.</summary>
    public bool FocusItem(NotificationItem item, NavigationMethod method) => RowOf(item)?.FocusToggle(method) == true;

    #endregion
}
