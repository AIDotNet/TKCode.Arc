using System.Windows.Input;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Base of the declarative modal overlays (Dialog, Drawer, BottomSheet).
/// The control itself takes no space and renders nothing where it is declared; while <see cref="IsOpen"/> its
/// <see cref="ModalSurface"/> is shown on the window's overlay layer by a <see cref="ModalPresence"/> (scrim, focus
/// trap and return, Escape / outside press, stacking, exit before removal). Content, actions and DataContext stay in
/// this control's logical tree, so bindings work as if the content were inline.
/// </summary>
public abstract class ModalOverlay : ContentControl, IOverlayResultTarget
{
    #region Properties

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalOverlay, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> TitleProperty =
        ModalSurface.TitleProperty.AddOwner<ModalOverlay>();

    public static readonly StyledProperty<string?> DescriptionProperty =
        ModalSurface.DescriptionProperty.AddOwner<ModalOverlay>();

    public static readonly StyledProperty<object?> ActionsProperty =
        ModalSurface.ActionsProperty.AddOwner<ModalOverlay>();

    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        ModalSurface.ShowCloseButtonProperty.AddOwner<ModalOverlay>();

    public static readonly StyledProperty<string> CloseButtonNameProperty =
        ModalSurface.CloseButtonNameProperty.AddOwner<ModalOverlay>();

    public static readonly StyledProperty<bool> CloseOnEscapeProperty =
        AvaloniaProperty.Register<ModalOverlay, bool>(nameof(CloseOnEscape), true);

    public static readonly StyledProperty<bool> CloseOnOutsidePressProperty =
        AvaloniaProperty.Register<ModalOverlay, bool>(nameof(CloseOnOutsidePress), true);

    public static readonly StyledProperty<bool> IsModalProperty =
        AvaloniaProperty.Register<ModalOverlay, bool>(nameof(IsModal), true);

    public static readonly StyledProperty<IBrush?> ScrimBrushProperty =
        AvaloniaProperty.Register<ModalOverlay, IBrush?>(nameof(ScrimBrush));

    public static readonly StyledProperty<double> BackdropBlurProperty =
        AvaloniaProperty.Register<ModalOverlay, double>(nameof(BackdropBlur));

    public static readonly DirectProperty<ModalOverlay, object?> CloseResultProperty =
        AvaloniaProperty.RegisterDirect<ModalOverlay, object?>(nameof(CloseResult), o => o.CloseResult);

    /// <summary>Shown while true; set false (or call <see cref="Close"/>) to play the exit. Two-way by default.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Heading. Changing it while open crossfades (the new copy rises in with a soft blur).</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Secondary line under the title (also the panel's accessible help text).</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Footer content, usually buttons; give them <c>arc:Dialog.Result</c> to close with a result.</summary>
    public object? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    public bool ShowCloseButton { get => GetValue(ShowCloseButtonProperty); set => SetValue(ShowCloseButtonProperty, value); }

    /// <summary>Accessible name of the close button.</summary>
    public string CloseButtonName { get => GetValue(CloseButtonNameProperty); set => SetValue(CloseButtonNameProperty, value); }

    /// <summary>Escape closes (default true; cancellable through <see cref="Closing"/>).</summary>
    public bool CloseOnEscape { get => GetValue(CloseOnEscapeProperty); set => SetValue(CloseOnEscapeProperty, value); }

    /// <summary>A press on the scrim closes (default true; cancellable through <see cref="Closing"/>).</summary>
    public bool CloseOnOutsidePress { get => GetValue(CloseOnOutsidePressProperty); set => SetValue(CloseOnOutsidePressProperty, value); }

    /// <summary>
    /// Modal (default): scrim, backdrop blur and a focus trap. When false the page stays interactive and focus is free
    /// (Radix <c>modal={false}</c>).
    /// </summary>
    public bool IsModal { get => GetValue(IsModalProperty); set => SetValue(IsModalProperty, value); }

    /// <summary>Scrim brush (the theme sets the component's own scrim color).</summary>
    public IBrush? ScrimBrush { get => GetValue(ScrimBrushProperty); set => SetValue(ScrimBrushProperty, value); }

    /// <summary>Backdrop blur radius in px (the theme sets Arc's value; off under ReduceTransparency).</summary>
    public double BackdropBlur { get => GetValue(BackdropBlurProperty); set => SetValue(BackdropBlurProperty, value); }

    private object? _closeResult;

    /// <summary>The result of the last close (from <c>arc:Dialog.Result</c> buttons, <see cref="Close"/> or <see cref="CloseCommand"/>).</summary>
    public object? CloseResult
    {
        get => _closeResult;
        private set => SetAndRaise(CloseResultProperty, ref _closeResult, value);
    }

