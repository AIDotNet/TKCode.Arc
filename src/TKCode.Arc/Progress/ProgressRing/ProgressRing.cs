using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using AvaloniaRangeBase = Avalonia.Controls.Primitives.RangeBase;

namespace TKCode.Arc;

/// <summary>
/// A circular progress and loading indicator. In determinate mode (<see cref="IsIndeterminate"/> is false, the
/// default), one smooth spring drives both the clockwise arc and the optional center percentage, turning to the
/// success color with a check at 100%. In indeterminate mode (<see cref="IsIndeterminate"/> is true), a smooth arc
/// orbits continuously under full motion and breathes softly under reduced motion.
/// </summary>
[TemplatePart(TrackPart, typeof(ProgressRingTrack))]
[TemplatePart(ReadoutPart, typeof(ProgressRingReadout))]
[PseudoClasses(IndeterminatePseudoClass, CompletePseudoClass, SmallPseudoClass, LargePseudoClass)]
public class ProgressRing : AvaloniaRangeBase
{
    public const string TrackPart = "PART_Track";
    public const string ReadoutPart = "PART_Readout";

    /// <summary>Set while <see cref="IsIndeterminate"/> is true.</summary>
    public const string IndeterminatePseudoClass = ":indeterminate";

    /// <summary>Set while determinate <see cref="Percentage"/> reaches 100.</summary>
    public const string CompletePseudoClass = ":complete";

    /// <summary>Set while <see cref="Size"/> is <see cref="ControlSize.Small"/>.</summary>
    public const string SmallPseudoClass = ":small";

    /// <summary>Set while <see cref="Size"/> is <see cref="ControlSize.Large"/>.</summary>
    public const string LargePseudoClass = ":large";

