using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Visual weight of a <see cref="SplitButton"/>.</summary>
public enum SplitButtonVariant
{
    /// <summary>The foreground fill of a primary button (default).</summary>
    Primary,

    /// <summary>A compact outlined pill: 36px tall, 12px text, a 142px minimum main half.</summary>
    Secondary
}

/// <summary>
/// One default action joined to a chevron menu of nearby alternatives (Arc split button): Merge pull request with Squash
/// and merge or Rebase and merge, an export with its usual format, Copy page that answers Copied in place.
/// <para>
/// The whole pill presses in for the main action; the chevron half anchors the menu, so it answers in color only and
/// turns while the menu is open. The main label morphs letter by letter (shared runs glide, changed letters rise out of a
/// soft blur), a new <see cref="Icon"/> grows in while the old one shrinks away, and the main half springs to the new
/// width. Menu rows are <see cref="MenuEntry"/> items declared inline or supplied with <see cref="ItemsSource"/>; the menu
/// opens 4px below the chevron, aligned to the end.
/// </para>
/// <para>
/// Keyboard: Enter or Space runs the main action on the main half and opens the menu on the first row from the chevron
/// (ArrowDown too); arrow keys move through the rows and loop, Escape closes the menu and returns focus to the chevron.
/// Label changes are announced politely.
/// </para>
/// </summary>
[TemplatePart(PrimaryButtonPart, typeof(Button))]
[TemplatePart(MenuButtonPart, typeof(Button))]
[TemplatePart(IconPart, typeof(Control))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(OpenPseudoClass, PrimaryPressedPseudoClass, SecondaryPseudoClass, HasIconPseudoClass)]
public class SplitButton : TemplatedControl
{
    public const string PrimaryButtonPart = "PART_PrimaryButton";
    public const string MenuButtonPart = "PART_MenuButton";
    public const string IconPart = "PART_Icon";
    public const string StatusPart = "PART_Status";
    public const string OpenPseudoClass = ":open";
    public const string PrimaryPressedPseudoClass = ":primary-pressed";
    public const string SecondaryPseudoClass = ":secondary";
    public const string HasIconPseudoClass = ":has-icon";

    #region 公共属性定义

    /// <summary>Main label (Arc: required). A change morphs letter by letter and is announced politely.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SplitButton, string>(nameof(Label), "Merge pull request");

    /// <summary>Leading icon of the main half (a 24×24 Lucide geometry such as <c>ArcIcons.Copy</c>); a new icon crossfades in.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SplitButton, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<SplitButtonVariant> VariantProperty =
        AvaloniaProperty.Register<SplitButton, SplitButtonVariant>(nameof(Variant));

    /// <summary>Command of the main action.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SplitButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<SplitButton, object?>(nameof(CommandParameter));

    /// <summary>Whether the menu is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<SplitButton, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Accessible name of the chevron half; null uses "{Label} more actions".</summary>
    public static readonly StyledProperty<string?> MenuButtonLabelProperty =
        AvaloniaProperty.Register<SplitButton, string?>(nameof(MenuButtonLabel));

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<SplitButton, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<SplitButton, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>Raised when the main half is activated (before <see cref="Command"/> runs).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<SplitButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    /// <summary>Raised when a menu row is activated.</summary>
    public static readonly RoutedEvent<MenuEntryClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<SplitButton, MenuEntryClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public SplitButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string? MenuButtonLabel
    {
        get => GetValue(MenuButtonLabelProperty);
        set => SetValue(MenuButtonLabelProperty, value);
    }

    /// <summary>Menu rows from data (<see cref="MenuEntry"/> instances are used as is; other items become row headers).</summary>
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

    /// <summary>Inline menu rows (<see cref="MenuEntry"/>, <see cref="MenuEntrySeparator"/>, <see cref="MenuEntryLabel"/>).</summary>
    [Content]
    public ItemCollection Items => Menu.Items;

    /// <summary>The menu surface (for advanced configuration).</summary>
    public MenuSurface Menu { get; }

    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public event EventHandler<MenuEntryClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    #endregion

