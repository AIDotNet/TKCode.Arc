using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Fill color of a <see cref="HoldToConfirm"/>.</summary>
public enum HoldToConfirmTone
{
    /// <summary>Fills with the selected accent (Arc default).</summary>
    Accent,

    /// <summary>For an irreversible action: danger ink and fill.</summary>
    Danger,

    /// <summary>Fills with the foreground.</summary>
    Neutral
}

/// <summary>Arguments of <see cref="HoldToConfirm.HoldingChanged"/>.</summary>
public sealed class HoldToConfirmHoldingChangedEventArgs : EventArgs
{
    public HoldToConfirmHoldingChangedEventArgs(bool isHolding) => IsHolding = isHolding;

    /// <summary>True when a hold started, false when it ended (completed or let go).</summary>
    public bool IsHolding { get; }
}

/// <summary>
/// A button that commits only after it is held, for destructive or hard to undo actions where a stray tap must not
/// count. The fill tracks the hold on a linear, pausable timeline (P22) and is revealed from the leading edge with the
/// label inverted exactly at the edge (P29); letting go early rewinds it on a smooth spring, and finishing morphs the
/// label and icon into the done state (P2, P5, P15). Space and Enter can be held too; the hint is in the help text.
/// </summary>
[TemplatePart(FacePart, typeof(Control))]
[TemplatePart(FillPart, typeof(Control))]
[TemplatePart(FillFacePart, typeof(Control))]
[TemplatePart(StatusPart, typeof(Control))]
[PseudoClasses(HoldingPseudoClass, ConfirmedPseudoClass, WidePseudoClass)]
public class HoldToConfirm : AvaloniaButton
{
    public const string FacePart = "PART_Face";
    public const string FillPart = "PART_Fill";
    public const string FillFacePart = "PART_FillFace";
    public const string StatusPart = "PART_Status";
    public const string HoldingPseudoClass = ":holding";
    public const string ConfirmedPseudoClass = ":confirmed";
    public const string WidePseudoClass = ":wide";

    /// <summary>Pointer travel beyond the button that cancels a hold, like a native press.</summary>
    internal const double CancelSlack = 24;

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<HoldToConfirm, string>(nameof(Label), "Hold to delete");

    public static readonly StyledProperty<string> ConfirmedLabelProperty =
        AvaloniaProperty.Register<HoldToConfirm, string>(nameof(ConfirmedLabel), "Done");

    public static readonly StyledProperty<TimeSpan> HoldDurationProperty =
        AvaloniaProperty.Register<HoldToConfirm, TimeSpan>(nameof(HoldDuration), TimeSpan.FromMilliseconds(1200));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<HoldToConfirm, Geometry?>(nameof(Icon), ArcIcons.Trash2);

    public static readonly StyledProperty<HoldToConfirmTone> ToneProperty =
        AvaloniaProperty.Register<HoldToConfirm, HoldToConfirmTone>(nameof(Tone));

