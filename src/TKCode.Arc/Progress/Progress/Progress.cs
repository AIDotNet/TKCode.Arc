using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using AvaloniaRangeBase = Avalonia.Controls.Primitives.RangeBase;

namespace TKCode.Arc;

/// <summary>
/// Shows how much of a known task is complete (Arc Progress). One smooth spring drives both the fill and the
/// counted percentage, so the number always matches the bar; the fill slides in from the left (keeping its round
/// end) and turns to the success color at 100%, where a check settles in beside the count. <c>Minimum</c>
/// is treated as 0 like Arc; a non-positive <see cref="AvaloniaRangeBase.Maximum"/> counts as 100.
/// </summary>
[TemplatePart(TrackPart, typeof(ProgressTrack))]
[TemplatePart(ReadoutPart, typeof(ProgressReadout))]
[PseudoClasses(CompletePseudoClass)]
public class Progress : AvaloniaRangeBase
{
    public const string TrackPart = "PART_Track";
    public const string ReadoutPart = "PART_Readout";
    public const string CompletePseudoClass = ":complete";

    /// <summary>Visible label above the bar; it is also the accessible name (defaults to "Progress").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Progress, string?>(nameof(Label));

    /// <summary>Shows the counted percentage and, at 100%, a check (Arc <c>showValue</c>).</summary>
    public static readonly StyledProperty<bool> IsValueVisibleProperty =
        AvaloniaProperty.Register<Progress, bool>(nameof(IsValueVisible));

    /// <summary>Defines the <see cref="Percentage"/> property.</summary>
    public static readonly DirectProperty<Progress, int> PercentageProperty =
        AvaloniaProperty.RegisterDirect<Progress, int>(nameof(Percentage), o => o.Percentage);

    /// <summary>Defines the <see cref="IsComplete"/> property.</summary>
    public static readonly DirectProperty<Progress, bool> IsCompleteProperty =
        AvaloniaProperty.RegisterDirect<Progress, bool>(nameof(IsComplete), o => o.IsComplete);

    internal static readonly DirectProperty<Progress, bool> HasMetaProperty =
        AvaloniaProperty.RegisterDirect<Progress, bool>(nameof(HasMeta), o => o.HasMeta);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsValueVisible { get => GetValue(IsValueVisibleProperty); set => SetValue(IsValueVisibleProperty, value); }

    private int _percentage;

    /// <summary>round(Value / Maximum × 100), clamped to 0–100.</summary>
    public int Percentage
    {
        get => _percentage;
        private set => SetAndRaise(PercentageProperty, ref _percentage, value);
    }

    private bool _isComplete;

    /// <summary>True when the percentage reaches 100.</summary>
    public bool IsComplete
    {
        get => _isComplete;
        private set => SetAndRaise(IsCompleteProperty, ref _isComplete, value);
    }

    private bool _hasMeta;

    internal bool HasMeta
    {
        get => _hasMeta;
        private set => SetAndRaise(HasMetaProperty, ref _hasMeta, value);
    }

    private ProgressTrack? _track;
    private ProgressReadout? _readout;
    private MotionValue<double>? _shown;

    static Progress()
    {
        MaximumProperty.OverrideDefaultValue<Progress>(100);
        FocusableProperty.OverrideDefaultValue<Progress>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Progress>(AutomationControlType.ProgressBar);
    }

    public Progress()
    {
        UpdateState(animate: false);
        UpdateAutomation();
    }

    /// <summary>Percentage currently drawn (follows the spring).</summary>
    internal double ShownPercentage => _shown?.Value ?? Percentage;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _track = e.NameScope.Find<ProgressTrack>(TrackPart);
        _readout = e.NameScope.Find<ProgressReadout>(ReadoutPart);
        _shown?.Jump(Percentage);
        Apply(ShownPercentage);
        if (_track is not null)
        {
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
        if (change.Property == ValueProperty || change.Property == MaximumProperty)
        {
            UpdateState(animate: true);
        }
        else if (change.Property == LabelProperty || change.Property == IsValueVisibleProperty)
        {
            HasMeta = !string.IsNullOrEmpty(Label) || IsValueVisible;
            UpdateAutomation();
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

    /// <summary>Arc's percentage rule: round(clamp(value, 0, max) / max × 100) with max ≤ 0 treated as 100.</summary>
    public static int ComputePercentage(double value, double maximum)
    {
        var max = maximum > 0 ? maximum : 100;
        var clamped = Math.Clamp(double.IsNaN(value) ? 0 : value, 0, max);
        return (int)Math.Round(clamped / max * 100, MidpointRounding.AwayFromZero);
    }

    private void UpdateState(bool animate)
    {
        var percentage = ComputePercentage(Value, Maximum);
        Percentage = percentage;
        IsComplete = percentage >= 100;
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
            _track.Fraction = clamped / 100;
        }
        if (_readout is not null)
        {
            _readout.Count = clamped;
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Label) ? "Progress" : Label);
        AutomationProperties.SetItemStatus(this, $"{Percentage}%");
    }
}
