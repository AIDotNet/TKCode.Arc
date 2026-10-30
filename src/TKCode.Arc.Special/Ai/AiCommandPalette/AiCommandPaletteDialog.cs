using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// An <see cref="AiCommandPalette"/> as a dialog over the page: declare it once and Cmd+K (Ctrl+K elsewhere) opens it near
/// the top of the window over a dimmed scrim, or bind <see cref="IsOpen"/>. It renders nothing in place.
/// <code>
/// &lt;arc:AiCommandPaletteDialog x:Name="Palette" IsOpen="{Binding IsPaletteOpen}"&gt;
///     &lt;arc:AiCommandPalette Responder="{Binding Ask}" ItemSelected="OnItemSelected" /&gt;
/// &lt;/arc:AiCommandPaletteDialog&gt;
/// &lt;arc:Button Content="Search" arc:AiCommandPaletteDialog.Opens="{Binding #Palette}" /&gt;
/// </code>
/// <para>
/// The palette gets the floating shadow and the focus when it opens. Escape in an empty search, a press on the scrim and
/// choosing a page or action (<see cref="CloseOnSelect"/>) close it, and the focus returns to what had it before. Without a
/// palette of its own it shows the sample one.
/// </para>
/// </summary>
public class AiCommandPaletteDialog : Control
{
    /// <summary>Shown while true; set false (or press Escape in an empty search) to close it. Two-way by default.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialog, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The palette to show; null shows a sample palette.</summary>
    public static readonly StyledProperty<AiCommandPalette?> PaletteProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialog, AiCommandPalette?>(nameof(Palette));

    /// <summary>The key combination that toggles the dialog from anywhere in the window; null is Cmd+K or Ctrl+K.</summary>
    public static readonly StyledProperty<KeyGesture?> OpenGestureProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialog, KeyGesture?>(nameof(OpenGesture));

    /// <summary>Turns the window-level open gesture on (default) or off.</summary>
    public static readonly StyledProperty<bool> IsShortcutEnabledProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialog, bool>(nameof(IsShortcutEnabled), true);

    /// <summary>Choosing a page or action closes the dialog (default true).</summary>
    public static readonly StyledProperty<bool> CloseOnSelectProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialog, bool>(nameof(CloseOnSelect), true);

    /// <summary>Set on a button: clicking it opens that dialog.</summary>
    public static readonly AttachedProperty<AiCommandPaletteDialog?> OpensProperty =
        AvaloniaProperty.RegisterAttached<AiCommandPaletteDialog, AvaloniaButton, AiCommandPaletteDialog?>("Opens");

    private AiCommandPalette? _shown;
    private AiCommandPaletteDialogFrame? _frame;
    private ModalPresence? _presence;
    private TopLevel? _keyHost;
    private bool _pendingOpen;