    public static readonly StyledProperty<bool> IsConfirmedProperty =
        AvaloniaProperty.Register<HoldToConfirm, bool>(nameof(IsConfirmed), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<HoldToConfirm, bool> IsHoldingProperty =
        AvaloniaProperty.RegisterDirect<HoldToConfirm, bool>(nameof(IsHolding), o => o.IsHolding);

    public static readonly RoutedEvent<RoutedEventArgs> ConfirmedEvent =
        RoutedEvent.Register<HoldToConfirm, RoutedEventArgs>(nameof(Confirmed), RoutingStrategies.Bubble);

    /// <summary>The instruction and the action, such as "Hold to delete project".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Shown once the hold completes (default "Done").</summary>
    public string ConfirmedLabel { get => GetValue(ConfirmedLabelProperty); set => SetValue(ConfirmedLabelProperty, value); }

    /// <summary>How long the button must be held (default 1.2s).</summary>
    public TimeSpan HoldDuration { get => GetValue(HoldDurationProperty); set => SetValue(HoldDurationProperty, value); }

    /// <summary>Leading 24×24 glyph (default Lucide trash-2); it becomes a drawn check when done.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public HoldToConfirmTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>The done state. Set it back to false to reset the button (the fill rewinds as the label morphs back).</summary>
    public bool IsConfirmed { get => GetValue(IsConfirmedProperty); set => SetValue(IsConfirmedProperty, value); }

    private bool _isHolding;

    /// <summary>True while a hold is in progress (for surrounding hints such as "Keep holding").</summary>
    public bool IsHolding
    {
        get => _isHolding;
        private set => SetAndRaise(IsHoldingProperty, ref _isHolding, value);
    }

    /// <summary>Raised once when a hold completes (after which <see cref="AvaloniaButton.Command"/> runs).</summary>
    public event EventHandler<RoutedEventArgs>? Confirmed
    {
        add => AddHandler(ConfirmedEvent, value);
        remove => RemoveHandler(ConfirmedEvent, value);
    }

    /// <summary>Raised when a hold starts and when it ends.</summary>
    public event EventHandler<HoldToConfirmHoldingChangedEventArgs>? HoldingChanged;

    #endregion

    private enum HoldSource
    {
        None,
        Pointer,
        Key
    }

    private HoldToConfirmFace? _face;
    private HoldToConfirmFace? _fillFace;
    private Control? _fill;
    private ButtonsLiveRegion? _status;
    private readonly RectangleGeometry _fillClip = new();
    private MotionTimeline? _progress;
    private HoldSource _source;
    private Key _heldKey;
    private bool _completing;
    private WindowBase? _window;

    private static readonly SpringSpec Rewind = ArcMotionPresets.Smooth;

    /// <summary>P22 hold fill: the Core timeline runs while held (at every motion level) and rewinds on a spring.</summary>
    private MotionTimeline Progress
    {
        get
        {
            if (_progress is null)
            {
                _progress = new MotionTimeline(this);
                _progress.Completed += Complete;
                _progress.Changed += _ => UpdateFill();
            }
            return _progress;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _face = e.NameScope.Find<HoldToConfirmFace>(FacePart);
        _fillFace = e.NameScope.Find<HoldToConfirmFace>(FillFacePart);
        _fill = e.NameScope.Find<Control>(FillPart);
        _status = e.NameScope.Find<ButtonsLiveRegion>(StatusPart);
        if (_fill is not null)
        {
            _fill.Clip = _fillClip;
        }
        if (IsConfirmed)
        {
            Progress.Jump(1);
        }
        UpdateFace(animate: false);
        UpdateFill();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateAutomation();
    }

    private string CurrentText => IsConfirmed ? ConfirmedLabel : Label;

    private void UpdateFace(bool animate)
    {
        _face?.Show(CurrentText, Icon, IsConfirmed, animate);
        _fillFace?.Show(CurrentText, Icon, IsConfirmed, animate);
    }

    private void UpdateFill()
    {
        var value = _progress?.Value ?? 0;
        if (_fill is null)
        {
            return;
        }
        _fill.IsVisible = value > 0.0005;
        _fillClip.Rect = new Rect(0, 0, Bounds.Width * Math.Clamp(value, 0, 1), Bounds.Height);
        // A clip geometry changed in place reaches the compositor only when its visual is invalidated.
        _fill.InvalidateVisual();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, CurrentText);
        var seconds = HoldDuration.TotalSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        AutomationProperties.SetHelpText(this, IsConfirmed
            ? string.Empty
            : $"Press and hold for {seconds} seconds to confirm. With a keyboard, hold Space or Enter.");
    }

    /// <summary>Starts or resumes the fill from wherever it is, so a quick re-press carries on.</summary>
    private void Begin(HoldSource source)
    {
        if (IsConfirmed || !IsEffectivelyEnabled || _source != HoldSource.None)
        {
            return;
        }
        _source = source;
        SetHolding(true);
        Progress.Start(HoldDuration, Progress.Value);
    }

    /// <summary>Lets go: rewinds unless the hold just completed.</summary>
    private void Release()
    {
        if (_source == HoldSource.None)
        {
            return;
        }
        _source = HoldSource.None;
        SetHolding(false);
        // A hold that just completed is no longer active and keeps its full fill.
        if (Progress.IsActive)
        {
            Progress.Rewind(Rewind);
        }
    }

    /// <summary>Cancels a hold in progress (for example when the window loses activation).</summary>
    public void CancelHold() => Release();

    private void SetHolding(bool holding)
    {
        if (IsHolding == holding)
        {
            return;
        }
        IsHolding = holding;
        PseudoClasses.Set(HoldingPseudoClass, holding);
        HoldingChanged?.Invoke(this, new HoldToConfirmHoldingChangedEventArgs(holding));
    }

    private void Complete()
    {
        if (_source == HoldSource.None)
        {
            return;
        }
        _source = HoldSource.None;
        SetHolding(false);
        SetCurrentValue(IsConfirmedProperty, true);
        _status?.Announce(ConfirmedLabel);
        RaiseEvent(new RoutedEventArgs(ConfirmedEvent));
        _completing = true;
        try
        {
            base.OnClick();
        }
        finally
        {
            _completing = false;
        }
    }

    protected override void OnClick()
    {
        // Only a completed hold commits; plain clicks (and automation invokes) do nothing.
        if (_completing)
        {
            base.OnClick();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            // Auto-repeat while held arrives as more key downs: only the first one starts the fill.
            if (_source == HoldSource.None)
            {
                _heldKey = e.Key;
                Begin(HoldSource.Key);
            }
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            if (_source == HoldSource.Key && e.Key == _heldKey)
            {
                Release();
            }
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || !e.Pointer.IsPrimary)
        {
            return;
        }
        e.Handled = true;
        if (Focusable && !IsFocused)
        {
            Focus(NavigationMethod.Pointer);
        }
        e.Pointer.Capture(this);
        Begin(HoldSource.Pointer);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_source != HoldSource.Pointer)
        {
            return;
        }
        // Sliding well off the button cancels, the way a native press does.
        var p = e.GetPosition(this);
        if (p.X < -CancelSlack || p.Y < -CancelSlack || p.X > Bounds.Width + CancelSlack || p.Y > Bounds.Height + CancelSlack)
        {
            Release();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_source == HoldSource.Pointer)
        {
            e.Handled = true;
            Release();
        }
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_source == HoldSource.Pointer)
        {
            Release();
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Release();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as WindowBase;
        if (_window is not null)
        {
            // Key-up can be lost when the window deactivates mid-hold.
            _window.Deactivated += OnWindowDeactivated;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null)
        {
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }
        if (_source != HoldSource.None)
        {
            _source = HoldSource.None;
            SetHolding(false);
            _progress?.Jump(IsConfirmed ? 1 : 0);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => Release();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsConfirmedProperty)
        {
            var confirmed = change.GetNewValue<bool>();
            PseudoClasses.Set(ConfirmedPseudoClass, confirmed);
            UpdateFace(animate: IsLoaded);
            UpdateAutomation();
            // A reset rewinds the fill as the label morphs back; a confirmation set from outside fills it.
            if (_source == HoldSource.None && _progress is not null)
            {
                Progress.SettleTo(confirmed ? 1 : 0, Rewind);
            }
            else if (_source == HoldSource.None && confirmed)
            {
                Progress.Jump(1);
            }
            if (!confirmed)
            {
                _status?.Announce(string.Empty);
            }
        }
        else if (change.Property == LabelProperty || change.Property == ConfirmedLabelProperty || change.Property == IconProperty)
        {
            UpdateFace(animate: IsLoaded);
            UpdateAutomation();
        }
        else if (change.Property == HoldDurationProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == BoundsProperty)
        {
            PseudoClasses.Set(WidePseudoClass, Bounds.Width > 220);
            UpdateFill();
        }
        else if (change.Property == IsEnabledProperty && !change.GetNewValue<bool>())
        {
            Release();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HoldToConfirmAutomationPeer(this);

    private sealed class HoldToConfirmAutomationPeer : ButtonAutomationPeer
    {
        public HoldToConfirmAutomationPeer(HoldToConfirm owner) : base(owner)
        {
        }

        // Done reports aria-disabled while the button keeps focus.
        protected override bool IsEnabledCore() => base.IsEnabledCore() && !((HoldToConfirm)Owner).IsConfirmed;
    }
}
