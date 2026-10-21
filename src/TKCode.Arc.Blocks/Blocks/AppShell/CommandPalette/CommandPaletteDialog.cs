using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Primitives;
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
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The command palette as a popup: declare it once and it opens a <see cref="CommandPalette"/> above the page on Cmd+K
/// (Ctrl+K elsewhere) or from a button, and closes it with a key. It renders nothing in place; while
/// <see cref="IsOpen"/> is true it shows the palette near the top centre of the window over a dimmed scrim.
/// <code>
/// &lt;arc:CommandPaletteDialog x:Name="Palette" PlaceholderText="Search project commands" ItemSelected="OnRun"&gt;
///     &lt;arc:CommandPaletteItem Id="share" Label="Share project" Group="Project" Shortcut="S" /&gt;
/// &lt;/arc:CommandPaletteDialog&gt;
/// &lt;arc:Button Content="Open" arc:CommandPaletteDialog.Opens="{Binding #Palette}" /&gt;
/// </code>
/// <para>
/// Keys: the open gesture (<see cref="OpenGesture"/>) toggles it from anywhere in the window; ↓ ↑ Home End move the
/// highlight, Enter runs the highlighted command (or the top match after typing), Escape clears the query and, in an empty
/// field, closes. A press on the scrim, a click on the "Esc" key cap and choosing a command also close it
/// (<see cref="CloseOnOutsidePress"/>, <see cref="CloseOnSelect"/>); focus returns to what had it before.
/// </para>
/// <para>
/// The palette is built the first time the dialog opens and reused after that; while the dialog is closed it only listens
/// for the open gesture. It is mounted by Core <see cref="ModalPresence"/> (scrim fade, focus trap, stacking with other
/// modals) and rises 6px on the smooth spring (reduced motion: fade only, Off: instant).
/// </para>
/// </summary>
public class CommandPaletteDialog : TemplatedControl
{
    #region Properties

    /// <summary>Shown while true; set false (or call <see cref="Close"/>) to play the exit. Two-way by default.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The commands to list; when set it replaces <see cref="Items"/>. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<CommandPaletteItem>?> ItemsSourceProperty =
        CommandPalette.ItemsSourceProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Hint in the empty search field.</summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        CommandPalette.PlaceholderTextProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Accessible name of the dialog and of its search field.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        CommandPalette.LabelProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Title of the empty state when nothing matches.</summary>
    public static readonly StyledProperty<string> EmptyTitleProperty =
        CommandPalette.EmptyTitleProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Description of the empty state when nothing matches.</summary>
    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        CommandPalette.EmptyDescriptionProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Heading for commands without a <see cref="CommandPaletteItem.Group"/>.</summary>
    public static readonly StyledProperty<string> DefaultGroupProperty =
        CommandPalette.DefaultGroupProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Accessible name of the button that clears the query.</summary>
    public static readonly StyledProperty<string> ClearButtonLabelProperty =
        CommandPalette.ClearButtonLabelProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Accessible name of the "Esc" close button.</summary>
    public static readonly StyledProperty<string> CloseButtonLabelProperty =
        CommandPalette.CloseButtonLabelProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Accessible name of the result list.</summary>
    public static readonly StyledProperty<string> ResultsLabelProperty =
        CommandPalette.ResultsLabelProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Executed with the chosen <see cref="CommandPaletteItem"/>.</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        CommandPalette.SelectCommandProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>Shows the clickable "Esc" key cap in the search row (default true).</summary>
    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        CommandPalette.ShowCloseButtonProperty.AddOwner<CommandPaletteDialog>(new StyledPropertyMetadata<bool>(defaultValue: true));

    /// <summary>
    /// Text of the shortcut key cap in the search row. Null shows the open gesture ("⌘ K" on macOS, "Ctrl K" elsewhere) and
    /// hides the cap while <see cref="IsShortcutEnabled"/> is false; an empty string always hides it.
    /// </summary>
    public static readonly StyledProperty<string?> ShortcutHintProperty =
        CommandPalette.ShortcutHintProperty.AddOwner<CommandPaletteDialog>();

    /// <summary>
    /// The key combination that toggles the dialog from anywhere in the window (handled before the window's own shortcuts).
    /// Null is Cmd+K or Ctrl+K on every platform.
    /// </summary>
    public static readonly StyledProperty<KeyGesture?> OpenGestureProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, KeyGesture?>(nameof(OpenGesture));