    static AiCommandPaletteDialog()
    {
        FocusableProperty.OverrideDefaultValue<AiCommandPaletteDialog>(false);
        AvaloniaButton.ClickEvent.AddClassHandler<AvaloniaButton>(OnAnyButtonClick, RoutingStrategies.Bubble);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="PaletteProperty"/>
    [Content]
    public AiCommandPalette? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    /// <inheritdoc cref="OpenGestureProperty"/>
    public KeyGesture? OpenGesture { get => GetValue(OpenGestureProperty); set => SetValue(OpenGestureProperty, value); }

    /// <inheritdoc cref="IsShortcutEnabledProperty"/>
    public bool IsShortcutEnabled { get => GetValue(IsShortcutEnabledProperty); set => SetValue(IsShortcutEnabledProperty, value); }

    /// <inheritdoc cref="CloseOnSelectProperty"/>
    public bool CloseOnSelect { get => GetValue(CloseOnSelectProperty); set => SetValue(CloseOnSelectProperty, value); }

    /// <summary>Gets the dialog that <paramref name="button"/> opens.</summary>
    public static AiCommandPaletteDialog? GetOpens(AvaloniaButton button) => button.GetValue(OpensProperty);

    /// <summary>Sets the dialog that <paramref name="button"/> opens.</summary>
    public static void SetOpens(AvaloniaButton button, AiCommandPaletteDialog? value) => button.SetValue(OpensProperty, value);

    /// <summary>The palette on screen (the one given, or the sample one once the dialog has opened).</summary>
    internal AiCommandPalette? Shown => _shown;

    internal ModalPresence? Presence => _presence;

    /// <summary>Opens the dialog.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the dialog.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Opens the dialog when it is closed and closes it when it is open.</summary>
    public void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    private static void OnAnyButtonClick(AvaloniaButton button, RoutedEventArgs e)
    {
        if (ReferenceEquals(e.Source, button) && button.GetValue(OpensProperty) is { } dialog)
        {
            dialog.Open();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
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
        else if (change.Property == PaletteProperty)
        {
            if (_frame is not null)
            {
                Mount(Palette ?? _shown ?? new AiCommandPalette());
            }
        }
        else if (change.Property == IsShortcutEnabledProperty)
        {
            UpdateKeyHost();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateKeyHost();
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateKeyHost();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    private void EnsureFrame()
    {
        if (_frame is not null)
        {
            return;
        }
        _frame = new AiCommandPaletteDialogFrame();
        AutomationProperties.SetControlTypeOverride(_frame, AutomationControlType.Window);
        LogicalChildren.Add(_frame);
        Mount(Palette ?? new AiCommandPalette());
        _presence = new ModalPresence(_frame, this) { ContainmentRoot = this, Entrance = ModalEntrance.Fade, DismissOnEscape = false };
        _presence.Dismissing += (_, e) =>
        {
            e.Cancel = true;
            Close();
        };
        _presence.Closed += (_, _) =>
        {
            if (IsOpen)
            {
                Close();
            }
        };
    }

    private void Mount(AiCommandPalette palette)
    {
        if (_frame is null || ReferenceEquals(palette, _shown))
        {
            return;
        }
        if (_shown is not null)
        {
            _shown.CloseRequested -= OnCloseRequested;
            _shown.ItemSelected -= OnItemSelected;
            _frame.Children.Remove(_shown);
        }
        _shown = palette;
        palette.IsElevated = true;
        palette.CloseRequested += OnCloseRequested;
        palette.ItemSelected += OnItemSelected;
        _frame.Children.Add(palette);
        AutomationProperties.SetName(_frame, palette.Labels.PaletteName);
    }

    private void OpenCore()
    {
        EnsureFrame();
        if (TopLevel.GetTopLevel(this) is null)
        {
            _pendingOpen = true;
            return;
        }
        var presence = _presence!;
        presence.Anchor = this;
        presence.InitialFocus = _shown?.Input;
        if (presence.State == ModalState.Closed)
        {
            _shown?.Reset();
        }
        if (!presence.Open())
        {
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        _shown?.StopWalkthroughForGood();
        _shown?.FocusInput();
    }

    private void CloseCore()
    {
        _pendingOpen = false;
        _presence?.Close(DismissReason.Programmatic);
    }

    private void OnCloseRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }

    private void OnItemSelected(object? sender, AiCommandPaletteItemEventArgs e)
    {
        if (CloseOnSelect)
        {
            Close();
        }
    }

    private void UpdateKeyHost()
    {
        var wanted = IsShortcutEnabled && this.IsAttachedToVisualTree() ? TopLevel.GetTopLevel(this) : null;
        if (ReferenceEquals(wanted, _keyHost))
        {
            return;
        }
        _keyHost?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _keyHost = wanted;
        _keyHost?.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsShortcutEnabled || !IsEffectivelyEnabled)
        {
            return;
        }
        var matches = OpenGesture is { } gesture
            ? gesture.Matches(e)
            : e.Key == Key.K && (e.KeyModifiers == KeyModifiers.Control || e.KeyModifiers == KeyModifiers.Meta);
        if (!matches)
        {
            return;
        }
        e.Handled = true;
        Toggle();
    }
}

/// <summary>Fills the modal layer and places the palette at the top centre, at most the token's width and 16px from the sides.</summary>
internal sealed class AiCommandPaletteDialogFrame : Panel
{
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialogFrame, double>(nameof(PanelWidth), 680);

    public static readonly StyledProperty<double> TopRatioProperty =
        AvaloniaProperty.Register<AiCommandPaletteDialogFrame, double>(nameof(TopRatio), 0.12);

    private const double Margin16 = 16;

    static AiCommandPaletteDialogFrame()
    {
        AffectsMeasure<AiCommandPaletteDialogFrame>(PanelWidthProperty, TopRatioProperty);
    }

    protected override Type StyleKeyOverride => typeof(AiCommandPaletteDialogFrame);

    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    public double TopRatio { get => GetValue(TopRatioProperty); set => SetValue(TopRatioProperty, value); }

    private double Top(double height) => Math.Max(Margin16, height * TopRatio);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? PanelWidth + 2 * Margin16 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 720 : availableSize.Height;
        var child = new Size(Math.Min(PanelWidth, Math.Max(0, width - 2 * Margin16)), Math.Max(0, height - Top(height) - Margin16));
        foreach (var control in Children)
        {
            control.Measure(child);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = Top(finalSize.Height);
        var width = Math.Min(PanelWidth, Math.Max(0, finalSize.Width - 2 * Margin16));
        foreach (var control in Children)
        {
            control.Arrange(new Rect((finalSize.Width - width) / 2, top, width, control.DesiredSize.Height));
        }
        return finalSize;
    }
}
