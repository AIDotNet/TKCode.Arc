using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A centered alert or confirmation dialog: a <see cref="Title"/>, a <see cref="Message"/> and up to three actions
/// (the items, usually mobile <see cref="Button"/>s) over a scrim. Put it in <see cref="Page.Overlay"/> and open it
/// with <see cref="IsOpen"/>. Two actions share a row when each fits its half; three, or narrow widths, stack them at
/// the full width. Pressing an action button closes the dialog (see <see cref="CloseOnAction"/>) after the button's own
/// click handler or command has run. Tapping the scrim and Escape close it only when <see cref="IsDismissible"/> is on.
/// Set <see cref="IsDestructiveProperty">Dialog.IsDestructive</see> on an action button for a danger-colored action. Closed, it does not catch touches.
/// <para>
/// Several overlays (a dialog, a sheet, a toast host) share the one <see cref="Page.Overlay"/> slot: put them together in a
/// <c>Grid</c>. The dialog's presence is one <see cref="MotionValue{T}"/> (0 gone, 1 shown) that drives the scrim's
/// opacity and the panel's opacity and scale, so a dialog closed while it is still appearing reverses from where it is.
/// It is not a ControlTheme transition because the exit must finish before the dialog stops catching touches.
/// </para>
/// </summary>
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_Panel", typeof(Border))]
public class Dialog : ItemsControl
{
    /// <summary>The panel starts at this scale and grows to 1 as it appears.</summary>
    private const double EnterScale = 0.94;

    /// <summary>Whether the dialog is open.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Dialog, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The title, shown in bold above the message.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Title));

    /// <summary>The message under the title.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Dialog, string?>(nameof(Message));

    /// <summary>Whether tapping the scrim or pressing Escape closes the dialog: false, so the person has to choose an action.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<Dialog, bool>(nameof(IsDismissible));

    /// <summary>Whether pressing an action button closes the dialog: true.</summary>
    public static readonly StyledProperty<bool> CloseOnActionProperty =
        AvaloniaProperty.Register<Dialog, bool>(nameof(CloseOnAction), true);

    /// <summary>Attached to an action <see cref="Button"/>: whether it is a destructive action, drawn with the Danger color (filled: a Danger pill, tinted: Danger text on a soft pill, plain: Danger text).</summary>
    public static readonly AttachedProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.RegisterAttached<Dialog, Button, bool>("IsDestructive");

    private static readonly AttachedProperty<List<IDisposable>?> DestructiveBindingsProperty =
        AvaloniaProperty.RegisterAttached<Dialog, Button, List<IDisposable>?>("DestructiveBindings");

    private readonly ScaleTransform _scale = new();
    private Border? _scrim;
    private Border? _panel;
    private MotionValue<double>? _progress;
    private bool _present;

    static Dialog()
    {
        ItemsPanelProperty.OverrideDefaultValue<Dialog>(new FuncTemplate<Panel?>(() => new DialogActionsPanel()));
        IsDestructiveProperty.Changed.AddClassHandler<Button>((button, _) => ApplyDestructive(button));
        Button.VariantProperty.Changed.AddClassHandler<Button>((button, _) => ApplyDestructive(button));
    }

    /// <summary>Gets whether <paramref name="button"/> is a destructive action.</summary>
    public static bool GetIsDestructive(Button button) => button.GetValue(IsDestructiveProperty);

    /// <summary>Sets whether <paramref name="button"/> is a destructive action.</summary>
    public static void SetIsDestructive(Button button, bool value) => button.SetValue(IsDestructiveProperty, value);

    // The actions are logical children of the Dialog, not of its template, so no template style reaches them: the colors are
    // token bindings (local priority: the button theme sets its colors from pseudo-class styles, which a template binding would lose to) that follow the theme, replaced whenever the flag or the button's variant changes.
    private static void ApplyDestructive(Button button)
    {
        if (button.GetValue(DestructiveBindingsProperty) is { } old)
        {
            foreach (var binding in old)
            {
                binding.Dispose();
            }
            button.ClearValue(DestructiveBindingsProperty);
        }
        if (!GetIsDestructive(button))
        {
            return;
        }
        var bindings = new List<IDisposable>();
        switch (button.Variant)
        {
            case ButtonVariant.Filled:
                bindings.Add(ArcTokens.Bind(button, TemplatedControl.BackgroundProperty, ArcDesignTokenKind.Danger, BindingPriority.LocalValue));
                bindings.Add(ArcTokens.Bind(button, TemplatedControl.ForegroundProperty, ArcDesignTokenKind.AccentForeground, BindingPriority.LocalValue));
                break;
            case ButtonVariant.Tinted:
                bindings.Add(ArcTokens.Bind(button, TemplatedControl.BackgroundProperty, ArcDesignTokenKind.DangerSubtle, BindingPriority.LocalValue));
                bindings.Add(ArcTokens.Bind(button, TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Danger, BindingPriority.LocalValue));
                break;
            default:
                bindings.Add(ArcTokens.Bind(button, TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Danger, BindingPriority.LocalValue));
                break;
        }
        button.SetValue(DestructiveBindingsProperty, bindings);
    }

