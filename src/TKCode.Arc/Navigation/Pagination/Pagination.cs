using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Raised when <see cref="Pagination.CurrentPage"/> changes.</summary>
public sealed class PaginationPageChangedEventArgs : RoutedEventArgs
{
    public PaginationPageChangedEventArgs(RoutedEvent routedEvent, int oldPage, int newPage) : base(routedEvent)
    {
        OldPage = oldPage;
        NewPage = newPage;
    }

    /// <summary>The page before the change.</summary>
    public int OldPage { get; }

    /// <summary>The page now shown (1-based).</summary>
    public int NewPage { get; }
}

/// <summary>
/// Arc pagination: previous and next buttons around a sliding window of five page buttons and one current-page
/// mark that travels between them on a morph spring. When the window moves, the numbers travel like a belt by
/// the number of slots it moved (smooth spring), entering numbers fade in and leaving ones fade out quickly.
/// <see cref="CurrentPage"/> is 1-based, two-way and clamped to <c>1..PageCount</c>; with
/// <see cref="ShowBoundaryPages"/> the first and last pages stay reachable behind an ellipsis.
/// </summary>
[TemplatePart(PanelPart, typeof(Panel))]
[TemplatePart(MarkPart, typeof(GlidingIndicator))]
[TemplatePart(PreviousPart, typeof(AvaloniaButton))]
[TemplatePart(NextPart, typeof(AvaloniaButton))]
[TemplatePart(FirstPart, typeof(AvaloniaButton))]
[TemplatePart(LastPart, typeof(AvaloniaButton))]
[TemplatePart(EllipsisStartPart, typeof(Control))]
[TemplatePart(EllipsisEndPart, typeof(Control))]
public class Pagination : TemplatedControl
{
    public const string PanelPart = "PART_Panel";
    public const string MarkPart = "PART_Mark";
    public const string PreviousPart = "PART_Previous";
    public const string NextPart = "PART_Next";
    public const string FirstPart = "PART_First";
    public const string LastPart = "PART_Last";
    public const string EllipsisStartPart = "PART_EllipsisStart";
    public const string EllipsisEndPart = "PART_EllipsisEnd";

    /// <summary>Number of page buttons in the sliding window.</summary>
    public const int WindowSize = 5;

    private static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    #region Properties