    /// <summary>
    /// Turns the window-level open gesture on (default) or off. The gesture is ignored while the dialog is not attached,
    /// not visible or disabled, and while another modal is on top.
    /// </summary>
    public static readonly StyledProperty<bool> IsShortcutEnabledProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, bool>(nameof(IsShortcutEnabled), true);

    /// <summary>Choosing a command closes the dialog, before <see cref="ItemSelected"/> is raised (default true).</summary>
    public static readonly StyledProperty<bool> CloseOnSelectProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, bool>(nameof(CloseOnSelect), true);

    /// <summary>Escape closes the dialog in an empty field (default true; cancellable through <see cref="Closing"/>).</summary>
    public static readonly StyledProperty<bool> CloseOnEscapeProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, bool>(nameof(CloseOnEscape), true);

    /// <summary>A press outside the palette closes the dialog (default true; cancellable through <see cref="Closing"/>).</summary>
    public static readonly StyledProperty<bool> CloseOnOutsidePressProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, bool>(nameof(CloseOnOutsidePress), true);

    /// <summary>
    /// Modal (default): a scrim over the page, a focus trap, and a press on the scrim closes. When false the page stays
    /// interactive and focus is free, and a press anywhere outside the palette closes it.
    /// </summary>
    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, bool>(nameof(IsModal), true);

    /// <summary>Scrim brush (the theme sets a light dim).</summary>
    public static readonly StyledProperty<IBrush?> ScrimBrushProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, IBrush?>(nameof(ScrimBrush));

    /// <summary>
    /// Backdrop blur radius in px applied to the page while the scrim is shown (default 0: none; Arc's dialog uses 7).
    /// Off under the ReduceTransparency token.
    /// </summary>
    public static readonly StyledProperty<double> BackdropBlurProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, double>(nameof(BackdropBlur));

    /// <summary>Preferred width of the palette in px; it shrinks to fit the window minus 16px on each side (default 560).</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, double>(nameof(PanelWidth), 560);

    /// <summary>The palette's distance from the window top as a fraction of the window height (default 0.13; at least 16px).</summary>
    public static readonly StyledProperty<double> PanelTopRatioProperty =
        AvaloniaProperty.Register<CommandPaletteDialog, double>(nameof(PanelTopRatio), 0.13);

    /// <summary>
    /// Set on a button: clicking it opens that dialog. <code>&lt;arc:Button arc:CommandPaletteDialog.Opens="{Binding #Palette}" /&gt;</code>
    /// </summary>
    public static readonly AttachedProperty<CommandPaletteDialog?> OpensProperty =
        AvaloniaProperty.RegisterAttached<CommandPaletteDialog, AvaloniaButton, CommandPaletteDialog?>("Opens");

    /// <summary>
    /// Raised when a command is chosen (Enter or a click). With <see cref="CloseOnSelect"/> the dialog has already started to
    /// close, so a handler that opens another dialog finds the focus back on the page; the palette then runs
    /// <see cref="SelectCommand"/> and the command's own <see cref="CommandPaletteItem.Command"/>.
    /// </summary>
    public static readonly RoutedEvent<CommandPaletteItemSelectedEventArgs> ItemSelectedEvent =
        RoutedEvent.Register<CommandPaletteDialog, CommandPaletteItemSelectedEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<CommandPaletteItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="EmptyTitleProperty"/>
    public string EmptyTitle { get => GetValue(EmptyTitleProperty); set => SetValue(EmptyTitleProperty, value); }

    /// <inheritdoc cref="EmptyDescriptionProperty"/>
    public string EmptyDescription { get => GetValue(EmptyDescriptionProperty); set => SetValue(EmptyDescriptionProperty, value); }

    /// <inheritdoc cref="DefaultGroupProperty"/>
    public string DefaultGroup { get => GetValue(DefaultGroupProperty); set => SetValue(DefaultGroupProperty, value); }

    /// <inheritdoc cref="ClearButtonLabelProperty"/>
    public string ClearButtonLabel { get => GetValue(ClearButtonLabelProperty); set => SetValue(ClearButtonLabelProperty, value); }

    /// <inheritdoc cref="CloseButtonLabelProperty"/>
    public string CloseButtonLabel { get => GetValue(CloseButtonLabelProperty); set => SetValue(CloseButtonLabelProperty, value); }

    /// <inheritdoc cref="ResultsLabelProperty"/>
    public string ResultsLabel { get => GetValue(ResultsLabelProperty); set => SetValue(ResultsLabelProperty, value); }

