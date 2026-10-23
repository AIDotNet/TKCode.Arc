using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The System / User tabs of <see cref="PromptStudio"/> (Arc's <c>role="tablist"</c>): two 30px pill tabs 2px apart with no
/// frame, one SurfaceMuted pill gliding under the active tab on the quick, critically damped spring (Reduced and Off
/// jump). Only the active tab is a tab stop; Left / Right move and activate at once (wrapping), Home and End go to the
/// ends. In the compare view a tab whose part changed shows a small dot after its name.
/// </summary>
[TemplatePart(SystemTabPart, typeof(PromptPartTab))]
[TemplatePart(UserTabPart, typeof(PromptPartTab))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
internal sealed class PromptPartTabs : TemplatedControl
{
    public const string SystemTabPart = "PART_SystemTab";
    public const string UserTabPart = "PART_UserTab";
    public const string HighlightPart = "PART_Highlight";

    /// <summary>The active tab (two-way).</summary>
    public static readonly StyledProperty<PromptPart> ActivePartProperty =
        AvaloniaProperty.Register<PromptPartTabs, PromptPart>(nameof(ActivePart), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The system tab's name.</summary>
    public static readonly StyledProperty<string?> SystemTextProperty =
        AvaloniaProperty.Register<PromptPartTabs, string?>(nameof(SystemText));

    /// <summary>The user tab's name.</summary>
    public static readonly StyledProperty<string?> UserTextProperty =
        AvaloniaProperty.Register<PromptPartTabs, string?>(nameof(UserText));

    /// <summary>Whether the system tab shows the changed dot.</summary>
    public static readonly StyledProperty<bool> SystemChangedProperty =
        AvaloniaProperty.Register<PromptPartTabs, bool>(nameof(SystemChanged));

    /// <summary>Whether the user tab shows the changed dot.</summary>
    public static readonly StyledProperty<bool> UserChangedProperty =
        AvaloniaProperty.Register<PromptPartTabs, bool>(nameof(UserChanged));

    /// <summary>The accessible name of a changed tab, from its name (", changed").</summary>
    public static readonly StyledProperty<Func<string, string>?> ChangedNameProperty =
        AvaloniaProperty.Register<PromptPartTabs, Func<string, string>?>(nameof(ChangedName));

    /// <summary>The accessible name of the tab list ("Prompt part").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<PromptPartTabs, string?>(nameof(AccessibleName));

    /// <summary>Raised when the person activates a tab (pointer or keys), after <see cref="ActivePart"/> changed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> PartChosenEvent =
        RoutedEvent.Register<PromptPartTabs, RoutedEventArgs>(nameof(PartChosen), RoutingStrategies.Bubble);

    private PromptPartTab? _system;
    private PromptPartTab? _user;
    private GlidingIndicator? _highlight;
    private Rect? _placed;
    private bool _glide;

    static PromptPartTabs()
    {
        FocusableProperty.OverrideDefaultValue<PromptPartTabs>(false);
    }

    /// <inheritdoc cref="ActivePartProperty"/>
    public PromptPart ActivePart { get => GetValue(ActivePartProperty); set => SetValue(ActivePartProperty, value); }

    /// <inheritdoc cref="SystemTextProperty"/>
    public string? SystemText { get => GetValue(SystemTextProperty); set => SetValue(SystemTextProperty, value); }

    /// <inheritdoc cref="UserTextProperty"/>
    public string? UserText { get => GetValue(UserTextProperty); set => SetValue(UserTextProperty, value); }

    /// <inheritdoc cref="SystemChangedProperty"/>
    public bool SystemChanged { get => GetValue(SystemChangedProperty); set => SetValue(SystemChangedProperty, value); }

    /// <inheritdoc cref="UserChangedProperty"/>
    public bool UserChanged { get => GetValue(UserChangedProperty); set => SetValue(UserChangedProperty, value); }

    /// <inheritdoc cref="ChangedNameProperty"/>
    public Func<string, string>? ChangedName { get => GetValue(ChangedNameProperty); set => SetValue(ChangedNameProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <summary>Raised when the person activates a tab.</summary>
    public event EventHandler<RoutedEventArgs>? PartChosen { add => AddHandler(PartChosenEvent, value); remove => RemoveHandler(PartChosenEvent, value); }

    /// <summary>The system tab (template part).</summary>
    internal PromptPartTab? SystemTab => _system;

    /// <summary>The user tab (template part).</summary>
    internal PromptPartTab? UserTab => _user;

    /// <summary>The gliding background (template part).</summary>
    internal GlidingIndicator? Highlight => _highlight;

    /// <summary>The tab of <paramref name="part"/>.</summary>
    internal PromptPartTab? TabOf(PromptPart part) => part == PromptPart.System ? _system : _user;

    /// <summary>Focuses the active tab by <paramref name="method"/>; false when it cannot take focus.</summary>
    public bool FocusActive(NavigationMethod method = NavigationMethod.Tab) =>
        TabOf(ActivePart) is { } tab && tab.IsEffectivelyVisible && tab.IsEffectivelyEnabled && tab.Focus(method);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_system is not null)
        {
            _system.Click -= OnTabClick;
        }
        if (_user is not null)
        {
            _user.Click -= OnTabClick;
        }
        _system = e.NameScope.Find<PromptPartTab>(SystemTabPart);
        _user = e.NameScope.Find<PromptPartTab>(UserTabPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        if (_system is not null)
        {
            _system.Part = PromptPart.System;
            _system.Click += OnTabClick;
        }
        if (_user is not null)
        {
            _user.Part = PromptPart.User;
            _user.Click += OnTabClick;
        }
        _placed = null;
        UpdateTabs();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivePartProperty)
        {
            _glide = true;
            UpdateTabs();
            InvalidateArrange();
            if (ControlAutomationPeer.FromElement(this) is TabsPeer peer)
            {
                peer.SelectionChanged();
            }
        }
        else if (change.Property == SystemTextProperty || change.Property == UserTextProperty ||
                 change.Property == SystemChangedProperty || change.Property == UserChangedProperty ||
                 change.Property == ChangedNameProperty)
        {
            UpdateTabs();
        }
        else if (change.Property == AccessibleNameProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    private void UpdateTabs()
    {
        Update(_system, SystemText, SystemChanged, ActivePart == PromptPart.System);
        Update(_user, UserText, UserChanged, ActivePart == PromptPart.User);
    }

    private void Update(PromptPartTab? tab, string? text, bool changed, bool active)
    {
        if (tab is null)
        {
            return;
        }
        tab.Text = text;
        tab.ShowsDot = changed;
        tab.IsSelected = active;
        // Roving tab stop: only the active tab is reached with Tab.
        KeyboardNavigation.SetIsTabStop(tab, active);
        var name = text ?? string.Empty;
        AutomationProperties.SetName(tab, changed && ChangedName is { } format ? format(name) : name);
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is PromptPartTab tab)
        {
            Choose(tab.Part);
        }
    }

    /// <summary>The person chose <paramref name="part"/>: activate it and say so when that changed the tab.</summary>
    internal void Choose(PromptPart part)
    {
        if (part == ActivePart)
        {
            return;
        }
        SetCurrentValue(ActivePartProperty, part);
        RaiseEvent(new RoutedEventArgs(PartChosenEvent, this));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source)
        {
            return;
        }
        var from = _system is { } s && (ReferenceEquals(s, source) || s.IsVisualAncestorOf(source)) ? PromptPart.System
            : _user is { } u && (ReferenceEquals(u, source) || u.IsVisualAncestorOf(source)) ? PromptPart.User
            : (PromptPart?)null;
        if (from is not { } current)
        {
            return;
        }
        PromptPart? to = e.Key switch
        {
            Key.Left or Key.Right => current == PromptPart.System ? PromptPart.User : PromptPart.System,
            Key.Home => PromptPart.System,
            Key.End => PromptPart.User,
            _ => null
        };
        if (to is not { } target)
        {
            return;
        }
        e.Handled = true;
        // Automatic activation: the tab that takes focus is the active one.
        Choose(target);
        TabOf(target)?.Focus(NavigationMethod.Directional);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // The tabs were just arranged: the background follows the active one (gliding when the tab changed).
        if (TabOf(ActivePart) is { } tab && _highlight is not null && tab.Bounds.Width > 0)
        {
            var origin = tab.TranslatePoint(default, _highlight) ?? tab.Bounds.Position;
            var rect = new Rect(origin, tab.Bounds.Size);
            if (_placed != rect || !_highlight.HasTarget)
            {
                var animate = _glide && _highlight.HasTarget;
                _highlight.MoveTo(rect, animate, ArcSpring.Quick);
                _placed = rect;
            }
        }
        _glide = false;
        return size;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);

    /// <summary>A tab list (Arc's <c>role="tablist"</c>) whose selection is the active tab.</summary>
    private sealed class TabsPeer(PromptPartTabs owner) : ControlAutomationPeer(owner), ISelectionProvider
    {
        private PromptPartTabs Tabs => (PromptPartTabs)Owner;

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => true;

        public IReadOnlyList<AutomationPeer> GetSelection() =>
            Tabs.TabOf(Tabs.ActivePart) is { } tab && CreatePeerForElement(tab) is { } peer ? [peer] : [];

        public void SelectionChanged() => RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionProvider) ? this : base.GetProviderCore(providerType);
    }
}

/// <summary>
/// One tab of <see cref="PromptPartTabs"/>: a 30px text button without a background of its own (the gliding background shows
/// under the active one), TextMuted at rest, Foreground on hover and when active; <c>:selected</c> while active,
/// <c>:hover</c> for mouse and pen only, <c>:changed</c> while its dot shows. Reads as a tab item.
/// </summary>
[PseudoClasses(SelectedPseudoClass, HoverPseudoClass, ChangedPseudoClass)]
internal sealed class PromptPartTab : AvaloniaButton
{
    public const string SelectedPseudoClass = ":selected";
    public const string HoverPseudoClass = ":hover";
    public const string ChangedPseudoClass = ":changed";

    /// <summary>The visible name.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptPartTab, string?>(nameof(Text));

    /// <summary>Whether this tab is the active one.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PromptPartTab, bool>(nameof(IsSelected));

    /// <summary>Whether the changed dot shows after the name.</summary>
    public static readonly StyledProperty<bool> ShowsDotProperty =
        AvaloniaProperty.Register<PromptPartTab, bool>(nameof(ShowsDot));

    /// <summary>The part this tab shows.</summary>
    public PromptPart Part { get; set; }

    protected override Type StyleKeyOverride => typeof(PromptPartTab);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <inheritdoc cref="ShowsDotProperty"/>
    public bool ShowsDot { get => GetValue(ShowsDotProperty); set => SetValue(ShowsDotProperty, value); }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            var selected = change.GetNewValue<bool>();
            PseudoClasses.Set(SelectedPseudoClass, selected);
            if (ControlAutomationPeer.FromElement(this) is TabPeer peer)
            {
                peer.RaiseSelectedChanged(selected);
            }
        }
        else if (change.Property == ShowsDotProperty)
        {
            PseudoClasses.Set(ChangedPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TabPeer(this);

    /// <summary>A tab item, selected while active (Arc's <c>role="tab"</c> + <c>aria-selected</c>).</summary>
    private sealed class TabPeer(PromptPartTab owner) : ButtonAutomationPeer(owner), ISelectionItemProvider
    {
        private PromptPartTab Tab => (PromptPartTab)Owner;

        public bool IsSelected => Tab.IsSelected;

        public ISelectionProvider? SelectionContainer =>
            Tab.FindAncestorOfType<PromptPartTabs>() is { } tabs ? CreatePeerForElement(tabs) as ISelectionProvider : null;

        public void Select() => Tab.FindAncestorOfType<PromptPartTabs>()?.Choose(Tab.Part);

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
        }

        public void RaiseSelectedChanged(bool selected) =>
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !selected, selected);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionItemProvider) ? this : base.GetProviderCore(providerType);
    }
}