    private Button? _primary;
    private ButtonsChevronTrigger? _menuButton;
    private ButtonsGlyphSwap? _icon;
    private ButtonsLiveRegion? _status;

    public SplitButton()
    {
        Menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.End, AnchorGap = 4 };
        Menu.ItemClicked += OnMenuItemClicked;
        Menu.PropertyChanged += OnMenuPropertyChanged;
    }

    /// <summary>The chevron half the menu is anchored to (null before the template is applied).</summary>
    public Control? MenuAnchor => _menuButton;

    /// <summary>Opens the menu; with <paramref name="focusFirst"/> the highlight starts on the first row.</summary>
    public void Open(bool focusFirst = false)
    {
        if (_menuButton is { } anchor)
        {
            Menu.Open(anchor, focusFirst ? MenuOpenFocus.First : MenuOpenFocus.Surface);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    /// <summary>Closes the menu.</summary>
    public void Close() => Menu.Close(returnFocus: false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_primary is not null)
        {
            _primary.Click -= OnPrimaryClick;
            _primary.PropertyChanged -= OnPrimaryPropertyChanged;
        }
        if (_menuButton is not null)
        {
            _menuButton.Menu = null;
        }
        _primary = e.NameScope.Find<Button>(PrimaryButtonPart);
        _menuButton = e.NameScope.Find<ButtonsChevronTrigger>(MenuButtonPart);
        _icon = e.NameScope.Find<ButtonsGlyphSwap>(IconPart);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
        if (_primary is not null)
        {
            _primary.Click += OnPrimaryClick;
            _primary.PropertyChanged += OnPrimaryPropertyChanged;
        }
        if (_menuButton is not null)
        {
            _menuButton.Menu = Menu;
        }
        UpdateNames();
        UpdateVariant();
        ShowIcon();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // A menu that starts open does not steal focus.
        if (IsOpen && !Menu.IsOpen && _menuButton is { } anchor)
        {
            Menu.Open(anchor, MenuOpenFocus.None);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            UpdateNames();
            if (IsLoaded)
            {
                _status?.Announce(change.GetNewValue<string>());
            }
        }
        else if (change.Property == MenuButtonLabelProperty)
        {
            UpdateNames();
        }
        else if (change.Property == IconProperty)
        {
            ShowIcon();
        }
        else if (change.Property == VariantProperty)
        {
            UpdateVariant();
        }
        else if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (open && !Menu.IsOpen && _menuButton is { } anchor && TopLevel.GetTopLevel(anchor) is not null)
            {
                Menu.Open(anchor);
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
    }

    private string MenuName => MenuButtonLabel ?? $"{Label} more actions";

    private void UpdateNames()
    {
        AutomationProperties.SetName(Menu, MenuName);
        if (_primary is not null)
        {
            AutomationProperties.SetName(_primary, Label);
        }
        if (_menuButton is not null)
        {
            AutomationProperties.SetName(_menuButton, MenuName);
        }
    }

    private void UpdateVariant()
    {
        var secondary = Variant == SplitButtonVariant.Secondary;
        PseudoClasses.Set(SecondaryPseudoClass, secondary);
        _primary?.Classes.Set("secondary", secondary);
        _menuButton?.Classes.Set("secondary", secondary);
    }

    private void ShowIcon()
    {
        PseudoClasses.Set(HasIconPseudoClass, Icon is not null);
        if (_icon is null)
        {
            return;
        }
        var glyph = Icon is { } data
            ? new ArcGlyph
            {
                Data = data,
                Size = 16,
                StrokeThickness = 1.75
            }
            : null;
        // The new glyph grows from 0.6 out of a subtle blur on the snappy spring; the old one shrinks away (fast).
        _icon.Show(glyph, ButtonsGlyphPose.Pop, ButtonsGlyphPose.Pop);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
    }

    private void OnPrimaryPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AvaloniaButton.IsPressedProperty)
        {
            // The whole pill presses in for the main action; the chevron half never scales.
            PseudoClasses.Set(PrimaryPressedPseudoClass, e.GetNewValue<bool>());
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
}
