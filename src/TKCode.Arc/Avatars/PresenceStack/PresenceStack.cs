using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Who is looking at this page right now: the avatars of the current viewers, overlapping, each in a ring of their own
/// color, the most recent first, with a "+N" chip for the rest and a rolling "N viewing" count. It follows a live list:
/// a newcomer grows in at the front and sends out one ring while the others slide over, someone pushed past
/// <see cref="MaxVisibleCount"/> slides into the chip, someone who leaves shrinks away and the gap closes. Idle viewers
/// fade. The chip opens the full list. Joins and leaves are announced politely to screen readers.
/// </summary>
[TemplatePart(PanelPart, typeof(PresenceStackPanel))]
[TemplatePart(CountPart, typeof(RollingDigits))]
[TemplatePart(ListPopupPart, typeof(Popup))]
[TemplatePart(AnnouncerPart, typeof(AvatarsLiveRegion))]
[PseudoClasses(EmptyPseudoClass, OverflowingPseudoClass)]
public class PresenceStack : TemplatedControl
{
    /// <summary>The overlapping row of avatars and the chip.</summary>
    public const string PanelPart = "PART_Panel";

    /// <summary>The rolling count of viewers.</summary>
    public const string CountPart = "PART_Count";

    /// <summary>The popup with everyone viewing.</summary>
    public const string ListPopupPart = "PART_ListPopup";

    /// <summary>The live region for joins and leaves.</summary>
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while nobody is viewing.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while some viewers are folded into the chip.</summary>
    public const string OverflowingPseudoClass = ":overflowing";

    /// <summary>The people viewing from a view model, oldest first; when set it replaces <see cref="Viewers"/>.</summary>
    public static readonly StyledProperty<IEnumerable<PresenceStackViewer>?> ItemsSourceProperty =
        AvaloniaProperty.Register<PresenceStack, IEnumerable<PresenceStackViewer>?>(nameof(ItemsSource));

    /// <summary>The most avatars shown before the rest fold into the "+N" chip (1 to 12, default 4).</summary>
    public static readonly StyledProperty<int> MaxVisibleCountProperty =
        AvaloniaProperty.Register<PresenceStack, int>(nameof(MaxVisibleCount), 4, coerce: static (_, v) => Math.Clamp(v, 1, 12));

    /// <summary>The avatar size: Small (28, the default) or Medium (36); larger sizes are shown as Medium.</summary>
    public static readonly StyledProperty<AvatarSize> SizeProperty =
        AvaloniaProperty.Register<PresenceStack, AvatarSize>(nameof(Size), AvatarSize.Small);

    /// <summary>Shows the most recent viewer first, on top (default true); false keeps the list's order.</summary>
    public static readonly StyledProperty<bool> IsNewestFirstProperty =
        AvaloniaProperty.Register<PresenceStack, bool>(nameof(IsNewestFirst), true);

    /// <summary>Shows the rolling count and <see cref="CountLabel"/> after the avatars (default true).</summary>
    public static readonly StyledProperty<bool> IsCountVisibleProperty =
        AvaloniaProperty.Register<PresenceStack, bool>(nameof(IsCountVisible), true);

    /// <summary>The words after the count (default "viewing").</summary>
    public static readonly StyledProperty<string> CountLabelProperty =
        AvaloniaProperty.Register<PresenceStack, string>(nameof(CountLabel), "viewing");

    /// <summary>The accessible name of the stack (default "Viewing now").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PresenceStack, string>(nameof(Label), "Viewing now");

    /// <summary>The word for an idle viewer in tooltips, names and the list (default "Idle").</summary>
    public static readonly StyledProperty<string> IdleTextProperty =
        AvaloniaProperty.Register<PresenceStack, string>(nameof(IdleText), "Idle");

    /// <summary>Says "Name joined" and "Name left" through a polite live region (default true).</summary>
    public static readonly StyledProperty<bool> AnnouncesChangesProperty =
        AvaloniaProperty.Register<PresenceStack, bool>(nameof(AnnouncesChanges), true);

