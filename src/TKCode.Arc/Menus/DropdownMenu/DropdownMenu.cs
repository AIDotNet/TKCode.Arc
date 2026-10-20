using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A focused list of actions anchored to a labelled trigger (Arc dropdown menu). The trigger shows an optional icon,
/// the <see cref="Label"/> (a new label rises in from a soft blur while the trigger width springs) and a chevron that
/// turns on the CSS spring while open. The menu grows from the trigger edge on Arc's 580ms CSS spring with a 160ms fade,
/// one highlight glides between rows for the pointer and jumps for the keyboard, and rows fade in with a short stagger.
/// <para>
/// Rows are declared inline (<c>&lt;arc:MenuEntry Header="Rename" Icon="{x:Static arc:ArcIcons.Pencil}"/&gt;</c>) or
/// supplied with <see cref="ItemsSource"/>. Replace the built-in trigger with <see cref="Trigger"/> (any control; buttons
/// toggle the menu on click and never press-scale). Enter, Space or ArrowDown on the trigger open the menu on the first
/// row; Escape closes it and returns focus to the trigger.
/// </para>
/// </summary>
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[TemplatePart(TriggerPresenterPart, typeof(ContentPresenter))]
[PseudoClasses(OpenPseudoClass, CustomTriggerPseudoClass)]
public class DropdownMenu : TemplatedControl
{
    public const string TriggerPart = "PART_Trigger";
    public const string TriggerPresenterPart = "PART_TriggerPresenter";
    public const string OpenPseudoClass = ":open";
    public const string CustomTriggerPseudoClass = ":custom-trigger";

    #region 公共属性定义

    /// <summary>
    /// Turns any control into a dropdown trigger for a <see cref="MenuSurface"/>: clicks (buttons) or taps toggle the
    /// menu, Enter / Space / ArrowDown open it on the first row, ArrowUp on the last, Escape returns focus to the
    /// control, and Arc buttons stop press-scaling while they anchor the menu. Used by split buttons, button groups and
    /// row "more" buttons: <c>&lt;arc:Button arc:DropdownMenu.Menu="{StaticResource RowMenu}"/&gt;</c>.
    /// </summary>
    public static readonly AttachedProperty<MenuSurface?> MenuProperty =
        AvaloniaProperty.RegisterAttached<DropdownMenu, Control, MenuSurface?>("Menu");

    public static MenuSurface? GetMenu(Control element) => element.GetValue(MenuProperty);

    public static void SetMenu(Control element, MenuSurface? value) => element.SetValue(MenuProperty, value);

    /// <summary>Trigger text (Arc: required). Changing it morphs the trigger.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<DropdownMenu, string?>(nameof(Label), "Actions");

    /// <summary>Leading icon of the built-in trigger (a 24x24 Lucide geometry such as <c>ArcIcons.Settings</c>).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DropdownMenu, Geometry?>(nameof(Icon));

    /// <summary>Custom trigger content that replaces the built-in button (the menu anchors to it).</summary>
    public static readonly StyledProperty<object?> TriggerProperty =
        AvaloniaProperty.Register<DropdownMenu, object?>(nameof(Trigger));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<DropdownMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<MenuSide> SideProperty =
        AvaloniaProperty.Register<DropdownMenu, MenuSide>(nameof(Side), MenuSide.Bottom);