/// <summary>
/// The header row of <see cref="PromptStudio"/> (Arc's wrapping flex row with space between): the title group on the left
/// and the model button on the right, vertically centered; when the two do not fit side by side with
/// <see cref="ColumnGap"/> between them, the model button moves under the title group, <see cref="RowGap"/> below it, on
/// the left.
/// </summary>
internal sealed class PromptStudioHeaderPanel : Panel
{
    /// <summary>The room between the title group and the model button on one row.</summary>
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<PromptStudioHeaderPanel, double>(nameof(ColumnGap), 12);

    /// <summary>The room between the two rows when the model button wraps.</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<PromptStudioHeaderPanel, double>(nameof(RowGap), 8);

    static PromptStudioHeaderPanel()
    {
        AffectsMeasure<PromptStudioHeaderPanel>(ColumnGapProperty, RowGapProperty);
    }

    /// <inheritdoc cref="ColumnGapProperty"/>
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <inheritdoc cref="RowGapProperty"/>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>Whether the model button sits under the title group (last measure).</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var title = Children[0];
        var end = Children[1];
        var natural = new Size(double.PositiveInfinity, availableSize.Height);
        title.Measure(natural);
        end.Measure(natural);
        var titleSize = title.DesiredSize;
        var endSize = end.DesiredSize;
        var fits = double.IsInfinity(availableSize.Width) || titleSize.Width + ColumnGap + endSize.Width <= availableSize.Width + 0.01;
        IsWrapped = !fits;
        if (fits)
        {
            return new Size(titleSize.Width + ColumnGap + endSize.Width, Math.Max(titleSize.Height, endSize.Height));
        }
        // Wrapped: each row has the whole width (a title wider than that is clipped by the card).
        title.Measure(availableSize);
        end.Measure(availableSize);
        titleSize = title.DesiredSize;
        endSize = end.DesiredSize;
        return new Size(Math.Max(titleSize.Width, endSize.Width), titleSize.Height + RowGap + endSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var title = Children[0];
        var end = Children[1];
        var titleSize = title.DesiredSize;
        var endSize = end.DesiredSize;
        if (!IsWrapped)
        {
            var height = finalSize.Height;
            title.Arrange(new Rect(0, (height - titleSize.Height) / 2, Math.Min(titleSize.Width, finalSize.Width), titleSize.Height));
            end.Arrange(new Rect(Math.Max(0, finalSize.Width - endSize.Width), (height - endSize.Height) / 2, endSize.Width, endSize.Height));
            return finalSize;
        }
        title.Arrange(new Rect(0, 0, Math.Min(titleSize.Width, finalSize.Width), titleSize.Height));
        end.Arrange(new Rect(0, titleSize.Height + RowGap, Math.Min(endSize.Width, finalSize.Width), endSize.Height));
        return finalSize;
    }
}

/// <summary>The text work of <see cref="PromptStudio"/>: filling in values, the default token estimate.</summary>
internal static class PromptStudioText
{
    /// <summary>
    /// <paramref name="text"/> with every <c>{{name}}</c> replaced by its value in <paramref name="values"/> (a name without a
    /// value, or with an empty one, becomes empty text).
    /// </summary>
    public static string Render(string? text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        var chips = PromptTemplateText.Parse(text);
        if (chips.Count == 0)
        {
            return text;
        }
        var builder = new StringBuilder(text.Length);
        var copied = 0;
        foreach (var chip in chips)
        {
            builder.Append(text, copied, chip.Start - copied);
            if (values.TryGetValue(chip.Id, out var value) && value is not null)
            {
                builder.Append(value);
            }
            copied = chip.End;
        }
        builder.Append(text, copied, text.Length - copied);
        return builder.ToString();
    }

    /// <summary>The default estimate: about a quarter of the characters (rounded up; 0 for no text).</summary>
    public static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
