using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Secondary actions kept close to the selected object (Arc context menu). Wraps its <see cref="ContentControl.Content"/>
/// and opens a menu at the pointer on right-click, or under the target (12px in, 8px below) on a plain click, a tap,
/// the ContextMenu key or Shift+F10. The menu grows from the pointer on a snappy spring, rows press with a tint, and it
/// closes on any scroll, resize or outside press instead of repositioning. Avoid it on targets that need their own click.
/// <para>
/// To give any control a right-click menu without wrapping it, attach a surface:
/// <c>&lt;Border&gt;&lt;arc:ContextMenu.Menu&gt;&lt;arc:MenuSurface&gt;…&lt;/arc:MenuSurface&gt;&lt;/arc:ContextMenu.Menu&gt;&lt;/Border&gt;</c>.
/// </para>
/// </summary>
public class ContextMenu : ContentControl
{
    #region 公共属性定义

    /// <summary>
    /// Attaches a right-click (and ContextMenu key / Shift+F10) menu to any control. The surface becomes a
    /// <see cref="MenuSurfaceVariant.Context"/> menu unless its variant was set explicitly.
    /// </summary>
    public static readonly AttachedProperty<MenuSurface?> MenuProperty =
        AvaloniaProperty.RegisterAttached<ContextMenu, Control, MenuSurface?>("Menu");

    /// <summary>Accessible name of both the target and the menu (Arc default: "Context menu").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ContextMenu, string?>(nameof(Label), "Context menu");

    /// <summary>Whether the menu is open; binds two-way by default.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ContextMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether a plain click or tap on the target opens the menu (Arc: true).</summary>
    public static readonly StyledProperty<bool> OpensOnClickProperty =
        AvaloniaProperty.Register<ContextMenu, bool>(nameof(OpensOnClick), true);

    /// <summary>Data items generated as menu rows; <see cref="ItemTemplate"/> defines how each row looks.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ContextMenu, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Template that builds each menu row from an <see cref="ItemsSource"/> item.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<ContextMenu, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>
    /// Children of an <see cref="ItemsSource"/> item: a non-empty result makes its row open a submenu of those children
    /// (same signature as <c>TreeView.ChildrenSelector</c>; applied at every level).
    /// </summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<ContextMenu, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Raised when a menu row is clicked; the argument carries the clicked entry and item.</summary>
    public static readonly RoutedEvent<MenuEntryClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<ContextMenu, MenuEntryClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    /// <summary>Raised when the menu opens.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<ContextMenu, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Direct);

    /// <summary>Raised when the menu closes.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<ContextMenu, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Direct);

    public static MenuSurface? GetMenu(Control element) => element.GetValue(MenuProperty);

    public static void SetMenu(Control element, MenuSurface? value) => element.SetValue(MenuProperty, value);

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool OpensOnClick
    {
        get => GetValue(OpensOnClickProperty);
        set => SetValue(OpensOnClickProperty, value);
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <inheritdoc cref="ChildrenSelectorProperty"/>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => GetValue(ChildrenSelectorProperty);
        set => SetValue(ChildrenSelectorProperty, value);
    }

    /// <summary>The menu rows (declare them under <c>&lt;arc:ContextMenu.Items&gt;</c>; the element content is the target).</summary>
    public ItemCollection Items => Menu.Items;

    /// <summary>The menu surface.</summary>
    public MenuSurface Menu { get; }

    /// <summary>Occurs when a menu row is clicked.</summary>
    public event EventHandler<MenuEntryClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    /// <summary>Occurs when the menu opens.</summary>
    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>Occurs when the menu closes.</summary>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    #endregion

    private const double AnchorInset = 12;
    private const double AnchorGap = 8;

    private static readonly AttachedProperty<ContextMenuAttachment?> AttachmentProperty =
        AvaloniaProperty.RegisterAttached<ContextMenu, Control, ContextMenuAttachment?>("Attachment");

    private bool _pressedWhileOpen;

    static ContextMenu()
    {
        FocusableProperty.OverrideDefaultValue<ContextMenu>(true);
        MenuProperty.Changed.AddClassHandler<Control>(OnAttachedMenuChanged);
    }