    /// <inheritdoc cref="SelectCommandProperty"/>
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }

    /// <inheritdoc cref="ShowCloseButtonProperty"/>
    public bool ShowCloseButton { get => GetValue(ShowCloseButtonProperty); set => SetValue(ShowCloseButtonProperty, value); }

    /// <inheritdoc cref="ShortcutHintProperty"/>
    public string? ShortcutHint { get => GetValue(ShortcutHintProperty); set => SetValue(ShortcutHintProperty, value); }

    /// <inheritdoc cref="OpenGestureProperty"/>
    public KeyGesture? OpenGesture { get => GetValue(OpenGestureProperty); set => SetValue(OpenGestureProperty, value); }

    /// <inheritdoc cref="IsShortcutEnabledProperty"/>
    public bool IsShortcutEnabled { get => GetValue(IsShortcutEnabledProperty); set => SetValue(IsShortcutEnabledProperty, value); }

    /// <inheritdoc cref="CloseOnSelectProperty"/>
    public bool CloseOnSelect { get => GetValue(CloseOnSelectProperty); set => SetValue(CloseOnSelectProperty, value); }

    /// <inheritdoc cref="CloseOnEscapeProperty"/>
    public bool CloseOnEscape { get => GetValue(CloseOnEscapeProperty); set => SetValue(CloseOnEscapeProperty, value); }

    /// <inheritdoc cref="CloseOnOutsidePressProperty"/>
    public bool CloseOnOutsidePress { get => GetValue(CloseOnOutsidePressProperty); set => SetValue(CloseOnOutsidePressProperty, value); }

    /// <inheritdoc cref="IsModalProperty"/>
    public bool IsModal { get => GetValue(IsModalProperty); set => SetValue(IsModalProperty, value); }

    /// <inheritdoc cref="ScrimBrushProperty"/>
    public IBrush? ScrimBrush { get => GetValue(ScrimBrushProperty); set => SetValue(ScrimBrushProperty, value); }

    /// <inheritdoc cref="BackdropBlurProperty"/>
    public double BackdropBlur { get => GetValue(BackdropBlurProperty); set => SetValue(BackdropBlurProperty, value); }

    /// <inheritdoc cref="PanelWidthProperty"/>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <inheritdoc cref="PanelTopRatioProperty"/>
    public double PanelTopRatio { get => GetValue(PanelTopRatioProperty); set => SetValue(PanelTopRatioProperty, value); }

    /// <summary>Gets the dialog that <paramref name="button"/> opens when clicked.</summary>
    public static CommandPaletteDialog? GetOpens(AvaloniaButton button) => button.GetValue(OpensProperty);

    /// <summary>Sets the dialog that <paramref name="button"/> opens when clicked.</summary>
    public static void SetOpens(AvaloniaButton button, CommandPaletteDialog? value) => button.SetValue(OpensProperty, value);

    /// <summary>Inline commands (used when <see cref="ItemsSource"/> is null).</summary>
    [Content]
    public AvaloniaList<CommandPaletteItem> Items { get; } = new();

    /// <summary>Why the last close happened.</summary>
    public DismissReason LastCloseReason { get; private set; }

    /// <summary>The palette shown in the dialog (created on first open; null before).</summary>
    public CommandPalette? Palette => _palette;

    /// <summary>The modal presence (created on first open; null before).</summary>
    public ModalPresence? Presence => _presence;

    /// <summary>Raised when the dialog opens.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised before a close requested by the user, a command or <see cref="Close"/>; cancel to keep it open.</summary>
    public event EventHandler<OverlayClosingEventArgs>? Closing;

    /// <summary>Raised when the dialog closes (the exit may still be playing).</summary>
    public event EventHandler<OverlayClosedEventArgs>? Closed;

    /// <inheritdoc cref="ItemSelectedEvent"/>
    public event EventHandler<CommandPaletteItemSelectedEventArgs>? ItemSelected
    {
        add => AddHandler(ItemSelectedEvent, value);
        remove => RemoveHandler(ItemSelectedEvent, value);
    }

    /// <summary>Opens the dialog.</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Toggles the dialog.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Closes the dialog.</summary>
    public ICommand CloseCommand { get; }

    #endregion

    private CommandPalette? _palette;
    private CommandPaletteFrame? _frame;
    private ModalPresence? _presence;
    private DismissReason _pendingReason = DismissReason.Programmatic;
    private bool _pendingOpen;
    private bool _opened;
    private TopLevel? _keyHost;
    private TopLevel? _pressHost;

    static CommandPaletteDialog()
    {
        IsVisibleProperty.OverrideDefaultValue<CommandPaletteDialog>(false);
        FocusableProperty.OverrideDefaultValue<CommandPaletteDialog>(false);
        AvaloniaButton.ClickEvent.AddClassHandler<AvaloniaButton>(OnAnyButtonClick, RoutingStrategies.Bubble);
    }

    /// <summary>Creates a closed dialog with no commands.</summary>
    public CommandPaletteDialog()
    {
        OpenCommand = new DialogCommand(Open);
        ToggleCommand = new DialogCommand(Toggle);
        CloseCommand = new DialogCommand(Close);
    }

    #region Open and close

    /// <summary>Opens the dialog.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the dialog (raises the cancellable <see cref="Closing"/>).</summary>
    public void Close() => RequestClose(DismissReason.Programmatic);

    /// <summary>Opens the dialog when it is closed and closes it when it is open.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private bool RequestClose(DismissReason reason)
    {
        if (!IsOpen)
        {
            return false;
        }
        var args = new OverlayClosingEventArgs(reason, null);
        Closing?.Invoke(this, args);
        if (args.Cancel)
        {
            return false;
        }
        _pendingReason = reason;
        SetCurrentValue(IsOpenProperty, false);
        return true;
    }

    private static void OnAnyButtonClick(AvaloniaButton button, RoutedEventArgs e)
    {
        if (ReferenceEquals(e.Source, button) && button.GetValue(OpensProperty) is { } dialog)
        {
            dialog.Open();
        }
    }

    #endregion

    #region Lifetime

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsOpenProperty)
        {
            if (change.GetNewValue<bool>())
            {
                OpenCore();
            }
            else
            {
                CloseCore();
            }
        }
        else if (property == ItemsSourceProperty)
        {
            if (_palette is not null)
            {
                _palette.ItemsSource = ItemsSource ?? Items;
            }
        }
        else if (property == LabelProperty)
        {
            UpdateAutomation();
        }
        else if (property == ShortcutHintProperty || property == OpenGestureProperty)
        {
            UpdateHint();
        }
        else if (property == IsShortcutEnabledProperty)
        {
            UpdateHint();
            UpdateKeyHost(attached: this.IsAttachedToVisualTree());
        }
        else if (property == PanelWidthProperty || property == PanelTopRatioProperty)
        {
            if (_frame is not null)
            {
                _frame.PanelWidth = PanelWidth;
                _frame.TopRatio = PanelTopRatio;
            }
        }
        else if (_presence is not null && (property == ScrimBrushProperty || property == BackdropBlurProperty || property == CloseOnEscapeProperty ||
                                           property == CloseOnOutsidePressProperty || property == IsModalProperty))
        {
            ApplyPresenceSettings(_presence);
            if (IsOpen && (property == IsModalProperty || property == CloseOnOutsidePressProperty))
            {
                DetachPressHost();
                AttachPressHost();
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateKeyHost(attached: true);
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // An open presence removes itself when its anchor (this) leaves the window and raises Closed.
        UpdateKeyHost(attached: false);
        DetachPressHost();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    private void EnsurePalette()
    {
        if (_palette is not null)
        {
            return;
        }
        _palette = new CommandPalette { IsHosted = true, IsShortcutEnabled = false, AutoFocus = true };
        Forward(CommandPalette.PlaceholderTextProperty, PlaceholderTextProperty);
        Forward(CommandPalette.LabelProperty, LabelProperty);
        Forward(CommandPalette.EmptyTitleProperty, EmptyTitleProperty);
        Forward(CommandPalette.EmptyDescriptionProperty, EmptyDescriptionProperty);
        Forward(CommandPalette.DefaultGroupProperty, DefaultGroupProperty);
        Forward(CommandPalette.ClearButtonLabelProperty, ClearButtonLabelProperty);
        Forward(CommandPalette.CloseButtonLabelProperty, CloseButtonLabelProperty);
        Forward(CommandPalette.ResultsLabelProperty, ResultsLabelProperty);
        Forward(CommandPalette.SelectCommandProperty, SelectCommandProperty);
        Forward(CommandPalette.ShowCloseButtonProperty, ShowCloseButtonProperty);
        _palette.ItemsSource = ItemsSource ?? Items;
        _palette.ItemSelected += OnPaletteItemSelected;
        _palette.CloseRequested += OnPaletteCloseRequested;

        _frame = new CommandPaletteFrame { PanelWidth = PanelWidth, TopRatio = PanelTopRatio };
        _frame.Children.Add(_palette);
        AutomationProperties.SetControlTypeOverride(_frame, AutomationControlType.Window);
        UpdateAutomation();
        UpdateHint();
        LogicalChildren.Add(_frame);

        _presence = new ModalPresence(_frame, this) { ContainmentRoot = this, Entrance = ModalEntrance.Fade };
        _presence.Dismissing += (_, e) =>
        {
            // The dialog owns the decision: raise Closing and update IsOpen (two-way bindings see every close).
            e.Cancel = true;
            RequestClose(e.Reason);
        };
        _presence.Closed += OnPresenceClosed;
    }

    private void Forward<T>(StyledProperty<T> target, StyledProperty<T> source)
        => _palette!.Bind(target, this.GetObservable(source));

    private void UpdateAutomation()
    {
        if (_frame is not null)
        {
            AutomationProperties.SetName(_frame, Label);
        }
    }

    private void UpdateHint()
    {
        if (_palette is null)
        {
            return;
        }
        string? hint;
        if (ShortcutHint is { } explicitHint)
        {
            hint = explicitHint;
        }
        else if (!IsShortcutEnabled)
        {
            hint = string.Empty;
        }
        else
        {
            hint = OpenGesture is { } gesture ? CommandPaletteGestures.Format(gesture) : null;
        }
        _palette.ShortcutHint = hint;
    }

    private void ApplyPresenceSettings(ModalPresence presence)
    {
        presence.ScrimBrush = ScrimBrush;
        presence.BackdropBlur = IsModal ? BackdropBlur : 0;
        presence.DismissOnEscape = CloseOnEscape;
        presence.DismissOnScrimPress = CloseOnOutsidePress && IsModal;
        presence.TrapFocus = IsModal;
        presence.Host.Scrim.IsVisible = IsModal;
    }

    private void OpenCore()
    {
        EnsurePalette();
        var presence = _presence!;
        if (TopLevel.GetTopLevel(this) is null)
        {
            // Not in a window yet: open once attached.
            _pendingOpen = true;
            return;
        }
        presence.Anchor = this;
        ApplyPresenceSettings(presence);
        if (presence.State == ModalState.Closed)
        {
            // A fresh open starts over; a reopen during the exit keeps what is on screen.
            _palette!.Clear();
        }
        _pendingReason = DismissReason.Programmatic;
        if (!presence.Open())
        {
            // No window layer yet (it appears with the first layout): try again once loaded.
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        _opened = true;
        AttachPressHost();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void CloseCore()
    {
        _pendingOpen = false;
        var reason = _pendingReason;
        _pendingReason = DismissReason.Programmatic;
        LastCloseReason = reason;
        DetachPressHost();
        _presence?.Close(reason);
        if (_opened)
        {
            _opened = false;
            Closed?.Invoke(this, new OverlayClosedEventArgs(reason, null));
        }
    }

    private void OnPresenceClosed(object? sender, ModalClosedEventArgs e)
    {
        if (IsOpen)
        {
            // Removed without a close request (the dialog left the window).
            _pendingReason = e.Reason;
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    #endregion

    #region Commands

    private void OnPaletteItemSelected(object? sender, CommandPaletteItemSelectedEventArgs e)
    {
        // Closing first returns the focus to the page, so a handler that opens another dialog finds a calm page to come back to.
        if (CloseOnSelect)
        {
            RequestClose(DismissReason.Action);
        }
        RaiseEvent(new CommandPaletteItemSelectedEventArgs(ItemSelectedEvent, e.Item));
    }

    private void OnPaletteCloseRequested(object? sender, CommandPaletteCloseRequestedEventArgs e)
    {
        // Escape is answered by the modal stack (CloseOnEscape); the key cap is an explicit click and always closes.
        if (e.Reason == CommandPaletteCloseReason.CloseButton)
        {
            RequestClose(DismissReason.CloseButton);
        }
    }

    #endregion

    #region Window-level keys and presses

    private void UpdateKeyHost(bool attached)
    {
        var wanted = IsShortcutEnabled && attached ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(wanted, _keyHost))
        {
            return;
        }
        _keyHost?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _keyHost = wanted;
        // Tunnel: the dialog answers the gesture before the window and its controls see it.
        _keyHost?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsShortcutEnabled || !Matches(e) || !IsEffectivelyEnabled || !IsHostVisible)
        {
            return;
        }
        if (_keyHost is { } top && TopModal(top) is { } modal && !ReferenceEquals(modal, _presence))
        {
            // Another modal owns the window right now.
            return;
        }
        e.Handled = true;
        Toggle();
    }

    private bool Matches(KeyEventArgs e)
    {
        if (OpenGesture is { } gesture)
        {
            return gesture.Matches(e);
        }
        return e.Key == Key.K && (e.KeyModifiers == KeyModifiers.Control || e.KeyModifiers == KeyModifiers.Meta);
    }

    /// <summary>The dialog itself never shows (it renders nothing), so its parent decides: a hidden page or tab does not react.</summary>
    private bool IsHostVisible => this.GetVisualParent() is { IsEffectivelyVisible: true };

    /// <summary>The topmost open modal. A presence that does not trap focus (a non-modal palette) is a popup, not a modal, and does not count.</summary>
    private static ModalPresence? TopModal(TopLevel topLevel)
    {
        var modals = ModalPresence.GetModals(topLevel);
        for (var i = modals.Count - 1; i >= 0; i--)
        {
            if (modals[i].IsOpen && modals[i].TrapFocus)
            {
                return modals[i];
            }
        }
        return null;
    }

    private void AttachPressHost()
    {
        if (IsModal || !CloseOnOutsidePress || _pressHost is not null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }
        _pressHost = top;
        top.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachPressHost()
    {
        _pressHost?.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        _pressHost = null;
    }

    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Non-modal: there is no scrim, so a press anywhere outside the palette closes it.
        if (!IsOpen || !CloseOnOutsidePress || _presence is null)
        {
            return;
        }
        if (e.Source is IInputElement source && _presence.Contains(source))
        {
            return;
        }
        RequestClose(DismissReason.PressOutside);
    }

    #endregion
}

/// <summary>Formats a <see cref="KeyGesture"/> as key cap text: "⌘ ⇧ P" on macOS, "Ctrl Shift P" elsewhere.</summary>
internal static class CommandPaletteGestures
{
    public static string Format(KeyGesture gesture)
    {
        var mac = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();
        var parts = new List<string>(4);
        var modifiers = gesture.KeyModifiers;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add(mac ? "⌃" : "Ctrl");
        }
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add(mac ? "⌥" : "Alt");
        }
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add(mac ? "⇧" : "Shift");
        }
        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add(mac ? "⌘" : "Win");
        }
        parts.Add(KeyName(gesture.Key));
        return string.Join(' ', parts);
    }

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.Space => "Space",
        Key.Return => "Enter",
        _ => key.ToString()
    };
}

