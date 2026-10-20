using System.Collections;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc's Add filter menu: a trigger that opens a two-step panel, pick a field, then a value. Fields show their
/// current value; the value step checks the applied one. Steps slide in the direction of travel and the title swaps
/// with a short blur. The panel is an <see cref="ArcPopup"/> aligned with the trigger's end (or start) edge that
/// flips when there is no room. Keyboard: ↓/↑ on the trigger opens on the first or last item, Enter/Space opens,
/// arrows and Home/End move, → enters a field, ← goes back, letters jump, Escape closes and returns focus.
/// Used by <see cref="FilterToolbar"/>; also usable on its own.
/// </summary>
[PseudoClasses(OpenPseudoClass, PressedPseudoClass)]
public class FilterMenu : TemplatedControl
{
    public const string OpenPseudoClass = ":open";
    public const string PressedPseudoClass = ":pressed";

    public static readonly StyledProperty<IEnumerable<FilterField>?> FieldsProperty =
        AvaloniaProperty.Register<FilterMenu, IEnumerable<FilterField>?>(nameof(Fields));

    public static readonly StyledProperty<IEnumerable<FilterChip>?> ActiveFiltersProperty =
        AvaloniaProperty.Register<FilterMenu, IEnumerable<FilterChip>?>(nameof(ActiveFilters));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FilterMenu, string>(nameof(Label), "Add filter");

    public static readonly StyledProperty<FilterMenuAlignment> AlignmentProperty =
        AvaloniaProperty.Register<FilterMenu, FilterMenuAlignment>(nameof(Alignment), FilterMenuAlignment.End);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<FilterMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> BackLabelProperty =
        AvaloniaProperty.Register<FilterMenu, string>(nameof(BackLabel), "Back to fields");

    public static readonly RoutedEvent<FilterMenuSelectedEventArgs> FilterSelectedEvent =
        RoutedEvent.Register<FilterMenu, FilterMenuSelectedEventArgs>(nameof(FilterSelected), RoutingStrategies.Bubble);

    /// <summary>The fields offered in the first step.</summary>
    public IEnumerable<FilterField>? Fields { get => GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>Applied filters: each field shows its current value and the value step checks it.</summary>
    public IEnumerable<FilterChip>? ActiveFilters { get => GetValue(ActiveFiltersProperty); set => SetValue(ActiveFiltersProperty, value); }

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The trigger edge the panel lines up with.</summary>
    public FilterMenuAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    public string BackLabel { get => GetValue(BackLabelProperty); set => SetValue(BackLabelProperty, value); }

    /// <summary>Raised when a value is picked; the chip id is the field id.</summary>
    public event EventHandler<FilterMenuSelectedEventArgs>? FilterSelected
    {
        add => AddHandler(FilterSelectedEvent, value);
        remove => RemoveHandler(FilterSelectedEvent, value);
    }

    private ArcPopup? _popup;
    private Border? _surface;
    private ArcContentSwap? _title;
    private ArcContentSwap? _stage;
    private PresenceHost? _backHost;
    private FilterToolbarIconButton? _back;
    private FilterMenuList? _list;
    private FilterField? _field;
    private bool _openedByKeyboard;
    private bool _restoreFocus;
    private bool _syncing;

    static FilterMenu()
    {
        FocusableProperty.OverrideDefaultValue<FilterMenu>(true);
    }

    public FilterMenu()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        FocusAdorner = null;
    }

    internal FilterMenuList? CurrentList => _list;

    internal FilterField? CurrentField => _field;

    internal ArcPopup? Popup => _popup;