    /// <summary>Why the last close happened.</summary>
    public DismissReason LastCloseReason { get; private set; }

    #endregion

    #region Events and commands

    /// <summary>Raised when the overlay opens.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised before a close requested by the user or by <see cref="Close"/>; cancel to keep it open.</summary>
    public event EventHandler<OverlayClosingEventArgs>? Closing;

    /// <summary>Raised when the overlay closes (the exit may still be playing).</summary>
    public event EventHandler<OverlayClosedEventArgs>? Closed;

    /// <summary>Opens the overlay.</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Closes the overlay; the command parameter becomes the <see cref="CloseResult"/>.</summary>
    public ICommand CloseCommand { get; }

    #endregion

    private ModalSurface? _surface;
    private ModalPresence? _presence;
    private DismissReason _pendingReason = DismissReason.Programmatic;
    private double _pendingVelocity;
    private bool _pendingOpen;
    private bool _codeParented;
    private Visual? _codeOwner;
    private TaskCompletionSource<object?>? _showTask;

    static ModalOverlay()
    {
        IsVisibleProperty.OverrideDefaultValue<ModalOverlay>(false);
        FocusableProperty.OverrideDefaultValue<ModalOverlay>(false);
    }

    protected ModalOverlay()
    {
        OpenCommand = new OverlayCommand(_ => SetCurrentValue(IsOpenProperty, true));
        CloseCommand = new OverlayCommand(Close);
    }

    /// <summary>The panel shown in the overlay layer (created on first open).</summary>
    public ModalSurface? Surface => _surface;

    /// <summary>The modal presence (created on first open).</summary>
    public ModalPresence? Presence => _presence;

    /// <summary>Creates this overlay's panel.</summary>
    protected abstract ModalSurface CreateSurface();

    /// <summary>Configures the presence once it exists (entrance, side, initial focus, custom motion).</summary>
    protected virtual void ConfigurePresence(ModalPresence presence)
    {
    }

    /// <summary>Called right before the presence opens (update per-open state here).</summary>
    protected virtual void OnOpening()
    {
    }

    /// <summary>Called when the presence finished its exit and was removed.</summary>
    protected virtual void OnExited()
    {
    }

    #region Open / close API

    /// <summary>Opens the overlay.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the overlay with <paramref name="result"/> (raises the cancellable <see cref="Closing"/>).</summary>
    public void Close(object? result = null) => RequestClose(result is null ? DismissReason.Programmatic : DismissReason.Action, result);

    void IOverlayResultTarget.CloseWithResult(object? result) => RequestClose(DismissReason.Action, result);

    /// <summary>
    /// Opens the overlay and completes with its <see cref="CloseResult"/> when it closes. A control that is not in a window
    /// (built in code) uses <paramref name="owner"/> to find the window and joins its logical tree while open.
    /// </summary>
    public Task<object?> ShowAsync(Visual? owner = null)
    {
        _showTask?.TrySetResult(CloseResult);
        _showTask = new TaskCompletionSource<object?>();
        if (owner is not null && TopLevel.GetTopLevel(this) is null)
        {
            _codeOwner = owner;
            if (Parent is null && owner is StyledElement logicalOwner)
            {
                ((ISetLogicalParent)this).SetParent(logicalOwner);
                _codeParented = true;
            }
        }
        CloseResult = null;
        SetCurrentValue(IsOpenProperty, true);
        return _showTask.Task;
    }

    /// <summary>
    /// Requests a close for <paramref name="reason"/>: raises <see cref="Closing"/> and closes unless cancelled.
    /// <paramref name="velocity"/> (px/s toward the panel's edge) lets a fling keep its speed.
    /// </summary>
    protected bool RequestClose(DismissReason reason, object? result, double velocity = 0)
    {
        if (!IsOpen)
        {
            return false;
        }
        var args = new OverlayClosingEventArgs(reason, result);
        Closing?.Invoke(this, args);
        if (args.Cancel)
        {
            return false;
        }
        CloseResult = result;
        _pendingReason = reason;
        _pendingVelocity = velocity;
        SetCurrentValue(IsOpenProperty, false);
        return true;
    }