    /// <summary>True while the list of everyone viewing is open; the chip toggles it, Escape and a click outside close it.</summary>
    public static readonly StyledProperty<bool> IsListOpenProperty =
        AvaloniaProperty.Register<PresenceStack, bool>(nameof(IsListOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The number of people viewing.</summary>
    public static readonly DirectProperty<PresenceStack, int> ViewerCountProperty =
        AvaloniaProperty.RegisterDirect<PresenceStack, int>(nameof(ViewerCount), o => o.ViewerCount);

    private readonly List<PresenceStackViewer> _order = new();
    private readonly HashSet<PresenceStackViewer> _known = new();
    private readonly HashSet<PresenceStackViewer> _joined = new();
    private readonly Dictionary<PresenceStackViewer, string?> _names = new();
    private readonly List<PresenceStackViewer> _watched = new();
    private INotifyCollectionChanged? _observedSource;
    private PresenceStackPanel? _panel;
    private RollingDigits? _count;
    private Popup? _popup;
    private AvatarsLiveRegion? _announcer;
    private StackPanel? _list;
    private int _viewerCount;
    private bool _attached;
    private bool _primed;
    private bool _syncPending;

    static PresenceStack()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<PresenceStack>(AutomationControlType.List);
    }

    /// <summary>Creates an empty stack.</summary>
    public PresenceStack()
    {
        Viewers.CollectionChanged += (_, _) =>
        {
            if (ItemsSource is null)
            {
                ScheduleSync();
            }
        };
        UpdateName();
    }

    /// <summary>
    /// The people viewing, oldest first (the XAML content): add a newcomer at the end, remove whoever leaves. Ignored while
    /// <see cref="ItemsSource"/> is set.
    /// </summary>
    [Content]
    public AvaloniaList<PresenceStackViewer> Viewers { get; } = new();

    /// <summary>The people viewing from a view model, oldest first; when set it replaces <see cref="Viewers"/>.</summary>
    public IEnumerable<PresenceStackViewer>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>The most avatars shown before the rest fold into the "+N" chip (1 to 12, default 4).</summary>
    public int MaxVisibleCount { get => GetValue(MaxVisibleCountProperty); set => SetValue(MaxVisibleCountProperty, value); }

    /// <summary>The avatar size: Small (28, the default) or Medium (36); larger sizes are shown as Medium.</summary>
    public AvatarSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <summary>Shows the most recent viewer first, on top (default true); false keeps the list's order.</summary>
    public bool IsNewestFirst { get => GetValue(IsNewestFirstProperty); set => SetValue(IsNewestFirstProperty, value); }

    /// <summary>Shows the rolling count and <see cref="CountLabel"/> after the avatars (default true).</summary>
    public bool IsCountVisible { get => GetValue(IsCountVisibleProperty); set => SetValue(IsCountVisibleProperty, value); }

    /// <summary>The words after the count (default "viewing").</summary>
    public string CountLabel { get => GetValue(CountLabelProperty); set => SetValue(CountLabelProperty, value); }

    /// <summary>The accessible name of the stack (default "Viewing now").</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The word for an idle viewer in tooltips, names and the list (default "Idle").</summary>
    public string IdleText { get => GetValue(IdleTextProperty); set => SetValue(IdleTextProperty, value); }

    /// <summary>Says "Name joined" and "Name left" through a polite live region (default true).</summary>
    public bool AnnouncesChanges { get => GetValue(AnnouncesChangesProperty); set => SetValue(AnnouncesChangesProperty, value); }

    /// <summary>True while the list of everyone viewing is open; the chip toggles it, Escape and a click outside close it.</summary>
    public bool IsListOpen { get => GetValue(IsListOpenProperty); set => SetValue(IsListOpenProperty, value); }

    /// <summary>The number of people viewing.</summary>
    public int ViewerCount { get => _viewerCount; private set => SetAndRaise(ViewerCountProperty, ref _viewerCount, value); }

    /// <summary>Opens the list of everyone viewing.</summary>
    public void OpenList() => SetCurrentValue(IsListOpenProperty, true);

    /// <summary>Closes the list.</summary>
    public void CloseList() => SetCurrentValue(IsListOpenProperty, false);

    /// <summary>The people in the order shown (first on top).</summary>
    internal IReadOnlyList<PresenceStackViewer> Order => _order;

    /// <summary>The row (tests).</summary>
    internal PresenceStackPanel? Panel => _panel;

    /// <summary>The live region (tests).</summary>
    internal AvatarsLiveRegion? Announcer => _announcer;

    /// <summary>The list's rows while it is open (tests).</summary>
    internal StackPanel? ListPanel => _list;

    /// <summary>The words for one person: name, detail, "idle".</summary>
    internal static string Describe(PresenceStackViewer viewer, string idle = "Idle")
    {
        var text = viewer.Name ?? string.Empty;
        if (viewer.IsIdle)
        {
            text += ", " + idle.ToLowerInvariant();
        }
        if (!string.IsNullOrEmpty(viewer.Detail))
        {
            text += ", " + viewer.Detail;
        }
        return text;
    }

    /// <summary>A stable series color index for a name, so a person keeps the same ring color.</summary>
    internal static int SeriesIndexOf(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return 0;
        }
        // FNV-1a over the characters: stable across runs (string.GetHashCode is not).
        uint hash = 2166136261;
        foreach (var c in name)
        {
            hash = (hash ^ c) * 16777619;
        }
        return (int)(hash % 4);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.Overflow.Click -= OnOverflowClick;
        }
        if (_popup is not null)
        {
            _popup.Closed -= OnPopupClosed;
        }
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<PresenceStackPanel>(PanelPart);
        _count = e.NameScope.Find<RollingDigits>(CountPart);
        _popup = e.NameScope.Find<Popup>(ListPopupPart);
        _announcer = e.NameScope.Find<AvatarsLiveRegion>(AnnouncerPart);
        if (_panel is not null)
        {
            _panel.Overflow.Click += OnOverflowClick;
        }
        if (_popup is not null)
        {
            _popup.Closed += OnPopupClosed;
            _popup.PlacementTarget = _panel?.Overflow;
        }
        _primed = false;
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveSource(ItemsSource);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveSource(null);
        Watch(Array.Empty<PresenceStackViewer>());
        CloseList();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            ObserveSource(_attached ? ItemsSource : null);
            ScheduleSync();
        }
        else if (change.Property == MaxVisibleCountProperty || change.Property == IsNewestFirstProperty)
        {
            Sync();
        }
        else if (change.Property == SizeProperty)
        {
            Sync();
            _panel?.Relayout();
        }
        else if (change.Property == LabelProperty || change.Property == CountLabelProperty)
        {
            UpdateName();
        }
        else if (change.Property == IsListOpenProperty)
        {
            UpdateList();
        }
    }

    private void ObserveSource(IEnumerable<PresenceStackViewer>? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleSync();

    private void ScheduleSync()
    {
        if (_syncPending)
        {
            return;
        }
        // Coalesce: a list that changes several people at once moves once.
        _syncPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _syncPending = false;
            Sync();
        }, DispatcherPriority.Background);
    }

    private void Watch(IReadOnlyCollection<PresenceStackViewer> viewers)
    {
        foreach (var viewer in _watched)
        {
            viewer.PropertyChanged -= OnViewerChanged;
        }
        _watched.Clear();
        foreach (var viewer in viewers)
        {
            viewer.PropertyChanged += OnViewerChanged;
            _watched.Add(viewer);
        }
    }

    private void OnViewerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (IsListOpen)
        {
            UpdateList();
        }
        UpdateName();
    }

    /// <summary>Applies the current list: who joined, who left, who is visible, the chip and the count.</summary>
    internal void Sync()
    {
        _syncPending = false;
        var source = (ItemsSource ?? Viewers).Where(v => v is not null).Distinct().ToList();
        if (IsNewestFirst)
        {
            source.Reverse();
        }
        var animate = _primed && _attached && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        _joined.Clear();
        var left = new List<PresenceStackViewer>();
        if (_primed)
        {
            foreach (var viewer in source)
            {
                if (!_known.Contains(viewer))
                {
                    _joined.Add(viewer);
                }
            }
            foreach (var viewer in _known)
            {
                if (!source.Contains(viewer))
                {
                    left.Add(viewer);
                }
            }
        }
        _known.Clear();
        foreach (var viewer in source)
        {
            _known.Add(viewer);
            _names[viewer] = viewer.Name;
        }
        _order.Clear();
        _order.AddRange(source);
        if (_attached)
        {
            Watch(_order);
        }
        ViewerCount = _order.Count;
        var size = Size is AvatarSize.Small ? AvatarSize.Small : AvatarSize.Medium;
        var hidden = Math.Max(0, _order.Count - MaxVisibleCount);
        _panel?.Sync(_order, MaxVisibleCount, size, _joined, animate, OverflowName(hidden));
        if (_count is not null)
        {
            _count.IsInstant = !animate;
            _count.Value = _order.Count;
        }
        PseudoClasses.Set(EmptyPseudoClass, _order.Count == 0);
        PseudoClasses.Set(OverflowingPseudoClass, hidden > 0);
        if (_primed && AnnouncesChanges)
        {
            Announce(_joined, left);
        }
        foreach (var viewer in left)
        {
            _names.Remove(viewer);
        }
        if (IsListOpen)
        {
            UpdateList();
        }
        UpdateName();
        _primed = _primed || (_attached && _panel is not null);
    }

    private void Announce(HashSet<PresenceStackViewer> joined, List<PresenceStackViewer> left)
    {
        if (_announcer is null || (joined.Count == 0 && left.Count == 0))
        {
            return;
        }
        var parts = new List<string>();
        foreach (var viewer in _order)
        {
            if (joined.Contains(viewer))
            {
                parts.Add((viewer.Name ?? string.Empty) + " joined");
            }
        }
        foreach (var viewer in left)
        {
            parts.Add((_names.GetValueOrDefault(viewer) ?? viewer.Name ?? string.Empty) + " left");
        }
        _announcer.Announce(string.Join(", ", parts));
    }

    private static string OverflowName(int hidden) => hidden == 1 ? "1 more viewer" : $"{hidden} more viewers";

    private void UpdateName()
    {
        var count = _order.Count;
        AutomationProperties.SetName(this, $"{Label}, {count} {CountLabel}");
    }

    private void OnOverflowClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsListOpenProperty, !IsListOpen);

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (IsListOpen)
        {
            SetCurrentValue(IsListOpenProperty, false);
        }
    }

    private void UpdateList()
    {
        if (_popup is null)
        {
            return;
        }
        if (!IsListOpen)
        {
            _popup.IsOpen = false;
            return;
        }
        if (_popup.Child is null)
        {
            _list = new StackPanel { Spacing = 10 };
            AutomationProperties.SetName(_list, Label);
            var scroll = new ScrollViewer { Content = _list, MaxHeight = 320 };
            var surface = new Border
            {
                Child = scroll,
                Padding = new Thickness(14, 12),
                MinWidth = 220,
                MaxWidth = 320
            }.BindCornerRadius(ArcDesignTokenKind.RadiusStep18);
            ArcTokens.Bind(surface, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
            ArcTokens.Bind(surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(surface, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
            surface.KeyDown += OnListKeyDown;
            _popup.Child = surface;
        }
        if (_list is not null)
        {
            _list.Children.Clear();
            foreach (var viewer in _order)
            {
                _list.Children.Add(new PresenceStackListRow(viewer, IdleText));
            }
        }
        _popup.PlacementTarget = _panel?.Overflow is { IsVisible: true } chip ? chip : (Control?)_panel ?? this;
        _popup.IsOpen = true;
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseList();
            _panel?.Overflow.Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsListOpen)
        {
            CloseList();
            e.Handled = true;
        }
    }
}
