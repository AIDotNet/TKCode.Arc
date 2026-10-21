using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="SettingsPage.SaveRequested"/> and <see cref="SettingsPage.DiscardRequested"/>.</summary>
public sealed class SettingsSectionEventArgs : RoutedEventArgs
{
    public SettingsSectionEventArgs(RoutedEvent routedEvent, SettingsSection section) : base(routedEvent)
    {
        Section = section;
    }

    /// <summary>The section whose changes are to be saved or discarded.</summary>
    public SettingsSection Section { get; }
}

/// <summary>
/// A full settings page. The left column holds the page <see cref="Title"/>, one navigation item per
/// <see cref="SettingsSection"/> (icon, header, badge) under a raised selection pill that glides between them, and a
/// <see cref="Footer"/> such as an account card; the chosen section fills the rest of the page. While the chosen section
/// is <see cref="SettingsSection.IsDirty"/>, a save bar rises at the bottom with Discard and Save. Narrower than
/// <see cref="CompactWidth"/>, the page becomes one column with the sections as a scrolling row of pills.
/// <para>
/// Write the sections inline (<c>&lt;arc:SettingsSection Header="Billing" Icon="..."&gt;…&lt;/arc:SettingsSection&gt;</c>)
/// or bind them through <see cref="ItemsControl.ItemsSource"/>; the first enabled section is selected by default.
/// </para>
/// </summary>
[TemplatePart(NavScrollPart, typeof(ScrollArea))]
[TemplatePart(NavHostPart, typeof(Panel))]
[TemplatePart(SelectionPillPart, typeof(GlidingIndicator))]
[TemplatePart(ContentHostPart, typeof(Control))]
[TemplatePart(ContentScrollPart, typeof(ScrollViewer))]
[TemplatePart(SaveBarPart, typeof(PresenceHost))]
[TemplatePart(SaveNotePart, typeof(TextBlock))]
[TemplatePart(SaveButtonPart, typeof(AvaloniaButton))]
[TemplatePart(DiscardButtonPart, typeof(AvaloniaButton))]
[TemplatePart(FooterSlotPart, typeof(Control))]
[PseudoClasses(CompactPseudoClass, DirtyPseudoClass)]
public class SettingsPage : SelectingItemsControl
{
    public const string NavScrollPart = "PART_NavScroll";
    public const string NavHostPart = "PART_NavHost";
    public const string SelectionPillPart = "PART_SelectionPill";
    public const string ContentHostPart = "PART_ContentHost";
    public const string ContentScrollPart = "PART_ContentScroll";
    public const string SaveBarPart = "PART_SaveBar";
    public const string SaveNotePart = "PART_SaveNote";
    public const string SaveButtonPart = "PART_SaveButton";
    public const string DiscardButtonPart = "PART_DiscardButton";
    public const string FooterSlotPart = "PART_FooterSlot";

    /// <summary>Set while the page is narrower than <see cref="CompactWidth"/>.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the selected section has unsaved changes.</summary>
    public const string DirtyPseudoClass = ":dirty";

    #region Properties

    /// <summary>The page title above the navigation (default "Settings").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SettingsPage, string?>(nameof(Title), "Settings");

    /// <summary>Content at the bottom of the navigation column, such as an account card. Hidden on compact pages.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SettingsPage, object?>(nameof(Footer));

    /// <summary>Below this width the page becomes one column with the sections as a row of pills (default 720).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<SettingsPage, double>(nameof(CompactWidth), 720);

    /// <summary>The built-in text (save bar, accessible names). Replace the whole object to translate it.</summary>
    public static readonly StyledProperty<SettingsPageLabels> LabelsProperty =
        AvaloniaProperty.Register<SettingsPage, SettingsPageLabels>(nameof(Labels), new SettingsPageLabels());

    /// <summary>Whether the page is narrower than <see cref="CompactWidth"/> and shows one column.</summary>
    public static readonly DirectProperty<SettingsPage, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<SettingsPage, bool>(nameof(IsCompact), o => o.IsCompact);