    /// <summary>The current page, 1-based. Two-way; values outside <c>1..PageCount</c> are clamped for display.</summary>
    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(CurrentPage), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Number of pages; 0 renders an empty navigation landmark.</summary>
    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(PageCount));

    /// <summary>Keeps the first and last pages visible behind an ellipsis when the window does not reach them (not in Arc).</summary>
    public static readonly StyledProperty<bool> ShowBoundaryPagesProperty =
        AvaloniaProperty.Register<Pagination, bool>(nameof(ShowBoundaryPages));

    /// <summary>Accessible name of the navigation landmark.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Pagination, string>(nameof(Label), "Pagination");

    public static readonly StyledProperty<string> PreviousLabelProperty =
        AvaloniaProperty.Register<Pagination, string>(nameof(PreviousLabel), "Previous page");

    public static readonly StyledProperty<string> NextLabelProperty =
        AvaloniaProperty.Register<Pagination, string>(nameof(NextLabel), "Next page");

    /// <summary>Format of a page button's accessible name; <c>{0}</c> is the page number.</summary>
    public static readonly StyledProperty<string> PageLabelFormatProperty =
        AvaloniaProperty.Register<Pagination, string>(nameof(PageLabelFormat), "Page {0}");

    public static readonly RoutedEvent<PaginationPageChangedEventArgs> PageChangedEvent =
        RoutedEvent.Register<Pagination, PaginationPageChangedEventArgs>(nameof(PageChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="CurrentPageProperty"/>
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }

    /// <inheritdoc cref="PageCountProperty"/>
    public int PageCount { get => GetValue(PageCountProperty); set => SetValue(PageCountProperty, value); }

    /// <inheritdoc cref="ShowBoundaryPagesProperty"/>
    public bool ShowBoundaryPages { get => GetValue(ShowBoundaryPagesProperty); set => SetValue(ShowBoundaryPagesProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Accessible name of the previous button.</summary>
    public string PreviousLabel { get => GetValue(PreviousLabelProperty); set => SetValue(PreviousLabelProperty, value); }

    /// <summary>Accessible name of the next button.</summary>
    public string NextLabel { get => GetValue(NextLabelProperty); set => SetValue(NextLabelProperty, value); }

    /// <inheritdoc cref="PageLabelFormatProperty"/>
    public string PageLabelFormat { get => GetValue(PageLabelFormatProperty); set => SetValue(PageLabelFormatProperty, value); }

    /// <summary>Raised after the user (or code) changes the page.</summary>
    public event EventHandler<PaginationPageChangedEventArgs>? PageChanged
    {
        add => AddHandler(PageChangedEvent, value);
        remove => RemoveHandler(PageChangedEvent, value);
    }

    #endregion

    private PaginationPanel? _panel;
    private GlidingIndicator? _mark;
    private AvaloniaButton? _previous;
    private AvaloniaButton? _next;
    private AvaloniaButton? _first;
    private AvaloniaButton? _last;
    private Control? _ellipsisStart;
    private Control? _ellipsisEnd;
    private readonly PaginationButton?[] _slots = new PaginationButton?[WindowSize];
    private int _start = 1;
    private int _shown;
    private Rect _markTarget;

    static Pagination()
    {
        FocusableProperty.OverrideDefaultValue<Pagination>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<Pagination>(AutomationLandmarkType.Navigation);
    }

    public Pagination()
    {
        FontFeatures = TabularNumbers;
        AutomationProperties.SetName(this, Label);
    }

    /// <summary>The page the buttons show: <see cref="CurrentPage"/> clamped to the valid range (0 without pages).</summary>
    public int EffectivePage => PageCount > 0 ? Math.Clamp(CurrentPage, 1, PageCount) : 0;

    /// <summary>First page of the five-page window.</summary>
    public int WindowStart => _start;

    /// <summary>Goes to <paramref name="page"/> (clamped). Returns false when nothing changed.</summary>
    public bool GoToPage(int page)
    {
        if (PageCount <= 0)
        {
            return false;
        }
        var target = Math.Clamp(page, 1, PageCount);
        if (target == EffectivePage && target == CurrentPage)
        {
            return false;
        }
        CurrentPage = target;
        return true;
    }

    /// <summary>Goes to the next page when there is one.</summary>
    public bool NextPage() => EffectivePage < PageCount && GoToPage(EffectivePage + 1);

    /// <summary>Goes to the previous page when there is one.</summary>
    public bool PreviousPage() => EffectivePage > 1 && GoToPage(EffectivePage - 1);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Unhook();
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<PaginationPanel>(PanelPart);
        _mark = e.NameScope.Find<GlidingIndicator>(MarkPart);
        _previous = e.NameScope.Find<AvaloniaButton>(PreviousPart);
        _next = e.NameScope.Find<AvaloniaButton>(NextPart);
        _first = e.NameScope.Find<AvaloniaButton>(FirstPart);
        _last = e.NameScope.Find<AvaloniaButton>(LastPart);
        _ellipsisStart = e.NameScope.Find<Control>(EllipsisStartPart);
        _ellipsisEnd = e.NameScope.Find<Control>(EllipsisEndPart);
        for (var i = 0; i < WindowSize; i++)
        {
            _slots[i] = e.NameScope.Find<PaginationButton>("PART_Page" + i);
        }
        Hook();
        _markTarget = default;
        _shown = 0;
        Update(animate: false);
    }

    private void Hook()
    {
        if (_panel is not null)
        {
            _panel.Arranged = OnPanelArranged;
            _panel.Slots = _slots;
        }
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
        }
        if (_first is not null)
        {
            _first.Click += OnPageClick;
        }
        if (_last is not null)
        {
            _last.Click += OnPageClick;
        }
        foreach (var slot in _slots)
        {
            if (slot is not null)
            {
                slot.Click += OnPageClick;
            }
        }
    }

    private void Unhook()
    {
        if (_panel is not null)
        {
            _panel.Arranged = null;
        }
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        if (_first is not null)
        {
            _first.Click -= OnPageClick;
        }
        if (_last is not null)
        {
            _last.Click -= OnPageClick;
        }
        foreach (var slot in _slots)
        {
            if (slot is not null)
            {
                slot.Click -= OnPageClick;
            }
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => PreviousPage();

    private void OnNextClick(object? sender, RoutedEventArgs e) => NextPage();

    private void OnPageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is PaginationButton { Page: > 0 } button)
        {
            GoToPage(button.Page);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentPageProperty)
        {
            var oldPage = change.GetOldValue<int>();
            var newPage = change.GetNewValue<int>();
            Update(animate: true);
            RaiseEvent(new PaginationPageChangedEventArgs(PageChangedEvent, oldPage, newPage));
        }
        else if (change.Property == PageCountProperty || change.Property == ShowBoundaryPagesProperty ||
                 change.Property == PageLabelFormatProperty)
        {
            Update(animate: change.Property == PageCountProperty);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    private void Update(bool animate)
    {
        if (_panel is null)
        {
            return;
        }
        var count = Math.Max(0, PageCount);
        var current = EffectivePage;
        var start = count > 0 ? Math.Max(1, Math.Min(count - (WindowSize - 1), current - 2)) : 1;
        var visible = Math.Min(count, WindowSize);
        var oldStart = _start;
        var oldShown = _shown;
        var shift = start - oldStart;
        var focusedPage = FocusedSlotPage(out var focusVisible);

        _start = start;
        _shown = visible;
        for (var i = 0; i < WindowSize; i++)
        {
            if (_slots[i] is not { } slot)
            {
                continue;
            }
            var page = start + i;
            var show = i < visible;
            slot.IsVisible = show;
            ConfigurePage(slot, show ? page : 0, current);
        }
        var end = start + visible - 1;
        var boundaries = ShowBoundaryPages && count > WindowSize;
        if (_first is PaginationButton first)
        {
            first.IsVisible = boundaries && start > 1;
            ConfigurePage(first, first.IsVisible ? 1 : 0, current);
        }
        if (_last is PaginationButton last)
        {
            last.IsVisible = boundaries && end < count;
            ConfigurePage(last, last.IsVisible ? count : 0, current);
        }
        if (_ellipsisStart is not null)
        {
            _ellipsisStart.IsVisible = boundaries && start > 2;
        }
        if (_ellipsisEnd is not null)
        {
            _ellipsisEnd.IsVisible = boundaries && end < count - 1;
        }
        if (_previous is not null)
        {
            _previous.IsVisible = count > 0;
            _previous.IsEnabled = current > 1;
            AutomationProperties.SetName(_previous, PreviousLabel);
        }
        if (_next is not null)
        {
            _next.IsVisible = count > 0;
            _next.IsEnabled = current < count;
            AutomationProperties.SetName(_next, NextLabel);
        }

        if (animate && IsLoaded && shift != 0 && oldShown > 0 && visible > 0)
        {
            _panel.StartBelt(shift, oldStart, oldShown, start, visible);
        }
        if (focusedPage > 0)
        {
            RestoreFocus(focusedPage, focusVisible);
        }
        UpdateMark(animate && IsLoaded);
    }

    private void ConfigurePage(PaginationButton button, int page, int current)
    {
        button.Page = page;
        button.Content = page > 0 ? page.ToString(System.Globalization.CultureInfo.CurrentCulture) : null;
        button.IsCurrent = page > 0 && page == current;
        AutomationProperties.SetName(button, page > 0 ? string.Format(System.Globalization.CultureInfo.CurrentCulture, PageLabelFormat, page) : string.Empty);
        AutomationProperties.SetItemStatus(button, button.IsCurrent ? "current page" : string.Empty);
    }

    /// <summary>Keyboard focus follows the number it was on (Arc keys buttons by page number).</summary>
    private int FocusedSlotPage(out bool focusVisible)
    {
        focusVisible = false;
        foreach (var slot in _slots)
        {
            if (slot is { IsFocused: true })
            {
                focusVisible = slot.Classes.Contains(":focus-visible");
                return slot.Page;
            }
        }
        return 0;
    }

    private void RestoreFocus(int page, bool focusVisible)
    {
        foreach (var slot in _slots)
        {
            if (slot is { IsVisible: true } && slot.Page == page)
            {
                if (!slot.IsFocused)
                {
                    slot.Focus(focusVisible ? NavigationMethod.Tab : NavigationMethod.Pointer);
                }
                return;
            }
        }
    }

    /// <summary>Left / Right move focus between the buttons (in addition to Tab).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && _panel is not null && e.Source is PaginationButton source && (e.Key == Key.Left || e.Key == Key.Right))
        {
            var buttons = _panel.Children;
            var index = buttons.IndexOf(source);
            var step = e.Key == Key.Right ? 1 : -1;
            for (var i = index + step; index >= 0 && i >= 0 && i < buttons.Count; i += step)
            {
                if (buttons[i] is PaginationButton { IsVisible: true, IsEffectivelyEnabled: true } next)
                {
                    next.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    break;
                }
            }
        }
        base.OnKeyDown(e);
    }

    private void OnPanelArranged() => UpdateMark(animate: false, onlyWhenMoved: true);

    private void UpdateMark(bool animate, bool onlyWhenMoved = false)
    {
        if (_mark is null || _panel is null)
        {
            return;
        }
        Control? target = null;
        foreach (var slot in _slots)
        {
            if (slot is { IsVisible: true, IsCurrent: true })
            {
                target = slot;
            }
        }
        if (target is null && _first is PaginationButton { IsVisible: true, IsCurrent: true } first)
        {
            target = first;
        }
        if (target is null && _last is PaginationButton { IsVisible: true, IsCurrent: true } last)
        {
            target = last;
        }
        if (target is null || target.Bounds.Width <= 0)
        {
            _markTarget = default;
            _mark.Hide(animate: false);
            return;
        }
        // Layout bounds: the mark goes to the button's final slot, not where the belt currently draws it.
        var rect = target.Bounds.Deflate(0.5);
        if (onlyWhenMoved && _mark.HasTarget && Near(rect, _markTarget))
        {
            return;
        }
        _markTarget = rect;
        _mark.MoveTo(rect, animate);
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    protected override AutomationPeer OnCreateAutomationPeer() => new PaginationAutomationPeer(this);

    private sealed class PaginationAutomationPeer : ControlAutomationPeer
    {
        public PaginationAutomationPeer(Pagination owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}
