using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Globalization;
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
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A list or table with a floating bar for what is selected in it. Put the list in <c>Content</c> and bind
/// <see cref="SelectedCount"/> to its selection: while anything is selected, a pill rises from the bottom edge with the
/// count (its digits roll as it changes), "Select all" for the rest, the bulk actions and a clear button; it sinks
/// away when the selection empties. Escape clears the selection from anywhere inside, the bar is one toolbar with the
/// arrow keys moving between its buttons, and the count is read out as it changes. Below <see cref="CompactWidth"/>
/// the actions keep only their icons, with tooltips.
/// </summary>
[TemplatePart(BarHostPart, typeof(PresenceHost))]
[TemplatePart(ActionsPart, typeof(StackPanel))]
[TemplatePart(SelectAllButtonPart, typeof(AvaloniaButton))]
[TemplatePart(ClearButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(OpenPseudoClass, CompactPseudoClass)]
public class SelectionToolbar : ContentControl
{
    public const string BarHostPart = "PART_BarHost";
    public const string ActionsPart = "PART_Actions";
    public const string SelectAllButtonPart = "PART_SelectAllButton";
    public const string ClearButtonPart = "PART_ClearButton";

    /// <summary>Set while the bar is shown (anything is selected).</summary>
    public const string OpenPseudoClass = ":open";

    /// <summary>Set below <see cref="CompactWidth"/>: actions show their icons only.</summary>
    public const string CompactPseudoClass = ":compact";

    internal const string CountPart = "PART_Count";
    internal const string BarPart = "PART_Bar";
    internal const string AnnouncerPart = "PART_Announcer";

    #region Properties

    /// <summary>How many items are selected (two-way); the bar shows while it is above zero. Clearing sets it to 0.</summary>
    public static readonly StyledProperty<int> SelectedCountProperty =
        AvaloniaProperty.Register<SelectionToolbar, int>(nameof(SelectedCount), defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => Math.Max(0, value));

    /// <summary>How many items there are; "Select all" shows while fewer are selected. Zero hides it.</summary>
    public static readonly StyledProperty<int> TotalCountProperty =
        AvaloniaProperty.Register<SelectionToolbar, int>(nameof(TotalCount));

    /// <summary>Below this width the actions show their icons only (set by the theme from the token).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<SelectionToolbar, double>(nameof(CompactWidth), 560);

    /// <summary>The built-in text; replace the whole object to localize the bar.</summary>
    public static readonly StyledProperty<SelectionToolbarLabels> LabelsProperty =
        AvaloniaProperty.Register<SelectionToolbar, SelectionToolbarLabels>(nameof(Labels), new SelectionToolbarLabels());

    /// <inheritdoc cref="SelectedCountProperty"/>
    public int SelectedCount { get => GetValue(SelectedCountProperty); set => SetValue(SelectedCountProperty, value); }

    /// <inheritdoc cref="TotalCountProperty"/>
    public int TotalCount { get => GetValue(TotalCountProperty); set => SetValue(TotalCountProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SelectionToolbarLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The bulk actions, in order; written inline in XAML or added in code. Empty shows Archive, Move, Share and Delete (<see cref="SelectionToolbarLabels.DefaultActions"/>).</summary>
    public AvaloniaList<SelectionToolbarAction> Actions { get; } = new();

    /// <summary>The actions on the bar: <see cref="Actions"/>, or the defaults while it is empty.</summary>
    public IReadOnlyList<SelectionToolbarAction> ShownActions => _shownActions;

    #endregion

    #region Events

    /// <summary>Raised when an action is chosen, with the action and the selected count (after its command runs).</summary>
    public static readonly RoutedEvent<SelectionToolbarActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<SelectionToolbar, SelectionToolbarActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised by "Select all": select every item, then set <see cref="SelectedCount"/>.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectAllRequestedEvent =
        RoutedEvent.Register<SelectionToolbar, RoutedEventArgs>(nameof(SelectAllRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the clear button or Escape clears the selection (<see cref="SelectedCount"/> is already 0): deselect your items.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClearedEvent =
        RoutedEvent.Register<SelectionToolbar, RoutedEventArgs>(nameof(Cleared), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<SelectionToolbarActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <inheritdoc cref="SelectAllRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? SelectAllRequested
    {
        add => AddHandler(SelectAllRequestedEvent, value);
        remove => RemoveHandler(SelectAllRequestedEvent, value);
    }

    /// <inheritdoc cref="ClearedEvent"/>
    public event EventHandler<RoutedEventArgs>? Cleared
    {
        add => AddHandler(ClearedEvent, value);
        remove => RemoveHandler(ClearedEvent, value);
    }

    #endregion

    private readonly List<(SelectionToolbarAction Action, AvaloniaButton Button, TextBlock Label)> _buttons = new();
    private readonly List<IDisposable> _actionBindings = new();
    private IReadOnlyList<SelectionToolbarAction> _shownActions = [];
    private PresenceHost? _barHost;
    private Border? _bar;
    private StackPanel? _actions;
    private ArcContentSwap? _count;
    private AvaloniaButton? _selectAll;
    private AvaloniaButton? _clear;
    private AuthAnnouncer? _announcer;
    private int _shownCount;

    static SelectionToolbar()
    {
        FocusableProperty.OverrideDefaultValue<SelectionToolbar>(false);
    }

    public SelectionToolbar()
    {
        Actions.CollectionChanged += OnActionsChanged;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    /// <summary>Clears the selection: <see cref="SelectedCount"/> becomes 0 and <see cref="Cleared"/> is raised.</summary>
    public void ClearSelection()
    {
        if (SelectedCount == 0)
        {
            return;
        }
        var hadFocus = _bar?.IsKeyboardFocusWithin == true;
        SetCurrentValue(SelectedCountProperty, 0);
        RaiseEvent(new RoutedEventArgs(ClearedEvent));
        if (hadFocus && Content is IInputElement content)
        {
            // The bar is leaving: focus goes back to the list instead of to nowhere.
            content.Focus(NavigationMethod.Tab);
        }
    }

    /// <summary>The button of <paramref name="action"/> on the bar (tests), or null.</summary>
    internal AvaloniaButton? ButtonOf(SelectionToolbarAction action)
        => _buttons.FirstOrDefault(b => ReferenceEquals(b.Action, action)).Button;

    internal bool IsBarPresent => _barHost?.IsPresent == true;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_selectAll is not null)
        {
            _selectAll.Click -= OnSelectAllClick;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        base.OnApplyTemplate(e);
        _barHost = e.NameScope.Find<PresenceHost>(BarHostPart);
        _bar = e.NameScope.Find<Border>(BarPart);
        _actions = e.NameScope.Find<StackPanel>(ActionsPart);
        _count = e.NameScope.Find<ArcContentSwap>(CountPart);
        _selectAll = e.NameScope.Find<AvaloniaButton>(SelectAllButtonPart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearButtonPart);
        _announcer = e.NameScope.Find<AuthAnnouncer>(AnnouncerPart);
        if (_selectAll is not null)
        {
            _selectAll.Click += OnSelectAllClick;
        }
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        if (_bar is not null)
        {
            AutomationProperties.SetControlTypeOverride(_bar, AutomationControlType.ToolBar);
            _bar.AddHandler(KeyDownEvent, OnBarKeyDown, RoutingStrategies.Bubble);
        }
        _shownCount = 0;
        BuildActions();
        UpdateTexts();
        UpdateBar(announce: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedCountProperty || change.Property == TotalCountProperty)
        {
            UpdateBar(announce: change.Property == SelectedCountProperty);
        }
        else if (change.Property == LabelsProperty)
        {
            BuildActions();
            UpdateTexts();
            UpdateBar(announce: false);
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            UpdateCompact();
        }
    }

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildActions();

    #endregion

    #region Content

    private void BuildActions()
    {
        _shownActions = Actions.Count > 0 ? Actions.ToArray() : Labels.DefaultActions();
        if (_actions is null)
        {
            return;
        }
        foreach (var (_, button, _) in _buttons)
        {
            button.Click -= OnActionClick;
        }
        foreach (var binding in _actionBindings)
        {
            binding.Dispose();
        }
        _actionBindings.Clear();
        _actions.Children.Clear();
        _buttons.Clear();
        // Destructive actions go last, after a gap, so they are not hit by a slip of the arrow keys.
        var ordered = _shownActions.Where(a => !a.IsDestructive).Concat(_shownActions.Where(a => a.IsDestructive)).ToArray();
        var hadSafe = false;
        foreach (var action in ordered)
        {
            var label = new TextBlock { Text = action.Label, VerticalAlignment = VerticalAlignment.Center };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (action.Icon is { } icon)
            {
                row.Children.Add(new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center });
            }
            row.Children.Add(label);
            var button = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Content = row,
                Tag = action,
                VerticalAlignment = VerticalAlignment.Center
            };
            // The bar follows an action's state live: an app switches actions off or hides them as the selection changes.
            _actionBindings.Add(button.Bind(InputElement.IsEnabledProperty, action.GetObservable(SelectionToolbarAction.IsEnabledProperty)));
            _actionBindings.Add(button.Bind(Visual.IsVisibleProperty, action.GetObservable(SelectionToolbarAction.IsVisibleProperty)));
            if (action.IsDestructive)
            {
                ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.Danger);
                if (hadSafe)
                {
                    button.Margin = new Thickness(6, 0, 0, 0);
                }
            }
            else
            {
                hadSafe = true;
            }
            AutomationProperties.SetName(button, action.Label);
            Tooltip.SetTip(button, null);
            button.Click += OnActionClick;
            _buttons.Add((action, button, label));
            _actions.Children.Add(button);
        }
        UpdateCompact();
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        if (_clear is not null)
        {
            AutomationProperties.SetName(_clear, labels.Clear);
            Tooltip.SetTip(_clear, labels.Clear);
        }
        if (_bar is not null)
        {
            AutomationProperties.SetName(_bar, labels.Toolbar);
        }
    }

    private string Format(int value) => value.ToString("N0", Labels.Culture);

    private void UpdateBar(bool announce)
    {
        var count = SelectedCount;
        var open = count > 0;
        PseudoClasses.Set(OpenPseudoClass, open);
        if (_barHost is not null)
        {
            _barHost.IsPresent = open;
        }
        if (open && _count is not null && count != _shownCount)
        {
            // The digits roll up for a larger count and down for a smaller one; the bar keeps the last count while it leaves.
            _count.Direction = count >= _shownCount ? 1 : -1;
            _count.Content = Labels.Count(Format(count));
            _shownCount = count;
        }
        if (!open)
        {
            _shownCount = 0;
        }
        if (_selectAll is not null)
        {
            var total = TotalCount;
            _selectAll.IsVisible = total > 0 && count < total;
            _selectAll.Content = Labels.SelectAll(Format(total));
        }
        if (announce && open)
        {
            _announcer?.Announce(Labels.Count(Format(count)));
        }
    }

    private void UpdateCompact()
    {
        var compact = Bounds.Width > 0 && Bounds.Width < CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        foreach (var (action, button, label) in _buttons)
        {
            // An icon-only action keeps its label as its tooltip; one without an icon keeps its text.
            var iconOnly = compact && action.Icon is not null;
            label.IsVisible = !iconOnly;
            Tooltip.SetTip(button, iconOnly ? action.Label : null);
        }
    }

    #endregion

    #region Handlers

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not AvaloniaButton { Tag: SelectionToolbarAction action })
        {
            return;
        }
        var count = SelectedCount;
        if (action.Command is { } command && command.CanExecute(count))
        {
            command.Execute(count);
        }
        RaiseEvent(new SelectionToolbarActionEventArgs(ActionInvokedEvent, action, count));
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(SelectAllRequestedEvent));

    private void OnClearClick(object? sender, RoutedEventArgs e) => ClearSelection();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && SelectedCount > 0)
        {
            e.Handled = true;
            ClearSelection();
        }
    }

    /// <summary>The bar is one toolbar: Left and Right move between its buttons, Home and End jump to the ends.</summary>
    private void OnBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (_bar is null || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End))
        {
            return;
        }
        var buttons = _bar.GetVisualDescendants().OfType<AvaloniaButton>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled).ToList();
        if (buttons.Count == 0)
        {
            return;
        }
        var current = buttons.FindIndex(b => b.IsKeyboardFocusWithin);
        var next = e.Key switch
        {
            Key.Home => 0,
            Key.End => buttons.Count - 1,
            Key.Left => Math.Max(0, current - 1),
            _ => Math.Min(buttons.Count - 1, current + 1)
        };
        buttons[next].Focus(NavigationMethod.Tab);
        e.Handled = true;
    }

    #endregion
}