    /// <summary>Raised when Save is pressed (or Ctrl/⌘+S) while the selected section has unsaved changes.</summary>
    public static readonly RoutedEvent<SettingsSectionEventArgs> SaveRequestedEvent =
        RoutedEvent.Register<SettingsPage, SettingsSectionEventArgs>(nameof(SaveRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Discard is pressed in the save bar.</summary>
    public static readonly RoutedEvent<SettingsSectionEventArgs> DiscardRequestedEvent =
        RoutedEvent.Register<SettingsPage, SettingsSectionEventArgs>(nameof(DiscardRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth
    {
        get => GetValue(CompactWidthProperty);
        set => SetValue(CompactWidthProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public SettingsPageLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    private bool _isCompact;

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact
    {
        get => _isCompact;
        private set
        {
            if (SetAndRaise(IsCompactProperty, ref _isCompact, value))
            {
                PseudoClasses.Set(CompactPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="SaveRequestedEvent"/>
    public event EventHandler<SettingsSectionEventArgs>? SaveRequested
    {
        add => AddHandler(SaveRequestedEvent, value);
        remove => RemoveHandler(SaveRequestedEvent, value);
    }

    /// <inheritdoc cref="DiscardRequestedEvent"/>
    public event EventHandler<SettingsSectionEventArgs>? DiscardRequested
    {
        add => AddHandler(DiscardRequestedEvent, value);
        remove => RemoveHandler(DiscardRequestedEvent, value);
    }

    #endregion

    private ScrollArea? _navScroll;
    private SettingsPageNavHost? _navHost;
    private GlidingIndicator? _pill;
    private SettingsPageContentHost? _contentHost;
    private ScrollViewer? _contentScroll;
    private PresenceHost? _saveBar;
    private TextBlock? _saveNote;
    private AvaloniaButton? _saveButton;
    private AvaloniaButton? _discardButton;
    private Control? _footerSlot;
    private Rect _pillTarget;
    private int _focusIndex = -1;
    private bool _revealPending;

    static SettingsPage()
    {
        SelectionModeProperty.OverrideDefaultValue<SettingsPage>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<SettingsPage>(false);
        AutoScrollToSelectedItemProperty.OverrideDefaultValue<SettingsPage>(false);
    }

    protected override Type StyleKeyOverride => typeof(SettingsPage);

    /// <summary>The section being shown (the selected item when it is a <see cref="SettingsSection"/>).</summary>
    public SettingsSection? SelectedSection => SelectedItem as SettingsSection;

    /// <summary>Shows <paramref name="section"/> when it is one of this page's sections and enabled.</summary>
    public bool Select(SettingsSection section)
    {
        var index = Items.IndexOf(section);
        if (index < 0 || !IsSectionEnabled(index))
        {
            return false;
        }
        SelectedIndex = index;
        return true;
    }

    #region Containers

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<SettingsPageNavItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new SettingsPageNavItem();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is SettingsPageNavItem nav && item is SettingsSection section)
        {
            Sync(nav, section);
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (index == SelectedIndex)
        {
            UpdateContent(animate: false);
        }
        UpdateTabStops();
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is SettingsPageNavItem nav)
        {
            nav.ClearValue(SettingsPageNavItem.HeaderProperty);
            nav.ClearValue(SettingsPageNavItem.IconProperty);
            nav.ClearValue(SettingsPageNavItem.BadgeProperty);
            nav.ClearValue(SettingsPageNavItem.IsDirtyProperty);
            nav.ClearValue(IsEnabledProperty);
        }
        UpdateTabStops();
    }

    private void Sync(SettingsPageNavItem nav, SettingsSection section)
    {
        nav.UnsavedText = Labels.UnsavedSection;
        nav.Header = section.Header as string ?? section.Header?.ToString();
        nav.Icon = section.Icon;
        nav.Badge = section.Badge;
        nav.IsDirty = section.IsDirty;
        nav.IsEnabled = section.IsEnabled;
    }

    /// <summary>Called by a <see cref="SettingsSection"/> when something its navigation item shows changes.</summary>
    internal void OnSectionChanged(SettingsSection section)
    {
        if (ContainerFromItem(section) is SettingsPageNavItem nav)
        {
            Sync(nav, section);
        }
        if (ReferenceEquals(section, SelectedItem))
        {
            UpdateSaveBar();
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_navHost is not null)
        {
            _navHost.Arranged = null;
        }
        if (_saveButton is not null)
        {
            _saveButton.Click -= OnSaveClick;
        }
        if (_discardButton is not null)
        {
            _discardButton.Click -= OnDiscardClick;
        }

        base.OnApplyTemplate(e);

        _navScroll = e.NameScope.Find<ScrollArea>(NavScrollPart);
        _navHost = e.NameScope.Find<SettingsPageNavHost>(NavHostPart);
        _pill = e.NameScope.Find<GlidingIndicator>(SelectionPillPart);
        _contentHost = e.NameScope.Find<SettingsPageContentHost>(ContentHostPart);
        _contentScroll = e.NameScope.Find<ScrollViewer>(ContentScrollPart);
        _saveBar = e.NameScope.Find<PresenceHost>(SaveBarPart);
        _saveNote = e.NameScope.Find<TextBlock>(SaveNotePart);
        _saveButton = e.NameScope.Find<AvaloniaButton>(SaveButtonPart);
        _discardButton = e.NameScope.Find<AvaloniaButton>(DiscardButtonPart);
        _footerSlot = e.NameScope.Find<Control>(FooterSlotPart);
        _pillTarget = default;
        _revealPending = true;

        if (_navHost is not null)
        {
            _navHost.Arranged = OnNavArranged;
        }
        if (_saveButton is not null)
        {
            _saveButton.Click += OnSaveClick;
        }
        if (_discardButton is not null)
        {
            _discardButton.Click += OnDiscardClick;
        }
        ApplyLabels();
        UpdateFooter();
        UpdateContent(animate: false);
        UpdateSaveBar();
    }

    /// <summary>The footer shows when there is one and the page has two columns.</summary>
    private void UpdateFooter()
    {
        if (_footerSlot is not null)
        {
            _footerSlot.IsVisible = Footer is not null && !IsCompact;
        }
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        if (_saveNote is not null)
        {
            _saveNote.Text = labels.UnsavedChanges;
        }
        if (_saveButton is not null)
        {
            _saveButton.Content = labels.Save;
        }
        if (_discardButton is not null)
        {
            _discardButton.Content = labels.Discard;
        }
        if (_navHost is not null)
        {
            AutomationProperties.SetName(_navHost, labels.NavigationName);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width comes from the parent, not from the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            IsCompact = availableSize.Width < CompactWidth;
        }
        return base.MeasureOverride(availableSize);
    }

    #endregion

    #region Selection

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            _focusIndex = change.GetNewValue<int>();
            UpdateContent(animate: true);
            UpdateIndicator(animate: true);
            UpdateTabStops();
            UpdateSaveBar();
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is SettingsPageNavItem nav)
                {
                    nav.UnsavedText = Labels.UnsavedSection;
                }
            }
        }
        else if (change.Property == CompactWidthProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == FooterProperty)
        {
            UpdateFooter();
        }
        else if (change.Property == IsCompactProperty)
        {
            UpdateFooter();
            _revealPending = true;
            // The pill jumps to the item's place in the new layout.
            _pillTarget = default;
            _pill?.Hide(animate: false);
        }
    }

    private void UpdateContent(bool animate)
    {
        var section = SelectedItem as SettingsSection;
        if (_contentHost is null || ReferenceEquals(_contentHost.Current, section))
        {
            return;
        }
        _contentHost.Show(section, animate && IsLoaded);
        if (_contentScroll is not null)
        {
            _contentScroll.Offset = default;
        }
    }

    private void UpdateSaveBar()
    {
        var dirty = SelectedItem is SettingsSection { IsDirty: true };
        PseudoClasses.Set(DirtyPseudoClass, dirty);
        if (_saveBar is not null)
        {
            _saveBar.IsPresent = dirty;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => RequestSave();

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedItem is SettingsSection section)
        {
            RaiseEvent(new SettingsSectionEventArgs(DiscardRequestedEvent, section));
        }
    }

    private bool RequestSave()
    {
        if (SelectedItem is not SettingsSection { IsDirty: true } section)
        {
            return false;
        }
        RaiseEvent(new SettingsSectionEventArgs(SaveRequestedEvent, section));
        return true;
    }

    /// <summary>Called by a navigation item when it is pressed.</summary>
    internal void OnNavItemPressed(SettingsPageNavItem nav)
    {
        var index = IndexFromContainer(nav);
        if (index >= 0 && nav.IsEffectivelyEnabled)
        {
            _focusIndex = index;
            SelectedIndex = index;
        }
    }

    /// <summary>Called by a navigation item when it receives focus: arrow keys select the item they move to.</summary>
    internal void OnNavItemFocused(SettingsPageNavItem nav, NavigationMethod method)
    {
        var index = IndexFromContainer(nav);
        if (index < 0)
        {
            return;
        }
        _focusIndex = index;
        UpdateTabStops();
        if (method == NavigationMethod.Directional && index != SelectedIndex)
        {
            SelectedIndex = index;
        }
    }

    private bool IsSectionEnabled(int index)
        => ContainerFromIndex(index) is { } container ? container.IsEnabled : Items[index] is not Control { IsEnabled: false };

    /// <summary>Only one navigation item is a tab stop: the focused one, else the selected one (roving tab index).</summary>
    private void UpdateTabStops()
    {
        var count = ItemCount;
        var stop = _focusIndex >= 0 && _focusIndex < count ? _focusIndex : SelectedIndex;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is SettingsPageNavItem nav)
            {
                nav.IsTabStop = i == stop || (stop < 0 && i == 0);
            }
        }
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled)
        {
            base.OnKeyDown(e);
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (command && e.Key == Key.S && RequestSave())
        {
            e.Handled = true;
            return;
        }
        if (e.Source is Visual source && FindNavItem(source) is { } nav)
        {
            var current = IndexFromContainer(nav);
            var previous = IsCompact ? Key.Left : Key.Up;
            var next = IsCompact ? Key.Right : Key.Down;
            int? target = e.KeyModifiers != KeyModifiers.None ? null
                : e.Key == previous ? Step(current, -1)
                : e.Key == next ? Step(current, 1)
                : e.Key == Key.Home ? Step(-1, 1)
                : e.Key == Key.End ? Step(ItemCount, -1)
                : null;
            if (target is { } index)
            {
                e.Handled = true;
                if (index >= 0 && ContainerFromIndex(index) is SettingsPageNavItem item)
                {
                    _focusIndex = index;
                    UpdateTabStops();
                    item.Focus(NavigationMethod.Directional);
                    SelectedIndex = index;
                }
                return;
            }
            if ((e.Key == Key.Enter || e.Key == Key.Space) && current >= 0)
            {
                e.Handled = true;
                SelectedIndex = current;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    private SettingsPageNavItem? FindNavItem(Visual source)
    {
        for (Visual? v = source; v is not null && v != this; v = v.GetVisualParent())
        {
            if (v is SettingsPageNavItem nav && IndexFromContainer(nav) >= 0)
            {
                return nav;
            }
        }
        return null;
    }

    /// <summary>The next enabled section from <paramref name="current"/> in <paramref name="delta"/>'s direction, without wrapping.</summary>
    private int Step(int current, int delta)
    {
        for (var index = current + delta; index >= 0 && index < ItemCount; index += delta)
        {
            if (IsSectionEnabled(index))
            {
                return index;
            }
        }
        return current >= 0 && current < ItemCount ? current : -1;
    }

    #endregion

    #region Indicator

    private void OnNavArranged()
    {
        // Passive layout changes (fonts, resizes, items) snap the pill; an equal target leaves a glide in flight alone.
        UpdateIndicator(animate: false, onlyWhenMoved: true);
        if (_revealPending && SelectedIndex >= 0 && ContainerFromIndex(SelectedIndex) is { Bounds.Width: > 0 } selected)
        {
            // The selected section starts in view (after the template is applied or the layout changes), without motion.
            _revealPending = false;
            RevealInNav(selected, instant: true);
        }
    }

    private void UpdateIndicator(bool animate, bool onlyWhenMoved = false)
    {
        if (_pill is null || _navHost is null)
        {
            return;
        }
        var index = SelectedIndex;
        if (index < 0 || ContainerFromIndex(index) is not { IsVisible: true } container || container.Bounds.Width <= 0)
        {
            _pillTarget = default;
            _pill.Hide(animate: false);
            return;
        }
        var origin = container.TranslatePoint(default, _navHost);
        if (origin is null)
        {
            return;
        }
        var target = new Rect(origin.Value, container.Bounds.Size);
        if (onlyWhenMoved && _pill.HasTarget && Near(target, _pillTarget))
        {
            return;
        }
        _pillTarget = target;
        _pill.MoveTo(target, animate && IsLoaded, ArcSpring.Snappy);
        if (!onlyWhenMoved)
        {
            RevealInNav(container);
        }
    }

    /// <summary>
    /// Scrolls the navigation (the row of pills on a compact page) so <paramref name="container"/> is in view, with a
    /// little room beside it. Scrolls only the navigation: BringIntoView would also scroll any page the block sits in.
    /// </summary>
    private void RevealInNav(Control container, bool instant = false)
    {
        if (_navScroll is null || _navHost is null || container.TranslatePoint(default, _navHost) is not { } origin)
        {
            return;
        }
        const double room = 16;
        var offset = _navScroll.Offset;
        var viewport = _navScroll.Viewport;
        var target = offset;
        if (IsCompact)
        {
            var left = origin.X + _navHost.Bounds.X;
            var right = left + container.Bounds.Width;
            if (left - room < offset.X)
            {
                target = target.WithX(Math.Max(0, left - room));
            }
            else if (right + room > offset.X + viewport.Width)
            {
                target = target.WithX(right + room - viewport.Width);
            }
        }
        else
        {
            var top = origin.Y + _navHost.Bounds.Y;
            var bottom = top + container.Bounds.Height;
            if (top - room < offset.Y)
            {
                target = target.WithY(Math.Max(0, top - room));
            }
            else if (bottom + room > offset.Y + viewport.Height)
            {
                target = target.WithY(bottom + room - viewport.Height);
            }
        }
        if (target == offset)
        {
            return;
        }
        if (instant)
        {
            _navScroll.Offset = target;
        }
        else
        {
            _navScroll.ScrollToOffset(target);
        }
    }

    private static bool Near(Rect a, Rect b)
        => Math.Abs(a.X - b.X) < 0.5 && Math.Abs(a.Y - b.Y) < 0.5 && Math.Abs(a.Width - b.Width) < 0.5 && Math.Abs(a.Height - b.Height) < 0.5;

    #endregion
}