    #endregion

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
        else if (change.Property == ActionsProperty)
        {
            if (change.OldValue is ILogical { LogicalParent: var parent } oldLogical && ReferenceEquals(parent, this))
            {
                LogicalChildren.Remove(oldLogical);
            }
            if (change.NewValue is ILogical { LogicalParent: null } newLogical)
            {
                LogicalChildren.Add(newLogical);
            }
        }
        else if (_presence is not null && (change.Property == ScrimBrushProperty || change.Property == BackdropBlurProperty ||
                                           change.Property == CloseOnEscapeProperty || change.Property == CloseOnOutsidePressProperty ||
                                           change.Property == IsModalProperty))
        {
            ApplyPresenceSettings(_presence);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_pendingOpen && IsOpen)
        {
            OpenCore();
        }
    }

    private void EnsureSurface()
    {
        if (_surface is not null)
        {
            return;
        }
        _surface = CreateSurface();
        _surface.Bind(ModalSurface.TitleProperty, this.GetObservable(TitleProperty));
        _surface.Bind(ModalSurface.DescriptionProperty, this.GetObservable(DescriptionProperty));
        _surface.Bind(ModalSurface.ActionsProperty, this.GetObservable(ActionsProperty));
        _surface.Bind(ModalSurface.ShowCloseButtonProperty, this.GetObservable(ShowCloseButtonProperty));
        _surface.Bind(ModalSurface.CloseButtonNameProperty, this.GetObservable(CloseButtonNameProperty));
        _surface.Bind(ContentProperty, this.GetObservable(ContentProperty));
        _surface.Bind(ContentTemplateProperty, this.GetObservable(ContentTemplateProperty));
        _surface.CloseRequested += (_, _) => RequestClose(DismissReason.CloseButton, null);
        LogicalChildren.Add(_surface);
        _presence = new ModalPresence(_surface, this) { ContainmentRoot = this };
        _presence.Dismissing += (_, e) =>
        {
            // The overlay owns the decision: raise Closing and update IsOpen (two-way bindings see every close).
            e.Cancel = true;
            RequestClose(e.Reason, null);
        };
        _presence.Closed += OnPresenceClosed;
        ConfigurePresence(_presence);
    }

    private void ApplyPresenceSettings(ModalPresence presence)
    {
        presence.ScrimBrush = ScrimBrush;
        presence.BackdropBlur = IsModal ? BackdropBlur : 0;
        presence.DismissOnEscape = CloseOnEscape;
        presence.DismissOnScrimPress = CloseOnOutsidePress;
        presence.TrapFocus = IsModal;
        presence.Host.Scrim.IsVisible = IsModal;
    }

    private void OpenCore()
    {
        EnsureSurface();
        var presence = _presence!;
        presence.Anchor = TopLevel.GetTopLevel(this) is not null ? this : _codeOwner;
        ApplyPresenceSettings(presence);
        OnOpening();
        _pendingReason = DismissReason.Programmatic;
        _pendingVelocity = 0;
        if (!presence.Open())
        {
            if (_codeOwner is not null)
            {
                // Built in code and its owner cannot host a modal (no window or no overlay layer): give up.
                SetCurrentValue(IsOpenProperty, false);
                return;
            }
            // Not in a window yet: open once attached.
            _pendingOpen = true;
            return;
        }
        _pendingOpen = false;
        Opened?.Invoke(this, EventArgs.Empty);
    }

    private void CloseCore()
    {
        _pendingOpen = false;
        var reason = _pendingReason;
        var velocity = _pendingVelocity;
        _pendingReason = DismissReason.Programmatic;
        _pendingVelocity = 0;
        LastCloseReason = reason;
        _presence?.Close(reason, velocity);
        Closed?.Invoke(this, new OverlayClosedEventArgs(reason, CloseResult));
        var task = _showTask;
        _showTask = null;
        task?.TrySetResult(CloseResult);
        if (_presence is null || _presence.State == ModalState.Closed)
        {
            ReleaseCodeOwner();
        }
    }

    private void OnPresenceClosed(object? sender, ModalClosedEventArgs e)
    {
        if (IsOpen)
        {
            // Removed without a close request (the owner left the window).
            _pendingReason = e.Reason;
            SetCurrentValue(IsOpenProperty, false);
        }
        OnExited();
        ReleaseCodeOwner();
    }

    private void ReleaseCodeOwner()
    {
        if (IsOpen)
        {
            return;
        }
        if (_codeParented)
        {
            _codeParented = false;
            ((ISetLogicalParent)this).SetParent(null);
        }
        _codeOwner = null;
    }

    /// <summary>Shared default for string content: wrapping body copy.</summary>
    internal static readonly IDataTemplate TextTemplate = new OverlayTextTemplate();
}

/// <summary>Minimal command used by overlay controls.</summary>
internal sealed class OverlayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public OverlayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);
}