    /// <summary>When true, displays a continuous spinning loading ring instead of determinate progress.</summary>
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsIndeterminate));

    /// <summary>Preset ring size: <see cref="ControlSize.Small"/>, <see cref="ControlSize.Medium"/> (default), or <see cref="ControlSize.Large"/>.</summary>
    public static readonly StyledProperty<ControlSize> SizeProperty =
        AvaloniaProperty.Register<ProgressRing, ControlSize>(nameof(Size), ControlSize.Medium);

    /// <summary>Stroke thickness of the ring track and indicator arc.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(Thickness), 3);

    /// <summary>Optional visible label shown below the ring; also serves as the accessible name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ProgressRing, string?>(nameof(Label));

    /// <summary>Shows the counted percentage (and completion check at 100%) centered inside the ring in determinate mode.</summary>
    public static readonly StyledProperty<bool> IsValueVisibleProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsValueVisible));

    /// <summary>Optional custom content centered inside the ring; takes precedence over the built-in percentage readout.</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<ProgressRing, object?>(nameof(Content));

    /// <summary>Data template used to display <see cref="Content"/> inside the ring.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<ProgressRing, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>Whole percentage of the range clamped to 0–100.</summary>
    public static readonly DirectProperty<ProgressRing, int> PercentageProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, int>(nameof(Percentage), o => o.Percentage);

    /// <summary>True when determinate <see cref="Percentage"/> reaches 100.</summary>
    public static readonly DirectProperty<ProgressRing, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, bool>(nameof(IsComplete), o => o.IsComplete);

    internal static readonly DirectProperty<ProgressRing, bool> HasLabelProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, bool>(nameof(HasLabel), o => o.HasLabel);

    internal static readonly DirectProperty<ProgressRing, bool> HasCustomContentProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, bool>(nameof(HasCustomContent), o => o.HasCustomContent);

    internal static readonly DirectProperty<ProgressRing, bool> ShowsReadoutProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, bool>(nameof(ShowsReadout), o => o.ShowsReadout);

    /// <inheritdoc cref="IsIndeterminateProperty"/>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public ControlSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <inheritdoc cref="ThicknessProperty"/>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsValueVisibleProperty"/>
    public bool IsValueVisible
    {
        get => GetValue(IsValueVisibleProperty);
        set => SetValue(IsValueVisibleProperty, value);
    }

    /// <inheritdoc cref="ContentProperty"/>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>Whole percentage of the range clamped to 0–100.</summary>
    public int Percentage
    {
        get;
        private set => SetAndRaise(PercentageProperty, ref field, value);
    }

    private bool _isComplete;

    /// <summary>True when determinate <see cref="Percentage"/> reaches 100.</summary>
    public bool IsComplete
    {
        get => _isComplete;
        private set => SetAndRaise(IsCompleteProperty, ref _isComplete, value);
    }

    private bool _hasLabel;

    internal bool HasLabel
    {
        get => _hasLabel;
        private set => SetAndRaise(HasLabelProperty, ref _hasLabel, value);
    }

    private bool _hasCustomContent;

    internal bool HasCustomContent
    {
        get => _hasCustomContent;
        private set => SetAndRaise(HasCustomContentProperty, ref _hasCustomContent, value);
    }

    private bool _showsReadout;

    internal bool ShowsReadout
    {
        get => _showsReadout;
        private set => SetAndRaise(ShowsReadoutProperty, ref _showsReadout, value);
    }

    private ProgressRingTrack? _track;
    private ProgressRingReadout? _readout;
    private MotionValue<double>? _shown;

    static ProgressRing()
    {
        MaximumProperty.OverrideDefaultValue<ProgressRing>(100);
        FocusableProperty.OverrideDefaultValue<ProgressRing>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<ProgressRing>(AutomationControlType.ProgressBar);
    }

    public ProgressRing()
    {
        UpdateState(animate: false);
        UpdateSizePseudoClasses();
        UpdateCenterFlags();
        UpdateAutomation();
    }

    /// <summary>Percentage currently drawn (follows the spring).</summary>
    internal double ShownPercentage => _shown?.Value ?? Percentage;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _track = e.NameScope.Find<ProgressRingTrack>(TrackPart);
        _readout = e.NameScope.Find<ProgressRingReadout>(ReadoutPart);
        _shown?.Jump(Percentage);
        Apply(ShownPercentage);
        if (_track is not null)
        {
            _track.IsIndeterminate = IsIndeterminate;
            _track.IsComplete = IsComplete;
        }
        if (_readout is not null)
        {
            _readout.IsComplete = IsComplete;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            UpdateState(animate: !IsIndeterminate);
        }
        else if (change.Property == IsIndeterminateProperty)
        {
            PseudoClasses.Set(IndeterminatePseudoClass, IsIndeterminate);
            if (_track is not null)
            {
                _track.IsIndeterminate = IsIndeterminate;
            }
            UpdateState(animate: false);
            UpdateCenterFlags();
            UpdateAutomation();
        }
        else if (change.Property == SizeProperty)
        {
            UpdateSizePseudoClasses();
        }
        else if (change.Property == LabelProperty)
        {
            HasLabel = !string.IsNullOrEmpty(Label);
            UpdateAutomation();
        }
        else if (change.Property == IsValueVisibleProperty || change.Property == ContentProperty)
        {
            UpdateCenterFlags();
        }
        else if (change.Property == IsCompleteProperty)
        {
            PseudoClasses.Set(CompletePseudoClass, IsComplete);
            if (_track is not null)
            {
                _track.IsComplete = IsComplete;
            }
            if (_readout is not null)
            {
                _readout.IsComplete = IsComplete;
            }
        }
    }

    /// <summary>Computes the whole percentage (0–100) for a value in [<paramref name="minimum"/>, <paramref name="maximum"/>].</summary>
    public static int ComputePercentage(double value, double minimum, double maximum)
    {
        var min = double.IsNaN(minimum) ? 0 : minimum;
        var max = double.IsNaN(maximum) || maximum <= min ? min + 100 : maximum;
        var clamped = Math.Clamp(double.IsNaN(value) ? min : value, min, max);
        return (int)Math.Round((clamped - min) / (max - min) * 100, MidpointRounding.AwayFromZero);
    }

    private void UpdateState(bool animate)
    {
        var percentage = ComputePercentage(Value, Minimum, Maximum);
        var complete = !IsIndeterminate && percentage >= 100;
        if (_shown is not null && percentage == Percentage && complete == IsComplete)
        {
            return;
        }
        Percentage = percentage;
        IsComplete = complete;
        UpdateAutomation();
        if (_shown is null)
        {
            _shown = new MotionValue<double>(this, percentage, MotionVectors.Double);
            _shown.Changed += Apply;
            Apply(percentage);
            return;
        }
        if (animate && IsLoaded)
        {
            _ = _shown.AnimateTo(percentage, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _shown.Jump(percentage);
        }
    }

    private void Apply(double percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        if (_track is not null)
        {
            _track.Fraction = clamped / 100.0;
        }
        if (_readout is not null)
        {
            _readout.Count = clamped;
        }
    }

    private void UpdateSizePseudoClasses()
    {
        PseudoClasses.Set(SmallPseudoClass, Size == ControlSize.Small);
        PseudoClasses.Set(LargePseudoClass, Size == ControlSize.Large);
    }

    private void UpdateCenterFlags()
    {
        HasCustomContent = Content is not null;
        ShowsReadout = !IsIndeterminate && IsValueVisible && !HasCustomContent;
    }

    private void UpdateAutomation()
    {
        var defaultName = IsIndeterminate ? "Loading" : "Progress";
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Label) ? defaultName : Label);
        AutomationProperties.SetItemStatus(this, IsIndeterminate ? "Loading" : $"{Percentage}%");
    }
}