    /// <summary>Menu alignment against the trigger (Arc: end).</summary>
    public static readonly StyledProperty<MenuAlignment> AlignmentProperty =
        AvaloniaProperty.Register<DropdownMenu, MenuAlignment>(nameof(Alignment), MenuAlignment.End);

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<DropdownMenu, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<DropdownMenu, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly RoutedEvent<MenuEntryClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<DropdownMenu, MenuEntryClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<DropdownMenu, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Direct);

    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<DropdownMenu, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Direct);

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public MenuSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public MenuAlignment Alignment
    {
        get => GetValue(AlignmentProperty);
        set => SetValue(AlignmentProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Inline rows (<see cref="MenuEntry"/>, <see cref="MenuEntrySeparator"/>, <see cref="MenuEntryLabel"/>).</summary>
    [Content]
    public ItemCollection Items => Menu.Items;

    /// <summary>The menu surface (for advanced configuration such as <see cref="MenuSurface.AnchorGap"/>).</summary>
    public MenuSurface Menu { get; }

    /// <summary>Raised when a row is activated.</summary>
    public event EventHandler<MenuEntryClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    private AvaloniaButton? _trigger;
    private ContentPresenter? _triggerPresenter;
    private Control? _customTrigger;

    private static readonly AttachedProperty<DropdownMenuAttachment?> AttachmentProperty =
        AvaloniaProperty.RegisterAttached<DropdownMenu, Control, DropdownMenuAttachment?>("Attachment");

    static DropdownMenu()
    {
        MenuProperty.Changed.AddClassHandler<Control>(OnAttachedMenuChanged);
    }

    private static void OnAttachedMenuChanged(Control trigger, AvaloniaPropertyChangedEventArgs e)
    {
        trigger.GetValue(AttachmentProperty)?.Dispose();
        trigger.ClearValue(AttachmentProperty);
        if (trigger is not DropdownMenu && e.NewValue is MenuSurface surface)
        {
            trigger.SetValue(AttachmentProperty, new DropdownMenuAttachment(trigger, surface));
        }
    }

    public DropdownMenu()
    {
        Menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.End, AnchorGap = 6 };
        Menu.ItemClicked += OnMenuItemClicked;
        Menu.Opened += (_, _) => RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
        Menu.Closed += (_, _) => RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        Menu.PropertyChanged += OnMenuPropertyChanged;
        AutomationProperties.SetName(Menu, "Actions");
    }

    /// <summary>The control the menu is anchored to (the built-in trigger or the custom <see cref="Trigger"/>).</summary>
    public Control? Anchor => _customTrigger ?? _trigger;

    /// <summary>Opens the menu; with <paramref name="focusFirst"/> the highlight starts on the first row.</summary>
    public void Open(bool focusFirst = false)
    {
        if (Anchor is { } anchor)
        {
            Menu.Open(anchor, focusFirst ? MenuOpenFocus.First : MenuOpenFocus.Surface);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    /// <summary>Closes the menu (focus returns to the trigger when it was inside the menu).</summary>
    public void Close() => Menu.Close(returnFocus: false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            SetMenu(_trigger, null);
        }
        if (_triggerPresenter is not null)
        {
            _triggerPresenter.PropertyChanged -= OnTriggerPresenterPropertyChanged;
        }
        _trigger = e.NameScope.Find<AvaloniaButton>(TriggerPart);
        _triggerPresenter = e.NameScope.Find<ContentPresenter>(TriggerPresenterPart);
        if (_trigger is not null)
        {
            SetMenu(_trigger, Menu);
            AutomationProperties.SetName(_trigger, Label);
        }
        if (_triggerPresenter is not null)
        {
            _triggerPresenter.PropertyChanged += OnTriggerPresenterPropertyChanged;
        }
        AttachCustomTrigger(_triggerPresenter?.Child);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // A menu that starts open does not steal focus.
        if (IsOpen && !Menu.IsOpen && Anchor is { } anchor)
        {
            Menu.Open(anchor, MenuOpenFocus.None);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (open && !Menu.IsOpen)
            {
                if (Anchor is { } anchor && TopLevel.GetTopLevel(anchor) is not null)
                {
                    Menu.Open(anchor);
                }
            }
            else if (!open && Menu.IsOpen)
            {
                Menu.Close(returnFocus: false);
            }
        }
        else if (change.Property == ItemsSourceProperty)
        {
            Menu.ItemsSource = change.GetNewValue<IEnumerable?>();
        }
        else if (change.Property == ItemTemplateProperty)
        {
            Menu.ItemTemplate = change.GetNewValue<IDataTemplate?>();
        }
        else if (change.Property == SideProperty)
        {
            Menu.Side = change.GetNewValue<MenuSide>();
        }
        else if (change.Property == AlignmentProperty)
        {
            Menu.Alignment = change.GetNewValue<MenuAlignment>();
        }
        else if (change.Property == LabelProperty)
        {
            var label = change.GetNewValue<string?>();
            AutomationProperties.SetName(Menu, label);
            if (_trigger is not null)
            {
                AutomationProperties.SetName(_trigger, label);
            }
        }
        else if (change.Property == TriggerProperty)
        {
            PseudoClasses.Set(CustomTriggerPseudoClass, change.NewValue is not null);
        }
    }

    private void OnTriggerPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ContentPresenter.ChildProperty)
        {
            AttachCustomTrigger(e.GetNewValue<Control?>());
        }
    }

    private void AttachCustomTrigger(Control? trigger)
    {
        var next = Trigger is null ? null : trigger;
        if (ReferenceEquals(next, _customTrigger))
        {
            return;
        }
        if (_customTrigger is not null)
        {
            SetMenu(_customTrigger, null);
        }
        _customTrigger = next;
        if (_customTrigger is not null)
        {
            // Clicks toggle, keys open on a row, and Arc buttons stop press-scaling while they anchor the menu.
            SetMenu(_customTrigger, Menu);
        }
    }

    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MenuSurface.IsOpenProperty)
        {
            SetCurrentValue(IsOpenProperty, e.GetNewValue<bool>());
        }
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        RaiseEvent(new MenuEntryClickedEventArgs(ItemClickedEvent, e.Entry, e.Item));
    }

    /// <summary>Trigger wiring of <see cref="MenuProperty"/> on an arbitrary control.</summary>
    private sealed class DropdownMenuAttachment : IDisposable
    {
        private readonly Control _trigger;
        private readonly MenuSurface _surface;
        private bool _keyboardActivation;

        public DropdownMenuAttachment(Control trigger, MenuSurface surface)
        {
            _trigger = trigger;
            _surface = surface;
            if (trigger is Button arcButton)
            {
                arcButton.IsPressScaleEnabled = false;
            }
            if (trigger is AvaloniaButton button)
            {
                button.Click += OnClick;
            }
            else
            {
                trigger.AddHandler(TappedEvent, OnTapped);
            }
            trigger.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            trigger.DetachedFromVisualTree += OnDetached;
        }

        private void OnClick(object? sender, RoutedEventArgs e)
        {
            var focus = _keyboardActivation ? MenuOpenFocus.First : MenuOpenFocus.Surface;
            _keyboardActivation = false;
            _surface.Toggle(_trigger, focus);
        }

        private void OnTapped(object? sender, TappedEventArgs e) => _surface.Toggle(_trigger);

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Down:
                case Key.Up:
                    e.Handled = true;
                    if (!_surface.IsOpen)
                    {
                        _surface.Open(_trigger, e.Key == Key.Down ? MenuOpenFocus.First : MenuOpenFocus.Last);
                    }
                    break;
                case Key.Enter:
                case Key.Space:
                    _keyboardActivation = true;
                    if (_trigger is not AvaloniaButton)
                    {
                        e.Handled = true;
                        _surface.Toggle(_trigger, MenuOpenFocus.First);
                    }
                    break;
            }
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => _surface.Close(returnFocus: false);

        public void Dispose()
        {
            _surface.Close(returnFocus: false);
            if (_trigger is AvaloniaButton button)
            {
                button.Click -= OnClick;
            }
            else
            {
                _trigger.RemoveHandler(TappedEvent, OnTapped);
            }
            _trigger.RemoveHandler(KeyDownEvent, OnKeyDown);
            _trigger.DetachedFromVisualTree -= OnDetached;
        }
    }
}
