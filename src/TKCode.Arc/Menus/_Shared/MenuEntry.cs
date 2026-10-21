using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="MenuSurface"/>: a leading icon (a Lucide <see cref="Geometry"/> such as
/// <c>{x:Static arc:ArcIcons.Pencil}</c>, or any control), a header, an optional shortcut hint and the check or radio
/// indicator of toggle rows. Rows never draw their own hover or focus fill: the surface's shared highlight glides to
/// the row under the pointer and jumps to the keyboard position. Activation runs <see cref="Command"/>, raises
/// <see cref="Click"/> and the surface's <see cref="MenuSurface.ItemClicked"/>, then closes the menu unless
/// <see cref="StaysOpenOnClick"/> is set.
/// </summary>
[TemplatePart(RowPart, typeof(Border))]
[PseudoClasses(HighlightedPseudoClass, PressedPseudoClass, CheckedPseudoClass)]
public class MenuEntry : TemplatedControl
{
    public const string RowPart = "PART_Row";
    public const string HighlightedPseudoClass = ":highlighted";
    public const string PressedPseudoClass = ":pressed";
    public const string CheckedPseudoClass = ":checked";

    #region 公共属性定义

    /// <summary>The row label (strings ellipsize; any content is accepted). Also the typeahead text unless <c>TextSearch.Text</c> is set.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<MenuEntry, object?>(nameof(Header));

    /// <summary>Data template that renders the header; without one, a string header is drawn as ellipsizing text.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<MenuEntry, IDataTemplate?>(nameof(HeaderTemplate));

    /// <summary>Leading icon: a 24x24 Lucide <see cref="Geometry"/> (drawn as a 16px stroke glyph) or any control.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<MenuEntry, object?>(nameof(Icon));

    /// <summary>Shortcut shown as a quiet hint at the row end (display only; bind the key with a hot key yourself).</summary>
    public static readonly StyledProperty<KeyGesture?> InputGestureProperty =
        AvaloniaProperty.Register<MenuEntry, KeyGesture?>(nameof(InputGesture));

    /// <summary>Free-form shortcut hint (for example "⌘ ,"); wins over <see cref="InputGesture"/>.</summary>
    public static readonly StyledProperty<string?> InputGestureTextProperty =
        AvaloniaProperty.Register<MenuEntry, string?>(nameof(InputGestureText));

    /// <summary>Command run when the row is activated; the row is disabled while the command cannot execute.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MenuEntry, ICommand?>(nameof(Command));

    /// <summary>Parameter passed to Command when the row is activated.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MenuEntry, object?>(nameof(CommandParameter));

    /// <summary>Danger text and icon, and a danger-tinted highlight. Put destructive rows last and name what they destroy.</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<MenuEntry, bool>(nameof(IsDestructive));

    /// <summary>How the row toggles when activated: None for a command row, CheckBox, or Radio.</summary>
    public static readonly StyledProperty<MenuEntryToggleType> ToggleTypeProperty =
        AvaloniaProperty.Register<MenuEntry, MenuEntryToggleType>(nameof(ToggleType));

    /// <summary>Checked state (two-way). Rows without an icon show a check (or a dot for radio rows) when checked.</summary>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<MenuEntry, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Radio rows with the same group name (within one surface) are mutually exclusive.</summary>
    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<MenuEntry, string?>(nameof(GroupName));

    /// <summary>Draws a separator line above the row (Arc's <c>separatorBefore</c>).</summary>
    public static readonly StyledProperty<bool> HasSeparatorBeforeProperty =
        AvaloniaProperty.Register<MenuEntry, bool>(nameof(HasSeparatorBefore));

    /// <summary>Keeps the menu open after activation (toggles that people flip several times).</summary>
    public static readonly StyledProperty<bool> StaysOpenOnClickProperty =
        AvaloniaProperty.Register<MenuEntry, bool>(nameof(StaysOpenOnClick));

    /// <summary>Inherited from the owning <see cref="MenuSurface"/>; selects the row geometry.</summary>
    public static readonly StyledProperty<MenuSurfaceVariant> VariantProperty =
        MenuSurface.VariantProperty.AddOwner<MenuEntry>();

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MenuEntry, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    internal static readonly DirectProperty<MenuEntry, Geometry?> IconGeometryProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, Geometry?>(nameof(IconGeometry), o => o.IconGeometry);

    internal static readonly DirectProperty<MenuEntry, object?> IconContentProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, object?>(nameof(IconContent), o => o.IconContent);

    internal static readonly DirectProperty<MenuEntry, bool> HasIconSlotProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, bool>(nameof(HasIconSlot), o => o.HasIconSlot);

    internal static readonly DirectProperty<MenuEntry, bool> ShowsRadioDotProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, bool>(nameof(ShowsRadioDot), o => o.ShowsRadioDot);

    internal static readonly DirectProperty<MenuEntry, bool> ShowsTrailingCheckProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, bool>(nameof(ShowsTrailingCheck), o => o.ShowsTrailingCheck);

    internal static readonly DirectProperty<MenuEntry, IDataTemplate?> EffectiveHeaderTemplateProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, IDataTemplate?>(nameof(EffectiveHeaderTemplate), o => o.EffectiveHeaderTemplate);

    internal static readonly DirectProperty<MenuEntry, string?> GestureTextProperty =
        AvaloniaProperty.RegisterDirect<MenuEntry, string?>(nameof(GestureText), o => o.GestureText);

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="HeaderTemplateProperty"/>
    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public KeyGesture? InputGesture
    {
        get => GetValue(InputGestureProperty);
        set => SetValue(InputGestureProperty, value);
    }

    public string? InputGestureText
    {
        get => GetValue(InputGestureTextProperty);
        set => SetValue(InputGestureTextProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public bool IsDestructive
    {
        get => GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    /// <inheritdoc cref="ToggleTypeProperty"/>
    public MenuEntryToggleType ToggleType
    {
        get => GetValue(ToggleTypeProperty);
        set => SetValue(ToggleTypeProperty, value);
    }

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public bool HasSeparatorBefore
    {
        get => GetValue(HasSeparatorBeforeProperty);
        set => SetValue(HasSeparatorBeforeProperty, value);
    }

    public bool StaysOpenOnClick
    {
        get => GetValue(StaysOpenOnClickProperty);
        set => SetValue(StaysOpenOnClickProperty, value);
    }

    public MenuSurfaceVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Raised when the row is activated (pointer release, Enter or Space), before the menu closes.</summary>
    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    #endregion

    private Geometry? _iconGeometry;
    private object? _iconContent;
    private bool _hasIconSlot;
    private bool _showsRadioDot;
    private bool _showsTrailingCheck;
    private string? _gestureText;
    private IDataTemplate? _effectiveHeaderTemplate;
    private static readonly MenuEntryTextTemplate s_textTemplate = new();
    private bool _commandCanExecute = true;
    private bool _pressed;
    private Border? _row;

    internal Geometry? IconGeometry
    {
        get => _iconGeometry;
        private set => SetAndRaise(IconGeometryProperty, ref _iconGeometry, value);
    }

    internal object? IconContent
    {
        get => _iconContent;
        private set => SetAndRaise(IconContentProperty, ref _iconContent, value);
    }

    internal bool HasIconSlot
    {
        get => _hasIconSlot;
        private set => SetAndRaise(HasIconSlotProperty, ref _hasIconSlot, value);
    }

    internal bool ShowsRadioDot
    {
        get => _showsRadioDot;
        private set => SetAndRaise(ShowsRadioDotProperty, ref _showsRadioDot, value);
    }

    internal bool ShowsTrailingCheck
    {
        get => _showsTrailingCheck;
        private set => SetAndRaise(ShowsTrailingCheckProperty, ref _showsTrailingCheck, value);
    }

    /// <summary>
    /// <see cref="HeaderTemplate"/>, or the ellipsizing text template for string headers. A control header gets no
    /// template: a content presenter with a template would show the control's type name instead of the control.
    /// </summary>
    internal IDataTemplate? EffectiveHeaderTemplate
    {
        get => _effectiveHeaderTemplate;
        private set => SetAndRaise(EffectiveHeaderTemplateProperty, ref _effectiveHeaderTemplate, value);
    }

    internal string? GestureText
    {
        get => _gestureText;
        private set => SetAndRaise(GestureTextProperty, ref _gestureText, value);
    }

    static MenuEntry()
    {
        FocusableProperty.OverrideDefaultValue<MenuEntry>(true);
        // The roving keyboard position lives in the surface; Tab leaves the menu instead of walking its rows.
        IsTabStopProperty.OverrideDefaultValue<MenuEntry>(false);
    }

    public MenuEntry()
    {
        UpdateIndicators();
    }

    /// <summary>The surface this row belongs to (null while it is not realized).</summary>
    internal MenuSurface? Surface => ItemsControl.ItemsControlFromItemContainer(this) as MenuSurface ?? this.FindLogicalAncestorOfType<MenuSurface>();

    /// <summary>The row part (excludes the separator above it); the highlight covers exactly this rectangle.</summary>
    internal Control RowVisual => _row ?? (Control)this;

    /// <summary>Rows the pointer highlights. Inline switches release the highlight instead (Arc user menu).</summary>
    internal virtual bool TracksPointerHighlight => true;

    /// <summary>Rows that Enter, Space and pointer release activate.</summary>
    internal virtual bool IsActivatable => true;

    /// <summary>Text used by typeahead.</summary>
    internal string? TypeaheadText => TextSearch.GetText(this) ?? Header as string ?? AutomationProperties.GetName(this);

    /// <summary>Gives a row the first chance at keys the surface does not consume itself (Left / Right in inline switches).</summary>
    internal virtual bool HandleMenuKey(KeyEventArgs e) => false;

    protected override bool IsEnabledCore => base.IsEnabledCore && _commandCanExecute;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _row = e.NameScope.Find<Border>(RowPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconProperty || change.Property == IsCheckedProperty ||
            change.Property == ToggleTypeProperty || change.Property == VariantProperty)
        {
            UpdateIndicators();
            if (change.Property == IsCheckedProperty)
            {
                PseudoClasses.Set(CheckedPseudoClass, IsChecked);
            }
        }
        else if (change.Property == HeaderProperty || change.Property == HeaderTemplateProperty)
        {
            EffectiveHeaderTemplate = HeaderTemplate ?? (Header is string ? s_textTemplate : null);
        }
        else if (change.Property == InputGestureProperty || change.Property == InputGestureTextProperty)
        {
            GestureText = InputGestureText ?? FormatGesture(InputGesture);
        }
        else if (change.Property == CommandProperty)
        {
            if (((ILogical)this).IsAttachedToLogicalTree)
            {
                if (change.OldValue is ICommand oldCommand)
                {
                    oldCommand.CanExecuteChanged -= OnCanExecuteChanged;
                }
                if (change.NewValue is ICommand newCommand)
                {
                    newCommand.CanExecuteChanged += OnCanExecuteChanged;
                }
            }
            UpdateCanExecute();
        }
        else if (change.Property == CommandParameterProperty)
        {
            UpdateCanExecute();
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        if (Command is { } command)
        {
            command.CanExecuteChanged += OnCanExecuteChanged;
        }
        UpdateCanExecute();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        if (Command is { } command)
        {
            command.CanExecuteChanged -= OnCanExecuteChanged;
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => UpdateCanExecute();

    private void UpdateCanExecute()
    {
        var canExecute = Command is not { } command || command.CanExecute(CommandParameter);
        if (canExecute != _commandCanExecute)
        {
            _commandCanExecute = canExecute;
            UpdateIsEffectivelyEnabled();
        }
    }

    private void UpdateIndicators()
    {
        var icon = Icon;
        var toggles = ToggleType != MenuEntryToggleType.None;
        var context = Variant == MenuSurfaceVariant.Context;
        var check = IsChecked && (toggles || icon is null);
        if (icon is Geometry geometry)
        {
            IconGeometry = geometry;
            IconContent = null;
        }
        else if (icon is not null)
        {
            IconGeometry = null;
            IconContent = icon;
        }
        else
        {
            IconContent = null;
            // Arc context menu: icon-less rows show a check when checked and a quiet ellipsis otherwise.
            IconGeometry = check && ToggleType != MenuEntryToggleType.Radio
                ? ArcIcons.Check
                : context && !IsChecked && !toggles ? ArcIcons.Ellipsis : null;
        }
        ShowsRadioDot = icon is null && IsChecked && ToggleType == MenuEntryToggleType.Radio;
        ShowsTrailingCheck = icon is not null && IsChecked && toggles;
        HasIconSlot = icon is not null || toggles || IsChecked || context;
    }

    internal static string? FormatGesture(KeyGesture? gesture)
    {
        if (gesture is null)
        {
            return null;
        }
        var text = gesture.ToString("p", null);
        var info = Avalonia.Input.Platform.KeyGestureFormatInfo.GetInstance(null);
        if (info.Meta.Length == 1 && info.Ctrl.Length == 1 && gesture.Key is not (Key.OemPlus or Key.Add))
        {
            // Symbolic platforms (macOS) show modifiers side by side (⌘,), like Arc's key hints.
            text = text.Replace("+", string.Empty, StringComparison.Ordinal);
        }
        return text;
    }

    internal void SetHighlighted(bool value) => PseudoClasses.Set(HighlightedPseudoClass, value);

    /// <summary>Activates the row as if it were clicked.</summary>
    public void PerformClick() => Activate(NavigationMethod.Unspecified);

    internal void Activate(NavigationMethod method)
    {
        if (!IsEffectivelyEnabled || !IsActivatable)
        {
            return;
        }
        var surface = Surface;
        if (ToggleType == MenuEntryToggleType.CheckBox)
        {
            SetCurrentValue(IsCheckedProperty, !IsChecked);
        }
        else if (ToggleType == MenuEntryToggleType.Radio)
        {
            SetCurrentValue(IsCheckedProperty, true);
            surface?.UncheckRadioSiblings(this);
        }
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        if (Command is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }
        surface?.OnEntryActivated(this, method);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsActivatable || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        PseudoClasses.Set(PressedPseudoClass, true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
        if (e.InitialPressMouseButton == MouseButton.Left && RowVisual.Bounds.Size is var size &&
            new Rect(size).Contains(e.GetPosition(RowVisual)))
        {
            e.Handled = true;
            Activate(NavigationMethod.Pointer);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MenuEntryAutomationPeer(this);

    /// <summary>MenuItem role with invoke (and toggle for check / radio rows).</summary>
    private sealed class MenuEntryAutomationPeer : ControlAutomationPeer, IInvokeProvider, IToggleProvider
    {
        public MenuEntryAutomationPeer(MenuEntry owner)
            : base(owner)
        {
        }

        private MenuEntry Entry => (MenuEntry)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Entry.TypeaheadText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke() => Entry.Activate(NavigationMethod.Unspecified);

        public ToggleState ToggleState => Entry.IsChecked ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Entry.Activate(NavigationMethod.Unspecified);

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(IToggleProvider) && Entry.ToggleType == MenuEntryToggleType.None)
            {
                return null;
            }
            return base.GetProviderCore(providerType);
        }
    }
}

/// <summary>Builds an ellipsizing text block for string row headers.</summary>
internal sealed class MenuEntryTextTemplate : IDataTemplate
{
    public Control? Build(object? param) => new TextBlock
    {
        Text = param as string,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    public bool Match(object? data) => data is string;
}