    /// <summary>Creates a closed dialog.</summary>
    public Dialog()
    {
        AddHandler(AvaloniaButton.ClickEvent, OnActionClick, RoutingStrategies.Bubble);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <inheritdoc cref="CloseOnActionProperty"/>
    public bool CloseOnAction
    {
        get => GetValue(CloseOnActionProperty);
        set => SetValue(CloseOnActionProperty, value);
    }

    /// <summary>How present the dialog is: 0 gone, 1 shown (a spring may overshoot). For tests and diagnostics.</summary>
    public double Presence => _progress?.Value ?? (IsOpen ? 1 : 0);

    /// <summary>Whether the actions are stacked one above the other instead of sharing a row. For tests and diagnostics.</summary>
    public bool IsActionsStacked => this.GetVisualDescendants().OfType<DialogActionsPanel>().FirstOrDefault()?.IsStacked ?? false;

    protected override Type StyleKeyOverride => typeof(Dialog);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _panel = e.NameScope.Find<Border>("PART_Panel");
        if (_panel is null)
        {
            return;
        }
        _panel.RenderTransform = _scale;
        _progress = new MotionValue<double>(_panel, IsOpen ? 1 : 0, MotionVectors.Double);
        _progress.Changed += _ => Apply();
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        _present = IsOpen;
        UpdatePresence();
        Apply();
    }

    private void Detach()
    {
        _progress?.Dispose();
        _progress = null;
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
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
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private ArcMotionLevel Level => ArcMotion.GetEffectiveLevel(this);

    /// <summary>The dialog catches touches while it is open or still leaving; otherwise the page under it does.</summary>
    private void UpdatePresence()
    {
        IsHitTestVisible = IsOpen || _present;
        if (_panel is not null)
        {
            _panel.IsVisible = IsOpen || _present;
        }
        if (_scrim is not null)
        {
            _scrim.IsVisible = IsOpen || _present;
        }
    }

    private void Apply()
    {
        if (_progress is null)
        {
            return;
        }
        var p = _progress.Value;
        var opacity = Math.Clamp(p, 0, 1);
        if (_panel is not null)
        {
            _panel.Opacity = opacity;
            // Reduced motion fades only: the panel keeps its size.
            var scale = Level == ArcMotionLevel.Full ? EnterScale + (1 - EnterScale) * p : 1;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
        }
        if (_scrim is not null)
        {
            _scrim.Opacity = opacity;
        }
    }

    private void Open()
    {
        if (_progress is null)
        {
            return;
        }
        _present = true;
        UpdatePresence();
        switch (Level)
        {
            case ArcMotionLevel.Off:
                _progress.Jump(1);
                break;
            case ArcMotionLevel.Reduced:
                _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
                break;
            default:
                _ = _progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
                break;
        }
        Focus();
    }

    private void Close()
    {
        if (_progress is null)
        {
            return;
        }
        if (Level == ArcMotionLevel.Off)
        {
            _progress.Jump(0);
            Finish();
            return;
        }
        // Leaving is faster than arriving and never overshoots below zero, so it is a short tween rather than a spring.
        var spec = Level == ArcMotionLevel.Reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Exit);
        _ = _progress.AnimateTo(0, spec).ContinueWith(
            t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (t.Result)
                {
                    Finish();
                }
            }));
    }

    /// <summary>The exit landed: the dialog stops catching touches.</summary>
    private void Finish()
    {
        if (!IsOpen)
        {
            _present = false;
            UpdatePresence();
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (CloseOnAction && IsOpen && e.Source is AvaloniaButton)
        {
            IsOpen = false;
        }
    }
}