/// <summary>
/// The panel a <see cref="CommandPaletteDialog"/> mounts: it fills the layer and places the palette at the top centre,
/// <see cref="PanelWidth"/> wide at most (16px from each side in a narrow window) and <see cref="TopRatio"/> of the height
/// down (at least 16px). The palette's height follows its results (and springs while it filters) without moving its
/// top edge, and never reaches below the layer.
/// </summary>
internal sealed class CommandPaletteFrame : Panel
{
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<CommandPaletteFrame, double>(nameof(PanelWidth), 560);

    public static readonly StyledProperty<double> TopRatioProperty =
        AvaloniaProperty.Register<CommandPaletteFrame, double>(nameof(TopRatio), 0.13);

    private const double SideMargin = 16;
    private const double BottomMargin = 16;
    private const double MinRoom = 224;

    static CommandPaletteFrame()
    {
        AffectsMeasure<CommandPaletteFrame>(PanelWidthProperty, TopRatioProperty);
        AffectsArrange<CommandPaletteFrame>(PanelWidthProperty, TopRatioProperty);
    }

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    public double TopRatio { get => GetValue(TopRatioProperty); set => SetValue(TopRatioProperty, value); }

    private double Top(double height) => Math.Min(Math.Max(height * TopRatio, SideMargin), Math.Max(SideMargin, height - MinRoom));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? PanelWidth + 2 * SideMargin : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 720 : availableSize.Height;
        var child = new Size(Math.Min(PanelWidth, Math.Max(0, width - 2 * SideMargin)), Math.Max(0, height - Top(height) - BottomMargin));
        foreach (var control in Children)
        {
            control.Measure(child);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = Top(finalSize.Height);
        foreach (var control in Children)
        {
            var size = control.DesiredSize;
            control.Arrange(new Rect((finalSize.Width - size.Width) / 2, top, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>Minimal command that runs an action.</summary>
internal sealed class DialogCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