    /// <summary>Opens the panel on the field step; <paramref name="focusLast"/> starts on the last field.</summary>
    public void Open(bool keyboard = false, bool focusLast = false)
    {
        if (Fields is null || !Fields.Any())
        {
            return;
        }
        EnsurePopup();
        _openedByKeyboard = keyboard;
        _field = null;
        _title!.Direction = -1;
        _title.Content = Label;
        _backHost!.IsPresent = false;
        ShowStep(FieldRows(), direction: -1, animate: false);
        var popup = _popup!;
        popup.Placement = Alignment == FilterMenuAlignment.End ? PlacementMode.BottomEdgeAlignedRight : PlacementMode.BottomEdgeAlignedLeft;
        if (popup.Parent is null)
        {
            ((ISetLogicalParent)popup).SetParent(this);
        }
        SetOpen(true);
        popup.IsOpen = true;
        var list = _list!;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && ReferenceEquals(list, _list))
            {
                list.FocusRow(focusLast ? list.Rows.Count - 1 : 0, keyboard);
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Closes the panel; <paramref name="restoreFocus"/> returns focus to the trigger.</summary>
    public void Close(bool restoreFocus = false, bool keyboard = false)
    {
        if (_popup is not { IsOpen: true } popup)
        {
            SetOpen(false);
            return;
        }
        _restoreFocus = restoreFocus;
        popup.IsOpen = false;
        SetOpen(false);
        if (restoreFocus)
        {
            Focus(keyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
        }
    }

    private void SetOpen(bool open)
    {
        _syncing = true;
        try
        {
            SetCurrentValue(IsOpenProperty, open);
        }
        finally
        {
            _syncing = false;
        }
        PseudoClasses.Set(OpenPseudoClass, open);
        AutomationProperties.SetHelpText(this, open ? "expanded" : string.Empty);
    }

    private string? CurrentValue(FilterField field) => ActiveFilters?.FirstOrDefault(f => f.Id == field.Id)?.Value;

    private List<FilterMenuList.Row> FieldRows() => (Fields ?? []).Select(f =>
        new FilterMenuList.Row(f.Id, f.Label, f.Icon, CurrentValue(f), false, true)).ToList();

    private List<FilterMenuList.Row> ValueRows(FilterField field)
    {
        var current = CurrentValue(field);
        return field.Options.Select(o => new FilterMenuList.Row(o.Value, o.Text, o.Icon, o.Hint, current == o.Text, false)).ToList();
    }

    private void ShowStep(List<FilterMenuList.Row> rows, int direction, bool animate)
    {
        var list = new FilterMenuList(rows, ItemHeight, PanelRadius - 6);
        list.Chosen = OnChosen;
        if (_field is not null)
        {
            list.Back = GoBack;
        }
        _list = list;
        _stage!.Direction = direction;
        if (!animate)
        {
            var mode = _stage.Mode;
            _stage.Mode = ContentSwapMode.None;
            _stage.Content = list;
            _stage.Mode = mode;
        }
        else
        {
            _stage.Content = list;
        }
    }

    private void OnChosen(FilterMenuList.Row row, bool keyboard)
    {
        if (_field is null)
        {
            var field = Fields?.FirstOrDefault(f => f.Id == row.Key);
            if (field is null)
            {
                return;
            }
            _field = field;
            _title!.Direction = 1;
            _title.Content = field.Label;
            _backHost!.IsPresent = true;
            ShowStep(ValueRows(field), direction: 1, animate: true);
            FocusStep(keyboard, null);
            return;
        }
        var chosenField = _field;
        var option = chosenField.Options.FirstOrDefault(o => o.Value == row.Key);
        var chip = new FilterChip(chosenField.Id, chosenField.Label, option?.Text ?? row.Label);
        Close(restoreFocus: true, keyboard: keyboard);
        RaiseEvent(new FilterMenuSelectedEventArgs(FilterSelectedEvent, chip, chosenField));
    }

    private void GoBack(bool keyboard)
    {
        if (_field is null)
        {
            return;
        }
        var key = _field.Id;
        _field = null;
        _title!.Direction = -1;
        _title.Content = Label;
        _backHost!.IsPresent = false;
        ShowStep(FieldRows(), direction: -1, animate: true);
        FocusStep(keyboard, key);
    }

    private void FocusStep(bool keyboard, string? key)
    {
        var list = _list!;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(list, _list))
            {
                return;
            }
            var index = key is null ? Math.Max(0, list.Rows.ToList().FindIndex(r => r.Checked)) : Math.Max(0, list.IndexOf(key));
            list.FocusRow(index, keyboard);
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    #region Popup

    private double PanelWidth => _panelWidth;
    private double ItemHeight => _itemHeight;
    private double PanelRadius => _panelRadius;
    private double _panelWidth = 256;
    private double _itemHeight = 36;
    private double _panelRadius = 26;

    private void EnsurePopup()
    {
        if (_popup is not null)
        {
            return;
        }
        _panelRadius = ArcTokens.Get<CornerRadius>(this, ArcDesignTokenKind.RadiusPanel).TopLeft;
        _itemHeight = ArcTokens.Get<double>(this, ArcDesignTokenKind.ControlSizeSM);
        _back = new FilterToolbarIconButton(ArcIcons.ChevronLeft, 28, 16, 1.8, 1, ArcDesignTokenKind.SurfaceMuted)
        {
            Margin = new Thickness(-6, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(_back, BackLabel);
        _back.Clicked = GoBack;
        _backHost = new PresenceHost
        {
            Mode = PresenceMode.Pop,
            Collapse = PresenceCollapse.Width,
            EnterScale = 0.6,
            Spring = ArcSpring.Snappy,
            IsPresent = false,
            Child = _back,
            VerticalAlignment = VerticalAlignment.Center
        };
        _title = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = Label
        };
        ArcTokens.Bind(_title, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_title, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_title, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var head = new DockPanel
        {
            Height = 32,
            Margin = new Thickness(10, 0, 8, 0),
            Children = { _backHost, _title }
        };
        DockPanel.SetDock(_backHost, Dock.Left);
        _stage = new ArcContentSwap { Mode = ContentSwapMode.Slide, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var scroller = new ScrollViewer
        {
            Content = _stage,
            MaxHeight = 320,
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var content = new StackPanel { Children = { head, scroller } };
        _surface = new Border
        {
            Width = PanelWidth,
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_panelRadius),
            Child = content
        };
        ArcTokens.Bind(_surface, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(_surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_surface, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        AutomationProperties.SetName(_surface, Label);
        _surface.AddHandler(KeyDownEvent, OnPanelKeyDown, RoutingStrategies.Bubble);
        _popup = new ArcPopup
        {
            Child = _surface,
            PlacementTarget = this,
            EnterOffset = 5,
            EnterScale = 0.96,
            MarginToAnchor = 8,
            IsLightDismissEnabled = true
        };
        _popup.Closed += OnPopupClosed;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        SetOpen(false);
        if (_popup is { IsOpen: false } popup && popup.Parent is not null)
        {
            ((ISetLogicalParent)popup).SetParent(null);
        }
        _restoreFocus = false;
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(restoreFocus: true, keyboard: true);
        }
        else if (e.Key == Key.Left && _field is not null)
        {
            e.Handled = true;
            GoBack(true);
        }
        else if (e.Key is Key.Down or Key.Up && _list is { } list && !list.IsFocused)
        {
            e.Handled = true;
            list.FocusRow(e.Key == Key.Down ? 0 : list.Rows.Count - 1, true);
        }
    }

    #endregion

    #region Trigger

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            PseudoClasses.Set(PressedPseudoClass, true);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        PseudoClasses.Set(PressedPseudoClass, false);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)) && !IsOpen)
        {
            e.Handled = true;
            Open(keyboard: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || IsOpen)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Down:
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                Open(keyboard: true);
                break;
            case Key.Up:
                e.Handled = true;
                Open(keyboard: true, focusLast: true);
                break;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty && !_syncing)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close();
            }
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == BackLabelProperty && _back is not null)
        {
            AutomationProperties.SetName(_back, BackLabel);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AutomationProperties.SetName(this, Label);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_popup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerPeer(this);

    private sealed class TriggerPeer : ControlAutomationPeer
    {
        public TriggerPeer(FilterMenu owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }

    #endregion
}
