using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A prize wheel: slices with their own odds, a spin that draws a winner by those odds and slows to a stop on it, pegs
/// that flick the pointer as they pass, and the winning slice lit once the wheel rests.
/// </summary>
/// <remarks>
/// The winner is decided when the spin starts (<see cref="Spin"/> draws it from the weights; <see cref="SpinTo"/> takes
/// one from your server), so the animation only shows a result that is already known. A click or tap on the wheel,
/// Enter or Space spins it. Under reduced motion the wheel turns straight to the winner and only the highlight fades in.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(FacePart, typeof(PrizeWheelFace))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[PseudoClasses(SpinningPseudoClass)]
public class PrizeWheel : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string FacePart = "PART_Face";
    public const string StatusPart = "PART_Status";
    public const string SpinningPseudoClass = ":spinning";

    private const double HighlightSeconds = 0.24;

    /// <summary>How long a spin takes from the first push to the stop.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<PrizeWheel, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(4.8));

    /// <summary>The least number of full turns a spin makes before it settles on the winner.</summary>
    public static readonly StyledProperty<int> TurnsProperty =
        AvaloniaProperty.Register<PrizeWheel, int>(nameof(Turns), 5);

    /// <summary>Whether slices are as wide as their odds (true) or all the same width.</summary>
    public static readonly StyledProperty<bool> SizesByWeightProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(SizesByWeight), true);

    /// <summary>The word in the hub, which spins the wheel.</summary>
    public static readonly StyledProperty<string> SpinTextProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(SpinText), "Spin");

    /// <summary>The accessible name of the wheel.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PrizeWheel, string>(nameof(Label), "Prize wheel");

    /// <summary>Whether a person can spin the wheel by clicking, tapping or pressing Enter or Space (true).</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<PrizeWheel, bool>(nameof(IsInteractive), true);

    /// <summary>The index of the slice the wheel last stopped on; -1 before the first spin ends.</summary>
    public static readonly DirectProperty<PrizeWheel, int> ResultIndexProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, int>(nameof(ResultIndex), o => o.ResultIndex);

    /// <summary>The slice the wheel last stopped on; null before the first spin ends.</summary>
    public static readonly DirectProperty<PrizeWheel, PrizeWheelSegment?> ResultProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, PrizeWheelSegment?>(nameof(Result), o => o.Result);

    /// <summary>Whether the wheel is turning.</summary>
    public static readonly DirectProperty<PrizeWheel, bool> IsSpinningProperty =
        AvaloniaProperty.RegisterDirect<PrizeWheel, bool>(nameof(IsSpinning), o => o.IsSpinning);

    /// <summary>Raised when the wheel comes to rest, with the winning slice.</summary>
    public static readonly RoutedEvent<PrizeWheelResultEventArgs> SpinCompletedEvent =
        RoutedEvent.Register<PrizeWheel, PrizeWheelResultEventArgs>(nameof(SpinCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised each time a peg flicks the pointer, for a click sound or a haptic tick.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> TickedEvent =
        RoutedEvent.Register<PrizeWheel, RoutedEventArgs>(nameof(Ticked), RoutingStrategies.Bubble);

    private readonly PrizeWheelModel _model = new();
    private readonly LiveLoop _loop;
    private LazyFocusRing? _focusRing;
    private PrizeWheelFace? _face;
    private TextBlock? _status;
    private bool _statusFlip;
    private bool _dirty = true;
    private int _resultIndex = -1;
    private PrizeWheelSegment? _result;
    private bool _isSpinning;
    private double _highlight;
    private bool _ownsName;

    static PrizeWheel()
    {
        FocusableProperty.OverrideDefaultValue<PrizeWheel>(true);
        ClipToBoundsProperty.OverrideDefaultValue<PrizeWheel>(false);
    }

    public PrizeWheel()
    {
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        Segments.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (PrizeWheelSegment segment in e.OldItems)
                {
                    segment.Changed -= OnSegmentChanged;
                }
            }
            if (e.NewItems is not null)
            {
                foreach (PrizeWheelSegment segment in e.NewItems)
                {
                    segment.Changed += OnSegmentChanged;
                }
            }
            SegmentsChanged();
        };
    }

    /// <summary>The slices, clockwise from the top.</summary>
    [Content]
    public AvaloniaList<PrizeWheelSegment> Segments { get; } = [];

    /// <inheritdoc cref="DurationProperty"/>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <inheritdoc cref="TurnsProperty"/>
    public int Turns
    {
        get => GetValue(TurnsProperty);
        set => SetValue(TurnsProperty, value);
    }

    /// <inheritdoc cref="SizesByWeightProperty"/>
    public bool SizesByWeight
    {
        get => GetValue(SizesByWeightProperty);
        set => SetValue(SizesByWeightProperty, value);
    }

    /// <inheritdoc cref="SpinTextProperty"/>
    public string SpinText
    {
        get => GetValue(SpinTextProperty);
        set => SetValue(SpinTextProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsInteractiveProperty"/>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <inheritdoc cref="ResultIndexProperty"/>
    public int ResultIndex
    {
        get => _resultIndex;
        private set => SetAndRaise(ResultIndexProperty, ref _resultIndex, value);
    }

    /// <inheritdoc cref="ResultProperty"/>
    public PrizeWheelSegment? Result
    {
        get => _result;
        private set => SetAndRaise(ResultProperty, ref _result, value);
    }

    /// <inheritdoc cref="IsSpinningProperty"/>
    public bool IsSpinning
    {
        get => _isSpinning;
        private set => SetAndRaise(IsSpinningProperty, ref _isSpinning, value);
    }

    /// <inheritdoc cref="SpinCompletedEvent"/>
    public event EventHandler<PrizeWheelResultEventArgs>? SpinCompleted
    {
        add => AddHandler(SpinCompletedEvent, value);
        remove => RemoveHandler(SpinCompletedEvent, value);
    }

    /// <inheritdoc cref="TickedEvent"/>
    public event EventHandler<RoutedEventArgs>? Ticked
    {
        add => AddHandler(TickedEvent, value);
        remove => RemoveHandler(TickedEvent, value);
    }

    /// <summary>The source of the draws, 0 (inclusive) to 1 (exclusive); <see cref="Random.Shared"/> by default.</summary>
    internal Func<double> Random { get; set; } = static () => System.Random.Shared.NextDouble();

    internal PrizeWheelModel Model
    {
        get
        {
            EnsureModel();
            return _model;
        }
    }

    /// <summary>How lit the winning slice is, 0 to 1.</summary>
    internal double Highlight => _highlight;

    /// <summary>
    /// Draws a winner by the slices' weights and spins the wheel to it. Returns the winner's index, or -1 when the wheel
    /// is already turning or has no slice that can win.
    /// </summary>
    public int Spin()
    {
        EnsureModel();
        if (_model.IsSpinning || _model.Count == 0)
        {
            return -1;
        }
        var index = _model.Pick(Random());
        if (index < 0)
        {
            return -1;
        }
        Start(index);
        return index;
    }

    /// <summary>Spins the wheel to a winner chosen elsewhere, such as by your server. Ignored while the wheel turns.</summary>
    public void SpinTo(int index)
    {
        EnsureModel();
        if (_model.IsSpinning || index < 0 || index >= _model.Count)
        {
            return;
        }
        Start(index);
    }

    /// <summary>Clears the result and the highlight; the wheel stays where it stopped.</summary>
    public void ClearResult()
    {
        ResultIndex = -1;
        Result = null;
        _highlight = 0;
        _face?.UpdateWinnerLabel();
        _face?.InvalidateVisual();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_face is not null)
        {
            _face.Owner = null;
        }
        _face = e.NameScope.Find<PrizeWheelFace>(FacePart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_face is not null)
        {
            _face.Owner = this;
            _face.Reset();
        }
        _focusRing ??= new LazyFocusRing(this, PrizeWheelTokens.Key(PrizeWheelTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SizesByWeightProperty)
        {
            SegmentsChanged();
        }
        else if (property == SpinTextProperty)
        {
            _face?.Reset();
        }
        else if (property == LabelProperty)
        {
            UpdateName();
        }
        else if (property == IsInteractiveProperty)
        {
            Focusable = IsInteractive;
        }
        else if (property == ArcMotion.LevelProperty && _model.IsSpinning && !ArcMotion.IsFull(this))
        {
            // Motion was reduced mid-spin: land at once.
            _model.Jump(_model.Target, Random());
            Finish();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && IsInteractive && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Spin();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsInteractive || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (_face is { } face && !face.IsOnWheel(e.GetPosition(face)))
        {
            return;
        }
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        Spin();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PrizeWheelAutomationPeer(this);

    private void Start(int index)
    {
        ResultIndex = -1;
        Result = null;
        _highlight = 0;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Full && VisualRoot is not null)
        {
            _model.Plan(index, Random(), Math.Max(1, Turns), Math.Max(0.3, Duration.TotalSeconds));
            IsSpinning = true;
            PseudoClasses.Set(SpinningPseudoClass, true);
            Announce(string.Empty);
            _loop.Start();
            _face?.InvalidateVisual();
            return;
        }
        _model.Jump(index, Random());
        Finish();
    }

    private bool Step(double dt)
    {
        EnsureModel();
        var wasSpinning = _model.IsSpinning;
        var ticks = _model.Step(dt);
        for (var i = 0; i < ticks; i++)
        {
            RaiseEvent(new RoutedEventArgs(TickedEvent, this));
        }
        if (wasSpinning && !_model.IsSpinning)
        {
            Finish();
        }
        if (!_model.IsSpinning && _resultIndex >= 0 && _highlight < 1)
        {
            _highlight = Math.Min(1, _highlight + dt / HighlightSeconds);
        }
        _face?.InvalidateVisual();
        return _model.IsSpinning || _model.IsFlapMoving || (_resultIndex >= 0 && _highlight < 1);
    }

    private void Finish()
    {
        var index = _model.Target;
        IsSpinning = false;
        PseudoClasses.Set(SpinningPseudoClass, false);
        if (index < 0 || index >= Segments.Count)
        {
            return;
        }
        ResultIndex = index;
        Result = Segments[index];
        _face?.UpdateWinnerLabel();
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || VisualRoot is null)
        {
            _highlight = 1;
        }
        else
        {
            _loop.Start();
        }
        _face?.InvalidateVisual();
        Announce(Segments[index].Label ?? string.Empty);
        RaiseEvent(new PrizeWheelResultEventArgs(SpinCompletedEvent, index, Segments[index]));
    }

    private void Announce(string message)
    {
        if (_status is null)
        {
            return;
        }
        // Alternate a trailing space so the same prize twice in a row is announced again.
        _statusFlip = !_statusFlip;
        _status.Text = message.Length == 0 ? string.Empty : _statusFlip ? message : message + " ";
    }

    private void OnSegmentChanged(object? sender, EventArgs e) => SegmentsChanged();

    private void SegmentsChanged()
    {
        _dirty = true;
        _face?.Reset();
    }

    private void EnsureModel()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        var weights = new double[Segments.Count];
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = Segments[i].Weight;
        }
        _model.Load(weights, SizesByWeight);
        if (_resultIndex >= Segments.Count)
        {
            ClearResult();
        }
    }

    private void UpdateName()
    {
        if (AutomationProperties.GetName(this) is not { Length: > 0 } || _ownsName)
        {
            _ownsName = true;
            AutomationProperties.SetName(this, Label);
        }
    }

    private sealed class PrizeWheelAutomationPeer(PrizeWheel owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetLocalizedControlTypeCore() => "prize wheel";

        protected override string? GetHelpTextCore() =>
            base.GetHelpTextCore() is { Length: > 0 } help ? help : owner.Result?.Label;

        public void Invoke()
        {
            if (owner.IsEffectivelyEnabled && owner.IsInteractive)
            {
                owner.Spin();
            }
        }
    }
}

/// <summary>The slice a <see cref="PrizeWheel"/> stopped on.</summary>
public sealed class PrizeWheelResultEventArgs : RoutedEventArgs
{
    public PrizeWheelResultEventArgs(RoutedEvent routedEvent, int index, PrizeWheelSegment segment) : base(routedEvent)
    {
        Index = index;
        Segment = segment;
    }

    /// <summary>The slice's position in <see cref="PrizeWheel.Segments"/>.</summary>
    public int Index { get; }

    /// <summary>The winning slice.</summary>
    public PrizeWheelSegment Segment { get; }
}
