using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class ChargeSession
{
    /// <summary>A limit change counts as settled this long after the last change (measured 350–368 ms).</summary>
    internal static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(350);

    private readonly ChargeSessionModel _model = new();
    private readonly ChargeSessionFlow _flow = new();
    private readonly LiveLoop _loop;
    private bool _initialized;
    private bool _applyingLimit;
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en-US");

    private Border? _card;
    private ChargeSessionHero? _hero;
    private ChargeSessionScene? _scene;
    private ChargeSessionLimitHandle? _handle;
    private ChargeSessionPlot? _plot;
    private ChargeSessionActionButton? _action;
    private ExplainerAnnouncer? _announcer;
    private TextBlock? _chargeText;
    private TextBlock? _statusText;
    private ArcGlyph? _statusIcon;
    private TextBlock? _curveTitle;
    private TextBlock? _curveReading;
    private TextBlock? _powerAxis;
    private TextBlock? _powerName;
    private TextBlock? _powerValue;
    private TextBlock? _energyName;
    private TextBlock? _energyValue;
    private TextBlock? _timeName;
    private TextBlock? _timeValue;
    private TextBlock? _costName;
    private TextBlock? _costValue;
    private TextBlock? _hint;
    private TextBlock? _axis0;
    private TextBlock? _axis50;
    private TextBlock? _axis100;
    private DragTracker? _heroTouch;
    private DragTracker? _plotTouch;

    // What the texts show, so a frame only writes what changed.
    private long _shownCharge = long.MinValue;
    private long _shownPower = long.MinValue;
    private long _shownEnergy = long.MinValue;
    private decimal _shownCost = decimal.MinValue;
    private long _shownTime = long.MinValue;
    private long _shownLimit = long.MinValue;
    private long _shownValueText = long.MinValue;
    private ChargeSessionState? _shownState;

    private Control? _dragSurface;
    private IPointer? _dragPointer;
    private Rect _dragRect;
    private Point _dragOrigin;
    private ITimer? _settleTimer;
    private int _settleToken;

    public ChargeSession()
    {
        // One loop advances the session and the packets: their state is a function of frame time, which a
        // ControlTheme transition cannot express. It idles once the flow has drained, offscreen and below full motion.
        _loop = new LiveLoop(this, StepFrame) { MinimumLevel = ArcMotionLevel.Full };
        PseudoClasses.Set(ChargingPseudoClass, true);
    }

    /// <summary>Stops charging: the power drains, the last packets land and the session waits for <see cref="Resume"/>.</summary>
    public void Stop() => Apply(_model.Stop(!IsMotionFull));

    /// <summary>Resumes a stopped session (or completes it when the limit is already reached).</summary>
    public void Resume() => Apply(_model.Resume(!IsMotionFull));

    /// <summary>Starts a new session from <see cref="StartCharge"/>: energy and cost back to zero, the limit kept.</summary>
    public void StartNewSession()
    {
        if (!_initialized)
        {
            Initialize();
            return;
        }
        SyncModel();
        Apply(_model.StartNew(!IsMotionFull));
    }

    /// <summary>Advances the simulation by <paramref name="seconds"/> as frames would (tests and the snapshot tool).</summary>
    internal void Advance(double seconds, double frame = 1.0 / 60)
    {
        while (seconds > 1e-9)
        {
            var dt = Math.Min(frame, seconds);
            seconds -= dt;
            StepFrame(dt);
        }
    }

    internal ChargeSessionModel Model => _model;

    internal ChargeSessionFlow Flow => _flow;

    internal LiveLoop Loop => _loop;

    internal ExplainerAnnouncer? Announcer => _announcer;

    private bool IsMotionFull => ArcMotion.IsFull(this);

    private double CoerceLimit(double value)
    {
        var min = Math.Clamp(Math.Round(MinLimit), 0, 100);
        return double.IsFinite(value) ? Math.Clamp(Math.Round(value), min, 100) : min;
    }

    #region Simulation

    private bool StepFrame(double dt)
    {
        if (IsPaused || !_initialized)
        {
            return false;
        }
        var previous = _model.State;
        var transition = _model.Step(dt, Speed);
        _flow.Step(dt, _model.Availability, ChargeSessionScene.PathEnd(_model.Charge));
        if (transition != ChargeSessionTransition.None)
        {
            OnTransition(transition, previous);
        }
        Refresh();
        return _model.IsMoving || !_flow.IsIdle;
    }

    private void Initialize()
    {
        _initialized = true;
        SyncModel();
        _model.Reset(StartCharge, Limit, instant: !IsMotionFull);
        _flow.Clear();
        UpdateState();
        EnsureRunning();
        Refresh();
    }

    private void SyncModel()
    {
        _model.Capacity = Capacity;
        _model.MaxPower = MaxPower;
        _model.StartCharge = StartCharge;
        _model.MinLimit = MinLimit;
        _model.TaperAt = TaperAt;
        _model.Curve = PowerCurve;
    }

    /// <summary>Starts the loop when there is anything to move; below full motion shows the resting frame instead.</summary>
    private void EnsureRunning()
    {
        if (!_initialized)
        {
            return;
        }
        if (!IsMotionFull)
        {
            // Reduced motion holds the session still: availability at its target, packets evenly spaced while charging.
            _model.Settle();
            _flow.LayOut(ChargeSessionScene.PathEnd(_model.Charge), _model.State == ChargeSessionState.Charging);
            Refresh();
            // Start only records the wish to run: the loop's MinimumLevel is Full, so it stays parked (no frames) below
            // full motion and resumes by itself if the level goes back to Full.
            if (!IsPaused)
            {
                _loop.Start();
            }
            return;
        }
        if (!IsPaused && (_model.IsMoving || !_flow.IsIdle || _flow.IsStatic))
        {
            _loop.Start();
        }
        else
        {
            _loop.Stop();
        }
    }

    private void Apply(ChargeSessionTransition transition)
    {
        if (!_initialized || transition == ChargeSessionTransition.None)
        {
            return;
        }
        // The previous state is whatever the card showed before the call.
        OnTransition(transition, _state);
        EnsureRunning();
        Refresh();
    }

    private void OnTransition(ChargeSessionTransition transition, ChargeSessionState previous)
    {
        UpdateState();
        var labels = Labels;
        var detail = Detail();
        var charge = labels.Percent(ChargeSessionFormat.ChargeNumber(_model.Charge, _culture));
        var limit = labels.Percent(ChargeSessionFormat.Whole(_model.Limit, _culture));
        var energy = labels.Energy(ChargeSessionFormat.OneDecimal(_model.EnergyAdded, _culture));
        var cost = ChargeSessionFormat.Money(detail.Cost, Currency, _culture);
        var text = transition switch
        {
            ChargeSessionTransition.Stopped => labels.StoppedAnnouncement(charge, energy, cost),
            ChargeSessionTransition.Resumed => labels.ResumedAnnouncement(limit),
            ChargeSessionTransition.Completed => labels.CompleteAnnouncement(charge, energy, cost),
            ChargeSessionTransition.NewSession => labels.NewSessionAnnouncement(
                labels.Percent(ChargeSessionFormat.Whole(_model.StartCharge, _culture)), limit),
            _ => null,
        };
        _announcer?.Announce(text);
        RaiseEvent(new ChargeSessionStateChangedEventArgs(StateChangedEvent, _model.State, previous,
            transition == ChargeSessionTransition.NewSession, detail));
    }

    private ChargeSessionDetail Detail() =>
        new(_model.Charge, _model.EnergyAdded, ChargeSessionFormat.Cost(_model.EnergyAdded, Rate, Currency), _model.Limit);

    private void UpdateState()
    {
        State = _model.State;
        PseudoClasses.Set(ChargingPseudoClass, _state == ChargeSessionState.Charging);
        PseudoClasses.Set(StoppedPseudoClass, _state == ChargeSessionState.Stopped);
        PseudoClasses.Set(CompletePseudoClass, _state == ChargeSessionState.Complete);
        if (_action is not null)
        {
            _action.State = _state;
        }
        UpdateStatusInk();
    }

    #endregion

    #region Limit

    private void OnLimitChanged(double limit)
    {
        if (_hero is not null)
        {
            _hero.Limit = limit;
        }
        if (!_initialized || _applyingLimit)
        {
            return;
        }
        _applyingLimit = true;
        try
        {
            var previous = _state;
            var transition = _model.SetLimit(limit, !IsMotionFull);
            if (transition != ChargeSessionTransition.None)
            {
                OnTransition(transition, previous);
                EnsureRunning();
            }
            ScheduleSettle();
            Refresh();
        }
        finally
        {
            _applyingLimit = false;
        }
    }

    private void ScheduleSettle()
    {
        _settleTimer?.Dispose();
        var token = ++_settleToken;
        _settleTimer = TimeProvider.CreateTimer(_ => Dispatcher.UIThread.Post(() => Settle(token)), null, SettleDelay,
            Timeout.InfiniteTimeSpan);
    }

    private void Settle(int token)
    {
        if (token != _settleToken)
        {
            return;
        }
        _settleTimer?.Dispose();
        _settleTimer = null;
        var labels = Labels;
        var limit = labels.Percent(ChargeSessionFormat.Whole(_model.Limit, _culture));
        var remaining = _model.Limit > _model.Charge ? labels.Remaining(_model.MinutesToLimit) : null;
        _announcer?.Announce(labels.LimitAnnouncement(limit, remaining));
        RaiseEvent(new ChargeSessionLimitChangedEventArgs(LimitChangedEvent, _model.Limit));
    }

    private void MoveLimitTo(double x)
    {
        if (_dragSurface is null || _dragRect.Width <= 0)
        {
            return;
        }
        var limit = _dragSurface == _plot
            ? (x - _dragRect.X) / _dragRect.Width * 100
            : ChargeSessionScene.LimitAt((x - _dragRect.X) / _dragRect.Width * ChargeSessionScene.UnitWidth);
        SetCurrentValue(LimitProperty, limit);
    }

    private void BeginDrag(Control surface, IPointer? pointer)
    {
        _dragSurface = surface;
        _dragPointer = pointer;
        // The target rectangle is read once per press.
        _dragRect = surface == _hero && _hero is not null ? _hero.SceneRect : new Rect(surface.Bounds.Size);
        _dragOrigin = TopLevel.GetTopLevel(this) is { } top ? surface.TranslatePoint(default, top) ?? default : default;
        SetHeld(true);
    }

    private void EndDrag()
    {
        if (_dragSurface is null)
        {
            return;
        }
        _dragSurface = null;
        _dragPointer = null;
        SetHeld(false);
    }

    private void SetHeld(bool held)
    {
        if (_handle is not null)
        {
            _handle.IsHeld = held;
        }
        _scene?.SetHeld(held);
    }

    private void OnSurfacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control surface || e.Pointer.Type == PointerType.Touch || _dragSurface is not null)
        {
            return;
        }
        if (!e.GetCurrentPoint(surface).Properties.IsLeftButtonPressed)
        {
            return;
        }
        // A press on the limit marker itself moves the limit too, and leaves focus where it was.
        e.Pointer.Capture(surface);
        BeginDrag(surface, e.Pointer);
        MoveLimitTo(e.GetPosition(surface).X);
        e.Handled = true;
    }

    private void OnSurfaceMoved(object? sender, PointerEventArgs e)
    {
        if (_dragSurface is not null && e.Pointer == _dragPointer && e.Pointer.Type != PointerType.Touch)
        {
            MoveLimitTo(e.GetPosition(_dragSurface).X);
        }
    }

    private void OnSurfaceReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer == _dragPointer && e.Pointer.Type != PointerType.Touch)
        {
            e.Pointer.Capture(null);
            EndDrag();
        }
    }

    private void OnSurfaceCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer == _dragPointer && e.Pointer.Type != PointerType.Touch)
        {
            EndDrag();
        }
    }

    private DragTracker CreateTouch(Control surface)
    {
        // Touch follows horizontal drags only and lets a vertical swipe scroll the page (CSS touch-action: pan-y).
        var tracker = new DragTracker(surface, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 4 })
        {
            CanBegin = e => e.Pointer.Type == PointerType.Touch && _dragSurface is null,
        };
        tracker.Pressed += (_, a) =>
        {
            BeginDrag(surface, null);
            MoveLimitTo(a.Position.X - _dragOrigin.X);
        };
        tracker.Moved += (_, a) => MoveLimitTo(a.Position.X - _dragOrigin.X);
        tracker.Completed += (_, _) => EndDrag();
        tracker.Cancelled += (_, _) => EndDrag();
        tracker.Released += (_, _) => EndDrag();
        return tracker;
    }

    private void OnHandleValueChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty && !_syncingHandle && e.NewValue is double value)
        {
            SetCurrentValue(LimitProperty, value);
        }
    }

    private bool _syncingHandle;

    #endregion

    #region Template and layout

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _card = e.NameScope.Find<Border>(CardPart);
        _hero = e.NameScope.Find<ChargeSessionHero>(HeroPart);
        _scene = e.NameScope.Find<ChargeSessionScene>(ScenePart);
        _handle = e.NameScope.Find<ChargeSessionLimitHandle>(HandlePart);
        _plot = e.NameScope.Find<ChargeSessionPlot>(PlotPart);
        _action = e.NameScope.Find<ChargeSessionActionButton>(ActionPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        _chargeText = e.NameScope.Find<TextBlock>("PART_ChargeText");
        _statusText = e.NameScope.Find<TextBlock>("PART_StatusText");
        _statusIcon = e.NameScope.Find<ArcGlyph>("PART_StatusIcon");
        _curveTitle = e.NameScope.Find<TextBlock>("PART_CurveTitle");
        _curveReading = e.NameScope.Find<TextBlock>("PART_CurveReading");
        _powerAxis = e.NameScope.Find<TextBlock>("PART_PowerAxis");
        _powerName = e.NameScope.Find<TextBlock>("PART_PowerName");
        _powerValue = e.NameScope.Find<TextBlock>("PART_PowerValue");
        _energyName = e.NameScope.Find<TextBlock>("PART_EnergyName");
        _energyValue = e.NameScope.Find<TextBlock>("PART_EnergyValue");
        _timeName = e.NameScope.Find<TextBlock>("PART_TimeName");
        _timeValue = e.NameScope.Find<TextBlock>("PART_TimeValue");
        _costName = e.NameScope.Find<TextBlock>("PART_CostName");
        _costValue = e.NameScope.Find<TextBlock>("PART_CostValue");
        _hint = e.NameScope.Find<TextBlock>("PART_Hint");
        _axis0 = e.NameScope.Find<TextBlock>("PART_Axis0");
        _axis50 = e.NameScope.Find<TextBlock>("PART_Axis50");
        _axis100 = e.NameScope.Find<TextBlock>("PART_Axis100");
        _scene?.Attach(_model, _flow);
        _plot?.Attach(_model);
        if (_hero is not null)
        {
            _hero.Limit = Limit;
        }
        if (_action is not null)
        {
            _action.State = _state;
        }
        Hook();
        UpdateStaticTexts();
        InvalidateTexts();
        UpdateStatusInk();
        UpdateBreakpoints(Bounds.Width);
        Refresh();
    }

    private void Hook()
    {
        foreach (var surface in Surfaces())
        {
            surface.PointerPressed += OnSurfacePressed;
            surface.PointerMoved += OnSurfaceMoved;
            surface.PointerReleased += OnSurfaceReleased;
            surface.PointerCaptureLost += OnSurfaceCaptureLost;
        }
        if (_hero is not null)
        {
            _heroTouch = CreateTouch(_hero);
        }
        if (_plot is not null)
        {
            _plotTouch = CreateTouch(_plot);
        }
        if (_handle is not null)
        {
            _handle.PropertyChanged += OnHandleValueChanged;
        }
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
    }

    private void Unhook()
    {
        foreach (var surface in Surfaces())
        {
            surface.PointerPressed -= OnSurfacePressed;
            surface.PointerMoved -= OnSurfaceMoved;
            surface.PointerReleased -= OnSurfaceReleased;
            surface.PointerCaptureLost -= OnSurfaceCaptureLost;
        }
        _heroTouch?.Dispose();
        _plotTouch?.Dispose();
        _heroTouch = null;
        _plotTouch = null;
        if (_handle is not null)
        {
            _handle.PropertyChanged -= OnHandleValueChanged;
        }
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        EndDrag();
    }

    private IEnumerable<Control> Surfaces()
    {
        if (_hero is not null)
        {
            yield return _hero;
        }
        if (_plot is not null)
        {
            yield return _plot;
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        switch (_state)
        {
            case ChargeSessionState.Charging:
                Stop();
                break;
            case ChargeSessionState.Stopped:
                Resume();
                break;
            default:
                StartNewSession();
                break;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        // Breakpoints follow the card's own width (a container query in the reference), never the window.
        PseudoClasses.Set(StackedPseudoClass, width < SideBySideWidth);
        PseudoClasses.Set(CompactPseudoClass, width <= CompactWidth);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialized)
        {
            Initialize();
        }
        else
        {
            EnsureRunning();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EndDrag();
        _settleTimer?.Dispose();
        _settleTimer = null;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == LimitProperty)
        {
            OnLimitChanged(Limit);
        }
        else if (p == MinLimitProperty)
        {
            CoerceValue(LimitProperty);
            _model.MinLimit = MinLimit;
            InvalidateTexts();
            Refresh();
        }
        else if (p == CapacityProperty || p == MaxPowerProperty || p == StartChargeProperty || p == TaperAtProperty ||
                 p == PowerCurveProperty)
        {
            SyncModel();
            _plot?.InvalidateCurve();
            if (p == MaxPowerProperty)
            {
                UpdateStaticTexts();
            }
            InvalidateTexts();
            Refresh();
        }
        else if (p == RateProperty || p == CurrencyProperty || p == LocaleProperty || p == LabelsProperty)
        {
            _culture = ChargeSessionFormat.Culture(Locale);
            UpdateStaticTexts();
            InvalidateTexts();
            Refresh();
        }
        else if (p == IsPausedProperty)
        {
            EnsureRunning();
        }
        else if (p == AccentBrushProperty)
        {
            if (AccentBrush is { } accent)
            {
                SetValue(AccentInkProperty, accent);
            }
            else
            {
                ClearValue(AccentInkProperty);
            }
        }
        else if (p == AccentInkProperty || p == MutedInkProperty)
        {
            UpdateStatusInk();
        }
        else if (p == TitleProperty || p == AccessibleLabelProperty)
        {
            AutomationProperties.SetName(this, AccessibleLabel ?? Title ?? string.Empty);
        }
        else if (p == ArcMotion.LevelProperty)
        {
            EnsureRunning();
        }
    }

    #endregion

    #region Texts

    private void UpdateStatusInk()
    {
        var labels = Labels;
        SetText(_statusText, _state switch
        {
            ChargeSessionState.Stopped => labels.Stopped,
            ChargeSessionState.Complete => labels.Complete,
            _ => labels.Charging,
        });
        if (_statusIcon is not null)
        {
            _statusIcon.Data = _state switch
            {
                ChargeSessionState.Stopped => ArcIcons.Square,
                ChargeSessionState.Complete => ChargeSessionIcons.BatteryFull,
                _ => ArcIcons.Zap,
            };
            _statusIcon.Foreground = _state == ChargeSessionState.Charging ? AccentInk : MutedInk;
        }
    }

    private void UpdateStaticTexts()
    {
        var labels = Labels;
        _culture = ChargeSessionFormat.Culture(Locale);
        SetText(_curveTitle, labels.CurveTitle);
        SetText(_powerAxis, labels.Power(ChargeSessionFormat.Whole(MaxPower, _culture)));
        SetText(_powerName, labels.PowerStat);
        SetText(_energyName, labels.EnergyStat);
        SetText(_costName, labels.CostStat);
        SetText(_hint, labels.Hint);
        SetText(_axis0, labels.Percent(ChargeSessionFormat.Whole(0, _culture)));
        SetText(_axis50, labels.Percent(ChargeSessionFormat.Whole(50, _culture)));
        SetText(_axis100, labels.Percent(ChargeSessionFormat.Whole(100, _culture)));
        UpdateStatusInk();
        if (_curveTitle?.Parent is Control section)
        {
            AutomationProperties.SetName(section, labels.CurveTitle);
        }
        if (_handle is not null)
        {
            AutomationProperties.SetName(_handle, labels.Limit);
        }
        if (_action is not null)
        {
            _action.StopText = labels.Stop;
            _action.ResumeText = labels.Resume;
            _action.NewSessionText = labels.NewSession;
        }
        AutomationProperties.SetName(this, AccessibleLabel ?? Title ?? string.Empty);
    }

    private void InvalidateTexts()
    {
        _shownCharge = long.MinValue;
        _shownPower = long.MinValue;
        _shownEnergy = long.MinValue;
        _shownCost = decimal.MinValue;
        _shownTime = long.MinValue;
        _shownLimit = long.MinValue;
        _shownValueText = long.MinValue;
        _shownState = null;
    }

    /// <summary>Writes the figures that changed and redraws the scene and the curve.</summary>
    private void Refresh()
    {
        if (!_initialized)
        {
            return;
        }
        var labels = Labels;
        var model = _model;

        Charge = Math.Round(model.Charge, 1);
        var power = Math.Round(model.Power, MidpointRounding.AwayFromZero);
        Power = power;
        EnergyAdded = Math.Round(model.EnergyAdded, 1);
        var cost = ChargeSessionFormat.Cost(model.EnergyAdded, Rate, Currency);
        Cost = cost;

        var chargeKey = (long)Math.Floor(model.Charge + 1e-9);
        var powerKey = (long)power;
        if (chargeKey != _shownCharge || powerKey != _shownPower)
        {
            var percent = labels.Percent(ChargeSessionFormat.ChargeNumber(model.Charge, _culture));
            if (chargeKey != _shownCharge)
            {
                SetText(_chargeText, percent);
            }
            var powerText = labels.Power(ChargeSessionFormat.Whole(power, _culture));
            if (powerKey != _shownPower)
            {
                SetText(_powerValue, powerText);
            }
            SetText(_curveReading, labels.CurveReading(powerText, percent));
            _shownCharge = chargeKey;
            _shownPower = powerKey;
        }

        var energyKey = (long)Math.Round(model.EnergyAdded * 10, MidpointRounding.AwayFromZero);
        if (energyKey != _shownEnergy)
        {
            _shownEnergy = energyKey;
            SetText(_energyValue, labels.Energy(ChargeSessionFormat.OneDecimal(model.EnergyAdded, _culture)));
        }
        if (cost != _shownCost)
        {
            _shownCost = cost;
            SetText(_costValue, ChargeSessionFormat.Money(cost, Currency, _culture));
        }

        var limitKey = (long)model.Limit;
        if (limitKey != _shownLimit)
        {
            _shownLimit = limitKey;
            var limitText = labels.Percent(ChargeSessionFormat.Whole(model.Limit, _culture));
            SetText(_timeName, labels.TimeStat(limitText));
            if (_handle is not null)
            {
                _handle.Text = limitText;
                _syncingHandle = true;
                _handle.Minimum = Math.Clamp(Math.Round(MinLimit), 0, 100);
                _handle.Value = model.Limit;
                _syncingHandle = false;
            }
            _shownValueText = long.MinValue;
        }

        // Time to the limit: paused while stopped, done once reached, else minutes at full power.
        var minutes = model.MinutesToLimit;
        var reached = model.State == ChargeSessionState.Complete || model.Limit <= model.Charge;
        long timeKey = model.State == ChargeSessionState.Stopped ? -1 : reached ? -2 : minutes < 1 ? 0 : (long)Math.Round(minutes, MidpointRounding.AwayFromZero);
        MinutesToLimit = timeKey < 0 ? null : timeKey;
        if (timeKey != _shownTime || _shownState != model.State)
        {
            _shownTime = timeKey;
            _shownState = model.State;
            SetText(_timeValue, timeKey switch
            {
                -1 => labels.TimePaused,
                -2 => labels.TimeDone,
                _ => labels.Time(minutes),
            });
        }

        // The marker's value text follows the limit and the whole minutes left (the reference only rewrote it on a limit
        // change, so it went stale while charging).
        var valueKey = model.Limit > model.Charge ? (minutes < 1 ? 0 : (long)Math.Round(minutes, MidpointRounding.AwayFromZero)) : -1;
        if (valueKey != _shownValueText && _handle is not null)
        {
            _shownValueText = valueKey;
            var limitText = labels.Percent(ChargeSessionFormat.Whole(model.Limit, _culture));
            AutomationProperties.SetHelpText(_handle, labels.LimitValue(limitText, valueKey < 0 ? null : labels.Remaining(minutes)));
        }

        // Only the layers whose inputs moved are re-recorded.
        _scene?.Update();
        _plot?.Update();
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null && !string.Equals(block.Text, text, StringComparison.Ordinal))
        {
            block.Text = text;
        }
    }

    #endregion

    /// <summary>The card's peer: a group named by the title.</summary>
    private sealed class CardPeer(ChargeSession owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(ChargeSession);
    }
}