    public ContextMenu()
    {
        Menu = new MenuSurface
        {
            Variant = MenuSurfaceVariant.Context,
            ClosesOnViewportChange = true,
            FocusReturnTarget = this
        };
        Menu.ItemClicked += (_, e) => RaiseEvent(new MenuEntryClickedEventArgs(ItemClickedEvent, e.Entry, e.Item));
        Menu.Opened += (_, _) => RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
        Menu.Closed += (_, _) => RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        Menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == MenuSurface.IsOpenProperty)
            {
                SetCurrentValue(IsOpenProperty, e.GetNewValue<bool>());
            }
        };
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetName(Menu, Label);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        AddHandler(PointerPressedEvent, OnTargetPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(TappedEvent, OnTargetTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>Opens the menu at <paramref name="position"/> (target coordinates), or under the target when null.</summary>
    public void Open(Point? position = null, MenuOpenFocus focus = MenuOpenFocus.Surface)
    {
        Menu.OpenAt(this, position ?? BelowTarget(this), focus);
    }

    /// <summary>Closes the menu.</summary>
    public void Close() => Menu.Close(returnFocus: false);

    internal static Point BelowTarget(Control target) => new(AnchorInset, target.Bounds.Height + AnchorGap);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
            AutomationProperties.SetName(Menu, change.GetNewValue<string?>());
        }
        else if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            if (open && !Menu.IsOpen && TopLevel.GetTopLevel(this) is not null)
            {
                Open();
            }
            else if (!open && Menu.IsOpen)
            {
                Close();
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
        else if (change.Property == ChildrenSelectorProperty)
        {
            Menu.ChildrenSelector = change.GetNewValue<Func<object, IEnumerable?>?>();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !ReferenceEquals(e.Source, this))
        {
            return;
        }
        if (e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            e.Handled = true;
            Open(null, MenuOpenFocus.First);
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        e.Handled = true;
        if (e.TryGetPosition(this, out var point))
        {
            Open(point);
        }
        else if (!Menu.IsOpen)
        {
            Open(null, MenuOpenFocus.First);
        }
    }

    private void OnTargetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A primary press on the target while the menu is open closes it; the click that follows must not reopen it.
        var properties = e.GetCurrentPoint(this).Properties;
        _pressedWhileOpen = Menu.IsOpen && properties.IsLeftButtonPressed;
        if (_pressedWhileOpen)
        {
            Close();
        }
    }

    private void OnTargetTapped(object? sender, TappedEventArgs e)
    {
        if (_pressedWhileOpen)
        {
            _pressedWhileOpen = false;
            return;
        }
        if (OpensOnClick && !Menu.IsOpen)
        {
            Open();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ContextMenuAutomationPeer(this);

    private sealed class ContextMenuAutomationPeer : ControlAutomationPeer, Avalonia.Automation.Provider.IExpandCollapseProvider
    {
        public ContextMenuAutomationPeer(ContextMenu owner)
            : base(owner)
        {
        }

        private ContextMenu Target => (ContextMenu)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        public ExpandCollapseState ExpandCollapseState => Target.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand() => Target.Open(null, MenuOpenFocus.First);

        public void Collapse() => Target.Close();
    }

    #region Attached menus

    private static void OnAttachedMenuChanged(Control target, AvaloniaPropertyChangedEventArgs e)
    {
        target.GetValue(AttachmentProperty)?.Dispose();
        target.ClearValue(AttachmentProperty);
        if (target is ContextMenu || e.NewValue is not MenuSurface surface)
        {
            return;
        }
        target.SetValue(AttachmentProperty, new ContextMenuAttachment(target, surface));
    }

    /// <summary>Right-click wiring of <see cref="MenuProperty"/> on an arbitrary control.</summary>
    private sealed class ContextMenuAttachment : IDisposable
    {
        private readonly Control _target;
        private readonly MenuSurface _surface;

        public ContextMenuAttachment(Control target, MenuSurface surface)
        {
            _target = target;
            _surface = surface;
            if (!surface.IsSet(MenuSurface.VariantProperty))
            {
                surface.Variant = MenuSurfaceVariant.Context;
            }
            if (!surface.IsSet(MenuSurface.ClosesOnViewportChangeProperty))
            {
                surface.ClosesOnViewportChange = true;
            }
            target.AddHandler(ContextRequestedEvent, OnContextRequested);
            target.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            target.DetachedFromVisualTree += OnDetached;
        }

        private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            e.Handled = true;
            if (e.TryGetPosition(_target, out var point))
            {
                _surface.OpenAt(_target, point);
            }
            else if (!_surface.IsOpen)
            {
                _surface.OpenAt(_target, BelowTarget(_target), MenuOpenFocus.First);
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_surface.IsOpen && e.GetCurrentPoint(_target).Properties.IsLeftButtonPressed)
            {
                _surface.Close(returnFocus: false);
            }
        }

        private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => _surface.Close(returnFocus: false);

        public void Dispose()
        {
            _surface.Close(returnFocus: false);
            _target.RemoveHandler(ContextRequestedEvent, OnContextRequested);
            _target.RemoveHandler(PointerPressedEvent, OnPointerPressed);
            _target.DetachedFromVisualTree -= OnDetached;
        }
    }

    #endregion
}
